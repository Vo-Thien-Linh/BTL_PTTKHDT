using System.Globalization;
using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

public sealed class DisbursementController : Controller
{
    private readonly QltdnhContext _db;
    private readonly ICreditScoreService _creditScoreService;
    private const int PageSize = 10;

    public DisbursementController(QltdnhContext db, ICreditScoreService creditScoreService)
    {
        _db = db;
        _creditScoreService = creditScoreService;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        var query = _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Where(x => x.TrangThaiDon == "Đã duyệt" && !x.KhoanVays.Any());

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                x.MaDon.Contains(term) ||
                x.MaKh.Contains(term) ||
                x.MucDichVay.Contains(term) ||
                x.MaKhNavigation.HoTen.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.NgayCapNhat)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        var items = new List<DisbursementRowViewModel>();
        foreach (var row in rows)
        {
            items.Add(await BuildRowAsync(row, ct));
        }

        ViewData["Title"] = "Giải ngân";
        return View(new DisbursementListViewModel
        {
            Items = items,
            Query = q,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        });
    }

    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        var don = await _db.DonVays
            .AsNoTracking()
            .Include(x => x.MaKhNavigation)
            .Include(x => x.KhoanVays)
            .FirstOrDefaultAsync(x => x.MaDon == id, ct);

        if (don == null) return NotFound();
        if (don.KhoanVays.Any())
        {
            var existingLoan = don.KhoanVays.First();
            return RedirectToAction("Details", "Debt", new { id = existingLoan.MaVay });
        }

        var assets = await GetEligibleAssetsAsync(don.MaKh, ct);
        var activeLoan = await GetActiveCustomerLoanAsync(don.MaKh, don.MaDon, ct);

        ViewData["Title"] = $"Kiem tra giai ngan: {don.MaDon}";
        return View(new DisbursementDetailViewModel
        {
            Loan = await BuildRowAsync(don, ct),
            TaiSanDamBao = assets,
            HasActiveLoan = activeLoan != null,
            ActiveLoanMessage = activeLoan == null
                ? null
                : $"Khach hang dang co khoan vay {activeLoan.MaVay} chua tat toan, du no goc {FormatMoney(activeLoan.DuNoGoc)}."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disburse(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var don = await _db.DonVays
            .Include(x => x.KhoanVays)
            .FirstOrDefaultAsync(x => x.MaDon == id, ct);
        if (don == null) return NotFound();

        if (don.TrangThaiDon != "Đã duyệt")
        {
            TempData["DisburseError"] = "Chi duoc giai ngan ho so da duoc phe duyet.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (don.KhoanVays.Any())
        {
            return RedirectToAction("Details", "Debt", new { id = don.KhoanVays.First().MaVay });
        }

        var activeCustomerLoan = await GetActiveCustomerLoanAsync(don.MaKh, don.MaDon, ct);
        if (activeCustomerLoan != null)
        {
            TempData["DisburseError"] =
                $"Khach hang dang co khoan vay {activeCustomerLoan.MaVay} chua tat toan, du no goc {FormatMoney(activeCustomerLoan.DuNoGoc)}. Khong the giai ngan them.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var assets = await _db.TaiSanKhachHangs
            .Where(x => x.MaKh == don.MaKh && x.TrangThaiSoHuu == "Đang sở hữu")
            .ToListAsync(ct);
        if (assets.Count == 0)
        {
            TempData["DisburseError"] = "Can co it nhat 1 tai san dam bao truoc khi giai ngan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var collateralLimit = assets.Sum(CalculateCollateralLimit);
        if (collateralLimit < don.SoTienYeuCau)
        {
            TempData["DisburseError"] =
                $"Han muc bao dam theo LTV {FormatMoney(collateralLimit)} nho hon so tien vay {FormatMoney(don.SoTienYeuCau)}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.Now;
        var actor = await GetDefaultEmployeeIdAsync(ct);
        if (string.IsNullOrWhiteSpace(actor))
        {
            TempData["DisburseError"] = "Chua co nhan vien de ghi nhan giai ngan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var maVay = await GetNextKhoanVayCodeAsync(ct);
        var ngayGiaiNgan = DateOnly.FromDateTime(now);
        var kyHan = don.KyHanDeNghi;
        var laiSuat = don.LaiSuatDeNghi ?? 12d;
        var soTienVay = don.SoTienYeuCau;

        _db.KhoanVays.Add(new KhoanVay
        {
            MaVay = maVay,
            MaDon = don.MaDon,
            MaKh = don.MaKh,
            SoTienVay = soTienVay,
            LaiSuat = laiSuat,
            KyHan = kyHan,
            PhuongThucTraNo = "Gốc lãi đều",
            NgayGiaiNgan = ngayGiaiNgan,
            NgayDaoHan = ngayGiaiNgan.AddMonths(kyHan),
            DuNoGoc = soTienVay,
            TrangThai = "Đang vay",
            NhomNo = 1,
            NgayCapNhatNhom = ngayGiaiNgan,
            GhiChu = null,
            NgayTao = now
        });

        _db.HopDongTinDungs.Add(new HopDongTinDung
        {
            MaHopDong = await GetNextHopDongCodeAsync(ct),
            MaVay = maVay,
            MaNv = actor,
            NgayKyHopDong = ngayGiaiNgan,
            NoiDung = null,
            DieuKhoan = null,
            FileUrl = null,
            NgayTao = now
        });

        var (_, tscNextStart) = await GetNextTaiSanTheChapSuffixAsync(ct);
        var tscNext = tscNextStart;
        foreach (var a in assets)
        {
            _db.TaiSanTheChaps.Add(new TaiSanTheChap
            {
                MaTaiSan = $"TSC{tscNext:0000}",
                MaVay = maVay,
                MaTaiSanKh = a.MaTaiSanKh,
                GiaTriTheChap = CalculateCollateralLimit(a),
                NgayTheChap = ngayGiaiNgan,
                NgayGiaiChap = null,
                TrangThai = "Đang thế chấp",
                GhiChu = null
            });
            tscNext++;
        }

        var (_, ltnNextStart) = await GetNextLichTraNoSuffixAsync(ct);
        var ltnNext = ltnNextStart;
        foreach (var row in BuildScheduleGocLaiDeu(soTienVay, laiSuat, kyHan, ngayGiaiNgan))
        {
            _db.LichTraNos.Add(new LichTraNo
            {
                MaLichTraNo = $"LTN{ltnNext:0000}",
                MaVay = maVay,
                KyThu = row.kyThu,
                NgayPhaiTra = row.ngayPhaiTra,
                SoTienGoc = row.goc,
                SoTienLai = row.lai,
                SoTienDaThanhToan = 0m,
                TrangThai = "Chưa trả",
                NgayThanhToanThucTe = null,
                GhiChu = null
            });
            ltnNext++;
        }

        await _db.SaveChangesAsync(ct);
        await _creditScoreService.RecalculateAsync(don.MaKh, "Giải ngân", ct);

        return RedirectToAction("Details", "Debt", new { id = maVay });
    }

    private async Task<DisbursementRowViewModel> BuildRowAsync(DonVay don, CancellationToken ct)
    {
        var assets = await GetEligibleAssetsAsync(don.MaKh, ct);
        var tong = assets.Sum(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao);
        var hanMuc = assets.Sum(CalculateCollateralLimit);

        return new DisbursementRowViewModel
        {
            MaDon = don.MaDon,
            MaKh = don.MaKh,
            TenKhachHang = don.MaKhNavigation.HoTen,
            MucDichVay = don.MucDichVay,
            SoTienYeuCau = don.SoTienYeuCau,
            KyHanDeNghi = don.KyHanDeNghi,
            LaiSuatDeNghi = don.LaiSuatDeNghi,
            NgayNopDon = don.NgayNopDon,
            TongGiaTriDamBao = tong,
            HanMucGoiY = hanMuc,
            DaDuDieuKienTaiSan = hanMuc >= don.SoTienYeuCau
        };
    }

    private static decimal CalculateCollateralLimit(TaiSanKhachHang asset)
    {
        var baseValue = asset.GiaTriDinhGia ?? asset.GiaTriKhaiBao;
        return baseValue * (decimal)asset.TyLeLtv;
    }

    private static decimal CalculateCollateralLimit(LoanCollateralViewModel asset)
    {
        var baseValue = asset.GiaTriDinhGia ?? asset.GiaTriKhaiBao;
        return baseValue * (decimal)asset.TyLeLtv;
    }

    private async Task<IReadOnlyList<LoanCollateralViewModel>> GetEligibleAssetsAsync(string maKh, CancellationToken ct)
    {
        var assets = await _db.TaiSanKhachHangs
            .AsNoTracking()
            .Where(x => x.MaKh == maKh && x.TrangThaiSoHuu == "Đang sở hữu")
            .OrderByDescending(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao)
            .ToListAsync(ct);

        return assets.Select(x => new LoanCollateralViewModel
        {
            MaTaiSanKh = x.MaTaiSanKh,
            LoaiTaiSan = x.LoaiTaiSan,
            GiaTriKhaiBao = x.GiaTriKhaiBao,
            GiaTriDinhGia = x.GiaTriDinhGia,
            TyLeLtv = x.TyLeLtv,
            TrangThai = x.TrangThai,
            TrangThaiSoHuu = x.TrangThaiSoHuu,
            MoTa = x.MoTa,
            GiayToPhapLy = x.GiayToPhapLy
        }).ToList();
    }

    private sealed record ActiveCustomerLoanInfo(string MaVay, decimal DuNoGoc, string TrangThai);

    private async Task<ActiveCustomerLoanInfo?> GetActiveCustomerLoanAsync(string maKh, string? exceptLoanApplicationId, CancellationToken ct)
    {
        return await _db.KhoanVays
            .AsNoTracking()
            .Where(x =>
                x.MaKh == maKh
                && (exceptLoanApplicationId == null || x.MaDon != exceptLoanApplicationId)
                && x.DuNoGoc > 0
                && (x.TrangThai == "Đang vay" || x.TrangThai == "Quá hạn" || x.TrangThai == "Cơ cấu lại"))
            .OrderByDescending(x => x.NgayGiaiNgan)
            .Select(x => new ActiveCustomerLoanInfo(x.MaVay, x.DuNoGoc, x.TrangThai))
            .FirstOrDefaultAsync(ct);
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

        return int.TryParse(code[prefix.Length..], out var value) ? value : 0;
    }

    private async Task<string> GetNextKhoanVayCodeAsync(CancellationToken ct)
    {
        var codes = await _db.KhoanVays.AsNoTracking().Select(x => x.MaVay).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "KV")).DefaultIfEmpty(0).Max();
        return $"KV{(maxId + 1):0000}";
    }

    private async Task<string> GetNextHopDongCodeAsync(CancellationToken ct)
    {
        var codes = await _db.HopDongTinDungs.AsNoTracking().Select(x => x.MaHopDong).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "HD")).DefaultIfEmpty(0).Max();
        return $"HD{(maxId + 1):0000}";
    }

    private async Task<(int start, int next)> GetNextLichTraNoSuffixAsync(CancellationToken ct)
    {
        var codes = await _db.LichTraNos.AsNoTracking().Select(x => x.MaLichTraNo).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "LTN")).DefaultIfEmpty(0).Max();
        return (maxId, maxId + 1);
    }

    private async Task<(int start, int next)> GetNextTaiSanTheChapSuffixAsync(CancellationToken ct)
    {
        var codes = await _db.TaiSanTheChaps.AsNoTracking().Select(x => x.MaTaiSan).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "TSC")).DefaultIfEmpty(0).Max();
        return (maxId, maxId + 1);
    }

    private static IEnumerable<(int kyThu, DateOnly ngayPhaiTra, decimal goc, decimal lai)> BuildScheduleGocLaiDeu(decimal principal, double annualRatePercent, int termMonths, DateOnly disburseDate)
    {
        if (termMonths <= 0 || principal <= 0) yield break;

        var r = (decimal)(annualRatePercent / 100d / 12d);
        decimal payment;
        if (r <= 0)
        {
            payment = principal / termMonths;
        }
        else
        {
            var pow = (decimal)Math.Pow((double)(1m + r), -termMonths);
            payment = principal * r / (1m - pow);
        }

        var remaining = principal;
        for (var k = 1; k <= termMonths; k++)
        {
            var interest = r <= 0 ? 0m : remaining * r;
            var principalPay = payment - interest;

            if (k == termMonths)
            {
                principalPay = remaining;
                payment = principalPay + interest;
            }

            remaining -= principalPay;
            if (remaining < 0) remaining = 0;

            yield return (
                k,
                disburseDate.AddMonths(k),
                DecimalRoundMoney(principalPay),
                DecimalRoundMoney(interest)
            );
        }
    }

    private static decimal DecimalRoundMoney(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static string FormatMoney(decimal value) =>
        value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " VND";
}
