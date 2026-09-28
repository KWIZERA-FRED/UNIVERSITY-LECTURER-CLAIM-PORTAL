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
    [ValidateAntiForgeryToken]
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

        [BindProperty]
        public int CourseAssignmentId { get; set; }

        [BindProperty]
        public IFormFile? MarksFile { get; set; }

        public string LecturerName { get; private set; } = string.Empty;

        public List<CourseAssignment> Assignments { get; private set; }
            = new();

        public string? ErrorMessage { get; private set; }

        public string? SuccessMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var lecturer = await GetAuthenticatedLecturerAsync();

            if (lecturer == null)
            {
                return Forbid();
            }

            LecturerName = lecturer.UserName;

            await LoadAssignmentsAsync(lecturer.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var lecturer = await GetAuthenticatedLecturerAsync();

            if (lecturer == null)
            {
                return Forbid();
            }

            LecturerName = lecturer.UserName;

            if (CourseAssignmentId <= 0)
            {
                ErrorMessage = "Please select a course.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            var assignment =
                await _context.CourseAssignments
                    .Include(ca => ca.Course)
                    .FirstOrDefaultAsync(ca =>
                        ca.Id == CourseAssignmentId &&
                        ca.LecturerId == lecturer.Id &&
                        ca.IsActive &&
                        ca.Course != null &&
                        ca.Course.IsActive);

            if (assignment == null)
            {
                ErrorMessage =
                    "The selected course is no longer available for submission.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            if (MarksFile == null || MarksFile.Length == 0)
            {
                ErrorMessage =
                    "Please upload the Excel marks sheet.";

                await LoadAssignmentsAsync(lecturer.Id);

                return Page();
            }

            var result = await _marksSigningService.SubmitAsync(
                lecturer.Id,
                assignment.Id,
                assignment.AcademicYear,
                assignment.Semester,
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

            SuccessMessage =
                $"Marks submitted successfully. " +
                $"Reference: {result.SubmissionReference}";

            CourseAssignmentId = 0;
            MarksFile = null;

            await LoadAssignmentsAsync(lecturer.Id);

            return Page();
        }

        private async Task<
            Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer?>
            GetAuthenticatedLecturerAsync()
        {
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

        private async Task LoadAssignmentsAsync(int lecturerId)
        {
            Assignments =
                await _context.CourseAssignments
                    .AsNoTracking()
                    .Include(ca => ca.Course)
                    .Where(ca =>
                        ca.LecturerId == lecturerId &&
                        ca.IsActive &&
                        ca.Course != null &&
                        ca.Course.IsActive)
                    .OrderByDescending(ca => ca.AcademicYear)
                    .ThenBy(ca => ca.Course.Code)
                    .ToListAsync();
        }
    }
}