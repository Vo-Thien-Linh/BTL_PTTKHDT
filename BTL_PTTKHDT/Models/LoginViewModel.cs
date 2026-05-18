using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKHDT.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc số điện thoại.")]
    [Display(Name = "Tên đăng nhập")]
    public string TenDangNhap { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
