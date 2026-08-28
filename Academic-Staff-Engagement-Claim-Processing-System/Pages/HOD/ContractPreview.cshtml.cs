using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class ContractPreviewModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ContractPreviewModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // COURSE ASSIGNMENT
        // ============================================================

        [BindProperty(SupportsGet = true)]
        public int CourseAssignmentId { get; set; }

        // ============================================================
        // CONTRACT
        // ============================================================

        public string ContractNumber { get; private set; } = string.Empty;

        public string ContractDate { get; private set; } = string.Empty;

        public string ContractHtml { get; private set; } = string.Empty;

        // ============================================================
        // LECTURER
        // ============================================================

        public string LecturerName { get; private set; } = "Not specified";

        public string LecturerEmail { get; private set; } = "Not specified";

        public string GovernmentId { get; private set; } = string.Empty;

        public string AcademicRank { get; private set; } = "Not specified";

        // ============================================================
        // COURSE / ASSIGNMENT
        // ============================================================

        public string Department { get; private set; } = "Not specified";

        public string CourseCode { get; private set; } = "Not specified";

        public string CourseTitle { get; private set; } = "Not specified";

        public string AcademicYear { get; private set; } = "Not specified";

        public string Semester { get; private set; } = "Not specified";

        public string Session { get; private set; } = "Not specified";

        public string Campus { get; private set; } = "Not specified";

        // ============================================================
        // TEMPLATE FIELDS
        // ============================================================

        public string Intake { get; private set; } = "Not specified";

        public decimal AllocatedHours { get; private set; }

        public decimal HourlyRate { get; private set; }

        public decimal NumberOfOnlineClasses { get; private set; }

        public decimal OnlineHours { get; private set; }

        public decimal TotalAmount =>
            AllocatedHours * HourlyRate;

        // ============================================================
        // ERROR
        // ============================================================

        public string ErrorMessage { get; private set; } = string.Empty;

        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            // --------------------------------------------------------
            // THE ASSIGNMENT WAS CREATED BY ASSIGNCOURSE
            // --------------------------------------------------------

            if (CourseAssignmentId <= 0)
            {
                ErrorMessage =
                    "No course assignment was supplied to the contract preview.";

                return Page();
            }

            // ========================================================
            // LOAD THE COURSE ASSIGNMENT
            // ========================================================

            var assignment = await _context.CourseAssignments
                .AsNoTracking()
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a =>
                    a.Id == CourseAssignmentId &&
                    a.IsActive);

            if (assignment == null)
            {
                ErrorMessage =
                    $"Course assignment #{CourseAssignmentId} could not be found.";

                return Page();
            }

            // ========================================================
            // LOAD LECTURER
            //
            // Projection is intentional.
            //
            // We do NOT load GovernmentIdEncrypted because your
            // existing encrypted records can trigger the protector
            // problem when EF materializes the complete Lecturer entity.
            // ========================================================

            var lecturer = await _context.Lecturers
                .AsNoTracking()
                .Where(l =>
                    l.Id == assignment.LecturerId &&
                    l.IsActive)
                .Select(l => new LecturerPreviewData
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    Rank = l.Rank
                })
                .FirstOrDefaultAsync();

            if (lecturer == null)
            {
                ErrorMessage =
                    "The lecturer belonging to this course assignment could not be found.";

                return Page();
            }

            // ========================================================
            // LOAD CONTRACT TEMPLATE
            //
            // THIS IS THE TEMPLATE INSERTED BY TemplateSeeder.
            //
            // Nothing from TemplateSeeder is placed inside the Razor
            // page.
            // ========================================================

            var template = await _context.Templates
                .AsNoTracking()
                .OrderBy(t => t.Id)
                .FirstOrDefaultAsync();

            if (template == null)
            {
                ErrorMessage =
                    "The contract template could not be found in the database. " +
                    "Make sure TemplateSeeder has been executed.";

                return Page();
            }

            if (string.IsNullOrWhiteSpace(template.Contract))
            {
                ErrorMessage =
                    "The contract template exists in the database, " +
                    "but its Contract content is empty.";

                return Page();
            }

            // ========================================================
            // CONTRACT DATE
            // ========================================================

            ContractDate =
                DateTime.Now.ToString("dd/MM/yyyy");

            // ========================================================
            // CONTRACT NUMBER
            //
            // Uses the CourseAssignment ID that was just created by
            // AssignCourse.
            // ========================================================

            ContractNumber =
                $"UNILAK-{DateTime.Now:yyyyMMdd}-{assignment.Id:D6}";

            // ========================================================
            // LECTURER INFORMATION
            // ========================================================

            LecturerName =
                string.IsNullOrWhiteSpace(lecturer.UserName)
                    ? $"Lecturer #{lecturer.Id}"
                    : lecturer.UserName;

            LecturerEmail =
                string.IsNullOrWhiteSpace(lecturer.Email)
                    ? "Not specified"
                    : lecturer.Email;

            AcademicRank =
                lecturer.Rank?.ToString()
                ?? "Not specified";

            // Government ID is deliberately not read here because
            // AssignCourse intentionally does not load it.
            GovernmentId = string.Empty;

            // ========================================================
            // COURSE INFORMATION
            // ========================================================

            CourseCode =
                assignment.Course?.Code
                ?? "Not specified";

            CourseTitle =
                assignment.Course?.Title
                ?? "Not specified";

            Department =
                assignment.Course?.Department
                ?? "Not specified";

            // ========================================================
            // ASSIGNMENT INFORMATION
            // ========================================================

            AcademicYear =
                string.IsNullOrWhiteSpace(assignment.AcademicYear)
                    ? "Not specified"
                    : assignment.AcademicYear;

            Semester =
                assignment.Semester.ToString();

            Session =
                assignment.Session.ToString();

            Campus =
                assignment.Campus.ToString();

            AllocatedHours =
                assignment.AllocatedHours;

            // ========================================================
            // TEMPLATE VALUES THAT ARE NOT CURRENTLY PART OF
            // CourseAssignment
            // ========================================================
            //
            // The seeded template contains these placeholders:
            //
            // {{Intake}}
            // {{NumberOfOnlineClasses}}
            // {{OnlineHours}}
            //
            // Your current AssignCourse form does not collect them,
            // so we provide safe values rather than inventing data.
            // ========================================================

            Intake = "Not specified";

            NumberOfOnlineClasses = 0;

            OnlineHours = 0;

            // ========================================================
            // HOURLY RATE
            // ========================================================

            HourlyRate =
                GetRateForRank(lecturer.Rank);

            // ========================================================
            // POPULATE THE SEEDED TEMPLATE
            // ========================================================

            string populatedContract =
                PopulateContractTemplate(template.Contract);

            // ========================================================
            // CONVERT TEMPLATE TEXT TO HTML
            // ========================================================

            ContractHtml =
                ConvertToHtml(populatedContract);

            return Page();
        }

        // ============================================================
        // LECTURER PROJECTION
        // ============================================================

        private sealed class LecturerPreviewData
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string? Email { get; set; }

            public LecturerRank? Rank { get; set; }
        }

        // ============================================================
        // POPULATE CONTRACT TEMPLATE
        // ============================================================

        private string PopulateContractTemplate(string template)
        {
            string contract = template;

            // ========================================================
            // DATABASE TEMPLATE PLACEHOLDERS
            // ========================================================

            contract = ReplacePlaceholder(
                contract,
                "{{ContractDate}}",
                ContractDate);

            contract = ReplacePlaceholder(
                contract,
                "{{ContractNumber}}",
                ContractNumber);

            contract = ReplacePlaceholder(
                contract,
                "{{LecturerName}}",
                LecturerName);

            contract = ReplacePlaceholder(
                contract,
                "{{LecturerEmail}}",
                LecturerEmail);

            contract = ReplacePlaceholder(
                contract,
                "{{GovernmentId}}",
                GovernmentId);

            contract = ReplacePlaceholder(
                contract,
                "{{AcademicRank}}",
                AcademicRank);

            contract = ReplacePlaceholder(
                contract,
                "{{Department}}",
                Department);

            contract = ReplacePlaceholder(
                contract,
                "{{CourseCode}}",
                CourseCode);

            contract = ReplacePlaceholder(
                contract,
                "{{CourseTitle}}",
                CourseTitle);

            contract = ReplacePlaceholder(
                contract,
                "{{AcademicYear}}",
                AcademicYear);

            contract = ReplacePlaceholder(
                contract,
                "{{Semester}}",
                Semester);

            contract = ReplacePlaceholder(
                contract,
                "{{Session}}",
                Session);

            contract = ReplacePlaceholder(
                contract,
                "{{Campus}}",
                Campus);

            contract = ReplacePlaceholder(
                contract,
                "{{Intake}}",
                Intake);

            contract = ReplacePlaceholder(
                contract,
                "{{AllocatedHours}}",
                AllocatedHours.ToString("0.##"));

            contract = ReplacePlaceholder(
                contract,
                "{{HourlyRate}}",
                HourlyRate.ToString("N0"));

            contract = ReplacePlaceholder(
                contract,
                "{{NumberOfOnlineClasses}}",
                NumberOfOnlineClasses.ToString("0.##"));

            contract = ReplacePlaceholder(
                contract,
                "{{OnlineHours}}",
                OnlineHours.ToString("0.##"));

            // ========================================================
            // SIGNATURE PLACEHOLDERS
            // ========================================================

            contract = ReplacePlaceholder(
                contract,
                "{{LecturerSignature}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{LecturerSignatureDate}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{DeanSignature}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{DeanSignatureDate}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{HRSignature}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{HRSignatureDate}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{DVCARSignature}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{DVCARSignatureDate}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{VCSignature}}",
                "");

            contract = ReplacePlaceholder(
                contract,
                "{{VCSignatureDate}}",
                "");

            // ========================================================
            // SUPPORT [[...]] PLACEHOLDERS TOO
            // ========================================================

            contract = ReplacePlaceholder(
                contract,
                "[[ContractDate]]",
                ContractDate);

            contract = ReplacePlaceholder(
                contract,
                "[[ContractNumber]]",
                ContractNumber);

            contract = ReplacePlaceholder(
                contract,
                "[[LecturerName]]",
                LecturerName);

            contract = ReplacePlaceholder(
                contract,
                "[[LecturerEmail]]",
                LecturerEmail);

            contract = ReplacePlaceholder(
                contract,
                "[[GovernmentId]]",
                GovernmentId);

            contract = ReplacePlaceholder(
                contract,
                "[[AcademicRank]]",
                AcademicRank);

            contract = ReplacePlaceholder(
                contract,
                "[[Department]]",
                Department);

            contract = ReplacePlaceholder(
                contract,
                "[[CourseCode]]",
                CourseCode);

            contract = ReplacePlaceholder(
                contract,
                "[[CourseTitle]]",
                CourseTitle);

            contract = ReplacePlaceholder(
                contract,
                "[[AcademicYear]]",
                AcademicYear);

            contract = ReplacePlaceholder(
                contract,
                "[[Semester]]",
                Semester);

            contract = ReplacePlaceholder(
                contract,
                "[[Session]]",
                Session);

            contract = ReplacePlaceholder(
                contract,
                "[[Campus]]",
                Campus);

            contract = ReplacePlaceholder(
                contract,
                "[[Intake]]",
                Intake);

            contract = ReplacePlaceholder(
                contract,
                "[[AllocatedHours]]",
                AllocatedHours.ToString("0.##"));

            contract = ReplacePlaceholder(
                contract,
                "[[HourlyRate]]",
                HourlyRate.ToString("N0"));

            contract = ReplacePlaceholder(
                contract,
                "[[NumberOfOnlineClasses]]",
                NumberOfOnlineClasses.ToString("0.##"));

            contract = ReplacePlaceholder(
                contract,
                "[[OnlineHours]]",
                OnlineHours.ToString("0.##"));

            return contract;
        }

        // ============================================================
        // PLACEHOLDER REPLACEMENT
        // ============================================================

        private static string ReplacePlaceholder(
            string source,
            string placeholder,
            string? value)
        {
            return source.Replace(
                placeholder,
                value ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // TEXT TO HTML
        // ============================================================

        private static string ConvertToHtml(string text)
        {
            string encoded =
                WebUtility.HtmlEncode(text);

            encoded = encoded
                .Replace(
                    "\r\n",
                    "<br>");

            encoded = encoded
                .Replace(
                    "\n",
                    "<br>");

            return encoded;
        }

        // ============================================================
        // HOURLY RATE
        // ============================================================

        private static decimal GetRateForRank(
            LecturerRank? rank)
        {
            if (!rank.HasValue)
            {
                return 5000m;
            }

            return rank.Value switch
            {
                LecturerRank.AssistantLecturer => 5000m,

                LecturerRank.Lecturer => 7000m,

                LecturerRank.SeniorLecturer => 9000m,

                LecturerRank.AssociateProfessor => 11000m,

                LecturerRank.Professor => 13000m,

                _ => 5000m
            };
        }
    }
}