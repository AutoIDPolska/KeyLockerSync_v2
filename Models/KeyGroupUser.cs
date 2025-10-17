using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class KeyGroupUser
    {
        // This is the groupIdApi from the audit's Object_ID.
        // It's used for the URL and is not part of the JSON body.
        [JsonIgnore]
        public string GroupIdApi { get; set; }

        // This is the list of person IDs from the audit's Additional_ID.
        [JsonPropertyName("ownerIdApis")]
        public List<string> OwnerIdApis { get; set; } = new List<string>();
    }
}