using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class KhoanVay
{
    public string MaVay { get; set; } = null!;

    public string MaDon { get; set; } = null!;

    public string MaKh { get; set; } = null!;

    public decimal SoTienVay { get; set; }

    public double LaiSuat { get; set; }

    public int KyHan { get; set; }

    public string PhuongThucTraNo { get; set; } = null!;

    public DateOnly NgayGiaiNgan { get; set; }

    public DateOnly NgayDaoHan { get; set; }

    public decimal DuNoGoc { get; set; }

    public string TrangThai { get; set; } = null!;

    public byte NhomNo { get; set; }

    public DateOnly NgayCapNhatNhom { get; set; }

    public string? GhiChu { get; set; }

    public DateTime NgayTao { get; set; }

    public virtual ICollection<BaoCaoTaiSanLog> BaoCaoTaiSanLogs { get; set; } = new List<BaoCaoTaiSanLog>();

    public virtual HopDongTinDung? HopDongTinDung { get; set; }

    public virtual ICollection<LichTraNo> LichTraNos { get; set; } = new List<LichTraNo>();

    public virtual DonVay MaDonNavigation { get; set; } = null!;

    public virtual KhachHang MaKhNavigation { get; set; } = null!;

    public virtual ICollection<SaoKeTinDung> SaoKeTinDungs { get; set; } = new List<SaoKeTinDung>();

    public virtual ICollection<TaiSanTheChap> TaiSanTheChaps { get; set; } = new List<TaiSanTheChap>();

    public virtual ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}
