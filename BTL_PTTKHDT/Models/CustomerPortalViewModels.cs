namespace BTL_PTTKHDT.Models;

public sealed class CustomerPortalDashboardViewModel
{
    public required CustomerPortalProfileViewModel Profile { get; init; }

    public required IReadOnlyList<CustomerPortalLoanApplicationViewModel> Applications { get; init; }

    public required CustomerPortalCreditLimitViewModel CreditLimit { get; init; }

    public required IReadOnlyList<CustomerPortalLoanViewModel> ActiveLoans { get; init; }

    public required IReadOnlyList<CustomerPortalScheduleViewModel> RepaymentSchedule { get; init; }

    public required CustomerPortalDebtStatusViewModel DebtStatus { get; init; }

    public required IReadOnlyList<CustomerPortalPaymentViewModel> Payments { get; init; }
}

public sealed class CustomerPortalProfileViewModel
{
    public required string MaKh { get; init; }

    public required string HoTen { get; init; }

    public required string SoDienThoai { get; init; }

    public string? Email { get; init; }

    public string? DiaChi { get; init; }

    public required string SoGiayTo { get; init; }

    public required string LoaiKhachHang { get; init; }

    public required string TrangThai { get; init; }

    public string? NgheNghiep { get; init; }

    public string? NoiLamViec { get; init; }

    public string? ChucVu { get; init; }

    public decimal? ThuNhapHangThang { get; init; }
}

public sealed class CustomerPortalLoanApplicationViewModel
{
    public required string MaDon { get; init; }

    public required string MucDichVay { get; init; }

    public decimal SoTienYeuCau { get; init; }

    public int KyHanDeNghi { get; init; }

    public double? LaiSuatDeNghi { get; init; }

    public required string TrangThaiDon { get; init; }

    public string? LyDoTuChoi { get; init; }

    public decimal? SoTienDuocDuyet { get; init; }

    public int? KyHanDuocDuyet { get; init; }

    public double? LaiSuatApDung { get; init; }

    public DateOnly? NgayGiaiNgan { get; init; }
}

public sealed class CustomerPortalCreditLimitViewModel
{
    public bool CoHanMucTinDung { get; init; }

    public decimal? HanMucToiDa { get; init; }

    public decimal? HanMucDaSuDung { get; init; }

    public decimal? HanMucConLai { get; init; }

    public decimal HanMucGoiYTheoTaiSan { get; init; }

    public decimal SoTienCoTheVay { get; init; }

    public decimal TongGiaTriTaiSanBaoDam { get; init; }

    public decimal TyLeLtvTongHop { get; init; }
}

public sealed class CustomerPortalLoanViewModel
{
    public required string MaVay { get; init; }

    public decimal SoTienVay { get; init; }

    public decimal DuNoGoc { get; init; }

    public double LaiSuat { get; init; }

    public int KyHan { get; init; }

    public DateOnly NgayGiaiNgan { get; init; }

    public DateOnly NgayDaoHan { get; init; }

    public required string TrangThai { get; init; }
}

public sealed class CustomerPortalScheduleViewModel
{
    public required string MaVay { get; init; }

    public int KyThu { get; init; }

    public DateOnly NgayPhaiTra { get; init; }

    public decimal SoTienGoc { get; init; }

    public decimal SoTienLai { get; init; }

    public decimal TongPhaiTra { get; init; }

    public decimal SoTienDaThanhToan { get; init; }

    public required string TrangThai { get; init; }
}

public sealed class CustomerPortalDebtStatusViewModel
{
    public decimal TongDuNoHienTai { get; init; }

    public int SoKyConPhaiTra { get; init; }

    public int SoKyTreHan { get; init; }

    public byte NhomNoCaoNhat { get; init; }

    public bool CoCanhBaoQuaHan { get; init; }
}

public sealed class CustomerPortalPaymentViewModel
{
    public DateTime NgayThanhToan { get; init; }

    public decimal SoTienThanhToan { get; init; }

    public decimal SoTienGocTra { get; init; }

    public decimal SoTienLaiTra { get; init; }

    public decimal SoTienPhatTra { get; init; }

    public required string HinhThuc { get; init; }
}
