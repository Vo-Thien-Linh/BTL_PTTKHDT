using System;

namespace BTL_PTTKHDT.Models;

public partial class CoCauNo
{
    public string MaCoCau { get; set; } = null!;

    public string MaVay { get; set; } = null!;

    public string MaNv { get; set; } = null!;

    public DateTime NgayCoCau { get; set; }

    public int KyHanCu { get; set; }

    public int KyHanMoi { get; set; }

    public double LaiSuatCu { get; set; }

    public double LaiSuatMoi { get; set; }

    public DateOnly NgayDaoHanCu { get; set; }

    public DateOnly NgayDaoHanMoi { get; set; }

    public decimal DuNoGocCoCau { get; set; }

    public string LyDo { get; set; } = null!;

    public string? GhiChu { get; set; }

    public virtual KhoanVay MaVayNavigation { get; set; } = null!;

    public virtual NhanVien MaNvNavigation { get; set; } = null!;
}
