using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text;
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

        // ============================================================
        // FORM PROPERTIES
        // ============================================================

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
        public decimal AllocatedHours { get; set; }

        // ============================================================
        // PAGE DATA
        // ============================================================

        public List<Course> Courses { get; set; } = new();

        public List<LecturerOption> Lecturers { get; set; } = new();

        public decimal HourlyRate { get; set; }

        public string? ErrorMessage { get; set; }

        // ============================================================
        // LECTURER OPTION
        // ============================================================

        public sealed class LecturerOption
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public LecturerRank? Rank { get; set; }
        }

        // ============================================================
        // GET
        // ============================================================

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        // ============================================================
        // POST
        // ============================================================

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDataAsync();

            // --------------------------------------------------------
            // VALIDATION
            // --------------------------------------------------------

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

            if (AllocatedHours <= 0)
            {
                ErrorMessage =
                    "Please enter a valid number of teaching hours.";

                return Page();
            }

            if (AllocatedHours > 500)
            {
                ErrorMessage =
                    "Allocated hours cannot exceed 500.";

                return Page();
            }

            // ========================================================
            // FIND COURSE
            // ========================================================

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

            // ========================================================
            // FIND LECTURER
            // ========================================================

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
                    Rank = l.Rank
                })
                .FirstOrDefaultAsync();

            if (lecturer is null)
            {
                ErrorMessage =
                    "Selected lecturer could not be found.";

                return Page();
            }

            // ========================================================
            // NORMALIZE ACADEMIC YEAR
            // ========================================================

            var normalizedAcademicYear =
                AcademicYear.Trim();

            // ========================================================
            // CHECK DUPLICATE ASSIGNMENT
            // ========================================================

            var existingAssignment =
                await _context.CourseAssignments
                    .AnyAsync(ca =>
                        ca.LecturerId == lecturer.Id &&
                        ca.CourseId == course.Id &&
                        ca.AcademicYear ==
                            normalizedAcademicYear &&
                        ca.Semester ==
                            Semester.Value &&
                        ca.IsActive);

            if (existingAssignment)
            {
                ErrorMessage =
                    "This lecturer has already been assigned this " +
                    "course for the selected academic year and semester.";

                return Page();
            }

            // ========================================================
            // GET HOURLY RATE
            // ========================================================

            HourlyRate =
                GetRateForRank(lecturer.Rank);

            // ========================================================
            // CREATE COURSE ASSIGNMENT
            // ========================================================

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
                        AllocatedHours,

                    IsApproved =
                        false,

                    IsActive =
                        true,

                    CreatedAtUtc =
                        DateTime.UtcNow
                };

            _context.CourseAssignments.Add(
                assignment);

            await _context.SaveChangesAsync();

            // ========================================================
            // GET GOVERNMENT ID
            // ========================================================

            var governmentId =
                await _context.Lecturers
                    .AsNoTracking()
                    .Where(l =>
                        l.Id == lecturer.Id)
                    .Select(l =>
                        l.GovernmentIdEncrypted)
                    .FirstOrDefaultAsync();

            // ========================================================
            // CONTRACT DATE
            // ========================================================

            var contractDate =
                DateTime.UtcNow;

            // ========================================================
            // BUILD CONTRACT HTML
            // ========================================================

            var content =
                await BuildContractHtmlAsync(
                    contractDate,
                    lecturer.UserName,
                    lecturer.Rank?.ToString(),
                    governmentId,
                    course.Department,
                    assignment.Session.ToString(),
                    course.Code,
                    course.Title,
                    assignment.AcademicYear,
                    assignment.Semester.ToString(),
                    assignment.Campus.ToString(),
                    assignment.AllocatedHours,
                    HourlyRate);

            // ========================================================
            // CREATE CONTRACT
            // ========================================================

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

            _context.Contracts.Add(
                contract);

            await _context.SaveChangesAsync();

            // ========================================================
            // CREATE SEQUENTIAL SIGNATURE STEPS
            // ========================================================
            //
            // 1 = Lecturer
            // 2 = Dean
            // 3 = HR Officer
            // 4 = DVCAR
            // 5 = Vice Chancellor
            // ========================================================

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

            // ========================================================
            // NOTIFY LECTURER
            // ========================================================

            if (!string.IsNullOrWhiteSpace(
                    lecturer.Email))
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

            // ========================================================
            // AUDIT LOG
            // ========================================================

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

            // ========================================================
            // GO TO CONTRACT PREVIEW
            // ========================================================

            return RedirectToPage(
                "./ContractPreview",
                new
                {
                    ContractId =
                        contract.Id
                });
        }

        // ============================================================
        // ============================================================
        // LOAD COURSES
        // ============================================================

        private async Task LoadDataAsync()
        {
            Courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync();

            Lecturers = await _context.Lecturers
                .AsNoTracking()
                .Where(l => l.IsActive)
                .OrderBy(l => l.UserName)
                .Select(l => new LecturerOption
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    Rank = l.Rank
                })
                .ToListAsync();
        }

        // ============================================================
        // HOURLY RATE
        // ============================================================

        private static decimal GetRateForRank(LecturerRank? rank)
        {
            return rank switch
            {
                LecturerRank.AssistantLecturer => 5000m,
                LecturerRank.Lecturer => 7000m,
                LecturerRank.SeniorLecturer => 9000m,
                LecturerRank.AssociateProfessor => 11000m,
                LecturerRank.Professor => 13000m,
                _ => 5000m
            };
        }

        // ============================================================
        // BUILD CONTRACT HTML
        // ============================================================

        private async Task<string> BuildContractHtmlAsync(
            DateTime contractDate,
            string lecturerName,
            string? academicRank,
            string? governmentId,
            string department,
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

            // --------------------------------------------------------
            // REPLACE DATABASE TEMPLATE PLACEHOLDERS
            // --------------------------------------------------------

            var mainContract = template
                .Replace("{{LecturerName}}",
                    WebUtility.HtmlEncode(lecturerName))
                .Replace("{{AcademicRank}}",
                    WebUtility.HtmlEncode(academicRank ?? "N/A"))
                .Replace("{{GovernmentId}}",
                    WebUtility.HtmlEncode(
                        string.IsNullOrWhiteSpace(governmentId)
                            ? "On file with UNILAK"
                            : governmentId))
                .Replace("{{Department}}",
                    WebUtility.HtmlEncode(department))
                .Replace("{{Intake}}",
                    WebUtility.HtmlEncode(session))
                .Replace("{{Session}}",
                    WebUtility.HtmlEncode(session))
                .Replace("{{CourseTitle}}",
                    WebUtility.HtmlEncode(
                        $"{courseCode} — {courseTitle}"))
                .Replace("{{AcademicYear}}",
                    WebUtility.HtmlEncode(academicYear))
                .Replace("{{Semester}}",
                    WebUtility.HtmlEncode(semester))
                .Replace("{{Campus}}",
                    WebUtility.HtmlEncode(campus))
                .Replace("{{AllocatedHours}}",
                    allocatedHours.ToString("0.##"))
                .Replace("{{HourlyRate}}",
                    $"{hourlyRate:N0} RWF")
                .Replace("{{NumberOfOnlineClasses}}",
                    "0")
                .Replace("{{OnlineHours}}",
                    "0");

            // --------------------------------------------------------
            // REMOVE THE ORIGINAL SIGNATURE SECTION
            // --------------------------------------------------------

            var signatureIndex =
                mainContract.IndexOf(
                    "SIGNATURES",
                    StringComparison.OrdinalIgnoreCase);

            if (signatureIndex >= 0)
            {
                mainContract =
                    mainContract.Substring(
                        0,
                        signatureIndex);
            }

            // --------------------------------------------------------
            // BUILD RENDERED HTML DOCUMENT
            // --------------------------------------------------------

            var html = new StringBuilder();

            html.AppendLine(
                "<div class=\"contract-content\">");

            // --------------------------------------------------------
            // UNILAK HEADER
            // --------------------------------------------------------

            html.AppendLine(
                "<div class=\"contract-header\">");

            html.AppendLine(
                "<img src=\"/images/PNG_LOGO-_UNILAK-removebg-preview.png\" " +
                "alt=\"UNILAK Logo\" class=\"contract-logo\" />");

            html.AppendLine(
                "<div class=\"university-name\">" +
                "UNIVERSITY OF LAY ADVENTISTS OF KIGALI" +
                "</div>");

            html.AppendLine(
                "<div>PO Box 6392 Kigali, Rwanda</div>");

            html.AppendLine(
                "<div>Phone: +250(0)731743439 / +250(0)751743431</div>");

            html.AppendLine(
                "<div>Website: www.unilak.ac.rw, " +
                "E-mail: info@unilak.ac.rw</div>");

            html.AppendLine(
                "</div>");

            // --------------------------------------------------------
            // DATE
            // --------------------------------------------------------

            html.AppendLine(
                $"<p class=\"contract-date\">" +
                $"Kigali, {contractDate.ToLocalTime():dd MMMM yyyy}" +
                $"</p>");

            // --------------------------------------------------------
            // TITLE
            // --------------------------------------------------------

            html.AppendLine(
                "<h1>EMPLOYMENT PART-TIME CONTRACT</h1>");

            // --------------------------------------------------------
            // CONTRACT BODY
            // --------------------------------------------------------

            var blocks =
                mainContract
                    .Replace("\r\n", "\n")
                    .Split(
                        new[] { "\n\n" },
                        StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawBlock in blocks)
            {
                var block =
                    rawBlock.Trim();

                if (string.IsNullOrWhiteSpace(block))
                    continue;

                // Skip the original database header/title.
                if (block.Contains(
                        "UNIVERSITY OF LAY ADVENTISTS OF KIGALI",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (block.StartsWith(
                        "PO Box",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (block.StartsWith(
                        "Phone:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (block.StartsWith(
                        "Website:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (block.StartsWith(
                        "Kigali,",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (block.Equals(
                        "EMPLOYMENT PART-TIME CONTRACT",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // ----------------------------------------------------
                // ARTICLE HEADINGS
                // ----------------------------------------------------

                if (block.StartsWith(
                        "ARTICLE ",
                        StringComparison.OrdinalIgnoreCase))
                {
                    html.AppendLine(
                        $"<h3>{WebUtility.HtmlEncode(block)}</h3>");

                    continue;
                }

                // ----------------------------------------------------
                // BULLET LISTS
                // ----------------------------------------------------

                var lines =
                    block
                        .Replace("\r\n", "\n")
                        .Split(
                            '\n',
                            StringSplitOptions.RemoveEmptyEntries);

                var bulletLines = new List<string>();

                foreach (var line in lines)
                {
                    var trimmedLine =
                        line.Trim();

                    if (trimmedLine.StartsWith("-"))
                    {
                        bulletLines.Add(
                            trimmedLine
                                .TrimStart('-')
                                .Trim());
                    }
                }

                if (bulletLines.Count > 0)
                {
                    html.AppendLine("<ul>");

                    foreach (var bullet in bulletLines)
                    {
                        html.AppendLine(
                            $"<li>{WebUtility.HtmlEncode(bullet)}</li>");
                    }

                    html.AppendLine("</ul>");

                    continue;
                }

                // ----------------------------------------------------
                // NORMAL PARAGRAPH
                // ----------------------------------------------------

                var paragraph =
                    string.Join(
                        " ",
                        lines.Select(
                            line => line.Trim()));

                html.AppendLine(
                    $"<p>{WebUtility.HtmlEncode(paragraph)}</p>");
            }

            // --------------------------------------------------------
            // SIGNATURE TABLE
            // --------------------------------------------------------

            html.AppendLine(
                "<h3>SIGNATURES</h3>");

            html.AppendLine(
                "<table class=\"signature-table\">");

            html.AppendLine("<thead>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Signatory</th>");
            html.AppendLine("<th>Name</th>");
            html.AppendLine("<th>Signature</th>");
            html.AppendLine("<th>Date</th>");
            html.AppendLine("</tr>");
            html.AppendLine("</thead>");

            html.AppendLine("<tbody>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Lecturer</td>");
            html.AppendLine(
                $"<td>{WebUtility.HtmlEncode(lecturerName)}</td>");
            html.AppendLine(
                "<td>{{LecturerSignature}}</td>");
            html.AppendLine(
                "<td>{{LecturerSignatureDate}}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Dean</td>");
            html.AppendLine("<td>Dean</td>");
            html.AppendLine(
                "<td>{{DeanSignature}}</td>");
            html.AppendLine(
                "<td>{{DeanSignatureDate}}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Human Resource Officer</td>");
            html.AppendLine("<td>Human Resource Officer</td>");
            html.AppendLine(
                "<td>{{HRSignature}}</td>");
            html.AppendLine(
                "<td>{{HRSignatureDate}}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>DVCAR</td>");
            html.AppendLine("<td>DVCAR</td>");
            html.AppendLine(
                "<td>{{DVCARSignature}}</td>");
            html.AppendLine(
                "<td>{{DVCARSignatureDate}}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("<tr>");
            html.AppendLine("<td>Vice Chancellor</td>");
            html.AppendLine("<td>Vice Chancellor</td>");
            html.AppendLine(
                "<td>{{VCSignature}}</td>");
            html.AppendLine(
                "<td>{{VCSignatureDate}}</td>");
            html.AppendLine("</tr>");

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");

            // --------------------------------------------------------
            // APPROVAL SEQUENCE
            // --------------------------------------------------------

            html.AppendLine(
                "<p class=\"approval-sequence\">" +
                "<strong>CONTRACT APPROVAL SEQUENCE</strong> " +
                "Lecturer → Dean → Human Resource Officer → " +
                "DVCAR → Vice Chancellor" +
                "</p>");

            // --------------------------------------------------------
            // FOOTER
            // --------------------------------------------------------

            html.AppendLine(
                "<div class=\"contract-footer\">" +
                "University of Lay Adventists of Kigali " +
                "Academic Staff Engagement Claim Processing System" +
                "</div>");

            html.AppendLine("</div>");

            return html.ToString();
        }
    }
}


