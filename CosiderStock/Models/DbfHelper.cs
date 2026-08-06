using System.Data;
using System.Data.OleDb;
using System.Runtime.InteropServices;
using System.Text;

namespace CosiderStock.Models
{
    public static class DbfHelper
    {
        // ==================== MÉTHODE PRINCIPALE ====================
        public static ArticlesViewModel LoadArticles(string yearPath)
        {
            var model = new ArticlesViewModel();

            if (string.IsNullOrEmpty(yearPath))
            {
                model.ErrorMessage = "Aucun exercice sélectionné.";
                return model;
            }

            string dbfPath = Path.Combine(yearPath, "ST_STOCK.DBF");
            model.DbfPath = dbfPath;
            model.SelectedYear = Path.GetFileName(yearPath);

            if (!File.Exists(dbfPath))
            {
                model.FileExists = false;
                model.ErrorMessage = $"Fichier introuvable: {dbfPath}";
                return model;
            }

            model.FileExists = true;
            var fileInfo = new FileInfo(dbfPath);
            model.FileSizeFormatted = FormatSize(fileInfo.Length);

            // Enregistrer les encodages
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // ==================== ESSAI 1: Lecture native (méthode robuste) ====================
            try
            {
                var records = ReadDbfNative(dbfPath);
                model.TotalRecordsInFile = records.Count;

                if (records.Count > 0)
                {
                    model.AvailableFields = records[0].Keys.ToList();
                    model.ReadMethod = "Lecture native binaire";

                    int idx = 0;
                    foreach (var rec in records)
                    {
                        idx++;
                        var article = MapToArticle(rec, idx);
                        if (!string.IsNullOrWhiteSpace(article.Ref) || !string.IsNullOrWhiteSpace(article.Intitule))
                        {
                            model.Articles.Add(article);
                        }
                    }

                    if (model.Articles.Count > 0)
                        return model;
                }
            }
            catch (Exception ex)
            {
                model.ErrorMessage = "Lecture native échouée: " + ex.Message;
            }

            // ==================== ESSAI 2: OleDb (si Ace/Jet installé) ====================
            try
            {
                var records = ReadDbfOleDb(dbfPath);
                model.TotalRecordsInFile = records.Count;

                if (records.Count > 0)
                {
                    model.AvailableFields = records[0].Keys.ToList();
                    model.ReadMethod = "OleDb (Access/Jet)";
                    model.ErrorMessage = null;

                    int idx = 0;
                    foreach (var rec in records)
                    {
                        idx++;
                        var article = MapToArticle(rec, idx);
                        if (!string.IsNullOrWhiteSpace(article.Ref) || !string.IsNullOrWhiteSpace(article.Intitule))
                        {
                            model.Articles.Add(article);
                        }
                    }
                }
            }
            catch (Exception ex2)
            {
                if (string.IsNullOrEmpty(model.ErrorMessage))
                    model.ErrorMessage = "Erreur OleDb: " + ex2.Message;
                else
                    model.ErrorMessage += " | OleDb: " + ex2.Message;
            }

            return model;
        }

        // ==================== LECTURE NATIVE BINAIRE (RECOMMANDÉE) ====================
        private static List<Dictionary<string, string>> ReadDbfNative(string filePath)
        {
            var records = new List<Dictionary<string, string>>();

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            // Lire l'en-tête (32 octets)
            byte version = reader.ReadByte();
            reader.ReadBytes(3); // Date de dernière MAJ
            uint numRecords = reader.ReadUInt32();
            ushort headerSize = reader.ReadUInt16();
            ushort recordSize = reader.ReadUInt16();
            reader.ReadBytes(20); // Réservé

            // Lire les descripteurs de champs
            var fields = new List<DbfFieldInfo>();
            while (reader.PeekChar() != 0x0D) // 0x0D = fin des descripteurs
            {
                byte[] nameBytes = reader.ReadBytes(11);
                string fieldName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ').ToUpper();
                char fieldType = (char)reader.ReadByte();
                reader.ReadBytes(4); // Adresse en mémoire
                byte fieldLength = reader.ReadByte();
                byte decimalCount = reader.ReadByte();
                reader.ReadBytes(14); // Réservé

                fields.Add(new DbfFieldInfo
                {
                    Name = fieldName,
                    Type = fieldType,
                    Length = fieldLength,
                    DecimalCount = decimalCount
                });
            }
            reader.ReadByte(); // 0x0D terminator

            // Aller au début des données
            fs.Seek(headerSize, SeekOrigin.Begin);

            // Détecter l'encodage (essai plusieurs encodages)
            Encoding encoding = DetectEncoding(version);

            // Lire les enregistrements
            for (int i = 0; i < numRecords; i++)
            {
                if (fs.Position >= fs.Length) break;

                byte deleteFlag = reader.ReadByte();

                if (deleteFlag == 0x2A) // Enregistrement supprimé
                {
                    reader.ReadBytes(recordSize - 1);
                    continue;
                }

                var record = new Dictionary<string, string>();
                foreach (var field in fields)
                {
                    byte[] valueBytes = reader.ReadBytes(field.Length);
                    string value = encoding.GetString(valueBytes).Trim().TrimEnd('\0');
                    record[field.Name] = value;
                }

                records.Add(record);
            }

            return records;
        }

        // ==================== LECTURE OLEDB (BACKUP) ====================
        private static List<Dictionary<string, string>> ReadDbfOleDb(string filePath)
        {
            var records = new List<Dictionary<string, string>>();
            string folder = Path.GetDirectoryName(filePath) ?? "";
            string fileName = Path.GetFileName(filePath);

            string[] providers = new[]
            {
                $"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={folder};Extended Properties=\"dBASE IV;\"",
                $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={folder};Extended Properties=\"dBASE IV;\"",
                $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={folder};Extended Properties=\"dBASE IV;\""
            };

            Exception? lastEx = null;

            foreach (var providerStr in providers)
            {
                try
                {
                    using var connection = new OleDbConnection(providerStr);
                    connection.Open();

                    string tableName = Path.GetFileNameWithoutExtension(fileName);
                    string query = $"SELECT * FROM [{tableName}]";

                    using var cmd = new OleDbCommand(query, connection);
                    using var reader = cmd.ExecuteReader();

                    var columnNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columnNames.Add(reader.GetName(i).ToUpper());
                    }

                    while (reader.Read())
                    {
                        var record = new Dictionary<string, string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.GetValue(i);
                            record[columnNames[i]] = val == DBNull.Value ? "" : val.ToString()?.Trim() ?? "";
                        }
                        records.Add(record);
                    }

                    return records;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    continue;
                }
            }

            if (lastEx != null) throw lastEx;
            return records;
        }

        // ==================== MAPPING ====================
        //private static Article MapToArticle(Dictionary<string, string> rec, int index)
        //{
        //    return new Article
        //    {
        //        RowIndex = index,
        //        Ref = GetString(rec, "REF"),
        //        Intitule = GetString(rec, "INTITULE"),
        //        Intitule2 = GetString(rec, "INTITULE2"),
        //        Intitule3 = GetString(rec, "INTITULE3"),
        //        Famille = GetString(rec, "FAMILLE"),
        //        Qte = GetDecimal(rec, "QTE"),
        //        Unite = GetString(rec, "UNITE"),
        //        Pamp = GetDecimal(rec, "PAMP"),
        //        StockIni = GetDecimal(rec, "STOCK_INI"),
        //        ValeurIni = GetDecimal(rec, "VALEUR_INI"),
        //        Casier = GetString(rec, "CASIER"),
        //        DateMaj = GetDate(rec, "DATE_MAJ")
        //    };
        //}

        private static Article MapToArticle(Dictionary<string, string> rec, int index)
        {
            return new Article
            {
                RowIndex = index,

                // Identification
                Ref = GetString(rec, "REF"),
                Intitule = GetString(rec, "INTITULE"),
                Intitule2 = GetString(rec, "INTITULE2"),
                Intitule3 = GetString(rec, "INTITULE3"),
                Famille = GetString(rec, "FAMILLE"),
                LieuStockage = GetStringMulti(rec, "LIEU_STOCK", "LIEUSTOCK", "LIEU", "STOCKAGE"),
                Casier = GetString(rec, "CASIER"),

                // Stock & Prix
                Qte = GetDecimal(rec, "QTE"),
                Unite = GetString(rec, "UNITE"),
                Pamp = GetDecimal(rec, "PAMP"),
                PrixAchat = GetDecimalMulti(rec, "PRIX_ACHAT", "PRIXACHAT", "PA", "PACHAT"),
                StockIni = GetDecimal(rec, "STOCK_INI"),
                ValeurIni = GetDecimal(rec, "VALEUR_INI"),
                StockMax = GetDecimalMulti(rec, "STOCK_MAX", "STOCKMAX", "QTE_MAX"),
                StockSecurite = GetDecimalMulti(rec, "STOCK_SEC", "STOCKSEC", "SECURITE", "STOCK_SECU"),
                DateMaj = GetDate(rec, "DATE_MAJ"),

                // Statistiques
                TotalMvtEntrees = GetDecimalMulti(rec, "TOT_MVT_E", "TOTMVTE", "NB_ENT", "TOTENT"),
                TotalMvtSorties = GetDecimalMulti(rec, "TOT_MVT_S", "TOTMVTS", "NB_SOR", "TOTSOR"),
                QteEntrees = GetDecimalMulti(rec, "QTE_ENT", "QTEENT", "QTE_E"),
                QteSorties = GetDecimalMulti(rec, "QTE_SOR", "QTESOR", "QTE_S"),
                ValeurResiduelle = GetDecimalMulti(rec, "TEMPVAL", "VAL_RES", "VALRES"),
                AchatHT = GetDecimalMulti(rec, "ACHAT_HT", "ACHATHT", "ACHAT"),
                ConsoHT = GetDecimalMulti(rec, "CONSO_HT", "CONSOHT", "CONSO"),
                CessionHT = GetDecimalMulti(rec, "CESSION_HT", "CESSIONHT", "CESSION"),
                ReinteHT = GetDecimalMulti(rec, "REINTE_HT", "REINTEHT", "REINTE", "REINT_HT")
            };
        }

        // Nouvelles méthodes helper
        private static string GetStringMulti(Dictionary<string, string> rec, params string[] keys)
        {
            foreach (var key in keys)
            {
                var val = GetString(rec, key);
                if (!string.IsNullOrEmpty(val)) return val;
            }
            return "";
        }

        private static decimal GetDecimalMulti(Dictionary<string, string> rec, params string[] keys)
        {
            foreach (var key in keys)
            {
                var val = GetDecimal(rec, key);
                if (val != 0) return val;
            }
            return 0;
        }

        private static string GetString(Dictionary<string, string> rec, string key)
        {
            key = key.ToUpper();
            if (rec.ContainsKey(key)) return rec[key]?.Trim() ?? "";
            // Essayer variations
            foreach (var k in rec.Keys)
            {
                if (k.Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return rec[k]?.Trim() ?? "";
            }
            return "";
        }

        private static decimal GetDecimal(Dictionary<string, string> rec, string key)
        {
            string val = GetString(rec, key);
            if (string.IsNullOrWhiteSpace(val)) return 0;
            val = val.Replace(",", ".").Trim();
            if (decimal.TryParse(val, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                return result;
            return 0;
        }

        private static DateTime? GetDate(Dictionary<string, string> rec, string key)
        {
            string val = GetString(rec, key);
            if (string.IsNullOrWhiteSpace(val)) return null;

            // Format DBF: YYYYMMDD
            if (val.Length == 8 && DateTime.TryParseExact(val, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime parsed))
                return parsed;

            if (DateTime.TryParse(val, out DateTime parsed2))
                return parsed2;

            return null;
        }

        // ==================== DÉTECTION D'ENCODAGE ====================
        private static Encoding DetectEncoding(byte version)
        {
            try
            {
                // DBF standard = CP850 (français) ou Windows-1252
                return Encoding.GetEncoding(1252);
            }
            catch
            {
                return Encoding.GetEncoding("ISO-8859-1");
            }
        }

        // ==================== UTILITAIRES ====================
        private static string FormatSize(long bytes)
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

        // ==================== CLASSES INTERNES ====================
        private class DbfFieldInfo
        {
            public string Name { get; set; } = "";
            public char Type { get; set; }
            public byte Length { get; set; }
            public byte DecimalCount { get; set; }
        }
    }
}