namespace BTL_PTTKHDT.Models;

public sealed class DashboardViewModel
{
    public required IReadOnlyList<KpiCard> Kpis { get; init; }
    public required int SelectedYear { get; init; }
    public required IReadOnlyList<int> AvailableYears { get; init; }

    public required IReadOnlyList<string> DisbursementMonthLabels { get; init; }
    public required IReadOnlyList<decimal> DisbursementValues { get; init; }

    public required IReadOnlyList<LatestLoanItem> LatestLoans { get; init; }
    public required IReadOnlyList<LoanStatusSummaryItem> LoanStatusSummary { get; init; }
    public required IReadOnlyList<OverdueScheduleItem> OverdueSchedules { get; init; }
    public required IReadOnlyList<RecentPaymentItem> RecentPayments { get; init; }
    public required DashboardTotals Totals { get; init; }
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

public sealed record LoanStatusSummaryItem(
    string StatusText,
    int Count,
    string OutstandingText,
    string StatusKind);

public sealed record OverdueScheduleItem(
    string CustomerName,
    string LoanCode,
    string DueDateText,
    string AmountText,
    int DaysOverdue);

public sealed record RecentPaymentItem(
    string CustomerName,
    string LoanCode,
    string PaidDateText,
    string AmountText,
    string Method);

public sealed record DashboardTotals(
    string DisbursedThisYearText,
    string CollectedThisMonthText,
    int OverdueScheduleCount,
    int ActiveCustomerCount);
