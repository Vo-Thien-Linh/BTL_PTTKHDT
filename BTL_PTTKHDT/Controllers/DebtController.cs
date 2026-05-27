using System.Globalization;
using System.Data;
using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Security;
using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Controllers;

[PermissionAuthorize(AppPermissions.ViewDebts)]
public sealed class DebtController : Controller
{
    private readonly QltdnhContext _db;
    private readonly ICreditScoreService _creditScoreService;

    public DebtController(QltdnhContext db, ICreditScoreService creditScoreService)
    {
        _db = db;
        _creditScoreService = creditScoreService;
    }

    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        await RefreshOverdueStatusAsync(ct);

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
                NhomNo = x.NhomNo,
                NgayGiaiNgan = x.NgayGiaiNgan,
                TrangThai = x.TrangThai
            })
            .ToListAsync(ct);

        ViewData["Title"] = "Quan ly no";
        ViewData["Query"] = q;
        ViewData["Page"] = page;
        ViewData["PageSize"] = PageSize;
        ViewData["TotalCount"] = totalCount;

        return View(items);
    }

    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        await RefreshOverdueStatusAsync(id, ct);

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
                GhiChu = x.GhiChu,
                TrangThai = x.TrangThai,
                DaysOverdue = CalculateDaysOverdue(x, DateOnly.FromDateTime(DateTime.Now)),
                WasPaidLate = x.TrangThai == "Đã trả"
                    && x.NgayThanhToanThucTe.HasValue
                    && x.NgayThanhToanThucTe.Value > x.NgayPhaiTra
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
                NhomNo = kv.NhomNo,
                NgayGiaiNgan = kv.NgayGiaiNgan,
                TrangThai = kv.TrangThai
            },
            LichTraNo = schedule,
            TaiSanTheChap = collaterals,
            HopDong = contract,
            ThanhToanMoi = new DebtPaymentCreateViewModel(),
            XuLyThuHoiNo = await LoadCollectionActionsAsync(kv.MaVay, ct),
            XuLyMoi = new DebtCollectionCreateViewModel(),
            LichSuCoCauNo = await LoadRestructureHistoryAsync(kv.MaVay, ct),
            CoCauMoi = new DebtRestructureCreateViewModel
            {
                KyHanMoi = kv.KyHan + 6,
                LaiSuatMoi = kv.LaiSuat
            }
        };

        ViewData["Title"] = $"Khoản vay: {kv.MaVay}";
        return View(vm);
    }

    private async Task<IReadOnlyList<DebtCollectionActionViewModel>> LoadCollectionActionsAsync(string maVay, CancellationToken ct)
    {
        if (!await TableExistsAsync("XuLyThuHoiNo", ct))
        {
            ViewData["DebtFeatureWarning"] = "Chưa có bảng XuLyThuHoiNo/CoCauNo trong database. Hãy chạy migration trước khi dùng xử lý thu hồi nợ/cơ cấu nợ.";
            return [];
        }

        try
        {
            return await _db.XuLyThuHoiNos
                .AsNoTracking()
                .Include(x => x.MaNvNavigation)
                .Where(x => x.MaVay == maVay)
                .OrderByDescending(x => x.NgayXuLy)
                .Select(x => new DebtCollectionActionViewModel
                {
                    MaXuLy = x.MaXuLy,
                    NgayXuLy = x.NgayXuLy,
                    MaNv = x.MaNv,
                    TenNhanVien = x.MaNvNavigation.HoTen,
                    HinhThucLienHe = x.HinhThucLienHe,
                    KetQua = x.KetQua,
                    NgayHenTra = x.NgayHenTra,
                    SoTienHenTra = x.SoTienHenTra,
                    DeXuatXuLy = x.DeXuatXuLy,
                    GhiChu = x.GhiChu
                })
                .ToListAsync(ct);
        }
        catch
        {
            ViewData["DebtFeatureWarning"] = "Bảng XuLyThuHoiNo chưa khớp cấu trúc code. Hãy chạy lại migration.";
            return [];
        }
    }

    private async Task<IReadOnlyList<DebtRestructureHistoryViewModel>> LoadRestructureHistoryAsync(string maVay, CancellationToken ct)
    {
        if (!await TableExistsAsync("CoCauNo", ct))
        {
            ViewData["DebtFeatureWarning"] = "Chưa có bảng XuLyThuHoiNo/CoCauNo trong database. Hãy chạy migration trước khi dùng xử lý thu hồi nợ/cơ cấu nợ.";
            return [];
        }

        try
        {
            return await _db.CoCauNos
                .AsNoTracking()
                .Include(x => x.MaNvNavigation)
                .Where(x => x.MaVay == maVay)
                .OrderByDescending(x => x.NgayCoCau)
                .Select(x => new DebtRestructureHistoryViewModel
                {
                    MaCoCau = x.MaCoCau,
                    NgayCoCau = x.NgayCoCau,
                    MaNv = x.MaNv,
                    TenNhanVien = x.MaNvNavigation.HoTen,
                    KyHanCu = x.KyHanCu,
                    KyHanMoi = x.KyHanMoi,
                    LaiSuatCu = x.LaiSuatCu,
                    LaiSuatMoi = x.LaiSuatMoi,
                    NgayDaoHanCu = x.NgayDaoHanCu,
                    NgayDaoHanMoi = x.NgayDaoHanMoi,
                    DuNoGocCoCau = x.DuNoGocCoCau,
                    LyDo = x.LyDo,
                    GhiChu = x.GhiChu
                })
                .ToListAsync(ct);
        }
        catch
        {
            ViewData["DebtFeatureWarning"] = "Bảng CoCauNo chưa khớp cấu trúc code. Hãy chạy lại migration.";
            return [];
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(AppPermissions.CollectDebts)]
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
            TempData["DebtError"] = "Khong tìm thấy kỳ trả nợ để thanh toán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var earlierUnpaid = kv.LichTraNos
            .Select(x => new
            {
                x.KyThu,
                x.MaLichTraNo,
                Due = x.SoTienGoc + x.SoTienLai,
                x.SoTienDaThanhToan
            })
            .Where(x => x.KyThu < schedule.KyThu)
            .Where(x => x.Due > 0m && x.SoTienDaThanhToan < x.Due)
            .OrderBy(x => x.KyThu)
            .FirstOrDefault();

        if (earlierUnpaid != null)
        {
            TempData["DebtError"] = $"Vui lòng thanh toán theo thứ tự kỳ. Kỳ {earlierUnpaid.KyThu} chưa thanh toán đủ nên không thể thanh toán kỳ {schedule.KyThu}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!TryParseFlexibleDecimal(Request.Form["SoTienThanhToan"].ToString(), out var amount) || amount <= 0)
        {
            TempData["DebtError"] = "So tien thanh toán phải là số và lớn hơn 0.";
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
            TempData["DebtError"] = $"So tien trả không được lớn hơn số tiền còn phải trả của kỳ này ({remaining:N0} đ).";
            return RedirectToAction(nameof(Details), new { id });
        }

        var paymentMethod = string.IsNullOrWhiteSpace(model.HinhThuc) ? "Tiền mặt" : model.HinhThuc.Trim();
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
            schedule.TrangThai = "Đã trả";
            schedule.NgayThanhToanThucTe = DateOnly.FromDateTime(now);
        }
        else
        {
            schedule.TrangThai = "Trả một phần";
        }

        kv.DuNoGoc = Math.Max(0m, kv.DuNoGoc - principalPay);
        await ReduceUsedCreditLimitAsync(kv.MaKh, principalPay, DateOnly.FromDateTime(now), ct);
        if (kv.DuNoGoc <= 0 && kv.LichTraNos.All(x => x.TrangThai == "Đã trả"))
        {
            kv.TrangThai = "Đã trả hết";
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
        RefreshLoanOverdueStatus(kv, DateOnly.FromDateTime(now));
        await _db.SaveChangesAsync(ct);
        await _creditScoreService.RecalculateAsync(kv.MaKh, "Thanh toán", ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(AppPermissions.CollectDebts)]
    public async Task<IActionResult> Payoff(string id, string? hinhThuc, string? ghiChu, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var kv = await _db.KhoanVays
            .Include(x => x.LichTraNos)
            .Include(x => x.TaiSanTheChaps)
            .ThenInclude(x => x.MaTaiSanKhNavigation)
            .FirstOrDefaultAsync(x => x.MaVay == id, ct);

        if (kv == null) return NotFound();

        if (kv.DuNoGoc <= 0 && kv.LichTraNos.All(x => x.TrangThai == "Đã trả"))
        {
            TempData["DebtError"] = "Khoản vay đã tất toán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var paymentMethod = string.IsNullOrWhiteSpace(hinhThuc) ? "Tiền mặt" : hinhThuc.Trim();
        if (!IsValidPaymentMethod(paymentMethod))
        {
            TempData["DebtError"] = "Hình thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var schedules = kv.LichTraNos
            .Where(x => x.TrangThai != "Đã trả")
            .OrderBy(x => x.KyThu)
            .ToList();

        var principalPay = 0m;
        var interestPay = 0m;
        var now = DateTime.Now;

        foreach (var schedule in schedules)
        {
            var interestPaidSoFar = Math.Min(schedule.SoTienLai, schedule.SoTienDaThanhToan);
            var principalPaidSoFar = Math.Max(0m, schedule.SoTienDaThanhToan - schedule.SoTienLai);

            var interestRemaining = Math.Max(0m, schedule.SoTienLai - interestPaidSoFar);
            var principalRemaining = Math.Max(0m, schedule.SoTienGoc - principalPaidSoFar);
            var remaining = interestRemaining + principalRemaining;

            if (remaining <= 0)
            {
                schedule.TrangThai = "Đã trả";
                schedule.NgayThanhToanThucTe ??= DateOnly.FromDateTime(now);
                continue;
            }

            schedule.SoTienDaThanhToan += remaining;
            schedule.TrangThai = "Đã trả";
            schedule.NgayThanhToanThucTe = DateOnly.FromDateTime(now);

            interestPay += interestRemaining;
            principalPay += principalRemaining;
        }

        var totalPay = principalPay + interestPay;
        if (totalPay <= 0)
        {
            TempData["DebtError"] = "Khong còn số tiền cần thanh toán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        kv.DuNoGoc = 0m;
        kv.TrangThai = "Đã trả hết";
        await ReduceUsedCreditLimitAsync(kv.MaKh, principalPay, DateOnly.FromDateTime(now), ct);

        foreach (var collateral in kv.TaiSanTheChaps.Where(x => x.TrangThai == "Đang thế chấp" || x.TrangThai == "Xử lý"))
        {
            collateral.TrangThai = "Đã giải chấp";
            collateral.NgayGiaiChap = DateOnly.FromDateTime(now);
        }

        var actor = await GetDefaultEmployeeIdAsync(ct);
        _db.ThanhToans.Add(new ThanhToan
        {
            MaThanhToan = await GetNextPaymentCodeAsync(ct),
            MaVay = kv.MaVay,
            MaLichTraNo = null,
            MaNv = actor,
            SoTienThanhToan = totalPay,
            SoTienGocTra = principalPay,
            SoTienLaiTra = interestPay,
            SoTienPhatTra = 0m,
            NgayThanhToan = now,
            HinhThuc = paymentMethod,
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? "Tat toan khoan vay" : ghiChu.Trim()
        });

        RefreshLoanOverdueStatus(kv, DateOnly.FromDateTime(now));
        await _db.SaveChangesAsync(ct);
        await _creditScoreService.RecalculateAsync(kv.MaKh, "Tat toan", ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(AppPermissions.CollectDebts)]
    public async Task<IActionResult> RecordCollectionAction(string id, DebtCollectionCreateViewModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        if (!await TableExistsAsync("XuLyThuHoiNo", ct))
        {
            TempData["DebtError"] = "Chưa có bảng XuLyThuHoiNo. Hãy chạy DebtCollectionActionMigration.sql trước khi ghi nhận xử lý thu hồi nợ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var loan = await _db.KhoanVays
            .Include(x => x.TaiSanTheChaps)
            .FirstOrDefaultAsync(x => x.MaVay == id, ct);
        if (loan == null) return NotFound();

        ModelState.Remove(nameof(DebtCollectionCreateViewModel.SoTienHenTra));
        if (!ModelState.IsValid)
        {
            TempData["DebtError"] = "Thông tin xử lý thu hồi nợ không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var contactMethod = (model.HinhThucLienHe ?? string.Empty).Trim();
        var result = (model.KetQua ?? string.Empty).Trim();
        var proposal = string.IsNullOrWhiteSpace(model.DeXuatXuLy) ? null : model.DeXuatXuLy.Trim();
        if (!IsValidCollectionContactMethod(contactMethod) || !IsValidCollectionResult(result) || !IsValidCollectionProposal(proposal))
        {
            TempData["DebtError"] = "Hình thức, kết quả hoặc đề xuất xử lý không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (model.NgayHenTra.HasValue && model.NgayHenTra.Value < DateOnly.FromDateTime(DateTime.Today))
        {
            TempData["DebtError"] = "Ngày hẹn trả không được nhỏ hơn ngày hiện tại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        decimal? promisedAmount = null;
        var promisedAmountRaw = Request.Form["SoTienHenTra"].ToString();
        if (!string.IsNullOrWhiteSpace(promisedAmountRaw))
        {
            if (!TryParseFlexibleDecimal(promisedAmountRaw, out var parsedPromisedAmount) || parsedPromisedAmount < 0)
            {
                TempData["DebtError"] = "Số tiền hẹn trả không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            promisedAmount = parsedPromisedAmount;
        }

        var actor = await GetDefaultEmployeeIdAsync(ct);
        if (string.IsNullOrWhiteSpace(actor))
        {
            TempData["DebtError"] = "Chưa có nhân viên để ghi nhận xử lý thu hồi nợ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.XuLyThuHoiNos.Add(new XuLyThuHoiNo
        {
            MaXuLy = await GetNextCollectionActionCodeAsync(ct),
            MaVay = loan.MaVay,
            MaNv = actor,
            NgayXuLy = DateTime.Now,
            HinhThucLienHe = contactMethod,
            KetQua = result,
            NgayHenTra = model.NgayHenTra,
            SoTienHenTra = promisedAmount,
            DeXuatXuLy = proposal,
            GhiChu = string.IsNullOrWhiteSpace(model.GhiChu) ? null : model.GhiChu.Trim()
        });

        if (proposal == "Xử lý tài sản bảo đảm")
        {
            foreach (var collateral in loan.TaiSanTheChaps.Where(x => x.TrangThai == "Đang thế chấp"))
            {
                collateral.TrangThai = "Xử lý";
            }
        }

        await _db.SaveChangesAsync(ct);
        TempData["DebtSuccess"] = "Đã ghi nhận xử lý thu hồi nợ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize(AppPermissions.CollectDebts)]
    public async Task<IActionResult> RestructureLoan(string id, DebtRestructureCreateViewModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        if (!await TableExistsAsync("CoCauNo", ct))
        {
            TempData["DebtError"] = "Chưa có bảng CoCauNo. Hãy chạy DebtRestructureMigration.sql trước khi cơ cấu nợ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var loan = await _db.KhoanVays
            .Include(x => x.LichTraNos)
            .ThenInclude(x => x.ThanhToans)
            .FirstOrDefaultAsync(x => x.MaVay == id, ct);
        if (loan == null) return NotFound();

        ModelState.Remove(nameof(DebtRestructureCreateViewModel.LaiSuatMoi));
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            TempData["DebtError"] = errors.Count == 0
                ? "Thông tin cơ cấu nợ không hợp lệ. Vui lòng nhập kỳ hạn mới và lý do cơ cấu."
                : string.Join(" ", errors);
            return RedirectToAction(nameof(Details), new { id });
        }

        if (loan.DuNoGoc <= 0 || loan.TrangThai == "Đã trả hết" || loan.TrangThai == "Xóa nợ")
        {
            TempData["DebtError"] = "Khoản vay đã tất toán hoặc không còn dư nợ để cơ cấu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var paidThroughPeriod = 0;
        foreach (var s in loan.LichTraNos.OrderBy(x => x.KyThu))
        {
            if (s.KyThu != paidThroughPeriod + 1) break;
            var due = s.SoTienGoc + s.SoTienLai;
            if (due <= 0m || s.SoTienDaThanhToan >= due)
            {
                paidThroughPeriod = s.KyThu;
                continue;
            }

            break;
        }

        if (model.KyHanMoi <= paidThroughPeriod)
        {
            TempData["DebtError"] = $"Kỳ hạn mới phải lớn hơn kỳ đã trả gần nhất ({paidThroughPeriod}).";
            return RedirectToAction(nameof(Details), new { id });
        }

        var unpaidWithPayment = loan.LichTraNos.Any(x => x.TrangThai != "Đã trả" && x.ThanhToans.Any());
        if (unpaidWithPayment)
        {
            TempData["DebtError"] = "Không thể cơ cấu khi đang có kỳ trả nợ thanh toán một phần. Vui lòng xử lý đủ kỳ đó trước.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var newRate = loan.LaiSuat;
        var rateRaw = Request.Form["LaiSuatMoi"].ToString();
        if (!string.IsNullOrWhiteSpace(rateRaw))
        {
            if (!TryParseFlexibleDouble(rateRaw, out newRate) || newRate <= 0)
            {
                TempData["DebtError"] = "Lãi suất mới không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        var actor = await GetDefaultEmployeeIdAsync(ct);
        if (string.IsNullOrWhiteSpace(actor))
        {
            TempData["DebtError"] = "Chưa có nhân viên để ghi nhận cơ cấu nợ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var oldTerm = loan.KyHan;
        var oldRate = loan.LaiSuat;
        if (model.KyHanMoi <= oldTerm && Math.Abs(newRate - oldRate) < 0.0001)
        {
            TempData["DebtError"] = "Cơ cấu nợ phải thay đổi kỳ hạn hoặc lãi suất. Thông thường hãy tăng kỳ hạn mới lớn hơn kỳ hạn hiện tại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var oldMaturity = loan.NgayDaoHan;
        var newMaturity = loan.NgayGiaiNgan.AddMonths(model.KyHanMoi);
        var remainingPeriods = model.KyHanMoi - paidThroughPeriod;

        var oldUnpaidSchedules = loan.LichTraNos
            .Where(x =>
            {
                var due = x.SoTienGoc + x.SoTienLai;
                return due <= 0m || x.SoTienDaThanhToan < due;
            })
            .OrderBy(x => x.KyThu)
            .ToList();

        var principalFromSchedules = oldUnpaidSchedules
            .Where(x => x.SoTienGoc + x.SoTienLai > 0m)
            .Select(x =>
            {
                var principalPaidSoFar = Math.Max(0m, x.SoTienDaThanhToan - x.SoTienLai);
                return Math.Max(0m, x.SoTienGoc - principalPaidSoFar);
            })
            .DefaultIfEmpty(0m)
            .Sum();

        var principalToRestructure = principalFromSchedules > 0m ? principalFromSchedules : loan.DuNoGoc;
        if (principalToRestructure <= 0m)
        {
            TempData["DebtError"] = "Không xác định được dư nợ gốc còn lại để cơ cấu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.LichTraNos.RemoveRange(oldUnpaidSchedules);

        var nextScheduleNo = await GetNextScheduleCodeSuffixAsync(ct);
        foreach (var row in BuildRestructuredSchedule(principalToRestructure, newRate, remainingPeriods, loan.NgayGiaiNgan, paidThroughPeriod))
        {
            _db.LichTraNos.Add(new LichTraNo
            {
                MaLichTraNo = $"LTN{nextScheduleNo:0000}",
                MaVay = loan.MaVay,
                KyThu = row.kyThu,
                NgayPhaiTra = row.ngayPhaiTra,
                SoTienGoc = row.goc,
                SoTienLai = row.lai,
                SoTienDaThanhToan = 0m,
                TrangThai = "Chưa trả",
                NgayThanhToanThucTe = null,
                GhiChu = "Tạo lại sau cơ cấu nợ"
            });
            nextScheduleNo++;
        }

        loan.KyHan = model.KyHanMoi;
        loan.LaiSuat = newRate;
        loan.NgayDaoHan = newMaturity;
        loan.TrangThai = "Cơ cấu lại";
        loan.GhiChu = string.IsNullOrWhiteSpace(model.GhiChu) ? loan.GhiChu : model.GhiChu.Trim();

        _db.CoCauNos.Add(new CoCauNo
        {
            MaCoCau = await GetNextRestructureCodeAsync(ct),
            MaVay = loan.MaVay,
            MaNv = actor,
            NgayCoCau = DateTime.Now,
            KyHanCu = oldTerm,
            KyHanMoi = model.KyHanMoi,
            LaiSuatCu = oldRate,
            LaiSuatMoi = newRate,
            NgayDaoHanCu = oldMaturity,
            NgayDaoHanMoi = newMaturity,
            DuNoGocCoCau = loan.DuNoGoc,
            LyDo = model.LyDo.Trim(),
            GhiChu = string.IsNullOrWhiteSpace(model.GhiChu) ? null : model.GhiChu.Trim()
        });

        await _db.SaveChangesAsync(ct);
        await _creditScoreService.RecalculateAsync(loan.MaKh, "Cơ cấu nợ", ct);
        TempData["DebtSuccess"] = "Đã cơ cấu lại khoản vay và tạo lại lịch trả nợ.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task ReduceUsedCreditLimitAsync(string maKh, decimal principalPaid, DateOnly updateDate, CancellationToken ct)
    {
        if (principalPaid <= 0) return;

        var creditLimit = await _db.HanMucTinDungs.FirstOrDefaultAsync(x => x.MaKh == maKh, ct);
        if (creditLimit == null) return;

        creditLimit.HanMucDaSuDung = Math.Max(0m, creditLimit.HanMucDaSuDung - principalPaid);
        creditLimit.NgayCapNhat = updateDate;
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken ct)
    {
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT CASE WHEN OBJECT_ID(N'dbo.{safeTableName}', N'U') IS NULL THEN 0 ELSE 1 END";
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task RefreshOverdueStatusAsync(CancellationToken ct)
    {
        var loans = await _db.KhoanVays
            .Include(x => x.LichTraNos)
            .Where(x => x.TrangThai != "Đã trả hết" && x.TrangThai != "Xóa nợ")
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var loan in loans)
        {
            RefreshLoanOverdueStatus(loan, today);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RefreshOverdueStatusAsync(string maVay, CancellationToken ct)
    {
        var loan = await _db.KhoanVays
            .Include(x => x.LichTraNos)
            .FirstOrDefaultAsync(x => x.MaVay == maVay, ct);

        if (loan == null) return;

        RefreshLoanOverdueStatus(loan, DateOnly.FromDateTime(DateTime.Now));
        await _db.SaveChangesAsync(ct);
    }

    private static void RefreshLoanOverdueStatus(KhoanVay loan, DateOnly today)
    {
        var maxDaysOverdue = 0;

        foreach (var schedule in loan.LichTraNos)
        {
            var due = schedule.SoTienGoc + schedule.SoTienLai;
            if (schedule.SoTienDaThanhToan >= due)
            {
                schedule.TrangThai = "Đã trả";
                continue;
            }

            var daysOverdue = CalculateDaysOverdue(schedule, today);
            if (daysOverdue > 0)
            {
                schedule.TrangThai = "Trễ hạn";
                maxDaysOverdue = Math.Max(maxDaysOverdue, daysOverdue);
            }
            else if (schedule.SoTienDaThanhToan > 0)
            {
                schedule.TrangThai = "Trả một phần";
            }
            else
            {
                schedule.TrangThai = "Chưa trả";
            }
        }

        var newGroup = MapDebtGroup(maxDaysOverdue);
        if (loan.NhomNo != newGroup)
        {
            loan.NhomNo = newGroup;
            loan.NgayCapNhatNhom = today;
        }

        if (loan.DuNoGoc <= 0 && loan.LichTraNos.All(x => x.TrangThai == "Đã trả"))
        {
            loan.TrangThai = "Đã trả hết";
            loan.NhomNo = 1;
            loan.NgayCapNhatNhom = today;
        }
        else if (maxDaysOverdue > 0)
        {
            loan.TrangThai = "Quá hạn";
        }
        else if (loan.TrangThai == "Quá hạn")
        {
            loan.TrangThai = "Đang vay";
        }
    }

    private static int CalculateDaysOverdue(LichTraNo schedule, DateOnly today)
    {
        var due = schedule.SoTienGoc + schedule.SoTienLai;
        if (schedule.SoTienDaThanhToan >= due) return 0;
        return schedule.NgayPhaiTra < today ? today.DayNumber - schedule.NgayPhaiTra.DayNumber : 0;
    }

    private static byte MapDebtGroup(int maxDaysOverdue)
    {
        return maxDaysOverdue switch
        {
            <= 9 => 1,
            <= 90 => 2,
            <= 180 => 3,
            <= 360 => 4,
            _ => 5
        };
    }

    private async Task<string?> GetDefaultEmployeeIdAsync(CancellationToken ct)
    {
        var maNv = User.FindFirst("MaNV")?.Value;
        if (!string.IsNullOrWhiteSpace(maNv))
        {
            return maNv;
        }

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

    private async Task<string> GetNextCollectionActionCodeAsync(CancellationToken ct)
    {
        var codes = await _db.XuLyThuHoiNos.AsNoTracking().Select(x => x.MaXuLy).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "XLN")).DefaultIfEmpty(0).Max();
        return $"XLN{(maxId + 1):0000}";
    }

    private async Task<string> GetNextRestructureCodeAsync(CancellationToken ct)
    {
        var codes = await _db.CoCauNos.AsNoTracking().Select(x => x.MaCoCau).ToListAsync(ct);
        var maxId = codes.Select(x => ParseCodeSuffix(x, "CCN")).DefaultIfEmpty(0).Max();
        return $"CCN{(maxId + 1):0000}";
    }

    private async Task<int> GetNextScheduleCodeSuffixAsync(CancellationToken ct)
    {
        var codes = await _db.LichTraNos.AsNoTracking().Select(x => x.MaLichTraNo).ToListAsync(ct);
        return codes.Select(x => ParseCodeSuffix(x, "LTN")).DefaultIfEmpty(0).Max() + 1;
    }

    private static IEnumerable<(int kyThu, DateOnly ngayPhaiTra, decimal goc, decimal lai)> BuildRestructuredSchedule(
        decimal principal,
        double annualRatePercent,
        int remainingPeriods,
        DateOnly disburseDate,
        int paidPeriodCount)
    {
        if (principal <= 0 || remainingPeriods <= 0) yield break;

        var r = (decimal)(annualRatePercent / 100d / 12d);
        decimal payment;
        if (r <= 0)
        {
            payment = principal / remainingPeriods;
        }
        else
        {
            var pow = (decimal)Math.Pow((double)(1m + r), -remainingPeriods);
            payment = principal * r / (1m - pow);
        }

        var remaining = DecimalRoundMoney(principal);
        for (var i = 1; i <= remainingPeriods; i++)
        {
            var interestRaw = r <= 0 ? 0m : remaining * r;
            var interest = DecimalRoundMoney(interestRaw);

            decimal principalPay;
            if (i == remainingPeriods)
            {
                principalPay = remaining;
            }
            else
            {
                var principalRaw = payment - interestRaw;
                principalPay = DecimalRoundMoney(principalRaw);
                if (principalPay < 0m) principalPay = 0m;
                if (principalPay > remaining) principalPay = remaining;
            }

            remaining -= principalPay;

            var kyThu = paidPeriodCount + i;
            yield return (
                kyThu,
                disburseDate.AddMonths(kyThu),
                principalPay,
                interest
            );
        }
    }

    private static decimal DecimalRoundMoney(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

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

    private static bool TryParseFlexibleDouble(string? input, out double value)
    {
        value = 0d;
        if (!TryParseFlexibleDecimal(input, out var decimalValue)) return false;
        value = (double)decimalValue;
        return true;
    }

    private static bool IsValidPaymentMethod(string method)
    {
        var m = (method ?? string.Empty).Trim();
        return m is "Tiền mặt" or "Chuyển khoản" or "Thu nợ tự động";
    }

    private static bool IsValidCollectionContactMethod(string method)
    {
        return method is "Gọi điện" or "SMS" or "Email" or "Gặp trực tiếp" or "Thông báo văn bản";
    }

    private static bool IsValidCollectionResult(string result)
    {
        return result is "Đã liên hệ" or "Không liên hệ được" or "Khách hẹn trả" or "Từ chối trả" or "Đã gửi thông báo";
    }

    private static bool IsValidCollectionProposal(string? proposal)
    {
        return string.IsNullOrWhiteSpace(proposal)
            || proposal is "Tiếp tục theo dõi" or "Cơ cấu lại nợ" or "Xử lý tài sản bảo đảm" or "Chuyển pháp lý";
    }
}
