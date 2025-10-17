using KeyLockerSync.Data;
using KeyLockerSync.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Configuration;

namespace KeyLockerSync.Services
{

    public class SyncService
    {
        private readonly DatabaseHelper _db;
        private readonly ApiService _api;
        private readonly Dictionary<string, ActionMapping> _mappings;

        public SyncService(DatabaseHelper db, ApiService api)
        {
            _db = db;
            _api = api;

            _mappings = new Dictionary<string, ActionMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["DEVICE"] = new ActionMapping
                {
                    GetDataFunc = (dbh, objectId) => dbh.GetDeviceDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendDeviceAsync(obj, method)
                },
                ["KEYGROUP"] = new ActionMapping
                {
                    GetDataFunc = (dbh, objectId) => dbh.GetKeyGroupDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendKeyGroupAsync(obj, method)
                },

                ["USER"] = new ActionMapping
                {
                    // Używamy tych samych metod co dla 'PERSON'
                    GetDataFunc = (dbh, objectId) => dbh.GetPersonDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendPersonAsync(obj, method)
                },

                ["KEY"] = new ActionMapping
                {
                    GetDataFunc = (dbh, objectId) => dbh.GetKeyDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.UpdateKeyNameAsync(obj, method)
                },
                
                ["KEYUSER"] = new ActionMapping
                {
                    //GetDataFunc = (dbh, objectId) => dbh.GetKeyUserDataAsync(objectId),
                    GetDataFunc = null, // Nie używamy już tej funkcji
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignKeyAsync(obj, method)
                },

                ["KEYGROUPKEY"] = new ActionMapping
                {
                    GetDataFunc = null, // Data comes directly from the audit record.
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignKeyInGroupAsync(obj, method)
                },
                
                ["KEYGROUPUSER"] = new ActionMapping
                {
                    GetDataFunc = null, // Data is built directly from the audit record.
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignPersonInGroupAsync(obj, method)
                },

                ["RESERVATION"] = new ActionMapping
                {
                    GetDataFunc = (dbh, objectId) => dbh.GetReservationDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendReservationAsync(obj, method)
                },

                ["CREDENTIAL"] = new ActionMapping
                {
                    GetDataFunc = null, // Obsługiwane ręcznie
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendCredentialAsync(obj, method)
                }

            };
        }

        public async Task SyncAsync()
        {
            var audits = _db.GetPendingAuditRecords();

            foreach (var audit in audits)
            {
                if (!_mappings.TryGetValue(audit.Object_Type ?? string.Empty, out var mapping))
                {
                    Console.WriteLine($"[WARN] Brak mapowania dla Object_Type: '{audit.Object_Type}'");
                    continue;
                }

                object data = null;
                try
                {
                    string objectTypeUpper = audit.Object_Type?.ToUpper();
                    string actionTypeUpper = audit.Action_Type?.ToUpper();

                    if (objectTypeUpper == "KEYGROUPKEY")
                    {
                        // For this type, Object_ID is the groupIdApi and Additional_ID is the keyIdExt.
                        if (string.IsNullOrEmpty(audit.Object_ID) || string.IsNullOrEmpty(audit.Additional_ID))
                        {
                            Console.WriteLine($"[ERROR] Nieprawidłowe dane dla KeyGroupKey w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'.");
                            continue;
                        }
                        data = new KeyGroupKey
                        {
                            GroupIdApi = audit.Object_ID,
                            KeyIdExts = new List<string> { audit.Additional_ID } // Create a list with the single key
                        };
                    }

                    else if (objectTypeUpper == "KEYGROUPUSER")
                    {
                        // For this type, Object_ID is the groupIdApi and Additional_ID is the ownerIdApi.
                        if (string.IsNullOrEmpty(audit.Object_ID) || string.IsNullOrEmpty(audit.Additional_ID))
                        {
                            Console.WriteLine($"[ERROR] Nieprawidłowe dane dla KeyGroupUser w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'.");
                            continue;
                        }
                        data = new KeyGroupUser
                        {
                            GroupIdApi = audit.Object_ID,
                            OwnerIdApis = new List<string> { audit.Additional_ID } // Create a list with the single person ID
                        };
                    }

                    else if (objectTypeUpper == "KEYUSER")
                    {
                        if (string.IsNullOrEmpty(audit.Additional_ID) || string.IsNullOrEmpty(audit.Object_ID))
                        {
                            Console.WriteLine($"[ERROR] Nieprawidłowy format danych dla KeyUser w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'.");
                            continue;
                        }
                        data = new KeyUser
                        {
                            OwnerIdApi = audit.Additional_ID,
                            // Object_ID jest teraz traktowany jako keyIdExt i umieszczany w liście
                            KeyIdExts = new List<string> { audit.Object_ID }
                        };
                    }
                    else if (objectTypeUpper == "CREDENTIAL")
                    {
                        if (actionTypeUpper == "DELETE")
                        {
                            // Dla DELETE tworzymy obiekt bezpośrednio z audytu
                            data = new CredentialData
                            {
                                Method = audit.Additional_ID, // method = Additional_ID
                                Credential = audit.Object_ID   // credential = Object_ID
                            };
                        }
                        else // Dla INSERT i UPDATE
                        {
                            // Dla INSERT pobieramy pełne dane z procedury
                            data = await _db.GetCredentialDataAsync(audit.Object_ID);
                        }
                    }
                    else
                    {
                        data = await mapping.GetDataFunc(_db, audit.Object_ID);
                    }

                    if (data == null)
                    {
                        Console.WriteLine($"[WARN] Brak danych dla {audit.Object_Type} ID={audit.Object_ID}. Audyt mógł być przestarzały.");
                        _db.MarkAuditAsWarning(audit.ID);
                        continue;
                    }

                    HttpMethod method = actionTypeUpper switch
                    {
                        "CREATE" => HttpMethod.Post,
                        "INSERT" => HttpMethod.Post,
                        "UPDATE" => HttpMethod.Put,
                        "DELETE" => HttpMethod.Delete,
                        _ => HttpMethod.Post
                    };

                    bool success = await mapping.SendDataFunc(_api, data, method);

                    if (success)
                    {
                        _db.MarkAuditProcessed(audit.ID);
                        Console.WriteLine($"[INFO] Sukces: Przetworzono audyt dla {audit.Object_Type} ID={audit.Object_ID}, Akcja={audit.Action_Type}");
                    }
                    else
                    {
                        _db.MarkAuditAsWarning(audit.ID);
                        Console.WriteLine($"[ERROR] Nie udało się wysłać danych dla Object_Type: {audit.Object_Type}, Object_ID={audit.Object_ID}, Akcja={audit.Action_Type}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Krytyczny błąd synchronizacji dla Object_Type: {audit.Object_Type}, Object_ID={audit.Object_ID}: {ex.Message}");
                    _db.MarkAuditAsWarning(audit.ID);
                }
            }
        }
    }
}


