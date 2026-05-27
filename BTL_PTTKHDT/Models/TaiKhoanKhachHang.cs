namespace BTL_PTTKHDT.Models;

public partial class TaiKhoanKhachHang
{
    public string MaTaiKhoanKh { get; set; } = null!;

    public string MaKh { get; set; } = null!;

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauHash { get; set; } = null!;

    public DateTime? LanDangNhapCuoi { get; set; }

    public byte SoLanSaiMatKhau { get; set; }

    public bool BiKhoa { get; set; }

    public DateTime NgayTao { get; set; }

    public DateTime NgayCapNhat { get; set; }

    public virtual KhachHang MaKhNavigation { get; set; } = null!;
}
