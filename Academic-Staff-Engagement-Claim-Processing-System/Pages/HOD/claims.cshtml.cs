using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ClaimsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ClaimsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    // Claims waiting on this HOD right now, oldest first.
    public List<PendingClaimRow> PendingClaims { get; set; } = new();

    // Every submitted claim in the HOD's faculty, whatever its status.
    public List<AllClaimRow> AllClaims { get; set; } = new();

    // False when the HOD account or faculty could not be resolved,
    // so the page shows the error instead of "all caught up".
    public bool HasAccess { get; private set; }

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public class PendingClaimRow
    {
        public int ClaimId { get; set; }

        public string LecturerName { get; set; } =
            string.Empty;

        public int ContractId { get; set; }

        public decimal HoursClaimed { get; set; }

        // When the claim started waiting on the HOD.
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

        public string LecturerName { get; set; } =
            string.Empty;

        public int ContractId { get; set; }

        public decimal HoursClaimed { get; set; }

        public ClaimStatus Status { get; set; }

        // The claim has an HOD approval step, so the details page can open it.
        public bool HasHodStep { get; set; }

        // It is this HOD's turn to approve the claim right now.
        public bool IsAwaitingYou { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        SuccessMessage =
            TempData["SuccessMessage"] as string;

        ErrorMessage =
            TempData["ErrorMessage"] as string;

        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return Challenge();

        var hod = await _context.Hods
            .AsNoTracking()
            .Where(h =>
                h.UserName == username &&
                h.IsActive)
            .Select(h => new
            {
                h.Id,
                h.Faculty
            })
            .FirstOrDefaultAsync();

        if (hod is null)
        {
            ErrorMessage =
                "Your HOD account could not be found or is inactive.";

            return Page();
        }

        var facultyDepartmentValues =
            GetFacultyCourseDepartmentValues(hod.Faculty);

        if (facultyDepartmentValues.Count == 0)
        {
            ErrorMessage =
                "No course departments are configured for your faculty.";

            return Page();
        }

        await LoadClaimsAsync(
            facultyDepartmentValues);

        HasAccess = true;

        return Page();
    }

    private async Task LoadClaimsAsync(
        HashSet<string> facultyDepartmentValues)
    {
        // ------------------------------------------------------------
        // Waiting on the HOD
        // ------------------------------------------------------------

        var pending = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(ca =>
                ca.ApprovalRole == ApprovalRole.HOD &&
                ca.Decision == ApprovalDecision.Pending &&
                ca.Claim.Status == ClaimStatus.PendingHODApproval &&
                ca.Claim.CourseAssignment.Course != null &&
                facultyDepartmentValues.Contains(
                    ca.Claim.CourseAssignment.Course.Department))
            .Select(ca => new
            {
                ClaimId = ca.Claim.Id,

                LecturerName =
                    ca.Claim.CourseAssignment.Lecturer.UserName,

                ContractId = ca.Claim.ContractId,

                HoursClaimed = ca.Claim.HoursClaimed,

                SubmittedAtUtc = ca.Claim.SubmittedAtUtc,

                CreatedAtUtc = ca.Claim.CreatedAtUtc,

                // When the approver before the HOD finished.
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

        // ------------------------------------------------------------
        // Every submitted claim in the HOD's faculty
        // ------------------------------------------------------------
        //
        // Drafts are left out: they are the lecturer's unsubmitted work.
        // Claims from other faculties are left out: they are not the
        // HOD's to review (the details page enforces the same rule).

        AllClaims = await _context.Claims
            .AsNoTracking()
            .Where(c =>
                c.Status != ClaimStatus.Draft &&
                c.CourseAssignment.Course != null &&
                facultyDepartmentValues.Contains(
                    c.CourseAssignment.Course.Department))
            .OrderByDescending(c => c.Id)
            .Select(c => new AllClaimRow
            {
                ClaimId = c.Id,

                LecturerName =
                    c.CourseAssignment.Lecturer.UserName,

                ContractId = c.ContractId,

                HoursClaimed = c.HoursClaimed,

                Status = c.Status,

                HasHodStep =
                    c.Approvals.Any(a =>
                        a.ApprovalRole == ApprovalRole.HOD),

                IsAwaitingYou =
                    c.Status == ClaimStatus.PendingHODApproval &&
                    c.Approvals.Any(a =>
                        a.ApprovalRole == ApprovalRole.HOD &&
                        a.Decision == ApprovalDecision.Pending)
            })
            .ToListAsync();
    }

    private static HashSet<string> GetFacultyCourseDepartmentValues(
        Faculty faculty)
    {
        return FacultyDepartments
            .GetDepartments(faculty)
            .Select(d => d.ToString())
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);
    }
}