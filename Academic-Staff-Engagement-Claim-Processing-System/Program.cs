using System.Threading.RateLimiting;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using Amazon.S3;

using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// ============================================================
// CONFIGURATION
// ============================================================

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
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);

            sqlOptions.CommandTimeout(60);
        });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.LogTo(Console.WriteLine);
    }
});

// ============================================================
// DATA PROTECTION — CLOUDFLARE R2
// ============================================================

var r2AccountId =
    builder.Configuration["R2:AccountId"];

var r2AccessKeyId =
    builder.Configuration["R2:AccessKeyId"];

var r2SecretAccessKey =
    builder.Configuration["R2:SecretAccessKey"];

var r2BucketName =
    builder.Configuration["R2:BucketName"];

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
    ServiceURL =
        $"https://{r2AccountId}.r2.cloudflarestorage.com",

    ForcePathStyle = true,

    AuthenticationRegion = "auto"
};

var r2Client = new AmazonS3Client(
    r2AccessKeyId,
    r2SecretAccessKey,
    r2Config);

builder.Services.AddSingleton<IAmazonS3>(
    r2Client);

var r2Repository =
    new CloudflareR2XmlRepository(
        r2Client,
        r2BucketName);

builder.Services.AddDataProtection()
    .SetApplicationName(
        "UnilakStaffClaimPortal")
    .AddKeyManagementOptions(options =>
    {
        options.XmlRepository =
            r2Repository;
    });

builder.Services.AddSingleton<GovernmentIdProtector>();

builder.Services.AddScoped<IMisAttendanceService, MisAttendanceService>();

// ============================================================
// RATE LIMITING
// ============================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected =
        async (context, cancellationToken) =>
        {
            Console.WriteLine(
                $"[RateLimiter] Rejected request from " +
                $"{context.HttpContext.Connection.RemoteIpAddress} " +
                $"to {context.HttpContext.Request.Path}");

            context.HttpContext.Response.ContentType =
                "text/plain";

            await context.HttpContext.Response.WriteAsync(
                "Too many attempts. Please wait a minute before trying again.",
                cancellationToken);
        };

    // ========================================================
    // LOGIN RATE LIMIT
    // ========================================================

    options.AddPolicy(
        "login-policy",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst,

                        QueueLimit = 0
                    }));

    // ========================================================
    // PUBLIC CLAIM DOCUMENTS RATE LIMIT
    // ========================================================
    //
    // The QR verification page is reachable without signing in,
    // so cap how often one client can hit it. Page views and
    // PDF generation share the same budget.
    // ========================================================

    options.AddPolicy(
        "public-documents-policy",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst,

                        QueueLimit = 0
                    }));

    // ========================================================
    // GENERAL APPLICATION RATE LIMIT
    // ========================================================

    options.AddSlidingWindowLimiter(
        policyName: "general-policy",
        configureOptions: opt =>
        {
            opt.PermitLimit = 100;

            opt.Window =
                TimeSpan.FromMinutes(1);

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

        options.AccessDeniedPath =
            "/AccessDenied";

        options.ExpireTimeSpan =
            TimeSpan.FromHours(8);

        options.SlidingExpiration = true;

        options.Cookie.HttpOnly = true;

        options.Cookie.SameSite =
            SameSiteMode.Strict;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.Cookie.Name =
            ".StaffPortal.Auth";
    });

// ============================================================
// AUTHORIZATION
// ============================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "HOD",
        policy => policy.RequireRole("HOD"));

    options.AddPolicy(
        "Dean",
        policy => policy.RequireRole("Dean"));

    options.AddPolicy(
        "Lecturer",
        policy => policy.RequireRole("Lecturer"));

    options.AddPolicy(
        "Management",
        policy => policy.RequireRole("Management"));
});

// ============================================================
// RAZOR PAGES
// ============================================================

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder(
        "/HOD",
        "HOD");

    options.Conventions.AuthorizeFolder(
        "/DEAN",
        "Dean");

    options.Conventions.AuthorizeFolder(
        "/Lecturer",
        "Lecturer");

    options.Conventions.AuthorizeFolder(
        "/Management",
        "Management");

    options.Conventions.AuthorizeFolder(
        "/Shared");

    options.Conventions.AllowAnonymousToPage(
        "/DEAN/RegisterUser");

    options.Conventions.AllowAnonymousToPage(
        "/Logout");

    options.Conventions.AllowAnonymousToPage(
        "/Login");

    options.Conventions.AllowAnonymousToPage(
        "/Index");

    options.Conventions.AllowAnonymousToPage(
        "/Privacy");

    options.Conventions.AllowAnonymousToPage(
        "/Error");

    options.Conventions.AllowAnonymousToPage(
        "/Public/ClaimDocuments");
});

// ============================================================
// SESSION
// ============================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout =
        TimeSpan.FromHours(8);

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;

    options.Cookie.SameSite =
        SameSiteMode.Strict;

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;

    options.Cookie.Name =
        ".StaffPortal.Session";
});

// ============================================================
// SERVICES
// ============================================================

builder.Services.AddScoped<EmailService>();

builder.Services.AddScoped<AuditLogger>();

builder.Services.AddScoped<AccountRegistrationService>();

builder.Services.AddScoped<ContractSigningService>();

builder.Services.AddScoped<MarksSigningService>();

builder.Services.AddScoped<ClaimSigningService>();

builder.Services.AddScoped<ClaimSubmissionService>();

builder.Services.AddScoped<OfficialDocumentService>();

builder.Services.AddScoped<IFileStorageService, SqlFileStorageService>();

// Cloudflare R2 signature storage
builder.Services.AddScoped<CloudflareR2SignatureStorageService>();


// ============================================================
// BUILD APPLICATION
// ============================================================

var app = builder.Build();

// ============================================================
// FORWARDED HEADERS
// ============================================================

// Render terminates TLS and forwards traffic from its own proxy, whose
// address is not loopback. By default ASP.NET Core only trusts loopback
// proxies and would ignore X-Forwarded-For / X-Forwarded-Proto, so every
// visitor would share the proxy's IP (breaking the per-IP rate limiters)
// and Request.Scheme would be "http" (giving http:// QR links).
//
// Clearing the trusted lists is safe here only because the container is
// reachable solely through Render's proxy. ForwardLimit stays at 1, so
// only the entry appended by that proxy is used; anything a client puts
// earlier in the header is ignored.
var forwardedHeadersOptions =
    new ForwardedHeadersOptions
    {
        ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto
    };

forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

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

app.UseRateLimiter();

app.UseSession();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Cache-Control"))
            {
                context.Response.Headers["Cache-Control"] =
                    "no-cache, no-store, must-revalidate, max-age=0";

                context.Response.Headers["Pragma"] =
                    "no-cache";

                context.Response.Headers["Expires"] =
                    "0";
            }

            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();