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
            CurrentHod?.UserName ?? "Head of Faculty";

        public string HodFaculty =>
            CurrentHod?.Faculty.ToString() ?? "Faculty";


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

            var facultyDepartments = FacultyDepartments
                .GetDepartments(CurrentHod.Faculty)
                .Select(d => d.ToString())
                .ToList();


            // ========================================================
            // FACULTY CONTRACTS
            // ========================================================

            var facultyContracts = _context.Contracts
                .AsNoTracking()
                .Where(c =>
                    c.CourseAssignment != null &&
                    facultyDepartments.Contains(
                        c.CourseAssignment.Course.Department));


            // ========================================================
            // CONTRACTS REQUIRING HOD ACTION
            // ========================================================

            ContractsAwaitingSignature = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs =>
                    cs.SignerRole == SignerRole.Dean &&
                    cs.Decision == SignatureDecision.Pending &&
                    cs.Contract.Status == ContractStatus.PendingSignature &&
                    cs.Contract.CourseAssignment != null &&
                    facultyDepartments.Contains(
                        cs.Contract.CourseAssignment.Course.Department))
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
                    facultyDepartments.Contains(
                        cs.Contract.CourseAssignment.Course.Department));


            // ========================================================
            // ACTIVE CONTRACTS
            // ========================================================

            ContractsToReview = await facultyContracts
                .CountAsync(c =>
                    c.Status == ContractStatus.Active);


            // ========================================================
            // FACULTY CLAIMS
            // ========================================================

            var facultyClaims = _context.Claims
                .AsNoTracking()
                .Where(c =>
                    c.CourseAssignment != null &&
                    facultyDepartments.Contains(
                        c.CourseAssignment.Course.Department));


            // ========================================================
            // CLAIMS WAITING FOR HOD
            // ========================================================

            ClaimsReceived = await facultyClaims
                .CountAsync(c =>
                    c.Status == ClaimStatus.PendingHODApproval);


            // ========================================================
            // RECENT CLAIMS
            // ========================================================

            RecentClaims = await facultyClaims
                .Where(c =>
                    c.Status == ClaimStatus.PendingHODApproval ||
                    c.Status == ClaimStatus.PendingDeanApproval ||
                    c.Status == ClaimStatus.PendingDirectorOfQualityApproval ||
                    c.Status == ClaimStatus.PendingDVCARApproval ||
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
                    facultyDepartments.Contains(
                        ca.Course.Department) &&
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