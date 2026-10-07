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

    // Anywhere in the approval chain: submitted, or waiting on the
    // HOD, Dean, Director of Quality or DVCAR.
    public int PendingClaims =>
        Claims.Count(c => c.IsPending);

    public int ApprovedClaims =>
        Claims.Count(c => c.IsApproved);

    public decimal TotalHours =>
        Claims.Sum(c => c.Hours);

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
                    c.Status,

                // The approval chain in signing order, so the page can
                // show how far the claim has got.
                Steps =
                    c.Approvals
                        .OrderBy(a => a.SequenceOrder)
                        .Select(a => new
                        {
                            a.ApprovalRole,
                            a.Decision
                        })
                        .ToList()
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

                Status =
                    c.Status,

                Steps =
                    BuildSteps(
                        c.Steps
                            .Select(s => (s.ApprovalRole, s.Decision))
                            .ToList(),
                        c.Status),

                OpenUrl =
                    $"/Lecturer/ClaimDetail?ClaimId={c.Id}",

                ReviewUrl =
                    string.Empty
            })
            .ToList();
    }

    // ================================================================
    // APPROVAL PROGRESS
    // ================================================================

    private static List<ClaimStep> BuildSteps(
        List<(ApprovalRole Role, ApprovalDecision Decision)> steps,
        ClaimStatus status)
    {
        var result = new List<ClaimStep>();

        var currentAssigned = false;

        // A claim that is rejected, still a draft, or already finished
        // has no step that is "current".
        var inFlight =
            status is not (ClaimStatus.Rejected
                or ClaimStatus.Draft
                or ClaimStatus.Approved
                or ClaimStatus.Paid);

        foreach (var (role, decision) in steps)
        {
            string state;

            if (decision == ApprovalDecision.Approved)
            {
                state = "done";
            }
            else if (decision == ApprovalDecision.Rejected)
            {
                state = "rejected";
            }
            else if (inFlight && !currentAssigned)
            {
                state = "current";
                currentAssigned = true;
            }
            else
            {
                state = "todo";
            }

            result.Add(new ClaimStep
            {
                Role = RoleLabel(role),
                State = state
            });
        }

        return result;
    }

    private static string RoleLabel(ApprovalRole role) =>
        role switch
        {
            ApprovalRole.HOD => "HOD",
            ApprovalRole.Dean => "Dean",
            ApprovalRole.DirectorOfQuality => "Director of Quality",
            ApprovalRole.DVCAR => "DVCAR",
            _ => role.ToString()
        };

    // ================================================================
    // VIEW MODELS
    // ================================================================

    public sealed class ClaimStep
    {
        public string Role { get; init; } =
            string.Empty;

        // done | current | rejected | todo
        public string State { get; init; } =
            "todo";

        public string StateLabel =>
            State switch
            {
                "done" => "Approved",
                "current" => "Waiting",
                "rejected" => "Rejected",
                _ => "Not yet reached"
            };
    }

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

        public ClaimStatus Status { get; init; }

        public List<ClaimStep> Steps { get; init; } =
            new();

        public string OpenUrl { get; init; } =
            "/Lecturer/Claims";

        public string ReviewUrl { get; init; } =
            string.Empty;

        // Same wording the HOD and Dean pages use for each stage.
        public string StatusLabel =>
            Status switch
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
                _ => Status.ToString()
            };

        public bool IsPending =>
            Status is
                ClaimStatus.Submitted
                or ClaimStatus.PendingHODApproval
                or ClaimStatus.PendingDeanApproval
                or ClaimStatus.PendingDirectorOfQualityApproval
                or ClaimStatus.PendingDVCARApproval;

        public bool IsApproved =>
            Status is
                ClaimStatus.Approved
                or ClaimStatus.Paid;

        public string StatusClass =>
            Status switch
            {
                ClaimStatus.Approved => "status-active",
                ClaimStatus.Paid => "status-paid",
                ClaimStatus.Rejected => "status-danger",
                ClaimStatus.Draft => "status-neutral",
                _ => "status-pending"
            };

        public int ApprovedSteps =>
            Steps.Count(s => s.State == "done");

        public int TotalSteps =>
            Steps.Count;

        // One short line under the progress bar.
        public string ProgressText
        {
            get
            {
                if (Status == ClaimStatus.Rejected)
                {
                    var rejectedBy =
                        Steps.FirstOrDefault(s => s.State == "rejected");

                    return rejectedBy is null
                        ? "Rejected"
                        : $"Rejected by {rejectedBy.Role}";
                }

                if (TotalSteps == 0)
                {
                    return Status == ClaimStatus.Draft
                        ? "Not submitted yet"
                        : "Waiting to be routed";
                }

                if (IsApproved)
                    return "All approvals complete";

                return $"{ApprovedSteps} of {TotalSteps} approved";
            }
        }
    }
}