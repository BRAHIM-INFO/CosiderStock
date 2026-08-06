using Microsoft.AspNetCore.Mvc;

namespace CosiderStock.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
            ViewBag.Username = HttpContext.Session.GetString("Username");
            return View();
        }
    }
}