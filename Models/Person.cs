using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class Person
    {
        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("ownerIdApi")]
        public string OwnerIdApi { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [JsonPropertyName("credentials")]
        public List<Credential> Credentials { get; set; } = new List<Credential>();

        [JsonPropertyName("keyIds")]
        public List<int> KeyIds { get; set; } = new List<int>();
    }

    public class Credential
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("credential")]
        public string CredentialValue { get; set; }
    }
}