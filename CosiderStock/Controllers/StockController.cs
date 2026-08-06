using CosiderStock.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace CosiderStock.Controllers
{
    public class StockController : Controller
    {
        // Cache en mémoire pour éviter de relire le DBF à chaque requête
        private static Dictionary<string, (DateTime LoadedAt, ArticlesViewModel Data)> _cache = new();
        private static readonly object _cacheLock = new();

        public IActionResult Articles()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            string yearPath = HttpContext.Session.GetString("SelectedYearPath") ?? "";
            var model = GetCachedOrLoad(yearPath);

            ViewBag.SelectedYear = HttpContext.Session.GetString("SelectedYear") ?? "";
            return View(model);
        }

        // ==================== API AJAX pour filtrage/pagination ====================
        [HttpPost]
        public JsonResult GetArticlesPage(int page = 1, int pageSize = 100,
            string search = "", string famille = "", string unite = "", string stock = "",
            string sortBy = "", string sortDir = "asc")
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false });

            string yearPath = HttpContext.Session.GetString("SelectedYearPath") ?? "";
            var model = GetCachedOrLoad(yearPath);

            var query = model.Articles.AsQueryable();

            // Filtres
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.ToLower();
                query = query.Where(a =>
                    (a.Ref ?? "").ToLower().Contains(s) ||
                    (a.Intitule ?? "").ToLower().Contains(s) ||
                    (a.Intitule2 ?? "").ToLower().Contains(s) ||
                    (a.Intitule3 ?? "").ToLower().Contains(s) ||
                    (a.Casier ?? "").ToLower().Contains(s) ||
                    (a.Famille ?? "").ToLower().Contains(s));
            }

            if (!string.IsNullOrEmpty(famille))
                query = query.Where(a => a.Famille == famille);

            if (!string.IsNullOrEmpty(unite))
                query = query.Where(a => a.Unite == unite);

            if (stock == "positive") query = query.Where(a => a.Qte > 0);
            else if (stock == "zero") query = query.Where(a => a.Qte == 0);
            else if (stock == "montant") query = query.Where(a => a.Montant > 0);

            // Tri
            query = ApplySort(query, sortBy, sortDir);

            var filtered = query.ToList();
            int total = filtered.Count;

            // Totaux filtrés
            decimal totalQte = filtered.Sum(a => a.Qte);
            decimal totalMontant = filtered.Sum(a => a.Montant);
            decimal totalValeurIni = filtered.Sum(a => a.ValeurIni);
            decimal totalStockIni = filtered.Sum(a => a.StockIni);

            // Pagination
            int skip = (page - 1) * pageSize;
            var pageData = filtered.Skip(skip).Take(pageSize).ToList();
            int totalPages = (int)Math.Ceiling(total / (double)pageSize);

            return Json(new
            {
                success = true,
                data = pageData.Select(a => new
                {
                    rowIndex = a.RowIndex,
                    reference = a.Ref,
                    intitule = a.Intitule,
                    intitule2 = a.Intitule2,
                    intitule3 = a.Intitule3,
                    famille = a.Famille,
                    qte = a.Qte,
                    qteFmt = a.Qte.ToString("N2"),
                    unite = a.Unite,
                    pamp = a.Pamp,
                    pampFmt = a.Pamp.ToString("N2"),
                    montant = a.Montant,
                    montantFmt = a.Montant.ToString("N2"),
                    stockIni = a.StockIni,
                    stockIniFmt = a.StockIni.ToString("N2"),
                    valeurIni = a.ValeurIni,
                    valeurIniFmt = a.ValeurIni.ToString("N2"),
                    casier = a.Casier,
                    dateMaj = a.DateMaj?.ToString("dd/MM/yyyy") ?? "—"
                }),
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalRecords = total,
                    totalPages = totalPages,
                    showingFrom = total == 0 ? 0 : skip + 1,
                    showingTo = Math.Min(skip + pageSize, total)
                },
                totals = new
                {
                    qte = totalQte.ToString("N2"),
                    montant = totalMontant.ToString("N2"),
                    stockIni = totalStockIni.ToString("N2"),
                    valeurIni = totalValeurIni.ToString("N2")
                }
            });
        }

        private IQueryable<Article> ApplySort(IQueryable<Article> query, string sortBy, string sortDir)
        {
            bool asc = sortDir != "desc";
            switch (sortBy?.ToLower())
            {
                case "ref": return asc ? query.OrderBy(a => a.Ref) : query.OrderByDescending(a => a.Ref);
                case "intitule": return asc ? query.OrderBy(a => a.Intitule) : query.OrderByDescending(a => a.Intitule);
                case "famille": return asc ? query.OrderBy(a => a.Famille) : query.OrderByDescending(a => a.Famille);
                case "qte": return asc ? query.OrderBy(a => a.Qte) : query.OrderByDescending(a => a.Qte);
                case "pamp": return asc ? query.OrderBy(a => a.Pamp) : query.OrderByDescending(a => a.Pamp);
                case "montant": return asc ? query.OrderBy(a => a.Montant) : query.OrderByDescending(a => a.Montant);
                case "stockini": return asc ? query.OrderBy(a => a.StockIni) : query.OrderByDescending(a => a.StockIni);
                case "valeurini": return asc ? query.OrderBy(a => a.ValeurIni) : query.OrderByDescending(a => a.ValeurIni);
                case "casier": return asc ? query.OrderBy(a => a.Casier) : query.OrderByDescending(a => a.Casier);
                case "datemaj": return asc ? query.OrderBy(a => a.DateMaj) : query.OrderByDescending(a => a.DateMaj);
                default: return query.OrderBy(a => a.RowIndex);
            }
        }

        // ==================== CACHE ====================
        private ArticlesViewModel GetCachedOrLoad(string yearPath)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(yearPath, out var cached))
                {
                    // Cache valide 10 minutes
                    if ((DateTime.Now - cached.LoadedAt).TotalMinutes < 10)
                        return cached.Data;
                }

                var model = DbfHelper.LoadArticles(yearPath);
                _cache[yearPath] = (DateTime.Now, model);
                return model;
            }
        }

        // ==================== RECHARGER CACHE ====================
        public IActionResult Refresh()
        {
            string yearPath = HttpContext.Session.GetString("SelectedYearPath") ?? "";
            lock (_cacheLock)
            {
                _cache.Remove(yearPath);
            }
            return RedirectToAction("Articles");
        }

        // ==================== EXPORT EXCEL ====================
        public IActionResult ExportExcel(string search = "", string famille = "", string unite = "", string stock = "")
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            string yearPath = HttpContext.Session.GetString("SelectedYearPath") ?? "";
            var model = GetCachedOrLoad(yearPath);

            if (!model.FileExists || model.Articles.Count == 0)
                return RedirectToAction("Articles");

            var query = model.Articles.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.ToLower();
                query = query.Where(a =>
                    (a.Ref ?? "").ToLower().Contains(s) ||
                    (a.Intitule ?? "").ToLower().Contains(s) ||
                    (a.Intitule2 ?? "").ToLower().Contains(s) ||
                    (a.Intitule3 ?? "").ToLower().Contains(s) ||
                    (a.Casier ?? "").ToLower().Contains(s) ||
                    (a.Famille ?? "").ToLower().Contains(s));
            }
            if (!string.IsNullOrEmpty(famille)) query = query.Where(a => a.Famille == famille);
            if (!string.IsNullOrEmpty(unite)) query = query.Where(a => a.Unite == unite);
            if (stock == "positive") query = query.Where(a => a.Qte > 0);
            else if (stock == "zero") query = query.Where(a => a.Qte == 0);
            else if (stock == "montant") query = query.Where(a => a.Montant > 0);

            var articles = query.ToList();

            var sb = new StringBuilder();
            sb.AppendLine("N°;REF;INTITULE;INTITULE2;INTITULE3;FAMILLE;QTE;UNITE;PAMP;MONTANT;STOCK_INI;VALEUR_INI;CASIER;DATE_MAJ");

            foreach (var a in articles)
            {
                sb.AppendLine(string.Join(";",
                    a.RowIndex,
                    EscapeCsv(a.Ref),
                    EscapeCsv(a.Intitule),
                    EscapeCsv(a.Intitule2),
                    EscapeCsv(a.Intitule3),
                    EscapeCsv(a.Famille),
                    a.Qte.ToString("F2"),
                    EscapeCsv(a.Unite),
                    a.Pamp.ToString("F2"),
                    a.Montant.ToString("F2"),
                    a.StockIni.ToString("F2"),
                    a.ValeurIni.ToString("F2"),
                    EscapeCsv(a.Casier),
                    a.DateMaj?.ToString("dd/MM/yyyy") ?? ""
                ));
            }

            sb.AppendLine(string.Join(";",
                "", "", "TOTAL", "", "", "",
                articles.Sum(a => a.Qte).ToString("F2"), "",
                "", articles.Sum(a => a.Montant).ToString("F2"),
                articles.Sum(a => a.StockIni).ToString("F2"),
                articles.Sum(a => a.ValeurIni).ToString("F2"),
                "", ""
            ));

            string year = HttpContext.Session.GetString("SelectedYear") ?? "Export";
            string fileName = $"Articles_Stock_{year}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] content = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] result = new byte[bom.Length + content.Length];
            bom.CopyTo(result, 0);
            content.CopyTo(result, bom.Length);

            return File(result, "text/csv;charset=utf-8", fileName);
        }

        private string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            if (val.Contains(";") || val.Contains("\"") || val.Contains("\n"))
                return "\"" + val.Replace("\"", "\"\"") + "\"";
            return val;
        }

        // ==================== DÉTAILS ARTICLE ====================
        [HttpGet]
        public JsonResult GetArticleDetails(int rowIndex)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false });

            string yearPath = HttpContext.Session.GetString("SelectedYearPath") ?? "";
            var model = GetCachedOrLoad(yearPath);

            var article = model.Articles.FirstOrDefault(a => a.RowIndex == rowIndex);
            if (article == null)
                return Json(new { success = false, message = "Article introuvable" });

            return Json(new
            {
                success = true,
                article = new
                {
                    // Identification
                    reference = article.Ref,
                    intitule = article.Intitule,
                    intitule2 = article.Intitule2,
                    intitule3 = article.Intitule3,
                    famille = article.Famille,
                    lieuStockage = article.LieuStockage,
                    casier = article.Casier,

                    // Stock & Prix
                    qte = article.Qte.ToString("N2"),
                    unite = article.Unite,
                    pamp = article.Pamp.ToString("N4"),
                    prixAchat = article.PrixAchat.ToString("N2"),
                    valeurIni = article.ValeurIni.ToString("N2"),
                    stockIni = article.StockIni.ToString("N2"),
                    stockMax = article.StockMax.ToString("N2"),
                    stockSecurite = article.StockSecurite.ToString("N2"),
                    dateMaj = article.DateMaj?.ToString("dd/MM/yyyy") ?? "—",
                    montant = article.Montant.ToString("N2"),

                    // Statistiques
                    totalMvtEntrees = article.TotalMvtEntrees.ToString("N2"),
                    totalMvtSorties = article.TotalMvtSorties.ToString("N2"),
                    qteEntrees = article.QteEntrees.ToString("N2"),
                    qteSorties = article.QteSorties.ToString("N2"),
                    valeurResiduelle = article.ValeurResiduelle.ToString("N2"),
                    achatHT = article.AchatHT.ToString("N2"),
                    consoHT = article.ConsoHT.ToString("N2"),
                    cessionHT = article.CessionHT.ToString("N2"),
                    reinteHT = article.ReinteHT.ToString("N2")
                }
            });
        }
    }
}