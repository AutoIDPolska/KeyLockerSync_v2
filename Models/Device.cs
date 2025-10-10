namespace KeyLockerSync.Models
{
    public class Device
    {
        public string Gid { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int? MasterDeviceId { get; set; }
        public DeviceConfig Config { get; set; }
        public string Status { get; set; }        
        public DateTime? LastSyncAt { get; set; } 
    }

    public class DeviceConfig
    {
        public string BaseUrl { get; set; }
        public string InterfacePassword { get; set; }
        public bool CheckSessionEnabled { get; set; }
        public bool CheckCrcEnabled { get; set; }
        public int TimeoutMs { get; set; }
        public string RetryPolicy { get; set; }
        public int ParallelLimit { get; set; }
    }
}
