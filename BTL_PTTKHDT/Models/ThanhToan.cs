using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class ThanhToan
{
    public string MaThanhToan { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public string? MaLichTraNo { get; set; }

    public string? MaNv { get; set; }

    public decimal SoTienThanhToan { get; set; }

    public decimal SoTienGocTra { get; set; }

    public decimal SoTienLaiTra { get; set; }

    public decimal SoTienPhatTra { get; set; }

    public DateTime NgayThanhToan { get; set; }

    public string HinhThuc { get; set; } = null!;

    public string? GhiChu { get; set; }

    public virtual LichTraNo? MaLichTraNoNavigation { get; set; }

    public virtual NhanVien? MaNvNavigation { get; set; }

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;
}
