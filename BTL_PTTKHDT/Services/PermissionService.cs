using System.Security.Claims;
using BTL_PTTKHDT.Models;
using BTL_PTTKHDT.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Services;

public sealed record RolePermissionState(PermissionItem Permission, bool IsAllowed, bool IsDefault);

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionCode, CancellationToken ct = default);

    Task<IReadOnlyList<RolePermissionState>> GetRolePermissionsAsync(string role, CancellationToken ct = default);

    Task SaveRolePermissionsAsync(string role, IReadOnlyCollection<string> selectedPermissionCodes, CancellationToken ct = default);
}

public sealed class PermissionService : IPermissionService
{
    private readonly QltdnhContext _db;

    public PermissionService(QltdnhContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionCode, CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true) return false;

        var role = AppRoles.NormalizeForClaim(user.FindFirst(ClaimTypes.Role)?.Value);
        if (string.IsNullOrWhiteSpace(role)) return false;

        var overrides = await LoadOverridesAsync(role, ct);
        if (overrides.TryGetValue(permissionCode, out var allowed))
        {
            return allowed;
        }

        return AppPermissions.DefaultsForRole(role).Contains(permissionCode);
    }

    public async Task<IReadOnlyList<RolePermissionState>> GetRolePermissionsAsync(string role, CancellationToken ct = default)
    {
        role = AppRoles.NormalizeForClaim(role);
        var defaults = AppPermissions.DefaultsForRole(role);
        var overrides = await LoadOverridesAsync(role, ct);

        return AppPermissions.All
            .Select(permission =>
            {
                var hasDefault = defaults.Contains(permission.Code);
                var allowed = overrides.TryGetValue(permission.Code, out var overrideValue)
                    ? overrideValue
                    : hasDefault;

                return new RolePermissionState(permission, allowed, hasDefault);
            })
            .ToList();
    }

    public async Task SaveRolePermissionsAsync(string role, IReadOnlyCollection<string> selectedPermissionCodes, CancellationToken ct = default)
    {
        role = AppRoles.NormalizeForClaim(role);
        if (!AppRoles.IsValid(role))
        {
            throw new InvalidOperationException("Vai trò không hợp lệ.");
        }

        var selected = selectedPermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (role == AppRoles.QuanTriHeThong)
        {
            selected.Add(AppPermissions.ManageEmployees);
            selected.Add(AppPermissions.ManagePermissions);
        }

        var existing = await _db.PhanQuyenVaiTros
            .Where(x => x.VaiTro == role)
            .ToListAsync(ct);

        var byCode = existing.ToDictionary(x => x.MaQuyen, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.Now;

        foreach (var permission in AppPermissions.All)
        {
            var allowed = selected.Contains(permission.Code);
            if (byCode.TryGetValue(permission.Code, out var row))
            {
                row.DuocPhep = allowed;
                row.NgayCapNhat = now;
            }
            else
            {
                _db.PhanQuyenVaiTros.Add(new PhanQuyenVaiTro
                {
                    VaiTro = role,
                    MaQuyen = permission.Code,
                    DuocPhep = allowed,
                    NgayCapNhat = now
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<string, bool>> LoadOverridesAsync(string role, CancellationToken ct)
    {
        try
        {
            var rows = await _db.PhanQuyenVaiTros
                .AsNoTracking()
                .Where(x => x.VaiTro == role)
                .ToListAsync(ct);

            return rows.ToDictionary(x => x.MaQuyen, x => x.DuocPhep, StringComparer.OrdinalIgnoreCase);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
