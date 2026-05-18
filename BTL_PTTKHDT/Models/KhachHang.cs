using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class KhachHang
{
    public string MaKh { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public DateOnly NgaySinh { get; set; }

    public string CmndCccd { get; set; } = null!;

    public string? DiaChi { get; set; }

    public string SoDienThoai { get; set; } = null!;

    public string? Email { get; set; }

    public string LoaiKhachHang { get; set; } = null!;

    public string? AnhDaiDienUrl { get; set; }

    public string? MaSoThue { get; set; }

    public string? TenNguoiDaiDien { get; set; }

    public string? ChucVuNguoiDaiDien { get; set; }

    public DateOnly? NgayThanhLap { get; set; }

    public string? LinhVucKinhDoanh { get; set; }

    public decimal? DoanhThuBinhQuanThang { get; set; }

    public decimal? LoiNhuanBinhQuanThang { get; set; }

    public int? SoLaoDong { get; set; }

    public DateTime NgayTao { get; set; }

    public DateTime NgayCapNhat { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<DonVay> DonVays { get; set; } = new List<DonVay>();

    public virtual HanMucTinDung? HanMucTinDung { get; set; }

    public virtual ICollection<KhoanVay> KhoanVays { get; set; } = new List<KhoanVay>();

    public virtual ICollection<LichSuTinDung> LichSuTinDungs { get; set; } = new List<LichSuTinDung>();

    public virtual ICollection<TaiSanKhachHang> TaiSanKhachHangs { get; set; } = new List<TaiSanKhachHang>();
}
