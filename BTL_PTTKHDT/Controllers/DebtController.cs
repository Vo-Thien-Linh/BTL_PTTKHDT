using System.Globalization;
using BTL_PTTKHDT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

public sealed class DebtController : Controller
{
    private readonly QltdnhContext _db;

    public DebtController(QltdnhContext db)
    {
        _db = db;
    }

    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        IQueryable<KhoanVay> baseQuery = _db.KhoanVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.MaDonNavigation);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            baseQuery = baseQuery.Where(x =>
                x.MaVay.Contains(term) ||
                x.MaDon.Contains(term) ||
                x.MaKh.Contains(term) ||
                x.MaKhNavigation.HoTen.Contains(term));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(x => x.NgayGiaiNgan)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new DebtLoanRowViewModel
            {
                MaVay = x.MaVay,
                MaDon = x.MaDon,
                MaKh = x.MaKh,
                TenKhachHang = x.MaKhNavigation.HoTen,
                SoTienVay = x.SoTienVay,
                DuNoGoc = x.DuNoGoc,
                KyHan = x.KyHan,
                LaiSuat = x.LaiSuat,
                NgayGiaiNgan = x.NgayGiaiNgan,
                TrangThai = x.TrangThai
            })
            .ToListAsync(ct);

        ViewData["Title"] = "Giải ngân & Quản lý nợ";
        ViewData["Query"] = q;
        ViewData["Page"] = page;
        ViewData["PageSize"] = PageSize;
        ViewData["TotalCount"] = totalCount;

        return View(items);
    }

    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var kv = await _db.KhoanVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.MaDonNavigation)
            .Include(x => x.HopDongTinDung)
            .Include(x => x.LichTraNos)
            .Include(x => x.TaiSanTheChaps)
            .ThenInclude(x => x.MaTaiSanKhNavigation)
            .FirstOrDefaultAsync(x => x.MaVay == id, ct);

        if (kv == null) return NotFound();

        var schedule = kv.LichTraNos
            .OrderBy(x => x.KyThu)
            .Select(x => new DebtScheduleRowViewModel
            {
                MaLichTraNo = x.MaLichTraNo,
                KyThu = x.KyThu,
                NgayPhaiTra = x.NgayPhaiTra,
                SoTienGoc = x.SoTienGoc,
                SoTienLai = x.SoTienLai,
                SoTienDaThanhToan = x.SoTienDaThanhToan,
                TrangThai = x.TrangThai
            })
            .ToList();

        var collaterals = kv.TaiSanTheChaps
            .OrderByDescending(x => x.GiaTriTheChap)
            .Select(x => new DebtCollateralRowViewModel
            {
                MaTaiSan = x.MaTaiSan,
                MaTaiSanKh = x.MaTaiSanKh,
                LoaiTaiSan = x.MaTaiSanKhNavigation.LoaiTaiSan,
                GiaTriTheChap = x.GiaTriTheChap,
                TrangThai = x.TrangThai
            })
            .ToList();

        DebtContractViewModel? contract = null;
        if (kv.HopDongTinDung != null)
        {
            contract = new DebtContractViewModel
            {
                MaHopDong = kv.HopDongTinDung.MaHopDong,
                NgayKyHopDong = kv.HopDongTinDung.NgayKyHopDong,
                MaNv = kv.HopDongTinDung.MaNv
            };
        }

        var vm = new DebtLoanDetailViewModel
        {
            Loan = new DebtLoanRowViewModel
            {
                MaVay = kv.MaVay,
                MaDon = kv.MaDon,
                MaKh = kv.MaKh,
                TenKhachHang = kv.MaKhNavigation.HoTen,
                SoTienVay = kv.SoTienVay,
                DuNoGoc = kv.DuNoGoc,
                KyHan = kv.KyHan,
                LaiSuat = kv.LaiSuat,
                NgayGiaiNgan = kv.NgayGiaiNgan,
                TrangThai = kv.TrangThai
            },
            LichTraNo = schedule,
            TaiSanTheChap = collaterals,
            HopDong = contract,
            ThanhToanMoi = new DebtPaymentCreateViewModel()
        };

        ViewData["Title"] = $"Khoản vay: {kv.MaVay}";
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(string id, DebtPaymentCreateViewModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var kv = await _db.KhoanVays
            .Include(x => x.LichTraNos)
            .FirstOrDefaultAsync(x => x.MaVay == id, ct);

        if (kv == null) return NotFound();

        var maLich = (model.MaLichTraNo ?? string.Empty).Trim();
        var schedule = kv.LichTraNos.FirstOrDefault(x => x.MaLichTraNo == maLich);
        if (schedule == null)
        {
            TempData["DebtError"] = "Không tìm thấy kỳ trả nợ để thanh toán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!TryParseFlexibleDecimal(Request.Form["SoTienThanhToan"].ToString(), out var amount) || amount <= 0)
        {
            TempData["DebtError"] = "Số tiền thanh toán phải là số và lớn hơn 0.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var due = schedule.SoTienGoc + schedule.SoTienLai;
        var alreadyPaid = schedule.SoTienDaThanhToan;
        var remaining = due - alreadyPaid;
        if (remaining <= 0)
        {
            TempData["DebtError"] = "Kỳ này đã thanh toán đủ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (amount > remaining)
        {
            TempData["DebtError"] = $"Số tiền trả không được lớn hơn số tiền còn phải trả của kỳ này ({remaining:N0} đ).";
            return RedirectToAction(nameof(Details), new { id });
        }

        var paymentMethod = string.IsNullOrWhiteSpace(model.HinhThuc) ? "Tien mat" : model.HinhThuc.Trim();
        if (!IsValidPaymentMethod(paymentMethod))
        {
            TempData["DebtError"] = "Hình thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var interestPaidSoFar = Math.Min(schedule.SoTienLai, schedule.SoTienDaThanhToan);
        var principalPaidSoFar = Math.Max(0m, schedule.SoTienDaThanhToan - schedule.SoTienLai);

        var interestRemaining = Math.Max(0m, schedule.SoTienLai - interestPaidSoFar);
        var principalRemaining = Math.Max(0m, schedule.SoTienGoc - principalPaidSoFar);

        var interestPay = Math.Min(amount, interestRemaining);
        var principalPay = Math.Min(amount - interestPay, principalRemaining);

        var now = DateTime.Now;
        var actor = await GetDefaultEmployeeIdAsync(ct);

        schedule.SoTienDaThanhToan += amount;
        if (schedule.SoTienDaThanhToan >= due)
        {
            schedule.TrangThai = "Da tra";
            schedule.NgayThanhToanThucTe = DateOnly.FromDateTime(now);
        }
        else
        {
            schedule.TrangThai = "Tra mot phan";
        }

        kv.DuNoGoc = Math.Max(0m, kv.DuNoGoc - principalPay);
        if (kv.DuNoGoc <= 0 && kv.LichTraNos.All(x => x.TrangThai == "Da tra"))
        {
            kv.TrangThai = "Da tra het";
        }

        var payment = new ThanhToan
        {
            MaThanhToan = await GetNextPaymentCodeAsync(ct),
            MaVay = kv.MaVay,
            MaLichTraNo = schedule.MaLichTraNo,
            MaNv = actor,
            SoTienThanhToan = amount,
            SoTienGocTra = principalPay,
            SoTienLaiTra = interestPay,
            SoTienPhatTra = 0m,
            NgayThanhToan = now,
            HinhThuc = paymentMethod,
            GhiChu = model.GhiChu
        };

        _db.ThanhToans.Add(payment);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<string?> GetDefaultEmployeeIdAsync(CancellationToken ct)
    {
        var nv = await _db.NhanViens.AsNoTracking().OrderBy(x => x.MaNv).Select(x => x.MaNv).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(nv) ? null : nv;
    }

    private static int ParseCodeSuffix(string code, string prefix)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return 0;

        var suffix = code.Substring(prefix.Length);
        return int.TryParse(suffix, out var value) ? value : 0;
    }

    private async Task<string> GetNextPaymentCodeAsync(CancellationToken ct)
    {
        var codes = await _db.ThanhToans.AsNoTracking().Select(x => x.MaThanhToan).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "TT")).DefaultIfEmpty(0).Max();
        return $"TT{(maxId + 1):0000}";
    }

    private static bool TryParseFlexibleDecimal(string? input, out decimal value)
    {
        value = 0m;
        var s = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s)) return false;

        s = s.Replace("₫", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty);

        s = new string(s.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (string.IsNullOrWhiteSpace(s)) return false;

        var dotCount = s.Count(c => c == '.');
        var commaCount = s.Count(c => c == ',');

        if (dotCount > 0 && commaCount > 0)
        {
            s = s.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (dotCount > 1 && commaCount == 0)
        {
            s = s.Replace(".", string.Empty);
        }
        else if (commaCount > 1 && dotCount == 0)
        {
            s = s.Replace(",", string.Empty);
        }
        else if (commaCount == 1 && dotCount == 0)
        {
            var parts = s.Split(',');
            s = parts.Length == 2 && parts[1].Length == 3 ? string.Concat(parts[0], parts[1]) : string.Concat(parts[0], ".", parts.ElementAtOrDefault(1));
        }

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsValidPaymentMethod(string method)
    {
        var m = (method ?? string.Empty).Trim();
        return m is "Tien mat" or "Chuyen khoan" or "Thu no tu dong";
    }
}
