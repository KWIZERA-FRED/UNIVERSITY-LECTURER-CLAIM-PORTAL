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
        //
        // IMPORTANT:
        // We deliberately do NOT load GovernmentIdEncrypted here.
        //
        // Loading the complete Lecturer entity can cause EF Core to
        // materialize the encrypted Government ID and invoke the
        // GovernmentIdProtector.Decrypt() logic.
        //
        // The assignment process only needs:
        // Id
        // UserName
        // Rank
        // ============================================================

        public class LecturerOption
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
            // --------------------------------------------------------
            // LOAD DROPDOWN DATA
            // --------------------------------------------------------

            await LoadDataAsync();

            // --------------------------------------------------------
            // COURSE VALIDATION
            // --------------------------------------------------------

            if (!SelectedCourse.HasValue)
            {
                ErrorMessage = "Please select a course.";
                return Page();
            }

            // --------------------------------------------------------
            // LECTURER VALIDATION
            // --------------------------------------------------------

            if (!SelectedLecturer.HasValue)
            {
                ErrorMessage = "Please select a lecturer.";
                return Page();
            }

            // --------------------------------------------------------
            // ACADEMIC YEAR VALIDATION
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage = "Please select an academic year.";
                return Page();
            }

            // --------------------------------------------------------
            // SEMESTER VALIDATION
            // --------------------------------------------------------

            if (!Semester.HasValue)
            {
                ErrorMessage = "Please select a semester.";
                return Page();
            }

            // --------------------------------------------------------
            // SESSION VALIDATION
            // --------------------------------------------------------

            if (!Session.HasValue)
            {
                ErrorMessage = "Please select a session.";
                return Page();
            }

            // --------------------------------------------------------
            // CAMPUS VALIDATION
            // --------------------------------------------------------

            if (!Campus.HasValue)
            {
                ErrorMessage = "Please select a campus.";
                return Page();
            }

            // --------------------------------------------------------
            // HOURS VALIDATION
            // --------------------------------------------------------

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

            if (course == null)
            {
                ErrorMessage =
                    "Selected course could not be found.";

                return Page();
            }

            // ========================================================
            // FIND LECTURER WITHOUT LOADING GOVERNMENT ID
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

            if (lecturer == null)
            {
                ErrorMessage =
                    "Selected lecturer could not be found.";

                return Page();
            }

            // ========================================================
            // CHECK FOR DUPLICATE ASSIGNMENT
            // ========================================================

            var existingAssignment =
                await _context.CourseAssignments
                    .AnyAsync(ca =>
                        ca.LecturerId == lecturer.Id &&
                        ca.CourseId == course.Id &&
                        ca.AcademicYear == AcademicYear &&
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

                AcademicYear = AcademicYear.Trim(),

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
            // The contract is stored as structured HTML.
            //
            // This is important because the HOD contract preview
            // renders Contract.Content directly.
            //
            // We therefore do NOT store the contract as one large
            // plain-text block.
            //
            // The HTML is generated server-side and all dynamic
            // values are HTML encoded by BuildContractHtml().
            // ========================================================

            var contractDate = DateTime.UtcNow;

            var content = BuildContractHtml(
                contractDate: contractDate,
                lecturerName: lecturer.UserName,
                academicRank: lecturer.Rank?.ToString() ?? "Not specified",
                department: course.Department,
                session: assignment.Session.ToString(),
                courseCode: course.Code,
                courseTitle: course.Title,
                academicYear: assignment.AcademicYear,
                semester: assignment.Semester.ToString(),
                campus: assignment.Campus.ToString(),
                allocatedHours: assignment.AllocatedHours,
                hourlyRate: HourlyRate);

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
            // CREATE STRICT SIGNATURE WORKFLOW
            // ========================================================
            //
            // REQUIRED ORDER:
            //
            // 1. Lecturer
            // 2. Dean
            // 3. HR Officer
            // 4. DVCAR
            // 5. Vice Chancellor
            //
            // IMPORTANT:
            // Every signer has a unique SequenceOrder.
            //
            // This prevents Dean and HR from signing in parallel.
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
            // AUDIT LOG
            // ========================================================

            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int parsedActorId);

            await _auditLogger.LogAsync(
                AuditAction.CourseAssigned,
                User.Identity?.Name ?? "Unknown",
                User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown",
                parsedActorId > 0 ? parsedActorId : (int?)null,
                "CourseAssignment",
                assignment.Id,
                $"{course.Code} assigned to {lecturer.UserName} " +
                $"({AcademicYear}, {Semester.Value}, {AllocatedHours}h)",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            // ========================================================
            // REDIRECT TO CONTRACT PREVIEW
            // ========================================================

            return RedirectToPage(
                "./ContractPreview",
                new { ContractId = contract.Id });
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        private async Task LoadDataAsync()
        {
            // --------------------------------------------------------
            // COURSES
            // --------------------------------------------------------

            Courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync();

            // --------------------------------------------------------
            // LECTURERS
            // --------------------------------------------------------
            //
            // DO NOT change this to:
            //
            // _context.Lecturers.ToListAsync()
            //
            // because that can cause EF Core to read
            // GovernmentIdEncrypted.
            // --------------------------------------------------------

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

        private string GetLecturerDisplayName(
            LecturerOption lecturer)
        {
            if (!string.IsNullOrWhiteSpace(lecturer.UserName))
            {
                return lecturer.UserName;
            }

            return "Lecturer #" + lecturer.Id;
        }

        // ============================================================
        // RATE CALCULATION
        // ============================================================

        private decimal GetRateForRank(LecturerRank? rank)
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

        // ============================================================
        // BUILD CONTRACT HTML
        // ============================================================
        //
        // This method creates the formal HTML contract stored in the
        // Contract.Content column.
        //
        // IMPORTANT:
        // Dynamic values are HTML encoded before being inserted.
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
            string E(string? value)
            {
                return WebUtility.HtmlEncode(value ?? string.Empty);
            }

            var lecturer = E(lecturerName);
            var rank = E(academicRank);
            var dept = E(department);
            var course = E(courseCode);
            var title = E(courseTitle);
            var year = E(academicYear);
            var sem = E(semester);
            var sess = E(session);
            var campusName = E(campus);

            var date = contractDate.ToString("dd MMMM yyyy");

            var hours = allocatedHours.ToString("0.##");

            var rate = hourlyRate.ToString("N0") + " RWF";

            return $"""
<div class="official-contract">

    <!-- =========================================================
         UNIVERSITY HEADER
         ========================================================= -->

    <div class="contract-header">

        <div class="contract-university">
            <div class="contract-university-name">
                UNIVERSITY OF LAY ADVENTISTS OF KIGALI
            </div>

            <div class="contract-address">
                P.O. Box 6392, Kigali, Rwanda
            </div>

            <div class="contract-document-type">
                ACADEMIC STAFF ENGAGEMENT CLAIM PROCESSING SYSTEM
            </div>
        </div>

    </div>

    <div class="contract-header-line"></div>

    <!-- =========================================================
         DOCUMENT TITLE
         ========================================================= -->

    <div class="contract-title-section">

        <h1>EMPLOYMENT PART-TIME CONTRACT</h1>

        <p class="contract-reference">
            Contract Reference:
            <strong>Generated upon approval</strong>
        </p>

    </div>

    <!-- =========================================================
         CONTRACT DATE
         ========================================================= -->

    <div class="contract-date">
        Kigali, {date}
    </div>

    <!-- =========================================================
         INTRODUCTION
         ========================================================= -->

    <div class="contract-section">

        <p>
            This Employment Part-Time Contract is made between the
            <strong>University of Lay Adventists of Kigali (UNILAK)</strong>,
            represented by the authorized University administration,
            hereinafter referred to as "the University", and
            <strong>{lecturer}</strong>,
            hereinafter referred to as "the Lecturer".
        </p>

        <p>
            The Lecturer is engaged to provide academic teaching and
            related academic services in accordance with the terms,
            conditions, policies and procedures of the University.
        </p>

    </div>

    <!-- =========================================================
         APPOINTMENT DETAILS
         ========================================================= -->

    <div class="contract-section">

        <h2>APPOINTMENT DETAILS</h2>

        <table class="contract-details-table">

            <tbody>

                <tr>
                    <th>Lecturer Name</th>
                    <td>{lecturer}</td>
                </tr>

                <tr>
                    <th>Academic Rank</th>
                    <td>{rank}</td>
                </tr>

                <tr>
                    <th>Department</th>
                    <td>{dept}</td>
                </tr>

                <tr>
                    <th>Course Code</th>
                    <td>{course}</td>
                </tr>

                <tr>
                    <th>Course Title</th>
                    <td>{title}</td>
                </tr>

                <tr>
                    <th>Academic Year</th>
                    <td>{year}</td>
                </tr>

                <tr>
                    <th>Semester</th>
                    <td>{sem}</td>
                </tr>

                <tr>
                    <th>Session</th>
                    <td>{sess}</td>
                </tr>

                <tr>
                    <th>Campus</th>
                    <td>{campusName}</td>
                </tr>

                <tr>
                    <th>Allocated Teaching Hours</th>
                    <td>{hours} hours</td>
                </tr>

                <tr>
                    <th>Hourly Rate</th>
                    <td>{rate}</td>
                </tr>

            </tbody>

        </table>

    </div>

    <!-- =========================================================
         ARTICLE 1
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 1 — APPOINTMENT</h2>

        <p>
            The University appoints the above-named Lecturer to teach
            <strong>{course} — {title}</strong>
            during the <strong>{year}</strong> academic year,
            <strong>{sem}</strong> semester,
            <strong>{sess}</strong> session,
            at the <strong>{campusName}</strong> Campus.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 2
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 2 — TEACHING RESPONSIBILITIES</h2>

        <p>
            The Lecturer shall conduct the assigned teaching activities,
            prepare and deliver appropriate course materials, attend
            scheduled classes, guide students in their academic work,
            participate in assessment activities and perform other
            academic responsibilities assigned by the University.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 3
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 3 — WORKLOAD</h2>

        <p>
            The Lecturer is allocated
            <strong>{hours} contact hours</strong>
            for the assigned course. The Lecturer shall complete the
            allocated workload in accordance with the approved academic
            timetable and University requirements.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 4
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 4 — ACADEMIC MATERIALS</h2>

        <p>
            The Lecturer shall prepare and provide the required course
            materials, lesson plans, assessment instruments and other
            academic documents required for effective delivery of the
            assigned course.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 5
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 5 — ASSESSMENT AND MARKS</h2>

        <p>
            The Lecturer shall participate in student assessment and
            shall submit marks, marking schemes, continuous assessment
            records and other academic records through the University's
            approved academic processes within the prescribed deadlines.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 6
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 6 — ATTENDANCE AND PROFESSIONAL CONDUCT</h2>

        <p>
            The Lecturer shall observe the approved teaching timetable,
            maintain professional conduct, attend assigned academic
            activities and comply with all applicable University policies,
            regulations and procedures.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 7
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 7 — ACADEMIC QUALITY</h2>

        <p>
            The Lecturer shall maintain appropriate academic and
            professional standards and shall cooperate with academic
            supervision, evaluation and quality assurance activities
            conducted by the University.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 8
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 8 — ABSENCE AND NON-COMPLIANCE</h2>

        <p>
            Unauthorised absence, persistent lateness, failure to perform
            assigned academic duties or failure to comply with University
            requirements may result in appropriate administrative action
            in accordance with University policy and applicable law.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 9
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 9 — REMUNERATION</h2>

        <p>
            The Lecturer shall be remunerated at the approved rate of
            <strong>{rate} per teaching hour</strong>, subject to the
            University's financial procedures and verification of the
            academic work performed.
        </p>

    </div>

    <!-- =========================================================
         ARTICLE 10
         ========================================================= -->

    <div class="contract-article">

        <h2>ARTICLE 10 — TERMINATION</h2>

        <p>
            This contract may be terminated by either party in accordance
            with applicable University policies, the terms of this
            contract and applicable law. The University reserves the
            right to take appropriate action where the Lecturer fails
            to meet the obligations established under this contract.
        </p>

    </div>

    <!-- =========================================================
         DECLARATION
         ========================================================= -->

    <div class="contract-declaration">

        <h2>DECLARATION</h2>

        <p>
            By signing this contract, the parties acknowledge that they
            have read, understood and agreed to the terms and conditions
            contained herein. The Lecturer further agrees to comply with
            the academic, administrative and professional requirements
            of the University.
        </p>

    </div>

    <!-- =========================================================
         SIGNATURES
         ========================================================= -->

    <div class="contract-signatures">

        <h2>SIGNATURES AND APPROVALS</h2>

        <p class="signature-introduction">
            This contract shall become active only after completion of
            the University's prescribed sequential approval process.
        </p>

        <table class="signature-table">

            <thead>

                <tr>
                    <th>No.</th>
                    <th>Authorized Signatory</th>
                    <th>Signature</th>
                    <th>Date</th>
                </tr>

            </thead>

            <tbody>

                <tr>
                    <td>1</td>
                    <td>
                        <strong>Lecturer</strong>
                        <br />
                        <span>{lecturer}</span>
                    </td>
                    <td class="signature-line">
                        ______________________________
                    </td>
                    <td>
                        __________________
                    </td>
                </tr>

                <tr>
                    <td>2</td>
                    <td>
                        <strong>Dean</strong>
                    </td>
                    <td class="signature-line">
                        ______________________________
                    </td>
                    <td>
                        __________________
                    </td>
                </tr>

                <tr>
                    <td>3</td>
                    <td>
                        <strong>Human Resource Officer</strong>
                    </td>
                    <td class="signature-line">
                        ______________________________
                    </td>
                    <td>
                        __________________
                    </td>
                </tr>

                <tr>
                    <td>4</td>
                    <td>
                        <strong>Deputy Vice Chancellor for Academic
                        Research</strong>
                        <br />
                        <span>DVCAR</span>
                    </td>
                    <td class="signature-line">
                        ______________________________
                    </td>
                    <td>
                        __________________
                    </td>
                </tr>

                <tr>
                    <td>5</td>
                    <td>
                        <strong>Vice Chancellor</strong>
                    </td>
                    <td class="signature-line">
                        ______________________________
                    </td>
                    <td>
                        __________________
                    </td>
                </tr>

            </tbody>

        </table>

    </div>

    <!-- =========================================================
         WORKFLOW NOTICE
         ========================================================= -->

    <div class="contract-workflow-notice">

        <strong>Sequential Approval Requirement</strong>

        <p>
            Lecturer approval must be completed before the contract is
            forwarded to the Dean. The Dean must approve before it is
            forwarded to the Human Resource Officer. HR approval must
            precede DVCAR approval, and DVCAR approval must precede
            Vice Chancellor approval.
        </p>

    </div>

    <!-- =========================================================
         FOOTER
         ========================================================= -->

    <div class="contract-document-footer">

        <div>
            University of Lay Adventists of Kigali
        </div>

        <div>
            Academic Staff Engagement Claim Processing System
        </div>

    </div>

</div>
""";
        }
    }
}