using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class CredentialData
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("credential")]
        public string Credential { get; set; }

        // To pole nie będzie wysyłane do API, ale jest potrzebne w logice
        [JsonIgnore]
        public string OwnerIdApi { get; set; }
    }
}