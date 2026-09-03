using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Shared;

[Authorize]
public class ClaimsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ClaimsModel(ApplicationDbContext context) => _context = context;

    public string CurrentUserName { get; private set; } = string.Empty;
    public string CurrentUserRole { get; private set; } = string.Empty;
    public List<ClaimItem> Claims { get; private set; } = new();
    public string? SuccessMessage { get; private set; }

    public bool CanReviewClaims => CurrentUserRole == "Dean";
    public int TotalClaims => Claims.Count;
    public int PendingClaims => Claims.Count(c => c.IsPending);
    public int ApprovedClaims => Claims.Count(c => c.Status == "Approved");
    public decimal TotalHours => Claims.Sum(c => c.Hours);

    public async Task OnGetAsync()
    {
        CurrentUserName = User.Identity?.Name ?? "User";
        CurrentUserRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";
        SuccessMessage = TempData["SuccessMessage"] as string;

        var query = _context.Claims
            .AsNoTracking()
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Course)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Lecturer)
            .AsQueryable();

        if (CurrentUserRole == "Lecturer" &&
            int.TryParse(User.FindFirstValue("UserId"), out var lecturerId))
        {
            query = query.Where(c => c.CourseAssignment.LecturerId == lecturerId);
        }
        else if (CurrentUserRole == "Dean")
        {
            query = query.Where(c => c.Approvals.Any(a => a.ApprovalRole == ApprovalRole.Dean));
        }
        else
        {
            Claims = new();
            return;
        }

        var claims = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                Id = c.Id,
                LecturerName = c.CourseAssignment.Lecturer.UserName,
                CourseCode = c.CourseAssignment.Course.Code,
                CourseName = c.CourseAssignment.Course.Title,
                AcademicYear = c.CourseAssignment.AcademicYear,
                Campus = c.CourseAssignment.Campus,
                Hours = c.HoursClaimed,
                Status = c.Status
            })
            .ToListAsync();

        Claims = claims
            .Select(c => new ClaimItem
            {
                Id = c.Id,
                ClaimNumber = $"CLM-{c.Id:D6}",
                LecturerName = c.LecturerName,
                CourseCode = c.CourseCode,
                CourseName = c.CourseName,
                AcademicYear = c.AcademicYear,
                Campus = c.Campus.ToString(),
                Hours = c.Hours,
                Status = c.Status is ClaimStatus.PendingHODApproval or ClaimStatus.PendingDeanApproval
                    ? "Under Review"
                    : c.Status.ToString(),
                OpenUrl = CurrentUserRole == "Lecturer"
                    ? $"/Lecturer/ClaimDetail?ClaimId={c.Id}"
                    : $"/DEAN/Claims?ClaimId={c.Id}",
                ReviewUrl = $"/DEAN/Claims?ClaimId={c.Id}"
            })
            .ToList();
    }

    public sealed class ClaimItem
    {
        public int Id { get; init; }
        public string ClaimNumber { get; init; } = string.Empty;
        public string LecturerName { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
        public string AcademicYear { get; init; } = string.Empty;
        public string Campus { get; init; } = string.Empty;
        public decimal Hours { get; init; }
        public string Status { get; init; } = string.Empty;
        public string OpenUrl { get; init; } = "/Claims";
        public string ReviewUrl { get; init; } = string.Empty;
        public bool IsPending => Status is "Submitted" or "Under Review";

        public string StatusClass => Status switch
        {
            "Approved" or "Paid" => "approved",
            "Rejected" => "rejected",
            "Under Review" => "processing",
            _ => "pending"
        };

        public string StatusIcon => Status switch
        {
            "Approved" or "Paid" => "bi-check-circle",
            "Rejected" => "bi-x-circle",
            "Under Review" => "bi-arrow-repeat",
            _ => "bi-clock"
        };
    }
}