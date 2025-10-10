using KeyLockerSync.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

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
           
            string query = "SELECT TOP 1000 ID, Object_ID, Additional_ID, Object_Type, Action_Type, Date_Added, Date_Processed, Status FROM keylocker_audit WHERE Status='0'";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AuditRecord
                {
                    ID = Convert.ToInt32(reader["ID"]),
                    Object_ID = reader["Object_ID"].ToString(),
                    Additional_ID = reader["Additional_ID"]?.ToString(), 
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
                return new Device { Gid = reader["gid"].ToString(), Name = reader["name"].ToString() };
            }
            return null;
        }

        public async Task InsertOrUpdateDevicesAsync(List<Device> devices)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            foreach (var device in devices)
            {
                if (string.IsNullOrEmpty(device.Gid))
                {
                    Console.WriteLine($"[WARN] Otrzymano urządzenie z pustym Gid. Pomijam rekord.");
                    continue;
                }
                Console.WriteLine($"[DEBUG] Przetwarzam urządzenie: GID='{device.Gid}', Name='{device.Name}', Status='{device.Status}'");


                using var cmd = new SqlCommand("keyLocker_device_get", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@gid", device.Gid);
                cmd.Parameters.AddWithValue("@name", device.Name);
                cmd.Parameters.AddWithValue("@status", device.Status);
                cmd.Parameters.AddWithValue("@last_sync_at", (object)device.LastSyncAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@pass", "");
                cmd.Parameters.AddWithValue("@ws_address", "");
                cmd.Parameters.AddWithValue("@last_log_date", DateTime.Now);
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine($"[INFO] Dodano nowy depozytor.");
            }
        }

        public async Task InsertKeysAsync(List<Key> keys)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            Console.WriteLine($"[DEBUG] InsertKeys Rozpoczynam przetwarzanie {keys.Count} kluczy...");

            foreach (var key in keys)
            {
                Console.WriteLine($"\n[DEBUG] InsertKeys Przetwarzam klucz: KeyIdExt='{key.KeyIdExt}', Name='{key.Name}', Gid='{key.Gid}', DeviceId z API='{key.DeviceId}'.");

                int? localDeviceId = null;
                if (!string.IsNullOrEmpty(key.Gid))
                {
                    Console.WriteLine($"[DEBUG] InsertKeys Próbuję znaleźć lokalne ID dla Gid='{key.Gid}'...");
                    using (var findCmd = new SqlCommand("SELECT id FROM dbo.keyLocker_device WHERE gid = @gid", conn))
                    {
                        findCmd.Parameters.AddWithValue("@gid", key.Gid);
                        var result = await findCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            localDeviceId = Convert.ToInt32(result);
                            Console.WriteLine($"[DEBUG] InsertKeys ZNALEZIONO lokalne ID urządzenia: {localDeviceId}.");
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] InsertKeys NIE ZNALEZIONO lokalnego ID urządzenia dla Gid='{key.Gid}'.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] InsertKeys Gid dla klucza jest pusty, nie mogę znaleźć lokalnego ID.");
                }

                if (localDeviceId.HasValue)
                {
                    Console.WriteLine($"[DEBUG] InsertKeys Wywołuję procedurę [keyLocker_key_get] z parametrami: @keyId={key.KeyId}, @deviceId={localDeviceId.Value}, @keyIdExt='{key.KeyIdExt}', @name='{key.Name}'.");
                    using var cmd = new SqlCommand("keyLocker_key_get", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("@keyId", key.KeyId);
                    cmd.Parameters.AddWithValue("@deviceId", localDeviceId.Value);
                    cmd.Parameters.AddWithValue("@gid", (object)key.Gid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@keyIdExt", key.KeyIdExt);
                    cmd.Parameters.AddWithValue("@serialNumberExt", (object)key.SerialNumberExt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@name", key.Name);
                    cmd.Parameters.AddWithValue("@createdAt", key.CreatedAt);
                    cmd.Parameters.AddWithValue("@updatedAt", (object)key.UpdatedAt ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine("[DEBUG] InsertKeys Procedura wykonana pomyślnie.");
                }
                else
                {
                    Console.WriteLine($"[WARN] InsertKeys Pomijam klucz '{key.KeyIdExt}', ponieważ nie udało się znaleźć lokalnego ID dla jego urządzenia (Gid='{key.Gid}').");
                }
            }
            Console.WriteLine($"[DEBUG] InsertKeys Zakończono przetwarzanie kluczy.");
        }

        public async Task InsertKeyStatesAsync(List<KeyState> keyStates)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            foreach (var state in keyStates)
            {
                // KROK 1: Znajdź LOKALNE id urządzenia na podstawie GID stanu klucza
                int? localDeviceId = null;
                if (!string.IsNullOrEmpty(state.Gid))
                {
                    using (var findCmd = new SqlCommand("SELECT id FROM dbo.keyLocker_device WHERE gid = @gid", conn))
                    {
                        findCmd.Parameters.AddWithValue("@gid", state.Gid);
                        var result = await findCmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            localDeviceId = Convert.ToInt32(result);
                        }
                    }
                }

                // KROK 2: Jeśli znaleziono lokalne ID, wstaw stan z poprawnym ID
                if (localDeviceId.HasValue)
                {
                    using var cmd = new SqlCommand("keyLocker_key_state_get", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.AddWithValue("@deviceId", localDeviceId.Value); // Używamy znalezionego LOKALNEGO ID
                    cmd.Parameters.AddWithValue("@gid", (object)state.Gid ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@keyIdExt", state.KeyIdExt);
                    cmd.Parameters.AddWithValue("@state", (object)state.State ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ownerIdApi", (object)state.OwnerIdApi ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ts", (object)state.Ts ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@createdAt", (object)state.CreatedAt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@updatedAt", (object)state.UpdatedAt ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ownerIdExt", DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    Console.WriteLine($"[WARN] Pomijam stan dla klucza '{state.KeyIdExt}', ponieważ urządzenie nadrzędne (Gid={state.Gid}) nie zostało jeszcze zsynchronizowane.");
                }
            }
        }
        public async Task<object> GetKeyGroupDataAsync(string groupIdApi)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("keyLocker_keygroup_postput", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (int.TryParse(groupIdApi, out int id))
            {
                cmd.Parameters.AddWithValue("@groupIdApi", id);
            }
            else
            {
                Console.WriteLine($"[ERROR] Nieprawidłowy format groupIdApi: '{groupIdApi}'. Oczekiwano liczby.");
                return null;
            }

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new KeyGroup
                {
                    Gid = reader["gid"].ToString(),
                    GroupIdApi = reader["groupIdApi"].ToString(), // Zwracamy jako string
                    Name = reader["name"].ToString(),
                    Description = "" // Procedura nie zwraca opisu, więc ustawiamy pusty
                };
            }
            return null;
        }

        public async Task<object> GetPersonDataAsync(string idautoid)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            Person person = null;

            // Krok 1: Pobierz podstawowe dane osoby
            using (var cmd = new SqlCommand("keyLocker_person_postput", conn) { CommandType = CommandType.StoredProcedure })
            {
                if (int.TryParse(idautoid, out int id))
                {
                    cmd.Parameters.AddWithValue("@idautoid", id);
                }
                else
                {
                    Console.WriteLine($"[ERROR] Nieprawidłowy format idautoid: '{idautoid}'. Oczekiwano liczby.");
                    return null;
                }

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    person = new Person
                    {
                        FirstName = reader["firstName"]?.ToString(),
                        LastName = reader["lastName"]?.ToString(),
                        OwnerIdApi = reader["ownerIdApi"].ToString()
                    };
                }
            }

            if (person == null) return null;

            // Krok 2: Znajdź GID urządzenia powiązanego z tą osobą
            // Zakładamy, że osoba może być przypisana do kluczy w jednym urządzeniu (jeden GID)
            using (var gidCmd = new SqlCommand(@"
                SELECT TOP 1 d.gid 
                FROM dbo.keyLocker_key k
                JOIN dbo.keyLocker_device d ON k.device_id = d.id
                JOIN unisuser.tAID_KeyUsers ku ON ku.key_id = k.id
                WHERE ku.idautoid = @idautoid", conn))
            {
                gidCmd.Parameters.AddWithValue("@idautoid", int.Parse(idautoid));
                var gid = await gidCmd.ExecuteScalarAsync();
                if (gid != null && gid != DBNull.Value)
                {
                    person.Gid = gid.ToString();
                }
                else
                {
                    // Jeśli nie znajdziemy GID, nie możemy wysłać danych do API
                    Console.WriteLine($"[WARN] Nie znaleziono GID dla osoby z OwnerIdApi={person.OwnerIdApi}. Pomijam rekord.");
                    return null;
                }
            }

            return person;
        }

        /// <summary>
        /// Pobiera dane klucza (ID i nazwę) na potrzeby audytu aktualizacji.
        /// </summary>
        public async Task<object> GetKeyDataAsync(string keyId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("keyLocker_key_postput", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (int.TryParse(keyId, out int id))
            {
                cmd.Parameters.AddWithValue("@keyid", id);
            }
            else
            {
                Console.WriteLine($"[ERROR] Nieprawidłowy format keyId w audycie: '{keyId}'. Oczekiwano liczby.");
                return null;
            }

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Używamy istniejącego modelu Key, ale wypełniamy tylko potrzebne pola
                return new Key
                {
                    KeyId = Convert.ToInt32(reader["id"]),
                    Name = reader["name"].ToString()
                };
            }
            return null;
        }
        /*
        /// <summary>
        /// Pobiera dane o powiązaniu klucz-użytkownik na podstawie ID z tabeli audytu.
        /// </summary>
        public async Task<object> GetKeyUserDataAsync(string objectId)
        {
            if (!int.TryParse(objectId, out int keyUserId))
            {
                Console.WriteLine($"[ERROR] Nieprawidłowy format Object_ID dla KeyUser: '{objectId}'. Oczekiwano pojedynczej liczby.");
                return null;
            }

            KeyUser keyUser = null;
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Wykonujemy bezpośrednie zapytanie, aby znaleźć idautoid i key_id
            string query = "SELECT idautoid, key_id FROM unisuser.tAID_KeyUsers WHERE id = @id";

            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", keyUserId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    keyUser = new KeyUser
                    {
                        OwnerIdApi = reader["idautoid"].ToString(),
                        KeyIds = new List<int> { Convert.ToInt32(reader["key_id"]) }
                    };
                }
            }

            if (keyUser == null)
            {
                Console.WriteLine($"[WARN] Nie znaleziono danych dla powiązania KeyUser o ID={keyUserId}. Rekord mógł zostać usunięty.");
            }

            return keyUser;
        }

        */
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