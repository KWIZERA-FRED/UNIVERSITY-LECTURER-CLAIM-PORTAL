using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
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
        private readonly AuditLogger _auditLogger;

        public StaffModel(
            ApplicationDbContext context,
            AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        // ============================================================
        // CURRENT HOD
        // ============================================================

        public Hod? CurrentHod { get; private set; }

        public string HodName =>
            CurrentHod?.UserName ?? "Head of Faculty";

        public string HodFaculty =>
            CurrentHod?.Faculty.ToString() ?? "Faculty";


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

            var facultyDepartments = GetCurrentFacultyDepartments();

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

            if (!facultyDepartments.Contains(assignment.Course.Department))
            {
                TempData["ErrorMessage"] =
                    "You are not authorized to modify this assignment.";

                return RedirectToPage();
            }

            if (assignment.LecturerId != LecturerId)
            {
                TempData["ErrorMessage"] =
                    "The lecturer associated with this assignment could not be verified.";

                return RedirectToPage();
            }

            var selectedCourse = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == CourseId &&
                    c.IsActive &&
                    facultyDepartments.Contains(c.Department));

            if (selectedCourse == null)
            {
                TempData["ErrorMessage"] =
                    "The selected course is not available in your faculty.";

                return RedirectToPage();
            }

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

            bool assignmentChanged =
                assignment.CourseId != CourseId ||
                !string.Equals(
                    assignment.AcademicYear,
                    AcademicYear,
                    StringComparison.Ordinal) ||
                assignment.Semester != Semester ||
                assignment.AllocatedHours != AllocatedHours;

            assignment.CourseId = CourseId;
            assignment.AcademicYear = AcademicYear.Trim();
            assignment.Semester = Semester;
            assignment.AllocatedHours = AllocatedHours;
            assignment.UpdatedAtUtc = DateTime.UtcNow;

            if (assignmentChanged)
            {
                assignment.IsApproved = false;
                assignment.ApprovedByHodId = null;
                assignment.ApprovedAtUtc = null;

                // Recorded in the same SaveChanges as the update itself.
                _auditLogger.Add(
                    AuditAction.CourseAssignmentUpdated,
                    CurrentHod!.UserName,
                    "HOD",
                    CurrentHod.Id,
                    "CourseAssignment",
                    assignment.Id,
                    $"{selectedCourse.Code} {assignment.AcademicYear} " +
                    $"{assignment.Semester}, {assignment.AllocatedHours:0.##}h " +
                    $"for lecturer {assignment.Lecturer.UserName}; " +
                    "returned to pending approval",
                    HttpContext.Connection.RemoteIpAddress?.ToString());
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

            var facultyDepartments = GetCurrentFacultyDepartments();

            var assignment = await _context.CourseAssignments
                .Include(ca => ca.Course)
                .FirstOrDefaultAsync(ca => ca.Id == assignmentId);

            if (assignment == null)
            {
                TempData["ErrorMessage"] =
                    "The course assignment could not be found.";

                return RedirectToPage();
            }

            if (!facultyDepartments.Contains(assignment.Course.Department))
            {
                TempData["ErrorMessage"] =
                    "You are not authorized to remove this assignment.";

                return RedirectToPage();
            }

            assignment.IsActive = false;
            assignment.UpdatedAtUtc = DateTime.UtcNow;

            _auditLogger.Add(
                AuditAction.CourseAssignmentRemoved,
                CurrentHod!.UserName,
                "HOD",
                CurrentHod.Id,
                "CourseAssignment",
                assignment.Id,
                $"{assignment.Course.Code} {assignment.AcademicYear} " +
                $"{assignment.Semester} deactivated " +
                $"(lecturer id {assignment.LecturerId})",
                HttpContext.Connection.RemoteIpAddress?.ToString());

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

            var facultyDepartments = GetCurrentFacultyDepartments();

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lecturer == null)
            {
                TempData["ErrorMessage"] =
                    "The lecturer account could not be found.";

                return RedirectToPage();
            }

            bool belongsToFaculty = lecturer.Faculty == CurrentHod!.Faculty;

            bool hasFacultyAssignment = await _context.CourseAssignments
                .AnyAsync(ca =>
                    ca.LecturerId == lecturer.Id &&
                    facultyDepartments.Contains(ca.Course.Department));

            bool createdByThisHod =
                lecturer.SignatureCapturedByHodId == CurrentHod.Id;

            if (!belongsToFaculty &&
                !hasFacultyAssignment &&
                !createdByThisHod)
            {
                TempData["ErrorMessage"] =
                    "You are not authorized to manage this lecturer.";

                return RedirectToPage();
            }

            bool wasActive = lecturer.IsActive;

            lecturer.IsActive = false;
            lecturer.UpdatedAtUtc = DateTime.UtcNow;

            if (wasActive)
            {
                _auditLogger.Add(
                    AuditAction.AccountDeactivated,
                    CurrentHod.UserName,
                    "HOD",
                    CurrentHod.Id,
                    "Lecturer",
                    lecturer.Id,
                    $"Lecturer account {lecturer.UserName} deactivated by HOD",
                    HttpContext.Connection.RemoteIpAddress?.ToString());
            }

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
        // GET HOD FACULTY DEPARTMENTS
        // ============================================================

        private List<string> GetCurrentFacultyDepartments()
        {
            if (CurrentHod == null)
            {
                return new List<string>();
            }

            return FacultyDepartments
                .GetDepartments(CurrentHod.Faculty)
                .Select(d => d.ToString())
                .ToList();
        }


        // ============================================================
        // LOAD COURSE OPTIONS
        // ============================================================

        private async Task LoadCourseOptionsAsync()
        {
            var facultyDepartments = GetCurrentFacultyDepartments();

            CourseOptions = await _context.Courses
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    facultyDepartments.Contains(c.Department))
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
            var facultyDepartments = GetCurrentFacultyDepartments();
            var hodId = CurrentHod!.Id;
            var faculty = CurrentHod.Faculty;

            var lecturers = await _context.Lecturers
                .AsNoTracking()
                .Where(l =>
                    l.Faculty == faculty ||
                    l.SignatureCapturedByHodId == hodId ||
                    l.CourseAssignments.Any(ca =>
                        facultyDepartments.Contains(
                            ca.Course.Department)))
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
                            facultyDepartments.Contains(
                                ca.Course.Department))
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