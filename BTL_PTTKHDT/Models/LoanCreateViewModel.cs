using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKHDT.Models;

public sealed class LoanCreateViewModel
{
    [Required(ErrorMessage = "Mã khách hàng không được để trống.")]
    [StringLength(10, ErrorMessage = "Mã khách hàng không được vượt quá 10 ký tự.")]
    public string MaKh { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên khách hàng không được để trống.")]
    [StringLength(256, ErrorMessage = "Tên khách hàng không được vượt quá 256 ký tự.")]
    public string TenKhachHang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Loại khách hàng không được để trống.")]
    public string LoaiKhachHang { get; set; } = "personal";

    [Required(ErrorMessage = "Số giấy tờ không được để trống.")]
    [StringLength(20, ErrorMessage = "Số giấy tờ không được vượt quá 20 ký tự.")]
    public string SoGiayTo { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "999999999999999", ErrorMessage = "Số tiền yêu cầu phải lớn hơn 0.")]
    public decimal SoTienYeuCau { get; set; }

    [Range(1, 600, ErrorMessage = "Kỳ hạn đề nghị phải lớn hơn 0.")]
    public int KyHanDeNghi { get; set; } = 12;

    [Range(typeof(double), "0.01", "1000", ErrorMessage = "Lãi suất đề nghị phải lớn hơn 0.")]
    public double? LaiSuatDeNghi { get; set; }

    [Required(ErrorMessage = "Mục đích vay không được để trống.")]
    [StringLength(255, ErrorMessage = "Mục đích vay không được vượt quá 255 ký tự.")]
    public string MucDichVay { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? GhiChu { get; set; }
}
