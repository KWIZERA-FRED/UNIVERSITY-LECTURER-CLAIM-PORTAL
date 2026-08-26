using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
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
        reloadOnChange: false)
    .AddEnvironmentVariables();

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

builder.Services.AddDataProtection();

// ============================================================
// RAZOR PAGES
// ============================================================

builder.Services.AddRazorPages();

// ============================================================
// SESSION
// ============================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);

    options.Cookie.HttpOnly = true;

    options.Cookie.IsEssential = true;

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
// IMPORTANT: Session must be before MapRazorPages()
// ============================================================

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.Run();