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
        // Loading a complete Lecturer entity causes EF Core to invoke
        // GovernmentIdProtector.Decrypt(), which currently fails for
        // existing records encrypted with the missing Data Protection key.
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
            //
            // We only need:
            // Id
            // UserName
            // Rank
            //
            // This projection prevents EF from materializing the
            // encrypted GovernmentIdEncrypted property.
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

            // A course assignment always creates a contract snapshot and its
            // fixed signature sequence. The claim workflow cannot be opened
            // until this exact contract reaches Active status.
            var template = await _context.Templates.AsNoTracking().FirstOrDefaultAsync();
            var content = template?.Contract ?? "UNILAK PART-TIME EMPLOYMENT CONTRACT";
            content = content
                .Replace("{{ContractDate}}", DateTime.UtcNow.ToString("dd MMMM yyyy"))
                .Replace("{{LecturerName}}", lecturer.UserName)
                .Replace("{{AcademicRank}}", lecturer.Rank?.ToString() ?? "Not specified")
                .Replace("{{GovernmentId}}", "On file")
                .Replace("{{Department}}", course.Department)
                .Replace("{{Intake}}", "N/A")
                .Replace("{{Session}}", assignment.Session.ToString())
                .Replace("{{CourseTitle}}", course.Title)
                .Replace("{{AcademicYear}}", assignment.AcademicYear)
                .Replace("{{Semester}}", assignment.Semester.ToString())
                .Replace("{{Campus}}", assignment.Campus.ToString())
                .Replace("{{AllocatedHours}}", assignment.AllocatedHours.ToString("0.##"))
                .Replace("{{HourlyRate}}", HourlyRate.ToString("N0") + " RWF")
                .Replace("{{NumberOfOnlineClasses}}", "0")
                .Replace("{{OnlineHours}}", "0")
                .Replace("{{LecturerSignature}}", "____________________")
                .Replace("{{LecturerSignatureDate}}", "____________________")
                .Replace("{{DeanSignature}}", "____________________")
                .Replace("{{DeanSignatureDate}}", "____________________")
                .Replace("{{HRSignature}}", "____________________")
                .Replace("{{HRSignatureDate}}", "____________________")
                .Replace("{{DVCARSignature}}", "____________________")
                .Replace("{{DVCARSignatureDate}}", "____________________")
                .Replace("{{VCSignature}}", "____________________")
                .Replace("{{VCSignatureDate}}", "____________________");

            var contract = new Contract(0, "1.0")
            {
                LecturerId = lecturer.Id,
                CourseAssignmentId = assignment.Id,
                Content = content,
                RatePerHour = HourlyRate,
                StartDateUtc = DateTime.UtcNow,
                Status = ContractStatus.PendingSignature
            };
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();

            _context.ContractSignatures.AddRange(
                new ContractSignature(0, contract.Id, 1, SignerRole.Lecturer),
                new ContractSignature(0, contract.Id, 2, SignerRole.Dean),
                new ContractSignature(0, contract.Id, 2, SignerRole.HROfficer),  // same number as Dean = parallel
                new ContractSignature(0, contract.Id, 3, SignerRole.DVCAR),
                new ContractSignature(0, contract.Id, 4, SignerRole.ViceChancellor));
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
            //
            // The preview page loads everything itself from the real
            // Contract row by Id — nothing is trusted from the URL.
            //
            // IMPORTANT:
            // GovernmentId is intentionally NOT read here because the
            // existing encrypted database value currently cannot be
            // decrypted due to the missing Data Protection key.
            //
            // We will fix the Government ID encryption/data separately.
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
            // because that will cause EF Core to read
            // GovernmentIdEncrypted and execute:
            //
            // GovernmentIdProtector.Decrypt()
            //
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
    }
}