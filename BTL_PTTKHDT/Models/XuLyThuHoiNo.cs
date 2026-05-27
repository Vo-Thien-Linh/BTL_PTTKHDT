using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class XuLyThuHoiNo
{
    public string MaXuLy { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public string MaNv { get; set; } = null!;

    public DateTime NgayXuLy { get; set; }

    public string HinhThucLienHe { get; set; } = null!;

    public string KetQua { get; set; } = null!;

    public DateOnly? NgayHenTra { get; set; }

    public decimal? SoTienHenTra { get; set; }

    public string? DeXuatXuLy { get; set; }

    public string? GhiChu { get; set; }

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;

    public virtual NhanVien MaNvNavigation { get; set; } = null!;
}
