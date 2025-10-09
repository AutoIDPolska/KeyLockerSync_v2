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
                    // 🔹 SendDataFunc pozostawione do ogólnych akcji
                    SendDataFunc = (apisvc, obj, method) =>
                    {
                        // 🔹 Dla UPDATE używamy UpdateDeviceNameAsync
                        if (method == HttpMethod.Put && obj is Device device)
                        {
                            return apisvc.UpdateDeviceNameAsync(device.Gid, device.Name);
                        }
                        // 🔹 Inne akcje (POST/DELETE) wywołują standardowe SendDeviceAsync
                        return apisvc.SendDeviceAsync(obj, method);
                    },
                    RetryCount = 3,
                    RetryDelay = TimeSpan.FromSeconds(5)
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
                    Console.WriteLine($"Brak mapowania dla Object_Type: {audit.Object_Type}");
                    continue;
                }

                object data = null;
                try
                {
                    data = await mapping.GetDataFunc(_db, audit.Object_ID);
                    if (data == null)
                    {
                        Console.WriteLine($"Brak danych dla {audit.Object_Type} ID={audit.Object_ID}");
                        continue;
                    }

                    // 🔹 Metoda HTTP na podstawie Action_Type
                    HttpMethod method = audit.Action_Type?.ToUpper() switch
                    {
                        "CREATE" => HttpMethod.Post,
                        "INSERT" => HttpMethod.Post,
                        "DELETE" => HttpMethod.Delete,
                        "UPDATE" => HttpMethod.Put,
                        _ => HttpMethod.Post
                    };

                    bool success = false;
                    
                    while (!success)
                    {
                        
                        try
                        {
                            // 🔹 Wywołanie SendDataFunc (UPDATE -> UpdateDeviceNameAsync)
                            success = await mapping.SendDataFunc(_api, data, method);
                            if (!success)
                                await Task.Delay(mapping.RetryDelay);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Błąd API dla {audit.Object_Type} ID={audit.Object_ID}: {ex.Message}");
                            await Task.Delay(mapping.RetryDelay);
                        }
                    }

                    if (success)
                    {
                        _db.MarkAuditProcessed(audit.ID);
                        Console.WriteLine($"Sukces: {audit.Object_Type} ID={audit.Object_ID}");
                    }
                    else
                    {
                        Console.WriteLine($"Nie udało się wysłać danych po {mapping.RetryCount} próbach: {audit.Object_Type} ID={audit.Object_ID}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd synchronizacji dla {audit.Object_Type} ID={audit.Object_ID}: {ex.Message}");
                }
            }
        }
    }
}
