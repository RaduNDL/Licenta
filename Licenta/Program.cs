using Infrastructure.Audit;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Models;
using Licenta.Services;
using Licenta.Services.Ml;
using Licenta.Services.Prediction;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Claims;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Licenta
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            QuestPDF.Settings.License = LicenseType.Community;

            var logsDir = Path.Combine(builder.Environment.ContentRootPath, "Logs");
            Directory.CreateDirectory(logsDir);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    Path.Combine(logsDir, "audit-.json"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("DefaultConnection missing"),
                    sql => sql.EnableRetryOnFailure()));

            builder.Services
                .AddDefaultIdentity<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 6;
                    options.Password.RequireDigit = true;
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

            builder.Services.AddSingleton<IPredictionTargetRegistry, PredictionTargetRegistry>();

            // Citim sectiunea "MlServiceOptions" asa cum am denumit-o in appsettings.json
            builder.Services.Configure<MlServiceOptions>(builder.Configuration.GetSection("MlServiceOptions"));

            // INREGISTRAM SERVICIUL NOU CARE PORNESTE PYTHON-UL
            builder.Services.AddHostedService<PythonServerHostedService>();

            builder.Services.AddHttpClient<IMlImagingClient, MlImagingClient>((sp, client) =>
            {
                var opt = sp.GetRequiredService<IOptions<MlServiceOptions>>().Value;

                var baseUrl = (opt.BaseUrl ?? "http://127.0.0.1:8001").Trim();
                if (!baseUrl.EndsWith('/')) baseUrl += "/";

                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 2, 120));
            }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

            builder.Services.AddHttpClient<IMlLabResultClient, MlLabResultClient>((sp, client) =>
            {
                var opt = sp.GetRequiredService<IOptions<MlServiceOptions>>().Value;

                var baseUrl = (opt.BaseUrl ?? "http://127.0.0.1:8001").Trim();
                if (!baseUrl.EndsWith('/')) baseUrl += "/";

                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(opt.TimeoutSeconds, 2, 120));
            }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

            var app = builder.Build();

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

            if (app.Environment.IsDevelopment())
            {
                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = app.Services.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            await SystemSettingsSeeder.SeedAsync(db);
                            await IdentitySeed.SeedAsync(scope.ServiceProvider);
                        }
                        catch (Exception ex)
                        {
                            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("StartupSeed");
                            logger.LogError(ex, "Startup seed failed");
                        }
                    });
                });
            }

            app.Lifetime.ApplicationStarted.Register(() =>
            {
                Console.WriteLine("APP READY");
            });

            await app.RunAsync();
        }
    }

    public class NameIdentifierUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
            => connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}