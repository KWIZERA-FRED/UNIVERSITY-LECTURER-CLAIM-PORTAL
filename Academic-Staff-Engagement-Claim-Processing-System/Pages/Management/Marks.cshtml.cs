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

        public MarksModel(
            ApplicationDbContext context,
            MarksSigningService marksService)
        {
            _context = context;
            _marksService = marksService;
        }

        public List<PendingMarksRow> PendingSubmissions { get; set; }
            = new();

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

            public string Reference { get; set; }
                = string.Empty;

            public string LecturerName { get; set; }
                = string.Empty;

            public string CourseTitle { get; set; }
                = string.Empty;

            public string AcademicYear { get; set; }
                = string.Empty;

            public DateTime SubmittedAtUtc { get; set; }
        }

        public class MarksReviewDto
        {
            public int Id { get; set; }

            public string Reference { get; set; }
                = string.Empty;

            public string LecturerName { get; set; }
                = string.Empty;

            public string CourseTitle { get; set; }
                = string.Empty;

            public string FileName { get; set; }
                = string.Empty;
        }

        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await IsExamOfficeAsync())
            {
                return RedirectToPage("/ManagementDashboard");
            }

            await LoadPendingListAsync();

            if (SubmissionId.HasValue)
            {
                var submission =
                    await _context.MarksSubmissions
                        .AsNoTracking()
                        .Include(ms => ms.Lecturer)
                        .Include(ms => ms.Course)
                        .FirstOrDefaultAsync(ms =>
                            ms.Id == SubmissionId.Value &&
                            ms.Status ==
                                MarksSubmissionStatus.Pending);

                if (submission is null)
                {
                    ErrorMessage =
                        "That submission could not be found, or has already been reviewed.";
                }
                else
                {
                    SelectedSubmission =
                        new MarksReviewDto
                        {
                            Id =
                                submission.Id,

                            Reference =
                                submission.SubmissionReference,

                            LecturerName =
                                submission.Lecturer.UserName,

                            CourseTitle =
                                submission.Course.Title,

                            FileName =
                                submission.FileName
                        };
                }
            }

            return Page();
        }

        // ============================================================
        // DOWNLOAD MARKS FILE
        // ============================================================

        public async Task<IActionResult> OnGetDownloadAsync(
            int submissionId)
        {
            if (!await IsExamOfficeAsync())
            {
                return Forbid();
            }

            if (submissionId <= 0)
            {
                return NotFound();
            }

            var submission =
                await _context.MarksSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ms =>
                        ms.Id == submissionId &&
                        ms.Status ==
                            MarksSubmissionStatus.Pending);

            if (submission == null)
            {
                return NotFound(
                    "The marks submission could not be found or is no longer awaiting review.");
            }

            if (string.IsNullOrWhiteSpace(
                    submission.SubmissionReference))
            {
                return NotFound(
                    "The marks file reference is missing.");
            }

            string fileName =
                $"{submission.SubmissionReference}.xlsx";

            string filePath =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "marks",
                    fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(
                    "The marks file could not be found in application storage.");
            }

            byte[] fileBytes;

            try
            {
                fileBytes =
                    await System.IO.File.ReadAllBytesAsync(
                        filePath);
            }
            catch
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "The marks file could not be read.");
            }

            string downloadName =
                string.IsNullOrWhiteSpace(
                    submission.FileName)
                    ? fileName
                    : Path.GetFileName(
                        submission.FileName);

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                downloadName);
        }

        // ============================================================
        // APPROVE / SIGN
        // ============================================================

        public async Task<IActionResult> OnPostApproveAsync()
        {
            if (!await IsExamOfficeAsync() ||
                !SubmissionId.HasValue)
            {
                return RedirectToPage("/ManagementDashboard");
            }

            var (actorId, actorUsername, ipAddress) =
                GetActorContext();

            if (actorId <= 0)
            {
                ErrorMessage =
                    "The Exam Office account could not be identified.";

                await LoadPendingListAsync();

                return Page();
            }

            var result =
                await _marksService.ReviewAsync(
                    actorId,
                    true,
                    null,
                    SubmissionId.Value,
                    actorUsername,
                    ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage;
            }
            else
            {
                SuccessMessage =
                    "Marks signed. The lecturer can now submit a claim for this course.";
            }

            await LoadPendingListAsync();

            SubmissionId = null;
            SelectedSubmission = null;

            return Page();
        }

        // ============================================================
        // DECLINE
        // ============================================================

        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!await IsExamOfficeAsync() ||
                !SubmissionId.HasValue)
            {
                return RedirectToPage("/ManagementDashboard");
            }

            if (string.IsNullOrWhiteSpace(
                    DeclineReason))
            {
                ErrorMessage =
                    "Please provide a reason for declining this submission.";

                await LoadPendingListAsync();

                return Page();
            }

            var (actorId, actorUsername, ipAddress) =
                GetActorContext();

            if (actorId <= 0)
            {
                ErrorMessage =
                    "The Exam Office account could not be identified.";

                await LoadPendingListAsync();

                return Page();
            }

            var result =
                await _marksService.ReviewAsync(
                    actorId,
                    false,
                    DeclineReason,
                    SubmissionId.Value,
                    actorUsername,
                    ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage;
            }
            else
            {
                SuccessMessage =
                    "Marks submission declined.";
            }

            await LoadPendingListAsync();

            SubmissionId = null;
            SelectedSubmission = null;
            DeclineReason = null;

            return Page();
        }

        // ============================================================
        // EXAM OFFICE AUTHORIZATION
        // ============================================================

        private async Task<bool> IsExamOfficeAsync()
        {
            var username =
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(
                    username))
            {
                return false;
            }

            return await _context.ManagementAccounts
                .AsNoTracking()
                .AnyAsync(m =>
                    m.UserName == username &&
                    m.IsActive &&
                    m.Title ==
                        ManagementTitle.ExamOffice);
        }

        // ============================================================
        // LOAD PENDING MARKS
        // ============================================================

        private async Task LoadPendingListAsync()
        {
            PendingSubmissions =
                await _context.MarksSubmissions
                    .AsNoTracking()
                    .Where(ms =>
                        ms.Status ==
                        MarksSubmissionStatus.Pending)
                    .Include(ms => ms.Lecturer)
                    .Include(ms => ms.Course)
                    .OrderBy(ms => ms.SubmittedAtUtc)
                    .Select(ms =>
                        new PendingMarksRow
                        {
                            Id =
                                ms.Id,

                            Reference =
                                ms.SubmissionReference,

                            LecturerName =
                                ms.Lecturer.UserName,

                            CourseTitle =
                                ms.Course.Title,

                            AcademicYear =
                                ms.AcademicYear,

                            SubmittedAtUtc =
                                ms.SubmittedAtUtc
                        })
                    .ToListAsync();
        }

        // ============================================================
        // ACTOR CONTEXT
        // ============================================================

        private (
            int actorId,
            string actorUsername,
            string? ipAddress) GetActorContext()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int actorId);

            string actorUsername =
                User.Identity?.Name ??
                "Unknown";

            string? ipAddress =
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString();

            return (
                actorId,
                actorUsername,
                ipAddress);
        }
    }
}