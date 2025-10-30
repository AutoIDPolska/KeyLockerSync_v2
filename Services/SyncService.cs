using KeyLockerSync.Data;
using KeyLockerSync.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace KeyLockerSync.Services
{

    public class SyncService
    {
        private readonly DatabaseHelper _db;
        private readonly ApiService _api;
        private readonly Dictionary<string, ActionMapping> _mappings;
        private readonly int _maxRetryCount;

        public SyncService(DatabaseHelper db, ApiService api)
        {
            _db = db;
            _api = api;

            if (!int.TryParse(ConfigurationManager.AppSettings["MaxRetryCount"], out _maxRetryCount))
            {
                _maxRetryCount = 3;
                Console.WriteLine($"[WARN] Brak lub niepoprawny klucz 'MaxRetryCount' w app.config. Ustawiono domyślną wartość: {_maxRetryCount}");
            }


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
                    // Dla DELETE GetDataFunc zostanie pominięta w logice SyncAsync
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
                    GetDataFunc = null, // bezpośrednio z audit
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignKeyAsync(obj, method)
                },

                ["KEYGROUPKEY"] = new ActionMapping
                {
                    GetDataFunc = null, // bezpośrednio z audit
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignKeyInGroupAsync(obj, method)
                },

                ["KEYGROUPUSER"] = new ActionMapping
                {
                    GetDataFunc = null, // bezpośrednio z audit
                    SendDataFunc = (apisvc, obj, method) => apisvc.AssignOrUnassignPersonInGroupAsync(obj, method)
                },

                ["RESERVATION"] = new ActionMapping
                {
                    GetDataFunc = (dbh, objectId) => dbh.GetReservationDataAsync(objectId),
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendReservationAsync(obj, method)
                },

                ["CREDENTIAL"] = new ActionMapping
                {
                    GetDataFunc = null, // bezpośrednio z audit
                    SendDataFunc = (apisvc, obj, method) => apisvc.SendCredentialAsync(obj, method)
                }

            };
        }

        public async Task SyncAsync()
        {
            Console.WriteLine("\n--- Rozpoczynam przetwarzanie nowych zadań (Status 0) ---");
            var pendingAudits = _db.GetPendingAuditRecords();
            await ProcessAuditBatch(pendingAudits);

            Console.WriteLine("\n--- Rozpoczynam ponowne przetwarzanie zadań z ostrzeżeniami (Status 2) ---");
            var warningAudits = _db.GetWarningAuditRecords(); // Zakładamy, że ta metoda istnieje w DatabaseHelper
            await ProcessAuditBatch(warningAudits);

            Console.WriteLine("\n--- Zakończono cykl synchronizacji audytu ---");
        }

        
        private async Task ProcessAuditBatch(List<AuditRecord> audits)
        {
            foreach (var audit in audits)
            {
                
                if (audit.Status == 2 && audit.RetryCount >= _maxRetryCount)
                {
                    Console.WriteLine($"[INFO] Audyt ID={audit.ID} osiągnął maksymalną liczbę prób ({_maxRetryCount}). Oznaczam jako ostatecznie nieudany (Status 3).");
                    _db.MarkAuditAsFailed(audit.ID);
                    continue;
                }

                if (!_mappings.TryGetValue(audit.Object_Type ?? string.Empty, out var mapping))
                {
                    Console.WriteLine($"[WARN] Brak mapowania dla Object_Type: '{audit.Object_Type}'. Audyt ID={audit.ID}");
                    _db.MarkAuditAsWarning(audit.ID);
                    continue;
                }

                object data = null;
                bool skipDataFetch = false;
                try
                {
                    string objectTypeUpper = audit.Object_Type?.ToUpper();
                    string actionTypeUpper = audit.Action_Type?.ToUpper();

                    if ((objectTypeUpper == "USER" || objectTypeUpper == "PERSON") && actionTypeUpper == "DELETE")
                    {
                        data = new Person { OwnerIdApi = audit.Object_ID };
                        skipDataFetch = true;
                        Console.WriteLine($"[INFO] Przygotowuję operację DELETE dla użytkownika {audit.Object_ID} bez pobierania dodatkowych danych.");
                    }
                    else if (objectTypeUpper == "KEYGROUPKEY")
                    {
                        if (string.IsNullOrEmpty(audit.Object_ID) || string.IsNullOrEmpty(audit.Additional_ID))
                        {
                            Console.WriteLine($"[ERROR] Nieprawidłowe dane dla KeyGroupKey w audycie: Object_ID(keyIdExt)='{audit.Object_ID}', Additional_ID(groupIdApi)='{audit.Additional_ID}'.");
                            _db.MarkAuditAsWarning(audit.ID);
                            continue;
                        }
                        data = new KeyGroupKey
                        {
                            GroupIdApi = audit.Additional_ID,
                            KeyIdExts = new List<string> { audit.Object_ID }
                        };
                        skipDataFetch = true;
                    }
                    /*{
                        if (string.IsNullOrEmpty(audit.Object_ID) || string.IsNullOrEmpty(audit.Additional_ID)) { Console.WriteLine($"[ERROR] Nieprawidłowe dane dla KeyGroupKey w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'."); continue; }
                        data = new KeyGroupKey { GroupIdApi = audit.Object_ID, KeyIdExts = new List<string> { audit.Additional_ID } };
                        skipDataFetch = true;
                    }
                    else if (objectTypeUpper == "KEYGROUPUSER")
                    {
                        if (string.IsNullOrEmpty(audit.Object_ID) || string.IsNullOrEmpty(audit.Additional_ID)) { Console.WriteLine($"[ERROR] Nieprawidłowe dane dla KeyGroupUser w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'."); continue; }
                        data = new KeyGroupUser { GroupIdApi = audit.Object_ID, OwnerIdApis = new List<string> { audit.Additional_ID } };
                        skipDataFetch = true;
                    }*/
                    else if (objectTypeUpper == "KEYGROUPUSER")
                    {                        
                        if (string.IsNullOrEmpty(audit.Additional_ID) || string.IsNullOrEmpty(audit.Object_ID))
                        {
                            Console.WriteLine($"[ERROR] Nieprawidłowe dane dla KeyGroupUser w audycie: Additional_ID(groupIdApi)='{audit.Additional_ID}', Object_ID(ownerIdApi)='{audit.Object_ID}'.");
                            _db.MarkAuditAsWarning(audit.ID); // Oznacz jako błąd
                            continue;
                        }
                        data = new KeyGroupUser
                        {                            
                            GroupIdApi = audit.Additional_ID,                            
                            OwnerIdApis = new List<string> { audit.Object_ID }
                        };
                        skipDataFetch = true;
                    }
                    else if (objectTypeUpper == "KEYUSER")
                    {
                        if (string.IsNullOrEmpty(audit.Additional_ID) || string.IsNullOrEmpty(audit.Object_ID)) { Console.WriteLine($"[ERROR] Nieprawidłowy format danych dla KeyUser w audycie: Object_ID='{audit.Object_ID}', Additional_ID='{audit.Additional_ID}'."); continue; }
                        data = new KeyUser { OwnerIdApi = audit.Additional_ID, KeyIdExts = new List<string> { audit.Object_ID } };
                        skipDataFetch = true;
                    }
                    else if (objectTypeUpper == "CREDENTIAL")
                    {
                        if (actionTypeUpper == "DELETE")
                        {
                            data = new CredentialData { Method = audit.Additional_ID, Credential = audit.Object_ID };
                            skipDataFetch = true;
                        }
                        else if (actionTypeUpper == "INSERT" || actionTypeUpper == "UPDATE")
                        {
                            // Wywołujemy GetCredentialDataAsync bezpośrednio, nie ustawiamy skipDataFetch
                            data = await _db.GetCredentialDataAsync(audit.Object_ID);
                            skipDataFetch = true;
                        }
                    }

                    if (!skipDataFetch)
                    {
                        if (mapping.GetDataFunc == null)
                        {
                            Console.WriteLine($"[ERROR] Brak zdefiniowanej metody GetDataFunc dla typu {audit.Object_Type} (Action: {actionTypeUpper}).");
                            _db.MarkAuditAsWarning(audit.ID);
                            continue;
                        }
                        data = await mapping.GetDataFunc(_db, audit.Object_ID);
                    }

                    if (data == null && !skipDataFetch) // dla DELETE
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

                    Console.WriteLine($"[INFO] Próbuję przetworzyć Audyt ID={audit.ID} (Typ: {audit.Object_Type}, Akcja: {audit.Action_Type}, Próba: {audit.RetryCount + 1}/{_maxRetryCount})");
                    bool success = await mapping.SendDataFunc(_api, data, method);

                    if (success)
                    {
                        _db.MarkAuditProcessed(audit.ID);
                        Console.WriteLine($"[INFO] Sukces: Audyt ID={audit.ID} przetworzony pomyślnie.");
                    }
                    else
                    {
                        _db.MarkAuditAsWarning(audit.ID);
                        Console.WriteLine($"[ERROR] Błąd przetwarzania Audytu ID={audit.ID}. Oznaczono statusem '2'.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Krytyczny błąd synchronizacji dla Audytu ID={audit.ID}: {ex.Message}");
                    _db.MarkAuditAsWarning(audit.ID);
                }
            }
        }
    }
}