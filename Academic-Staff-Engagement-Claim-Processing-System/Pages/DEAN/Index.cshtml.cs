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
        private const int MaxRowsPerTable = 8;

        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }


        // ============================================================
        // DASHBOARD STATISTICS
        // ============================================================

        // "To sign" = the Dean's turn has actually come.
        public int ContractsToSignCount { get; set; }

        // Pending for the Dean but waiting on an earlier signer.
        public int ContractsWaitingCount { get; set; }

        public int ContractsTotalPending { get; set; }

        public int ActiveContractsCount { get; set; }

        public int ClaimsToSignCount { get; set; }

        public int ClaimsWaitingCount { get; set; }

        public int ClaimsTotalPending { get; set; }

        public int CompletedApprovalsCount { get; set; }

        public int TotalReadyCount =>
            ContractsToSignCount + ClaimsToSignCount;


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

            public bool IsReady { get; set; }

            public string? WaitingFor { get; set; }

            public DateTime? WaitingSince { get; set; }

            public int? DaysWaiting { get; set; }
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

            public bool IsReady { get; set; }

            public string? WaitingFor { get; set; }

            public DateTime? WaitingSince { get; set; }

            public int? DaysWaiting { get; set; }
        }


        // ============================================================
        // VIEW HELPERS
        // ============================================================

        public string AgeText(int? days)
        {
            if (!days.HasValue)
                return "—";

            return days.Value switch
            {
                0 => "Today",
                1 => "1 day",
                _ => $"{days.Value} days"
            };
        }

        public string AgeCss(int? days)
        {
            if (!days.HasValue)
                return "age-none";

            if (days.Value >= 7)
                return "age-late";

            if (days.Value >= 3)
                return "age-warn";

            return "age-normal";
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

            var now = DateTime.UtcNow;


            // ========================================================
            // CONTRACTS PENDING ON THE DEAN
            // ========================================================

            var pendingContracts = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs =>
                    cs.SignerRole == SignerRole.Dean &&
                    cs.Decision == SignatureDecision.Pending)
                .Select(cs => new
                {
                    cs.ContractId,
                    cs.SequenceOrder,

                    ContractCreatedAtUtc =
                        cs.Contract.CreatedAtUtc,

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
                            : 0m
                })
                .ToListAsync();

            var contractIds =
                pendingContracts
                    .Select(c => c.ContractId)
                    .Distinct()
                    .ToList();

            var contractSteps = await _context.ContractSignatures
                .AsNoTracking()
                .Where(s => contractIds.Contains(s.ContractId))
                .Select(s => new
                {
                    s.ContractId,
                    s.SequenceOrder,
                    s.SignerRole,
                    s.Decision,
                    s.SignedAtUtc
                })
                .ToListAsync();

            var contractRows = new List<ContractSignRow>();

            foreach (var pending in pendingContracts)
            {
                var steps =
                    contractSteps
                        .Where(s => s.ContractId == pending.ContractId)
                        .ToList();

                // A declined contract can no longer be signed.
                if (steps.Any(s => s.Decision == SignatureDecision.Declined))
                    continue;

                var earlier =
                    steps
                        .Where(s => s.SequenceOrder < pending.SequenceOrder)
                        .ToList();

                var blocking =
                    earlier
                        .Where(s => s.Decision != SignatureDecision.Signed)
                        .OrderBy(s => s.SequenceOrder)
                        .FirstOrDefault();

                var isReady = blocking is null;

                DateTime? since = null;

                if (isReady)
                {
                    var lastSigned =
                        earlier
                            .Where(s => s.SignedAtUtc.HasValue)
                            .Select(s => s.SignedAtUtc!.Value)
                            .OrderByDescending(d => d)
                            .Cast<DateTime?>()
                            .FirstOrDefault();

                    since = lastSigned ?? pending.ContractCreatedAtUtc;
                }

                int? days =
                    since.HasValue
                        ? Math.Max(0, (int)Math.Floor((now - since.Value).TotalDays))
                        : null;

                contractRows.Add(new ContractSignRow
                {
                    ContractId = pending.ContractId,
                    LecturerName = pending.LecturerName,
                    Department = FormatDepartment(pending.Department),
                    CourseTitle = pending.CourseTitle,
                    AllocatedHours = pending.AllocatedHours,
                    IsReady = isReady,
                    WaitingFor = blocking is null
                        ? null
                        : SignerLabel(blocking.SignerRole),
                    WaitingSince = since,
                    DaysWaiting = days
                });
            }

            ContractsTotalPending = contractRows.Count;

            ContractsToSignCount =
                contractRows.Count(r => r.IsReady);

            ContractsWaitingCount =
                contractRows.Count - ContractsToSignCount;

            ContractsAwaitingSignature =
                contractRows
                    .OrderByDescending(r => r.IsReady)
                    .ThenBy(r => r.WaitingSince ?? DateTime.MaxValue)
                    .Take(MaxRowsPerTable)
                    .ToList();


            // ========================================================
            // CLAIMS PENDING ON THE DEAN
            // ========================================================

            var pendingClaims = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(ca =>
                    ca.ApprovalRole == ApprovalRole.Dean &&
                    ca.Decision == ApprovalDecision.Pending &&
                    ca.Claim.Status != ClaimStatus.Rejected)
                .Select(ca => new
                {
                    ca.ClaimId,
                    ca.SequenceOrder,

                    LecturerName =
                        ca.Claim.CourseAssignment.Lecturer.UserName,

                    ca.Claim.ContractId,

                    ca.Claim.HoursClaimed,

                    ClaimCreatedAtUtc =
                        ca.Claim.SubmittedAtUtc ?? ca.Claim.CreatedAtUtc
                })
                .ToListAsync();

            var claimIds =
                pendingClaims
                    .Select(c => c.ClaimId)
                    .Distinct()
                    .ToList();

            var claimSteps = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(a => claimIds.Contains(a.ClaimId))
                .Select(a => new
                {
                    a.ClaimId,
                    a.SequenceOrder,
                    a.ApprovalRole,
                    a.Decision,
                    a.DecidedAtUtc
                })
                .ToListAsync();

            var claimRows = new List<ClaimSignRow>();

            foreach (var pending in pendingClaims)
            {
                var steps =
                    claimSteps
                        .Where(s => s.ClaimId == pending.ClaimId)
                        .ToList();

                var earlier =
                    steps
                        .Where(s => s.SequenceOrder < pending.SequenceOrder)
                        .ToList();

                var blocking =
                    earlier
                        .Where(s => s.Decision != ApprovalDecision.Approved)
                        .OrderBy(s => s.SequenceOrder)
                        .FirstOrDefault();

                var isReady = blocking is null;

                DateTime? since = null;

                if (isReady)
                {
                    var lastDecided =
                        earlier
                            .Where(s => s.DecidedAtUtc.HasValue)
                            .Select(s => s.DecidedAtUtc!.Value)
                            .OrderByDescending(d => d)
                            .Cast<DateTime?>()
                            .FirstOrDefault();

                    since = lastDecided ?? pending.ClaimCreatedAtUtc;
                }

                int? days =
                    since.HasValue
                        ? Math.Max(0, (int)Math.Floor((now - since.Value).TotalDays))
                        : null;

                claimRows.Add(new ClaimSignRow
                {
                    ClaimId = pending.ClaimId,
                    LecturerName = pending.LecturerName,
                    ContractId = pending.ContractId,
                    HoursClaimed = pending.HoursClaimed,
                    IsReady = isReady,
                    WaitingFor = blocking is null
                        ? null
                        : ApproverLabel(blocking.ApprovalRole),
                    WaitingSince = since,
                    DaysWaiting = days
                });
            }

            ClaimsTotalPending = claimRows.Count;

            ClaimsToSignCount =
                claimRows.Count(r => r.IsReady);

            ClaimsWaitingCount =
                claimRows.Count - ClaimsToSignCount;

            ClaimsAwaitingSignature =
                claimRows
                    .OrderByDescending(r => r.IsReady)
                    .ThenBy(r => r.WaitingSince ?? DateTime.MaxValue)
                    .Take(MaxRowsPerTable)
                    .ToList();


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


        // ============================================================
        // LABEL HELPERS
        // ============================================================

        // "InternationalLawEnvironmentAndLandUseLaw"
        //   -> "International Law Environment and Land Use Law"
        private static string FormatDepartment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "—")
                return raw;

            return System.Text.RegularExpressions.Regex
                .Replace(raw, "(?<!^)([A-Z])", " $1")
                .Replace(" And ", " and ");
        }

        private static string SignerLabel(SignerRole role) =>
            role switch
            {
                SignerRole.Lecturer => "Lecturer",
                SignerRole.Dean => "Dean",
                SignerRole.HROfficer => "HR Officer",
                SignerRole.DVCAR => "DVCAR",
                SignerRole.ViceChancellor => "Vice Chancellor",
                SignerRole.ExamOffice => "Exam Office",
                _ => role.ToString()
            };

        private static string ApproverLabel(ApprovalRole role) =>
            role switch
            {
                ApprovalRole.HOD => "HOD",
                ApprovalRole.Dean => "Dean",
                ApprovalRole.DirectorOfQuality => "Director of Quality",
                ApprovalRole.DVCAR => "DVCAR",
                ApprovalRole.HROfficer => "HR Officer",
                ApprovalRole.ViceChancellor => "Vice Chancellor",
                ApprovalRole.Management => "Management",
                _ => role.ToString()
            };
    }
}