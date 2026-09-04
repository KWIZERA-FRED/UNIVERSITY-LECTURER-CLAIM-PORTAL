using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages
{
    [Authorize(Roles = "Management")]
    public class ManagementDashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ManagementDashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string DisplayName { get; private set; } = string.Empty;
        public ManagementTitle Title { get; private set; }
        public int PendingContractCount { get; private set; }
        public int PendingClaimCount { get; private set; }
        public int PendingMarksCount { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToPage("/Login");

            var management = await _context.ManagementAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserName == username && m.IsActive);

            if (management is null)
                return RedirectToPage("/Login");

            DisplayName = management.UserName;
            Title = management.Title;

            if (Title != ManagementTitle.ExamOffice)
            {
                var signerRole = MapTitleToSignerRole(Title);

                PendingContractCount = await _context.ContractSignatures
                    .Where(cs => cs.SignerRole == signerRole && cs.Decision == SignatureDecision.Pending)
                    .CountAsync();

                var approvalRole = MapTitleToApprovalRole(Title);

                PendingClaimCount = await _context.ClaimApprovals
                    .Where(ca => ca.ApprovalRole == approvalRole && ca.Decision == ApprovalDecision.Pending)
                    .CountAsync();
            }
            else
            {
                PendingMarksCount = await _context.MarksSubmissions
                    .Where(ms => ms.Status == MarksSubmissionStatus.Pending)
                    .CountAsync();
            }

            return Page();
        }

        public static SignerRole MapTitleToSignerRole(ManagementTitle title) => title switch
        {
            ManagementTitle.HROfficer => SignerRole.HROfficer,
            ManagementTitle.DVCAR => SignerRole.DVCAR,
            ManagementTitle.ViceChancellor => SignerRole.ViceChancellor,
            _ => throw new InvalidOperationException("This title does not sign contracts.")
        };

        public static ApprovalRole MapTitleToApprovalRole(ManagementTitle title) => title switch
        {
            ManagementTitle.HROfficer => ApprovalRole.HROfficer,
            ManagementTitle.DVCAR => ApprovalRole.DVCAR,
            ManagementTitle.ViceChancellor => ApprovalRole.ViceChancellor,
            _ => throw new InvalidOperationException("This title does not approve claims.")
        };
    }
}