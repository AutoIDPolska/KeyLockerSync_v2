using KeyLockerSync.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KeyLockerSync.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl; //  bazowy adres API

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // 🔹 BaseAddress z app.config
            string apiUrl = ConfigurationManager.AppSettings["ApiUrl"];
            if (string.IsNullOrEmpty(apiUrl))
                throw new Exception("Brak ustawienia ApiUrl w app.config");

            _httpClient.BaseAddress = new Uri(apiUrl);
        }

        // Wysyłka obiektu Device (POST/PUT/DELETE)
        public async Task<bool> SendDeviceAsync(object obj, HttpMethod method)
        {
            if (obj is not Device device)
                return false;

            string url = method == HttpMethod.Put ? $"/devices/{device.Gid}" : "/devices";

            HttpRequestMessage request;

            // 🔹 Jeśli PUT, najpierw GET aby pobrać pełny JSON i zmienić tylko Name
            if (method == HttpMethod.Put)
            {
                try
                {
                    var getResponse = await _httpClient.GetAsync(url);
                    if (getResponse.IsSuccessStatusCode)
                    {
                        var json = await getResponse.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(json))
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                // 🔹 Tworzymy słownik z JSON
                                var objDict = new Dictionary<string, JsonElement>();
                                foreach (var prop in doc.RootElement.EnumerateObject())
                                    objDict[prop.Name] = prop.Value.Clone();

                                // 🔹 Nadpisujemy tylko name
                                objDict["name"] = JsonDocument.Parse($"\"{device.Name}\"").RootElement.Clone();

                                // 🔹 Serializacja do JSON i PUT
                                var updatedJson = JsonSerializer.Serialize(objDict);
                                request = new HttpRequestMessage(method, url)
                                {
                                    Content = new StringContent(updatedJson, Encoding.UTF8, "application/json")
                                };

                                Console.WriteLine($"[INFO] Uaktualniono JSON z GET /devices/{device.Gid} przed PUT.");
                                Console.WriteLine($"Body:\n{updatedJson}");
                            }
                            else
                            {
                                Console.WriteLine($"[WARN] GET /devices/{device.Gid} nie zwrócił obiektu JSON.");
                                // fallback: wyślij minimalny JSON tylko z Gid i Name
                                request = new HttpRequestMessage(method, url)
                                {
                                    Content = JsonContent.Create(new { gid = device.Gid, name = device.Name })
                                };
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] GET /devices/{device.Gid} zwrócił pusty body.");
                            request = new HttpRequestMessage(method, url)
                            {
                                Content = JsonContent.Create(new { gid = device.Gid, name = device.Name })
                            };
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] GET /devices/{device.Gid} nie powiódł się: {getResponse.StatusCode}");
                        request = new HttpRequestMessage(method, url)
                        {
                            Content = JsonContent.Create(new { gid = device.Gid, name = device.Name })
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Błąd GET /devices/{device.Gid}: {ex.Message}");
                    request = new HttpRequestMessage(method, url)
                    {
                        Content = JsonContent.Create(new { gid = device.Gid, name = device.Name })
                    };
                }
            }
            else
            {
                // 🔹 POST/DELETE używa pełnego obiektu
                request = new HttpRequestMessage(method, url)
                {
                    Content = JsonContent.Create(device)
                };
            }

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla device {device.Gid}: {ex.Message}");
                return false;
            }
        }

        // 🔹 Wygodna metoda do zmiany tylko Name
        public async Task<bool> UpdateDeviceNameAsync(string gid, string newName)
        {
            var tempDevice = new Device { Gid = gid, Name = newName };
            return await SendDeviceAsync(tempDevice, HttpMethod.Put);
        }
    }
}
