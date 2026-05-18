using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class TaiSanTheChap
{
    public string MaTaiSan { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public string MaTaiSanKh { get; set; } = null!;

    public decimal GiaTriTheChap { get; set; }

    public DateOnly NgayTheChap { get; set; }

    public DateOnly? NgayGiaiChap { get; set; }

    public string TrangThai { get; set; } = null!;

    public string? GhiChu { get; set; }

    public virtual TaiSanKhachHang MaTaiSanKhNavigation { get; set; } = null!;

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;
}
