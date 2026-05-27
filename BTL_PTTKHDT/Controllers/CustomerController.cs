using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Security;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

[PermissionAuthorize(AppPermissions.ViewCustomers)]
public sealed class CustomerController : Controller
{
    private const string PendingPasswordSessionPrefix = "PendingCustomerPassword";
    private readonly QltdnhContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ICreditScoreService _creditScoreService;

    public CustomerController(QltdnhContext db, IWebHostEnvironment env, ICreditScoreService creditScoreService)
    {
        _db = db;
        _env = env;
        _creditScoreService = creditScoreService;
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

    private void NotifyInvalidCustomerForm()
    {
        TempData["CustomerError"] = "Thông tin khách hàng chưa hợp lệ. Vui lòng kiểm tra các ô được báo lỗi.";
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? type, string? status, int page = 1, int pageSize = 8, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 8;

        IQueryable<KhachHang> baseQuery = _db.KhachHangs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = q.Trim();

            baseQuery = baseQuery.Where(x =>
                x.MaKh.Contains(query) ||
                x.HoTen.Contains(query) ||
                x.SoDienThoai.Contains(query) ||
                x.CmndCccd.Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalized = type.Trim().ToLowerInvariant();
            if (normalized is "canhan" or "ca-nhan" or "personal")
            {
                baseQuery = baseQuery.Where(x => !EF.Functions.Like(x.LoaiKhachHang.ToLower(), "%doanh%"));
            }
            else if (normalized is "doanhnghiep" or "doanh-nghiep" or "business")
            {
                baseQuery = baseQuery.Where(x => EF.Functions.Like(x.LoaiKhachHang.ToLower(), "%doanh%"));
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (normalizedStatus is "active" or "hoatdong")
            {
                baseQuery = baseQuery.Where(x => x.IsActive);
            }
            else if (normalizedStatus is "inactive" or "tamngung")
            {
                baseQuery = baseQuery.Where(x => !x.IsActive);
            }
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(x => x.NgayTao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CustomerRowViewModel
            {
                MaKh = x.MaKh,
                MaKhText = x.MaKhText ?? x.MaKh,
                HoTen = x.HoTen,
                NgaySinh = x.NgaySinh,
                CmndCccd = x.CmndCccd,
                LoaiKhachHangText = NormalizeCustomerTypeText(x.LoaiKhachHang),
                LoaiKhachHangKind = MapCustomerTypeKind(x.LoaiKhachHang),
                SoDienThoai = x.SoDienThoai,
                Email = x.Email,
                DiaChi = x.DiaChi,
                AnhDaiDienUrl = x.AnhDaiDienUrl,
                DiemTinDung = x.LichSuTinDungs
                    .OrderByDescending(ls => ls.NgayCapNhat)
                    .Select(ls => (int?)ls.DiemTinDung)
                    .FirstOrDefault(),
                XepHangRuiRo = x.LichSuTinDungs
                    .OrderByDescending(ls => ls.NgayCapNhat)
                    .Select(ls => ls.XepHangRuiRo)
                    .FirstOrDefault(),
                IsActive = x.IsActive,
                IsLocked = x.TaiKhoanKhachHang != null && x.TaiKhoanKhachHang.BiKhoa
            })
            .ToListAsync(cancellationToken);

        var model = new CustomerListViewModel
        {
            Items = items,
            Query = q,
            Type = type,
            Status = status,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        ViewData["Title"] = "Quản lý Khach hang";
        return View(model);
    }

    private static string MapCustomerTypeKind(string raw)
    {
        var s = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        return s.Contains("doanh") ? "business" : "personal";
    }

    private static string NormalizeCustomerTypeText(string raw)
    {
        var kind = MapCustomerTypeKind(raw);
        return kind == "business" ? "Doanh nghiệp" : "Cá nhân";
    }

    private static bool HasReasonableBirthDate(DateOnly value)
    {
        return value.Year >= 1900;
    }

    private static void NormalizeCustomerInput(KhachHang model)
    {
        model.MaKh = model.MaKh?.Trim() ?? string.Empty;
        model.HoTen = model.HoTen?.Trim() ?? string.Empty;
        model.CmndCccd = model.CmndCccd?.Trim() ?? string.Empty;
        model.SoDienThoai = model.SoDienThoai?.Trim() ?? string.Empty;
        model.LoaiKhachHang = string.IsNullOrWhiteSpace(model.LoaiKhachHang)
            ? string.Empty
            : NormalizeCustomerTypeText(model.LoaiKhachHang);
        model.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        model.DiaChi = string.IsNullOrWhiteSpace(model.DiaChi) ? null : model.DiaChi.Trim();
        model.MaSoThue = string.IsNullOrWhiteSpace(model.MaSoThue) ? null : model.MaSoThue.Trim();
        model.TenNguoiDaiDien = string.IsNullOrWhiteSpace(model.TenNguoiDaiDien) ? null : model.TenNguoiDaiDien.Trim();
        model.ChucVuNguoiDaiDien = string.IsNullOrWhiteSpace(model.ChucVuNguoiDaiDien) ? null : model.ChucVuNguoiDaiDien.Trim();
        model.NgheNghiep = string.IsNullOrWhiteSpace(model.NgheNghiep) ? null : model.NgheNghiep.Trim();
        model.NoiLamViec = string.IsNullOrWhiteSpace(model.NoiLamViec) ? null : model.NoiLamViec.Trim();
        model.ChucVu = string.IsNullOrWhiteSpace(model.ChucVu) ? null : model.ChucVu.Trim();
        model.LinhVucKinhDoanh = string.IsNullOrWhiteSpace(model.LinhVucKinhDoanh) ? null : model.LinhVucKinhDoanh.Trim();
    }

    private async Task ValidateUniqueCustomerFieldsAsync(KhachHang model, string? excludeId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(model.CmndCccd))
        {
            var duplicateCmnd = await _db.KhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.CmndCccd == model.CmndCccd && (excludeId == null || x.MaKh != excludeId), ct);

            if (duplicateCmnd)
            {
                ModelState.AddModelError(nameof(KhachHang.CmndCccd), "CMND/CCCD hoặc mã đăng ký này đã tồn tại.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.SoDienThoai))
        {
            var duplicatePhone = await _db.KhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.SoDienThoai == model.SoDienThoai && (excludeId == null || x.MaKh != excludeId), ct);

            if (duplicatePhone)
            {
                ModelState.AddModelError(nameof(KhachHang.SoDienThoai), "Số điện thoại này đã tồn tại.");
            }

            var duplicateCustomerLogin = await _db.TaiKhoanKhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.TenDangNhap == model.SoDienThoai && (excludeId == null || x.MaKh != excludeId), ct);

            if (duplicateCustomerLogin)
            {
                ModelState.AddModelError(nameof(KhachHang.SoDienThoai), "Số điện thoại này đã được dùng làm tài khoản khách hàng.");
            }

            var duplicateEmployeePhone = await _db.NhanViens
                .AsNoTracking()
                .AnyAsync(x => x.SoDienThoai == model.SoDienThoai, ct);

            if (duplicateEmployeePhone)
            {
                ModelState.AddModelError(nameof(KhachHang.SoDienThoai), "Số điện thoại này đã được dùng cho nhân viên.");
            }

            var duplicateEmployeeLogin = await _db.TaiKhoanNhanViens
                .AsNoTracking()
                .AnyAsync(x => x.TenDangNhap == model.SoDienThoai, ct);

            if (duplicateEmployeeLogin)
            {
                ModelState.AddModelError(nameof(KhachHang.SoDienThoai), "Số điện thoại này đã được dùng làm tài khoản nhân viên.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var duplicateCustomerEmail = await _db.KhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.Email == model.Email && (excludeId == null || x.MaKh != excludeId), ct);

            if (duplicateCustomerEmail)
            {
                ModelState.AddModelError(nameof(KhachHang.Email), "Email này đã được dùng cho khách hàng.");
            }

            var duplicateEmployeeEmail = await _db.NhanViens
                .AsNoTracking()
                .AnyAsync(x => x.Email == model.Email, ct);

            if (duplicateEmployeeEmail)
            {
                ModelState.AddModelError(nameof(KhachHang.Email), "Email này đã được dùng cho nhân viên.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.MaSoThue))
        {
            var duplicateTaxCode = await _db.KhachHangs
                .AsNoTracking()
                .AnyAsync(x => x.MaSoThue == model.MaSoThue && (excludeId == null || x.MaKh != excludeId), ct);

            if (duplicateTaxCode)
            {
                ModelState.AddModelError(nameof(KhachHang.MaSoThue), "Mã số thuế này đã tồn tại.");
            }
        }
    }

    private bool AddUniqueConstraintModelErrors(DbUpdateException ex)
    {
        var message = $"{ex.Message} {ex.InnerException?.Message}";
        var handled = false;

        if (message.Contains("UQ__KhachHan__B91373138AC39C09", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CMND_CCCD", StringComparison.OrdinalIgnoreCase)
            || message.Contains(nameof(KhachHang.CmndCccd), StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(KhachHang.CmndCccd), "CMND/CCCD hoặc mã đăng ký này đã tồn tại.");
            handled = true;
        }

        if (message.Contains("UQ__KhachHan__0389B7BD09FB62B9", StringComparison.OrdinalIgnoreCase)
            || message.Contains(nameof(KhachHang.SoDienThoai), StringComparison.OrdinalIgnoreCase)
            || message.Contains(nameof(TaiKhoanKhachHang.TenDangNhap), StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(KhachHang.SoDienThoai), "Số điện thoại này đã tồn tại.");
            handled = true;
        }

        return handled;
    }

    private async Task PopulateCustomerEditViewDataAsync(string maKh, CancellationToken ct)
    {
        ViewData["CustomerAssets"] = await _db.TaiSanKhachHangs
            .AsNoTracking()
            .Where(x => x.MaKh == maKh)
            .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
            .ToListAsync(ct);
        ViewData["ActivePledgedAssetIds"] = await GetActivePledgedAssetIdsAsync(maKh, ct);
        ViewData["CreditHistory"] = await GetLatestCreditHistoryAsync(maKh, ct);
        ViewData["CustomerPortalAccount"] = await _db.TaiKhoanKhachHangs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == maKh, ct);
    }

    [HttpGet]
    [PermissionAuthorize(AppPermissions.CreateCustomers)]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        var model = new KhachHang
        {
            MaKh = await GetNextCustomerCodeAsync(ct),
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

    private async Task<string> GetNextCustomerCodeAsync(CancellationToken ct)
    {
        var existingCodes = await _db.KhachHangs
            .AsNoTracking()
            .Select(x => x.MaKh)
            .ToListAsync(ct);

        var maxId = existingCodes
            .Select(x => ParseCodeSuffix(x, "KH"))
            .DefaultIfEmpty(0)
            .Max();
        return $"KH{(maxId + 1):000}";
    }

    private async Task<string> GetNextTaiSanKhCodeAsync(CancellationToken ct)
    {
        var codes = await _db.TaiSanKhachHangs.AsNoTracking().Select(x => x.MaTaiSanKh).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "TSK")).DefaultIfEmpty(0).Max();
        return $"TSK{(maxId + 1):0000}";
    }

    private async Task<string> GetNextCustomerAccountCodeAsync(CancellationToken ct)
    {
        var codes = await _db.TaiKhoanKhachHangs.AsNoTracking().Select(x => x.MaTaiKhoanKh).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "TKKH")).DefaultIfEmpty(0).Max();
        return $"TKKH{(maxId + 1):0000}";
    }

    private Task<LichSuTinDung?> GetLatestCreditHistoryAsync(string maKh, CancellationToken ct)
    {
        return _db.LichSuTinDungs
            .AsNoTracking()
            .Where(x => x.MaKh == maKh)
            .OrderByDescending(x => x.NgayCapNhat)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<HashSet<string>> GetActivePledgedAssetIdsAsync(string maKh, CancellationToken ct)
    {
        var ids = await _db.TaiSanTheChaps
            .AsNoTracking()
            .Where(x =>
                x.MaTaiSanKhNavigation.MaKh == maKh
                && (x.TrangThai == "Đang thế chấp" || x.TrangThai == "Xử lý"))
            .Select(x => x.MaTaiSanKh)
            .ToListAsync(ct);

        return ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [PermissionAuthorize(AppPermissions.CreateCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KhachHang model, IFormFile? AnhDaiDienFile, string? matKhau, string? matKhauToken, CancellationToken ct = default)
    {
        NormalizeCustomerInput(model);

        if (!IsValidCode(model.MaKh, "KH", 3))
        {
            model.MaKh = await GetNextCustomerCodeAsync(ct);
            ModelState.Remove(nameof(KhachHang.MaKh));
            ModelState.Remove(nameof(KhachHang.MaKhText));
        }

        var isBusiness = MapCustomerTypeKind(model.LoaiKhachHang) == "business";
        ValidateMinimumAge(model);
        ValidateBusinessFields(model, isBusiness);
        ValidatePersonalFields(model, isBusiness);
        await ValidateUniqueCustomerFieldsAsync(model, excludeId: null, ct);
        if (AnhDaiDienFile != null && AnhDaiDienFile.Length > 0)
        {
            try
            {
                model.AnhDaiDienUrl = await SaveAvatarAsync(AnhDaiDienFile, ct);
                ModelState.Remove(nameof(KhachHang.AnhDaiDienUrl));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(AnhDaiDienFile), ex.Message);
            }
        }

        if (!isBusiness && !IsSavedAvatarUrl(model.AnhDaiDienUrl))
        {
            ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện khách hàng là bắt buộc với khách hàng cá nhân.");
        }

        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(matKhau))
        {
            if (matKhau.Trim().Length < 6)
            {
                ModelState.AddModelError("matKhau", "Mật khẩu cổng khách hàng phải có ít nhất 6 ký tự.");
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
            ModelState.AddModelError("matKhau", "Mật khẩu cổng khách hàng phải có ít nhất 6 ký tự.");
        }
        else
        {
            ViewData["PendingPasswordToken"] = matKhauToken;
        }

        if (!ModelState.IsValid)
        {
            NotifyInvalidCustomerForm();
            ViewData["PendingPasswordToken"] = matKhauToken;
            return View(model);
        }

        try
        {
            model.NgayTao = DateTime.Now;
            model.NgayCapNhat = DateTime.Now;
            model.IsActive = true;
            _db.KhachHangs.Add(model);
            _db.TaiKhoanKhachHangs.Add(new TaiKhoanKhachHang
            {
                MaTaiKhoanKh = await GetNextCustomerAccountCodeAsync(ct),
                MaKh = model.MaKh,
                TenDangNhap = model.SoDienThoai,
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
        catch (DbUpdateException ex) when (AddUniqueConstraintModelErrors(ex))
        {
            NotifyInvalidCustomerForm();
            ViewData["PendingPasswordToken"] = matKhauToken;
            return View(model);
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError(string.Empty, $"Không thể lưu khách hàng: {innerMsg}");
            TempData["CustomerError"] = $"Không thể lưu khách hàng: {innerMsg}";
            return View(model);
        }
    }

    [HttpGet]
    [PermissionAuthorize(AppPermissions.EditCustomers)]
    public async Task<IActionResult> Edit(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();

        await PopulateCustomerEditViewDataAsync(customer.MaKh, ct);
        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs
            .AsNoTracking()
            .Include(x => x.TaiKhoanKhachHang)
            .FirstOrDefaultAsync(x => x.MaKh == id, ct);

        if (customer == null) return NotFound();

        ViewData["CustomerAssets"] = await _db.TaiSanKhachHangs
            .AsNoTracking()
            .Where(x => x.MaKh == id)
            .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
            .Take(5)
            .ToListAsync(ct);
        ViewData["CreditHistory"] = await GetLatestCreditHistoryAsync(id, ct);
        ViewData["CreditLimit"] = await _db.HanMucTinDungs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == id, ct);

        return View(customer);
    }

    [PermissionAuthorize(AppPermissions.AppraiseCollateral)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCreditLimit(string id, string? hanMucToiDa, string? returnUrl, CancellationToken ct = default)
    {
        var customerExists = await _db.KhachHangs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct);
        if (!customerExists) return NotFound();

        string RedirectBack()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return Url.Action(nameof(Details), new { id }) ?? $"/Customer/Details/{id}";
        }

        if (!TryParseMoney(hanMucToiDa, out var creditLimitAmount) || creditLimitAmount <= 0)
        {
            TempData["CreditLimitError"] = "Hạn mức tối đa phải là số tiền lớn hơn 0.";
            TempData["CreditLimitInput"] = hanMucToiDa;
            return Redirect(RedirectBack());
        }

        var creditLimit = await _db.HanMucTinDungs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (creditLimit == null)
        {
            creditLimit = new HanMucTinDung
            {
                MaKh = id,
                HanMucToiDa = creditLimitAmount,
                HanMucDaSuDung = 0m,
                NgayCapNhat = DateOnly.FromDateTime(DateTime.Today)
            };
            _db.HanMucTinDungs.Add(creditLimit);
        }
        else
        {
            if (creditLimitAmount < creditLimit.HanMucDaSuDung)
            {
                TempData["CreditLimitError"] = $"Hạn mức tối đa không được nhỏ hơn số đã sử dụng ({creditLimit.HanMucDaSuDung:N0} VNĐ).";
                TempData["CreditLimitInput"] = hanMucToiDa;
                return Redirect(RedirectBack());
            }

            creditLimit.HanMucToiDa = creditLimitAmount;
            creditLimit.NgayCapNhat = DateOnly.FromDateTime(DateTime.Today);
        }

        await _db.SaveChangesAsync(ct);
        TempData["CreditLimitSuccess"] = "Đã cập nhật hạn mức tín dụng cho khách hàng.";
        return Redirect(RedirectBack());
    }

    [PermissionAuthorize(AppPermissions.AppraiseCollateral)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCreditIncome(string id, string? thuNhapHangThang, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();

        if (!TryParseMoney(thuNhapHangThang, out var monthlyIncome) || monthlyIncome <= 0)
        {
            TempData["CreditError"] = "Thu nhap hang thang phai lon hon 0.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (MapCustomerTypeKind(customer.LoaiKhachHang) == "business")
        {
            customer.DoanhThuBinhQuanThang = monthlyIncome;
            customer.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            customer.ThuNhapHangThang = monthlyIncome;
            customer.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync(ct);
        }

        var hasLoanHistory = await _db.KhoanVays.AsNoTracking().AnyAsync(x => x.MaKh == id, ct);
        if (hasLoanHistory)
        {
            await _creditScoreService.RecalculateAsync(id, "Cap nhat thu nhap", ct, monthlyIncome);
            TempData["CreditSuccess"] = "Da cap nhat thu nhap va tinh lai diem tin dung.";
        }
        else
        {
            TempData["CreditSuccess"] = "Da cap nhat thu nhap. Khach hang chua co khoan vay nen chua tinh diem tin dung.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private static bool TryParseMoney(string? value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var cleaned = value.Trim()
            .Replace("VNĐ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("VND", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace(",", string.Empty);

        return decimal.TryParse(cleaned, out result);
    }

    private void ValidateBusinessFields(KhachHang model, bool isBusiness)
    {
        if (!isBusiness) return;

        if (string.IsNullOrWhiteSpace(model.MaSoThue))
        {
            ModelState.AddModelError(nameof(KhachHang.MaSoThue), "Ma so thue la bat buoc voi khach hang doanh nghiep.");
        }

        if (string.IsNullOrWhiteSpace(model.TenNguoiDaiDien))
        {
            ModelState.AddModelError(nameof(KhachHang.TenNguoiDaiDien), "Ten nguoi dai dien la bat buoc voi khach hang doanh nghiep.");
        }

        if (!model.DoanhThuBinhQuanThang.HasValue || model.DoanhThuBinhQuanThang.Value <= 0)
        {
            ModelState.AddModelError(nameof(KhachHang.DoanhThuBinhQuanThang), "Doanh thu binh quan thang phai lon hon 0.");
        }
    }

    private void ValidatePersonalFields(KhachHang model, bool isBusiness)
    {
        if (isBusiness) return;

        if (model.ThuNhapHangThang.HasValue && model.ThuNhapHangThang.Value < 0)
        {
            ModelState.AddModelError(nameof(KhachHang.ThuNhapHangThang), "Thu nhap hang thang khong duoc am.");
        }
    }

    private void ValidateMinimumAge(KhachHang model)
    {
        if (model.NgaySinh == default || !HasReasonableBirthDate(model.NgaySinh))
        {
            ModelState.AddModelError(nameof(KhachHang.NgaySinh), "Ngày sinh không hợp lệ, vui lòng chọn lại.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var minBirthDate = today.AddYears(-18);
        if (model.NgaySinh > minBirthDate)
        {
            ModelState.AddModelError(nameof(KhachHang.NgaySinh), "Khách hàng hoặc người đại diện phải đủ 18 tuổi.");
        }
    }

    [PermissionAuthorize(AppPermissions.EditCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAsset(string id, LoanCollateralCreateViewModel model, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();

        if (!ModelState.IsValid)
        {
            var msg = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            TempData["AssetError"] = string.IsNullOrWhiteSpace(msg) ? "Dữ liệu tài sản không hợp lệ." : msg;
            return RedirectToAction(nameof(Edit), new { id });
        }

        var now = DateTime.Now;
        var newCode = await GetNextTaiSanKhCodeAsync(ct);

        var entity = new TaiSanKhachHang
        {
            MaTaiSanKh = newCode,
            MaKh = customer.MaKh,
            LoaiTaiSan = model.LoaiTaiSan.Trim(),
            GiaTriKhaiBao = model.GiaTriKhaiBao,
            GiaTriDinhGia = null,
            TyLeLtv = model.TyLeLtv,
            GiayToPhapLy = model.GiayToPhapLy,
            MoTa = model.MoTa,
            NgayKhaiBao = DateOnly.FromDateTime(now),
            NgayDinhGia = null,
            MaNvdinhGia = null,
            TrangThai = "Chưa định giá",
            TrangThaiSoHuu = "Đang sở hữu",
            NgayBan = null,
            GhiChuSoHuu = null
        };

        _db.TaiSanKhachHangs.Add(entity);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [PermissionAuthorize(AppPermissions.EditCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePortalAccount(string id, string? matKhau, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();

        if (await _db.TaiKhoanKhachHangs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct))
        {
            TempData["CustomerWarning"] = "Khách hàng đã có tài khoản cổng khách hàng.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (string.IsNullOrWhiteSpace(matKhau) || matKhau.Length < 6)
        {
            TempData["CustomerError"] = "Mật khẩu cổng khách hàng phải có ít nhất 6 ký tự.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        _db.TaiKhoanKhachHangs.Add(new TaiKhoanKhachHang
        {
            MaTaiKhoanKh = await GetNextCustomerAccountCodeAsync(ct),
            MaKh = customer.MaKh,
            TenDangNhap = customer.SoDienThoai,
            MatKhauHash = PasswordHashing.Hash(matKhau.Trim()),
            SoLanSaiMatKhau = 0,
            BiKhoa = false,
            NgayTao = DateTime.Now,
            NgayCapNhat = DateTime.Now
        });

        await _db.SaveChangesAsync(ct);
        TempData["CustomerSuccess"] = "Đã cấp tài khoản cổng khách hàng.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [PermissionAuthorize(AppPermissions.EditCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPortalPassword(string id, string? matKhau, CancellationToken ct = default)
    {
        var account = await _db.TaiKhoanKhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (account == null) return NotFound();

        if (string.IsNullOrWhiteSpace(matKhau) || matKhau.Length < 6)
        {
            TempData["CustomerError"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        account.MatKhauHash = PasswordHashing.Hash(matKhau.Trim());
        account.SoLanSaiMatKhau = 0;
        account.BiKhoa = false;
        account.NgayCapNhat = DateTime.Now;
        await _db.SaveChangesAsync(ct);

        TempData["CustomerSuccess"] = "Đã đặt lại mật khẩu cổng khách hàng.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [PermissionAuthorize(AppPermissions.EditCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePortalAccountLock(string id, CancellationToken ct = default)
    {
        var account = await _db.TaiKhoanKhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (account == null) return NotFound();

        account.BiKhoa = !account.BiKhoa;
        account.NgayCapNhat = DateTime.Now;
        await _db.SaveChangesAsync(ct);

        TempData["CustomerSuccess"] = account.BiKhoa
            ? "Đã khóa tài khoản cổng khách hàng."
            : "Đã mở khóa tài khoản cổng khách hàng.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [PermissionAuthorize(AppPermissions.EditCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, KhachHang model, IFormFile? AnhDaiDienFile, CancellationToken ct = default)
    {
        var existing = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (existing == null) return NotFound();

        NormalizeCustomerInput(model);

        var isBusiness = MapCustomerTypeKind(model.LoaiKhachHang) == "business";
        if (AnhDaiDienFile != null && AnhDaiDienFile.Length > 0)
        {
            try
            {
                model.AnhDaiDienUrl = await SaveAvatarAsync(AnhDaiDienFile, ct);
                ModelState.Remove(nameof(KhachHang.AnhDaiDienUrl));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(AnhDaiDienFile), ex.Message);
            }
        }
        else if (!IsSavedAvatarUrl(model.AnhDaiDienUrl))
        {
            model.AnhDaiDienUrl = existing.AnhDaiDienUrl;
            ModelState.Remove(nameof(KhachHang.AnhDaiDienUrl));
        }

        ValidateMinimumAge(model);
        ValidateBusinessFields(model, isBusiness);
        ValidatePersonalFields(model, isBusiness);
        await ValidateUniqueCustomerFieldsAsync(model, excludeId: existing.MaKh, ct);

        if (!ModelState.IsValid)
        {
            NotifyInvalidCustomerForm();
            model.MaKh = existing.MaKh;
            await PopulateCustomerEditViewDataAsync(existing.MaKh, ct);
            return View(model);
        }

        try
        {
            existing.HoTen = model.HoTen;
            existing.NgaySinh = model.NgaySinh;
            existing.CmndCccd = model.CmndCccd;
            existing.DiaChi = model.DiaChi;
            existing.SoDienThoai = model.SoDienThoai;
            existing.Email = model.Email;
            existing.LoaiKhachHang = model.LoaiKhachHang;
            existing.NgheNghiep = isBusiness ? null : model.NgheNghiep;
            existing.NoiLamViec = isBusiness ? null : model.NoiLamViec;
            existing.ChucVu = isBusiness ? null : model.ChucVu;
            existing.ThuNhapHangThang = isBusiness ? null : model.ThuNhapHangThang;
            existing.MaSoThue = model.MaSoThue;
            existing.TenNguoiDaiDien = model.TenNguoiDaiDien;
            existing.ChucVuNguoiDaiDien = model.ChucVuNguoiDaiDien;
            existing.NgayThanhLap = model.NgayThanhLap;
            existing.LinhVucKinhDoanh = model.LinhVucKinhDoanh;
            existing.DoanhThuBinhQuanThang = model.DoanhThuBinhQuanThang;
            existing.LoiNhuanBinhQuanThang = model.LoiNhuanBinhQuanThang;
            existing.SoLaoDong = model.SoLaoDong;
            existing.IsActive = model.IsActive;
            existing.NgayCapNhat = DateTime.Now;

            if (IsSavedAvatarUrl(model.AnhDaiDienUrl)) existing.AnhDaiDienUrl = model.AnhDaiDienUrl;

            var portalAccount = await _db.TaiKhoanKhachHangs.FirstOrDefaultAsync(x => x.MaKh == existing.MaKh, ct);
            if (portalAccount != null && portalAccount.TenDangNhap != model.SoDienThoai)
            {
                portalAccount.TenDangNhap = model.SoDienThoai;
                portalAccount.NgayCapNhat = DateTime.Now;
            }

            _db.KhachHangs.Update(existing);
            await _db.SaveChangesAsync(ct);
            TempData["CustomerSuccess"] = "Đã cập nhật thông tin khách hàng.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (AddUniqueConstraintModelErrors(ex))
        {
            NotifyInvalidCustomerForm();
            model.MaKh = existing.MaKh;
            await PopulateCustomerEditViewDataAsync(existing.MaKh, ct);
            return View(model);
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError(string.Empty, $"Không thể lưu khách hàng: {innerMsg}");
            TempData["CustomerError"] = $"Không thể lưu khách hàng: {innerMsg}";

            model.MaKh = existing.MaKh;
            await PopulateCustomerEditViewDataAsync(existing.MaKh, ct);
            return View(model);
        }
    }

    [PermissionAuthorize(AppPermissions.EditCustomers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAssetSold(string id, string maTaiSanKh, CancellationToken ct = default)
    {
        var asset = await _db.TaiSanKhachHangs
            .FirstOrDefaultAsync(x => x.MaKh == id && x.MaTaiSanKh == maTaiSanKh, ct);
        if (asset == null) return NotFound();

        var isPledged = await _db.TaiSanTheChaps
            .AsNoTracking()
            .AnyAsync(x =>
                x.MaTaiSanKh == maTaiSanKh
                && (x.TrangThai == "Đang thế chấp" || x.TrangThai == "Xử lý"),
                ct);
        if (isPledged)
        {
            TempData["AssetError"] = "Tai san dang the chap, can giai chap truoc khi ghi nhan da ban.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        asset.TrangThaiSoHuu = "Đã bán";
        asset.NgayBan = DateOnly.FromDateTime(DateTime.Now);
        asset.GhiChuSoHuu = "Khach hang da ban tai san";
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    [PermissionAuthorize(AppPermissions.DeleteCustomers)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [PermissionAuthorize(AppPermissions.DeleteCustomers)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer != null)
        {
            var hasRelatedData = await _db.DonVays.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
                || await _db.KhoanVays.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
                || await _db.TaiSanKhachHangs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
                || await _db.LichSuTinDungs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
                || await _db.HanMucTinDungs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct);

            if (hasRelatedData)
            {
                customer.IsActive = false;
                customer.NgayCapNhat = DateTime.Now;
                await _db.SaveChangesAsync(ct);
                TempData["CustomerWarning"] = "Khach hang da co du lieu nghiep vu nen khong xoa vinh vien. Hệ thống da chuyen sang trang thai tam ngung.";
            }
            else
            {
                try
                {
                    var portalAccounts = await _db.TaiKhoanKhachHangs
                        .Where(x => x.MaKh == id)
                        .ToListAsync(ct);
                    _db.TaiKhoanKhachHangs.RemoveRange(portalAccounts);
                    _db.KhachHangs.Remove(customer);
                    await _db.SaveChangesAsync(ct);
                    TempData["CustomerSuccess"] = "Da xoa khach hang.";
                }
                catch (DbUpdateException)
                {
                    _db.ChangeTracker.Clear();
                    var softDeleteCustomer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
                    if (softDeleteCustomer != null)
                    {
                        softDeleteCustomer.IsActive = false;
                        softDeleteCustomer.NgayCapNhat = DateTime.Now;
                        await _db.SaveChangesAsync(ct);
                    }
                    TempData["CustomerWarning"] = "Khong the xoa vinh vien vi khach hang dang duoc tham chieu. Hệ thống da chuyen sang trang thai tam ngung.";
                }
            }
        }
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────
    // API Endpoints
    // ──────────────────────────────────────────────

    /// <summary>
    /// Tim kiếm + lọc danh sách khách hàng — trả JSON cho AJAX.
    /// GET /api/customers/search?q=&amp;type=&amp;page=1&amp;pageSize=8
    /// </summary>
    [HttpGet("/api/customers/search")]
    public async Task<IActionResult> SearchApi(
        string? q, string? type, int page = 1, int pageSize = 8,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 8;

        IQueryable<KhachHang> baseQuery = _db.KhachHangs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            baseQuery = baseQuery.Where(x =>
                x.MaKh.Contains(term) ||
                x.HoTen.Contains(term) ||
                x.SoDienThoai.Contains(term) ||
                x.CmndCccd.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var norm = type.Trim().ToLowerInvariant();
            if (norm is "canhan" or "ca-nhan" or "personal")
                baseQuery = baseQuery.Where(x => !EF.Functions.Like(x.LoaiKhachHang.ToLower(), "%doanh%"));
            else if (norm is "doanhnghiep" or "doanh-nghiep" or "business")
                baseQuery = baseQuery.Where(x => EF.Functions.Like(x.LoaiKhachHang.ToLower(), "%doanh%"));
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var totalPages = totalCount <= 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await baseQuery
            .OrderByDescending(x => x.NgayTao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.MaKh,
                MaKhText = x.MaKhText,
                x.HoTen,
                NgaySinhText = x.NgaySinh == default || !HasReasonableBirthDate(x.NgaySinh) ? "Chưa có" : x.NgaySinh.ToString("dd/MM/yyyy"),
                x.CmndCccd,
                LoaiKhachHangText = NormalizeCustomerTypeText(x.LoaiKhachHang),
                LoaiKhachHangKind = MapCustomerTypeKind(x.LoaiKhachHang),
                x.SoDienThoai,
                x.Email,
                x.DiaChi,
                x.AnhDaiDienUrl,
                x.IsActive,
                DiemTinDung = x.LichSuTinDungs
                    .OrderByDescending(ls => ls.NgayCapNhat)
                    .Select(ls => (int?)ls.DiemTinDung)
                    .FirstOrDefault(),
                XepHangRuiRo = x.LichSuTinDungs
                    .OrderByDescending(ls => ls.NgayCapNhat)
                    .Select(ls => ls.XepHangRuiRo)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return Json(new { items, totalCount, page, totalPages });
    }

    /// <summary>
    /// Kiểm tra CCCD/CMND đã tồn tại chưa - dùng cho validate real-time.
    /// GET /api/customers/check-cmnd?value=038...&amp;excludeId=KH001
    /// </summary>
    [HttpGet("/api/customers/check-cmnd")]
    public async Task<IActionResult> CheckCmndApi(string value, string? excludeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Json(new { exists = false });

        var query = _db.KhachHangs.AsNoTracking().Where(x => x.CmndCccd == value.Trim());
        if (!string.IsNullOrWhiteSpace(excludeId))
            query = query.Where(x => x.MaKh != excludeId);

        var exists = await query.AnyAsync(ct);
        return Json(new { exists });
    }

    /// <summary>
    /// Kiểm tra số điện thoại đã tồn tại chưa - dùng cho validate real-time.
    /// GET /api/customers/check-phone?value=09...&amp;excludeId=KH001
    /// </summary>
    [HttpGet("/api/customers/check-phone")]
    public async Task<IActionResult> CheckPhoneApi(string value, string? excludeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Json(new { exists = false });

        var query = _db.KhachHangs.AsNoTracking().Where(x => x.SoDienThoai == value.Trim());
        if (!string.IsNullOrWhiteSpace(excludeId))
            query = query.Where(x => x.MaKh != excludeId);

        var exists = await query.AnyAsync(ct);
        return Json(new { exists });
    }

    [HttpGet("/api/customers/lookup")]
    public async Task<IActionResult> LookupApi(string q, CancellationToken ct = default)
    {
        var term = (q ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(term))
            return BadRequest(new { message = "Thiếu tham số q." });

        KhachHang? customer;
        if (term.StartsWith("KH", StringComparison.OrdinalIgnoreCase))
        {
            customer = await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(x => x.MaKh == term, ct);
        }
        else
        {
            customer = await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(
                x => x.CmndCccd == term || x.SoDienThoai == term,
                ct);
        }

        if (customer == null)
            return NotFound(new { message = "Khong tìm thấy khách hàng." });

        var credit = await GetLatestCreditHistoryAsync(customer.MaKh, ct);
        return Ok(new
        {
            customer.MaKh,
            customer.HoTen,
            customer.CmndCccd,
            customer.SoDienThoai,
            customer.LoaiKhachHang,
            DiemTinDung = credit?.DiemTinDung,
            XepHangRuiRo = credit?.XepHangRuiRo
        });
    }

    [HttpGet("/api/customers/lookup-phone")]
    public async Task<IActionResult> LookupByPhoneApi(string phone, CancellationToken ct = default)
    {
        var term = (phone ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(term))
            return BadRequest(new { message = "Thiếu tham số phone." });

            var customer = await _db.KhachHangs.AsNoTracking().FirstOrDefaultAsync(x => x.SoDienThoai == term, ct);
            if (customer == null)
                return NotFound(new { message = "Khong tìm thấy khách hàng." });

        var credit = await GetLatestCreditHistoryAsync(customer.MaKh, ct);
        return Ok(new
        {
            customer.MaKh,
            customer.HoTen,
            customer.CmndCccd,
            customer.SoDienThoai,
            customer.LoaiKhachHang,
            DiemTinDung = credit?.DiemTinDung,
            XepHangRuiRo = credit?.XepHangRuiRo
        });
    }

    /// <summary>
    /// Kiểm tra số điện thoại đã được dùng ở khách hàng hoặc nhân viên chưa.
    /// GET /api/users/check-phone?value=09...&amp;excludeCustomerId=KH001&amp;excludeEmployeeId=NV001
    /// </summary>
    [HttpGet("/api/users/check-phone")]
    public async Task<IActionResult> CheckSystemPhoneApi(
        string value,
        string? excludeCustomerId = null,
        string? excludeEmployeeId = null,
        CancellationToken ct = default)
    {
        var phone = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(phone))
            return Json(new { exists = false });

        var customerExists = await _db.KhachHangs.AsNoTracking()
            .AnyAsync(x => x.SoDienThoai == phone && (excludeCustomerId == null || x.MaKh != excludeCustomerId), ct);
        var customerLoginExists = await _db.TaiKhoanKhachHangs.AsNoTracking()
            .AnyAsync(x => x.TenDangNhap == phone && (excludeCustomerId == null || x.MaKh != excludeCustomerId), ct);
        var employeeExists = await _db.NhanViens.AsNoTracking()
            .AnyAsync(x => x.SoDienThoai == phone && (excludeEmployeeId == null || x.MaNv != excludeEmployeeId), ct);
        var employeeLoginExists = await _db.TaiKhoanNhanViens.AsNoTracking()
            .AnyAsync(x => x.TenDangNhap == phone && (excludeEmployeeId == null || x.MaNv != excludeEmployeeId), ct);

        return Json(new
        {
            exists = customerExists || customerLoginExists || employeeExists || employeeLoginExists,
            customerExists,
            customerLoginExists,
            employeeExists,
            employeeLoginExists
        });
    }

    /// <summary>
    /// Kiểm tra email đã được dùng ở khách hàng hoặc nhân viên chưa.
    /// GET /api/users/check-email?value=a@gmail.com&amp;excludeCustomerId=KH001&amp;excludeEmployeeId=NV001
    /// </summary>
    [HttpGet("/api/users/check-email")]
    public async Task<IActionResult> CheckSystemEmailApi(
        string value,
        string? excludeCustomerId = null,
        string? excludeEmployeeId = null,
        CancellationToken ct = default)
    {
        var email = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
            return Json(new { exists = false });

        var customerExists = await _db.KhachHangs.AsNoTracking()
            .AnyAsync(x => x.Email == email && (excludeCustomerId == null || x.MaKh != excludeCustomerId), ct);
        var employeeExists = await _db.NhanViens.AsNoTracking()
            .AnyAsync(x => x.Email == email && (excludeEmployeeId == null || x.MaNv != excludeEmployeeId), ct);

        return Json(new
        {
            exists = customerExists || employeeExists,
            customerExists,
            employeeExists
        });
    }

    /// <summary>
    /// Xóa khách hàng qua API — dùng cho xóa inline không reload trang.
    /// DELETE /api/customers/{id}
    /// </summary>
    [PermissionAuthorize(AppPermissions.DeleteCustomers)]
    [HttpDelete("/api/customers/{id}")]
    public async Task<IActionResult> DeleteApi(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null)
            return NotFound(new { message = $"Khong tìm thấy khách hàng: {id}" });

        var hasRelatedData = await _db.DonVays.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
            || await _db.KhoanVays.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
            || await _db.TaiSanKhachHangs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
            || await _db.LichSuTinDungs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct)
            || await _db.HanMucTinDungs.AsNoTracking().AnyAsync(x => x.MaKh == id, ct);

        if (hasRelatedData)
        {
            customer.IsActive = false;
            customer.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "Khach hang da co du lieu nghiep vu nen he thong da tam ngung thay vi xoa vinh vien." });
        }

        try
        {
            var portalAccounts = await _db.TaiKhoanKhachHangs
                .Where(x => x.MaKh == id)
                .ToListAsync(ct);
            _db.TaiKhoanKhachHangs.RemoveRange(portalAccounts);
            _db.KhachHangs.Remove(customer);
            await _db.SaveChangesAsync(ct);
            return Ok(new { success = true, message = "Da xoa khach hang thanh cong." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { success = false, message = "Khong the xoa khach hang vi dang duoc tham chieu boi du lieu nghiep vu." });
        }
    }
}
