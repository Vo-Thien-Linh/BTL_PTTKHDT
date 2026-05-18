using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class LichSuTinDung
{
    public string MaLichSu { get; set; } = null!;

    public string MaKh { get; set; } = null!;

    public int DiemTinDung { get; set; }

    public string XepHangRuiRo { get; set; } = null!;

    public int SoLanTraTre { get; set; }

    public decimal? ThuNhapHangThang { get; set; }

    public double? TyLeNoThuNhap { get; set; }

    public string NguonCapNhat { get; set; } = null!;

    public DateTime NgayCapNhat { get; set; }

    public string? GhiChu { get; set; }

    public virtual KhachHang MaKhNavigation { get; set; } = null!;
}
