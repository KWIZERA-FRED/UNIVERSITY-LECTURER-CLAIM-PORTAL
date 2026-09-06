using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN
{
    [Authorize(Roles = "Dean")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // DASHBOARD STATISTICS
        // ============================================================

        public int ContractsToSignCount { get; set; }

        public int ActiveContractsCount { get; set; }

        public int ClaimsToSignCount { get; set; }

        public int CompletedApprovalsCount { get; set; }


        // ============================================================
        // DASHBOARD TABLES
        // ============================================================

        public List<ContractSignRow> ContractsAwaitingSignature { get; set; }
            = new();

        public List<ClaimSignRow> ClaimsAwaitingSignature { get; set; }
            = new();


        // ============================================================
        // CONTRACT ROW
        // ============================================================

        public class ContractSignRow
        {
            public int ContractId { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public string Department { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public decimal AllocatedHours { get; set; }
        }


        // ============================================================
        // CLAIM ROW
        // ============================================================

        public class ClaimSignRow
        {
            public int ClaimId { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public int ContractId { get; set; }

            public decimal HoursClaimed { get; set; }
        }


        // ============================================================
        // GET
        // ============================================================

        public async Task OnGetAsync()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int currentDeanId
            );


            // ========================================================
            // CONTRACTS AWAITING DEAN SIGNATURE
            // ========================================================

            ContractsAwaitingSignature = await _context.ContractSignatures

                .Where(cs =>
                    cs.SignerRole == SignerRole.Dean &&
                    cs.Decision == SignatureDecision.Pending)

                .Include(cs => cs.Contract)
                    .ThenInclude(c => c.Lecturer)

                .Include(cs => cs.Contract)
                    .ThenInclude(c => c.CourseAssignment)
                        .ThenInclude(ca => ca!.Course)

                .Select(cs => new ContractSignRow
                {
                    ContractId = cs.Contract.Id,

                    LecturerName =
                        cs.Contract.Lecturer.UserName,

                    Department =
                        cs.Contract.CourseAssignment != null
                            ? cs.Contract.CourseAssignment.Course.Department
                            : "—",

                    CourseTitle =
                        cs.Contract.CourseAssignment != null
                            ? cs.Contract.CourseAssignment.Course.Title
                            : "—",

                    AllocatedHours =
                        cs.Contract.CourseAssignment != null
                            ? cs.Contract.CourseAssignment.AllocatedHours
                            : 0
                })

                .ToListAsync();


            ContractsToSignCount =
                ContractsAwaitingSignature.Count;


            // ========================================================
            // CLAIMS AWAITING DEAN SIGNATURE
            // ========================================================

            ClaimsAwaitingSignature = await _context.ClaimApprovals

                .Where(ca =>
                    ca.ApprovalRole == ApprovalRole.Dean &&
                    ca.Decision == ApprovalDecision.Pending)

                .Include(ca => ca.Claim)
                    .ThenInclude(c => c.CourseAssignment)
                        .ThenInclude(courseAssignment =>
                            courseAssignment.Lecturer)

                .Select(ca => new ClaimSignRow
                {
                    ClaimId = ca.Claim.Id,

                    LecturerName =
                        ca.Claim.CourseAssignment.Lecturer.UserName,

                    ContractId =
                        ca.Claim.ContractId,

                    HoursClaimed =
                        ca.Claim.HoursClaimed
                })

                .ToListAsync();


            ClaimsToSignCount =
                ClaimsAwaitingSignature.Count;


            // ========================================================
            // ACTIVE CONTRACTS
            // ========================================================

            ActiveContractsCount =
                await _context.Contracts
                    .CountAsync(c =>
                        c.Status == ContractStatus.Active);


            // ========================================================
            // COMPLETED APPROVALS BY THIS DEAN
            // ========================================================

            int completedContractSignatures =
                await _context.ContractSignatures

                    .CountAsync(cs =>
                        cs.SignedByAdminAccountId == currentDeanId &&
                        cs.Decision == SignatureDecision.Signed);


            int completedClaimApprovals =
                await _context.ClaimApprovals

                    .CountAsync(ca =>
                        ca.ApprovedByAdminAccountId == currentDeanId &&
                        ca.Decision == ApprovalDecision.Approved);


            CompletedApprovalsCount =
                completedContractSignatures +
                completedClaimApprovals;
        }
    }
}