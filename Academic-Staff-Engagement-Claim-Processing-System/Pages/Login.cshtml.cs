using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using LecturerModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public LoginModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your username and password.";
                return Page();
            }

            string username = Username.Trim();

            // ========================================================
            // 1. LECTURER
            // ========================================================

            var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.UserName == username);

            if (lecturer != null)
            {
                if (!lecturer.IsActive)
                {
                    ErrorMessage = "This account has been deactivated. Please contact the administrator.";
                    return Page();
                }

                if (lecturer.LockoutEndUtc.HasValue && lecturer.LockoutEndUtc.Value > DateTime.UtcNow)
                {
                    ErrorMessage = "This account is temporarily locked. Please try again later.";
                    return Page();
                }

                var hasher = new PasswordHasher<LecturerModel>();
                var result = hasher.VerifyHashedPassword(lecturer, lecturer.PasswordHash, Password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    lecturer.FailedLoginAttempts = 0;
                    lecturer.LockoutEndUtc = null;
                    lecturer.LastLoginUtc = DateTime.UtcNow;
                    lecturer.UpdatedAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await SignInAsync(lecturer.UserName, "Lecturer", lecturer.Id);

                    if (lecturer.MustChangePassword)
                        return RedirectToPage("/ChangePassword", new { username = lecturer.UserName });

                    return lecturer.Type == UserRole.PartTimeLecturer
                        ? RedirectToPage("/Lecturer/Part/Index")
                        : RedirectToPage("/Lecturer/Index");
                }

                lecturer.FailedLoginAttempts++;
                if (lecturer.FailedLoginAttempts >= 5)
                {
                    lecturer.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    lecturer.FailedLoginAttempts = 0;
                }
                lecturer.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            // ========================================================
            // 2. HOD
            // ========================================================

            var hod = await _context.Hods.FirstOrDefaultAsync(h => h.UserName == username);

            if (hod != null)
            {
                if (!hod.IsActive)
                {
                    ErrorMessage = "This account has been deactivated. Please contact the administrator.";
                    return Page();
                }

                if (hod.LockoutEndUtc.HasValue && hod.LockoutEndUtc.Value > DateTime.UtcNow)
                {
                    ErrorMessage = "This account is temporarily locked. Please try again later.";
                    return Page();
                }

                var hasher = new PasswordHasher<AdminAccount>();
                var result = hasher.VerifyHashedPassword(hod, hod.PasswordHash, Password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    hod.FailedLoginAttempts = 0;
                    hod.LockoutEndUtc = null;
                    hod.LastLoginUtc = DateTime.UtcNow;
                    hod.UpdatedAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await SignInAsync(hod.UserName, "HOD", hod.Id);
                    return RedirectToPage("/HOD/Index");
                }

                hod.FailedLoginAttempts++;
                if (hod.FailedLoginAttempts >= 5)
                {
                    hod.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    hod.FailedLoginAttempts = 0;
                }
                hod.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            // ========================================================
            // 3. DEAN
            // ========================================================

            var dean = await _context.Deans.FirstOrDefaultAsync(d => d.UserName == username);

            if (dean != null)
            {
                if (!dean.IsActive)
                {
                    ErrorMessage = "This account has been deactivated. Please contact the administrator.";
                    return Page();
                }

                if (dean.LockoutEndUtc.HasValue && dean.LockoutEndUtc.Value > DateTime.UtcNow)
                {
                    ErrorMessage = "This account is temporarily locked. Please try again later.";
                    return Page();
                }

                var hasher = new PasswordHasher<AdminAccount>();
                var result = hasher.VerifyHashedPassword(dean, dean.PasswordHash, Password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    dean.FailedLoginAttempts = 0;
                    dean.LockoutEndUtc = null;
                    dean.LastLoginUtc = DateTime.UtcNow;
                    dean.UpdatedAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await SignInAsync(dean.UserName, "Dean", dean.Id);
                    return RedirectToPage("/DEAN/Index");
                }

                dean.FailedLoginAttempts++;
                if (dean.FailedLoginAttempts >= 5)
                {
                    dean.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    dean.FailedLoginAttempts = 0;
                }
                dean.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            // ========================================================
            // 4. MANAGEMENT
            // ========================================================

            var management = await _context.ManagementAccounts.FirstOrDefaultAsync(m => m.UserName == username);

            if (management != null)
            {
                if (!management.IsActive)
                {
                    ErrorMessage = "This account has been deactivated. Please contact the administrator.";
                    return Page();
                }

                if (management.LockoutEndUtc.HasValue && management.LockoutEndUtc.Value > DateTime.UtcNow)
                {
                    ErrorMessage = "This account is temporarily locked. Please try again later.";
                    return Page();
                }

                var hasher = new PasswordHasher<AdminAccount>();
                var result = hasher.VerifyHashedPassword(management, management.PasswordHash, Password);

                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    management.FailedLoginAttempts = 0;
                    management.LockoutEndUtc = null;
                    management.LastLoginUtc = DateTime.UtcNow;
                    management.UpdatedAtUtc = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await SignInAsync(management.UserName, "Management", management.Id);
                    return RedirectToPage("/ManagementDashboard");
                }

                management.FailedLoginAttempts++;
                if (management.FailedLoginAttempts >= 5)
                {
                    management.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                    management.FailedLoginAttempts = 0;
                }
                management.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            // ========================================================
<<<<<<< HEAD
            // INVALID LOGIN
            // ========================================================

            ErrorMessage =
                "Invalid username or password.";

=======
            // NO MATCH
            // ========================================================

            ErrorMessage = "Invalid username or password.";
>>>>>>> cc560ac753fd5b307f26fa3af4c533512c572404
            return Page();
        }

        // ============================================================
        // COOKIE SIGN-IN
        // ============================================================

        private async Task SignInAsync(string username, string role, int userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("UserId", userId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false, // don't survive browser close — re-auth each session
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
        }
    }
}