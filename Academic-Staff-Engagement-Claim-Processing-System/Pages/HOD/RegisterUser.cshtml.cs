using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public string HodFacultyDisplayName { get; private set; } = string.Empty;

        public List<DepartmentOption> AvailableDepartments { get; private set; } = new();

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        public List<string> RegistrationResults { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var currentHod = await GetCurrentHodAsync();

            bool anyHodExists = await _context.Hods.AnyAsync();

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

            if (currentHod == null)
            {
                ErrorMessage =
                    "Your HOD account could not be identified. Please log in again.";

                Users = new List<LecturerRegistrationInput>
                {
                    new LecturerRegistrationInput()
                };

                return Page();
            }

            LoadFacultyData(currentHod.Faculty);

            if (Users.Count == 0)
            {
                Users.Add(new LecturerRegistrationInput());
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string actorUsername =
                User.Identity?.Name ?? "Unknown";

            string actorRole =
                User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            string? ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            var currentHod = await GetCurrentHodAsync();

            bool anyHodExists = await _context.Hods.AnyAsync();

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

                Users = new List<LecturerRegistrationInput>
                {
                    new LecturerRegistrationInput()
                };

                return Page();
            }

            LoadFacultyData(currentHod.Faculty);

            if (Users == null || Users.Count == 0)
            {
                ErrorMessage = "Please add at least one lecturer.";

                Users = new List<LecturerRegistrationInput>
                {
                    new LecturerRegistrationInput()
                };

                return Page();
            }

            for (int i = 0; i < Users.Count; i++)
            {
                var user = Users[i];
                string prefix = $"Lecturer {i + 1}";

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

                if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].PhoneNumber",
                        $"{prefix}: Phone number is required.");
                }

                if (string.IsNullOrWhiteSpace(user.RssbNumber))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].RssbNumber",
                        $"{prefix}: RSSB number is required.");
                }

                if (string.IsNullOrWhiteSpace(user.AccountNumber))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].AccountNumber",
                        $"{prefix}: Account number is required.");
                }

                if (string.IsNullOrWhiteSpace(user.AccountName))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].AccountName",
                        $"{prefix}: Account name is required.");
                }

                if (string.IsNullOrWhiteSpace(user.BankName))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].BankName",
                        $"{prefix}: Bank name is required.");
                }

                if (string.IsNullOrWhiteSpace(user.Department))
                {
                    ModelState.AddModelError(
                        $"Users[{i}].Department",
                        $"{prefix}: Department is required.");
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

                if (!string.IsNullOrWhiteSpace(user.Department))
                {
                    if (!Enum.TryParse<Department>(
                            user.Department,
                            true,
                            out Department selectedDepartment))
                    {
                        ModelState.AddModelError(
                            $"Users[{i}].Department",
                            $"{prefix}: Invalid department selected.");
                    }
                    else if (!FacultyDepartments.IsValidDepartment(
                                 currentHod.Faculty,
                                 selectedDepartment))
                    {
                        ModelState.AddModelError(
                            $"Users[{i}].Department",
                            $"{prefix}: The selected department does not belong to your faculty.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(user.LecturerType))
                {
                    if (!Enum.TryParse<UserRole>(
                            user.LecturerType,
                            true,
                            out UserRole lecturerType))
                    {
                        ModelState.AddModelError(
                            $"Users[{i}].LecturerType",
                            $"{prefix}: Invalid employment type.");
                    }
                    else if (lecturerType != UserRole.PartTimeLecturer &&
                             lecturerType != UserRole.FullTimeLecturer)
                    {
                        ModelState.AddModelError(
                            $"Users[{i}].LecturerType",
                            $"{prefix}: Only Part-Time or Full-Time Lecturer is allowed.");
                    }
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

                if (!Enum.TryParse<Department>(
                        user.Department,
                        true,
                        out Department selectedDepartment))
                {
                    failedRegistrations++;

                    RegistrationResults.Add(
                        $"{user.Name}: Invalid department selected.");

                    continue;
                }

                if (!FacultyDepartments.IsValidDepartment(
                        currentHod.Faculty,
                        selectedDepartment))
                {
                    failedRegistrations++;

                    RegistrationResults.Add(
                        $"{user.Name}: The selected department does not belong to your faculty.");

                    continue;
                }

                var request = new AccountRegistrationRequest
                {
                    Name = user.Name.Trim(),
                    Email = user.Email.Trim(),
                    PhoneNumber = user.PhoneNumber.Trim(),
                    RssbNumber = user.RssbNumber.Trim(),
                    AccountNumber = user.AccountNumber.Trim(),
                    AccountName = user.AccountName.Trim(),
                    BankName = user.BankName.Trim(),
                    Department = selectedDepartment.ToString(),
                    Rank = user.Rank.Trim(),
                    Role = role,
                    GovernmentId = user.GovernmentId.Trim(),
                    SignatureData = user.SignatureData,
                    LecturerType = lecturerType,
                    RegisteringUserId = currentHod.Id,
                    ActorUsername = actorUsername,
                    ActorRole = actorRole,
                    IpAddress = ipAddress
                };

                var result =
                    await _registrationService.RegisterAsync(request);

                if (result.Succeeded)
                {
                    successfulRegistrations++;

                    RegistrationResults.Add(
                        result.SuccessMessage ??
                        $"{user.Name} was registered successfully.");
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
                Users = new List<LecturerRegistrationInput>
                {
                    new LecturerRegistrationInput()
                };
            }

            return Page();
        }

        private async Task<Data.Models.Hod?> GetCurrentHodAsync()
        {
            string? username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            username = username.Trim();

            return await _context.Hods
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.UserName == username &&
                    h.IsActive);
        }

        private void LoadFacultyData(Faculty faculty)
        {
            HodFacultyDisplayName =
                GetFacultyDisplayName(faculty);

            AvailableDepartments =
                FacultyDepartments
                    .GetDepartments(faculty)
                    .Select(department => new DepartmentOption
                    {
                        Value = department,
                        Name = GetDepartmentDisplayName(department)
                    })
                    .ToList();
        }

        private static string GetFacultyDisplayName(Faculty faculty)
        {
            return faculty switch
            {
                Faculty.ComputingAndInformationSciences =>
                    "Computing & Information Sciences",

                Faculty.Law =>
                    "Law",

                Faculty.EconomicSciencesAndManagement =>
                    "Economic Sciences & Management",

                Faculty.EnvironmentalStudies =>
                    "Environmental Studies",

                _ => faculty.ToString()
            };
        }

        private static string GetDepartmentDisplayName(
            Department department)
        {
            return department switch
            {
                Department.SoftwareEngineering =>
                    "Software Engineering",

                Department.InformationSystemsManagement =>
                    "Information Systems Management",

                Department.Multimedia =>
                    "Multimedia",

                Department.Networking =>
                    "Networking",

                Department.PublicLaw =>
                    "Public Law",

                Department.PrivateLaw =>
                    "Private Law",

                Department.InternationalLawEnvironmentAndLandUseLaw =>
                    "International Law / Environment & Land Use Law",

                Department.Accounting =>
                    "Accounting",

                Department.Finance =>
                    "Finance",

                Department.Marketing =>
                    "Marketing",

                Department.HumanResourcesManagement =>
                    "Human Resources Management",

                Department.Economics =>
                    "Economics",

                Department.CooperativeManagement =>
                    "Cooperative Management",

                Department.EnvironmentalManagementAndConservation =>
                    "Environmental Management & Conservation",

                Department.EmergencyAndDisasterManagement =>
                    "Emergency & Disaster Management",

                Department.RuralDevelopment =>
                    "Rural Development",

                _ => department.ToString()
            };
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

        public class DepartmentOption
        {
            public Department Value { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        public class LecturerRegistrationInput
        {
            public string Name { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public string PhoneNumber { get; set; } = string.Empty;

            public string RssbNumber { get; set; } = string.Empty;

            public string AccountNumber { get; set; } = string.Empty;

            public string AccountName { get; set; } = string.Empty;

            public string BankName { get; set; } = string.Empty;

            public string Department { get; set; } = string.Empty;

            public string Rank { get; set; } = string.Empty;

            public string GovernmentId { get; set; } = string.Empty;

            public string LecturerType { get; set; } = string.Empty;

            public string SignatureData { get; set; } = string.Empty;
        }
    }
}