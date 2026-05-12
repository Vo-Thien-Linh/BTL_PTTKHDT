using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class TaiKhoanNhanVien
{
    public string MaTaiKhoan { get; set; } = null!;

    public string MaNv { get; set; } = null!;

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauHash { get; set; } = null!;

    public DateTime? LanDangNhapCuoi { get; set; }

    public byte SoLanSaiMatKhau { get; set; }

    public bool BiKhoa { get; set; }

    public DateTime NgayTao { get; set; }

    public DateTime NgayCapNhat { get; set; }

    public virtual NhanVien MaNvNavigation { get; set; } = null!;
}
