using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public LoginModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // LOGIN FIELDS
        // ============================================================

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        // ============================================================
        // GET
        // ============================================================

        public void OnGet()
        {
        }

        // ============================================================
        // POST
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
            // 1. CHECK LECTURER ACCOUNTS
            // ========================================================

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.UserName == username);

            if (lecturer != null)
            {
                // ----------------------------------------------------
                // ACCOUNT ACTIVE?
                // ----------------------------------------------------

                if (!lecturer.IsActive)
                {
                    ErrorMessage =
                        "This account has been deactivated. Please contact the administrator.";

                    return Page();
                }

                // ----------------------------------------------------
                // ACCOUNT LOCKED?
                // ----------------------------------------------------

                if (lecturer.LockoutEndUtc.HasValue &&
                    lecturer.LockoutEndUtc.Value > DateTime.UtcNow)
                {
                    ErrorMessage =
                        "This account is temporarily locked. Please try again later.";

                    return Page();
                }

                // ----------------------------------------------------
                // VERIFY PASSWORD
                // ----------------------------------------------------

                var passwordHasher =
                    new PasswordHasher<LecturerModel>();

                var passwordResult =
                    passwordHasher.VerifyHashedPassword(
                        lecturer,
                        lecturer.PasswordHash,
                        Password);

                if (passwordResult ==
                        PasswordVerificationResult.Success ||
                    passwordResult ==
                        PasswordVerificationResult.SuccessRehashNeeded)
                {
                    // ------------------------------------------------
                    // RESET LOGIN SECURITY VALUES
                    // ------------------------------------------------

                    lecturer.FailedLoginAttempts = 0;
                    lecturer.LockoutEndUtc = null;
                    lecturer.LastLoginUtc = DateTime.UtcNow;
                    lecturer.UpdatedAtUtc = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    // ------------------------------------------------
                    // SESSION
                    // ------------------------------------------------

                    HttpContext.Session.SetString(
                        "Username",
                        lecturer.UserName);

                    HttpContext.Session.SetString(
                        "Role",
                        "Lecturer");

                    HttpContext.Session.SetString(
                        "LecturerId",
                        lecturer.Id.ToString());

                    // ------------------------------------------------
                    // FIRST LOGIN
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
                    // PART-TIME LECTURER
                    // ------------------------------------------------

                    if (lecturer.Type ==
                        UserRole.PartTimeLecturer)
                    {
                        return RedirectToPage(
                            "/Lecturer/Part/Index");
                    }

                    // ------------------------------------------------
                    // FULL-TIME LECTURER
                    // ------------------------------------------------

                    return RedirectToPage(
                        "/Lecturer/Index");
                }

                // ----------------------------------------------------
                // WRONG PASSWORD
                // ----------------------------------------------------

                lecturer.FailedLoginAttempts++;

                if (lecturer.FailedLoginAttempts >= 5)
                {
                    lecturer.LockoutEndUtc =
                        DateTime.UtcNow.AddMinutes(15);

                    lecturer.FailedLoginAttempts = 0;
                }

                lecturer.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                ErrorMessage =
                    "Invalid username or password.";

                return Page();
            }

            // ========================================================
            // 2. CHECK HOD ACCOUNTS
            // ========================================================

            var hod = await _context.Hods
                .FirstOrDefaultAsync(h =>
                    h.UserName == username);

            if (hod != null)
            {
                if (!hod.IsActive)
                {
                    ErrorMessage =
                        "This account has been deactivated. Please contact the administrator.";

                    return Page();
                }

                var passwordHasher =
                    new PasswordHasher<AdminAccount>();

                var passwordResult =
                    passwordHasher.VerifyHashedPassword(
                        hod,
                        hod.PasswordHash,
                        Password);

                if (passwordResult ==
                        PasswordVerificationResult.Success ||
                    passwordResult ==
                        PasswordVerificationResult.SuccessRehashNeeded)
                {
                    hod.FailedLoginAttempts = 0;
                    hod.LockoutEndUtc = null;
                    hod.LastLoginUtc = DateTime.UtcNow;
                    hod.UpdatedAtUtc = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    HttpContext.Session.SetString(
                        "Username",
                        hod.UserName);

                    HttpContext.Session.SetString(
                        "Role",
                        "HOD");

                    HttpContext.Session.SetString(
                        "UserId",
                        hod.Id.ToString());

                    return RedirectToPage(
                        "/HOD/Index");
                }

                ErrorMessage =
                    "Invalid username or password.";

                return Page();
            }

            // ========================================================
            // 3. CHECK DEAN ACCOUNTS
            // ========================================================

            var dean = await _context.Deans
                .FirstOrDefaultAsync(d =>
                    d.UserName == username);

            if (dean != null)
            {
                if (!dean.IsActive)
                {
                    ErrorMessage =
                        "This account has been deactivated. Please contact the administrator.";

                    return Page();
                }

                var passwordHasher =
                    new PasswordHasher<AdminAccount>();

                var passwordResult =
                    passwordHasher.VerifyHashedPassword(
                        dean,
                        dean.PasswordHash,
                        Password);

                if (passwordResult ==
                        PasswordVerificationResult.Success ||
                    passwordResult ==
                        PasswordVerificationResult.SuccessRehashNeeded)
                {
                    dean.FailedLoginAttempts = 0;
                    dean.LockoutEndUtc = null;
                    dean.LastLoginUtc = DateTime.UtcNow;
                    dean.UpdatedAtUtc = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    HttpContext.Session.SetString(
                        "Username",
                        dean.UserName);

                    HttpContext.Session.SetString(
                        "Role",
                        "Dean");

                    HttpContext.Session.SetString(
                        "UserId",
                        dean.Id.ToString());

                    return RedirectToPage(
                        "/DEAN/Index");
                }

                ErrorMessage =
                    "Invalid username or password.";

                return Page();
            }

            // ========================================================
            // 4. CHECK MANAGEMENT ACCOUNTS
            // ========================================================

            var management =
                await _context.ManagementAccounts
                    .FirstOrDefaultAsync(m =>
                        m.UserName == username);

            if (management != null)
            {
                if (!management.IsActive)
                {
                    ErrorMessage =
                        "This account has been deactivated. Please contact the administrator.";

                    return Page();
                }

                var passwordHasher =
                    new PasswordHasher<AdminAccount>();

                var passwordResult =
                    passwordHasher.VerifyHashedPassword(
                        management,
                        management.PasswordHash,
                        Password);

                if (passwordResult ==
                        PasswordVerificationResult.Success ||
                    passwordResult ==
                        PasswordVerificationResult.SuccessRehashNeeded)
                {
                    management.FailedLoginAttempts = 0;
                    management.LockoutEndUtc = null;
                    management.LastLoginUtc = DateTime.UtcNow;
                    management.UpdatedAtUtc = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    HttpContext.Session.SetString(
                        "Username",
                        management.UserName);

                    HttpContext.Session.SetString(
                        "Role",
                        "Management");

                    HttpContext.Session.SetString(
                        "UserId",
                        management.Id.ToString());

                    return RedirectToPage(
                        "/ManagementDashboard");
                }

                ErrorMessage =
                    "Invalid username or password.";

                return Page();
            }

            // ========================================================
            // 5. OLD TEST ACCOUNTS
            // ========================================================

            if (username == "Ali" && Password == "1234")
            {
                HttpContext.Session.SetString(
                    "Username",
                    username);

                HttpContext.Session.SetString(
                    "Role",
                    "Lecturer");

                return RedirectToPage(
                    "/Lecturer/Index");
            }

            if (username == "Malaz" && Password == "1234")
            {
                HttpContext.Session.SetString(
                    "Username",
                    username);

                HttpContext.Session.SetString(
                    "Role",
                    "HOD");

                return RedirectToPage(
                    "/HOD/Index");
            }

            if (username == "Fred" && Password == "1234")
            {
                HttpContext.Session.SetString(
                    "Username",
                    username);

                HttpContext.Session.SetString(
                    "Role",
                    "Dean");

                return RedirectToPage(
                    "/DEAN/Index");
            }

            if (username == "management" && Password == "1234")
            {
                HttpContext.Session.SetString(
                    "Username",
                    username);

                HttpContext.Session.SetString(
                    "Role",
                    "Management");

                return RedirectToPage(
                    "/ManagementDashboard");
            }

            if (username == "Reem" && Password == "1234")
            {
                HttpContext.Session.SetString(
                    "Username",
                    username);

                HttpContext.Session.SetString(
                    "Role",
                    "Lecturer");

                return RedirectToPage(
                    "/Lecturer/Part/Index");
            }

            // ========================================================
            // INVALID LOGIN
            // ========================================================

            ErrorMessage =
                "Invalid username or password.";

            return Page();
        }
    }
}