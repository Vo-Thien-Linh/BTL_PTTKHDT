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

    public required IReadOnlyList<DebtCollectionActionViewModel> XuLyThuHoiNo { get; init; }

    public required DebtCollectionCreateViewModel XuLyMoi { get; init; }

    public required IReadOnlyList<DebtRestructureHistoryViewModel> LichSuCoCauNo { get; init; }

    public required DebtRestructureCreateViewModel CoCauMoi { get; init; }
}

public sealed class DebtScheduleRowViewModel
{
    public required string MaLichTraNo { get; init; }
    public int KyThu { get; init; }
    public DateOnly NgayPhaiTra { get; init; }
    public decimal SoTienGoc { get; init; }
    public decimal SoTienLai { get; init; }
    public decimal SoTienDaThanhToan { get; init; }
    public string? GhiChu { get; init; }
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

public sealed class DebtCollectionActionViewModel
{
    public required string MaXuLy { get; init; }
    public DateTime NgayXuLy { get; init; }
    public required string MaNv { get; init; }
    public string? TenNhanVien { get; init; }
    public required string HinhThucLienHe { get; init; }
    public required string KetQua { get; init; }
    public DateOnly? NgayHenTra { get; init; }
    public decimal? SoTienHenTra { get; init; }
    public string? DeXuatXuLy { get; init; }
    public string? GhiChu { get; init; }
}

public sealed class DebtCollectionCreateViewModel
{
    [Required]
    [StringLength(30)]
    public string HinhThucLienHe { get; set; } = "Gọi điện";

    [Required]
    [StringLength(30)]
    public string KetQua { get; set; } = "Đã liên hệ";

    public DateOnly? NgayHenTra { get; set; }

    [Range(typeof(decimal), "0", "999999999999999", ErrorMessage = "Số tiền hẹn trả không hợp lệ.")]
    public decimal? SoTienHenTra { get; set; }

    [StringLength(50)]
    public string? DeXuatXuLy { get; set; }

    [StringLength(500)]
    public string? GhiChu { get; set; }
}

public sealed class DebtRestructureHistoryViewModel
{
    public required string MaCoCau { get; init; }
    public DateTime NgayCoCau { get; init; }
    public required string MaNv { get; init; }
    public string? TenNhanVien { get; init; }
    public int KyHanCu { get; init; }
    public int KyHanMoi { get; init; }
    public double LaiSuatCu { get; init; }
    public double LaiSuatMoi { get; init; }
    public DateOnly NgayDaoHanCu { get; init; }
    public DateOnly NgayDaoHanMoi { get; init; }
    public decimal DuNoGocCoCau { get; init; }
    public required string LyDo { get; init; }
    public string? GhiChu { get; init; }
}

public sealed class DebtRestructureCreateViewModel
{
    [Range(1, 360, ErrorMessage = "Kỳ hạn mới phải từ 1 đến 360 tháng.")]
    public int KyHanMoi { get; set; }

    public double? LaiSuatMoi { get; set; }

    [Required]
    [StringLength(500)]
    public string LyDo { get; set; } = string.Empty;

    [StringLength(500)]
    public string? GhiChu { get; set; }
}
