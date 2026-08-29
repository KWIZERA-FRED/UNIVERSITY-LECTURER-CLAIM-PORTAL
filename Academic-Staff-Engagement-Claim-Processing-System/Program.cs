using System.Threading.RateLimiting;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Amazon.S3;


var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURATION
// ============================================================
// Disable reloadOnChange to prevent Linux inotify limit crashes on Render.
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: true,
        reloadOnChange: false)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: false);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

builder.Configuration.AddEnvironmentVariables();

// ============================================================
// DATABASE
// ============================================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine);
    }
});// ============================================================
// DATA PROTECTION — CLOUDFLARE R2
// ============================================================

var r2AccountId = builder.Configuration["R2:AccountId"];
var r2AccessKeyId = builder.Configuration["R2:AccessKeyId"];
var r2SecretAccessKey = builder.Configuration["R2:SecretAccessKey"];
var r2BucketName = builder.Configuration["R2:BucketName"];

if (string.IsNullOrWhiteSpace(r2AccountId) ||
    string.IsNullOrWhiteSpace(r2AccessKeyId) ||
    string.IsNullOrWhiteSpace(r2SecretAccessKey) ||
    string.IsNullOrWhiteSpace(r2BucketName))
{
    throw new InvalidOperationException(
        "Cloudflare R2 Data Protection configuration is missing.");
}

var r2Config = new AmazonS3Config
{
    ServiceURL = $"https://{r2AccountId}.r2.cloudflarestorage.com",
    ForcePathStyle = true,
    AuthenticationRegion = "auto"
};

var r2Client = new AmazonS3Client(
    r2AccessKeyId,
    r2SecretAccessKey,
    r2Config);

builder.Services.AddSingleton<IAmazonS3>(r2Client);

// Custom XML repository for Cloudflare R2.
// This avoids AWS streaming payloads that R2 does not support.
var r2Repository = new CloudflareR2XmlRepository(
    r2Client,
    r2BucketName);

builder.Services.AddDataProtection()
    .SetApplicationName("UnilakStaffClaimPortal")
    .AddKeyManagementOptions(options =>
    {
        options.XmlRepository = r2Repository;
    });

builder.Services.AddSingleton<GovernmentIdProtector>();
// ============================================================
// RATE LIMITING
// ============================================================
// ============================================================
// RATE LIMITING
// ============================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        Console.WriteLine(
            $"[RateLimiter] Rejected request from {context.HttpContext.Connection.RemoteIpAddress} to {context.HttpContext.Request.Path}");

        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync(
            "Too many attempts. Please wait a minute before trying again.",
            cancellationToken);
    };

    // Per-IP fixed window — each IP gets its own 5-attempts-per-minute
    // budget, so one person's retries (or one attacker) can't lock out
    // every other user of the login page.
    options.AddPolicy("login-policy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // General sliding window policy for application pages
    options.AddSlidingWindowLimiter(policyName: "general-policy", configureOptions: opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueLimit = 0;
    });
});

// ============================================================
// AUTHENTICATION
// ============================================================
builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // Hardened Cookie Security
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.Name = ".StaffPortal.Auth";
    });

// ============================================================
// AUTHORIZATION
// ============================================================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HOD", policy => policy.RequireRole("HOD"));
    options.AddPolicy("Dean", policy => policy.RequireRole("Dean"));
    options.AddPolicy("Lecturer", policy => policy.RequireRole("Lecturer"));
});

// ============================================================
// RAZOR PAGES
// ============================================================
builder.Services.AddRazorPages(options =>
{
    // Folder-level role requirements
    options.Conventions.AuthorizeFolder("/HOD", "HOD");
    options.Conventions.AuthorizeFolder("/DEAN", "Dean");
    options.Conventions.AuthorizeFolder("/Lecturer", "Lecturer");
    options.Conventions.AuthorizeFolder("/Shared");

    // RegisterUser allows initial bootstrap check in code
    options.Conventions.AllowAnonymousToPage("/HOD/RegisterUser");

    // Public pages — no login required
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Privacy");
    options.Conventions.AllowAnonymousToPage("/Error");
});

// ============================================================
// SESSION
// ============================================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = ".StaffPortal.Session";
});

// ============================================================
// SERVICES
// ============================================================
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuditLogger>();

// ============================================================
// BUILD APPLICATION
// ============================================================
var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// ============================================================
// TEMPLATE SEEDING
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await TemplateSeeder.SeedAsync(db);
}

// ============================================================
// HTTP REQUEST PIPELINE
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

// Rate Limiter must run directly after routing
app.UseRateLimiter();

// Session state configuration
app.UseSession();

// Authentication & Authorization Pipeline
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// RAZOR PAGES
// ============================================================
app.MapRazorPages();

app.Run();