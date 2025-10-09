using KeyLockerSync.Models;
using System.Data;
using System.Data.SqlClient;

namespace KeyLockerSync.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;
        public DatabaseHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<AuditRecord> GetPendingAuditRecords()
        {
            var list = new List<AuditRecord>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT TOP 1000 * FROM keylocker_audit WHERE Status='0'", conn);
            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AuditRecord
                {
                    ID = Convert.ToInt32(reader["ID"]),
                    Object_ID = reader["Object_ID"].ToString(),
                    Object_Type = reader["Object_Type"].ToString(),
                    Action_Type = reader["Action_Type"].ToString(),
                    Date_Added = Convert.ToDateTime(reader["Date_Added"]),
                    Date_Processed = reader["Date_Processed"] as DateTime?,
                    Status = Convert.ToInt32(reader["Status"]),
                });
            }
            return list;
        }

        public async Task<object> GetDeviceDataAsync(string gid)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("keyLocker_device_postput", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@gid", gid);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Device
                {
                    Gid = reader["gid"].ToString(),
                    Name = reader["name"].ToString(),
                    
                };
            }
            return null;
        }

        
        public void MarkAuditProcessed(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("UPDATE keylocker_audit SET Status='1', Date_Processed=GETDATE() WHERE ID=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            conn.Open();
            cmd.ExecuteNonQuery();
        }
    }
}
