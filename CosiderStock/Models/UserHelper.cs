using Microsoft.Data.Sqlite;

namespace CosiderStock.Models
{
    public static class UserHelper
    {
        public static void EnsurePhoneColumn()
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();
            try
            {
                string alter = "ALTER TABLE Users ADD COLUMN Phone TEXT;";
                using var cmd = new SqliteCommand(alter, connection);
                cmd.ExecuteNonQuery();
            }
            catch { /* Colonne existe déjà */ }
        }

        public static List<User> GetAllUsers()
        {
            var users = new List<User>();
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            string query = "SELECT * FROM Users ORDER BY Id DESC;";
            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                users.Add(new User
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Username = reader["Username"].ToString() ?? "",
                    FullName = reader["FullName"]?.ToString(),
                    Email = reader["Email"]?.ToString(),
                    Phone = reader.GetOrdinal("Phone") >= 0 ? reader["Phone"]?.ToString() : "",
                    Role = reader["Role"]?.ToString(),
                    IsActive = Convert.ToInt32(reader["IsActive"]) == 1,
                    CreatedDate = DateTime.TryParse(reader["CreatedDate"]?.ToString(), out var cd) ? cd : DateTime.Now,
                    LastLogin = DateTime.TryParse(reader["LastLogin"]?.ToString(), out var ll) ? ll : null,
                    ProfileImage = reader["ProfileImage"]?.ToString()
                });
            }
            return users;
        }

        public static User? GetUserById(int id)
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            string query = "SELECT * FROM Users WHERE Id = @id;";
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new User
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Username = reader["Username"].ToString() ?? "",
                    FullName = reader["FullName"]?.ToString(),
                    Email = reader["Email"]?.ToString(),
                    Phone = reader.GetOrdinal("Phone") >= 0 ? reader["Phone"]?.ToString() : "",
                    Role = reader["Role"]?.ToString(),
                    IsActive = Convert.ToInt32(reader["IsActive"]) == 1
                };
            }
            return null;
        }

        public static (bool Success, string Message) CreateUser(User user)
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            // Vérifier l'unicité du username
            string check = "SELECT COUNT(*) FROM Users WHERE Username = @u;";
            using (var chkCmd = new SqliteCommand(check, connection))
            {
                chkCmd.Parameters.AddWithValue("@u", user.Username);
                if ((long)(chkCmd.ExecuteScalar() ?? 0L) > 0)
                    return (false, "Ce nom d'utilisateur existe déjà");
            }

            string hashed = BCrypt.Net.BCrypt.HashPassword(user.Password);
            string insert = @"INSERT INTO Users (Username, Password, FullName, Email, Phone, Role, IsActive) 
                             VALUES (@u, @p, @f, @e, @ph, @r, @a);";
            using var cmd = new SqliteCommand(insert, connection);
            cmd.Parameters.AddWithValue("@u", user.Username);
            cmd.Parameters.AddWithValue("@p", hashed);
            cmd.Parameters.AddWithValue("@f", (object?)user.FullName ?? "");
            cmd.Parameters.AddWithValue("@e", (object?)user.Email ?? "");
            cmd.Parameters.AddWithValue("@ph", (object?)user.Phone ?? "");
            cmd.Parameters.AddWithValue("@r", (object?)user.Role ?? "Utilisateur");
            cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();

            return (true, "Utilisateur créé avec succès");
        }

        public static (bool Success, string Message) UpdateUser(User user, bool changePassword)
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            // Vérifier l'unicité du username (sauf pour lui-même)
            string check = "SELECT COUNT(*) FROM Users WHERE Username = @u AND Id != @id;";
            using (var chkCmd = new SqliteCommand(check, connection))
            {
                chkCmd.Parameters.AddWithValue("@u", user.Username);
                chkCmd.Parameters.AddWithValue("@id", user.Id);
                if ((long)(chkCmd.ExecuteScalar() ?? 0L) > 0)
                    return (false, "Ce nom d'utilisateur est déjà utilisé");
            }

            string update;
            if (changePassword && !string.IsNullOrEmpty(user.Password))
            {
                string hashed = BCrypt.Net.BCrypt.HashPassword(user.Password);
                update = @"UPDATE Users SET Username=@u, Password=@p, FullName=@f, Email=@e, 
                          Phone=@ph, Role=@r, IsActive=@a WHERE Id=@id;";
                using var cmd = new SqliteCommand(update, connection);
                cmd.Parameters.AddWithValue("@u", user.Username);
                cmd.Parameters.AddWithValue("@p", hashed);
                cmd.Parameters.AddWithValue("@f", (object?)user.FullName ?? "");
                cmd.Parameters.AddWithValue("@e", (object?)user.Email ?? "");
                cmd.Parameters.AddWithValue("@ph", (object?)user.Phone ?? "");
                cmd.Parameters.AddWithValue("@r", (object?)user.Role ?? "Utilisateur");
                cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", user.Id);
                cmd.ExecuteNonQuery();
            }
            else
            {
                update = @"UPDATE Users SET Username=@u, FullName=@f, Email=@e, 
                          Phone=@ph, Role=@r, IsActive=@a WHERE Id=@id;";
                using var cmd = new SqliteCommand(update, connection);
                cmd.Parameters.AddWithValue("@u", user.Username);
                cmd.Parameters.AddWithValue("@f", (object?)user.FullName ?? "");
                cmd.Parameters.AddWithValue("@e", (object?)user.Email ?? "");
                cmd.Parameters.AddWithValue("@ph", (object?)user.Phone ?? "");
                cmd.Parameters.AddWithValue("@r", (object?)user.Role ?? "Utilisateur");
                cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", user.Id);
                cmd.ExecuteNonQuery();
            }

            return (true, "Utilisateur mis à jour avec succès");
        }

        public static (bool Success, string Message) DeleteUser(int id)
        {
            // Empêcher la suppression de l'admin principal
            if (id == 1)
                return (false, "Impossible de supprimer l'administrateur principal");

            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            string delete = "DELETE FROM Users WHERE Id = @id;";
            using var cmd = new SqliteCommand(delete, connection);
            cmd.Parameters.AddWithValue("@id", id);
            int rows = cmd.ExecuteNonQuery();

            return rows > 0
                ? (true, "Utilisateur supprimé avec succès")
                : (false, "Utilisateur introuvable");
        }

        public static (bool Success, string Message) ToggleUserStatus(int id)
        {
            if (id == 1)
                return (false, "Impossible de désactiver l'administrateur principal");

            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            string toggle = "UPDATE Users SET IsActive = 1 - IsActive WHERE Id = @id;";
            using var cmd = new SqliteCommand(toggle, connection);
            cmd.Parameters.AddWithValue("@id", id);
            int rows = cmd.ExecuteNonQuery();

            return rows > 0
                ? (true, "Statut modifié avec succès")
                : (false, "Utilisateur introuvable");
        }
    }
}