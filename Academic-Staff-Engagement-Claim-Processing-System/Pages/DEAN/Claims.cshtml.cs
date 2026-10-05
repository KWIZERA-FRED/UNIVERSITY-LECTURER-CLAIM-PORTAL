using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN;

[Authorize(Roles = "Dean")]
public class ClaimsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ClaimsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    // Claims waiting on the Dean right now, oldest first.
    public List<PendingClaimRow> PendingClaims { get; set; } = new();

    public List<AllClaimRow> AllClaims { get; set; } = new();

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }


    public class PendingClaimRow
    {
        public int ClaimId { get; set; }

        public string LecturerName { get; set; } = string.Empty;

        public int ContractId { get; set; }

        public decimal HoursClaimed { get; set; }

        // When the claim started waiting on the Dean.
        public DateTime WaitingSinceUtc { get; set; }

        public int DaysWaiting { get; set; }

        public string WaitText =>
            DaysWaiting switch
            {
                0 => "since today",
                1 => "for 1 day",
                _ => $"for {DaysWaiting} days"
            };
    }


    public class AllClaimRow
    {
        public int ClaimId { get; set; }

        public string LecturerName { get; set; } = string.Empty;

        public int ContractId { get; set; }

        public decimal HoursClaimed { get; set; }

        public ClaimStatus Status { get; set; }

        // The claim has a Dean approval step, so the details page can open it.
        public bool HasDeanStep { get; set; }

        // It is the Dean's turn to approve this claim right now.
        public bool IsAwaitingYou { get; set; }
    }


    public async Task OnGetAsync()
    {
        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;

        var pending = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(ca =>
                ca.ApprovalRole == ApprovalRole.Dean &&
                ca.Decision == ApprovalDecision.Pending &&
                ca.Claim.Status == ClaimStatus.PendingDeanApproval)
            .Select(ca => new
            {
                ClaimId = ca.Claim.Id,

                LecturerName =
                    ca.Claim.CourseAssignment.Lecturer.UserName,

                ContractId = ca.Claim.ContractId,

                HoursClaimed = ca.Claim.HoursClaimed,

                SubmittedAtUtc = ca.Claim.SubmittedAtUtc,

                CreatedAtUtc = ca.Claim.CreatedAtUtc,

                // When the approver before the Dean finished.
                LastEarlierDecisionUtc =
                    ca.Claim.Approvals
                        .Where(a =>
                            a.SequenceOrder < ca.SequenceOrder &&
                            a.DecidedAtUtc != null)
                        .Max(a => a.DecidedAtUtc)
            })
            .ToListAsync();

        var nowUtc = DateTime.UtcNow;

        PendingClaims = pending
            .Select(p =>
            {
                var since =
                    p.LastEarlierDecisionUtc ??
                    p.SubmittedAtUtc ??
                    p.CreatedAtUtc;

                return new PendingClaimRow
                {
                    ClaimId = p.ClaimId,
                    LecturerName = p.LecturerName,
                    ContractId = p.ContractId,
                    HoursClaimed = p.HoursClaimed,
                    WaitingSinceUtc = since,
                    DaysWaiting = Math.Max(
                        0,
                        (int)Math.Floor((nowUtc - since).TotalDays))
                };
            })
            .OrderBy(r => r.WaitingSinceUtc)
            .ThenBy(r => r.ClaimId)
            .ToList();

        // Every submitted claim in the system, whatever its status.
        // Drafts are left out: they are the lecturer's unsubmitted work.
        AllClaims = await _context.Claims
            .AsNoTracking()
            .Where(c => c.Status != ClaimStatus.Draft)
            .OrderByDescending(c => c.Id)
            .Select(c => new AllClaimRow
            {
                ClaimId = c.Id,

                LecturerName =
                    c.CourseAssignment.Lecturer.UserName,

                ContractId = c.ContractId,

                HoursClaimed = c.HoursClaimed,

                Status = c.Status,

                HasDeanStep =
                    c.Approvals.Any(a =>
                        a.ApprovalRole == ApprovalRole.Dean),

                IsAwaitingYou =
                    c.Status == ClaimStatus.PendingDeanApproval &&
                    c.Approvals.Any(a =>
                        a.ApprovalRole == ApprovalRole.Dean &&
                        a.Decision == ApprovalDecision.Pending)
            })
            .ToListAsync();
    }
}