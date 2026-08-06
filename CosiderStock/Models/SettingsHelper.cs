using Microsoft.Data.Sqlite;

namespace CosiderStock.Models
{
    public static class SettingsHelper
    {
        public static void InitializeSettingsTable()
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            string createTable = @"
                CREATE TABLE IF NOT EXISTS AppSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SettingKey TEXT NOT NULL UNIQUE,
                    SettingValue TEXT,
                    UpdatedBy INTEGER,
                    UpdatedDate TEXT DEFAULT (datetime('now','localtime'))
                );";

            using (var cmd = new SqliteCommand(createTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            InsertDefaultSetting(connection, "RootPath", @"C:\PCSTOCK");
            InsertDefaultSetting(connection, "SelectedYear", "");
            InsertDefaultSetting(connection, "SelectedYearPath", "");
        }

        private static void InsertDefaultSetting(SqliteConnection connection, string key, string value)
        {
            string check = "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = @key;";
            using var cmd = new SqliteCommand(check, connection);
            cmd.Parameters.AddWithValue("@key", key);
            long count = (long)(cmd.ExecuteScalar() ?? 0L);
            if (count == 0)
            {
                string insert = "INSERT INTO AppSettings (SettingKey, SettingValue) VALUES (@key, @value);";
                using var insertCmd = new SqliteCommand(insert, connection);
                insertCmd.Parameters.AddWithValue("@key", key);
                insertCmd.Parameters.AddWithValue("@value", value);
                insertCmd.ExecuteNonQuery();
            }
        }

        public static string GetSetting(string key)
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();
            string query = "SELECT SettingValue FROM AppSettings WHERE SettingKey = @key;";
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? "";
        }

        public static void SetSetting(string key, string value, int? userId = null)
        {
            using var connection = new SqliteConnection(DatabaseHelper.ConnectionString);
            connection.Open();

            string check = "SELECT COUNT(*) FROM AppSettings WHERE SettingKey = @key;";
            long exists = 0;
            using (var checkCmd = new SqliteCommand(check, connection))
            {
                checkCmd.Parameters.AddWithValue("@key", key);
                exists = (long)(checkCmd.ExecuteScalar() ?? 0L);
            }

            string query;
            if (exists > 0)
            {
                query = @"UPDATE AppSettings 
                         SET SettingValue = @value, UpdatedBy = @userId, UpdatedDate = datetime('now','localtime')
                         WHERE SettingKey = @key;";
            }
            else
            {
                query = @"INSERT INTO AppSettings (SettingKey, SettingValue, UpdatedBy) 
                         VALUES (@key, @value, @userId);";
            }

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value ?? "");
            cmd.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public static List<YearFolder> GetYearFolders(string rootPath)
        {
            var years = new List<YearFolder>();

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return years;
            }

            try
            {
                var selectedYear = GetSetting("SelectedYear");
                var directories = Directory.GetDirectories(rootPath);

                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    string folderName = dirInfo.Name;

                    bool isYearFormat = folderName.Length == 4 &&
                                        int.TryParse(folderName, out int year) &&
                                        year >= 1990 && year <= 2100;

                    var yearFolder = new YearFolder
                    {
                        Year = folderName,
                        FullPath = dirInfo.FullName,
                        LastModified = dirInfo.LastWriteTime,
                        IsSelected = folderName == selectedYear,
                        IsValid = isYearFormat
                    };

                    try
                    {
                        var dbfFiles = dirInfo.GetFiles("*.dbf", SearchOption.TopDirectoryOnly);
                        yearFolder.FileCount = dbfFiles.Length;
                        yearFolder.TotalSize = dbfFiles.Sum(f => f.Length);
                    }
                    catch { }

                    years.Add(yearFolder);
                }

                years = years.OrderByDescending(y => y.Year).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erreur lecture dossiers: " + ex.Message);
            }

            return years;
        }

        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}