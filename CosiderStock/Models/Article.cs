namespace CosiderStock.Models
{
    public class Article
    {
        public int RowIndex { get; set; }

        // ==================== IDENTIFICATION ====================
        public string Ref { get; set; } = "";
        public string Intitule { get; set; } = "";
        public string Intitule2 { get; set; } = "";
        public string Intitule3 { get; set; } = "";
        public string Famille { get; set; } = "";
        public string LieuStockage { get; set; } = "";
        public string Casier { get; set; } = "";

        // ==================== STOCK & PRIX ====================
        public decimal Qte { get; set; }
        public string Unite { get; set; } = "";
        public decimal Pamp { get; set; }
        public decimal PrixAchat { get; set; }
        public decimal ValeurIni { get; set; }
        public decimal StockIni { get; set; }
        public decimal StockMax { get; set; }
        public decimal StockSecurite { get; set; }
        public DateTime? DateMaj { get; set; }
        public decimal Montant => Math.Round(Pamp * Qte, 2);

        // ==================== STATISTIQUES ====================
        public decimal TotalMvtEntrees { get; set; }  // Total mouvements entrées
        public decimal TotalMvtSorties { get; set; }  // Total mouvements sorties
        public decimal QteEntrees { get; set; }        // Quantité cumulée entrées
        public decimal QteSorties { get; set; }        // Quantité cumulée sorties
        public decimal ValeurResiduelle { get; set; }  // TEMPVAL
        public decimal AchatHT { get; set; }
        public decimal ConsoHT { get; set; }
        public decimal CessionHT { get; set; }
        public decimal ReinteHT { get; set; }
    }

    public class ArticlesViewModel
    {
        public List<Article> Articles { get; set; } = new();
        public string SelectedYear { get; set; } = "";
        public string DbfPath { get; set; } = "";
        public bool FileExists { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalArticles => Articles.Count;
        public decimal TotalMontant => Articles.Sum(a => a.Montant);
        public decimal TotalValeurIni => Articles.Sum(a => a.ValeurIni);
        public decimal TotalQte => Articles.Sum(a => a.Qte);
        public decimal TotalStockIni => Articles.Sum(a => a.StockIni);
        public List<string> Familles => Articles.Select(a => a.Famille).Where(f => !string.IsNullOrEmpty(f)).Distinct().OrderBy(f => f).ToList();
        public List<string> Unites => Articles.Select(a => a.Unite).Where(u => !string.IsNullOrEmpty(u)).Distinct().OrderBy(u => u).ToList();

        public List<string> AvailableFields { get; set; } = new();
        public int TotalRecordsInFile { get; set; }
        public string FileSizeFormatted { get; set; } = "";
        public string ReadMethod { get; set; } = "";
    }
}