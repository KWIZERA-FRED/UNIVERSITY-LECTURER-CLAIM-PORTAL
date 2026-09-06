using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
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
        public string Rank { get; set; } = string.Empty;

        [BindProperty]
        public string Role { get; set; } = string.Empty;

        [BindProperty]
        public string GovernmentId { get; set; } = string.Empty;

        [BindProperty]
        public string SignatureData { get; set; } = string.Empty;

        // "PartTimeLecturer" or "FullTimeLecturer" — bound as a string
        // from the form's dropdown, parsed below.
        [BindProperty]
        public string LecturerType { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            bool anyHodExists = await _context.Hods.AnyAsync();

            if (anyHodExists && !User.IsInRole("HOD"))
            {
                await _auditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    User.Identity?.Name ?? "Unknown",
                    User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown",
                    GetActorId(),
                    "RegisterUser",
                    null,
                    "GET blocked: not authorized as HOD",
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return Forbid();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string actorUsername = User.Identity?.Name ?? "Unknown";
            string actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            int? actorId = GetActorId();
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            bool anyHodExists = await _context.Hods.AnyAsync();

            if (anyHodExists && !User.IsInRole("HOD"))
            {
                await _auditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    actorUsername,
                    actorRole,
                    actorId,
                    "RegisterUser",
                    null,
                    "POST blocked: not authorized as HOD",
                    ipAddress);

                return Forbid();
            }

            // HOD may create Lecturer, HOD, or Dean — not Management.
            // Management accounts (HR Officer, DVCAR, Vice Chancellor)
            // are created only by the Dean.
            if (Role.Trim().Equals("Management", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "HOD accounts cannot create Management accounts. Please contact a Dean.";
                return Page();
            }

            var request = new AccountRegistrationRequest
            {
                Name = Name,
                Email = Email,
                Department = Department,
                Rank = Rank,
                Role = Role,
                GovernmentId = GovernmentId,
                SignatureData = SignatureData,
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
            Rank = string.Empty;
            Role = string.Empty;
            GovernmentId = string.Empty;
            SignatureData = string.Empty;
        }
    }
}