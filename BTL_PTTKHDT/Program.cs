using Microsoft.EntityFrameworkCore;
using BTL_PTTKHDT.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var principal = context.Principal;
                if (principal?.Identity?.IsAuthenticated != true) return;

                var accountId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(accountId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var role = principal.FindFirst(ClaimTypes.Role)?.Value;
                var accountKind = principal.FindFirst("AccountKind")?.Value;
                var db = context.HttpContext.RequestServices.GetRequiredService<QltdnhContext>();
                var ct = context.HttpContext.RequestAborted;

                if (string.Equals(accountKind, "Customer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(role, BTL_PTTKHDT.Security.AppRoles.KhachHang, StringComparison.OrdinalIgnoreCase))
                {
                    var isAllowed = await (
                            from account in db.TaiKhoanKhachHangs.AsNoTracking()
                            join customer in db.KhachHangs.AsNoTracking() on account.MaKh equals customer.MaKh
                            where account.MaTaiKhoanKh == accountId
                            select !account.BiKhoa && customer.IsActive)
                        .FirstOrDefaultAsync(ct);

                    if (!isAllowed)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }

                    return;
                }

                var employeeAllowed = await (
                        from account in db.TaiKhoanNhanViens.AsNoTracking()
                        join employee in db.NhanViens.AsNoTracking() on account.MaNv equals employee.MaNv
                        where account.MaTaiKhoan == accountId
                        select !account.BiKhoa && employee.IsActive)
                    .FirstOrDefaultAsync(ct);

                if (!employeeAllowed)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddDbContext<QltdnhContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");
    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<BTL_PTTKHDT.Services.IDashboardService, BTL_PTTKHDT.Services.DashboardService>();
builder.Services.AddScoped<BTL_PTTKHDT.Services.ICreditScoreService, BTL_PTTKHDT.Services.CreditScoreService>();
builder.Services.AddScoped<BTL_PTTKHDT.Services.IPermissionService, BTL_PTTKHDT.Services.PermissionService>();
builder.Services.Configure<BTL_PTTKHDT.Services.GmailSmtpOptions>(builder.Configuration.GetSection("GmailSmtp"));
builder.Services.AddScoped<BTL_PTTKHDT.Services.IEmailSender, BTL_PTTKHDT.Services.GmailEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
