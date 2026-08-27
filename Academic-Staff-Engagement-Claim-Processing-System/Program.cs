using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURATION
// ============================================================

// Disable reloadOnChange to prevent Linux inotify limit crashes
// on Render.
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
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
    ));

// ============================================================
// DATA PROTECTION
// ============================================================

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyPath"] ?? "keys"))
    .SetApplicationName("UnilakStaffClaimPortal");

builder.Services.AddSingleton<GovernmentIdProtector>();

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
    options.Conventions.AuthorizeFolder("/Shared"); // any authenticated role, no specific one

    // RegisterUser has its own bootstrap-aware check in code (allows the
    // very first HOD to self-register when none exist yet) — so it must
    // opt OUT of the blanket /HOD folder policy, or nobody could ever
    // reach it to create that first account.
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
    options.IdleTimeout =
        TimeSpan.FromHours(8);

    options.Cookie.HttpOnly =
        true;

    options.Cookie.IsEssential =
        true;

    options.Cookie.Name =
        ".StaffPortal.Session";
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
// HTTP REQUEST PIPELINE
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    app.UseHsts();
}

// HTTPS temporarily disabled
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// ============================================================
// SESSION
// ============================================================

app.UseSession();

// ============================================================
// AUTHENTICATION
// Must run before Authorization
// ============================================================

app.UseAuthentication();

// ============================================================
// AUTHORIZATION
// ============================================================

app.UseAuthorization();

// ============================================================
// RAZOR PAGES
// ============================================================

app.MapRazorPages();

app.Run();