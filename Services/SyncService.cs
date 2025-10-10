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
                /*
                ["KEYUSER"] = new ActionMapping
                {
                    GetDataFunc = (dbh, objectId) => dbh.GetKeyUserDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignKeyAsync(obj, method)
                }*/
                ["KEYUSER"] = new ActionMapping
                {
                    GetDataFunc = null, // Nie używamy już tej funkcji
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignKeyAsync(obj, method)
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
                    // *** KLUCZOWA ZMIANA: Specjalna obsługa dla KEYUSER ***
                    if (audit.Object_Type?.ToUpper() == "KEYUSER")
                    {
                        // Tworzymy obiekt KeyUser bezpośrednio z danych audytu
                        if (string.IsNullOrEmpty(audit.Additional_ID) || !int.TryParse(audit.Object_ID, out int keyId))
                        {
                            Console.WriteLine($"[ERROR] Nieprawidłowy format danych dla KeyUser w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'.");
                            continue; // Pomiń ten wadliwy rekord
                        }
                        data = new KeyUser
                        {
                            OwnerIdApi = audit.Additional_ID,
                            KeyIds = new List<int> { keyId }
                        };
                    }
                    else
                    {
                        // Standardowa ścieżka dla wszystkich innych typów obiektów
                        data = await mapping.GetDataFunc(_db, audit.Object_ID);
                    }

                    if (data == null)
                    {
                        Console.WriteLine($"[WARN] Brak danych dla {audit.Object_Type} ID={audit.Object_ID}. Audyt mógł być przestarzały.");
                        continue;
                    }

                    HttpMethod method = (audit.Action_Type?.ToUpper()) switch
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
                        Console.WriteLine($"[ERROR] Nie udało się wysłać danych dla {audit.Object_Type} ID={audit.Object_ID}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Krytyczny błąd synchronizacji dla {audit.Object_Type} ID={audit.Object_ID}: {ex.Message}");
                }
            }
        }
    }
}