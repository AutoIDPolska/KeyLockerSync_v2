using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class KeyGroupKey
    {
        // This is the groupIdApi from the audit's Object_ID
        // It won't be part of the JSON body but is needed for the URL.
        [JsonIgnore]
        public string GroupIdApi { get; set; }

        // This is the list of keys from the audit's Additional_ID
        [JsonPropertyName("keyIdExts")]
        public List<string> KeyIdExts { get; set; } = new List<string>();
    }
}