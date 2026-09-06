using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN
{
    // The Dean is now the bootstrap role: the very first account in
    // the system is a Dean, created here anonymously (mirrors the old
    // HOD bootstrap pattern — see Program.cs's AllowAnonymousToPage
    // override for this page). Once a Dean exists, this page requires
    // an authenticated Dean (enforced by the manual check below, the
    // same way HOD/RegisterUser used to).
    //
    // A Dean's account-creation privilege is HOD + Management only.
    // Lecturer accounts are created by an HOD, and a second Dean
    // account can never be created from here once the first exists —
    // that's a decision that belongs outside self-service registration.
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

        // True only when no Dean account exists yet — the view uses
        // this to show the bootstrap-only "Dean" option instead of the
        // normal HOD/Management choices.
        public bool IsBootstrapMode { get; set; }

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            bool anyDeanExists = await _context.Deans.AnyAsync();

            if (anyDeanExists && !User.IsInRole("Dean"))
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

            IsBootstrapMode = !anyDeanExists;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string actorUsername = User.Identity?.Name ?? "Unknown";
            string actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            int? actorId = GetActorId();
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            bool anyDeanExists = await _context.Deans.AnyAsync();

            if (anyDeanExists && !User.IsInRole("Dean"))
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

            IsBootstrapMode = !anyDeanExists;

            if (IsBootstrapMode)
            {
                // The one and only time "Dean" is a legal value here —
                // creating the very first account in the system.
                if (!Role.Trim().Equals("Dean", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "The first account created in the system must be a Dean account.";
                    return Page();
                }
            }
            else
            {
                // Dean may only create HOD or Management accounts —
                // never Lecturer, and never another Dean.
                if (!Role.Trim().Equals("HOD", StringComparison.OrdinalIgnoreCase) &&
                    !Role.Trim().Equals("Management", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Dean accounts can only create HOD or Management accounts. Lecturer accounts are created by an HOD.";
                    return Page();
                }
            }

            ManagementTitle? parsedTitle = null;

            if (Role.Trim().Equals("Management", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse<ManagementTitle>(ManagementTitle, true, out var titleValue))
                {
                    ErrorMessage = "Please select a valid Management office.";
                    return Page();
                }

                parsedTitle = titleValue;
            }

            var request = new AccountRegistrationRequest
            {
                Name = Name,
                Email = Email,
                Department = Department,
                Role = Role,
                SignatureData = SignatureData,
                ManagementTitle = parsedTitle,
                RegisteringUserId = actorId ?? 0,
                ActorUsername = actorUsername,
                ActorRole = actorRole,
                IpAddress = ipAddress
            };

            var result = await _registrationService.RegisterAsync(request);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return Page();
            }

            SuccessMessage = result.SuccessMessage;
            ClearForm();
            return Page();
        }

        private int? GetActorId()
        {
            int.TryParse(User.FindFirst("UserId")?.Value, out int parsedActorId);
            return parsedActorId > 0 ? parsedActorId : (int?)null;
        }

        private void ClearForm()
        {
            Name = string.Empty;
            Email = string.Empty;
            Department = string.Empty;
            Role = string.Empty;
            SignatureData = string.Empty;
            ManagementTitle = string.Empty;
        }
    }
}