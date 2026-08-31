using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN
{
    // No bootstrap exception here, unlike HOD/RegisterUser — a Dean
    // account always already exists (created by an HOD) before this
    // page can ever be reached, so [Authorize(Roles = "Dean")] alone
    // is sufficient; there's no first-run chicken-and-egg problem to
    // solve for this role the way there was for the very first HOD.
    [Authorize(Roles = "Dean")]
    public class RegisterUserModel : PageModel
    {
        private readonly AccountRegistrationService _registrationService;
        private readonly AuditLogger _auditLogger;

        public RegisterUserModel(
            AccountRegistrationService registrationService,
            AuditLogger auditLogger)
        {
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

        // Only meaningful when Role == "Management". Bound as a string
        // from the form's dropdown, parsed below.
        [BindProperty]
        public string ManagementTitle { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string actorUsername = User.Identity?.Name ?? "Unknown";
            string actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            int? actorId = GetActorId();
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

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
                Rank = Rank,
                Role = Role,
                GovernmentId = GovernmentId,
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
            Rank = string.Empty;
            Role = string.Empty;
            GovernmentId = string.Empty;
            SignatureData = string.Empty;
            ManagementTitle = string.Empty;
        }
    }
}