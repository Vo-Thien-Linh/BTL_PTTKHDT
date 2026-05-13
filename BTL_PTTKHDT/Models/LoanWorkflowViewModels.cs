using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKHDT.Models;

public sealed class LoanDetailViewModel
{
    public required LoanRowViewModel Loan { get; init; }

    public int KyHanDeNghi { get; init; }
    public double? LaiSuatDeNghi { get; init; }
    public DateOnly NgayNopDon { get; init; }
    public string? GhiChu { get; init; }

    public required IReadOnlyList<LoanCollateralViewModel> TaiSanDamBao { get; init; }
    public required IReadOnlyList<LoanApprovalStepViewModel> PheDuyet { get; init; }

    public decimal TongGiaTriDamBao { get; init; }
    public decimal HanMucGoiY { get; init; }
    public decimal TyLeLtv { get; init; }
}

public sealed class LoanCollateralViewModel
{
    public required string MaTaiSanKh { get; init; }
    public required string LoaiTaiSan { get; init; }
    public required decimal GiaTriKhaiBao { get; init; }
    public decimal? GiaTriDinhGia { get; init; }
    public double TyLeLtv { get; init; }
    public required string TrangThai { get; init; }
    public string? MoTa { get; init; }
    public string? GiayToPhapLy { get; init; }
}

public sealed class LoanCollateralCreateViewModel
{
    [Required(ErrorMessage = "Loại tài sản không được để trống.")]
    [StringLength(100, ErrorMessage = "Loại tài sản không được vượt quá 100 ký tự.")]
    public string LoaiTaiSan { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "999999999999999", ErrorMessage = "Giá trị khai báo phải lớn hơn 0.")]
    public decimal GiaTriKhaiBao { get; set; }

    [Range(typeof(double), "0.01", "1", ErrorMessage = "Tỷ lệ LTV phải trong khoảng (0, 1].")]
    public double TyLeLtv { get; set; } = 0.7;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string? MoTa { get; set; }

    [StringLength(500, ErrorMessage = "Giấy tờ pháp lý không được vượt quá 500 ký tự.")]
    public string? GiayToPhapLy { get; set; }
}

public sealed class LoanCollateralValuationViewModel
{
    [Required]
    public string MaTaiSanKh { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "999999999999999", ErrorMessage = "Giá trị định giá phải lớn hơn 0.")]
    public decimal GiaTriDinhGia { get; set; }
}

public sealed class LoanApprovalStepViewModel
{
    public int CapPheDuyet { get; init; }
    public required string CapText { get; init; }
    public required string TrangThai { get; init; }
    public string? MaNv { get; init; }
    public DateTime? NgayXuLy { get; init; }
    public string? GhiChu { get; init; }
}

