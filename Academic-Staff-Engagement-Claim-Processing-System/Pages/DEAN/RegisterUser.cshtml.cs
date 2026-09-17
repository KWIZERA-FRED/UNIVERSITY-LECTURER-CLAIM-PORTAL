using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN
{
    // The Dean is the bootstrap role: the very first account in
    // the system is a Dean, created here anonymously.
    //
    // Once a Dean exists, this page requires an authenticated Dean.
    //
    // A Dean's account-creation privilege is HOD + Management only.
    // Lecturer accounts are created by an HOD, and another Dean
    // account cannot be created from this page.
    public class RegisterUserModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountRegistrationService _registrationService;
        private readonly AuditLogger _auditLogger;

        public RegisterUserModel(
            ApplicationDbContext context,
            AccountRegistrationService registrationService,
            AuditLogger auditLogger)
        {
            _context = context;
            _registrationService = registrationService;
            _auditLogger = auditLogger;
        }


        [BindProperty]
        public string Name { get; set; } = string.Empty;


        [BindProperty]
        public string Email { get; set; } = string.Empty;


        [BindProperty]
        public string Department { get; set; } = string.Empty;


        [BindProperty]
        public string Role { get; set; } = string.Empty;


        [BindProperty]
        public string SignatureData { get; set; } = string.Empty;


        // Only meaningful when Role == "Management".
        [BindProperty]
        public string ManagementTitle { get; set; } = string.Empty;


        // True only when no Dean account exists yet.
        public bool IsBootstrapMode { get; set; }


        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }


        public async Task<IActionResult> OnGetAsync()
        {
            bool anyDeanExists =
                await _context.Deans.AnyAsync();


            if (anyDeanExists &&
                !User.IsInRole("Dean"))
            {
                await _auditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    User.Identity?.Name ?? "Unknown",
                    User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown",
                    GetActorId(),
                    "RegisterUser",
                    null,
                    "GET blocked: not authorized as Dean",
                    HttpContext.Connection.RemoteIpAddress?.ToString());


                return Forbid();
            }


            IsBootstrapMode =
                !anyDeanExists;


            return Page();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            string actorUsername =
                User.Identity?.Name ?? "Unknown";


            string actorRole =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? "Unknown";


            int? actorId =
                GetActorId();


            string? ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();


            bool anyDeanExists =
                await _context.Deans.AnyAsync();


            if (anyDeanExists &&
                !User.IsInRole("Dean"))
            {
                await _auditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    actorUsername,
                    actorRole,
                    actorId,
                    "RegisterUser",
                    null,
                    "POST blocked: not authorized as Dean",
                    ipAddress);


                return Forbid();
            }


            IsBootstrapMode =
                !anyDeanExists;


            string selectedRole =
                Role?.Trim() ?? string.Empty;


            if (IsBootstrapMode)
            {
                // The first account must be a Dean.
                if (!selectedRole.Equals(
                        "Dean",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage =
                        "The first account created in the system must be a Dean account.";

                    return Page();
                }
            }
            else
            {
                // An existing Dean may only create HOD
                // or Management accounts.
                if (!selectedRole.Equals(
                        "HOD",
                        StringComparison.OrdinalIgnoreCase) &&
                    !selectedRole.Equals(
                        "Management",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage =
                        "Dean accounts can only create HOD or Management accounts. Lecturer accounts are created by an HOD.";

                    return Page();
                }
            }


            ManagementTitle? parsedTitle =
                null;


            if (selectedRole.Equals(
                    "Management",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse<ManagementTitle>(
                        ManagementTitle,
                        true,
                        out var titleValue))
                {
                    ErrorMessage =
                        "Please select a valid Management office.";

                    return Page();
                }


                parsedTitle =
                    titleValue;
            }


            var request =
                new AccountRegistrationRequest
                {
                    Name =
                        Name?.Trim() ?? string.Empty,

                    Email =
                        Email?.Trim() ?? string.Empty,

                    Department =
                        Department?.Trim() ?? string.Empty,

                    Role =
                        selectedRole,

                    SignatureData =
                        SignatureData ?? string.Empty,

                    // AccountRegistrationRequest.ManagementTitle
                    // is a string, while the Dean page parses the
                    // submitted value into ManagementTitle?.
                    //
                    // Convert the enum to its string representation
                    // before passing it to the service.
                    ManagementTitle =
                        parsedTitle?.ToString(),

                    RegisteringUserId =
                        actorId ?? 0,

                    ActorUsername =
                        actorUsername,

                    ActorRole =
                        actorRole,

                    IpAddress =
                        ipAddress
                };


            var result =
                await _registrationService.RegisterAsync(
                    request);


            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage;

                return Page();
            }


            SuccessMessage =
                result.SuccessMessage;


            ClearForm();


            return Page();
        }


        private int? GetActorId()
        {
            bool parsed =
                int.TryParse(
                    User.FindFirst("UserId")?.Value,
                    out int parsedActorId);


            if (parsed &&
                parsedActorId > 0)
            {
                return parsedActorId;
            }


            return null;
        }


        private void ClearForm()
        {
            Name =
                string.Empty;


            Email =
                string.Empty;


            Department =
                string.Empty;


            Role =
                string.Empty;


            SignatureData =
                string.Empty;


            ManagementTitle =
                string.Empty;
        }
    }
}