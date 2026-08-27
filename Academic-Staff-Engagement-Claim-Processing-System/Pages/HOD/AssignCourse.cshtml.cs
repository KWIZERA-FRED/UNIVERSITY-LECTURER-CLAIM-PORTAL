using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

// IMPORTANT:
// The HOD/Lecturer namespace conflicts with the Lecturer model.
// This alias forces C# to use the database Lecturer model.
using LecturerModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

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
        // FORM VALUES
        // ============================================================

        [BindProperty]
        public int? SelectedCourse { get; set; }

        [BindProperty]
        public int? SelectedLecturer { get; set; }

        [BindProperty]
        public Semester? Semester { get; set; }

        [BindProperty]
        public Session? Session { get; set; }

        [BindProperty]
        public Campus? Campus { get; set; }

        [BindProperty]
        public string? AcademicYear { get; set; }

        [BindProperty]
        public decimal AllocatedHours { get; set; }

        // ============================================================
        // DATABASE DATA
        // ============================================================

        public List<Course> Courses { get; set; } = new();

        public List<LecturerModel> Lecturers { get; set; } = new();

        public LecturerModel? SelectedLecturerDetails { get; set; }

        // ============================================================
        // OTHER DATA
        // ============================================================

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
            // COURSE
            // --------------------------------------------------------

            if (!SelectedCourse.HasValue)
            {
                ErrorMessage = "Please select a course.";
                return Page();
            }

            // --------------------------------------------------------
            // LECTURER
            // --------------------------------------------------------

            if (!SelectedLecturer.HasValue)
            {
                ErrorMessage = "Please select a lecturer.";
                return Page();
            }

            // --------------------------------------------------------
            // SEMESTER
            // --------------------------------------------------------

            if (!Semester.HasValue)
            {
                ErrorMessage = "Please select a semester.";
                return Page();
            }

            // --------------------------------------------------------
            // SESSION
            // --------------------------------------------------------

            if (!Session.HasValue)
            {
                ErrorMessage = "Please select a session.";
                return Page();
            }

            // --------------------------------------------------------
            // CAMPUS
            // --------------------------------------------------------

            if (!Campus.HasValue)
            {
                ErrorMessage = "Please select a campus.";
                return Page();
            }

            // --------------------------------------------------------
            // ACADEMIC YEAR
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage = "Please select an academic year.";
                return Page();
            }

            // --------------------------------------------------------
            // HOURS
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
                    "The number of teaching hours cannot exceed 500.";

                return Page();
            }

            // ========================================================
            // GET LECTURER FROM DATABASE
            // ========================================================

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.Id == SelectedLecturer.Value);

            if (lecturer == null)
            {
                ErrorMessage =
                    "The selected lecturer could not be found.";

                return Page();
            }

            // ========================================================
            // CHECK LECTURER ACTIVE
            // ========================================================

            if (!lecturer.IsActive)
            {
                ErrorMessage =
                    "The selected lecturer account is inactive.";

                return Page();
            }

            // ========================================================
            // GET COURSE FROM DATABASE
            // ========================================================

            var course = await _context.Courses
                .FirstOrDefaultAsync(c =>
                    c.Id == SelectedCourse.Value);

            if (course == null)
            {
                ErrorMessage =
                    "The selected course could not be found.";

                return Page();
            }

            // ========================================================
            // CHECK COURSE ACTIVE
            // ========================================================

            if (!course.IsActive)
            {
                ErrorMessage =
                    "The selected course is inactive.";

                return Page();
            }

            // ========================================================
            // CALCULATE RATE
            // ========================================================

            HourlyRate = GetRateForRank(lecturer.Rank);

            SelectedLecturerDetails = lecturer;

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

            // ========================================================
            // SAVE
            // ========================================================

            _context.CourseAssignments.Add(assignment);

            await _context.SaveChangesAsync();

            // ========================================================
            // CONTRACT PREVIEW
            // ========================================================

            return RedirectToPage(
                "./ContractPreview",
                new
                {
                    Course =
                        course.Code + " - " + course.Title,

                    Lecturer =
                        lecturer.UserName,

                    GovernmentId =
                        lecturer.GovernmentIdEncrypted,

                    Rank =
                        lecturer.Rank?.ToString(),

                    Session =
                        Session.Value.ToString(),

                    Semester =
                        Semester.Value.ToString(),

                    AcademicYear =
                        AcademicYear,

                    Campus =
                        Campus.Value.ToString(),

                    Hours =
                        AllocatedHours,

                    Rate =
                        HourlyRate,

                    AssignmentId =
                        assignment.Id
                });
        }

        // ============================================================
        // LOAD DATABASE DATA
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
        // HOURLY RATE
        // ============================================================

        private decimal GetRateForRank(LecturerRank? rank)
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
    }
}