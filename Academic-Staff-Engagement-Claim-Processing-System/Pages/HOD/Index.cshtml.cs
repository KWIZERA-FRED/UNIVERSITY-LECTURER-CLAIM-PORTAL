using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD
{
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

        public int ContractsToSign { get; private set; }

        public int ContractsToReview { get; private set; }

        public int ClaimsReceived { get; private set; }

        public int AcademicStaff { get; private set; }

        // ============================================================
        // TABLE DATA
        // ============================================================

        public List<ContractDashboardItem> ContractsAwaitingSignature { get; private set; }
            = new();

        public List<ClaimDashboardItem> RecentClaims { get; private set; }
            = new();

        // ============================================================
        // GET
        // ============================================================

        public async Task<IActionResult> OnGetAsync()
        {
            // --------------------------------------------------------
            // Find the currently logged-in HOD
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

            // --------------------------------------------------------
            // Department belonging to this HOD
            // --------------------------------------------------------

            var department = CurrentHod.Department;

            // ========================================================
            // CONTRACTS
            // ========================================================

            /*
             * A contract belongs to a department through:
             *
             * Contract
             *    -> CourseAssignment
             *       -> Course
             *          -> Department
             *
             * We therefore only display contracts whose course belongs
             * to the current HOD's department.
             */

            var departmentContracts = _context.Contracts
                .AsNoTracking()
                .Include(c => c.Lecturer)
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca!.Course)
                .Where(c =>
                    c.CourseAssignment != null &&
                    c.CourseAssignment.Course.Department == department);

            // --------------------------------------------------------
            // Contracts awaiting signature
            // --------------------------------------------------------

            ContractsAwaitingSignature = await departmentContracts
                .Where(c =>
                    c.Status == ContractStatus.PendingSignature)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(10)
                .Select(c => new ContractDashboardItem
                {
                    Id = c.Id,
                    LecturerName = c.Lecturer.UserName,
                    CourseTitle = c.CourseAssignment!.Course.Title,
                    Hours = c.CourseAssignment.AllocatedHours,
                    Status = c.Status
                })
                .ToListAsync();

            ContractsToSign = await departmentContracts
                .CountAsync(c =>
                    c.Status == ContractStatus.PendingSignature);

            // --------------------------------------------------------
            // Contracts to review
            // --------------------------------------------------------

            /*
             * Your current Contract model does not have a separate
             * "Review" status.
             *
             * Therefore ContractsToReview is based on contracts in
             * the HOD's department that have already been signed by
             * the lecturer and are active.
             */

            ContractsToReview = await departmentContracts
                .CountAsync(c =>
                    c.Status == ContractStatus.Active);

            // ========================================================
            // CLAIMS
            // ========================================================

            /*
             * A claim belongs to the HOD's department through:
             *
             * Claim
             *    -> CourseAssignment
             *       -> Course
             *          -> Department
             */

            var departmentClaims = _context.Claims
                .AsNoTracking()
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca.Lecturer)
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca.Course)
                .Include(c => c.Contract)
                .Where(c =>
                    c.CourseAssignment.Course.Department == department);

            // --------------------------------------------------------
            // Claims waiting for HOD approval
            // --------------------------------------------------------

            ClaimsReceived = await departmentClaims
                .CountAsync(c =>
                    c.Status == ClaimStatus.PendingHODApproval);

            // --------------------------------------------------------
            // Recent claims
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
                    LecturerName = c.CourseAssignment.Lecturer.UserName,
                    ContractId = c.ContractId,
                    Status = c.Status
                })
                .ToListAsync();

            // ========================================================
            // ACADEMIC STAFF
            // ========================================================

            /*
             * Count distinct lecturers who have a course assignment
             * belonging to this HOD's department.
             */

            AcademicStaff = await _context.CourseAssignments
                .AsNoTracking()
                .Where(ca =>
                    ca.IsActive &&
                    ca.Course.Department == department)
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

            public string LecturerName { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public decimal Hours { get; set; }

            public ContractStatus Status { get; set; }
        }

        public class ClaimDashboardItem
        {
            public int Id { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public int ContractId { get; set; }

            public ClaimStatus Status { get; set; }
        }
    }
}