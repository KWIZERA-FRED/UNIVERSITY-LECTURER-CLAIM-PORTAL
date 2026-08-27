using System.Threading.RateLimiting;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

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

    options.EnableDetailedErrors();

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine);
    }
});
// ============================================================
// DATA PROTECTION
// ============================================================
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyPath"] ?? "keys"))
    .SetApplicationName("UnilakStaffClaimPortal");

builder.Services.AddSingleton<GovernmentIdProtector>();

// ============================================================
// RATE LIMITING
// ============================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Strict policy for login attempts (5 attempts per minute per remote IP)
    options.AddFixedWindowLimiter(policyName: "login-policy", configureOptions: opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = ".StaffPortal.Session";
});

// ============================================================
// SERVICES
// ============================================================
builder.Services.AddScoped<EmailService>();

// ============================================================
// BUILD APPLICATION
// ============================================================
var app = builder.Build();

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