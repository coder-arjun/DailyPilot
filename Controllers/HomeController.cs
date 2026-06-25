using DailyPilot.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DailyPilot.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Logged-in users go straight to their day; visitors see the landing page.
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Tasks");

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
