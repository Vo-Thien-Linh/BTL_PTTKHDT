using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Security;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTL_PTTKHDT.Controllers
{
    public sealed class EmployeeController : Controller
    {
        private const string PendingPasswordSessionPrefix = "PendingEmployeePassword";
        private readonly QltdnhContext _db;
        private readonly IWebHostEnvironment _env;

        public EmployeeController(QltdnhContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private async Task<string?> SaveAvatarAsync(IFormFile? file, CancellationToken ct)
        {
            if (file == null || file.Length <= 0) return null;

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowed.Contains(ext)) throw new InvalidOperationException("Chỉ cho phép ảnh .jpg/.jpeg/.png/.gif/.webp");

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot)) throw new InvalidOperationException("WebRootPath chưa được cấu hình (wwwroot)");

            var folder = Path.Combine(webRoot, "uploads", "avatars");
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(folder, fileName);

            await using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }

            return $"/uploads/avatars/{fileName}";
        }

        private static bool IsSavedAvatarUrl(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase);
        }

        private string StorePendingPasswordHash(string password)
        {
            var token = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString($"{PendingPasswordSessionPrefix}:{token}", PasswordHashing.Hash(password));
            return token;
        }

        private string? GetPendingPasswordHash(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            return HttpContext.Session.GetString($"{PendingPasswordSessionPrefix}:{token}");
        }

        private void ClearPendingPasswordHash(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                HttpContext.Session.Remove($"{PendingPasswordSessionPrefix}:{token}");
            }
        }

        private void NotifyInvalidEmployeeForm()
        {
            TempData["EmployeeError"] = "Thông tin nhân viên chưa hợp lệ. Vui lòng kiểm tra các ô được báo lỗi.";
        }

        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Index(string? q, string? status, int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            IQueryable<NhanVien> query = _db.NhanViens.AsNoTracking();
            
            if (!string.IsNullOrWhiteSpace(q)) {
                var terms = q.Trim();
                query = query.Where(x =>
                    x.HoTen.Contains(terms)
                    || x.SoDienThoai.Contains(terms)
                    || x.VaiTro.Contains(terms)
                    || (x.Email != null && x.Email.Contains(terms)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToLowerInvariant();
                if (normalizedStatus is "active" or "hoatdong")
                {
                    query = query.Where(x => x.IsActive);
                }
                else if (normalizedStatus is "inactive" or "nghiviec")
                {
                    query = query.Where(x => !x.IsActive);
                }
            }
            
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.NgayTao).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            
            ViewBag.Query = q; ViewBag.Status = status; ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalPages = (int)Math.Ceiling(total/(double)pageSize);
            return View(items);
        }

        [PermissionAuthorize(AppPermissions.ManagePermissions)]
        public async Task<IActionResult> Permissions(string? role, CancellationToken ct = default)
        {
            var selectedRole = AppRoles.IsValid(role) ? AppRoles.NormalizeForClaim(role) : AppRoles.GiaoDichVien;
            var permissionService = HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            ViewBag.SelectedRole = selectedRole;
            return View(await permissionService.GetRolePermissionsAsync(selectedRole, ct));
        }

        [PermissionAuthorize(AppPermissions.ManagePermissions)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePermissions(string role, string[] permissions, CancellationToken ct = default)
        {
            var permissionService = HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            try
            {
                await permissionService.SaveRolePermissionsAsync(role, permissions ?? [], ct);
                TempData["EmployeeSuccess"] = $"Đã cập nhật quyền cho chức vụ {AppRoles.NormalizeForClaim(role)}.";
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                TempData["EmployeeError"] = "Database chưa có bảng PhanQuyenVaiTro. Hãy chạy file Database/RolePermissionMigration.sql trước.";
            }

            return RedirectToAction(nameof(Permissions), new { role = AppRoles.NormalizeForClaim(role) });
        }

        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            var model = new NhanVien
            {
                MaNv = await GetNextEmployeeCodeAsync(ct),
                NgaySinh = DateOnly.FromDateTime(DateTime.Today).AddYears(-18)
            };

            return View(model);
        }

        private static bool IsValidCode(string? code, string prefix, int width)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            var suffix = code.Substring(prefix.Length);
            if (suffix.Length != width) return false;
            for (var i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i])) return false;
            }
            return true;
        }

        private static int ParseCodeSuffix(string code, string prefix)
        {
            if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var suffix = code.Substring(prefix.Length);
            return int.TryParse(suffix, out var value) ? value : 0;
        }

        private async Task<string> GetNextEmployeeCodeAsync(CancellationToken ct)
        {
            var existingCodes = await _db.NhanViens
                .AsNoTracking()
                .Select(x => x.MaNv)
                .ToListAsync(ct);

            var maxId = existingCodes
                .Select(x => ParseCodeSuffix(x, "NV"))
                .DefaultIfEmpty(0)
                .Max();

            return $"NV{(maxId + 1):000}";
        }

        private async Task<string> GetNextAccountCodeAsync(CancellationToken ct)
        {
            var existingCodes = await _db.TaiKhoanNhanViens
                .AsNoTracking()
                .Select(x => x.MaTaiKhoan)
                .ToListAsync(ct);

            var maxId = existingCodes
                .Select(x => ParseCodeSuffix(x, "TK"))
                .DefaultIfEmpty(0)
                .Max();

            return $"TK{(maxId + 1):000}";
        }

        private static void NormalizeEmployeeInput(NhanVien m)
        {
            m.MaNv = m.MaNv?.Trim() ?? string.Empty;
            m.HoTen = m.HoTen?.Trim() ?? string.Empty;
            m.SoDienThoai = m.SoDienThoai?.Trim() ?? string.Empty;
            m.Email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim();
            m.DiaChi = string.IsNullOrWhiteSpace(m.DiaChi) ? null : m.DiaChi.Trim();
            m.GioiTinh = string.IsNullOrWhiteSpace(m.GioiTinh) ? null : m.GioiTinh.Trim();
            m.VaiTro = m.VaiTro?.Trim() ?? string.Empty;
        }

        private async Task ValidateUniqueEmployeePhoneAsync(NhanVien m, string? excludeId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(m.SoDienThoai)) return;

            var duplicateEmployeePhone = await _db.NhanViens
                .AsNoTracking()
                .AnyAsync(x => x.SoDienThoai == m.SoDienThoai && (excludeId == null || x.MaNv != excludeId), ct);

            if (duplicateEmployeePhone)
            {
                ModelState.AddModelError(nameof(NhanVien.SoDienThoai), "Số điện thoại này đã được dùng cho nhân viên.");
            }

            var duplicateEmployeeLogin = await _db.TaiKhoanNhanViens
                .AsNoTracking()
                .AnyAsync(x => x.TenDangNhap == m.SoDienThoai && (excludeId == null || x.MaNv != excludeId), ct);

            if (duplicateEmployeeLogin)
            {
                ModelState.AddModelError(nameof(NhanVien.SoDienThoai), "Số điện thoại này đã được dùng làm tài khoản nhân viên.");
            }

            var duplicateCustomerPhone = await _db.KhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.SoDienThoai == m.SoDienThoai, ct);

            if (duplicateCustomerPhone)
            {
                ModelState.AddModelError(nameof(NhanVien.SoDienThoai), "Số điện thoại này đã được dùng cho khách hàng.");
            }

            var duplicateCustomerLogin = await _db.TaiKhoanKhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.TenDangNhap == m.SoDienThoai, ct);

            if (duplicateCustomerLogin)
            {
                ModelState.AddModelError(nameof(NhanVien.SoDienThoai), "Số điện thoại này đã được dùng làm tài khoản khách hàng.");
            }
        }

        private async Task ValidateUniqueEmployeeEmailAsync(NhanVien m, string? excludeId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(m.Email)) return;

            var duplicateEmployeeEmail = await _db.NhanViens
                .AsNoTracking()
                .AnyAsync(x => x.Email == m.Email && (excludeId == null || x.MaNv != excludeId), ct);

            if (duplicateEmployeeEmail)
            {
                ModelState.AddModelError(nameof(NhanVien.Email), "Email này đã được dùng cho nhân viên.");
            }

            var duplicateCustomerEmail = await _db.KhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.Email == m.Email, ct);

            if (duplicateCustomerEmail)
            {
                ModelState.AddModelError(nameof(NhanVien.Email), "Email này đã được dùng cho khách hàng.");
            }
        }

        private void ValidateMinimumAge(NhanVien m)
        {
            if (m.NgaySinh == default)
            {
                ModelState.AddModelError(nameof(NhanVien.NgaySinh), "Ngày sinh không được để trống.");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (m.NgaySinh > today.AddYears(-18))
            {
                ModelState.AddModelError(nameof(NhanVien.NgaySinh), "Nhân viên phải đủ 18 tuổi.");
            }
        }

        private bool AddUniqueEmployeeModelErrors(DbUpdateException ex)
        {
            var message = $"{ex.Message} {ex.InnerException?.Message}";
            if (!message.Contains(nameof(NhanVien.SoDienThoai), StringComparison.OrdinalIgnoreCase)
                && !message.Contains(nameof(TaiKhoanNhanVien.TenDangNhap), StringComparison.OrdinalIgnoreCase)
                && !message.Contains("UQ__TaiKhoan__55F68FC07A8B11BF", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ModelState.AddModelError(nameof(NhanVien.SoDienThoai), "Số điện thoại này đã được dùng trong hệ thống.");
            return true;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Create(NhanVien m, IFormFile? AnhDaiDienFile, string? matKhau, string? matKhauToken, CancellationToken ct = default)
        {
            NormalizeEmployeeInput(m);
            m.VaiTro = AppRoles.NormalizeForClaim(m.VaiTro);
            if (!IsValidCode(m.MaNv, "NV", 3))
            {
                m.MaNv = await GetNextEmployeeCodeAsync(ct);
                ModelState.Remove(nameof(NhanVien.MaNv));
                ModelState.Remove(nameof(NhanVien.MaNvText));
            }

            if (AnhDaiDienFile != null && AnhDaiDienFile.Length > 0)
            {
                try
                {
                    m.AnhDaiDienUrl = await SaveAvatarAsync(AnhDaiDienFile, ct);
                    ModelState.Remove(nameof(NhanVien.AnhDaiDienUrl));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(AnhDaiDienFile), ex.Message);
                }
            }

            if (!IsSavedAvatarUrl(m.AnhDaiDienUrl))
            {
                ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện nhân viên là bắt buộc.");
            }

            string? passwordHash = null;
            if (!string.IsNullOrWhiteSpace(matKhau))
            {
                if (matKhau.Trim().Length < 6)
                {
                    ModelState.AddModelError("matKhau", "Mat khau ban dau phai co it nhat 6 ky tu.");
                }
                else
                {
                    matKhauToken = StorePendingPasswordHash(matKhau.Trim());
                    passwordHash = GetPendingPasswordHash(matKhauToken);
                }
            }
            else
            {
                passwordHash = GetPendingPasswordHash(matKhauToken);
            }

            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                ModelState.AddModelError("matKhau", "Mat khau ban dau phai co it nhat 6 ky tu.");
            }
            else
            {
                ViewData["PendingPasswordToken"] = matKhauToken;
            }

            if (!AppRoles.IsValid(m.VaiTro))
            {
                ModelState.AddModelError(nameof(NhanVien.VaiTro), "Vai trò không hợp lệ.");
            }

            await ValidateUniqueEmployeePhoneAsync(m, excludeId: null, ct);
            await ValidateUniqueEmployeeEmailAsync(m, excludeId: null, ct);
            ValidateMinimumAge(m);

            if (!ModelState.IsValid)
            {
                NotifyInvalidEmployeeForm();
                ViewData["PendingPasswordToken"] = matKhauToken;
                return View(m);
            }

            try {
                m.NgayTao = DateTime.Now; m.IsActive = true;
                _db.NhanViens.Add(m);
                _db.TaiKhoanNhanViens.Add(new TaiKhoanNhanVien
                {
                    MaTaiKhoan = await GetNextAccountCodeAsync(ct),
                    MaNv = m.MaNv,
                    TenDangNhap = m.SoDienThoai,
                    MatKhauHash = passwordHash!,
                    SoLanSaiMatKhau = 0,
                    BiKhoa = false,
                    NgayTao = DateTime.Now,
                    NgayCapNhat = DateTime.Now
                });
                await _db.SaveChangesAsync(ct);
                ClearPendingPasswordHash(matKhauToken);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex) when (AddUniqueEmployeeModelErrors(ex))
            {
                NotifyInvalidEmployeeForm();
                ViewData["PendingPasswordToken"] = matKhauToken;
                return View(m);
            }
            catch (Exception ex) {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(string.Empty, $"Không thể lưu nhân viên: {innerMsg}");
                TempData["EmployeeError"] = $"Không thể lưu nhân viên: {innerMsg}";
                return View(m);
            }
        }

        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Edit(string id) => View(await _db.NhanViens.FindAsync(id));

        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Details(string id, CancellationToken ct = default)
        {
            var employee = await _db.NhanViens
                .AsNoTracking()
                .Include(x => x.TaiKhoanNhanVien)
                .FirstOrDefaultAsync(x => x.MaNv == id, ct);

            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Edit(string id, NhanVien m, IFormFile? AnhDaiDienFile, CancellationToken ct = default)
        {
            var e = await _db.NhanViens.FindAsync(id);
            if(e==null) return NotFound();
            NormalizeEmployeeInput(m);
            m.VaiTro = AppRoles.NormalizeForClaim(m.VaiTro);

            if (AnhDaiDienFile != null && AnhDaiDienFile.Length > 0)
            {
                try
                {
                    m.AnhDaiDienUrl = await SaveAvatarAsync(AnhDaiDienFile, ct);
                    ModelState.Remove(nameof(NhanVien.AnhDaiDienUrl));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(AnhDaiDienFile), ex.Message);
                }
            }
            else if (!IsSavedAvatarUrl(m.AnhDaiDienUrl))
            {
                m.AnhDaiDienUrl = e.AnhDaiDienUrl;
                ModelState.Remove(nameof(NhanVien.AnhDaiDienUrl));
            }

            var hasAvatar = !string.IsNullOrWhiteSpace(e.AnhDaiDienUrl) || IsSavedAvatarUrl(m.AnhDaiDienUrl);
            if (!hasAvatar)
            {
                ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện nhân viên là bắt buộc.");
            }

            if (!AppRoles.IsValid(m.VaiTro))
            {
                ModelState.AddModelError(nameof(NhanVien.VaiTro), "Vai trò không hợp lệ.");
            }

            if (IsCurrentEmployee(id) && m.VaiTro != AppRoles.QuanTriHeThong)
            {
                ModelState.AddModelError(nameof(NhanVien.VaiTro), "Không thể tự hạ quyền quản trị của tài khoản đang đăng nhập.");
            }

            await ValidateUniqueEmployeePhoneAsync(m, excludeId: e.MaNv, ct);
            await ValidateUniqueEmployeeEmailAsync(m, excludeId: e.MaNv, ct);
            ValidateMinimumAge(m);

            if (!ModelState.IsValid)
            {
                NotifyInvalidEmployeeForm();
                return View(m);
            }

            try {
                e.HoTen = m.HoTen; e.SoDienThoai = m.SoDienThoai; e.Email = m.Email; e.DiaChi = m.DiaChi; e.NgaySinh = m.NgaySinh; e.GioiTinh = m.GioiTinh; e.VaiTro = m.VaiTro; e.IsActive = m.IsActive;
                var account = await _db.TaiKhoanNhanViens.FirstOrDefaultAsync(x => x.MaNv == e.MaNv, ct);
                if (account != null)
                {
                    account.TenDangNhap = m.SoDienThoai;
                    account.NgayCapNhat = DateTime.Now;
                }
                if (IsSavedAvatarUrl(m.AnhDaiDienUrl)) e.AnhDaiDienUrl = m.AnhDaiDienUrl;
                await _db.SaveChangesAsync();
                TempData["EmployeeSuccess"] = "Đã cập nhật thông tin nhân viên.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex) when (AddUniqueEmployeeModelErrors(ex))
            {
                NotifyInvalidEmployeeForm();
                return View(m);
            }
            catch (Exception ex) {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(string.Empty, $"Không thể lưu nhân viên: {innerMsg}");
                TempData["EmployeeError"] = $"Không thể lưu nhân viên: {innerMsg}";
                return View(m);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> ChangeRole(string id, string vaiTro, CancellationToken ct = default)
        {
            var employee = await _db.NhanViens.FirstOrDefaultAsync(x => x.MaNv == id, ct);
            if (employee == null) return NotFound();

            var normalizedRole = AppRoles.NormalizeForClaim(vaiTro);
            if (!AppRoles.IsValid(normalizedRole))
            {
                TempData["EmployeeError"] = "Vai trò không hợp lệ.";
                return RedirectBackToPermissionsIfNeeded();
            }

            if (IsCurrentEmployee(id) && normalizedRole != AppRoles.QuanTriHeThong)
            {
                TempData["EmployeeError"] = "Không thể tự hạ quyền quản trị của tài khoản đang đăng nhập.";
                return RedirectBackToPermissionsIfNeeded();
            }

            employee.VaiTro = normalizedRole;
            await _db.SaveChangesAsync(ct);
            TempData["EmployeeSuccess"] = $"Đã cập nhật quyền cho nhân viên {employee.MaNv}.";
            return RedirectBackToPermissionsIfNeeded();
        }

        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> Delete(string id)
        {
            if (IsCurrentEmployee(id))
            {
                TempData["EmployeeError"] = "Không thể xóa hoặc khóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            return View(await _db.NhanViens.FindAsync(id));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize(AppPermissions.ManageEmployees)]
        public async Task<IActionResult> DeleteConfirmed(string id, CancellationToken ct = default) {
            if (IsCurrentEmployee(id))
            {
                TempData["EmployeeError"] = "Không thể xóa hoặc khóa tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            var e = await _db.NhanViens.FindAsync(id);
            if(e!=null)
            {
                var hasRelatedData = await _db.DonVays.AsNoTracking().AnyAsync(x => x.MaNvsoan == id, ct)
                    || await _db.QuyTrinhPheDuyets.AsNoTracking().AnyAsync(x => x.MaNv == id, ct)
                    || await _db.HopDongTinDungs.AsNoTracking().AnyAsync(x => x.MaNv == id, ct)
                    || await _db.ThanhToans.AsNoTracking().AnyAsync(x => x.MaNv == id, ct)
                    || await _db.TaiSanKhachHangs.AsNoTracking().AnyAsync(x => x.MaNvdinhGia == id, ct);

                if (hasRelatedData)
                {
                    e.IsActive = false;
                    var account = await _db.TaiKhoanNhanViens.FirstOrDefaultAsync(x => x.MaNv == id, ct);
                    if (account != null)
                    {
                        account.BiKhoa = true;
                        account.NgayCapNhat = DateTime.Now;
                    }
                    await _db.SaveChangesAsync(ct);
                    TempData["EmployeeWarning"] = "Nhan vien da co du lieu nghiep vu nen khong xoa vinh vien. Hệ thống da chuyen sang nghi viec va khoa tai khoan.";
                }
                else
                {
                    try
                    {
                        var account = await _db.TaiKhoanNhanViens.FirstOrDefaultAsync(x => x.MaNv == id, ct);
                        if (account != null) _db.TaiKhoanNhanViens.Remove(account);
                        _db.NhanViens.Remove(e);
                        await _db.SaveChangesAsync(ct);
                        TempData["EmployeeSuccess"] = "Da xoa nhan vien.";
                    }
                    catch (DbUpdateException)
                    {
                        _db.ChangeTracker.Clear();
                        var employee = await _db.NhanViens.FirstOrDefaultAsync(x => x.MaNv == id, ct);
                        if (employee != null)
                        {
                            employee.IsActive = false;
                            var account = await _db.TaiKhoanNhanViens.FirstOrDefaultAsync(x => x.MaNv == id, ct);
                            if (account != null)
                            {
                                account.BiKhoa = true;
                                account.NgayCapNhat = DateTime.Now;
                            }
                            await _db.SaveChangesAsync(ct);
                        }
                        TempData["EmployeeWarning"] = "Khong the xoa vinh vien vi nhan vien dang duoc tham chieu. Hệ thống da chuyen sang nghi viec va khoa tai khoan.";
                    }
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool IsCurrentEmployee(string maNv)
        {
            var currentMaNv = User.FindFirst("MaNV")?.Value;
            return string.Equals(currentMaNv, maNv, StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult RedirectBackToPermissionsIfNeeded()
        {
            var referer = Request.Headers.Referer.ToString();
            if (referer.Contains("/Employee/Permissions", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Permissions));
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
