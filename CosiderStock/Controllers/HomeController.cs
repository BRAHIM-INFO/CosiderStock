using Microsoft.AspNetCore.Mvc;

namespace CosiderStock.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }
    }
}