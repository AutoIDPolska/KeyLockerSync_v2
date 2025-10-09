using KeyLockerSync.Data;
using KeyLockerSync.Services;
using System;
using System.Configuration; 
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Uruchamianie KeyLockerSync...");

        // Wczytanie connectionString z app.config
        // <connectionStrings>
        //   <add name="KeyLockerDB" connectionString="Server=...;Database=...;User ID=...;Password=...;" />
        // </connectionStrings>
        string connectionString = ConfigurationManager.ConnectionStrings["KeyLockerDB"]?.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("Brak connectionString w app.config (KeyLockerDB).");
            return;
        }

        // Wczytanie interwału z app.config (w minutach)
        // <appSettings>
        //   <add key="SyncIntervalMinutes" value="1" />
        // </appSettings>
        string intervalValue = ConfigurationManager.AppSettings["SyncIntervalMinutes"];
        int intervalMinutes = 1; // domyślnie 1 minuta

        if (!int.TryParse(intervalValue, out intervalMinutes))
        {
            Console.WriteLine("Niepoprawny format SyncIntervalMinutes, ustawiono 1 minutę.");
            intervalMinutes = 1;
        }

        // Inicjalizacja serwisów
        var dbHelper = new DatabaseHelper(connectionString);
        var httpClient = new HttpClient();
        var apiService = new ApiService(httpClient);
        var syncService = new SyncService(dbHelper, apiService);

        Console.WriteLine($"Uruchomiono synchronizację. Interwał: {intervalMinutes} min.\n");

        // Główna pętla cyklicznej synchronizacji
        while (true)
        {
            try
            {
                await syncService.SyncAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd głównej pętli: {ex.Message}");
            }

            Console.WriteLine($"Kolejna synchronizacja za {intervalMinutes} minut...\n");
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes));
        }
    }
}
