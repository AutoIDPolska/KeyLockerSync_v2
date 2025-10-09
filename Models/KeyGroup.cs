using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class KeyGroup
    {
        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("groupIdApi")]
        public string GroupIdApi { get; set; } // Zmieniono typ na string

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } // Dodano pole Description
    }
}