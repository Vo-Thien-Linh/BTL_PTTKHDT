using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class HopDongTinDung
{
    public string MaHopDong { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public string MaNv { get; set; } = null!;

    public DateOnly NgayKyHopDong { get; set; }

    public string? NoiDung { get; set; }

    public string? DieuKhoan { get; set; }

    public string? FileUrl { get; set; }

    public DateTime NgayTao { get; set; }

    public virtual NhanVien MaNvNavigation { get; set; } = null!;

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;
}
