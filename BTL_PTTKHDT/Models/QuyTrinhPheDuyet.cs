using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class QuyTrinhPheDuyet
{
    public string MaPheDuyet { get; set; } = null!;

    public string MaDon { get; set; } = null!;

    public string MaNv { get; set; } = null!;

    public byte CapPheDuyet { get; set; }

    public string TrangThai { get; set; } = null!;

    public DateTime NgayXuLy { get; set; }

    public string? GhiChu { get; set; }

    public virtual DonVay MaDonNavigation { get; set; } = null!;

    public virtual NhanVien MaNvNavigation { get; set; } = null!;
}
