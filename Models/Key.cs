using System;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class Key
    {
        [JsonPropertyName("keyId")]
        public int KeyId { get; set; }

        [JsonPropertyName("deviceId")]
        public int DeviceId { get; set; }

        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("keyIdExt")]
        public string KeyIdExt { get; set; }

        [JsonPropertyName("serialNumberExt")]
        public string SerialNumberExt { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("syncOutPending")]
        public bool SyncOutPending { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}