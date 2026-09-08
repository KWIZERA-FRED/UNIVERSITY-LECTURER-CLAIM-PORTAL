using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    [Authorize(Roles = "HOD")]
    public class StaffModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StaffModel(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // CURRENT HOD
        // ============================================================

        public Hod? CurrentHod { get; private set; }

        public string HodName =>
            CurrentHod?.UserName ?? "Head of Department";

        public string HodDepartment =>
            CurrentHod?.Department ?? "Department";


        // ============================================================
        // STAFF
        // ============================================================

        public List<StaffListItem> Lecturers { get; private set; } = new();


        public class StaffListItem
        {
            public int Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public UserRole Type { get; set; }

            public LecturerRank? Rank { get; set; }

            public bool IsActive { get; set; }

            public List<CourseAssignmentItem> Assignments { get; set; } = new();

            public int ActiveAssignmentCount =>
                Assignments.Count(a => a.IsActive);
        }


        // ============================================================
        // COURSE ASSIGNMENT VIEW MODEL
        // ============================================================

        public class CourseAssignmentItem
        {
            public int Id { get; set; }

            public int LecturerId { get; set; }

            public int CourseId { get; set; }

            public string CourseCode { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public string AcademicYear { get; set; } = string.Empty;

            public Semester Semester { get; set; }

            public decimal AllocatedHours { get; set; }

            public bool IsApproved { get; set; }

            public bool IsActive { get; set; }
        }


        // ============================================================
        // COURSE OPTIONS
        // ============================================================

        public List<CourseOption> CourseOptions { get; private set; } = new();


        public class CourseOption
        {
            public int Id { get; set; }

            public string Code { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;
        }


        // ============================================================
        // UPDATE ASSIGNMENT
        // ============================================================

        [BindProperty]
        public int AssignmentId { get; set; }

        [BindProperty]
        public int LecturerId { get; set; }

        [BindProperty]
        public int CourseId { get; set; }

        [BindProperty]
        public string AcademicYear { get; set; } = string.Empty;

        [BindProperty]
        public Semester Semester { get; set; }

        [BindProperty]
        public decimal AllocatedHours { get; set; }


        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await LoadCurrentHodAsync())
            {
                return RedirectToPage("/Login");
            }

            await LoadCourseOptionsAsync();

            await LoadLecturersAsync();

            return Page();
        }


        // ============================================================
        // UPDATE COURSE ASSIGNMENT
        // ============================================================

        public async Task<IActionResult> OnPostUpdateAssignmentAsync()
        {
            if (!await LoadCurrentHodAsync())
            {
                return RedirectToPage("/Login");
            }

            await LoadCourseOptionsAsync();

            if (AssignmentId <= 0)
            {
                TempData["ErrorMessage"] =
                    "The selected course assignment is invalid.";

                return RedirectToPage();
            }

            if (CourseId <= 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a valid course.";

                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                TempData["ErrorMessage"] =
                    "Academic year is required.";

                return RedirectToPage();
            }

            if (AllocatedHours <= 0 || AllocatedHours > 500)
            {
                TempData["ErrorMessage"] =
                    "Allocated teaching hours must be between 0 and 500.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // LOAD ASSIGNMENT
            // --------------------------------------------------------

            var assignment = await _context.CourseAssignments
                .Include(ca => ca.Course)
                .Include(ca => ca.Lecturer)
                .FirstOrDefaultAsync(ca => ca.Id == AssignmentId);

            if (assignment == null)
            {
                TempData["ErrorMessage"] =
                    "The course assignment could not be found.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // SECURITY:
            // VERIFY CURRENT HOD OWNS THIS DEPARTMENT
            // --------------------------------------------------------

            if (!string.Equals(
                    assignment.Course.Department,
                    CurrentHod!.Department,
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "You are not authorized to modify this assignment.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // VERIFY LECTURER
            // --------------------------------------------------------

            if (assignment.LecturerId != LecturerId)
            {
                TempData["ErrorMessage"] =
                    "The lecturer associated with this assignment could not be verified.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // VERIFY SELECTED COURSE BELONGS TO HOD DEPARTMENT
            // --------------------------------------------------------

            var selectedCourse = await _context.Courses
                .FirstOrDefaultAsync(c =>
                    c.Id == CourseId &&
                    c.IsActive &&
                    c.Department == CurrentHod.Department);

            if (selectedCourse == null)
            {
                TempData["ErrorMessage"] =
                    "The selected course is not available in your department.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // PREVENT DUPLICATE ACTIVE ASSIGNMENTS
            // --------------------------------------------------------

            bool duplicateExists = await _context.CourseAssignments
                .AnyAsync(ca =>
                    ca.Id != AssignmentId &&
                    ca.LecturerId == LecturerId &&
                    ca.CourseId == CourseId &&
                    ca.AcademicYear == AcademicYear &&
                    ca.Semester == Semester &&
                    ca.IsActive);

            if (duplicateExists)
            {
                TempData["ErrorMessage"] =
                    "This lecturer already has an active assignment for the selected course, academic year and semester.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // DETECT MATERIAL CHANGES
            // --------------------------------------------------------

            bool assignmentChanged =
                assignment.CourseId != CourseId ||
                !string.Equals(
                    assignment.AcademicYear,
                    AcademicYear,
                    StringComparison.Ordinal) ||
                assignment.Semester != Semester ||
                assignment.AllocatedHours != AllocatedHours;


            // --------------------------------------------------------
            // UPDATE
            // --------------------------------------------------------

            assignment.CourseId = CourseId;
            assignment.AcademicYear = AcademicYear.Trim();
            assignment.Semester = Semester;
            assignment.AllocatedHours = AllocatedHours;
            assignment.UpdatedAtUtc = DateTime.UtcNow;


            // --------------------------------------------------------
            // IMPORTANT:
            // CHANGING ASSIGNMENT DETAILS INVALIDATES OLD APPROVAL
            // --------------------------------------------------------

            if (assignmentChanged)
            {
                assignment.IsApproved = false;
                assignment.ApprovedByHodId = null;
                assignment.ApprovedAtUtc = null;
            }


            await _context.SaveChangesAsync();


            TempData["SuccessMessage"] = assignmentChanged
                ? "Course assignment updated and returned to Pending Approval."
                : "Course assignment updated successfully.";

            return RedirectToPage();
        }


        // ============================================================
        // REMOVE / DEACTIVATE COURSE ASSIGNMENT
        // ============================================================

        public async Task<IActionResult> OnPostRemoveAssignmentAsync(
            int assignmentId)
        {
            if (!await LoadCurrentHodAsync())
            {
                return RedirectToPage("/Login");
            }


            var assignment = await _context.CourseAssignments
                .Include(ca => ca.Course)
                .FirstOrDefaultAsync(ca => ca.Id == assignmentId);

            if (assignment == null)
            {
                TempData["ErrorMessage"] =
                    "The course assignment could not be found.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // SECURITY:
            // ONLY THIS HOD'S DEPARTMENT
            // --------------------------------------------------------

            if (!string.Equals(
                    assignment.Course.Department,
                    CurrentHod!.Department,
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] =
                    "You are not authorized to remove this assignment.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // SOFT DELETE
            // --------------------------------------------------------

            assignment.IsActive = false;
            assignment.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();


            TempData["SuccessMessage"] =
                $"The {assignment.Course.Code} assignment has been deactivated.";

            return RedirectToPage();
        }


        // ============================================================
        // DEACTIVATE LECTURER ACCOUNT
        // ============================================================

        public async Task<IActionResult> OnPostDeactivateAsync(int id)
        {
            if (!await LoadCurrentHodAsync())
            {
                return RedirectToPage("/Login");
            }


            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecturer == null)
            {
                TempData["ErrorMessage"] =
                    "The lecturer account could not be found.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // SECURITY:
            // VERIFY THIS HOD CREATED / OWNS THE LECTURER
            // OR HAS AN ASSIGNMENT IN THE HOD DEPARTMENT
            // --------------------------------------------------------

            bool belongsToDepartment = await _context.CourseAssignments
                .AnyAsync(ca =>
                    ca.LecturerId == lecturer.Id &&
                    ca.Course.Department == CurrentHod.Department);

            bool createdByThisHod =
                lecturer.SignatureCapturedByHodId == CurrentHod.Id;


            if (!belongsToDepartment && !createdByThisHod)
            {
                TempData["ErrorMessage"] =
                    "You are not authorized to manage this lecturer.";

                return RedirectToPage();
            }


            // --------------------------------------------------------
            // SOFT DEACTIVATE
            // --------------------------------------------------------

            lecturer.IsActive = false;
            lecturer.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();


            TempData["SuccessMessage"] =
                $"The lecturer account for {lecturer.UserName} has been deactivated.";

            return RedirectToPage();
        }


        // ============================================================
        // LOAD CURRENT HOD
        // ============================================================

        private async Task<bool> LoadCurrentHodAsync()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            CurrentHod = await _context.Hods
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.UserName == username &&
                    h.IsActive);

            return CurrentHod != null;
        }


        // ============================================================
        // LOAD COURSE OPTIONS
        // ============================================================

        private async Task LoadCourseOptionsAsync()
        {
            CourseOptions = await _context.Courses
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.Department == CurrentHod!.Department)
                .OrderBy(c => c.Code)
                .Select(c => new CourseOption
                {
                    Id = c.Id,
                    Code = c.Code,
                    Title = c.Title
                })
                .ToListAsync();
        }


        // ============================================================
        // LOAD LECTURERS
        // ============================================================

        private async Task LoadLecturersAsync()
        {
            var department = CurrentHod!.Department;
            var hodId = CurrentHod.Id;


            // --------------------------------------------------------
            // LECTURERS ARE LIMITED TO:
            //
            // 1. Lecturers created/captured by this HOD
            // OR
            // 2. Lecturers who have a course assignment in this
            //    HOD's department
            // --------------------------------------------------------

            var lecturers = await _context.Lecturers
                .AsNoTracking()
                .Where(l =>
                    l.SignatureCapturedByHodId == hodId ||
                    l.CourseAssignments.Any(ca =>
                        ca.Course.Department == department))
                .OrderBy(l => l.UserName)
                .Select(l => new StaffListItem
                {
                    Id = l.Id,
                    UserName = l.UserName,
                    Email = l.Email,
                    Type = l.Type,
                    Rank = l.Rank,
                    IsActive = l.IsActive,

                    Assignments = l.CourseAssignments
                        .Where(ca =>
                            ca.Course.Department == department)
                        .OrderByDescending(ca => ca.IsActive)
                        .ThenByDescending(ca => ca.AcademicYear)
                        .ThenBy(ca => ca.Course.Code)
                        .Select(ca => new CourseAssignmentItem
                        {
                            Id = ca.Id,
                            LecturerId = ca.LecturerId,
                            CourseId = ca.CourseId,

                            CourseCode = ca.Course.Code,
                            CourseTitle = ca.Course.Title,

                            AcademicYear = ca.AcademicYear,
                            Semester = ca.Semester,

                            AllocatedHours = ca.AllocatedHours,

                            IsApproved = ca.IsApproved,
                            IsActive = ca.IsActive
                        })
                        .ToList()
                })
                .ToListAsync();


            Lecturers = lecturers;
        }
    }
}