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

        // ============================================================
        // CURRENT MANAGEMENT MEMBER
        // ============================================================

        public string DisplayName { get; private set; } = string.Empty;

        public ManagementTitle Title { get; private set; }

        public string RoleLabel => Title switch
        {
            ManagementTitle.HROfficer => "HR Officer",
            ManagementTitle.DVCAR => "DVCAR",
            ManagementTitle.ViceChancellor => "Vice Chancellor",
            ManagementTitle.ExamOffice => "Exam Officer",
            ManagementTitle.DirectorOfQuality => "Director of Quality",
            _ => Title.ToString()
        };

        // ============================================================
        // DASHBOARD COUNTERS
        // ============================================================

        public int PendingContractCount { get; private set; }

        public int MySignedContractCount { get; private set; }

        public int InSigningProcessCount { get; private set; }

        public int ActiveContractsCount { get; private set; }

        // ============================================================
        // CONTRACTS AWAITING CURRENT USER
        // ============================================================

        public List<PendingContractRow> PendingContracts { get; private set; } = new();

        public class PendingContractRow
        {
            public int ContractId { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public string Department { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public decimal AllocatedHours { get; set; }

            public decimal RatePerHour { get; set; }

            public string Version { get; set; } = string.Empty;

            public DateTime CreatedAtUtc { get; set; }

            public string CurrentStage { get; set; } = string.Empty;
        }

        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
                return RedirectToPage("/Login");

            var management = await _context.ManagementAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.UserName == username &&
                    m.IsActive);

            if (management is null)
                return RedirectToPage("/Login");

            DisplayName = management.UserName;
            Title = management.Title;

            // ========================================================
            // ROLE ROUTING
            // ========================================================
            //
            // The contract dashboard is for management members who
            // actually participate in the contract-signing chain:
            //   HR Officer → DVCAR → Vice Chancellor
            //
            // Everyone else is sent to the domain page that matches
            // their responsibility:
            //   Exam Office        → verifies marks
            //   Director of Quality → reviews claims
            //

            if (!IsContractSigner(Title))
            {
                return Title switch
                {
                    ManagementTitle.ExamOffice =>
                        RedirectToPage("/Management/Marks"),

                    ManagementTitle.DirectorOfQuality =>
                        RedirectToPage("/Management/Claims"),

                    _ => RedirectToPage("/Login")
                };
            }

            var signerRole = MapTitleToSignerRole(Title);

            // ========================================================
            // CONTRACTS AWAITING THIS MANAGEMENT MEMBER
            // ========================================================

            PendingContracts = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs =>
                    cs.SignerRole == signerRole &&
                    cs.Decision == SignatureDecision.Pending &&
                    cs.Contract.Status == ContractStatus.PendingSignature)
                .Include(cs => cs.Contract)
                    .ThenInclude(c => c.Lecturer)
                .Include(cs => cs.Contract)
                    .ThenInclude(c => c.CourseAssignment)
                        .ThenInclude(ca => ca!.Course)
                .Select(cs => new PendingContractRow
                {
                    ContractId = cs.Contract.Id,

                    LecturerName = cs.Contract.Lecturer.UserName,

                    Department = cs.Contract.CourseAssignment != null
                        ? cs.Contract.CourseAssignment.Course.Department
                        : "—",

                    CourseTitle = cs.Contract.CourseAssignment != null
                        ? cs.Contract.CourseAssignment.Course.Title
                        : "—",

                    AllocatedHours = cs.Contract.CourseAssignment != null
                        ? cs.Contract.CourseAssignment.AllocatedHours
                        : 0,

                    RatePerHour = cs.Contract.RatePerHour,

                    Version = cs.Contract.Version,

                    CreatedAtUtc = cs.Contract.CreatedAtUtc,

                    CurrentStage = GetStageLabel(cs.SignerRole)
                })
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();

            PendingContractCount = PendingContracts.Count;

            // ========================================================
            // CONTRACTS SIGNED BY THIS MANAGEMENT MEMBER
            // ========================================================

            MySignedContractCount = await _context.ContractSignatures
                .AsNoTracking()
                .CountAsync(cs =>
                    cs.SignerRole == signerRole &&
                    cs.SignedByAdminAccountId == management.Id &&
                    cs.Decision == SignatureDecision.Signed);

            // ========================================================
            // CONTRACTS CURRENTLY MOVING THROUGH SIGNING WORKFLOW
            // ========================================================

            InSigningProcessCount = await _context.Contracts
                .AsNoTracking()
                .CountAsync(c =>
                    c.Status == ContractStatus.PendingSignature);

            // ========================================================
            // ACTIVE CONTRACTS
            // ========================================================

            ActiveContractsCount = await _context.Contracts
                .AsNoTracking()
                .CountAsync(c =>
                    c.Status == ContractStatus.Active);

            return Page();
        }

        // ============================================================
        // CONTRACT SIGNER VALIDATION
        // ============================================================
        //
        // Only HR Officer, DVCAR, and Vice Chancellor sign contracts.
        //
        // Exam Office verifies marks — a separate workflow.
        // Director of Quality reviews claims — a separate workflow.
        //

        private static bool IsContractSigner(ManagementTitle title)
        {
            return title == ManagementTitle.HROfficer
                || title == ManagementTitle.DVCAR
                || title == ManagementTitle.ViceChancellor;
        }

        // ============================================================
        // MANAGEMENT TITLE → SIGNER ROLE
        // ============================================================

        public static SignerRole MapTitleToSignerRole(ManagementTitle title) => title switch
        {
            ManagementTitle.HROfficer => SignerRole.HROfficer,

            ManagementTitle.DVCAR => SignerRole.DVCAR,

            ManagementTitle.ViceChancellor => SignerRole.ViceChancellor,

            _ => throw new InvalidOperationException(
                "This management title does not participate in contract signing.")
        };

        // ============================================================
        // SIGNING STAGE LABEL
        // ============================================================

        private static string GetStageLabel(SignerRole role)
        {
            return role switch
            {
                SignerRole.Lecturer => "Lecturer",

                SignerRole.Dean => "Dean",

                SignerRole.HROfficer => "HR Officer",

                SignerRole.DVCAR => "DVCAR",

                SignerRole.ViceChancellor => "Vice Chancellor",

                _ => role.ToString()
            };
        }

        // ============================================================
        // LEGACY CLAIMS ROLE MAPPING
        // ============================================================
        //
        // Kept temporarily so the existing Claims page continues
        // to compile while the current development focus is contracts.
        //

        public static ApprovalRole MapTitleToApprovalRole(ManagementTitle title) => title switch
        {
            ManagementTitle.HROfficer => ApprovalRole.HROfficer,
            ManagementTitle.DVCAR => ApprovalRole.DVCAR,
            ManagementTitle.ViceChancellor => ApprovalRole.ViceChancellor,

            _ => throw new InvalidOperationException(
                "This management title does not approve claims.")
        };
    }
}