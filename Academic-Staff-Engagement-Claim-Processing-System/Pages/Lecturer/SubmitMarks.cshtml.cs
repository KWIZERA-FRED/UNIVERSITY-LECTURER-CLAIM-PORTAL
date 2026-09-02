using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    [Authorize(Roles = "Lecturer")]
    public class SubmitMarksModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MarksSigningService _marksSigningService;

        public SubmitMarksModel(
            ApplicationDbContext context,
            MarksSigningService marksSigningService)
        {
            _context = context;
            _marksSigningService = marksSigningService;
        }

        // ============================================================
        // FORM
        // ============================================================

        [BindProperty]
        public int CourseAssignmentId { get; set; }

        [BindProperty]
        public string AcademicYear { get; set; } = string.Empty;

        [BindProperty]
        public Semester Semester { get; set; }

        [BindProperty]
        public IFormFile? MarksFile { get; set; }

        // ============================================================
        // DISPLAY
        // ============================================================

        public string LecturerName { get; private set; }
            = string.Empty;

        public List<CourseAssignment> Assignments { get; private set; }
            = new();

        public string? ErrorMessage { get; private set; }

        public string? SuccessMessage { get; private set; }

        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            var lecturer = await GetAuthenticatedLecturerAsync();

            if (lecturer == null)
                return Forbid();

            LecturerName = lecturer.UserName;

            await LoadAssignmentsAsync(lecturer.Id);

            return Page();
        }

        // ============================================================
        // POST
        // ============================================================

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            var lecturer = await GetAuthenticatedLecturerAsync();

            if (lecturer == null)
                return Forbid();

            LecturerName = lecturer.UserName;

            // --------------------------------------------------------
            // BASIC INPUT VALIDATION
            // --------------------------------------------------------

            if (CourseAssignmentId <= 0)
            {
                ErrorMessage =
                    "Please select your course.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage =
                    "Please select the academic year.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            if (!Enum.IsDefined(typeof(Semester), Semester))
            {
                ErrorMessage =
                    "Please select a valid semester.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            if (MarksFile == null ||
                MarksFile.Length == 0)
            {
                ErrorMessage =
                    "Please upload the Excel marks sheet.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            // --------------------------------------------------------
            // SERVICE HANDLES SECURITY-SENSITIVE VALIDATION
            // --------------------------------------------------------

            var result =
                await _marksSigningService.SubmitAsync(
                    lecturer.Id,
                    CourseAssignmentId,
                    AcademicYear.Trim(),
                    Semester,
                    MarksFile,
                    lecturer.UserName,
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString());

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage ??
                    "The marks submission could not be completed.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            // --------------------------------------------------------
            // SUCCESS
            // --------------------------------------------------------

            SuccessMessage =
                $"Marks submitted successfully. " +
                $"Reference: {result.SubmissionReference}";

            // Clear the form after successful submission.
            CourseAssignmentId = 0;
            AcademicYear = string.Empty;
            Semester = default;
            MarksFile = null;

            await LoadAssignmentsAsync(lecturer.Id);

            return Page();
        }

        // ============================================================
        // GET AUTHENTICATED LECTURER
        // ============================================================

        private async Task<Lecturer?> GetAuthenticatedLecturerAsync()
        {
            /*
             * Login.cshtml.cs creates this claim:
             *
             * new Claim("UserId", userId.ToString())
             *
             * We therefore use the authenticated claim rather than
             * accepting a lecturer ID from the browser.
             */

            var userIdValue =
                User.FindFirstValue("UserId");

            if (!int.TryParse(
                    userIdValue,
                    out int lecturerId))
            {
                return null;
            }

            return await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.Id == lecturerId &&
                    l.IsActive);
        }

        // ============================================================
        // LOAD ONLY THIS LECTURER'S ASSIGNMENTS
        // ============================================================

        private async Task LoadAssignmentsAsync(
            int lecturerId)
        {
            Assignments =
                await _context.CourseAssignments
                    .AsNoTracking()
                    .Include(ca => ca.Course)
                    .Where(ca =>
                        ca.LecturerId == lecturerId &&
                        ca.IsActive &&
                        ca.IsApproved &&
                        ca.Course.IsActive)
                    .OrderBy(ca => ca.AcademicYear)
                    .ThenBy(ca => ca.Course.Code)
                    .ToListAsync();
        }
    }
}