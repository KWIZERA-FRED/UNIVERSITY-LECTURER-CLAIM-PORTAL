using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Management.ExamOffice
{
    [Authorize(Roles = "Management")]
    public class MarkDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MarksSigningService _marksService;

        public MarkDetailsModel(
            ApplicationDbContext context,
            MarksSigningService marksService)
        {
            _context = context;
            _marksService = marksService;
        }

        public MarksReviewDto? SelectedSubmission { get; set; }

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SubmissionId { get; set; }

        [BindProperty]
        public string? DeclineReason { get; set; }

        [BindProperty]
        public bool ScriptsMatchPhysical { get; set; }

        [BindProperty]
        public bool StudentCountMatches { get; set; }


        public class MarksReviewDto
        {
            public int Id { get; set; }

            public string Reference { get; set; } = string.Empty;

            public string LecturerName { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public string FileName { get; set; } = string.Empty;
        }


        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await IsExamOfficeAsync())
            {
                return RedirectToPage("/Management/ManagementDashboard");
            }

            SuccessMessage = TempData["SuccessMessage"] as string;
            ErrorMessage = TempData["ErrorMessage"] as string;

            if (!SubmissionId.HasValue)
            {
                return RedirectToPage("/Management/ExamOffice/Marks");
            }

            var submission = await _context.MarksSubmissions
                .AsNoTracking()
                .Include(ms => ms.Lecturer)
                .Include(ms => ms.Course)
                .FirstOrDefaultAsync(ms =>
                    ms.Id == SubmissionId.Value &&
                    ms.Status == MarksSubmissionStatus.Pending);

            if (submission is null)
            {
                ErrorMessage =
                    "That submission could not be found, or has already been reviewed.";
            }
            else
            {
                SelectedSubmission = new MarksReviewDto
                {
                    Id = submission.Id,
                    Reference = submission.SubmissionReference,
                    LecturerName = submission.Lecturer.UserName,
                    CourseTitle = submission.Course.Title,
                    FileName = submission.FileName
                };
            }

            return Page();
        }


        // ============================================================
        // DOWNLOAD MARKS FILE
        // ============================================================

        public async Task<IActionResult> OnGetDownloadAsync(int submissionId)
        {
            if (!await IsExamOfficeAsync())
            {
                return Forbid();
            }

            if (submissionId <= 0)
            {
                return NotFound();
            }

            var submission = await _context.MarksSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(ms =>
                    ms.Id == submissionId &&
                    ms.Status == MarksSubmissionStatus.Pending);

            if (submission is null)
            {
                return NotFound(
                    "The marks submission could not be found or is no longer awaiting review.");
            }

            if (string.IsNullOrWhiteSpace(submission.SubmissionReference))
            {
                return NotFound("The marks file reference is missing.");
            }

            var fileName = $"{submission.SubmissionReference}.xlsx";

            var filePath = Path.Combine(
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
                fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            }
            catch
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "The marks file could not be read.");
            }

            var downloadName = string.IsNullOrWhiteSpace(submission.FileName)
                ? fileName
                : Path.GetFileName(submission.FileName);

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
            if (!await IsExamOfficeAsync() || !SubmissionId.HasValue)
            {
                return RedirectToPage("/Management/ManagementDashboard");
            }

            // ============================================================
            // GATE ON THE VERIFICATION CHECKLIST
            // ============================================================

            if (!ScriptsMatchPhysical || !StudentCountMatches)
            {
                ErrorMessage =
                    "Please confirm both verification items before signing. " +
                    "The submitted scripts must match the physical scripts, " +
                    "and the student count must match.";

                await LoadSelectedSubmissionAsync(SubmissionId.Value);

                return Page();
            }

            var (actorId, actorUsername, ipAddress) = GetActorContext();

            if (actorId <= 0)
            {
                ErrorMessage =
                    "The Exam Office account could not be identified.";

                await LoadSelectedSubmissionAsync(SubmissionId.Value);

                return Page();
            }

            var result = await _marksService.ReviewAsync(
                actorId,
                true,
                null,
                SubmissionId.Value,
                actorUsername,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;

                await LoadSelectedSubmissionAsync(SubmissionId.Value);

                return Page();
            }

            TempData["SuccessMessage"] =
                "Marks signed. The lecturer can now submit a claim for this course.";

            return RedirectToPage("/Management/ExamOffice/Marks");
        }


        // ============================================================
        // DECLINE
        // ============================================================

        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!await IsExamOfficeAsync() || !SubmissionId.HasValue)
            {
                return RedirectToPage("/Management/ManagementDashboard");
            }

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage =
                    "Please provide a reason for declining this submission.";

                await LoadSelectedSubmissionAsync(SubmissionId.Value);

                return Page();
            }

            var (actorId, actorUsername, ipAddress) = GetActorContext();

            if (actorId <= 0)
            {
                ErrorMessage =
                    "The Exam Office account could not be identified.";

                await LoadSelectedSubmissionAsync(SubmissionId.Value);

                return Page();
            }

            var result = await _marksService.ReviewAsync(
                actorId,
                false,
                DeclineReason,
                SubmissionId.Value,
                actorUsername,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;

                await LoadSelectedSubmissionAsync(SubmissionId.Value);

                return Page();
            }

            TempData["SuccessMessage"] =
                "Marks submission declined. The lecturer has been notified.";

            return RedirectToPage("/Management/ExamOffice/Marks");
        }


        // ============================================================
        // HELPERS
        // ============================================================

        private async Task LoadSelectedSubmissionAsync(int submissionId)
        {
            var submission = await _context.MarksSubmissions
                .AsNoTracking()
                .Include(ms => ms.Lecturer)
                .Include(ms => ms.Course)
                .FirstOrDefaultAsync(ms =>
                    ms.Id == submissionId &&
                    ms.Status == MarksSubmissionStatus.Pending);

            if (submission is null)
            {
                return;
            }

            SelectedSubmission = new MarksReviewDto
            {
                Id = submission.Id,
                Reference = submission.SubmissionReference,
                LecturerName = submission.Lecturer.UserName,
                CourseTitle = submission.Course.Title,
                FileName = submission.FileName
            };
        }


        // ============================================================
        // EXAM OFFICE AUTHORIZATION
        // ============================================================

        private async Task<bool> IsExamOfficeAsync()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            return await _context.ManagementAccounts
                .AsNoTracking()
                .AnyAsync(m =>
                    m.UserName == username &&
                    m.IsActive &&
                    m.Title == ManagementTitle.ExamOffice);
        }


        // ============================================================
        // ACTOR CONTEXT
        // ============================================================

        private (int actorId, string actorUsername, string? ipAddress)
            GetActorContext()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int actorId);

            string actorUsername =
                User.Identity?.Name ?? "Unknown";

            string? ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            return (actorId, actorUsername, ipAddress);
        }
    }
}