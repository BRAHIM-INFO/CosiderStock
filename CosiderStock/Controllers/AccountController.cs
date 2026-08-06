using CosiderStock.Models;
using Microsoft.AspNetCore.Mvc;

namespace CosiderStock.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = DatabaseHelper.ValidateUser(model.Username, model.Password);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("FullName", user.FullName ?? "");
                HttpContext.Session.SetString("UserRole", user.Role ?? "");
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");

                // Charger les paramètres
                HttpContext.Session.SetString("SelectedYear", SettingsHelper.GetSetting("SelectedYear"));
                HttpContext.Session.SetString("SelectedYearPath", SettingsHelper.GetSetting("SelectedYearPath"));
                HttpContext.Session.SetString("RootPath", SettingsHelper.GetSetting("RootPath"));

                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                DatabaseHelper.LogLogin(user.Id, ip, true);

                return RedirectToAction("Index", "Dashboard");
            }
            else
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                DatabaseHelper.LogLogin(null, ip, false);

                ModelState.AddModelError("", "Nom d'utilisateur ou mot de passe incorrect");
                ViewBag.LoginError = "Nom d'utilisateur ou mot de passe incorrect";
                return View(model);
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}