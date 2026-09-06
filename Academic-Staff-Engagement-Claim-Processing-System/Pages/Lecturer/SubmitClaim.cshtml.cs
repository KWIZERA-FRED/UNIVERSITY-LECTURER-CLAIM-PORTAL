using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using LecturerModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    [Authorize(Roles = "Lecturer")]
    public class SubmitClaimModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ClaimSubmissionService _claimSubmissionService;

        public SubmitClaimModel(
            ApplicationDbContext context,
            ClaimSubmissionService claimSubmissionService)
        {
            _context = context;
            _claimSubmissionService = claimSubmissionService;
        }

        [BindProperty]
        public int SelectedCourseAssignmentId { get; set; }

        [BindProperty]
        public decimal Hours { get; set; }

        public string LecturerName { get; private set; } = string.Empty;

        public List<CourseOption> Courses { get; private set; } = new();

        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var lecturer = await GetAuthenticatedLecturerAsync();

            if (lecturer == null)
            {
                return Forbid();
            }

            LecturerName = lecturer.UserName;

            await LoadCoursesAsync(lecturer.Id);

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

            if (SelectedCourseAssignmentId <= 0)
            {
                ErrorMessage = "Please select your course.";

                await LoadCoursesAsync(lecturer.Id);

                return Page();
            }

            if (Hours <= 0)
            {
                ErrorMessage = "Please enter the verified teaching hours.";

                await LoadCoursesAsync(lecturer.Id);

                return Page();
            }

            var result = await _claimSubmissionService.SubmitAsync(
                lecturer.Id,
                SelectedCourseAssignmentId,
                Hours,
                description: null,
                lecturer.UserName,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage ??
                    "The claim could not be submitted.";

                await LoadCoursesAsync(lecturer.Id);

                return Page();
            }

            TempData["SuccessMessage"] = "Claim submitted successfully.";

            return Redirect("/Claims");
        }

        private async Task<LecturerModel?> GetAuthenticatedLecturerAsync()
        {
            var userIdValue = User.FindFirstValue("UserId");

            if (!int.TryParse(userIdValue, out int lecturerId))
            {
                return null;
            }

            return await _context.Lecturers
                .FirstOrDefaultAsync(l => l.Id == lecturerId && l.IsActive);
        }

        private async Task LoadCoursesAsync(int lecturerId)
        {
            var assignments = await _context.CourseAssignments
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

            var assignmentIds = assignments.Select(a => a.Id).ToList();

            var marksStatuses = await _context.MarksSubmissions
                .AsNoTracking()
                .Where(m =>
                    m.LecturerId == lecturerId &&
                    assignmentIds.Contains(m.CourseAssignmentId))
                .Select(m => new { m.CourseAssignmentId, m.Status })
                .ToListAsync();

            Courses = assignments
                .Select(a =>
                {
                    var marksForAssignment = marksStatuses
                        .Where(m => m.CourseAssignmentId == a.Id)
                        .ToList();

                    return new CourseOption
                    {
                        AssignmentId = a.Id,
                        Code = a.Course.Code,
                        Name = a.Course.Title,
                        AcademicYear = a.AcademicYear,
                        AllocatedHours = a.AllocatedHours,
                        MarksSubmitted = marksForAssignment.Any(),
                        MarksSigned = marksForAssignment.Any(m => m.Status == MarksSubmissionStatus.Signed)
                    };
                })
                .ToList();
        }

        public sealed class CourseOption
        {
            public int AssignmentId { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public string AcademicYear { get; init; } = string.Empty;
            public decimal AllocatedHours { get; init; }
            public bool MarksSubmitted { get; init; }
            public bool MarksSigned { get; init; }
        }
    }
}