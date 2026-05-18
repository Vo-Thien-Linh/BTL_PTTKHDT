namespace BTL_PTTKHDT.Services;

public interface ICreditScoreService
{
    Task RecalculateAsync(string maKh, string source, CancellationToken cancellationToken = default, decimal? monthlyIncomeOverride = null);
}
