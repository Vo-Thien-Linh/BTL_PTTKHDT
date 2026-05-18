namespace BTL_PTTKHDT.Models;

public sealed class CustomerListViewModel
{
    public required IReadOnlyList<CustomerRowViewModel> Items { get; init; }

    public string? Query { get; init; }
    public string? Type { get; init; }

    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    public int TotalPages => TotalCount <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);

    public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);
}

public sealed class CustomerRowViewModel
{
    public required string MaKh { get; init; }
    public required string MaKhText { get; init; }

    public required string HoTen { get; init; }
    public required DateOnly NgaySinh { get; init; }
    public required string CmndCccd { get; init; }
    public required string LoaiKhachHangText { get; init; }
    public required string LoaiKhachHangKind { get; init; }

    public required string SoDienThoai { get; init; }
    public string? Email { get; init; }
    public string? DiaChi { get; init; }
    public string? AnhDaiDienUrl { get; init; }

    public int? DiemTinDung { get; init; }
    public string? XepHangRuiRo { get; init; }

    public required bool IsActive { get; init; }
}
