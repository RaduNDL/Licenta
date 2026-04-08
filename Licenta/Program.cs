using Infrastructure.Audit;
using Licenta.Areas.Identity.Data;
using Licenta.Data;
using Licenta.Services;
using Licenta.Services.Ml;
using Licenta.Services.Prediction;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;
using System;
using System.IO;
using System.Security.Claims;
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

                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequiredUniqueChars = 1;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();

            builder.Services.RemoveAll<IPasswordValidator<ApplicationUser>>();
            builder.Services.AddScoped<IPasswordValidator<ApplicationUser>, DynamicPasswordValidator>();

            builder.Services.AddRazorPages();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

            builder.Services.AddDistributedMemoryCache();

            builder.Services.AddSession(o =>
            {
                o.Cookie.HttpOnly = true;
                o.Cookie.IsEssential = true;
                o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
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

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedHost;

                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IPdfService, PdfService>();

            builder.Services.AddSingleton<IPredictionTargetRegistry, PredictionTargetRegistry>();

            builder.Services.Configure<MlServiceOptions>(
                builder.Configuration.GetSection("MlServiceOptions"));

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

            app.UseForwardedHeaders();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseAuditMiddleware();

            app.MapRazorPages();

            app.MapHub<NotificationHub>("/hubs/notifications");

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
                            var logger = app.Services
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("StartupSeed");

                            logger.LogError(ex, "Startup seed failed");
                        }
                    });
                });
            }

            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                logger.LogInformation("Application started successfully");
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