using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKHDT.Models;

public sealed class DebtLoanRowViewModel
{
    public required string MaVay { get; init; }
    public required string MaDon { get; init; }
    public required string MaKh { get; init; }
    public required string TenKhachHang { get; init; }
    public decimal SoTienVay { get; init; }
    public decimal DuNoGoc { get; init; }
    public int KyHan { get; init; }
    public double LaiSuat { get; init; }
    public byte NhomNo { get; init; }
    public DateOnly NgayGiaiNgan { get; init; }
    public string TrangThai { get; init; } = string.Empty;
}

public sealed class DebtLoanDetailViewModel
{
    public required DebtLoanRowViewModel Loan { get; init; }

    public required IReadOnlyList<DebtScheduleRowViewModel> LichTraNo { get; init; }
    public required IReadOnlyList<DebtCollateralRowViewModel> TaiSanTheChap { get; init; }

    public DebtContractViewModel? HopDong { get; init; }

    public required DebtPaymentCreateViewModel ThanhToanMoi { get; init; }
}

public sealed class DebtScheduleRowViewModel
{
    public required string MaLichTraNo { get; init; }
    public int KyThu { get; init; }
    public DateOnly NgayPhaiTra { get; init; }
    public decimal SoTienGoc { get; init; }
    public decimal SoTienLai { get; init; }
    public decimal SoTienDaThanhToan { get; init; }
    public required string TrangThai { get; init; }
    public int DaysOverdue { get; init; }
    public bool WasPaidLate { get; init; }
}

public sealed class DebtCollateralRowViewModel
{
    public required string MaTaiSan { get; init; }
    public required string MaTaiSanKh { get; init; }
    public required string LoaiTaiSan { get; init; }
    public decimal GiaTriTheChap { get; init; }
    public required string TrangThai { get; init; }
}

public sealed class DebtContractViewModel
{
    public required string MaHopDong { get; init; }
    public DateOnly NgayKyHopDong { get; init; }
    public required string MaNv { get; init; }
}

public sealed class DebtPaymentCreateViewModel
{
    [Required]
    public string MaLichTraNo { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "999999999999999", ErrorMessage = "So tien thanh toán phải lớn hơn 0.")]
    public decimal SoTienThanhToan { get; set; }

    [StringLength(30)]
    public string HinhThuc { get; set; } = "Tiền mặt";

    [StringLength(255)]
    public string? GhiChu { get; set; }
}
