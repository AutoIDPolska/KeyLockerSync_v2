using System;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class KeyPlace
    {
        [JsonPropertyName("placeId")]
        public int PlaceId { get; set; }

        [JsonPropertyName("deviceId")]
        public int DeviceId { get; set; }

        [JsonPropertyName("deviceGid")]
        public string DeviceGid { get; set; } // API returns string

        [JsonPropertyName("gid")]
        public int Gid { get; set; } // API returns int here

        [JsonPropertyName("block")]
        public int Block { get; set; }

        [JsonPropertyName("keyplace")]
        public int Keyplace { get; set; }

        [JsonPropertyName("sensorState")]
        public string SensorState { get; set; }

        [JsonPropertyName("fixedKeyFlag")]
        public bool FixedKeyFlag { get; set; }

        [JsonPropertyName("errorId")]
        public string ErrorId { get; set; }

        [JsonPropertyName("errorMsg")]
        public string ErrorMsg { get; set; }

        [JsonPropertyName("keyId")]
        public int? KeyId { get; set; }

        [JsonPropertyName("keyIdExt")]
        public string KeyIdExt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}