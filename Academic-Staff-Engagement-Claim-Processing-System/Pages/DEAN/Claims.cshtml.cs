using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

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

    public List<PendingClaimRow> PendingClaims { get; set; } = new();

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }


    public class PendingClaimRow
    {
        public int ClaimId { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public int ContractId { get; set; }
        public decimal HoursClaimed { get; set; }
    }


    public async Task OnGetAsync()
    {
        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;

        PendingClaims = await _context.ClaimApprovals
            .Where(ca =>
                ca.ApprovalRole == ApprovalRole.Dean &&
                ca.Decision == ApprovalDecision.Pending)
            .Include(ca => ca.Claim)
                .ThenInclude(c => c.CourseAssignment)
                    .ThenInclude(ca2 => ca2.Lecturer)
            .Select(ca => new PendingClaimRow
            {
                ClaimId = ca.Claim.Id,

                LecturerName =
                    ca.Claim.CourseAssignment.Lecturer.UserName,

                ContractId = ca.Claim.ContractId,

                HoursClaimed = ca.Claim.HoursClaimed
            })
            .ToListAsync();
    }
}