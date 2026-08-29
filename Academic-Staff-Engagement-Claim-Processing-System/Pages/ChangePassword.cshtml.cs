using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages
{
    // Requires an authenticated Lecturer — this page sits outside the
    // /Lecturer folder convention in Program.cs, so without this
    // attribute it would have no authorization at all.
    [Authorize(Roles = "Lecturer")]
    [EnableRateLimiting("login-policy")]
    public class ChangePasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;

        public ChangePasswordModel(
            ApplicationDbContext context,
            AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        [BindProperty]
        public string CurrentPassword { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public string? DisplayUsername { get; private set; }

        public string? ErrorMessage { get; set; }

        public bool IsLockedOut { get; private set; }

        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            var lecturer = await GetCurrentLecturerAsync();

            if (lecturer == null)
            {
                return RedirectToPage("/Login");
            }

            // Already changed — don't let this page be revisited as a
            // general "change my password" screen; that's a separate,
            // unbuilt feature, and MustChangePassword is specifically
            // the "you're on an admin-issued temp password" flag.
            if (!lecturer.MustChangePassword)
            {
                return RedirectToPage(DashboardPathFor(lecturer));
            }

            if (IsCurrentlyLockedOut(lecturer))
            {
                IsLockedOut = true;
                ErrorMessage =
                    "Too many incorrect attempts. Please try again later.";
            }

            DisplayUsername = lecturer.UserName;

            return Page();
        }

        // ============================================================
        // POST
        // ============================================================

        public async Task<IActionResult> OnPostAsync()
        {
            var lecturer = await GetCurrentLecturerAsync();

            if (lecturer == null)
            {
                return RedirectToPage("/Login");
            }

            DisplayUsername = lecturer.UserName;

            if (!lecturer.MustChangePassword)
            {
                return RedirectToPage(DashboardPathFor(lecturer));
            }

            // --------------------------------------------------------
            // LOCKOUT
            // --------------------------------------------------------
            // Shares FailedLoginAttempts / LockoutEndUtc with Login —
            // see the note above the class. This is the same
            // credential being guessed, so it's the same counter.

            if (IsCurrentlyLockedOut(lecturer))
            {
                IsLockedOut = true;
                ErrorMessage =
                    "Too many incorrect attempts. Please try again later.";

                return Page();
            }

            if (string.IsNullOrWhiteSpace(CurrentPassword) ||
                string.IsNullOrWhiteSpace(NewPassword) ||
                string.IsNullOrWhiteSpace(ConfirmNewPassword))
            {
                ErrorMessage = "Please fill in every field.";
                return Page();
            }

            var hasher = new PasswordHasher<LecturerModel>();

            var verification = hasher.VerifyHashedPassword(
                lecturer,
                lecturer.PasswordHash,
                CurrentPassword);

            if (verification == PasswordVerificationResult.Failed)
            {
                await RecordFailedAttemptAsync(lecturer);

                ErrorMessage = "Current password is incorrect.";
                return Page();
            }

            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return Page();
            }

            string? passwordError =
                ValidateNewPassword(NewPassword, lecturer, hasher);

            if (passwordError != null)
            {
                ErrorMessage = passwordError;
                return Page();
            }

            // --------------------------------------------------------
            // APPLY THE CHANGE
            // --------------------------------------------------------

            string newHash = hasher.HashPassword(lecturer, NewPassword);

            lecturer.SetPasswordHash(newHash);
            lecturer.MustChangePassword = false;
            lecturer.FailedLoginAttempts = 0;
            lecturer.LockoutEndUtc = null;
            lecturer.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ErrorMessage =
                    "This account was updated elsewhere. Please try again.";

                return Page();
            }

            await _auditLogger.LogAsync(
                AuditAction.PasswordChanged,
                lecturer.UserName,
                "Lecturer",
                lecturer.Id,
                "Lecturer",
                lecturer.Id,
                "Temporary password changed on first login",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return RedirectToPage(DashboardPathFor(lecturer));
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private async Task<LecturerModel?> GetCurrentLecturerAsync()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int userId);

            if (userId <= 0)
            {
                return null;
            }

            return await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == userId);
        }

        private static bool IsCurrentlyLockedOut(LecturerModel lecturer)
        {
            return lecturer.LockoutEndUtc != null &&
                   lecturer.LockoutEndUtc > DateTime.UtcNow;
        }

        private async Task RecordFailedAttemptAsync(LecturerModel lecturer)
        {
            lecturer.FailedLoginAttempts++;

            bool justLockedOut = false;

            if (lecturer.FailedLoginAttempts >= 5)
            {
                lecturer.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                lecturer.FailedLoginAttempts = 0;
                justLockedOut = true;
            }

            lecturer.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Best-effort — losing a lockout increment to a rare
                // concurrency clash isn't worth failing the request.
            }

            string? ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            await _auditLogger.LogAsync(
                justLockedOut
                    ? AuditAction.AccountLockedOut
                    : AuditAction.LoginFailed,
                lecturer.UserName,
                "Lecturer",
                lecturer.Id,
                "Lecturer",
                lecturer.Id,
                justLockedOut
                    ? "Account locked after 5 failed attempts on change-password"
                    : "Invalid current password on change-password",
                ipAddress);

            if (justLockedOut)
            {
                IsLockedOut = true;
            }
        }

        private static string? ValidateNewPassword(
            string newPassword,
            LecturerModel lecturer,
            PasswordHasher<LecturerModel> hasher)
        {
            if (newPassword.Length < 10)
            {
                return "New password must be at least 10 characters long.";
            }

            bool hasUpper = newPassword.Any(char.IsUpper);
            bool hasLower = newPassword.Any(char.IsLower);
            bool hasDigit = newPassword.Any(char.IsDigit);
            bool hasSymbol = newPassword.Any(c => !char.IsLetterOrDigit(c));

            if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
            {
                return "New password must include an uppercase letter, a lowercase letter, a number, and a symbol.";
            }

            var sameAsCurrent = hasher.VerifyHashedPassword(
                lecturer,
                lecturer.PasswordHash,
                newPassword);

            if (sameAsCurrent == PasswordVerificationResult.Success ||
                sameAsCurrent == PasswordVerificationResult.SuccessRehashNeeded)
            {
                return "New password must be different from your current password.";
            }

            return null;
        }

        private static string DashboardPathFor(LecturerModel lecturer)
        {
            return lecturer.Type == UserRole.PartTimeLecturer
                ? "/Lecturer/Part/Index"
                : "/Lecturer/Index";
        }
    }
}