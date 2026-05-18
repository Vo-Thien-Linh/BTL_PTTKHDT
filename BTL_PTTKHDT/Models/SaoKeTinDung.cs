using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class SaoKeTinDung
{
    public string MaSaoKe { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public DateOnly NgaySaoKe { get; set; }

    public decimal DuNoDauKy { get; set; }

    public decimal TongTraGoc { get; set; }

    public decimal TongTraLai { get; set; }

    public decimal DuNoCuoiKy { get; set; }

    public decimal SoTienPhaiTraKy { get; set; }

    public int SoKyTraTre { get; set; }

    public string? ChiTietGiaoDich { get; set; }

    public DateTime NgayTao { get; set; }

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;
}
