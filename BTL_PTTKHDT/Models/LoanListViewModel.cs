namespace BTL_PTTKHDT.Models;

public sealed class LoanListViewModel
{
    public required IReadOnlyList<LoanRowViewModel> Items { get; init; }

    public string? Query { get; init; }
    public string? Status { get; init; }
    public string? Period { get; init; }

    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);
}

public sealed class LoanRowViewModel
{
    public required string MaDon { get; init; }
    public required string MaKh { get; init; }
    public required string TenKhachHang { get; init; }
    public required string LoaiKhachHang { get; init; }   // "personal" | "business"
    public required string SoGiayTo { get; init; }        // CMND hoặc MST
    public required string NhanDangGiayTo { get; init; }  // "CMND:" | "MST:"
    public required decimal SoTienYeuCau { get; init; }
    public required string MucDichVay { get; init; }
    public required string TrangThaiDon { get; init; }

    // Tiến trình phê duyệt: M -> C -> A
    public required string TrangThaiMaker { get; init; }   // "pending" | "approved" | "rejected" | "inactive"
    public required string TrangThaiChecker { get; init; }
    public required string TrangThaiApprover { get; init; }
}
