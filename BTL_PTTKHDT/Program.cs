using Microsoft.EntityFrameworkCore;
using BTL_PTTKHDT.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
