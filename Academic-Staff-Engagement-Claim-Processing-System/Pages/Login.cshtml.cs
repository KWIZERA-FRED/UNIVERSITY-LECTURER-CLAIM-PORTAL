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

// Type aliases to eliminate namespace collisions
using LecturerModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;
using SecurityClaim = System.Security.Claims.Claim;

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

            // 1. Check Lecturer
            var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.UserName == username);
            if (lecturer != null)
            {
                return await ProcessUserLoginAsync(
                    lecturer,
                    "Lecturer",
                    lecturer.Id,
                    new PasswordHasher<LecturerModel>(),
                    onSuccess: async () =>
                    {
                        if (lecturer.MustChangePassword)
                        {
                            return RedirectToPage("/ChangePassword", new { username = lecturer.UserName });
                        }

                        if (lecturer.Type == UserRole.PartTimeLecturer)
                        {
                            return RedirectToPage("/Lecturer/Part/Index");
                        }

                        return RedirectToPage("/Lecturer/Index");
                    });
            }

            // 2. Check HOD
            var hod = await _context.Hods.FirstOrDefaultAsync(h => h.UserName == username);
            if (hod != null)
            {
                return await ProcessUserLoginAsync(
                    hod,
                    "HOD",
                    hod.Id,
                    new PasswordHasher<AdminAccount>(),
                    onSuccess: () => Task.FromResult<IActionResult>(RedirectToPage("/HOD/Index")));
            }

            // 3. Check Dean
            var dean = await _context.Deans.FirstOrDefaultAsync(d => d.UserName == username);
            if (dean != null)
            {
                return await ProcessUserLoginAsync(
                    dean,
                    "Dean",
                    dean.Id,
                    new PasswordHasher<AdminAccount>(),
                    onSuccess: () => Task.FromResult<IActionResult>(RedirectToPage("/DEAN/Index")));
            }

            // 4. Check Management
            var management = await _context.ManagementAccounts.FirstOrDefaultAsync(m => m.UserName == username);
            if (management != null)
            {
                return await ProcessUserLoginAsync(
                    management,
                    "Management",
                    management.Id,
                    new PasswordHasher<AdminAccount>(),
                    onSuccess: () => Task.FromResult<IActionResult>(RedirectToPage("/ManagementDashboard")));
            }

            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        private async Task<IActionResult> ProcessUserLoginAsync<TUser>(
            TUser user,
            string role,
            int userId,
            IPasswordHasher<TUser> hasher,
            Func<Task<IActionResult>> onSuccess) where TUser : class
        {
            dynamic entity = user;

            if (!entity.IsActive)
            {
                ErrorMessage = "This account has been deactivated. Please contact the administrator.";
                return Page();
            }

            if (entity.LockoutEndUtc != null && entity.LockoutEndUtc > DateTime.UtcNow)
            {
                ErrorMessage = "This account is temporarily locked. Please try again later.";
                return Page();
            }

            var verificationResult = hasher.VerifyHashedPassword(user, entity.PasswordHash, Password);

            if (verificationResult == PasswordVerificationResult.Success ||
                verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    entity.PasswordHash = hasher.HashPassword(user, Password);
                }

                entity.FailedLoginAttempts = 0;
                entity.LockoutEndUtc = null;
                entity.LastLoginUtc = DateTime.UtcNow;
                entity.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await SignInAsync(entity.UserName, role, userId);

                return await onSuccess();
            }

            // Handle Failed Attempt
            entity.FailedLoginAttempts++;
            if (entity.FailedLoginAttempts >= 5)
            {
                entity.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                entity.FailedLoginAttempts = 0;
            }

            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        private async Task SignInAsync(string username, string role, int userId)
        {
            // Explicitly using SecurityClaim alias to prevent CS0104 collision with database entity Claim
            var claims = new List<SecurityClaim>
            {
                new SecurityClaim(ClaimTypes.Name, username),
                new SecurityClaim(ClaimTypes.Role, role),
                new SecurityClaim("UserId", userId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
        }
    }
}