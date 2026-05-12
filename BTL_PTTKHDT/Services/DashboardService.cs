using BTL_PTTKHDT.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BTL_PTTKHDT.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly QltdnhContext _db;

    private static readonly string[] ActiveLoanStatuses =
    {
        "Dang vay",
        "Qua han",
        "Co cau lai"
    };

    public DashboardService(QltdnhContext db)
    {
        _db = db;
    }

    public async Task<DashboardViewModel> GetDashboardAsync(int? year, CancellationToken cancellationToken)
    {
        var yearsFromDonVay = await _db.DonVays
            .AsNoTracking()
            .Select(x => x.NgayTao.Year)
            .Distinct()
            .ToListAsync(cancellationToken);

        var yearsFromKhoanVay = await _db.KhoanVays
            .AsNoTracking()
            .Select(x => x.NgayGiaiNgan.Year)
            .Distinct()
            .ToListAsync(cancellationToken);

        var yearsFromKhachHang = await _db.KhachHangs
            .AsNoTracking()
            .Select(x => x.NgayTao.Year)
            .Distinct()
            .ToListAsync(cancellationToken);

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

        // Chart: giải ngân theo tháng (T1..T12)
        var monthLabels = Enumerable.Range(1, 12).Select(m => $"T{m}").ToArray();
        var monthlyValues = new decimal[12];

        var disbursed = await _db.KhoanVays
            .AsNoTracking()
            .Where(x => x.NgayGiaiNgan.Year == selectedYear)
            .Select(x => new { x.SoTienVay, Month = x.NgayGiaiNgan.Month })
            .ToListAsync(cancellationToken);

        foreach (var item in disbursed)
        {
            if (item.Month is >= 1 and <= 12)
            {
                monthlyValues[item.Month - 1] += item.SoTienVay;
            }
        }

        // Latest loan applications
        var latestLoans = await _db.DonVays
            .AsNoTracking()
            .OrderByDescending(x => x.NgayTao)
            .Take(4)
            .Select(x => new LatestLoanItem(
                x.MaKhNavigation.HoTen,
                FormatDonVayCode(x.MaDon, x.NgayTao),
                FormatVnd(x.SoTienYeuCau),
                NormalizeDonVayStatus(x.TrangThaiDon),
                MapDonVayStatusKind(x.TrangThaiDon)))
            .ToListAsync(cancellationToken);

        // KPIs
        var now = DateTime.Now;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var nextMonthStart = thisMonthStart.AddMonths(1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var totalOutstanding = await _db.KhoanVays
            .AsNoTracking()
            .Where(x => ActiveLoanStatuses.Contains(x.TrangThai) && x.DuNoGoc > 0)
            .SumAsync(x => (decimal?)x.DuNoGoc, cancellationToken) ?? 0m;

        var pendingThis = await _db.DonVays
            .AsNoTracking()
            .CountAsync(x => x.TrangThaiDon == "Cho duyet" && x.NgayTao >= thisMonthStart && x.NgayTao < nextMonthStart, cancellationToken);

        var pendingLast = await _db.DonVays
            .AsNoTracking()
            .CountAsync(x => x.TrangThaiDon == "Cho duyet" && x.NgayTao >= lastMonthStart && x.NgayTao < thisMonthStart, cancellationToken);

        var activeLoanCount = await _db.KhoanVays
            .AsNoTracking()
            .CountAsync(x => ActiveLoanStatuses.Contains(x.TrangThai) && x.DuNoGoc > 0, cancellationToken);

        var nplCount = await _db.KhoanVays
            .AsNoTracking()
            .CountAsync(x => ActiveLoanStatuses.Contains(x.TrangThai) && x.DuNoGoc > 0 && x.NhomNo >= 3, cancellationToken);

        var nplRate = activeLoanCount == 0 ? 0m : (decimal)nplCount / activeLoanCount;

        var newCustomersThis = await _db.KhachHangs
            .AsNoTracking()
            .CountAsync(x => x.NgayTao >= thisMonthStart && x.NgayTao < nextMonthStart, cancellationToken);

        var newCustomersLast = await _db.KhachHangs
            .AsNoTracking()
            .CountAsync(x => x.NgayTao >= lastMonthStart && x.NgayTao < thisMonthStart, cancellationToken);

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
                ValueText: $"{pendingThis:N0} Đơn",
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

        return new DashboardViewModel
        {
            SelectedYear = selectedYear,
            AvailableYears = availableYears,
            Kpis = kpis,
            DisbursementMonthLabels = monthLabels,
            DisbursementValues = monthlyValues,
            LatestLoans = latestLoans
        };
    }

    private static string NormalizeDonVayStatus(string status)
    {
        // Per schema: Dang soan → Cho duyet → Da duyet / Tu choi / Da huy
        return status switch
        {
            "Dang soan" => "Đang soạn",
            "Cho duyet" => "Chờ duyệt",
            "Da duyet" => "Đã duyệt",
            "Tu choi" => "Từ chối",
            "Da huy" => "Đã hủy",
            _ => status
        };
    }

    private static string MapDonVayStatusKind(string rawStatus)
    {
        return rawStatus switch
        {
            "Cho duyet" => "warning",
            "Dang soan" => "secondary",
            "Da duyet" => "success",
            "Tu choi" => "danger",
            "Da huy" => "secondary",
            _ => "secondary"
        };
    }

    private static string FormatDonVayCode(string maDon, DateTime createdAt)
    {
        return string.IsNullOrWhiteSpace(maDon)
            ? $"DV-{createdAt:yyyyMMddHHmmss}"
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
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###} Tỷ VND", amount / billion);
        }

        if (amount >= million)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###} Triệu VND", amount / million);
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
}
