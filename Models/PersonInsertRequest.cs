using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class PersonInsertRequest
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
        public PersonInsertCredentials Credentials { get; set; }

        [JsonPropertyName("keyIdExts")]
        public List<string> KeyIdExts { get; set; }
    }
}