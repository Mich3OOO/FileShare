using FileShare.Api.Data;
using FileShare.Api.Endpoints;
using FileShare.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Allow uploads of any size.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=fileshare;Username=fileshare;Password=fileshare";

var storageRoot = builder.Configuration["Storage:RootPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IFileStorage>(_ => new FileStorage(storageRoot));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CodeService>();
builder.Services.AddHostedService<CleanupService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(AuthEndpoints.ConfigureCookie);
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("uploads", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Incremental schema patches for databases created by older versions.
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE \"access_codes\" ADD COLUMN IF NOT EXISTS \"Note\" character varying(200) NULL");

    var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
    await auth.EnsureAdminAsync(
        app.Configuration["Admin:Username"] ?? "admin",
        app.Configuration["Admin:Password"] ?? "admin");
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

// "/" serves the guest upload page by default.
var defaultFiles = new DefaultFilesOptions();
defaultFiles.DefaultFileNames.Clear();
defaultFiles.DefaultFileNames.Add("upload.html");
app.UseDefaultFiles(defaultFiles);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAuthEndpoints();
app.MapFileEndpoints();
app.MapCodeEndpoints();
app.MapGuestEndpoints();
app.MapDownloadEndpoints();

app.Run();