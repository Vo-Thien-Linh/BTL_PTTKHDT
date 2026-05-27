using BTL_PTTKHDT.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BTL_PTTKHDT.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    public PermissionAuthorizeAttribute(string permissionCode)
        : base(typeof(PermissionAuthorizeFilter))
    {
        Arguments = [permissionCode];
    }
}

public sealed class PermissionAuthorizeFilter : IAsyncAuthorizationFilter
{
    private readonly string _permissionCode;
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizeFilter(string permissionCode, IPermissionService permissionService)
    {
        _permissionCode = permissionCode;
        _permissionService = permissionService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var allowed = await _permissionService.HasPermissionAsync(
            context.HttpContext.User,
            _permissionCode,
            context.HttpContext.RequestAborted);

        if (!allowed)
        {
            context.Result = new ForbidResult();
        }
    }
}
