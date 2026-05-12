using BTL_PTTKHDT.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using BTL_PTTKHDT.Services;

namespace BTL_PTTKHDT.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public HomeController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public IActionResult Index()
        {
            // Tạm thời bỏ qua Dashboard, redirect thẳng tới trang Khách hàng
            return RedirectToAction("Index", "Customer");
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
