namespace BTL_PTTKHDT.Data;

public sealed class LoanApplication
{
    public int Id { get; set; }

    public required string Code { get; set; }
    public required string CustomerName { get; set; }

    public decimal Amount { get; set; }

    public required string Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? DisbursedAt { get; set; }

    public bool IsNonPerforming { get; set; }
}
