using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class DonVay
{
    public string MaDon { get; set; } = null!;

    public string MaKh { get; set; } = null!;

    public string? MaNvsoan { get; set; }

    public string MucDichVay { get; set; } = null!;

    public decimal SoTienYeuCau { get; set; }

    public int KyHanDeNghi { get; set; }

    public double? LaiSuatDeNghi { get; set; }

    public DateOnly NgayNopDon { get; set; }

    public string TrangThaiDon { get; set; } = null!;

    public string? GhiChu { get; set; }

    public DateTime NgayTao { get; set; }

    public DateTime NgayCapNhat { get; set; }

    public virtual ICollection<KhoanVay> KhoanVays { get; set; } = new List<KhoanVay>();

    public virtual KhachHang MaKhNavigation { get; set; } = null!;

    public virtual NhanVien? MaNvsoanNavigation { get; set; }

    public virtual ICollection<QuyTrinhPheDuyet> QuyTrinhPheDuyets { get; set; } = new List<QuyTrinhPheDuyet>();
}
