using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BTL_PTTKHDT.Models;

[ModelMetadataType(typeof(NhanVienValidationMetadata))]
public partial class NhanVien
{
    [NotMapped]
    [BindNever]
    [ValidateNever]
    public string? MaNvText => MaNv;
}

public sealed class NhanVienValidationMetadata
{
    [Required(ErrorMessage = "Họ tên không được để trống.")]
    [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
    public string HoTen { get; set; } = null!;

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Số điện thoại phải đúng 10 chữ số.")]
    public string SoDienThoai { get; set; } = null!;

    [Required(ErrorMessage = "Ngày sinh không được để trống.")]
    public DateOnly NgaySinh { get; set; }

    [Required(ErrorMessage = "Giới tính không được để trống.")]
    public string? GioiTinh { get; set; }

    [Required(ErrorMessage = "Vai trò không được để trống.")]
    [StringLength(50, ErrorMessage = "Vai trò không được vượt quá 50 ký tự.")]
    public string VaiTro { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
    public string? DiaChi { get; set; }
}