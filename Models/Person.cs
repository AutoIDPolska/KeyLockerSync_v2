using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyLockerSync.Models
{
    public class Person
    {
        // --- Pola wspólne, używane w obu typach żądań ---
        public string Gid { get; set; }
        public string OwnerIdApi { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        // --- Pola pobierane z procedury, używane do budowania payloadów ---
        [JsonIgnore] // Ignorujemy te pola w domyślnej serializacji
        public List<string> Cards { get; set; } = new List<string>();
        [JsonIgnore]
        public List<string> Pins { get; set; } = new List<string>();
        [JsonIgnore]
        public List<string> KeyIdExts { get; set; } = new List<string>();

        // --- Pola specyficzne dla PUT (zostawiamy je, jeśli są potrzebne) ---
        public int PersonId { get; set; }
        public int? DeviceId { get; set; }
        public string OwnerIdExt { get; set; }
        public bool Active { get; set; } = true;
        public string Origin { get; set; } = "unis";
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool SyncOutPending { get; set; } = true;
    }
}