namespace KeyLockerSync.Models
{
    public class AuditRecord
    {
        public int ID { get; set; }
        public string Object_ID { get; set; }
        public string Object_Type { get; set; }
        public string Action_Type { get; set; }
        public DateTime Date_Added { get; set; }
        public DateTime? Date_Processed { get; set; }
        public int Status { get; set; }
    }
}