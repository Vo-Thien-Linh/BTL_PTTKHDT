namespace BTL_PTTKHDT.Models;

public sealed class DashboardViewModel
{
    public required IReadOnlyList<KpiCard> Kpis { get; init; }
    public required int SelectedYear { get; init; }
    public required IReadOnlyList<int> AvailableYears { get; init; }

    public required IReadOnlyList<string> DisbursementMonthLabels { get; init; }
    public required IReadOnlyList<decimal> DisbursementValues { get; init; }

    public required IReadOnlyList<LatestLoanItem> LatestLoans { get; init; }
}

public sealed record KpiCard(
    string Title,
    string ValueText,
    string DeltaText,
    bool IsDeltaPositive,
    string Icon,
    string Theme);

public sealed record LatestLoanItem(
    string CustomerName,
    string Code,
    string AmountText,
    string StatusText,
    string StatusKind);
