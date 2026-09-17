using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

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
        public List<LecturerRegistrationInput> Users { get; set; } = new();

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public List<string> RegistrationResults { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var currentHod =
                await GetCurrentHodAsync();

            bool anyHodExists =
                await _context.Hods.AnyAsync();

            if (anyHodExists && currentHod == null)
            {
                await _auditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    User.Identity?.Name ?? "Unknown",
                    User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown",
                    GetActorId(),
                    "RegisterUser",
                    null,
                    "GET blocked: authenticated user is not a registered HOD.",
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return Forbid();
            }

            if (Users.Count == 0)
            {
                Users.Add(
                    new LecturerRegistrationInput());
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string actorUsername =
                User.Identity?.Name ?? "Unknown";

            string actorRole =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? "Unknown";

            string? ipAddress =
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString();

            var currentHod =
                await GetCurrentHodAsync();

            bool anyHodExists =
                await _context.Hods.AnyAsync();

            if (anyHodExists && currentHod == null)
            {
                await _auditLogger.LogAsync(
                    AuditAction.AccessDenied,
                    actorUsername,
                    actorRole,
                    GetActorId(),
                    "RegisterUser",
                    null,
                    "POST blocked: authenticated user is not a registered HOD.",
                    ipAddress);

                return Forbid();
            }

            if (currentHod == null)
            {
                ErrorMessage =
                    "Your HOD account could not be identified. Please log in again.";

                Users =
                    new List<LecturerRegistrationInput>
                    {
                        new LecturerRegistrationInput()
                    };

                return Page();
            }

            if (Users == null ||
                Users.Count == 0)
            {
                ErrorMessage =
                    "Please add at least one lecturer.";

                Users =
                    new List<LecturerRegistrationInput>
                    {
                        new LecturerRegistrationInput()
                    };

                return Page();
            }

            for (int i = 0;
                 i < Users.Count;
                 i++)
            {
                var user =
                    Users[i];

                string prefix =
                    $"Lecturer {i + 1}";

                if (string.IsNullOrWhiteSpace(user.Name))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].Name",
                        $"{prefix}: Full name is required.");
                }

                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].Email",
                        $"{prefix}: Email address is required.");
                }

                if (string.IsNullOrWhiteSpace(user.GovernmentId))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].GovernmentId",
                        $"{prefix}: Government ID is required.");
                }

                if (string.IsNullOrWhiteSpace(user.Rank))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].Rank",
                        $"{prefix}: Academic rank is required.");
                }

                if (string.IsNullOrWhiteSpace(user.LecturerType))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].LecturerType",
                        $"{prefix}: Employment type is required.");
                }

                if (string.IsNullOrWhiteSpace(user.SignatureData))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].SignatureData",
                        $"{prefix}: Digital signature is required.");
                }
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage =
                    "Please correct the highlighted lecturer information.";

                return Page();
            }

            int successfulRegistrations = 0;

            int failedRegistrations = 0;

            foreach (var user in Users)
            {
                const string role = "Lecturer";

                if (!Enum.TryParse<UserRole>(
                    user.LecturerType,
                    true,
                    out UserRole lecturerType))
                {
                    failedRegistrations++;

                    RegistrationResults.Add(
                        $"{user.Name}: Invalid employment type.");

                    continue;
                }

                if (lecturerType != UserRole.PartTimeLecturer &&
                    lecturerType != UserRole.FullTimeLecturer)
                {
                    failedRegistrations++;

                    RegistrationResults.Add(
                        $"{user.Name}: Only Part-Time or Full-Time Lecturer is allowed.");

                    continue;
                }

                var request =
                    new AccountRegistrationRequest
                    {
                        Name = user.Name,
                        Email = user.Email,
                        Department = currentHod.Department ?? string.Empty,
                        Rank = user.Rank,
                        Role = role,
                        GovernmentId = user.GovernmentId,
                        SignatureData = user.SignatureData,
                        LecturerType = lecturerType,
                        RegisteringUserId = currentHod.Id,
                        ActorUsername = actorUsername,
                        ActorRole = actorRole,
                        IpAddress = ipAddress
                    };

                var result =
                    await _registrationService
                        .RegisterAsync(request);

                if (result.Succeeded)
                {
                    successfulRegistrations++;

                    RegistrationResults.Add(
                        result.SuccessMessage
                        ?? $"{user.Name} was registered successfully.");
                }
                else
                {
                    failedRegistrations++;

                    RegistrationResults.Add(
                        $"{user.Name}: {result.ErrorMessage ?? "Registration failed."}");
                }
            }

            if (successfulRegistrations > 0 &&
                failedRegistrations == 0)
            {
                SuccessMessage =
                    successfulRegistrations == 1
                        ? "The lecturer account was created successfully."
                        : $"{successfulRegistrations} lecturer accounts were created successfully.";
            }
            else if (successfulRegistrations > 0 &&
                     failedRegistrations > 0)
            {
                SuccessMessage =
                    $"{successfulRegistrations} lecturer account(s) created successfully.";

                ErrorMessage =
                    $"{failedRegistrations} lecturer account(s) could not be created. See the results below.";
            }
            else
            {
                ErrorMessage =
                    "No lecturer accounts were created. See the results below.";
            }

            if (successfulRegistrations > 0)
            {
                Users =
                    new List<LecturerRegistrationInput>
                    {
                        new LecturerRegistrationInput()
                    };
            }

            return Page();
        }

        private async Task<Data.Models.Hod?> GetCurrentHodAsync()
        {
            string? username =
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
                return null;

            username =
                username.Trim();

            return await _context.Hods
                .FirstOrDefaultAsync(
                    h => h.UserName == username);
        }

        private int? GetActorId()
        {
            if (int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int parsedActorId))
            {
                return parsedActorId > 0
                    ? parsedActorId
                    : null;
            }

            return null;
        }

        public class LecturerRegistrationInput
        {
            public string Name { get; set; } =
                string.Empty;

            public string Email { get; set; } =
                string.Empty;

            public string Department { get; set; } =
                string.Empty;

            public string Rank { get; set; } =
                string.Empty;

            public string GovernmentId { get; set; } =
                string.Empty;

            public string LecturerType { get; set; } =
                string.Empty;

            public string SignatureData { get; set; } =
                string.Empty;
        }
    }
}