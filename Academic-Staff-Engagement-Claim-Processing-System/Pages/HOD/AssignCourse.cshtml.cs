using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class AssignCourseModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;
        private readonly EmailService _emailService;

        public AssignCourseModel(
            ApplicationDbContext context,
            AuditLogger auditLogger,
            EmailService emailService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _emailService = emailService;
        }

        [BindProperty]
        public int? SelectedCourse { get; set; }

        [BindProperty]
        public int? SelectedLecturer { get; set; }

        [BindProperty]
        public string? AcademicYear { get; set; }

        [BindProperty]
        public Semester? Semester { get; set; }

        [BindProperty]
        public Session? Session { get; set; }

        [BindProperty]
        public Campus? Campus { get; set; }

        [BindProperty]
        public TeachingHoursOption? AllocatedHoursOption { get; set; }

        public List<Course> Courses { get; set; } = new();

        public List<LecturerOption> Lecturers { get; set; } = new();

        public decimal HourlyRate { get; set; }

        public string? ErrorMessage { get; set; }

        public sealed class LecturerOption
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public LecturerRank? Rank { get; set; }

            public UserRole Type { get; set; }

            public Faculty? Faculty { get; set; }

            public string Department { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDataAsync();

            if (!SelectedCourse.HasValue)
            {
                ErrorMessage = "Please select a course.";
                return Page();
            }

            if (!SelectedLecturer.HasValue)
            {
                ErrorMessage = "Please select a lecturer.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage = "Please select an academic year.";
                return Page();
            }

            if (!Semester.HasValue)
            {
                ErrorMessage = "Please select a semester.";
                return Page();
            }

            if (!Session.HasValue)
            {
                ErrorMessage = "Please select a session.";
                return Page();
            }

            if (!Campus.HasValue)
            {
                ErrorMessage = "Please select a campus.";
                return Page();
            }

            if (!AllocatedHoursOption.HasValue)
            {
                ErrorMessage =
                    "Please select the number of teaching hours (30, 45, or 60).";

                return Page();
            }

            var hod = await GetCurrentHodAsync();

            if (hod is null)
            {
                ErrorMessage =
                    "The current HOD account could not be identified.";

                return Page();
            }

            var allocatedHours =
                (decimal)AllocatedHoursOption.Value;

            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == SelectedCourse.Value &&
                    c.IsActive);

            if (course is null)
            {
                ErrorMessage =
                    "Selected course could not be found.";

                return Page();
            }

            var lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l =>
                    l.Id == SelectedLecturer.Value &&
                    l.IsActive)
                .Select(l => new LecturerOption
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    Rank = l.Rank,
                    Type = l.Type,
                    Faculty = l.Faculty,
                    Department = l.Department
                })
                .FirstOrDefaultAsync();

            if (lecturer is null)
            {
                ErrorMessage =
                    "Selected lecturer could not be found.";

                return Page();
            }

            if (lecturer.Faculty != hod.Faculty)
            {
                ErrorMessage =
                    "The selected lecturer does not belong to your faculty.";

                return Page();
            }

            if (!lecturer.Faculty.HasValue)
            {
                ErrorMessage =
                    "The selected lecturer does not have a faculty assigned.";

                return Page();
            }

            if (string.IsNullOrWhiteSpace(lecturer.Department))
            {
                ErrorMessage =
                    "The selected lecturer does not have a department assigned.";

                return Page();
            }

            var normalizedAcademicYear =
                AcademicYear.Trim();

            var existingAssignment =
                await _context.CourseAssignments
                    .AnyAsync(ca =>
                        ca.LecturerId == lecturer.Id &&
                        ca.CourseId == course.Id &&
                        ca.AcademicYear == normalizedAcademicYear &&
                        ca.Semester == Semester.Value &&
                        ca.IsActive);

            if (existingAssignment)
            {
                ErrorMessage =
                    "This lecturer has already been assigned this course " +
                    "for the selected academic year and semester.";

                return Page();
            }

            HourlyRate =
                GetRateForRank(lecturer.Rank);

            var assignment =
                new CourseAssignment
                {
                    LecturerId =
                        lecturer.Id,

                    CourseId =
                        course.Id,

                    AcademicYear =
                        normalizedAcademicYear,

                    Semester =
                        Semester.Value,

                    Session =
                        Session.Value,

                    Campus =
                        Campus.Value,

                    AllocatedHours =
                        allocatedHours,

                    IsApproved =
                        false,

                    IsActive =
                        true,

                    CreatedAtUtc =
                        DateTime.UtcNow
                };

            _context.CourseAssignments.Add(assignment);

            await _context.SaveChangesAsync();

            var governmentId =
                await _context.Lecturers
                    .AsNoTracking()
                    .Where(l =>
                        l.Id == lecturer.Id)
                    .Select(l =>
                        l.GovernmentIdEncrypted)
                    .FirstOrDefaultAsync();

            var contractDate =
                DateTime.UtcNow;

            var content =
                await BuildContractHtmlAsync(
                    contractDate,
                    lecturer.UserName,
                    lecturer.Rank?.ToString(),
                    governmentId,
                    lecturer.Department,
                    lecturer.Faculty.Value,
                    lecturer.Type,
                    assignment.Session.ToString(),
                    course.Code,
                    course.Title,
                    assignment.AcademicYear,
                    assignment.Semester.ToString(),
                    assignment.Campus.ToString(),
                    assignment.AllocatedHours,
                    HourlyRate);

            var contract =
                new Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Contract(
                    0,
                    "1.0")
                {
                    LecturerId =
                        lecturer.Id,

                    CourseAssignmentId =
                        assignment.Id,

                    Content =
                        content,

                    RatePerHour =
                        HourlyRate,

                    StartDateUtc =
                        contractDate,

                    Status =
                        ContractStatus.PendingSignature
                };

            _context.Contracts.Add(contract);

            await _context.SaveChangesAsync();

            _context.ContractSignatures.AddRange(

                new ContractSignature(
                    0,
                    contract.Id,
                    1,
                    SignerRole.Lecturer),

                new ContractSignature(
                    0,
                    contract.Id,
                    2,
                    SignerRole.Dean),

                new ContractSignature(
                    0,
                    contract.Id,
                    3,
                    SignerRole.HROfficer),

                new ContractSignature(
                    0,
                    contract.Id,
                    4,
                    SignerRole.DVCAR),

                new ContractSignature(
                    0,
                    contract.Id,
                    5,
                    SignerRole.ViceChancellor)
            );

            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(lecturer.Email))
            {
                try
                {
                    await _emailService
                        .SendContractSigningNotificationAsync(
                            lecturer.Email,
                            lecturer.UserName,
                            $"CON-{contract.Id:D6}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Contract notification email failed for " +
                        $"{lecturer.Email}, Contract " +
                        $"CON-{contract.Id:D6}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine(
                    $"Contract CON-{contract.Id:D6} was created, " +
                    "but the selected lecturer has no email address.");
            }

            var actorUsername =
                User.Identity?.Name ??
                "Unknown";

            var actorRole =
                User.FindFirstValue(
                    ClaimTypes.Role) ??
                "HOD";

            int? actorId = null;

            var actorIdClaim =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (int.TryParse(
                    actorIdClaim,
                    out var parsedActorId))
            {
                actorId = parsedActorId;
            }

            var ipAddress =
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString();

            await _auditLogger.LogAsync(
                AuditAction.CourseAssigned,
                actorUsername,
                actorRole,
                actorId,
                "Contract",
                contract.Id,
                $"Course '{course.Code} - {course.Title}' " +
                $"assigned to lecturer '{lecturer.UserName}'. " +
                $"Contract CON-{contract.Id:D6} created.",
                ipAddress);

            return RedirectToPage(
                "./ContractPreview",
                new
                {
                    ContractId =
                        contract.Id
                });
        }

        private async Task LoadDataAsync()
        {
            Courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync();

            var hod = await GetCurrentHodAsync();

            if (hod is null)
            {
                Lecturers = new List<LecturerOption>();
                return;
            }

            Lecturers = await _context.Lecturers
                .AsNoTracking()
                .Where(l =>
                    l.IsActive &&
                    l.Faculty == hod.Faculty)
                .OrderBy(l => l.UserName)
                .Select(l => new LecturerOption
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    Rank = l.Rank,
                    Type = l.Type,
                    Faculty = l.Faculty,
                    Department = l.Department
                })
                .ToListAsync();
        }

        private async Task<Hod?> GetCurrentHodAsync()
        {
            var userName =
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            return await _context.Hods
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.UserName == userName &&
                    h.IsActive);
        }

        private static decimal GetRateForRank(
            LecturerRank? rank)
        {
            if (!rank.HasValue)
            {
                return 7000m;
            }

            return rank.Value switch
            {
                LecturerRank.TutorialAssistant => 7000m,
                LecturerRank.AssistantLecturer => 10000m,
                LecturerRank.LecturerWithMasters => 14000m,
                LecturerRank.LecturerWithPhD => 16000m,
                LecturerRank.SeniorLecturer => 18000m,
                LecturerRank.AssistantProfessor => 20000m,
                LecturerRank.Professor => 25000m,
                _ => 7000m
            };
        }

        private async Task<string> BuildContractHtmlAsync(
            DateTime contractDate,
            string lecturerName,
            string? academicRank,
            string? governmentId,
            string department,
            Faculty faculty,
            UserRole lecturerType,
            string session,
            string courseCode,
            string courseTitle,
            string academicYear,
            string semester,
            string campus,
            decimal allocatedHours,
            decimal hourlyRate)
        {
            var template =
                await _context.Templates
                    .AsNoTracking()
                    .Select(t => t.Contract)
                    .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(template))
            {
                throw new InvalidOperationException(
                    "The contract template could not be found in the database.");
            }

            var facultyName =
                GetEnumDisplayName(faculty);

            var employmentType =
                GetEmploymentTypeText(lecturerType);

            var departmentOptions =
                BuildDepartmentOptions(
                    faculty,
                    department);

            var sessionOptions =
                BuildSessionOptions(session);

            var campusOptions =
                BuildCampusOptions(campus);

            var contactHoursOptions =
                BuildContactHoursOptions(allocatedHours);

            var rateOptions =
                BuildRateOptions(hourlyRate);

            var mainContract =
                template
                    .Replace(
                        "{{ContractDate}}",
                        WebUtility.HtmlEncode(
                            contractDate.ToLocalTime()
                                .ToString("dd/MM/yyyy")))
                    .Replace(
                        "{{LecturerName}}",
                        WebUtility.HtmlEncode(
                            lecturerName))
                    .Replace(
                        "{{AcademicRank}}",
                        WebUtility.HtmlEncode(
                            academicRank ?? "N/A"))
                    .Replace(
                        "{{GovernmentId}}",
                        WebUtility.HtmlEncode(
                            string.IsNullOrWhiteSpace(governmentId)
                                ? "On file with UNILAK"
                                : governmentId))
                    .Replace(
                        "{{EmploymentType}}",
                        WebUtility.HtmlEncode(
                            employmentType))
                    .Replace(
                        "{{Faculty}}",
                        WebUtility.HtmlEncode(
                            facultyName))
                    .Replace(
                        "{{Department}}",
                        WebUtility.HtmlEncode(
                            department))
                    .Replace(
                        "{{DepartmentOptionsList}}",
                        departmentOptions)
                    .Replace(
                        "{{Intake}}",
                        WebUtility.HtmlEncode(
                            session))
                    .Replace(
                        "{{Session}}",
                        WebUtility.HtmlEncode(
                            session))
                    .Replace(
                        "{{SessionOptionsList}}",
                        sessionOptions)
                    .Replace(
                        "{{CourseTitle}}",
                        WebUtility.HtmlEncode(
                            $"{courseCode} — {courseTitle}"))
                    .Replace(
                        "{{AcademicYear}}",
                        WebUtility.HtmlEncode(
                            academicYear))
                    .Replace(
                        "{{Semester}}",
                        WebUtility.HtmlEncode(
                            semester))
                    .Replace(
                        "{{Campus}}",
                        WebUtility.HtmlEncode(
                            campus))
                    .Replace(
                        "{{CampusOptionsList}}",
                        campusOptions)
                    .Replace(
                        "{{AllocatedHours}}",
                        allocatedHours.ToString("0.##"))
                    .Replace(
                        "{{ContactHoursOptionsList}}",
                        contactHoursOptions)
                    .Replace(
                        "{{HourlyRate}}",
                        $"{hourlyRate:N0} RWF")
                    .Replace(
                        "{{RateOptionsList}}",
                        rateOptions)
                    .Replace(
                        "{{NumberOfOnlineClasses}}",
                        "....")
                    .Replace(
                        "{{OnlineHours}}",
                        "....");

            mainContract =
                RemoveExistingSignatureSection(
                    mainContract);

            mainContract =
                RemoveWorkflowNotice(
                    mainContract);

            mainContract =
                RemoveDocumentFooter(
                    mainContract);

            var paperSignatureSection =
                BuildPaperSignatureSection(
                    lecturerName);

            var accreditationMarker =
                "<p class=\"contract-accreditation-note\">";

            var accreditationIndex =
                mainContract.IndexOf(
                    accreditationMarker,
                    StringComparison.OrdinalIgnoreCase);

            if (accreditationIndex >= 0)
            {
                mainContract =
                    mainContract.Insert(
                        accreditationIndex,
                        paperSignatureSection +
                        Environment.NewLine);
            }
            else
            {
                var closingIndex =
                    mainContract.LastIndexOf(
                        "</div>",
                        StringComparison.OrdinalIgnoreCase);

                if (closingIndex >= 0)
                {
                    mainContract =
                        mainContract.Insert(
                            closingIndex,
                            paperSignatureSection +
                            Environment.NewLine);
                }
                else
                {
                    mainContract +=
                        Environment.NewLine +
                        paperSignatureSection;
                }
            }

            return mainContract;
        }

        private static string RemoveExistingSignatureSection(
            string html)
        {
            return Regex.Replace(
                html,
                @"<div\s+class\s*=\s*[""']contract-signatures[""'][^>]*>.*?</div>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
        }

        private static string RemoveWorkflowNotice(
            string html)
        {
            return Regex.Replace(
                html,
                @"<div\s+class\s*=\s*[""']contract-workflow-notice[""'][^>]*>.*?</div>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
        }

        private static string RemoveDocumentFooter(
            string html)
        {
            return Regex.Replace(
                html,
                @"<div\s+class\s*=\s*[""']contract-document-footer[""'][^>]*>.*?</div>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
        }

        private static string BuildPaperSignatureSection(
            string lecturerName)
        {
            var html =
                new StringBuilder();

            html.AppendLine(
                "<div class=\"paper-signatures\">");

            html.AppendLine(
                "<div class=\"paper-signature-line lecturer-signature-line\">");

            html.AppendLine(
                "<span class=\"paper-signature-name\">" +
                $"{WebUtility.HtmlEncode(lecturerName)}" +
                "...................................................." +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Signature........................" +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Date................." +
                "</span>");

            html.AppendLine(
                "</div>");

            html.AppendLine(
                "<div class=\"paper-signature-line dean-signature-line\">");

            html.AppendLine(
                "<span class=\"paper-signature-name\">" +
                "Dean of Faculty: Prof. NYESHEJA M. Enan" +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Signature........................." +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Date................." +
                "</span>");

            html.AppendLine(
                "</div>");

            html.AppendLine(
                "<div class=\"paper-signature-line hr-signature-line\">");

            html.AppendLine(
                "<span class=\"paper-signature-name\">" +
                "Human Resource Officer Mr. NTAKIRUTIMANA Elison" +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Signature......" +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Date................." +
                "</span>");

            html.AppendLine(
                "</div>");

            html.AppendLine(
                "<div class=\"paper-signature-line dvcar-signature-line\">");

            html.AppendLine(
                "<span class=\"paper-signature-name\">" +
                "DVCAR Prof. HAKIZIMANA Emmanuel" +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Signature........................." +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Date................." +
                "</span>");

            html.AppendLine(
                "</div>");

            html.AppendLine(
                "<div class=\"paper-signature-line vc-signature-line\">");

            html.AppendLine(
                "<span class=\"paper-signature-name\">" +
                "Vice Chancellor Prof. NGAMIJE Jean" +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Signature..............................." +
                "</span>");

            html.AppendLine(
                "<span class=\"paper-signature-field\">" +
                "Date................." +
                "</span>");

            html.AppendLine(
                "</div>");

            html.AppendLine(
                "</div>");

            return html.ToString();
        }

        private static string BuildDepartmentOptions(
            Faculty faculty,
            string selectedDepartment)
        {
            var options =
                faculty ==
                Faculty.ComputingAndInformationSciences
                    ? new[]
                    {
                        ("IT-NET", "Networking"),
                        ("IT-MULT", "Multimedia"),
                        ("SE", "Software Engineering"),
                        ("ISM", "Information Systems Management")
                    }
                    : FacultyDepartments
                        .GetDepartments(faculty)
                        .Select(d =>
                            (
                                GetEnumDisplayName(d),
                                GetEnumDisplayName(d)))
                        .ToArray();

            var selected =
                NormalizeOptionText(
                    selectedDepartment);

            return string.Join(
                ", ",
                options.Select(option =>
                    IsDepartmentMatch(
                        selected,
                        option.Item1,
                        option.Item2)
                        ? BuildSelectedOption(
                            option.Item1)
                        : WebUtility.HtmlEncode(
                            option.Item1)));
        }

        private static bool IsDepartmentMatch(
            string selected,
            string abbreviation,
            string fullName)
        {
            if (string.Equals(
                    selected,
                    NormalizeOptionText(abbreviation),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(
                    selected,
                    NormalizeOptionText(fullName),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return selected switch
            {
                "softwareengineering" =>
                    abbreviation == "SE",

                "informationsystemsmanagement" =>
                    abbreviation == "ISM",

                "multimedia" =>
                    abbreviation == "IT-MULT",

                "networking" =>
                    abbreviation == "IT-NET",

                _ => false
            };
        }

        private static string BuildSessionOptions(
            string selectedSession)
        {
            var options =
                new[]
                {
                    "Day",
                    "Evening",
                    "Weekend"
                };

            return BuildOptions(
                options,
                selectedSession);
        }

        private static string BuildCampusOptions(
            string selectedCampus)
        {
            var options =
                new[]
                {
                    "KIGALI",
                    "NYANZA",
                    "RWAMAGANA"
                };

            return BuildOptions(
                options,
                selectedCampus);
        }

        private static string BuildContactHoursOptions(
            decimal selectedHours)
        {
            var options =
                new[]
                {
                    "30",
                    "45",
                    "60"
                };

            return string.Join(
                "/",
                options.Select(option =>
                    decimal.TryParse(
                            option,
                            out var value) &&
                        value == selectedHours
                        ? BuildSelectedOption(option)
                        : WebUtility.HtmlEncode(option)));
        }

        private static string BuildRateOptions(
            decimal selectedRate)
        {
            var rates =
                new[]
                {
                    (25000m, "25K"),
                    (20000m, "20K"),
                    (18000m, "18K"),
                    (16000m, "16K"),
                    (14000m, "14K"),
                    (10000m, "10K"),
                    (7000m, "7K")
                };

            return string.Join(
                ", ",
                rates.Select(rate =>
                    rate.Item1 == selectedRate
                        ? BuildSelectedOption(rate.Item2)
                        : WebUtility.HtmlEncode(rate.Item2)));
        }

        private static string BuildOptions(
            IEnumerable<string> options,
            string selected)
        {
            var normalizedSelected =
                NormalizeOptionText(selected);

            return string.Join(
                ", ",
                options.Select(option =>
                    NormalizeOptionText(option) ==
                    normalizedSelected
                        ? BuildSelectedOption(option)
                        : WebUtility.HtmlEncode(option)));
        }

        private static string BuildSelectedOption(
            string value)
        {
            return
                $"<span class=\"contract-selected-option\">" +
                $"({WebUtility.HtmlEncode(value)})" +
                "</span>";
        }

        private static string GetEmploymentTypeText(
            UserRole type)
        {
            return type switch
            {
                UserRole.FullTimeLecturer =>
                    "Internal",

                UserRole.PartTimeLecturer =>
                    "External",

                _ =>
                    "External/Internal"
            };
        }

        private static string GetEnumDisplayName<TEnum>(
            TEnum value)
            where TEnum : struct, Enum
        {
            var name =
                value.ToString();

            return SplitPascalCase(name);
        }

        private static string SplitPascalCase(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var result =
                new StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0 &&
                    char.IsUpper(value[i]) &&
                    !char.IsUpper(value[i - 1]))
                {
                    result.Append(' ');
                }

                result.Append(value[i]);
            }

            return result.ToString();
        }

        private static string NormalizeOptionText(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(
                value
                    .Where(char.IsLetterOrDigit)
                    .ToArray())
                .ToLowerInvariant();
        }
    }
}