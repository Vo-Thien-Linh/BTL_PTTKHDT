using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKHDT.Models;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập, số điện thoại hoặc email.")]
    [Display(Name = "Tên đăng nhập / Số điện thoại / Email")]
    public string LoginOrEmail { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required]
    public string MaTaiKhoan { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác nhận gồm 6 chữ số.")]
    [Display(Name = "Mã xác nhận")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
