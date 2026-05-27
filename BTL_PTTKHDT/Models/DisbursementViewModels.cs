namespace BTL_PTTKHDT.Models;

public sealed class DisbursementListViewModel
{
    public required IReadOnlyList<DisbursementRowViewModel> Items { get; init; }
    public string? Query { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);
}

public sealed class DisbursementRowViewModel
{
    public required string MaDon { get; init; }
    public required string MaKh { get; init; }
    public required string TenKhachHang { get; init; }
    public required string MucDichVay { get; init; }
    public decimal SoTienYeuCau { get; init; }
    public int KyHanDeNghi { get; init; }
    public double? LaiSuatDeNghi { get; init; }
    public DateOnly NgayNopDon { get; init; }
    public decimal TongGiaTriDamBao { get; init; }
    public decimal HanMucGoiY { get; init; }
    public decimal? HanMucTinDungConLai { get; init; }
    public decimal SoTienCoTheGiaiNgan { get; init; }
    public bool DaDuDieuKienTaiSan { get; init; }
    public bool DaDuDieuKienHanMuc { get; init; }
}

public sealed class DisbursementDetailViewModel
{
    public required DisbursementRowViewModel Loan { get; init; }
    public required IReadOnlyList<LoanCollateralViewModel> TaiSanDamBao { get; init; }
    public bool HasActiveLoan { get; init; }
    public string? ActiveLoanMessage { get; init; }
    public bool CanDisburse => !HasActiveLoan && Loan.DaDuDieuKienTaiSan && Loan.DaDuDieuKienHanMuc;
}
