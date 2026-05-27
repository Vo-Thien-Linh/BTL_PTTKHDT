namespace BTL_PTTKHDT.Models;

public partial class PhanQuyenVaiTro
{
    public string VaiTro { get; set; } = null!;

    public string MaQuyen { get; set; } = null!;

    public bool DuocPhep { get; set; }

    public DateTime NgayCapNhat { get; set; }
}
