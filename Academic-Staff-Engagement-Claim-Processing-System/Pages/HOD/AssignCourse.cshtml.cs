using System.Net;
using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class AssignCourseModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;

        public AssignCourseModel(
            ApplicationDbContext context,
            AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        // ============================================================
        // FORM FIELDS
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
        // LIGHTWEIGHT LECTURER DATA
        // ============================================================

        public sealed class LecturerOption
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

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
            // BASIC VALIDATION
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
            //
            // Deliberately project only the fields required here.
            // This prevents GovernmentIdEncrypted from being
            // materialized and decrypted.
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
            // DUPLICATE ASSIGNMENT CHECK
            // ========================================================

            var normalizedAcademicYear = AcademicYear.Trim();

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
                    "This lecturer has already been assigned this " +
                    "course for the selected academic year and semester.";

                return Page();
            }

            // ========================================================
            // DETERMINE HOURLY RATE
            // ========================================================

            HourlyRate = GetRateForRank(lecturer.Rank);

            // ========================================================
            // CREATE COURSE ASSIGNMENT
            // ========================================================

            var assignment = new CourseAssignment
            {
                LecturerId = lecturer.Id,
                CourseId = course.Id,

                AcademicYear = normalizedAcademicYear,

                Semester = Semester.Value,
                Session = Session.Value,
                Campus = Campus.Value,

                AllocatedHours = AllocatedHours,

                IsApproved = false,
                IsActive = true,

                CreatedAtUtc = DateTime.UtcNow
            };

            _context.CourseAssignments.Add(assignment);

            await _context.SaveChangesAsync();

            // ========================================================
            // CREATE CONTRACT HTML SNAPSHOT
            // ========================================================
            //
            // IMPORTANT:
            //
            // TemplateSeeder is NOT used here.
            //
            // The actual contract stored in Contract.Content comes
            // from BuildContractHtml().
            // ========================================================

            var contractDate = DateTime.UtcNow;

            var content = BuildContractHtml(
                contractDate,
                lecturer.UserName,
                lecturer.Rank?.ToString(),
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

            var contract = new Contract(0, "1.0")
            {
                LecturerId = lecturer.Id,
                CourseAssignmentId = assignment.Id,

                Content = content,

                RatePerHour = HourlyRate,

                StartDateUtc = contractDate,

                Status = ContractStatus.PendingSignature
            };

            _context.Contracts.Add(contract);

            await _context.SaveChangesAsync();

            // ========================================================
            // CREATE STRICT SEQUENTIAL SIGNATURE WORKFLOW
            // ========================================================
            //
            // 1 Lecturer
            // 2 Dean
            // 3 HR Officer
            // 4 DVCAR
            // 5 Vice Chancellor
            //
            // There is NO parallel signing here.
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
            // AUDIT
            // ========================================================

            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int parsedActorId);

            await _auditLogger.LogAsync(
                AuditAction.CourseAssigned,
                User.Identity?.Name ?? "Unknown",
                User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown",
                parsedActorId > 0 ? parsedActorId : null,
                "CourseAssignment",
                assignment.Id,
                $"{course.Code} assigned to {lecturer.UserName} " +
                $"({normalizedAcademicYear}, {Semester.Value}, " +
                $"{AllocatedHours}h at {HourlyRate:N0} RWF/hour)",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            // ========================================================
            // CONTRACT PREVIEW
            // ========================================================

            return RedirectToPage(
                "./ContractPreview",
                new
                {
                    ContractId = contract.Id
                });
        }

        // ============================================================
        // BUILD OFFICIAL CONTRACT HTML
        // ============================================================

        private string BuildContractHtml(
            DateTime contractDate,
            string? lecturerName,
            string? academicRank,
            string? department,
            string? session,
            string? courseCode,
            string? courseTitle,
            string? academicYear,
            string? semester,
            string? campus,
            decimal allocatedHours,
            decimal hourlyRate)
        {
            static string E(string? value)
            {
                return WebUtility.HtmlEncode(value ?? string.Empty);
            }

            var lecturer = E(lecturerName);
            var rank = E(academicRank ?? "Not specified");

            var dept = E(department);
            var sess = E(session);

            var course = E(courseCode);
            var title = E(courseTitle);

            var year = E(academicYear);
            var sem = E(semester);
            var campusName = E(campus);

            var date =
                contractDate.ToString("dd MMMM yyyy");

            var hours =
                allocatedHours.ToString("0.##");

            var rate =
                hourlyRate.ToString("N0") + " RWF";

            return $"""
<div class="official-contract">

    <!-- =========================================================
         UNILAK OFFICIAL LETTERHEAD
         ========================================================= -->

    <div class="contract-header">

        <img src="/images/PNG_LOGO-_UNILAK-removebg-preview.png"
             alt="UNILAK Logo"
             class="contract-logo" />

        <div class="contract-university-name">
            UNIVERSITY OF LAY ADVENTISTS OF KIGALI
        </div>

        <div class="contract-address">
            PO Box 6392 Kigali, Rwanda
        </div>

        <div class="contract-contact">
            Phone: +250(0)731743439 / +250(0)751743431
        </div>

        <div class="contract-web">
            Website: www.unilak.ac.rw
            &nbsp;&nbsp;&nbsp;&nbsp;
            E-mail: info@unilak.ac.rw
        </div>

    </div>

    <div class="contract-header-line"></div>


    <!-- =========================================================
         DATE
         ========================================================= -->

    <div class="contract-date">
        Kigali, {date}
    </div>


    <!-- =========================================================
         TITLE
         ========================================================= -->

    <div class="contract-title-section">

        <h1>
            EMPLOYMENT PART-TIME CONTRACT
        </h1>

    </div>


    <!-- =========================================================
         PARTIES
         ========================================================= -->

    <div class="contract-section">

        <p>
            Between the undersigned:
        </p>

        <p>
            University of Lay Adventists of Kigali (UNILAK)
            represented by Vice Chancellor
            <strong>Prof. Jean NGAMIJE</strong> on one hand,
        </p>

        <p>
            And the Employee,
            <strong>{lecturer}</strong>,
            having the Academic rank of
            <strong>{rank}</strong>
            with identity card/Passport No:
            <strong>On file with UNILAK</strong>
            on other hand;
        </p>

        <p>
            The following has been agreed:
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 1
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 1</h2>

        <p>
            UNILAK employs <strong>{lecturer}</strong> as
            External/Internal part time lecturer in the faculty of
            Computing and Information Sciences Department of
            <strong>{dept}</strong>, Intake <strong>N/A</strong>,
            Session <strong>{sess}</strong> to teach the course of
            <strong>{course} — {title}</strong>,
            Academic year <strong>{year}</strong>,
            semester <strong>{sem}</strong>,
            <strong>{campusName}</strong> Campus.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 2
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 2</h2>

        <p>
            The number of contact hours allocated to the course/module
            if the course is taught through face-to-face mode is
            <strong>{hours}</strong> hours and this include the theory,
            practical as well as examinations. The rate per hour will
            be <strong>{rate}</strong> (gross).
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 3
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 3</h2>

        <p>
            The numbers of classes combined if the module/course is
            taught through online teaching mode: <strong>0</strong>
            and the total number of hours allocated to those combined
            classes taught by one academic staff:
            <strong>0</strong>.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 4
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 4</h2>

        <p>
            The employee is required to hand into the Deputy Vice
            Chancellor for Academic and Research office his/her
            application letter, CV, notarized copy of the degree/,
            Equivalence if the degree is offered from foreigner
            countries, as well as his/her nomination papers for his
            previous academic rank.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 5
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 5</h2>

        <p>
            The Lecturer is required to submit to the Head of the
            Department the following documents:
        </p>

        <ul>

            <li>
                Course materials such as Handout/syllabuses and other
                supporting documents must be uploaded to UNILAK online
                teaching platform and submitted to the Head of
                department office before starting the class;
            </li>

            <li>
                Final exam and marking scheme;
            </li>

            <li>
                Continuous assessment papers:
                assignments/quiz/test.
            </li>

        </ul>

    </div>


    <!-- =========================================================
         ARTICLE 6
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 6</h2>

        <p>
            The sheet of marks properly recorded should be submitted
            within fifteen days dating from the time of exam, in case
            of urgency the institution is entitled to short this
            deadline.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 7
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 7</h2>

        <p>
            Any teaching staff member is evaluated at the end of the
            course and at the end of academic year by the hierarchy
            based on:
        </p>

        <ul>

            <li>
                His/her scientific competence
                (his/her handling of the course contents,
                scientific articles and papers publishing);
            </li>

            <li>
                His/her pedagogic competence
                (methodology techniques, and strategies applied in
                transmitting efficiently the course contents);
            </li>

            <li>
                His/her moral aptitudes
                (punctuality, objectivity, sense of responsibility,
                commitment to students' education, etc…);
            </li>

        </ul>

        <p>
            In order to maintain or keep his/her course, a teacher
            must get at least <strong>70%</strong> of mark of the
            evaluation done by hierarchy.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 8
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 8</h2>

        <p>
            A non-informed absence (or late informed) brings prejudice
            to the students in many regards, disturbs the functioning
            of the teaching activities and seriously spoils the
            reputation of the institution cannot be tolerated.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 9
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 9</h2>

        <p>
            The wage of the part-time employee will be set in
            accordance with his/her Academic rank.
        </p>

    </div>


    <!-- =========================================================
         ARTICLE 10
         ========================================================= -->

    <div class="contract-article">

        <h2>Article 10</h2>

        <p>
            Each party may terminate the appointment by giving to the
            other party 15 days Notice in writing. However, the
            University reserves the right to cancel the present
            contract without prior notice in case the employee seems
            to be inefficient, immoral, or absent without informing
            the HOD.
        </p>

    </div>


    <!-- =========================================================
         SIGNATURES
         ========================================================= -->

    <div class="contract-signatures">

        <h2>
            SIGNATURES
        </h2>

        <table class="signature-table">

            <thead>

                <tr>

                    <th>
                        Signatory
                    </th>

                    <th>
                        Name
                    </th>

                    <th>
                        Signature
                    </th>

                    <th>
                        Date
                    </th>

                </tr>

            </thead>

            <tbody>

                <tr>

                    <td>
                        <strong>Lecturer</strong>
                    </td>

                    <td>
                        {lecturer}
                    </td>

                    <td class="signature-placeholder">
                        Pending electronic signature
                    </td>

                    <td>
                        Pending
                    </td>

                </tr>

                <tr>

                    <td>
                        <strong>Dean of Faculty</strong>
                    </td>

                    <td>
                        Prof. NYESHEJA M. Enan
                    </td>

                    <td class="signature-placeholder">
                        Pending electronic signature
                    </td>

                    <td>
                        Pending
                    </td>

                </tr>

                <tr>

                    <td>
                        <strong>Human Resource Officer</strong>
                    </td>

                    <td>
                        Mr. NTAKIRUTIMANA Elison
                    </td>

                    <td class="signature-placeholder">
                        Pending electronic signature
                    </td>

                    <td>
                        Pending
                    </td>

                </tr>

                <tr>

                    <td>
                        <strong>DVCAR</strong>
                    </td>

                    <td>
                        Prof. HAKIZIMANA Emmanuel
                    </td>

                    <td class="signature-placeholder">
                        Pending electronic signature
                    </td>

                    <td>
                        Pending
                    </td>

                </tr>

                <tr>

                    <td>
                        <strong>Vice Chancellor</strong>
                    </td>

                    <td>
                        Prof. NGAMIJE Jean
                    </td>

                    <td class="signature-placeholder">
                        Pending electronic signature
                    </td>

                    <td>
                        Pending
                    </td>

                </tr>

            </tbody>

        </table>

    </div>


    <!-- =========================================================
         WORKFLOW NOTICE
         ========================================================= -->

    <div class="contract-workflow-notice">

        <strong>
            CONTRACT APPROVAL SEQUENCE
        </strong>

        <span>
            Lecturer → Dean → Human Resource Officer →
            DVCAR → Vice Chancellor
        </span>

    </div>


    <!-- =========================================================
         DOCUMENT FOOTER
         ========================================================= -->

    <div class="contract-document-footer">

        <span>
            University of Lay Adventists of Kigali
        </span>

        <span>
            Academic Staff Engagement Claim Processing System
        </span>

    </div>

</div>
""";
        }

        // ============================================================
        // LOAD PAGE DATA
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
                    Rank = l.Rank
                })
                .ToListAsync();
        }

        // ============================================================
        // LECTURER DISPLAY NAME
        // ============================================================

        private static string GetLecturerDisplayName(
            LecturerOption lecturer)
        {
            if (!string.IsNullOrWhiteSpace(lecturer.UserName))
                return lecturer.UserName;

            return "Lecturer #" + lecturer.Id;
        }

        // ============================================================
        // HOURLY RATE
        // ============================================================
        //
        // These are the rates currently represented by this project.
        // If the database later gets a dedicated Wage/HourlyRate field,
        // this method should be replaced with a database lookup.
        // ============================================================

        private static decimal GetRateForRank(LecturerRank? rank)
        {
            if (!rank.HasValue)
                return 5000m;

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