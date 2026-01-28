using Infrastructure.Audit;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services;
using Licenta.Services.Ml;
using Licenta.Services.Predictions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

var logsDir = Path.Combine(builder.Environment.ContentRootPath, "Logs");
Directory.CreateDirectory(logsDir);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logsDir, "audit-.json"),
        rollingInterval: RollingInterval.Day,
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection missing")
    ));

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRazorPages();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromHours(8);
});

var uploadLimitMb = builder.Configuration.GetValue<long?>("SystemSettings:MaxUploadMb") ?? 50;
var uploadLimitBytes = uploadLimitMb * 1024L * 1024L;

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = uploadLimitBytes;
});

builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = uploadLimitBytes;
});

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IPdfService, PdfService>();

builder.Services.AddHttpClient<IMlTabularClient, MlTabularClient>();
builder.Services.AddHttpClient<IMlImagingClient, MlImagingClient>();
builder.Services.AddHttpClient<IMlLabResultClient, MlLabResultClient>();

builder.Services.AddHostedService<MlServerStarter>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SystemSettingsSeeder.SeedAsync(db);
    await IdentitySeed.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseAuditMiddleware();

app.MapRazorPages();
app.MapHub<NotificationHub>("/notificationHub");

await app.RunAsync();

public class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
