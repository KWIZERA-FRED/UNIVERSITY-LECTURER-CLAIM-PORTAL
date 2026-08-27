using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    public class AssignCourseModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AssignCourseModel(ApplicationDbContext context)
        {
            _context = context;
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

        public List<LecturerModel> Lecturers { get; set; } = new();

        public decimal HourlyRate { get; set; }

        public string? ErrorMessage { get; set; }

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
                ErrorMessage = "Please enter a valid number of teaching hours.";
                return Page();
            }

            if (AllocatedHours > 500)
            {
                ErrorMessage = "Allocated hours cannot exceed 500.";
                return Page();
            }

            // ========================================================
            // FIND COURSE FROM DATABASE
            // ========================================================

            var course = await _context.Courses
                .FirstOrDefaultAsync(c =>
                    c.Id == SelectedCourse.Value &&
                    c.IsActive);

            if (course == null)
            {
                ErrorMessage = "Selected course could not be found.";
                return Page();
            }

            // ========================================================
            // FIND LECTURER FROM DATABASE
            // ========================================================

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.Id == SelectedLecturer.Value &&
                    l.IsActive);

            if (lecturer == null)
            {
                ErrorMessage = "Selected lecturer could not be found.";
                return Page();
            }

            // ========================================================
            // CHECK FOR DUPLICATE ASSIGNMENT
            // ========================================================

            var existingAssignment = await _context.CourseAssignments
                .AnyAsync(ca =>
                    ca.LecturerId == lecturer.Id &&
                    ca.CourseId == course.Id &&
                    ca.AcademicYear == AcademicYear &&
                    ca.Semester == Semester.Value &&
                    ca.IsActive);

            if (existingAssignment)
            {
                ErrorMessage =
                    "This lecturer has already been assigned this course " +
                    "for the selected academic year and semester.";

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

                AcademicYear = AcademicYear,

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
            // REDIRECT TO CONTRACT PREVIEW
            // ========================================================

            return RedirectToPage(
                "./ContractPreview",
                new
                {
                    Course = course.Code + " - " + course.Title,

                    Lecturer = GetLecturerDisplayName(lecturer),

                    GovernmentId = lecturer.GovernmentIdEncrypted,

                    Rank = lecturer.Rank?.ToString() ?? "Not specified",

                    AcademicYear = AcademicYear,

                    Semester = Semester.Value.ToString(),

                    Session = Session.Value.ToString(),

                    Campus = Campus.Value.ToString(),

                    Hours = AllocatedHours,

                    Rate = HourlyRate,

                    CourseAssignmentId = assignment.Id
                }
            );
        }

        // ============================================================
        // LOAD DATA
        // ============================================================

        private async Task LoadDataAsync()
        {
            Courses = await _context.Courses
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync();

            Lecturers = await _context.Lecturers
                .Where(l => l.IsActive)
                .OrderBy(l => l.UserName)
                .ToListAsync();
        }

        // ============================================================
        // LECTURER DISPLAY NAME
        // ============================================================

        private string GetLecturerDisplayName(LecturerModel lecturer)
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