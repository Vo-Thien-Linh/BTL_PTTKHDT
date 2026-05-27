using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class NhanVien
{
    public string MaNv { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public string SoDienThoai { get; set; } = null!;

    public string? DiaChi { get; set; }

    public string? Email { get; set; }

    public DateOnly NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string VaiTro { get; set; } = null!;

    public string? AnhDaiDienUrl { get; set; }

    public DateTime NgayTao { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<DonVay> DonVays { get; set; } = new List<DonVay>();

    public virtual ICollection<CoCauNo> CoCauNos { get; set; } = new List<CoCauNo>();

    public virtual ICollection<HopDongTinDung> HopDongTinDungs { get; set; } = new List<HopDongTinDung>();

    public virtual ICollection<QuyTrinhPheDuyet> QuyTrinhPheDuyets { get; set; } = new List<QuyTrinhPheDuyet>();

    public virtual TaiKhoanNhanVien? TaiKhoanNhanVien { get; set; }

    public virtual ICollection<TaiSanKhachHang> TaiSanKhachHangs { get; set; } = new List<TaiSanKhachHang>();

    public virtual ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();

    public virtual ICollection<XuLyThuHoiNo> XuLyThuHoiNos { get; set; } = new List<XuLyThuHoiNo>();
}
