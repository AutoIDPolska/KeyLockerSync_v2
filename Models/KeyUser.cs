using System.Collections.Generic;

namespace KeyLockerSync.Models
{
    public class KeyUser
    {
        public string OwnerIdApi { get; set; }
        public List<string> KeyIdExts { get; set; } = new List<string>();
    }
}