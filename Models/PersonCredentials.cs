using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class PersonInsertCredentials
    {
        [JsonPropertyName("pin")]
        public List<string> Pin { get; set; } = new List<string>();

        [JsonPropertyName("card")]
        public List<string> Card { get; set; } = new List<string>();

        [JsonPropertyName("temporary")]
        public List<string> Temporary { get; set; } = new List<string>();
    }
}