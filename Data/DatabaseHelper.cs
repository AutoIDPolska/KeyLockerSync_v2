using KeyLockerSync.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                    Console.WriteLine($"[WARN] InsertOrUpdateDevices Otrzymano urządzenie z pustym Gid. Pomijam rekord.");
                    continue;
                }
                Console.WriteLine($"[DEBUG] InsertOrUpdateDevices Przetwarzam urządzenie: GID='{device.Gid}', Name='{device.Name}', Status='{device.Status}'");


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
                Console.WriteLine($"[DEBUG] InsertKeys Przetwarzam klucz: KeyIdExt='{key.KeyIdExt}', Name='{key.Name}', Gid='{key.Gid}', DeviceId z API='{key.DeviceId}'.");

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
                    Console.WriteLine("[DEBUG] InsertKeyStates Procedura wykonana pomyślnie.");
                }
                else
                {
                    Console.WriteLine($"[WARN] InsertKeyStates Pomijam stan dla klucza '{state.KeyIdExt}', ponieważ urządzenie nadrzędne (Gid={state.Gid}) nie zostało jeszcze zsynchronizowane.");
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
                Console.WriteLine($"[ERROR] GetKeyGroupData Nieprawidłowy format groupIdApi: '{groupIdApi}'. Oczekiwano liczby.");
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
                    Description = "" 
                };
            }
            return null;
        }

        public async Task<object> GetPersonDataAsync(string idautoid)
        {
            if (!int.TryParse(idautoid, out int personId))
            {
                Console.WriteLine($"[ERROR] Nieprawidłowy format idautoid: '{idautoid}'. Oczekiwano liczby.");
                return null;
            }

            Person person = null;
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using (var cmd = new SqlCommand("keyLocker_person_postput", conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@idautoid", personId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    person = new Person
                    {
                        FirstName = reader["FirstName"]?.ToString(),
                        LastName = reader["LastName"]?.ToString(),
                        OwnerIdApi = reader["OwnerIdApi"].ToString()
                    };

                    string cards = reader["Card"]?.ToString();
                    if (!string.IsNullOrEmpty(cards))
                    {
                        person.Cards.AddRange(cards.Split(',').Select(c => c.Trim('"')));
                    }

                    string pins = reader["PIN"]?.ToString();
                    if (!string.IsNullOrEmpty(pins))
                    {
                        person.Pins.AddRange(pins.Split(',').Select(p => p.Trim('"')));
                    }

                    string keyIdExts = reader["KeyIdExt"]?.ToString();
                    if (!string.IsNullOrEmpty(keyIdExts))
                    {
                        person.KeyIdExts.AddRange(keyIdExts.Split(',').Select(k => k.Trim('"')));
                    }
                }
            }

            if (person == null)
            {
                Console.WriteLine($"[WARN] Nie znaleziono osoby o idautoid={personId} lub nie spełnia ona kryteriów procedury.");
                return null;
            }

            // Znajdź GID na podstawie pierwszego klucza, jeśli istnieje
            if (person.KeyIdExts.Any())
            {
                string gidQuery = "SELECT TOP 1 gid FROM dbo.keyLocker_key WHERE device_key_id = @keyIdExt";
                using (var gidCmd = new SqlCommand(gidQuery, conn))
                {
                    gidCmd.Parameters.AddWithValue("@keyIdExt", person.KeyIdExts.First());
                    var gid = await gidCmd.ExecuteScalarAsync();
                    person.Gid = gid?.ToString();
                }
            }

            return person;
        }
       
        // Pobiera dane klucza (ID i nazwę) na potrzeby audytu aktualizacji.
       
        public async Task<object> GetKeyDataAsync(string objectId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("keyLocker_key_postput", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            if (string.IsNullOrEmpty(objectId))
            {
                Console.WriteLine($"[ERROR] Pusty Object_ID w audycie dla typu 'key'.");
                return null;
            }

            cmd.Parameters.AddWithValue("@keyIdExt", objectId);

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
            Console.WriteLine($"[WARN] Procedura 'keyLocker_key_postput' nie zwróciła danych dla klucza o identyfikatorze: '{objectId}'.");
            return null;
        }
        
        // Pobiera dane rezerwacji na podstawie jej ID z audytu.
       
        public async Task<object> GetReservationDataAsync(string reservationId)
        {
            if (!int.TryParse(reservationId, out int id))
            {
                Console.WriteLine($"[ERROR] Nieprawidłowy format ID dla rezerwacji: '{reservationId}'. Oczekiwano liczby.");
                return null;
            }

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using (var cmd = new SqlCommand("keyLocker_reservation_postput", conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // Tworzymy obiekt Reservation, odczytując 'keyIdExt' bezpośrednio jako string.
                    return new Reservation
                    {
                        ReservationId = id,
                        Gid = reader["gid"].ToString(),
                        OwnerIdApi = reader["ownerIdApi"].ToString(),
                        KeyIdExt = reader["keyIdExt"].ToString(),
                        ValidFrom = Convert.ToDateTime(reader["validFrom"]),
                        ValidTo = Convert.ToDateTime(reader["validTo"])
                    };
                }
            }

            Console.WriteLine($"[WARN] Nie znaleziono danych dla rezerwacji o ID={id}.");
            return null;
        }

       
        // Pobiera dane poświadczenia (dla operacji INSERT).
        
        public async Task<object> GetCredentialDataAsync(string credential)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("keyLocker_credential_postput", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@creditional", credential);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CredentialData
                {
                    OwnerIdApi = reader["Idautoid"].ToString(),
                    Credential = reader["C_CardNum"].ToString(),
                    Method = reader["Method"].ToString()
                };
            }
            Console.WriteLine($"[WARN] GetCredentialData Procedura 'keyLocker_credential_postput' nie zwróciła danych dla credential='{credential}'.");
            return null;
        }

        public void MarkAuditProcessed(int id)
        {
            using var conn = new SqlConnection(_connectionString);

            Console.WriteLine($"[DB] Oznaczam audyt ID={id} jako pomyślnie przetworzony (Status=1).");
            using var cmd = new SqlCommand("UPDATE keylocker_audit SET Status='1', Date_Processed=GETDATE() WHERE ID=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            conn.Open();
            cmd.ExecuteNonQuery();     
        }
        
        // Oznacza rekord audytu jako przetworzony z błędem lub ostrzeżeniem.
       
        public void MarkAuditAsWarning(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            Console.WriteLine($"[DB] Oznaczam audyt ID={id} jako przetworzony z ostrzeżeniem (Status=2).");
            using var cmd = new SqlCommand("UPDATE keylocker_audit SET Status='2', Date_Processed=GETDATE() WHERE ID=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            conn.Open();
            cmd.ExecuteNonQuery();
        }
    }
}