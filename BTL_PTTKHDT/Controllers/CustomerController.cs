using BTL_PTTKHDT.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

public sealed class CustomerController : Controller
{
    private readonly QltdnhContext _db;
    private readonly IWebHostEnvironment _env;

    public CustomerController(QltdnhContext db, IWebHostEnvironment env)
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

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? type, int page = 1, int pageSize = 4, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 4;

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

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(x => x.NgayTao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CustomerRowViewModel
            {
                MaKh = x.MaKh,
                MaKhText = x.MaKhText,
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
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        var model = new CustomerListViewModel
        {
            Items = items,
            Query = q,
            Type = type,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        ViewData["Title"] = "Quản lý Khách hàng";
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

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        var model = new KhachHang
        {
            MaKh = await GetNextCustomerCodeAsync(ct)
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(KhachHang model, IFormFile? AnhDaiDienFile, CancellationToken ct = default)
    {
        if (!IsValidCode(model.MaKh, "KH", 3))
        {
            model.MaKh = await GetNextCustomerCodeAsync(ct);
            ModelState.Remove(nameof(KhachHang.MaKh));
            ModelState.Remove(nameof(KhachHang.MaKhText));
        }

        var isBusiness = MapCustomerTypeKind(model.LoaiKhachHang) == "business";
        if (!isBusiness && (AnhDaiDienFile == null || AnhDaiDienFile.Length <= 0))
        {
            ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện khách hàng là bắt buộc với khách hàng cá nhân.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            model.NgayTao = DateTime.Now;
            model.NgayCapNhat = DateTime.Now;
            model.IsActive = true;
            model.AnhDaiDienUrl = await SaveAvatarAsync(AnhDaiDienFile, ct) ?? model.AnhDaiDienUrl;
            _db.KhachHangs.Add(model);
            await _db.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError(nameof(AnhDaiDienFile), innerMsg);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();

        var assets = await _db.TaiSanKhachHangs
            .AsNoTracking()
            .Where(x => x.MaKh == customer.MaKh)
            .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
            .ToListAsync(ct);

        ViewData["CustomerAssets"] = assets;
        return View(customer);
    }

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
            TrangThai = "Chua dinh gia"
        };

        _db.TaiSanKhachHangs.Add(entity);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, KhachHang model, IFormFile? AnhDaiDienFile, CancellationToken ct = default)
    {
        var existing = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (existing == null) return NotFound();

        var isBusiness = MapCustomerTypeKind(model.LoaiKhachHang) == "business";
        var hasAvatar = !string.IsNullOrWhiteSpace(existing.AnhDaiDienUrl) || (AnhDaiDienFile != null && AnhDaiDienFile.Length > 0);
        if (!isBusiness && !hasAvatar)
        {
            ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện khách hàng là bắt buộc với khách hàng cá nhân.");
        }

        if (!ModelState.IsValid)
        {
            var assets = await _db.TaiSanKhachHangs
                .AsNoTracking()
                .Where(x => x.MaKh == existing.MaKh)
                .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
                .ToListAsync(ct);

            ViewData["CustomerAssets"] = assets;
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
            existing.IsActive = model.IsActive;
            existing.NgayCapNhat = DateTime.Now;

            var newAvatarUrl = await SaveAvatarAsync(AnhDaiDienFile, ct);
            if (!string.IsNullOrWhiteSpace(newAvatarUrl)) existing.AnhDaiDienUrl = newAvatarUrl;

            _db.KhachHangs.Update(existing);
            await _db.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            ModelState.AddModelError(nameof(AnhDaiDienFile), innerMsg);

            var assets = await _db.TaiSanKhachHangs
                .AsNoTracking()
                .Where(x => x.MaKh == existing.MaKh)
                .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
                .ToListAsync(ct);

            ViewData["CustomerAssets"] = assets;
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer != null)
        {
            _db.KhachHangs.Remove(customer);
            await _db.SaveChangesAsync(ct);
        }
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────
    // API Endpoints
    // ──────────────────────────────────────────────

    /// <summary>
    /// Tìm kiếm + lọc danh sách khách hàng — trả JSON cho AJAX.
    /// GET /api/customers/search?q=&amp;type=&amp;page=1&amp;pageSize=4
    /// </summary>
    [HttpGet("/api/customers/search")]
    public async Task<IActionResult> SearchApi(
        string? q, string? type, int page = 1, int pageSize = 4,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 4;

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
                NgaySinhText = x.NgaySinh.ToString("dd/MM/yyyy"),
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
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return Json(new { items, totalCount, page, totalPages });
    }

    /// <summary>
    /// Kiểm tra CCCD/CMND đã tồn tại chưa — dùng cho validate real-time.
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
            return NotFound(new { message = "Không tìm thấy khách hàng." });

        return Ok(new
        {
            customer.MaKh,
            customer.HoTen,
            customer.CmndCccd,
            customer.SoDienThoai,
            customer.LoaiKhachHang
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
            return NotFound(new { message = "Không tìm thấy khách hàng." });

        return Ok(new
        {
            customer.MaKh,
            customer.HoTen,
            customer.CmndCccd,
            customer.SoDienThoai,
            customer.LoaiKhachHang
        });
    }

    /// <summary>
    /// Xóa khách hàng qua API — dùng cho xóa inline không reload trang.
    /// DELETE /api/customers/{id}
    /// </summary>
    [HttpDelete("/api/customers/{id}")]
    public async Task<IActionResult> DeleteApi(string id, CancellationToken ct = default)
    {
        var customer = await _db.KhachHangs.FirstOrDefaultAsync(x => x.MaKh == id, ct);
        if (customer == null)
            return NotFound(new { message = $"Không tìm thấy khách hàng: {id}" });

        _db.KhachHangs.Remove(customer);
        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Đã xóa khách hàng thành công." });
    }
}
