using System;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class KeyState
    {
        [JsonPropertyName("keyStateId")]
        public int KeyStateId { get; set; }

        [JsonPropertyName("keyId")]
        public int KeyId { get; set; }

        [JsonPropertyName("deviceId")]
        public int DeviceId { get; set; }

        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("keyIdExt")]
        public string KeyIdExt { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("personId")]
        public string? PersonId { get; set; }

        [JsonPropertyName("ownerIdApi")]
        public string? OwnerIdApi { get; set; }

        [JsonPropertyName("ts")]
        public DateTime Ts { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("sourceEventId")]
        public int? SourceEventId { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}