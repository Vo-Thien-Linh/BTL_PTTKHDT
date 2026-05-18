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
    [Required(ErrorMessage = "Ho ten/ten doanh nghiep khong duoc de trong.")]
    [StringLength(100, ErrorMessage = "Ho ten/ten doanh nghiep khong duoc vuot qua 100 ky tu.")]
    public string HoTen { get; set; } = null!;

    [Required(ErrorMessage = "Ngay sinh/ngay dai dien khong duoc de trong.")]
    public DateOnly NgaySinh { get; set; }

    [Required(ErrorMessage = "CMND/CCCD hoac ma dang ky khong duoc de trong.")]
    [RegularExpression(@"^\d{10,20}$", ErrorMessage = "CMND/CCCD hoac ma dang ky phai tu 10 den 20 chu so.")]
    public string CmndCccd { get; set; } = null!;

    [Required(ErrorMessage = "So dien thoai khong duoc de trong.")]
    [RegularExpression(@"^\d{10,15}$", ErrorMessage = "So dien thoai phai tu 10 den 15 chu so.")]
    public string SoDienThoai { get; set; } = null!;

    [Required(ErrorMessage = "Loai khach hang khong duoc de trong.")]
    [StringLength(20, ErrorMessage = "Loai khach hang khong duoc vuot qua 20 ky tu.")]
    public string LoaiKhachHang { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Dia chi khong duoc vuot qua 255 ky tu.")]
    public string? DiaChi { get; set; }

    [EmailAddress(ErrorMessage = "Email khong dung dinh dang.")]
    [StringLength(150, ErrorMessage = "Email khong duoc vuot qua 150 ky tu.")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "Ma so thue khong duoc vuot qua 20 ky tu.")]
    public string? MaSoThue { get; set; }

    [StringLength(100, ErrorMessage = "Ten nguoi dai dien khong duoc vuot qua 100 ky tu.")]
    public string? TenNguoiDaiDien { get; set; }

    [StringLength(100, ErrorMessage = "Chuc vu nguoi dai dien khong duoc vuot qua 100 ky tu.")]
    public string? ChucVuNguoiDaiDien { get; set; }

    [StringLength(150, ErrorMessage = "Linh vuc kinh doanh khong duoc vuot qua 150 ky tu.")]
    public string? LinhVucKinhDoanh { get; set; }
}
