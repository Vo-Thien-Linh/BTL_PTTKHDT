using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKHDT.Models;

public sealed class LoanDetailViewModel
{
    public required LoanRowViewModel Loan { get; init; }

    public required LoanAppraisalReportViewModel ThamDinh { get; init; }

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

public sealed class LoanAppraisalReportViewModel
{
    public required string MaKh { get; init; }
    public required string HoTen { get; init; }
    public required string LoaiKhachHang { get; init; }
    public DateOnly NgaySinh { get; init; }
    public string? CmndCccd { get; init; }
    public string? DiaChi { get; init; }
    public string? SoDienThoai { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }

    public string? MaSoThue { get; init; }
    public string? TenNguoiDaiDien { get; init; }
    public string? ChucVuNguoiDaiDien { get; init; }
    public DateOnly? NgayThanhLap { get; init; }
    public string? LinhVucKinhDoanh { get; init; }
    public decimal? DoanhThuBinhQuanThang { get; init; }
    public decimal? LoiNhuanBinhQuanThang { get; init; }
    public int? SoLaoDong { get; init; }

    public int? DiemTinDung { get; init; }
    public string? XepHangRuiRo { get; init; }
    public int SoLanTraTre { get; init; }
    public decimal? ThuNhapHangThang { get; init; }
    public double? TyLeNoThuNhap { get; init; }
    public string? GhiChuTinDung { get; init; }
    public DateTime? NgayCapNhatTinDung { get; init; }
    public string? NguonCapNhatTinDung { get; init; }

    public decimal TongDuNoGocHienTai { get; init; }
    public int SoKhoanVayDangHoatDong { get; init; }
    public byte NhomNoCaoNhat { get; init; }
    public bool CoNoQuaHan { get; init; }
    public bool CoNoXau { get; init; }

    public bool DuTuCachVayVon => IsActive;
    public bool CoKhaNangTaiChinh => (DiemTinDung ?? 650) >= 500 && !CoNoXau;
    public bool MucDichHopPhap => true;
    public bool DuDieuKienDamBao => TongGiaTriDamBao > 0 && HanMucGoiY >= SoTienYeuCau;
    public bool DeXuatChoVay => DuTuCachVayVon && CoKhaNangTaiChinh && MucDichHopPhap && DuDieuKienDamBao;

    public decimal SoTienYeuCau { get; init; }
    public decimal TongGiaTriDamBao { get; init; }
    public decimal HanMucGoiY { get; init; }
    public decimal TyLeLtv { get; init; }

    public decimal? HanMucToiDa { get; init; }
    public decimal? HanMucDaSuDung { get; init; }
    public decimal? HanMucConLai { get; init; }
    public DateOnly? NgayCapNhatHanMuc { get; init; }
}

public sealed class LoanCollateralViewModel
{
    public required string MaTaiSanKh { get; init; }
    public required string LoaiTaiSan { get; init; }
    public required decimal GiaTriKhaiBao { get; init; }
    public decimal? GiaTriDinhGia { get; init; }
    public double TyLeLtv { get; init; }
    public required string TrangThai { get; init; }
    public required string TrangThaiSoHuu { get; init; }
    public string? MoTa { get; init; }
    public string? GiayToPhapLy { get; init; }
    public DateOnly NgayKhaiBao { get; init; }
    public DateOnly? NgayDinhGia { get; init; }
}

public sealed class LoanCollateralCreateViewModel
{
    [Required(ErrorMessage = "Loai tai san không được để trống.")]
    [StringLength(100, ErrorMessage = "Loai tai san không được vượt quá 100 ký tự.")]
    public string LoaiTaiSan { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "999999999999999", ErrorMessage = "Giá trị khai báo phải lớn hơn 0.")]
    public decimal GiaTriKhaiBao { get; set; }

    [Range(typeof(double), "0.01", "1", ErrorMessage = "Tỷ lệ LTV phải trong khoảng (0, 1].")]
    public double TyLeLtv { get; set; } = 0.7;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string? MoTa { get; set; }

    [StringLength(500, ErrorMessage = "Giay to phap ly không được vượt quá 500 ký tự.")]
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
