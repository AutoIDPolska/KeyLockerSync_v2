using KeyLockerSync.Data;
using KeyLockerSync.Services;

namespace KeyLockerSync.Models
{
    public class ActionMapping
    {
        // Pobiera dane z DB po objectId (string)
        public Func<DatabaseHelper, string, Task<object>> GetDataFunc { get; set; }

        // Wysyła dane do API: (ApiService, obiekt, HttpMethod) -> success
        public Func<ApiService, object, HttpMethod, Task<bool>> SendDataFunc { get; set; }

    }
}
