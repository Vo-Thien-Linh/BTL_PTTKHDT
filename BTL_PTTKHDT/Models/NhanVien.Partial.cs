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
    [Required(ErrorMessage = "Ho ten không được để trống.")]
    [StringLength(100, ErrorMessage = "Ho ten không được vượt quá 100 ký tự.")]
    public string HoTen { get; set; } = null!;

    [Required(ErrorMessage = "So dien thoai không được để trống.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "So dien thoai phải đúng 10 chữ số.")]
    public string SoDienThoai { get; set; } = null!;

    [Required(ErrorMessage = "Ngay sinh không được để trống.")]
    public DateOnly NgaySinh { get; set; }

    [Required(ErrorMessage = "Gioi tinh không được để trống.")]
    public string? GioiTinh { get; set; }

    [Required(ErrorMessage = "Vai tro không được để trống.")]
    [StringLength(50, ErrorMessage = "Vai tro không được vượt quá 50 ký tự.")]
    public string VaiTro { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Dia chi không được vượt quá 255 ký tự.")]
    public string? DiaChi { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [RegularExpression(@"^[^@\s]+@gmail\.com$", ErrorMessage = "Vui lòng nhập địa chỉ Gmail hợp lệ.")]
    [StringLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    public string? Email { get; set; }
}
