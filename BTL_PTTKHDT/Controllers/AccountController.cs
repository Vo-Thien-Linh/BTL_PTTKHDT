using System.Security.Claims;
using System.Security.Cryptography;
using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

[AllowAnonymous]
public sealed class AccountController : Controller
{
    private const string AdminRole = "Quản trị hệ thống";
    private readonly QltdnhContext _db;
    private readonly IEmailSender _emailSender;

    public AccountController(QltdnhContext db, IEmailSender emailSender)
    {
        _db = db;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var loginName = model.TenDangNhap.Trim();
        var phoneNumber = NormalizePhoneNumber(loginName);
        var loginInfo = await (
                from account in _db.TaiKhoanNhanViens.AsNoTracking()
                join employee in _db.NhanViens.AsNoTracking() on account.MaNv equals employee.MaNv
                where employee.SoDienThoai == phoneNumber || account.TenDangNhap == loginName
                select new
                {
                    account.MaTaiKhoan,
                    account.MaNv,
                    account.TenDangNhap,
                    account.MatKhauHash,
                    account.BiKhoa,
                    employee.HoTen,
                    employee.SoDienThoai,
                    employee.VaiTro,
                    employee.IsActive
                })
            .FirstOrDefaultAsync();

        if (loginInfo == null)
        {
            AddLoginError("Không tìm thấy tài khoản quản trị khớp với thông tin đăng nhập.");
            return View(model);
        }

        if (loginInfo.BiKhoa)
        {
            AddLoginError("Tài khoản đang bị khóa. Hãy mở khóa tài khoản trong bảng TaiKhoanNhanVien.");
            return View(model);
        }

        if (!loginInfo.IsActive)
        {
            AddLoginError("Nhân viên của tài khoản này đang bị ngừng hoạt động.");
            return View(model);
        }

        if (!IsAdminRole(loginInfo.VaiTro))
        {
            AddLoginError($"Tài khoản không có quyền quản trị. Vai trò hiện tại: {loginInfo.VaiTro}.");
            return View(model);
        }

        var trackedAccount = await _db.TaiKhoanNhanViens
            .FirstOrDefaultAsync(x => x.MaTaiKhoan == loginInfo.MaTaiKhoan);

        if (trackedAccount == null)
        {
            AddLoginError("Không tìm thấy bản ghi tài khoản để cập nhật trạng thái đăng nhập.");
            return View(model);
        }

        if (!PasswordHashing.Verify(model.MatKhau, loginInfo.MatKhauHash))
        {
            trackedAccount.SoLanSaiMatKhau++;
            if (trackedAccount.SoLanSaiMatKhau >= 5)
            {
                trackedAccount.BiKhoa = true;
            }

            trackedAccount.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync();

            AddLoginError("Mật khẩu không đúng.");
            return View(model);
        }

        trackedAccount.SoLanSaiMatKhau = 0;
        trackedAccount.LanDangNhapCuoi = DateTime.Now;
        trackedAccount.NgayCapNhat = DateTime.Now;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, loginInfo.MaTaiKhoan),
            new(ClaimTypes.Name, loginInfo.HoTen),
            new("MaNV", loginInfo.MaNv),
            new("TenDangNhap", loginInfo.TenDangNhap),
            new("SoDienThoai", loginInfo.SoDienThoai),
            new(ClaimTypes.Role, AdminRole)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var login = model.LoginOrEmail.Trim();
        var normalizedPhone = NormalizePhoneNumber(login);
        var accountInfo = await (
                from account in _db.TaiKhoanNhanViens
                join employee in _db.NhanViens on account.MaNv equals employee.MaNv
                where account.TenDangNhap == login
                    || employee.SoDienThoai == normalizedPhone
                    || employee.Email == login
                select new
                {
                    Account = account,
                    employee.HoTen,
                    employee.Email,
                    employee.IsActive,
                    employee.VaiTro
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (accountInfo == null)
        {
            ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản khớp với thông tin đã nhập.");
            return View(model);
        }

        if (!accountInfo.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Nhân viên của tài khoản này đang bị ngừng hoạt động.");
            return View(model);
        }

        if (!IsAdminRole(accountInfo.VaiTro))
        {
            ModelState.AddModelError(string.Empty, "Chỉ tài khoản quản trị hệ thống được đặt lại mật khẩu ở màn này.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(accountInfo.Email))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản này chưa có email. Hãy cập nhật email nhân viên trước khi dùng quên mật khẩu.");
            return View(model);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        accountInfo.Account.ResetPasswordCodeHash = PasswordHashing.Hash(code);
        accountInfo.Account.ResetPasswordExpiresAt = DateTime.Now.AddMinutes(10);
        accountInfo.Account.NgayCapNhat = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);

        var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { id = accountInfo.Account.MaTaiKhoan }, Request.Scheme);
        var body = $"""
Xin chào {accountInfo.HoTen},

Mã xác nhận đặt lại mật khẩu của bạn là: {code}
Mã có hiệu lực trong 10 phút.

Link đặt lại mật khẩu:
{resetUrl}

Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.
""";

        try
        {
            await _emailSender.SendAsync(accountInfo.Email, "Mã đặt lại mật khẩu hệ thống tín dụng", body, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            accountInfo.Account.ResetPasswordCodeHash = null;
            accountInfo.Account.ResetPasswordExpiresAt = null;
            accountInfo.Account.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);

            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception)
        {
            accountInfo.Account.ResetPasswordCodeHash = null;
            accountInfo.Account.ResetPasswordExpiresAt = null;
            accountInfo.Account.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);

            ModelState.AddModelError(string.Empty, "Không gửi được email đặt lại mật khẩu. Hãy kiểm tra Gmail, App Password hoặc kết nối mạng.");
            return View(model);
        }

        TempData["ForgotPasswordMessage"] = "Mã xác nhận đã được gửi đến email của tài khoản.";
        return RedirectToAction(nameof(ResetPassword), new { id = accountInfo.Account.MaTaiKhoan });
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(ForgotPassword));

        var exists = await _db.TaiKhoanNhanViens.AsNoTracking().AnyAsync(x => x.MaTaiKhoan == id, cancellationToken);
        if (!exists) return RedirectToAction(nameof(ForgotPassword));

        return View(new ResetPasswordViewModel { MaTaiKhoan = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _db.TaiKhoanNhanViens.FirstOrDefaultAsync(x => x.MaTaiKhoan == model.MaTaiKhoan, cancellationToken);
        if (account == null)
        {
            ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(account.ResetPasswordCodeHash)
            || account.ResetPasswordExpiresAt == null
            || account.ResetPasswordExpiresAt < DateTime.Now)
        {
            ModelState.AddModelError(string.Empty, "Mã xác nhận đã hết hạn. Hãy yêu cầu mã mới.");
            return View(model);
        }

        if (!PasswordHashing.Verify(model.Code.Trim(), account.ResetPasswordCodeHash))
        {
            ModelState.AddModelError(nameof(model.Code), "Mã xác nhận không đúng.");
            return View(model);
        }

        account.MatKhauHash = PasswordHashing.Hash(model.NewPassword);
        account.SoLanSaiMatKhau = 0;
        account.BiKhoa = false;
        account.ResetPasswordCodeHash = null;
        account.ResetPasswordExpiresAt = null;
        account.NgayCapNhat = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["LoginMessage"] = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private void AddLoginError(string message)
    {
        ModelState.AddModelError(string.Empty, message);
    }

    private static string NormalizePhoneNumber(string value)
    {
        return value.Trim().Replace(" ", string.Empty).Replace(".", string.Empty).Replace("-", string.Empty);
    }

    private static bool IsAdminRole(string? value)
    {
        var role = value?.Trim();
        return string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Quan tri he thong", StringComparison.OrdinalIgnoreCase);
    }
}
