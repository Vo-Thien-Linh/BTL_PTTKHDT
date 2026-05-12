using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BTL_PTTKHDT.Models;

[ModelMetadataType(typeof(KhachHangValidationMetadata))]
public partial class KhachHang
{
    [NotMapped]
    [BindNever]
    [ValidateNever]
    public string? MaKhText => MaKh;
}

public sealed class KhachHangValidationMetadata
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string HoTen { get; set; } = null!;

    [Required(ErrorMessage = "Ngày sinh không được để trống.")]
    public DateOnly NgaySinh { get; set; }

    [Required(ErrorMessage = "CMND/CCCD không được để trống.")]
    [RegularExpression(@"^\d{12}$", ErrorMessage = "CMND/CCCD phải đúng 12 chữ số.")]
    public string CmndCccd { get; set; } = null!;

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải đúng 10 chữ số.")]
    public string SoDienThoai { get; set; } = null!;

    [Required(ErrorMessage = "Loại khách hàng không được để trống.")]
    [StringLength(20, ErrorMessage = "Loại khách hàng không được vượt quá 20 ký tự.")]
    public string LoaiKhachHang { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
    public string? DiaChi { get; set; }

    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    public string? Email { get; set; }
}