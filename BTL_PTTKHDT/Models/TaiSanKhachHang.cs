using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class TaiSanKhachHang
{
    public string MaTaiSanKh { get; set; } = null!;

    public string MaKh { get; set; } = null!;

    public string LoaiTaiSan { get; set; } = null!;

    public decimal GiaTriKhaiBao { get; set; }

    public decimal? GiaTriDinhGia { get; set; }

    public double TyLeLtv { get; set; }

    public string? GiayToPhapLy { get; set; }

    public string? MoTa { get; set; }

    public DateOnly NgayKhaiBao { get; set; }

    public DateOnly? NgayDinhGia { get; set; }

    public string? MaNvdinhGia { get; set; }

    public string TrangThai { get; set; } = null!;

    public string TrangThaiSoHuu { get; set; } = null!;

    public DateOnly? NgayBan { get; set; }

    public string? GhiChuSoHuu { get; set; }

    public virtual KhachHang MaKhNavigation { get; set; } = null!;

    public virtual NhanVien? MaNvdinhGiaNavigation { get; set; }

    public virtual ICollection<TaiSanTheChap> TaiSanTheChaps { get; set; } = new List<TaiSanTheChap>();
}
