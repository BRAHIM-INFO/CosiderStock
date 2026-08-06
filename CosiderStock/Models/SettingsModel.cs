using System.ComponentModel.DataAnnotations;

namespace CosiderStock.Models
{
    public class YearFolder
    {
        public string Year { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
        public bool IsSelected { get; set; }
        public bool IsValid { get; set; }
    }

    public class SettingsViewModel
    {
        [Display(Name = "Chemin racine")]
        public string RootPath { get; set; } = @"C:\PCSTOCK";

        public List<YearFolder> AvailableYears { get; set; } = new();

        public string SelectedYear { get; set; } = string.Empty;
        public string SelectedYearPath { get; set; } = string.Empty;
        public bool PathExists { get; set; }
        public string? ErrorMessage { get; set; }
    }
}