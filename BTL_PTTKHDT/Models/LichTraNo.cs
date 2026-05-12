using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class LichTraNo
{
    public string MaLichTraNo { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public int KyThu { get; set; }

    public DateOnly NgayPhaiTra { get; set; }

    public decimal SoTienGoc { get; set; }

    public decimal SoTienLai { get; set; }

    public decimal? TongPhaiTra { get; set; }

    public decimal SoTienDaThanhToan { get; set; }

    public string TrangThai { get; set; } = null!;

    public DateOnly? NgayThanhToanThucTe { get; set; }

    public string? GhiChu { get; set; }

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;

    public virtual ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}
