using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Management
{
    [Authorize(Roles = "Management")]
    public class MarksModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MarksSigningService _marksService;

        public MarksModel(ApplicationDbContext context, MarksSigningService marksService)
        {
            _context = context;
            _marksService = marksService;
        }

        public List<PendingMarksRow> PendingSubmissions { get; set; } = new();
        public MarksReviewDto? SelectedSubmission { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SubmissionId { get; set; }

        [BindProperty]
        public string? DeclineReason { get; set; }

        public class PendingMarksRow
        {
            public int Id { get; set; }
            public string Reference { get; set; } = string.Empty;
            public string LecturerName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
            public string AcademicYear { get; set; } = string.Empty;
            public DateTime SubmittedAtUtc { get; set; }
        }

        public class MarksReviewDto
        {
            public int Id { get; set; }
            public string Reference { get; set; } = string.Empty;
            public string LecturerName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await IsExamOfficeAsync())
                return RedirectToPage("/ManagementDashboard");

            await LoadPendingListAsync();

            if (SubmissionId.HasValue)
            {
                var s = await _context.MarksSubmissions
                    .AsNoTracking()
                    .Include(ms => ms.Lecturer)
                    .Include(ms => ms.Course)
                    .FirstOrDefaultAsync(ms => ms.Id == SubmissionId.Value && ms.Status == MarksSubmissionStatus.Pending);

                if (s is null)
                {
                    ErrorMessage = "That submission could not be found, or has already been reviewed.";
                }
                else
                {
                    SelectedSubmission = new MarksReviewDto
                    {
                        Id = s.Id,
                        Reference = s.SubmissionReference,
                        LecturerName = s.Lecturer.UserName,
                        CourseTitle = s.Course.Title,
                        FileName = s.FileName
                    };
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            if (!await IsExamOfficeAsync() || !SubmissionId.HasValue)
                return RedirectToPage("/ManagementDashboard");

            var (actorId, actorUsername, ipAddress) = GetActorContext();

            var result = await _marksService.ReviewAsync(
                SubmissionId.Value, true, null, actorId, actorUsername, ipAddress);

            if (!result.Succeeded)
                ErrorMessage = result.ErrorMessage;
            else
                SuccessMessage = "Marks signed. The lecturer can now submit a claim for this course.";

            await LoadPendingListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!await IsExamOfficeAsync() || !SubmissionId.HasValue)
                return RedirectToPage("/ManagementDashboard");

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage = "Please provide a reason for declining this submission.";
                await LoadPendingListAsync();
                return Page();
            }

            var (actorId, actorUsername, ipAddress) = GetActorContext();

            var result = await _marksService.ReviewAsync(
                SubmissionId.Value, false, DeclineReason, actorId, actorUsername, ipAddress);

            if (!result.Succeeded)
                ErrorMessage = result.ErrorMessage;
            else
                SuccessMessage = "Marks submission declined.";

            await LoadPendingListAsync();
            return Page();
        }

        private async Task<bool> IsExamOfficeAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return await _context.ManagementAccounts
                .AsNoTracking()
                .AnyAsync(m => m.UserName == username && m.IsActive && m.Title == ManagementTitle.ExamOffice);
        }

        private async Task LoadPendingListAsync()
        {
            PendingSubmissions = await _context.MarksSubmissions
                .AsNoTracking()
                .Where(ms => ms.Status == MarksSubmissionStatus.Pending)
                .Include(ms => ms.Lecturer)
                .Include(ms => ms.Course)
                .OrderBy(ms => ms.SubmittedAtUtc)
                .Select(ms => new PendingMarksRow
                {
                    Id = ms.Id,
                    Reference = ms.SubmissionReference,
                    LecturerName = ms.Lecturer.UserName,
                    CourseTitle = ms.Course.Title,
                    AcademicYear = ms.AcademicYear,
                    SubmittedAtUtc = ms.SubmittedAtUtc
                })
                .ToListAsync();
        }

        private (int actorId, string actorUsername, string? ipAddress) GetActorContext()
        {
            int.TryParse(User.FindFirst("UserId")?.Value, out int actorId);
            string actorUsername = User.Identity?.Name ?? "Unknown";
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            return (actorId, actorUsername, ipAddress);
        }
    }
}