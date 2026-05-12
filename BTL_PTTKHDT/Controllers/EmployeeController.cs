using BTL_PTTKHDT.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers
{
    public sealed class EmployeeController : Controller
    {
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

        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            IQueryable<NhanVien> query = _db.NhanViens.AsNoTracking();
            
            if (!string.IsNullOrWhiteSpace(q)) {
                var terms = q.Trim();
                query = query.Where(x => x.HoTen.Contains(terms) || x.SoDienThoai.Contains(terms) || x.VaiTro.Contains(terms));
            }
            
            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.NgayTao).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            
            ViewBag.Query = q; ViewBag.Page = page; ViewBag.PageSize = pageSize; ViewBag.TotalPages = (int)Math.Ceiling(total/(double)pageSize);
            return View(items);
        }

        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            var model = new NhanVien
            {
                MaNv = await GetNextEmployeeCodeAsync(ct)
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NhanVien m, IFormFile? AnhDaiDienFile, CancellationToken ct = default)
        {
            if (!IsValidCode(m.MaNv, "NV", 3))
            {
                m.MaNv = await GetNextEmployeeCodeAsync(ct);
                ModelState.Remove(nameof(NhanVien.MaNv));
                ModelState.Remove(nameof(NhanVien.MaNvText));
            }

            if (AnhDaiDienFile == null || AnhDaiDienFile.Length <= 0)
            {
                ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện nhân viên là bắt buộc.");
            }

            if (!ModelState.IsValid)
            {
                return View(m);
            }

            try {
                m.NgayTao = DateTime.Now; m.IsActive = true;
                m.AnhDaiDienUrl = await SaveAvatarAsync(AnhDaiDienFile, ct) ?? m.AnhDaiDienUrl;
                _db.NhanViens.Add(m); await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            } catch (Exception ex) {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(nameof(AnhDaiDienFile), innerMsg);
                return View(m);
            }
        }

        public async Task<IActionResult> Edit(string id) => View(await _db.NhanViens.FindAsync(id));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, NhanVien m, IFormFile? AnhDaiDienFile, CancellationToken ct = default)
        {
            var e = await _db.NhanViens.FindAsync(id);
            if(e==null) return NotFound();

            var hasAvatar = !string.IsNullOrWhiteSpace(e.AnhDaiDienUrl) || (AnhDaiDienFile != null && AnhDaiDienFile.Length > 0);
            if (!hasAvatar)
            {
                ModelState.AddModelError(nameof(AnhDaiDienFile), "Ảnh đại diện nhân viên là bắt buộc.");
            }

            if (!ModelState.IsValid)
            {
                return View(m);
            }

            try {
                e.HoTen = m.HoTen; e.SoDienThoai = m.SoDienThoai; e.DiaChi = m.DiaChi; e.NgaySinh = m.NgaySinh; e.GioiTinh = m.GioiTinh; e.VaiTro = m.VaiTro; e.IsActive = m.IsActive;
                var newAvatarUrl = await SaveAvatarAsync(AnhDaiDienFile, ct);
                if (!string.IsNullOrWhiteSpace(newAvatarUrl)) e.AnhDaiDienUrl = newAvatarUrl;
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            } catch (Exception ex) {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(nameof(AnhDaiDienFile), innerMsg);
                return View(m);
            }
        }

        public async Task<IActionResult> Delete(string id) => View(await _db.NhanViens.FindAsync(id));
        [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(string id) {
            var e = await _db.NhanViens.FindAsync(id);
            if(e!=null){ _db.NhanViens.Remove(e); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}