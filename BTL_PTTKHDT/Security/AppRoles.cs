using System.Globalization;
using System.Text;

namespace BTL_PTTKHDT.Security;

public static class AppRoles
{
    public const string GiaoDichVien = "Giao dịch viên";
    public const string NhanVienTinDung = "Nhân viên tín dụng";
    public const string NhanVienThuNo = "Nhân viên thu nợ";
    public const string KiemSoatVien = "Kiểm soát viên";
    public const string TruongPhong = "Trưởng phòng";
    public const string QuanTriHeThong = "Quản trị hệ thống";
    public const string KhachHang = "Khách hàng";

    public const string CustomerAccess = GiaoDichVien + "," + NhanVienTinDung + "," + KiemSoatVien + "," + TruongPhong + "," + QuanTriHeThong;
    public const string CustomerWrite = GiaoDichVien + "," + NhanVienTinDung + "," + QuanTriHeThong;
    public const string LoanAccess = GiaoDichVien + "," + NhanVienTinDung + "," + KiemSoatVien + "," + TruongPhong + "," + QuanTriHeThong;
    public const string LoanCreateEdit = GiaoDichVien + "," + QuanTriHeThong;
    public const string CollateralAppraisal = NhanVienTinDung + "," + QuanTriHeThong;
    public const string CheckerApproval = KiemSoatVien + "," + QuanTriHeThong;
    public const string ManagerApproval = TruongPhong + "," + QuanTriHeThong;
    public const string Disbursement = TruongPhong + "," + QuanTriHeThong;
    public const string DebtCollection = NhanVienThuNo + "," + TruongPhong + "," + QuanTriHeThong;
    public const string EmployeeAdmin = QuanTriHeThong;
    public const string Dashboard = GiaoDichVien + "," + NhanVienTinDung + "," + NhanVienThuNo + "," + KiemSoatVien + "," + TruongPhong + "," + QuanTriHeThong;

    public static readonly string[] All =
    [
        GiaoDichVien,
        NhanVienTinDung,
        NhanVienThuNo,
        KiemSoatVien,
        TruongPhong,
        QuanTriHeThong
    ];

    public static bool IsValid(string? value)
    {
        var role = NormalizeForClaim(value);
        return All.Contains(role, StringComparer.Ordinal);
    }

    public static string NormalizeForClaim(string? value)
    {
        var normalized = RemoveDiacritics(value).ToLowerInvariant();
        return normalized switch
        {
            "giao dich vien" => GiaoDichVien,
            "nhan vien tin dung" => NhanVienTinDung,
            "nhan vien thu no" => NhanVienThuNo,
            "kiem soat vien" => KiemSoatVien,
            "truong phong" => TruongPhong,
            "quan tri he thong" => QuanTriHeThong,
            _ => value?.Trim() ?? string.Empty
        };
    }

    private static string RemoveDiacritics(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var formD = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
