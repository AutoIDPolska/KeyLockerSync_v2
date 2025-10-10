using System.Collections.Generic;

namespace KeyLockerSync.Models
{
    public class KeyUser
    {
        public string OwnerIdApi { get; set; }
        public List<int> KeyIds { get; set; } = new List<int>();
    }
}