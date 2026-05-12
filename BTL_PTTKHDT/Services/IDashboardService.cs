using BTL_PTTKHDT.Models;

namespace BTL_PTTKHDT.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardAsync(int? year, CancellationToken cancellationToken);
}
