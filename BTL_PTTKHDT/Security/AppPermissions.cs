namespace BTL_PTTKHDT.Security;

public sealed record PermissionItem(string Code, string Name, string Group);

public static class AppPermissions
{
    public const string ViewDashboard = "dashboard.view";
    public const string ViewCustomers = "customers.view";
    public const string CreateCustomers = "customers.create";
    public const string EditCustomers = "customers.edit";
    public const string DeleteCustomers = "customers.delete";
    public const string ViewLoans = "loans.view";
    public const string CreateLoans = "loans.create";
    public const string EditLoans = "loans.edit";
    public const string AppraiseCollateral = "collateral.appraise";
    public const string ExportAppraisalPdf = "appraisal.pdf";
    public const string ApproveLevel1 = "loans.approve.level1";
    public const string ApproveLevel2 = "loans.approve.level2";
    public const string ApproveLevel3 = "loans.approve.level3";
    public const string DisburseLoans = "loans.disburse";
    public const string ViewDebts = "debts.view";
    public const string CollectDebts = "debts.collect";
    public const string ManageEmployees = "employees.manage";
    public const string ManagePermissions = "permissions.manage";

    public static readonly PermissionItem[] All =
    [
        new(ViewDashboard, "Xem bảng điều khiển", "Tổng quan"),
        new(ViewCustomers, "Xem khách hàng", "Khách hàng"),
        new(CreateCustomers, "Thêm khách hàng", "Khách hàng"),
        new(EditCustomers, "Sửa khách hàng / tài sản", "Khách hàng"),
        new(DeleteCustomers, "Xóa / ngừng khách hàng", "Khách hàng"),
        new(ViewLoans, "Xem hồ sơ vay", "Hồ sơ vay"),
        new(CreateLoans, "Tạo hồ sơ vay", "Hồ sơ vay"),
        new(EditLoans, "Sửa hồ sơ vay", "Hồ sơ vay"),
        new(AppraiseCollateral, "Thẩm định tài sản", "Thẩm định"),
        new(ExportAppraisalPdf, "Xuất PDF thẩm định", "Thẩm định"),
        new(ApproveLevel1, "Duyệt cấp 1 / tạo đơn", "Phê duyệt"),
        new(ApproveLevel2, "Duyệt cấp 2 / kiểm soát", "Phê duyệt"),
        new(ApproveLevel3, "Duyệt cấp 3 / trưởng phòng", "Phê duyệt"),
        new(DisburseLoans, "Giải ngân", "Giải ngân"),
        new(ViewDebts, "Xem khoản vay / nợ", "Thu nợ"),
        new(CollectDebts, "Ghi nhận thanh toán / tất toán / xử lý thu hồi nợ", "Thu nợ"),
        new(ManageEmployees, "Quản lý nhân viên", "Quản trị"),
        new(ManagePermissions, "Phân quyền chức vụ", "Quản trị")
    ];

    public static IReadOnlySet<string> DefaultsForRole(string role)
    {
        role = AppRoles.NormalizeForClaim(role);

        if (role == AppRoles.QuanTriHeThong)
        {
            return All.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var codes = role switch
        {
            AppRoles.GiaoDichVien => new[]
            {
                ViewDashboard, ViewCustomers, CreateCustomers, EditCustomers,
                ViewLoans, CreateLoans, EditLoans, ApproveLevel1
            },
            AppRoles.NhanVienTinDung => new[]
            {
                ViewDashboard, ViewCustomers, CreateCustomers, EditCustomers,
                ViewLoans, AppraiseCollateral, ExportAppraisalPdf
            },
            AppRoles.NhanVienThuNo => new[]
            {
                ViewDashboard, ViewDebts, CollectDebts
            },
            AppRoles.KiemSoatVien => new[]
            {
                ViewDashboard, ViewCustomers, ViewLoans, ExportAppraisalPdf, ApproveLevel2
            },
            AppRoles.TruongPhong => new[]
            {
                ViewDashboard, ViewCustomers, ViewLoans, ExportAppraisalPdf,
                ApproveLevel3, DisburseLoans, ViewDebts
            },
            _ => []
        };

        return codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
