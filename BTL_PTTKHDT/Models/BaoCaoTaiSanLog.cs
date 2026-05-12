using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class BaoCaoTaiSanLog
{
    public string MaBaoCao { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public decimal TongGiaTri { get; set; }

    public int SoLuongTaiSan { get; set; }

    public double? TyLeLtvtongHop { get; set; }

    public DateTime NgayBaoCao { get; set; }

    public string? GhiChu { get; set; }

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;
}
