using KeyLockerSync.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace KeyLockerSync.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl; 

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            string apiUrl = ConfigurationManager.AppSettings["ApiUrl"];
            if (string.IsNullOrEmpty(apiUrl))
                throw new Exception("Brak ustawienia ApiUrl w app.config");

            _httpClient.BaseAddress = new Uri(apiUrl);
        }

        public async Task<List<Device>> GetDevicesAsync(int? gid = null, string name = null, string status = null)
        {
            Console.WriteLine("[INFO] Wykonuję GET /devices"); // Logowanie
            try
            {
                var builder = new UriBuilder(_httpClient.BaseAddress + "devices");
                var query = HttpUtility.ParseQueryString(builder.Query);
                if (gid.HasValue)
                {
                    query["gid"] = gid.Value.ToString();
                }
                if (!string.IsNullOrEmpty(name))
                {
                    query["name"] = name;
                }
                if (!string.IsNullOrEmpty(status))
                {
                    query["status"] = status;
                }
                builder.Query = query.ToString();

                var response = await _httpClient.GetAsync(builder.ToString());
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                //Console.WriteLine($"[DEBUG] Otrzymano odpowiedź z GET /devices (api): {content}");

                return JsonSerializer.Deserialize<List<Device>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd GET /devices (api): {ex.Message}");
                return null;
            }
        }

        public async Task<List<Key>> GetKeysAsync()
        {
            Console.WriteLine("[INFO] Wykonuję GET /keys");
            try
            {
                var response = await _httpClient.GetAsync("/keys");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var keys = JsonSerializer.Deserialize<List<Key>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (keys != null)
                {
                    Console.WriteLine($"[INFO] GET /keys - Pobrano {keys.Count} kluczy");
                    foreach (var key in keys)
                    {
                        // Console.WriteLine($"  - Id: {key.Id}, Name: {key.Name}"); // Zmień właściwości zgodnie z definicją modelu Key
                    }
                }
                else
                {
                    Console.WriteLine("[WARNING] Brak pobranych kluczy (null lub pusta lista)!");
                }
                return keys;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd GET /keys: {ex.Message}");
                return null;
            }
        }

        public async Task<List<KeyState>> GetKeyStatesAsync()
        {
            Console.WriteLine("[INFO] Wykonuję GET /keys/states");
            try
            {
                var response = await _httpClient.GetAsync("/keys/states");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<KeyState>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd GET /keys/states: {ex.Message}");
                return null;
            }
        }

        // Aktualizuje nazwę klucza w API (dla Object_Type: key, Action_Type: update).
        
        public async Task<bool> UpdateKeyNameAsync(object obj, HttpMethod method)
        {
            // Metoda obsługuje tylko operacje PUT dla obiektu Key
            if (method != HttpMethod.Put || obj is not Key key)
                return false;

            string url = $"/keys/{key.KeyId}/name";

            // Tworzymy payload z samą nazwą, zgodnie z wymaganiami API
            var payload = new { name = key.Name };
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(payload)
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP PUT {url} -> {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki PUT dla klucza {key.KeyId}: {ex.Message}");
                return false;
            }
        }

        
        // Wysyłka obiektu Device (POST/PUT/DELETE)
        public async Task<bool> SendDeviceAsync(object obj, HttpMethod method)
        {
            if (obj is not Device device)
                return false;

            string url = method == HttpMethod.Put ? $"/devices/{device.Gid}" : "/devices";

            HttpRequestMessage request;

            // Jeśli PUT, najpierw GET aby pobrać pełny JSON i zmienić tylko Name
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
                                // Tworzymy słownik z JSON
                                var objDict = new Dictionary<string, JsonElement>();
                                foreach (var prop in doc.RootElement.EnumerateObject())
                                    objDict[prop.Name] = prop.Value.Clone();

                                // Nadpisujemy tylko name
                                objDict["name"] = JsonDocument.Parse($"\"{device.Name}\"").RootElement.Clone();

                                // Serializacja do JSON i PUT
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
                // POST/DELETE używa pełnego obiektu
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

        public async Task<bool> SendKeyGroupAsync(object obj, HttpMethod method)
        {
            if (obj is not KeyGroup keyGroup)
                return false;

            string url = (method == HttpMethod.Delete || method == HttpMethod.Put)
                ? $"/groups/{keyGroup.GroupIdApi}"
                : "/groups";

            var request = new HttpRequestMessage(method, url);

            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                if (string.IsNullOrEmpty(keyGroup.Gid))
                {
                    Console.WriteLine($"[WARN] Pomijam operację {method} dla grupy '{keyGroup.Name}' (GroupIdApi={keyGroup.GroupIdApi}), ponieważ GID jest wymagany, a procedura zwróciła NULL.");
                    return false;   // *** ZMIANA: Zwracamy 'false', aby oznaczyć audyt statusem '2' ***
                }

                var payload = new
                {
                    gid = keyGroup.Gid,
                    name = keyGroup.Name,
                    description = keyGroup.Description,
                    groupIdApi = keyGroup.GroupIdApi
                };
                request.Content = JsonContent.Create(payload);

                var jsonPayloadForLogging = JsonSerializer.Serialize(payload);
                Console.WriteLine($"[DEBUG] Wysyłam {method} do {url} z danymi: {jsonPayloadForLogging}");
            }

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {method} {url} -> {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] API zwróciło błąd. Treść odpowiedzi: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla keygroup {keyGroup.GroupIdApi}: {ex.Message}");
                return false;
            }
        }

        
        // Tworzy (POST) lub aktualizuje (PUT) dane osoby w API.
        // Implementuje logikę "UPSERT" oraz mechanizm ponawiania próby utworzenia użytkownika bez kluczy.
        
        public async Task<bool> SendPersonAsync(object obj, HttpMethod method)
        {
            if (obj is not Person person)
            {
                Console.WriteLine("[ERROR] Nieprawidłowy typ obiektu w SendPersonAsync. Oczekiwano 'Person'.");
                return false;
            }

            var initialPayload = new
            {
                gid = person.Gid,
                ownerIdApi = person.OwnerIdApi,
                firstName = person.FirstName,
                lastName = person.LastName,
                credentials = new
                {
                    pin = person.Pins,
                    card = person.Cards,
                    temporary = new string[] { }
                },
                keyIdExts = person.KeyIdExts
            };

            // Najpierw próbujemy zaktualizować (PUT)
            string url = $"/persons/{person.OwnerIdApi}";
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(initialPayload)
            };

            try
            {
                Console.WriteLine($"[INFO] Próbuję zaktualizować (PUT) osobę: {person.OwnerIdApi}");
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP PUT {url} -> {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"[INFO] Osoba {person.OwnerIdApi} nie istnieje. Próbuję ją utworzyć (POST)...");

                    url = "/persons";
                    request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(initialPayload)
                    };

                    response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"HTTP POST {url} -> {response.StatusCode}");

                    // *** KLUCZOWA ZMIANA: Obsługa błędu "Keys not found" ***
                    if (!response.IsSuccessStatusCode && response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        if (errorContent.Contains("Keys not found"))
                        {
                            Console.WriteLine("[WARN] API odrzuciło żądanie z powodu nieistniejących kluczy. Ponawiam próbę, tworząc użytkownika bez przypisania kluczy.");

                            // Tworzymy nowy payload z pustą listą kluczy
                            var fallbackPayload = new
                            {
                                gid = person.Gid,
                                ownerIdApi = person.OwnerIdApi,
                                firstName = person.FirstName,
                                lastName = person.LastName,
                                credentials = new
                                {
                                    pin = person.Pins,
                                    card = person.Cards,
                                    temporary = new string[] { }
                                },
                                keyIdExts = new string[] { } // Pusta lista kluczy
                            };

                            request = new HttpRequestMessage(HttpMethod.Post, "/persons")
                            {
                                Content = JsonContent.Create(fallbackPayload)
                            };

                            response = await _httpClient.SendAsync(request);
                            Console.WriteLine($"HTTP POST (ponowienie bez kluczy) /persons -> {response.StatusCode}");
                        }
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response body (po POST): {content}");
                    }
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body (po PUT): {content}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Krytyczny błąd podczas operacji na osobie {person.OwnerIdApi}: {ex.Message}");
                return false;
            }
        }

        // Przypisuje (POST) lub odbiera (DELETE) klucze osobie, używając keyIdExts.

        public async Task<bool> AssignOrUnassignKeyAsync(object obj, HttpMethod method)
        {
            if (obj is not KeyUser keyUser)
                return false;

            string url = $"/persons/{keyUser.OwnerIdApi}/keys";

            // *** ZMIANA: Zaktualizowano payload, aby wysyłał pole "keyIdExts" ***
            var payload = new { keyIdExts = keyUser.KeyIdExts };

            var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(payload)
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {method} {url} -> {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla KeyUser {keyUser.OwnerIdApi}: {ex.Message}");
                return false;
            }
        }

        // Adds (POST) or removes (DELETE) a key from a key group.
        
        public async Task<bool> AssignOrUnassignKeyInGroupAsync(object obj, HttpMethod method)
        {
            if (obj is not KeyGroupKey keyGroupKey)
                return false;

            // The URL is constructed using the GroupIdApi.
            string url = $"/groups/{keyGroupKey.GroupIdApi}/keys";

            // The payload is the same for both POST and DELETE requests.
            var payload = new { keyIdExts = keyGroupKey.KeyIdExts };
            var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(payload)
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {method} {url} -> {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla KeyGroupKey (grupa: {keyGroupKey.GroupIdApi}): {ex.Message}");
                return false;
            }
        }

        // Adds (POST) or removes (DELETE) a person from a key group.
        
        public async Task<bool> AssignOrUnassignPersonInGroupAsync(object obj, HttpMethod method)
        {
            if (obj is not KeyGroupUser keyGroupUser)
                return false;

            // The URL is constructed using the GroupIdApi.
            string url = $"/groups/{keyGroupUser.GroupIdApi}/persons";

            // The payload is the same for both POST and DELETE requests.
            var payload = new { ownerIdApis = keyGroupUser.OwnerIdApis };
            var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(payload)
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {method} {url} -> {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla KeyGroupUser (grupa: {keyGroupUser.GroupIdApi}): {ex.Message}");
                return false;
            }
        }

        // Wysyła dane rezerwacji (POST, PUT, DELETE).

        public async Task<bool> SendReservationAsync(object obj, HttpMethod method)
        {
            if (obj is not Reservation reservation)
                return false;

            string url = method switch
            {
                var m when m == HttpMethod.Post => "/reservations",
                var m when m == HttpMethod.Put => $"/reservations/{reservation.ReservationId}",
                var m when m == HttpMethod.Delete => $"/reservations/{reservation.ReservationId}",
                _ => throw new NotSupportedException($"Metoda {method} nie jest obsługiwana dla rezerwacji.")
            };

            var request = new HttpRequestMessage(method, url);

            // Dla POST i PUT dołączamy ciało żądania
            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                // Tworzymy obiekt anonimowy, aby wysłać tylko te pola, których oczekuje API,
                // używając poprawnej nazwy pola "keyIdExt".
                var payload = new
                {
                    gid = reservation.Gid,
                    ownerIdApi = reservation.OwnerIdApi,
                    keyIdExt = reservation.KeyIdExt,
                    validFrom = reservation.ValidFrom,
                    validTo = reservation.ValidTo
                };
                request.Content = JsonContent.Create(payload);
            }

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {method} {url} -> {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla rezerwacji {reservation.ReservationId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendCredentialAsync(object obj, HttpMethod method)
        {
            if (obj is not CredentialData credentialData)
                return false;

            string url;

            if (method == HttpMethod.Delete)
            {
                url = "/persons/credentials";
            }
            else
            {
                if (string.IsNullOrEmpty(credentialData.OwnerIdApi))
                {
                    Console.WriteLine($"[ERROR] Nie można wysłać poświadczenia, ponieważ OwnerIdApi jest pusty.");
                    return false;
                }
                url = $"/persons/{credentialData.OwnerIdApi}/credentials";
            }

            var payload = new
            {
                method = credentialData.Method,
                credential = credentialData.Credential
            };
            var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent.Create(payload)
            };

            try
            {
                var response = await _httpClient.SendAsync(request);
                Console.WriteLine($"HTTP {method} {url} -> {response.StatusCode}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"[WARN] API zwróciło błąd 404 (Not Found). Audyt zostanie oznaczony jako przetworzony.");
                    return true; // Zwracamy 'true', aby usunąć zadanie z kolejki.
                }

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response body: {content}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Błąd wysyłki {method} dla Credential: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateDeviceNameAsync(string gid, string newName)
        {
            var tempDevice = new Device { Gid = gid, Name = newName };
            return await SendDeviceAsync(tempDevice, HttpMethod.Put);
        }
    }
}