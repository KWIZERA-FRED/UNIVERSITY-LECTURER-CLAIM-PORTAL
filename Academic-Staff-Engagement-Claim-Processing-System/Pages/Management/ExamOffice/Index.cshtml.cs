using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Management.ExamOffice
{
    [Authorize(Roles = "Management")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int PendingCount { get; set; }

        public int SignedThisWeekCount { get; set; }

        public int DeclinedThisWeekCount { get; set; }

        public int SignedThisMonthCount { get; set; }

        public List<ActivityItem> RecentActivity { get; set; }
            = new List<ActivityItem>();

        public class ActivityItem
        {
            public int Id { get; set; }

            public string Reference { get; set; } = string.Empty;

            public string LecturerName { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public MarksSubmissionStatus Status { get; set; }

            public DateTime? ReviewedAtUtc { get; set; }

            public string StatusLabel =>
                Status == MarksSubmissionStatus.Signed
                    ? "Signed"
                    : "Declined";

            public string StatusCssClass =>
                Status == MarksSubmissionStatus.Signed
                    ? "is-signed"
                    : "is-declined";
        }

        public async Task OnGetAsync()
        {
            var now = DateTime.UtcNow;

            var startOfWeek = now.Date.AddDays(
                -((int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1));

            var startOfMonth = new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

            PendingCount = await _context.MarksSubmissions
                .AsNoTracking()
                .CountAsync(ms =>
                    ms.Status == MarksSubmissionStatus.Pending);

            SignedThisWeekCount = await _context.MarksSubmissions
                .AsNoTracking()
                .CountAsync(ms =>
                    ms.Status == MarksSubmissionStatus.Signed &&
                    ms.ReviewedAtUtc != null &&
                    ms.ReviewedAtUtc >= startOfWeek);

            DeclinedThisWeekCount = await _context.MarksSubmissions
                .AsNoTracking()
                .CountAsync(ms =>
                    ms.Status == MarksSubmissionStatus.Declined &&
                    ms.ReviewedAtUtc != null &&
                    ms.ReviewedAtUtc >= startOfWeek);

            SignedThisMonthCount = await _context.MarksSubmissions
                .AsNoTracking()
                .CountAsync(ms =>
                    ms.Status == MarksSubmissionStatus.Signed &&
                    ms.ReviewedAtUtc != null &&
                    ms.ReviewedAtUtc >= startOfMonth);

            RecentActivity = await _context.MarksSubmissions
                .AsNoTracking()
                .Include(ms => ms.Lecturer)
                .Include(ms => ms.Course)
                .Where(ms =>
                    ms.Status == MarksSubmissionStatus.Signed ||
                    ms.Status == MarksSubmissionStatus.Declined)
                .OrderByDescending(ms => ms.ReviewedAtUtc)
                .Take(5)
                .Select(ms => new ActivityItem
                {
                    Id = ms.Id,
                    Reference = ms.SubmissionReference,
                    LecturerName = ms.Lecturer.UserName,
                    CourseTitle = ms.Course.Title,
                    Status = ms.Status,
                    ReviewedAtUtc = ms.ReviewedAtUtc
                })
                .ToListAsync();
        }
    }
}