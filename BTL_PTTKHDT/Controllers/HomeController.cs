using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Security;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BTL_PTTKHDT.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly QltdnhContext _db;

        public HomeController(IDashboardService dashboardService, QltdnhContext db)
        {
            _dashboardService = dashboardService;
            _db = db;
        }

        [PermissionAuthorize(AppPermissions.ViewDashboard)]
        public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken)
        {
            var dashboard = await _dashboardService.GetDashboardAsync(year, cancellationToken);
            return View(dashboard);
        }

        public IActionResult Privacy()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Privacy(ChangePasswordViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var maTaiKhoan = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(maTaiKhoan))
            {
                return RedirectToAction("Login", "Account");
            }

            var account = await _db.TaiKhoanNhanViens.FirstOrDefaultAsync(x => x.MaTaiKhoan == maTaiKhoan, cancellationToken);
            if (account == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!PasswordHashing.Verify(model.CurrentPassword, account.MatKhauHash))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mat khau cu khong dung.");
                return View(model);
            }

            account.MatKhauHash = PasswordHashing.Hash(model.NewPassword);
            account.SoLanSaiMatKhau = 0;
            account.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);

            TempData["PasswordChanged"] = "Doi mat khau thanh cong.";
            return RedirectToAction(nameof(Privacy));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
