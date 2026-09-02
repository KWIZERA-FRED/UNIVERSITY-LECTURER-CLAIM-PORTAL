using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// Type aliases to avoid namespace/type collisions
using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

using SecurityClaim = System.Security.Claims.Claim;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages
{
    [EnableRateLimiting("login-policy")]
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;

        public LoginModel(
            ApplicationDbContext context,
            AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        // ============================================================
        // LOGIN
        // ============================================================

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage =
                    "Please enter your username and password.";

                return Page();
            }

            string username = Username.Trim();

            // ========================================================
            // 1. CHECK LECTURER
            // ========================================================

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.UserName == username);

            if (lecturer != null)
            {
                return await ProcessUserLoginAsync(
                    lecturer,
                    "Lecturer",
                    lecturer.Id,
                    new PasswordHasher<LecturerModel>(),

                    onSuccess: async () =>
                    {
                        // ------------------------------------------------
                        // First-login password change
                        // ------------------------------------------------

                        if (lecturer.MustChangePassword)
                        {
                            return RedirectToPage(
                                "/ChangePassword",
                                new
                                {
                                    username = lecturer.UserName
                                });
                        }

                        // ------------------------------------------------
                        // Lecturer dashboard
                        // ------------------------------------------------

                        return RedirectToPage(
                            "/Lecturer/Index");
                    });
            }

            // ============================================================
            // 2. CHECK HOD
            // ============================================================

            var hod = await _context.Hods
                .FirstOrDefaultAsync(h =>
                    h.UserName == username);

            if (hod != null)
            {
                return await ProcessUserLoginAsync(
                    hod,
                    "HOD",
                    hod.Id,
                    new PasswordHasher<AdminAccount>(),

                    onSuccess: () =>
                        Task.FromResult<IActionResult>(
                            RedirectToPage("/HOD/Index")));
            }

            // ============================================================
            // 3. CHECK DEAN
            // ============================================================

            var dean = await _context.Deans
                .FirstOrDefaultAsync(d =>
                    d.UserName == username);

            if (dean != null)
            {
                return await ProcessUserLoginAsync(
                    dean,
                    "Dean",
                    dean.Id,
                    new PasswordHasher<AdminAccount>(),

                    onSuccess: () =>
                        Task.FromResult<IActionResult>(
                            RedirectToPage("/DEAN/Index")));
            }

            // ============================================================
            // 4. CHECK MANAGEMENT
            // ============================================================

            var management = await _context.ManagementAccounts
                .FirstOrDefaultAsync(m =>
                    m.UserName == username);

            if (management != null)
            {
                return await ProcessUserLoginAsync(
                    management,
                    "Management",
                    management.Id,
                    new PasswordHasher<AdminAccount>(),

                    onSuccess: () =>
                        Task.FromResult<IActionResult>(
                            RedirectToPage("/ManagementDashboard")));
            }

            // ============================================================
            // INVALID USERNAME
            // ============================================================

            await _auditLogger.LogAsync(
                AuditAction.LoginFailed,
                username,
                "Unknown",
                actorId: null,
                entityType: null,
                entityId: null,
                details: "Username not found",
                ipAddress:
                    HttpContext.Connection.RemoteIpAddress?.ToString());

            ErrorMessage =
                "Invalid username or password.";

            return Page();
        }

        // ============================================================
        // PROCESS LOGIN
        // ============================================================

        private async Task<IActionResult> ProcessUserLoginAsync<TUser>(
            TUser user,
            string role,
            int userId,
            IPasswordHasher<TUser> hasher,
            Func<Task<IActionResult>> onSuccess)
            where TUser : class
        {
            dynamic entity = user;

            string? ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            // ========================================================
            // ACCOUNT STATUS
            // ========================================================

            if (!entity.IsActive)
            {
                await _auditLogger.LogAsync(
                    AuditAction.LoginFailed,
                    entity.UserName,
                    role,
                    userId,
                    role,
                    userId,
                    "Attempted login on deactivated account",
                    ipAddress);

                ErrorMessage =
                    "This account has been deactivated. " +
                    "Please contact the administrator.";

                return Page();
            }

            // ========================================================
            // LOCKOUT
            // ========================================================

            if (entity.LockoutEndUtc != null &&
                entity.LockoutEndUtc > DateTime.UtcNow)
            {
                await _auditLogger.LogAsync(
                    AuditAction.LoginFailed,
                    entity.UserName,
                    role,
                    userId,
                    role,
                    userId,
                    "Attempted login while account locked",
                    ipAddress);

                ErrorMessage =
                    "This account is temporarily locked. " +
                    "Please try again later.";

                return Page();
            }

            // ========================================================
            // VERIFY PASSWORD
            // ========================================================

            var verificationResult =
                hasher.VerifyHashedPassword(
                    user,
                    entity.PasswordHash,
                    Password);

            // ========================================================
            // SUCCESSFUL LOGIN
            // ========================================================

            if (verificationResult ==
                    PasswordVerificationResult.Success ||
                verificationResult ==
                    PasswordVerificationResult.SuccessRehashNeeded)
            {
                // ----------------------------------------------------
                // Rehash password if required
                // ----------------------------------------------------

                if (verificationResult ==
                    PasswordVerificationResult.SuccessRehashNeeded)
                {
                    entity.PasswordHash =
                        hasher.HashPassword(
                            user,
                            Password);
                }

                // ----------------------------------------------------
                // Reset login security information
                // ----------------------------------------------------

                entity.FailedLoginAttempts = 0;
                entity.LockoutEndUtc = null;
                entity.LastLoginUtc = DateTime.UtcNow;
                entity.UpdatedAtUtc = DateTime.UtcNow;

                // ----------------------------------------------------
                // Save changes
                // ----------------------------------------------------

                bool saved =
                    await SaveLoginChangesWithConcurrencyRetryAsync(
                        entity);

                if (!saved)
                {
                    ErrorMessage =
                        "Your login could not be completed because " +
                        "the account was updated by another request. " +
                        "Please try again.";

                    return Page();
                }

                // ----------------------------------------------------
                // Audit successful login
                // ----------------------------------------------------

                await _auditLogger.LogAsync(
                    AuditAction.LoginSucceeded,
                    entity.UserName,
                    role,
                    userId,
                    role,
                    userId,
                    null,
                    ipAddress);

                // ----------------------------------------------------
                // CREATE AUTHENTICATION COOKIE
                // ----------------------------------------------------

                await SignInAsync(
                    entity.UserName,
                    role,
                    userId);

                // ----------------------------------------------------
                // REDIRECT
                // ----------------------------------------------------

                return await onSuccess();
            }

            // ========================================================
            // FAILED PASSWORD
            // ========================================================

            entity.FailedLoginAttempts++;

            bool justLockedOut = false;

            if (entity.FailedLoginAttempts >= 5)
            {
                entity.LockoutEndUtc =
                    DateTime.UtcNow.AddMinutes(15);

                entity.FailedLoginAttempts = 0;

                justLockedOut = true;
            }

            entity.UpdatedAtUtc = DateTime.UtcNow;

            // --------------------------------------------------------
            // Save failed login attempt
            // --------------------------------------------------------

            bool failedAttemptSaved =
                await SaveLoginChangesWithConcurrencyRetryAsync(
                    entity);

            if (!failedAttemptSaved)
            {
                ErrorMessage =
                    "Invalid username or password.";

                return Page();
            }

            // --------------------------------------------------------
            // Audit failed login
            // --------------------------------------------------------

            await _auditLogger.LogAsync(
                justLockedOut
                    ? AuditAction.AccountLockedOut
                    : AuditAction.LoginFailed,

                entity.UserName,
                role,
                userId,
                role,
                userId,

                justLockedOut
                    ? "Account locked after 5 failed attempts"
                    : "Invalid password",

                ipAddress);

            ErrorMessage =
                "Invalid username or password.";

            return Page();
        }

        // ============================================================
        // SAVE LOGIN CHANGES WITH CONCURRENCY RETRY
        // ============================================================

        private async Task<bool>
            SaveLoginChangesWithConcurrencyRetryAsync(
                object user)
        {
            const int maxAttempts = 2;

            for (int attempt = 1;
                 attempt <= maxAttempts;
                 attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync();

                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (attempt >= maxAttempts)
                    {
                        return false;
                    }

                    foreach (var entry in _context.ChangeTracker
                        .Entries()
                        .Where(e => e.Entity == user))
                    {
                        var databaseValues =
                            await entry.GetDatabaseValuesAsync();

                        if (databaseValues == null)
                        {
                            return false;
                        }

                        // Preserve our changes
                        var currentValues =
                            entry.CurrentValues.Clone();

                        // Refresh original database values
                        entry.OriginalValues
                            .SetValues(databaseValues);

                        // Restore our changes
                        entry.CurrentValues
                            .SetValues(currentValues);

                        // Tell EF to update again
                        entry.State =
                            EntityState.Modified;
                    }
                }
            }

            return false;
        }

        // ============================================================
        // SIGN IN
        // ============================================================

        private async Task SignInAsync(
            string username,
            string role,
            int userId)
        {
            var claims = new List<SecurityClaim>
            {
                new SecurityClaim(
                    ClaimTypes.Name,
                    username),

                new SecurityClaim(
                    ClaimTypes.Role,
                    role),

                new SecurityClaim(
                    "UserId",
                    userId.ToString())
            };

            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,

                    ExpiresUtc =
                        DateTimeOffset.UtcNow
                            .AddHours(8)
                });
        }
    }
}