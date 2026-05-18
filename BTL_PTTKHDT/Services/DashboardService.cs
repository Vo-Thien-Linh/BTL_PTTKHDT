using BTL_PTTKHDT.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BTL_PTTKHDT.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly QltdnhContext _db;

    public DashboardService(QltdnhContext db)
    {
        _db = db;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(int? year, CancellationToken cancellationToken)
    {
        var donVayDates = await _db.DonVays
            .AsNoTracking()
            .Select(x => x.NgayNopDon)
            .ToListAsync(cancellationToken);

        var khoanVayDates = await _db.KhoanVays
            .AsNoTracking()
            .Select(x => x.NgayGiaiNgan)
            .ToListAsync(cancellationToken);

        var khachHangCreatedDates = await _db.KhachHangs
            .AsNoTracking()
            .Select(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        var yearsFromDonVay = donVayDates.Select(x => x.Year);
        var yearsFromKhoanVay = khoanVayDates.Select(x => x.Year);
        var yearsFromKhachHang = khachHangCreatedDates.Select(x => x.Year);

        var availableYears = yearsFromDonVay
            .Concat(yearsFromKhoanVay)
            .Concat(yearsFromKhachHang)
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        var selectedYear = year ?? availableYears.FirstOrDefault();
        if (selectedYear <= 0)
        {
            selectedYear = DateTime.Now.Year;
            availableYears = new List<int> { selectedYear };
        }

        var monthLabels = Enumerable.Range(1, 12).Select(m => $"T{m}").ToArray();
        var monthlyValues = new decimal[12];
        var selectedYearStart = new DateOnly(selectedYear, 1, 1);
        var nextYearStart = selectedYearStart.AddYears(1);

        var disbursed = await _db.KhoanVays
            .AsNoTracking()
            .Where(x => x.NgayGiaiNgan >= selectedYearStart && x.NgayGiaiNgan < nextYearStart)
            .Select(x => new { x.SoTienVay, x.NgayGiaiNgan })
            .ToListAsync(cancellationToken);

        foreach (var item in disbursed)
        {
            var month = item.NgayGiaiNgan.Month;
            if (month is >= 1 and <= 12)
            {
                monthlyValues[month - 1] += item.SoTienVay;
            }
        }

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var nextMonthStart = thisMonthStart.AddMonths(1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var thisMonthStartDate = DateOnly.FromDateTime(thisMonthStart);
        var nextMonthStartDate = DateOnly.FromDateTime(nextMonthStart);
        var lastMonthStartDate = DateOnly.FromDateTime(lastMonthStart);

        var latestLoans = await _db.DonVays
            .AsNoTracking()
            .OrderByDescending(x => x.NgayNopDon)
            .Take(4)
            .Select(x => new LatestLoanItem(
                x.MaKhNavigation.HoTen,
                FormatDonVayCode(x.MaDon, x.NgayNopDon),
                FormatVnd(x.SoTienYeuCau),
                NormalizeDonVayStatus(x.TrangThaiDon),
                MapDonVayStatusKind(x.TrangThaiDon)))
            .ToListAsync(cancellationToken);

        var totalOutstanding = await _db.KhoanVays
            .AsNoTracking()
            .Where(x =>
                (x.TrangThai == "Đang vay" || x.TrangThai == "Quá hạn" || x.TrangThai == "Cơ cấu lại")
                && x.DuNoGoc > 0)
            .SumAsync(x => (decimal?)x.DuNoGoc, cancellationToken) ?? 0m;

        var pendingThis = await _db.DonVays
            .AsNoTracking()
            .CountAsync(x => x.TrangThaiDon == "Chờ duyệt" && x.NgayNopDon >= thisMonthStartDate && x.NgayNopDon < nextMonthStartDate, cancellationToken);

        var pendingLast = await _db.DonVays
            .AsNoTracking()
            .CountAsync(x => x.TrangThaiDon == "Chờ duyệt" && x.NgayNopDon >= lastMonthStartDate && x.NgayNopDon < thisMonthStartDate, cancellationToken);

        var activeLoanCount = await _db.KhoanVays
            .AsNoTracking()
            .CountAsync(x =>
                (x.TrangThai == "Đang vay" || x.TrangThai == "Quá hạn" || x.TrangThai == "Cơ cấu lại")
                && x.DuNoGoc > 0,
                cancellationToken);

        var nplCount = await _db.KhoanVays
            .AsNoTracking()
            .CountAsync(x =>
                (x.TrangThai == "Đang vay" || x.TrangThai == "Quá hạn" || x.TrangThai == "Cơ cấu lại")
                && x.DuNoGoc > 0
                && x.NhomNo >= 3,
                cancellationToken);

        var nplRate = activeLoanCount == 0 ? 0m : (decimal)nplCount / activeLoanCount;

        var newCustomersThis = await _db.KhachHangs
            .AsNoTracking()
            .CountAsync(x => x.NgayTao >= thisMonthStart && x.NgayTao < nextMonthStart, cancellationToken);

        var newCustomersLast = await _db.KhachHangs
            .AsNoTracking()
            .CountAsync(x => x.NgayTao >= lastMonthStart && x.NgayTao < thisMonthStart, cancellationToken);

        var collectedThisMonth = await _db.ThanhToans
            .AsNoTracking()
            .Where(x => x.NgayThanhToan >= thisMonthStart && x.NgayThanhToan < nextMonthStart)
            .SumAsync(x => (decimal?)x.SoTienThanhToan, cancellationToken) ?? 0m;

        var overdueScheduleCount = await _db.LichTraNos
            .AsNoTracking()
            .CountAsync(x => x.NgayPhaiTra < today && x.TrangThai != "Đã trả", cancellationToken);

        var activeCustomerCount = await _db.KhachHangs
            .AsNoTracking()
            .CountAsync(x => x.IsActive == true, cancellationToken);

        var kpis = new List<KpiCard>
        {
            new(
                Title: "Tổng dư nợ",
                ValueText: FormatCompactVnd(totalOutstanding),
                DeltaText: "Dư nợ gốc còn lại",
                IsDeltaPositive: true,
                Icon: "bi-cash-stack",
                Theme: "primary"),
            new(
                Title: "Đơn vay chờ duyệt",
                ValueText: $"{pendingThis:N0} đơn",
                DeltaText: FormatDelta(pendingThis, pendingLast, "so với tháng trước"),
                IsDeltaPositive: pendingThis <= pendingLast,
                Icon: "bi-journal-check",
                Theme: "warning"),
            new(
                Title: "Tỷ lệ nợ xấu (NPL)",
                ValueText: $"{nplRate * 100m:0.0}%",
                DeltaText: nplRate < 0.03m ? "Kiểm soát tốt" : "Cần theo dõi",
                IsDeltaPositive: nplRate < 0.03m,
                Icon: "bi-exclamation-triangle",
                Theme: "danger"),
            new(
                Title: "Khách hàng mới",
                ValueText: $"{newCustomersThis:N0}",
                DeltaText: FormatDelta(newCustomersThis, newCustomersLast, "so với tháng trước"),
                IsDeltaPositive: newCustomersThis >= newCustomersLast,
                Icon: "bi-person-plus",
                Theme: "success"),
        };

        var loanStatusRaw = await _db.KhoanVays
            .AsNoTracking()
            .GroupBy(x => x.TrangThai)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Outstanding = g.Sum(x => x.DuNoGoc)
            })
            .OrderByDescending(x => x.Outstanding)
            .ToListAsync(cancellationToken);

        var loanStatusSummary = loanStatusRaw
            .Select(x => new LoanStatusSummaryItem(
                NormalizeKhoanVayStatus(x.Status),
                x.Count,
                FormatCompactVnd(x.Outstanding),
                MapKhoanVayStatusKind(x.Status)))
            .ToList();

        var overdueRaw = await _db.LichTraNos
            .AsNoTracking()
            .Where(x => x.NgayPhaiTra < today && x.TrangThai != "Đã trả")
            .OrderBy(x => x.NgayPhaiTra)
            .Take(5)
            .Select(x => new
            {
                CustomerName = x.MaVayNavigation.MaKhNavigation.HoTen,
                x.MaVay,
                x.NgayPhaiTra,
                Amount = x.TongPhaiTra ?? x.SoTienGoc + x.SoTienLai
            })
            .ToListAsync(cancellationToken);

        var overdueSchedules = overdueRaw
            .Select(x => new OverdueScheduleItem(
                x.CustomerName,
                x.MaVay,
                FormatDate(x.NgayPhaiTra),
                FormatVnd(x.Amount),
                Math.Max(0, today.DayNumber - x.NgayPhaiTra.DayNumber)))
            .ToList();

        var recentPayments = await _db.ThanhToans
            .AsNoTracking()
            .OrderByDescending(x => x.NgayThanhToan)
            .Take(5)
            .Select(x => new RecentPaymentItem(
                x.MaVayNavigation.MaKhNavigation.HoTen,
                x.MaVay,
                x.NgayThanhToan.ToString("dd/MM/yyyy"),
                FormatVnd(x.SoTienThanhToan),
                x.HinhThuc))
            .ToListAsync(cancellationToken);

        return new DashboardViewModel
        {
            SelectedYear = selectedYear,
            AvailableYears = availableYears,
            Kpis = kpis,
            DisbursementMonthLabels = monthLabels,
            DisbursementValues = monthlyValues,
            LatestLoans = latestLoans,
            LoanStatusSummary = loanStatusSummary,
            OverdueSchedules = overdueSchedules,
            RecentPayments = recentPayments,
            Totals = new DashboardTotals(
                FormatCompactVnd(monthlyValues.Sum()),
                FormatCompactVnd(collectedThisMonth),
                overdueScheduleCount,
                activeCustomerCount)
        };
    }

    private static string NormalizeDonVayStatus(string status)
    {
        return status switch
        {
            "Đang soạn" => "Đang soạn",
            "Chờ duyệt" => "Chờ duyệt",
            "Đã duyệt" => "Đã duyệt",
            "Từ chối" => "Từ chối",
            "Đã hủy" => "Đã hủy",
            _ => status
        };
    }

    private static string NormalizeKhoanVayStatus(string status)
    {
        return status switch
        {
            "Đang vay" => "Đang vay",
            "Quá hạn" => "Quá hạn",
            "Đã trả hết" => "Đã trả hết",
            "Xóa nợ" => "Xóa nợ",
            "Cơ cấu lại" => "Cơ cấu lại",
            _ => status
        };
    }

    private static string MapDonVayStatusKind(string rawStatus)
    {
        return rawStatus switch
        {
            "Chờ duyệt" => "warning",
            "Đang soạn" => "secondary",
            "Đã duyệt" => "success",
            "Từ chối" => "danger",
            "Đã hủy" => "secondary",
            _ => "secondary"
        };
    }

    private static string MapKhoanVayStatusKind(string rawStatus)
    {
        return rawStatus switch
        {
            "Đang vay" => "primary",
            "Quá hạn" => "danger",
            "Cơ cấu lại" => "warning",
            "Đã trả hết" => "success",
            "Xóa nợ" => "secondary",
            _ => "secondary"
        };
    }

    private static string FormatDonVayCode(string maDon, DateOnly submittedAt)
    {
        return string.IsNullOrWhiteSpace(maDon)
            ? $"DV-{submittedAt:yyyyMMdd}"
            : maDon;
    }

    private static string FormatVnd(decimal amount)
    {
        return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:n0} đ", amount);
    }

    private static string FormatCompactVnd(decimal amount)
    {
        const decimal billion = 1_000_000_000m;
        const decimal million = 1_000_000m;

        if (amount >= billion)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###} tỷ VND", amount / billion);
        }

        if (amount >= million)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###} triệu VND", amount / million);
        }

        return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:n0} VND", amount);
    }

    private static string FormatDelta(int current, int previous, string suffix)
    {
        if (previous == 0)
        {
            return current == 0 ? $"0% {suffix}" : $"+100% {suffix}";
        }

        var delta = (decimal)(current - previous) / previous * 100m;
        var sign = delta >= 0 ? "+" : string.Empty;
        return $"{sign}{delta:0.0}% {suffix}";
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }
}
