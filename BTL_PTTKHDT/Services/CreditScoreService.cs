using BTL_PTTKHDT.Models;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Services;

public sealed class CreditScoreService : ICreditScoreService
{
    private readonly QltdnhContext _db;

    public CreditScoreService(QltdnhContext db)
    {
        _db = db;
    }

    public async Task RecalculateAsync(string maKh, string source, CancellationToken cancellationToken = default, decimal? monthlyIncomeOverride = null)
    {
        if (string.IsNullOrWhiteSpace(maKh)) return;

        var customer = await _db.KhachHangs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaKh == maKh, cancellationToken);
        if (customer == null) return;

        var isBusiness = IsBusiness(customer.LoaiKhachHang);

        var previous = await _db.LichSuTinDungs
            .AsNoTracking()
            .Where(x => x.MaKh == maKh)
            .OrderByDescending(x => x.NgayCapNhat)
            .FirstOrDefaultAsync(cancellationToken);

        var loans = await _db.KhoanVays
            .AsNoTracking()
            .Include(x => x.LichTraNos)
            .Where(x => x.MaKh == maKh)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var hasLoanHistory = loans.Count > 0;
        if (!hasLoanHistory)
        {
            return;
        }

        var score = 1000;

        var maxDebtGroup = loans.Select(x => (int)x.NhomNo).DefaultIfEmpty(1).Max();
        score -= maxDebtGroup switch
        {
            1 => 0,
            2 => 80,
            3 => 200,
            4 => 350,
            _ => 500
        };

        var schedules = loans.SelectMany(x => x.LichTraNos).ToList();
        var lateSchedules = schedules
            .Where(x => IsLate(x, today))
            .ToList();

        var lateCount = lateSchedules.Count;
        score -= Math.Min(180, lateCount * 30);

        var maxLateDays = lateSchedules
            .Select(x => CalculateLateDays(x, today))
            .DefaultIfEmpty(0)
            .Max();
        score -= Math.Min(200, (maxLateDays / 10) * 20);

        var activeOutstanding = loans
            .Where(x => (x.TrangThai == "Đang vay" || x.TrangThai == "Quá hạn" || x.TrangThai == "Cơ cấu lại") && x.DuNoGoc > 0)
            .Sum(x => x.DuNoGoc);

        var monthlyIncome = monthlyIncomeOverride
            ?? (isBusiness ? customer.DoanhThuBinhQuanThang : customer.ThuNhapHangThang)
            ?? previous?.ThuNhapHangThang;
        double? debtToIncome = null;
        if (monthlyIncome.HasValue && monthlyIncome.Value > 0)
        {
            debtToIncome = (double)(activeOutstanding / monthlyIncome.Value);
            score -= debtToIncome switch
            {
                <= 0.30 => 0,
                <= 0.50 => 80,
                <= 0.70 => 150,
                _ => 250
            };
        }

        if (activeOutstanding > 0)
        {
            score -= activeOutstanding switch
            {
                >= 1_000_000_000m => 120,
                >= 500_000_000m => 80,
                >= 100_000_000m => 40,
                _ => 0
            };
        }

        if (isBusiness)
        {
            if (!string.IsNullOrWhiteSpace(customer.MaSoThue)) score += 20;
            if (!string.IsNullOrWhiteSpace(customer.TenNguoiDaiDien)) score += 20;

            if (customer.NgayThanhLap.HasValue)
            {
                var yearsActive = Math.Max(0, today.Year - customer.NgayThanhLap.Value.Year);
                score += yearsActive switch
                {
                    >= 5 => 60,
                    >= 3 => 40,
                    >= 1 => 20,
                    _ => -40
                };
            }
            else
            {
                score -= 40;
            }

            if (customer.LoiNhuanBinhQuanThang.HasValue)
            {
                score += customer.LoiNhuanBinhQuanThang.Value switch
                {
                    > 0 => 50,
                    < 0 => -120,
                    _ => -40
                };
            }

            if (customer.DoanhThuBinhQuanThang.HasValue && customer.LoiNhuanBinhQuanThang.HasValue && customer.DoanhThuBinhQuanThang.Value > 0)
            {
                var profitMargin = customer.LoiNhuanBinhQuanThang.Value / customer.DoanhThuBinhQuanThang.Value;
                score += profitMargin switch
                {
                    >= 0.20m => 40,
                    >= 0.10m => 25,
                    >= 0.03m => 10,
                    < 0 => -80,
                    _ => -20
                };
            }
        }

        var hasOnTimeSettledLoan = loans.Any(x =>
            x.TrangThai == "Đã trả hết"
            && x.LichTraNos.Count > 0
            && x.LichTraNos.All(s => s.TrangThai == "Đã trả"
                && s.NgayThanhToanThucTe.HasValue
                && s.NgayThanhToanThucTe.Value <= s.NgayPhaiTra));
        if (hasOnTimeSettledLoan)
        {
            score += 50;
        }

        if (hasLoanHistory && lateCount == 0)
        {
            score += 50;
        }

        score = Math.Clamp(score, 0, 1000);

        _db.LichSuTinDungs.Add(new LichSuTinDung
        {
            MaLichSu = await GetNextHistoryCodeAsync(cancellationToken),
            MaKh = maKh,
            DiemTinDung = score,
            XepHangRuiRo = MapRiskRank(score),
            SoLanTraTre = lateCount,
            ThuNhapHangThang = monthlyIncome,
            TyLeNoThuNhap = debtToIncome,
            NguonCapNhat = source,
            NgayCapNhat = DateTime.Now,
            GhiChu = isBusiness
                ? $"Doanh nghiệp; Nhom no cao nhat: {maxDebtGroup}; Tre lon nhat: {maxLateDays} ngay"
                : $"Cá nhân; Nhom no cao nhat: {maxDebtGroup}; Tre lon nhat: {maxLateDays} ngay"
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetNextHistoryCodeAsync(CancellationToken cancellationToken)
    {
        var codes = await _db.LichSuTinDungs
            .AsNoTracking()
            .Select(x => x.MaLichSu)
            .ToListAsync(cancellationToken);

        var max = codes
            .Select(x => ParseCodeSuffix(x, "LS"))
            .DefaultIfEmpty(0)
            .Max();

        return $"LS{max + 1:000}";
    }

    private static int ParseCodeSuffix(string code, string prefix)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(code[prefix.Length..], out var value) ? value : 0;
    }

    private static bool IsLate(LichTraNo schedule, DateOnly today)
    {
        if (schedule.NgayThanhToanThucTe.HasValue && schedule.NgayThanhToanThucTe.Value > schedule.NgayPhaiTra)
        {
            return true;
        }

        return schedule.TrangThai == "Trễ hạn" || (schedule.TrangThai != "Đã trả" && schedule.NgayPhaiTra < today);
    }

    private static int CalculateLateDays(LichTraNo schedule, DateOnly today)
    {
        if (schedule.NgayThanhToanThucTe.HasValue)
        {
            return Math.Max(0, schedule.NgayThanhToanThucTe.Value.DayNumber - schedule.NgayPhaiTra.DayNumber);
        }

        return schedule.NgayPhaiTra < today ? today.DayNumber - schedule.NgayPhaiTra.DayNumber : 0;
    }

    private static string MapRiskRank(int score)
    {
        return score switch
        {
            >= 900 => "AAA",
            >= 800 => "AA",
            >= 700 => "A",
            >= 600 => "BBB",
            >= 500 => "BB",
            >= 400 => "B",
            >= 300 => "CCC",
            >= 200 => "CC",
            >= 100 => "C",
            _ => "D"
        };
    }

    private static bool IsBusiness(string? type)
    {
        return (type ?? string.Empty).Trim().ToLowerInvariant().Contains("doanh");
    }
}
