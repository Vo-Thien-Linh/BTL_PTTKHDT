using System.Security.Claims;
using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

[Authorize(Roles = AppRoles.KhachHang)]
public sealed class CustomerPortalController : Controller
{
    private readonly QltdnhContext _db;

    public CustomerPortalController(QltdnhContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var maKh = User.FindFirst("MaKH")?.Value;
        if (string.IsNullOrWhiteSpace(maKh)) return RedirectToAction("Login", "Account");

        var customer = await _db.KhachHangs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == maKh, ct);
        if (customer == null) return NotFound();

        var applications = await _db.DonVays
            .AsNoTracking()
            .Include(x => x.QuyTrinhPheDuyets)
            .Include(x => x.KhoanVays)
            .Where(x => x.MaKh == maKh)
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(ct);

        var loans = await _db.KhoanVays
            .AsNoTracking()
            .Where(x => x.MaKh == maKh)
            .OrderByDescending(x => x.NgayGiaiNgan)
            .ToListAsync(ct);

        var loanIds = loans.Select(x => x.MaVay).ToList();
        var schedules = await _db.LichTraNos
            .AsNoTracking()
            .Where(x => loanIds.Contains(x.MaVay))
            .OrderBy(x => x.NgayPhaiTra)
            .ThenBy(x => x.KyThu)
            .ToListAsync(ct);

        var payments = await _db.ThanhToans
            .AsNoTracking()
            .Where(x => loanIds.Contains(x.MaVay))
            .OrderByDescending(x => x.NgayThanhToan)
            .ToListAsync(ct);

        var creditLimit = await _db.HanMucTinDungs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == maKh, ct);

        var assets = await _db.TaiSanKhachHangs
            .AsNoTracking()
            .Where(x => x.MaKh == maKh && x.TrangThaiSoHuu == "Đang sở hữu")
            .ToListAsync(ct);

        var collateralValue = assets.Sum(x => x.GiaTriDinhGia ?? x.GiaTriKhaiBao);
        var suggestedLimit = assets.Sum(x => (x.GiaTriDinhGia ?? x.GiaTriKhaiBao) * (decimal)x.TyLeLtv);
        var availableLoanLimit = CalculateAvailableLoanLimit(creditLimit?.HanMucConLai, suggestedLimit);
        var ltv = collateralValue <= 0 ? 0m : suggestedLimit / collateralValue;
        var unpaidSchedules = schedules.Where(x => x.TrangThai != "Đã trả").ToList();
        var overdueSchedules = unpaidSchedules.Where(x => x.TrangThai == "Trễ hạn" || x.NgayPhaiTra < DateOnly.FromDateTime(DateTime.Now)).ToList();

        var model = new CustomerPortalDashboardViewModel
        {
            Profile = new CustomerPortalProfileViewModel
            {
                MaKh = customer.MaKh,
                HoTen = customer.HoTen,
                SoDienThoai = customer.SoDienThoai,
                Email = customer.Email,
                DiaChi = customer.DiaChi,
                SoGiayTo = string.IsNullOrWhiteSpace(customer.MaSoThue) ? customer.CmndCccd : customer.MaSoThue,
                LoaiKhachHang = customer.LoaiKhachHang,
                TrangThai = customer.IsActive ? "Đang hoạt động" : "Tạm ngừng",
                NgheNghiep = customer.NgheNghiep,
                NoiLamViec = customer.NoiLamViec,
                ChucVu = customer.ChucVu,
                ThuNhapHangThang = customer.ThuNhapHangThang
            },
            Applications = applications.Select(MapApplication).ToList(),
            CreditLimit = new CustomerPortalCreditLimitViewModel
            {
                CoHanMucTinDung = creditLimit != null,
                HanMucToiDa = creditLimit?.HanMucToiDa,
                HanMucDaSuDung = creditLimit?.HanMucDaSuDung,
                HanMucConLai = creditLimit?.HanMucConLai,
                HanMucGoiYTheoTaiSan = suggestedLimit,
                SoTienCoTheVay = availableLoanLimit,
                TongGiaTriTaiSanBaoDam = collateralValue,
                TyLeLtvTongHop = ltv
            },
            ActiveLoans = loans.Select(x => new CustomerPortalLoanViewModel
            {
                MaVay = x.MaVay,
                SoTienVay = x.SoTienVay,
                DuNoGoc = x.DuNoGoc,
                LaiSuat = x.LaiSuat,
                KyHan = x.KyHan,
                NgayGiaiNgan = x.NgayGiaiNgan,
                NgayDaoHan = x.NgayDaoHan,
                TrangThai = x.TrangThai
            }).ToList(),
            RepaymentSchedule = schedules.Select(x => new CustomerPortalScheduleViewModel
            {
                MaVay = x.MaVay,
                KyThu = x.KyThu,
                NgayPhaiTra = x.NgayPhaiTra,
                SoTienGoc = x.SoTienGoc,
                SoTienLai = x.SoTienLai,
                TongPhaiTra = x.SoTienGoc + x.SoTienLai,
                SoTienDaThanhToan = x.SoTienDaThanhToan,
                TrangThai = x.TrangThai
            }).ToList(),
            DebtStatus = new CustomerPortalDebtStatusViewModel
            {
                TongDuNoHienTai = loans.Sum(x => x.DuNoGoc),
                SoKyConPhaiTra = unpaidSchedules.Count,
                SoKyTreHan = overdueSchedules.Count,
                NhomNoCaoNhat = loans.Select(x => x.NhomNo).DefaultIfEmpty((byte)1).Max(),
                CoCanhBaoQuaHan = overdueSchedules.Count > 0
            },
            Payments = payments.Select(x => new CustomerPortalPaymentViewModel
            {
                NgayThanhToan = x.NgayThanhToan,
                SoTienThanhToan = x.SoTienThanhToan,
                SoTienGocTra = x.SoTienGocTra,
                SoTienLaiTra = x.SoTienLaiTra,
                SoTienPhatTra = x.SoTienPhatTra,
                HinhThuc = x.HinhThuc
            }).ToList()
        };

        return View(model);
    }

    private static decimal CalculateAvailableLoanLimit(decimal? creditLimitRemaining, decimal collateralLimit)
    {
        var safeCollateralLimit = Math.Max(0m, collateralLimit);
        if (!creditLimitRemaining.HasValue)
        {
            return safeCollateralLimit;
        }

        return Math.Min(Math.Max(0m, creditLimitRemaining.Value), safeCollateralLimit);
    }

    private static CustomerPortalLoanApplicationViewModel MapApplication(DonVay application)
    {
        var loan = application.KhoanVays.OrderByDescending(x => x.NgayGiaiNgan).FirstOrDefault();
        var rejectNote = application.QuyTrinhPheDuyets
            .Where(x => x.TrangThai == "Từ chối")
            .OrderByDescending(x => x.NgayXuLy)
            .Select(x => x.GhiChu)
            .FirstOrDefault();

        return new CustomerPortalLoanApplicationViewModel
        {
            MaDon = application.MaDon,
            MucDichVay = application.MucDichVay,
            SoTienYeuCau = application.SoTienYeuCau,
            KyHanDeNghi = application.KyHanDeNghi,
            LaiSuatDeNghi = application.LaiSuatDeNghi,
            TrangThaiDon = application.TrangThaiDon,
            LyDoTuChoi = rejectNote,
            SoTienDuocDuyet = loan?.SoTienVay,
            KyHanDuocDuyet = loan?.KyHan,
            LaiSuatApDung = loan?.LaiSuat,
            NgayGiaiNgan = loan?.NgayGiaiNgan
        };
    }
}
