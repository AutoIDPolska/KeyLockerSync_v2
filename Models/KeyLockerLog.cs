using System;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class KeyLockerLog
    {
        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("deviceGid")]
        public string DeviceGid { get; set; }

        [JsonPropertyName("logNum")]
        public int LogNum { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("dateK")]
        public DateTime DateK { get; set; }

        [JsonPropertyName("dateE")]
        public DateTime? DateE { get; set; }

        [JsonPropertyName("ownerIdApi")]
        public string OwnerIdApi { get; set; }

        [JsonPropertyName("accessId")]
        public string AccessId { get; set; }

        [JsonPropertyName("accessName")]
        public string AccessName { get; set; }

        [JsonPropertyName("keyIdExt")]
        public string KeyIdExt { get; set; }

        [JsonPropertyName("placeId")]
        public int? PlaceId { get; set; }

        [JsonPropertyName("block")]
        public int? Block { get; set; }

        [JsonPropertyName("keyPlace")]
        public int? KeyPlace { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [JsonPropertyName("payloadJson")]
        public string PayloadJson { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}