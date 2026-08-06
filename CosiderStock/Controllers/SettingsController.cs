using CosiderStock.Models;
using Microsoft.AspNetCore.Mvc;

namespace CosiderStock.Controllers
{
    public class SettingsController : Controller
    {
        // ==================== INDEX (Redirection) ====================
        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            return RedirectToAction("Database");
        }

        // ==================== DATABASE ====================
        public IActionResult Database()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            var model = new SettingsViewModel();
            model.RootPath = @"C:\PCSTOCK"; // Chemin FIXE
            model.SelectedYear = SettingsHelper.GetSetting("SelectedYear");
            model.SelectedYearPath = SettingsHelper.GetSetting("SelectedYearPath");
            model.PathExists = Directory.Exists(model.RootPath);

            if (model.PathExists)
                model.AvailableYears = SettingsHelper.GetYearFolders(model.RootPath);
            else
                model.ErrorMessage = "Le dossier racine n'existe pas: " + model.RootPath;

            ViewBag.ActiveSection = "database";
            return View(model);
        }

        // ==================== USERS ====================
        public IActionResult Users()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            var users = UserHelper.GetAllUsers();
            ViewBag.ActiveSection = "users";
            return View(users);
        }

        // ==================== SUPPORT ====================
        public IActionResult Support()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveSection = "support";
            return View();
        }

        // ==================== ABOUT ====================
        public IActionResult About()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            ViewBag.ActiveSection = "about";
            return View();
        }

        // ==================== ACTIONS: YEAR SELECTION ====================
        [HttpPost]
        public JsonResult SelectYear(string year, string fullPath)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false, message = "Non autorisé" });

            if (string.IsNullOrEmpty(year) || string.IsNullOrEmpty(fullPath))
                return Json(new { success = false, message = "Année invalide" });

            if (!Directory.Exists(fullPath))
                return Json(new { success = false, message = "Le dossier n'existe pas" });

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            SettingsHelper.SetSetting("SelectedYear", year, userId);
            SettingsHelper.SetSetting("SelectedYearPath", fullPath, userId);

            HttpContext.Session.SetString("SelectedYear", year);
            HttpContext.Session.SetString("SelectedYearPath", fullPath);

            return Json(new { success = true, message = $"Année {year} sélectionnée", year, path = fullPath });
        }

        [HttpPost]
        public JsonResult CreateYearFolder(string year)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false, message = "Non autorisé" });

            if (string.IsNullOrEmpty(year) || year.Length != 4 || !int.TryParse(year, out int y) || y < 1990 || y > 2100)
                return Json(new { success = false, message = "L'année doit être un nombre à 4 chiffres (1990-2100)" });

            string rootPath = @"C:\PCSTOCK";
            if (!Directory.Exists(rootPath))
            {
                try { Directory.CreateDirectory(rootPath); }
                catch { return Json(new { success = false, message = "Impossible de créer le dossier racine" }); }
            }

            string newFolder = Path.Combine(rootPath, year);
            if (Directory.Exists(newFolder))
                return Json(new { success = false, message = "Ce dossier existe déjà" });

            try
            {
                Directory.CreateDirectory(newFolder);
                return Json(new { success = true, message = "Dossier créé: " + year });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Erreur: " + ex.Message });
            }
        }

        // ==================== ACTIONS: USERS ====================
        [HttpPost]
        public JsonResult SaveUser(User user, bool changePassword = false)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false, message = "Non autorisé" });

            if (string.IsNullOrWhiteSpace(user.Username))
                return Json(new { success = false, message = "Le nom d'utilisateur est requis" });

            if (user.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(user.Password) || user.Password.Length < 4)
                    return Json(new { success = false, message = "Mot de passe requis (min. 4 caractères)" });

                var result = UserHelper.CreateUser(user);
                return Json(new { success = result.Success, message = result.Message });
            }
            else
            {
                if (changePassword && (string.IsNullOrWhiteSpace(user.Password) || user.Password.Length < 4))
                    return Json(new { success = false, message = "Mot de passe invalide (min. 4 caractères)" });

                var result = UserHelper.UpdateUser(user, changePassword);
                return Json(new { success = result.Success, message = result.Message });
            }
        }

        [HttpGet]
        public JsonResult GetUser(int id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false });

            var user = UserHelper.GetUserById(id);
            if (user == null)
                return Json(new { success = false, message = "Utilisateur introuvable" });

            return Json(new
            {
                success = true,
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    fullName = user.FullName,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    isActive = user.IsActive
                }
            });
        }

        [HttpPost]
        public JsonResult DeleteUser(int id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false, message = "Non autorisé" });

            var result = UserHelper.DeleteUser(id);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        public JsonResult ToggleUserStatus(int id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return Json(new { success = false, message = "Non autorisé" });

            var result = UserHelper.ToggleUserStatus(id);
            return Json(new { success = result.Success, message = result.Message });
        }
    }
}