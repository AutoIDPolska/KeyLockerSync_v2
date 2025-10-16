using System;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class Reservation
    {
        [JsonIgnore]
        public int ReservationId { get; set; }

        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("ownerIdApi")]
        public string OwnerIdApi { get; set; }

        // Poprawnie zdefiniowane jako 'string'
        [JsonPropertyName("keyIdExt")]
        public string KeyIdExt { get; set; }

        [JsonPropertyName("validFrom")]
        public DateTime ValidFrom { get; set; }

        [JsonPropertyName("validTo")]
        public DateTime ValidTo { get; set; }
    }
}