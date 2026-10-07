using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Management.ExamOffice
{
    [Authorize(Roles = "Management")]
    public class MarksModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MarksModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // A sheet waiting this many days (or more) is highlighted amber,
        // and red once it passes the urgent threshold.
        public const int AttentionAfterDays = 3;

        public const int UrgentAfterDays = 7;

        public List<PendingMarksRow> PendingSubmissions { get; set; } = new();

        public int PendingCount { get; set; }

        public int NeedsAttentionCount { get; set; }

        public int LongestWaitDays { get; set; }

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }


        public class PendingMarksRow
        {
            public int Id { get; set; }

            public string Reference { get; set; } = string.Empty;

            public string LecturerName { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public string AcademicYear { get; set; } = string.Empty;

            public DateTime SubmittedAtUtc { get; set; }

            public int WaitingDays { get; set; }

            public string WaitingLabel =>
                WaitingDays switch
                {
                    0 => "Today",
                    1 => "1 day",
                    _ => $"{WaitingDays} days"
                };

            public string WaitingCssClass =>
                WaitingDays >= UrgentAfterDays
                    ? "wait-pill--late"
                    : WaitingDays >= AttentionAfterDays
                        ? "wait-pill--warn"
                        : string.Empty;
        }


        public async Task<IActionResult> OnGetAsync()
        {
            if (!await IsExamOfficeAsync())
            {
                return RedirectToPage("/Management/ManagementDashboard");
            }

            SuccessMessage = TempData["SuccessMessage"] as string;
            ErrorMessage = TempData["ErrorMessage"] as string;

            await LoadPendingListAsync();

            return Page();
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
        // LOAD PENDING MARKS
        // ============================================================

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

            var now = DateTime.UtcNow;

            foreach (var row in PendingSubmissions)
            {
                row.WaitingDays = Math.Max(
                    0,
                    (int)(now - row.SubmittedAtUtc).TotalDays);
            }

            PendingCount = PendingSubmissions.Count;

            NeedsAttentionCount = PendingSubmissions
                .Count(r => r.WaitingDays >= AttentionAfterDays);

            LongestWaitDays = PendingSubmissions.Count == 0
                ? 0
                : PendingSubmissions.Max(r => r.WaitingDays);
        }
    }
}