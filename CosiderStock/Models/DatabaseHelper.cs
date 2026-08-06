using Microsoft.Data.Sqlite;

namespace CosiderStock.Models
{
    public static class DatabaseHelper
    {
        private static string _dbPath = string.Empty;

        public static void Initialize(string contentRootPath)
        {
            string dbFolder = Path.Combine(contentRootPath, "wwwroot", "db");
            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }
            _dbPath = Path.Combine(dbFolder, "cosiderstock.db");
        }

        public static string DbPath => _dbPath;

        public static string ConnectionString => $"Data Source={_dbPath}";

        public static void InitializeDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string createUsersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    Password TEXT NOT NULL,
                    FullName TEXT,
                    Email TEXT,
                    Role TEXT DEFAULT 'Utilisateur',
                    IsActive INTEGER DEFAULT 1,
                    CreatedDate TEXT DEFAULT (datetime('now','localtime')),
                    LastLogin TEXT,
                    ProfileImage TEXT
                );";

            using (var command = new SqliteCommand(createUsersTable, connection))
            {
                command.ExecuteNonQuery();
            }

            // Vérifier si l'admin existe
            string checkAdmin = "SELECT COUNT(*) FROM Users WHERE Username = 'admin';";
            using (var command = new SqliteCommand(checkAdmin, connection))
            {
                long count = (long)(command.ExecuteScalar() ?? 0L);
                if (count == 0)
                {
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword("admin123");
                    string insertAdmin = @"
                        INSERT INTO Users (Username, Password, FullName, Email, Role, IsActive)
                        VALUES (@username, @password, @fullname, @email, @role, 1);";

                    using var insertCommand = new SqliteCommand(insertAdmin, connection);
                    insertCommand.Parameters.AddWithValue("@username", "admin");
                    insertCommand.Parameters.AddWithValue("@password", hashedPassword);
                    insertCommand.Parameters.AddWithValue("@fullname", "Administrateur Système");
                    insertCommand.Parameters.AddWithValue("@email", "admin@cosider-tp.com");
                    insertCommand.Parameters.AddWithValue("@role", "Administrateur");
                    insertCommand.ExecuteNonQuery();
                }
            }

            string createLoginLogs = @"
                CREATE TABLE IF NOT EXISTS LoginLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    LoginDate TEXT DEFAULT (datetime('now','localtime')),
                    IpAddress TEXT,
                    Success INTEGER,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );";

            using (var command = new SqliteCommand(createLoginLogs, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        public static User? ValidateUser(string username, string password)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string query = "SELECT * FROM Users WHERE Username = @username AND IsActive = 1;";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@username", username);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                string storedHash = reader["Password"].ToString() ?? "";
                if (BCrypt.Net.BCrypt.Verify(password, storedHash))
                {
                    var user = new User
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Username = reader["Username"].ToString() ?? "",
                        FullName = reader["FullName"]?.ToString(),
                        Email = reader["Email"]?.ToString(),
                        Role = reader["Role"]?.ToString(),
                        IsActive = Convert.ToInt32(reader["IsActive"]) == 1,
                        ProfileImage = reader["ProfileImage"]?.ToString()
                    };
                    reader.Close();

                    // Mettre à jour la dernière connexion
                    string updateLogin = "UPDATE Users SET LastLogin = datetime('now','localtime') WHERE Id = @id;";
                    using var updateCommand = new SqliteCommand(updateLogin, connection);
                    updateCommand.Parameters.AddWithValue("@id", user.Id);
                    updateCommand.ExecuteNonQuery();

                    return user;
                }
            }
            return null;
        }

        public static void LogLogin(int? userId, string ipAddress, bool success)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string query = @"INSERT INTO LoginLogs (UserId, IpAddress, Success) 
                            VALUES (@userId, @ip, @success);";
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@ip", ipAddress);
            command.Parameters.AddWithValue("@success", success ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }
}