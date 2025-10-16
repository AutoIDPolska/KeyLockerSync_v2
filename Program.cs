using KeyLockerSync.Data;
using KeyLockerSync.Services;
using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // *** KLUCZOWA ZMIANA: Inicjalizacja logowania do pliku ***
        string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
        var fileLogger = new FileLogger(logFilePath);
        Console.SetOut(fileLogger); // Przekierowanie standardowego wyjścia konsoli

        try
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("Uruchamianie KeyLockerSync...");

            string connectionString = ConfigurationManager.ConnectionStrings["KeyLockerDB"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("[FATAL] Brak connectionString w app.config (KeyLockerDB). Aplikacja zostanie zamknięta.");
                return;
            }

            int auditInterval = GetIntervalFromConfig("SyncAuditIntervalSeconds", 30);
            int devicesInterval = GetIntervalFromConfig("SyncDevicesIntervalSeconds", 60);
            int keysInterval = GetIntervalFromConfig("SyncKeysIntervalSeconds", 70);
            int keyStatesInterval = GetIntervalFromConfig("SyncKeyStatesIntervalSeconds", 80);

            var dbHelper = new DatabaseHelper(connectionString);
            var httpClient = new HttpClient();
            var apiService = new ApiService(httpClient);
            var syncService = new SyncService(dbHelper, apiService);

            Console.WriteLine($"Synchronizacja audytu co: {auditInterval} sek.");
            Console.WriteLine($"Synchronizacja urządzeń co: {devicesInterval} sek.");
            Console.WriteLine($"Synchronizacja kluczy co: {keysInterval} sek.");
            Console.WriteLine($"Synchronizacja stanów kluczy co: {keyStatesInterval} sek.\n");

            Task auditSyncTask = AuditSyncLoopAsync(syncService, auditInterval);
            Task devicesSyncTask = GetDevicesLoopAsync(apiService, dbHelper, devicesInterval);
            Task keysSyncTask = GetKeysLoopAsync(apiService, dbHelper, keysInterval);
            Task keyStatesSyncTask = GetKeyStatesLoopAsync(apiService, dbHelper, keyStatesInterval);

            await Task.WhenAll(auditSyncTask, devicesSyncTask, keysSyncTask, keyStatesSyncTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Wystąpił nieobsługiwany błąd w głównej metodzie: {ex.Message}");
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            // Upewniamy się, że plik logów jest zawsze poprawnie zamykany
            fileLogger?.Close();
        }
    }

    static int GetIntervalFromConfig(string key, int defaultValue)
    {
        if (int.TryParse(ConfigurationManager.AppSettings[key], out int value) && value > 0)
        {
            return value;
        }
        Console.WriteLine($"Niepoprawny format klucza '{key}' w app.config. Ustawiono wartość domyślną: {defaultValue} sek.");
        return defaultValue;
    }
    
    static async Task AuditSyncLoopAsync(SyncService syncService, int intervalSeconds)
    {
        while (true)
        {
            try
            {
                await syncService.SyncAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd pętli synchronizacji audytu: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }
    
    static async Task GetDevicesLoopAsync(ApiService apiService, DatabaseHelper dbHelper, int intervalSeconds)
    {
        while (true)
        {
            Console.WriteLine("\n--- Rozpoczynam cykl synchronizacji urządzeń ---");
            try
            {
                var devices = await apiService.GetDevicesAsync();
                if (devices != null)
                {
                    await dbHelper.InsertOrUpdateDevicesAsync(devices);
                    Console.WriteLine($"[INFO] Przetworzono {devices.Count} urządzeń.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd pętli synchronizacji urządzeń: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }

    static async Task GetKeysLoopAsync(ApiService apiService, DatabaseHelper dbHelper, int intervalSeconds)
    {
        while (true)
        {
            Console.WriteLine("\n--- Rozpoczynam cykl synchronizacji kluczy ---");
            try
            {
                var keys = await apiService.GetKeysAsync();
                if (keys != null)
                {
                    await dbHelper.InsertKeysAsync(keys);
                    Console.WriteLine($"[INFO] Przetworzono {keys.Count} kluczy.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd pętli synchronizacji kluczy: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }

    static async Task GetKeyStatesLoopAsync(ApiService apiService, DatabaseHelper dbHelper, int intervalSeconds)
    {
        while (true)
        {
            Console.WriteLine("\n--- Rozpoczynam cykl synchronizacji stanów kluczy ---");
            try
            {
                var keyStates = await apiService.GetKeyStatesAsync();
                if (keyStates != null)
                {
                    await dbHelper.InsertKeyStatesAsync(keyStates);
                    Console.WriteLine($"[INFO] Przetworzono {keyStates.Count} stanów kluczy.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd pętli synchronizacji stanów kluczy: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }
}