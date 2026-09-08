using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
    [Authorize(Roles = "HOD")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // HOD INFORMATION
        // ============================================================

        public Hod? CurrentHod { get; private set; }

        public string HodName =>
            CurrentHod?.UserName ?? "Head of Department";

        public string HodDepartment =>
            CurrentHod?.Department ?? "Department";


        // ============================================================
        // DASHBOARD STATISTICS
        // ============================================================

        /// <summary>
        /// Contracts in the department that are currently pending
        /// and require HOD action.
        /// </summary>
        public int ContractsToSign { get; private set; }

        /// <summary>
        /// Contracts that have completed the entire signing workflow
        /// and are currently active.
        /// </summary>
        public int ContractsToReview { get; private set; }

        /// <summary>
        /// Claims currently waiting for HOD approval.
        /// </summary>
        public int ClaimsReceived { get; private set; }

        /// <summary>
        /// Number of distinct active lecturers in the department.
        /// </summary>
        public int AcademicStaff { get; private set; }


        // ============================================================
        // TABLE DATA
        // ============================================================

        public List<ContractDashboardItem> ContractsAwaitingSignature
        {
            get;
            private set;
        } = new();

        public List<ClaimDashboardItem> RecentClaims
        {
            get;
            private set;
        } = new();


        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            // --------------------------------------------------------
            // Identify logged-in HOD
            // --------------------------------------------------------

            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToPage("/Login");
            }

            CurrentHod = await _context.Hods
                .AsNoTracking()
                .FirstOrDefaultAsync(h =>
                    h.UserName == username &&
                    h.IsActive);

            if (CurrentHod == null)
            {
                return RedirectToPage("/Login");
            }

            var department = CurrentHod.Department;


            // ========================================================
            // DEPARTMENT CONTRACTS
            // ========================================================

            var departmentContracts = _context.Contracts
                .AsNoTracking()
                .Where(c =>
                    c.CourseAssignment != null &&
                    c.CourseAssignment.Course.Department == department);


            // --------------------------------------------------------
            // CONTRACTS REQUIRING HOD ACTION
            // --------------------------------------------------------

            ContractsAwaitingSignature = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs =>
                    cs.SignerRole == SignerRole.Dean &&
                    cs.Decision == SignatureDecision.Pending &&
                    cs.Contract.Status == ContractStatus.PendingSignature &&
                    cs.Contract.CourseAssignment != null &&
                    cs.Contract.CourseAssignment.Course.Department == department)
                .OrderByDescending(cs => cs.Contract.CreatedAtUtc)
                .Take(10)
                .Select(cs => new ContractDashboardItem
                {
                    Id = cs.Contract.Id,

                    LecturerName =
                        cs.Contract.Lecturer.UserName,

                    CourseTitle =
                        cs.Contract.CourseAssignment!.Course.Title,

                    Hours =
                        cs.Contract.CourseAssignment.AllocatedHours,

                    Status =
                        cs.Contract.Status
                })
                .ToListAsync();


            ContractsToSign = await _context.ContractSignatures
                .AsNoTracking()
                .CountAsync(cs =>
                    cs.SignerRole == SignerRole.Dean &&
                    cs.Decision == SignatureDecision.Pending &&
                    cs.Contract.Status == ContractStatus.PendingSignature &&
                    cs.Contract.CourseAssignment != null &&
                    cs.Contract.CourseAssignment.Course.Department == department);


            // --------------------------------------------------------
            // ACTIVE CONTRACTS
            // --------------------------------------------------------

            ContractsToReview = await departmentContracts
                .CountAsync(c =>
                    c.Status == ContractStatus.Active);


            // ========================================================
            // DEPARTMENT CLAIMS
            // ========================================================

            var departmentClaims = _context.Claims
                .AsNoTracking()
                .Where(c =>
                    c.CourseAssignment != null &&
                    c.CourseAssignment.Course.Department == department);


            // --------------------------------------------------------
            // CLAIMS WAITING FOR HOD
            // --------------------------------------------------------

            ClaimsReceived = await departmentClaims
                .CountAsync(c =>
                    c.Status == ClaimStatus.PendingHODApproval);


            // --------------------------------------------------------
            // RECENT CLAIMS
            // --------------------------------------------------------

            RecentClaims = await departmentClaims
                .Where(c =>
                    c.Status == ClaimStatus.PendingHODApproval ||
                    c.Status == ClaimStatus.Submitted ||
                    c.Status == ClaimStatus.Rejected ||
                    c.Status == ClaimStatus.Approved ||
                    c.Status == ClaimStatus.Paid)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(10)
                .Select(c => new ClaimDashboardItem
                {
                    Id = c.Id,

                    LecturerName =
                        c.CourseAssignment!.Lecturer.UserName,

                    ContractId =
                        c.ContractId,

                    Status =
                        c.Status
                })
                .ToListAsync();


            // ========================================================
            // ACADEMIC STAFF
            // ========================================================

            AcademicStaff = await _context.CourseAssignments
                .AsNoTracking()
                .Where(ca =>
                    ca.IsActive &&
                    ca.Course.Department == department &&
                    ca.Lecturer.IsActive)
                .Select(ca => ca.LecturerId)
                .Distinct()
                .CountAsync();


            return Page();
        }


        // ============================================================
        // VIEW MODELS
        // ============================================================

        public class ContractDashboardItem
        {
            public int Id { get; set; }

            public string LecturerName { get; set; }
                = string.Empty;

            public string CourseTitle { get; set; }
                = string.Empty;

            public decimal Hours { get; set; }

            public ContractStatus Status { get; set; }
        }


        public class ClaimDashboardItem
        {
            public int Id { get; set; }

            public string LecturerName { get; set; }
                = string.Empty;

            public int ContractId { get; set; }

            public ClaimStatus Status { get; set; }
        }
    }
}