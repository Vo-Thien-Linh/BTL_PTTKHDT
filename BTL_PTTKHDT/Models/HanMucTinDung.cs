using System;
using System.Collections.Generic;

namespace BTL_PTTKHDT.Models;

public partial class HanMucTinDung
{
    public string MaKh { get; set; } = null!;

    public decimal HanMucToiDa { get; set; }

    public decimal HanMucDaSuDung { get; set; }

    public decimal? HanMucConLai { get; set; }

    public DateOnly NgayCapNhat { get; set; }

    public virtual KhachHang MaKhNavigation { get; set; } = null!;
}
