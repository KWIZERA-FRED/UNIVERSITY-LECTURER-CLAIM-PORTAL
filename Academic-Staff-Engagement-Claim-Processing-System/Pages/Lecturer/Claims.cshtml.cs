using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer;

[Authorize(Roles = "Lecturer")]
public class ClaimsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ClaimsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public string CurrentUserName { get; private set; } = string.Empty;

    public string CurrentUserRole { get; private set; } = string.Empty;

    public List<ClaimItem> Claims { get; private set; } = new();

    public string? SuccessMessage { get; private set; }

    public bool CanReviewClaims =>
        CurrentUserRole == "Dean";

    public int TotalClaims =>
        Claims.Count;

    // Claims still moving through the approval chain.
    public int PendingClaims =>
        Claims.Count(c => c.IsPending);

    public int ApprovedClaims =>
        Claims.Count(c => c.Status is "Approved" or "Paid");

    public decimal TotalHours =>
        Claims.Sum(c => c.Hours);

    // Claims that need the lecturer to do something (rejected ones).
    public List<ClaimItem> AttentionClaims =>
        Claims
            .Where(c => c.NeedsAttention)
            .ToList();

    public async Task OnGetAsync()
    {
        CurrentUserName =
            User.Identity?.Name ?? "Lecturer";

        CurrentUserRole =
            User.FindFirstValue(ClaimTypes.Role) ?? "Lecturer";

        SuccessMessage =
            TempData["SuccessMessage"] as string;

        var userIdValue =
            User.FindFirstValue("UserId");

        if (!int.TryParse(userIdValue, out var lecturerId))
        {
            Claims = new();
            return;
        }

        var claims = await _context.Claims
            .AsNoTracking()
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Course)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Lecturer)
            .Where(c =>
                c.CourseAssignment.LecturerId == lecturerId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                Id = c.Id,

                LecturerName =
                    c.CourseAssignment.Lecturer.UserName,

                CourseCode =
                    c.CourseAssignment.Course.Code,

                CourseName =
                    c.CourseAssignment.Course.Title,

                AcademicYear =
                    c.CourseAssignment.AcademicYear,

                Campus =
                    c.CourseAssignment.Campus,

                Hours =
                    c.HoursClaimed,

                Status =
                    c.Status
            })
            .ToListAsync();

        Claims = claims
            .Select(c => new ClaimItem
            {
                Id = c.Id,

                ClaimNumber =
                    $"CLM-{c.Id:D6}",

                LecturerName =
                    c.LecturerName,

                CourseCode =
                    c.CourseCode,

                CourseName =
                    c.CourseName,

                AcademicYear =
                    c.AcademicYear,

                Campus =
                    c.Campus.ToString(),

                Hours =
                    c.Hours,

                RawStatus =
                    c.Status,

                Status =
                    ToLabel(c.Status),

                OpenUrl =
                    $"/Lecturer/ClaimDetail?ClaimId={c.Id}",

                ReviewUrl =
                    string.Empty
            })
            .ToList();
    }

    // Same wording as the HOD and Dean claim lists, so a claim reads
    // the same way for everyone who handles it.
    private static string ToLabel(ClaimStatus status) =>
        status switch
        {
            ClaimStatus.Draft => "Draft",
            ClaimStatus.Submitted => "Submitted",
            ClaimStatus.PendingHODApproval => "Pending HOD",
            ClaimStatus.PendingDeanApproval => "Pending Dean",
            ClaimStatus.PendingDirectorOfQualityApproval => "Pending Director of Quality",
            ClaimStatus.PendingDVCARApproval => "Pending DVCAR",
            ClaimStatus.Approved => "Approved",
            ClaimStatus.Rejected => "Rejected",
            ClaimStatus.Paid => "Paid",
            _ => status.ToString()
        };

    public sealed class ClaimItem
    {
        public int Id { get; init; }

        public string ClaimNumber { get; init; } =
            string.Empty;

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseCode { get; init; } =
            string.Empty;

        public string CourseName { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public string Campus { get; init; } =
            string.Empty;

        public decimal Hours { get; init; }

        public ClaimStatus RawStatus { get; init; }

        public string Status { get; init; } =
            string.Empty;

        public string OpenUrl { get; init; } =
            "/Lecturer/Claims";

        public string ReviewUrl { get; init; } =
            string.Empty;

        // Submitted, or waiting on any approver in the chain.
        public bool IsPending =>
            RawStatus is
                ClaimStatus.Submitted
                or ClaimStatus.PendingHODApproval
                or ClaimStatus.PendingDeanApproval
                or ClaimStatus.PendingDirectorOfQualityApproval
                or ClaimStatus.PendingDVCARApproval;

        // A rejected claim is the only state that asks the lecturer to act.
        public bool NeedsAttention =>
            RawStatus == ClaimStatus.Rejected;

        public string StatusClass =>
            RawStatus switch
            {
                ClaimStatus.Approved => "status-active",
                ClaimStatus.Paid => "status-paid",
                ClaimStatus.Rejected => "status-danger",
                ClaimStatus.Draft => "status-neutral",
                _ => "status-pending"
            };
    }
}