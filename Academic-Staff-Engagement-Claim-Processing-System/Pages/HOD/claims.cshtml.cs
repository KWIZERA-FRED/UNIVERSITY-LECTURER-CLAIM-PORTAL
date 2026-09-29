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

    public List<PendingClaimRow> PendingClaims { get; set; } = new();

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public class PendingClaimRow
    {
        public int ClaimId { get; set; }

        public string LecturerName { get; set; } =
            string.Empty;

        public int ContractId { get; set; }

        public decimal HoursClaimed { get; set; }
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

        await LoadPendingListAsync(
            facultyDepartmentValues);

        return Page();
    }

    private async Task LoadPendingListAsync(
        HashSet<string> facultyDepartmentValues)
    {
        PendingClaims = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(ca =>
                ca.ApprovalRole == ApprovalRole.HOD &&
                ca.Decision == ApprovalDecision.Pending &&
                ca.Claim.Status == ClaimStatus.PendingHODApproval &&
                ca.Claim.CourseAssignment.Course != null &&
                facultyDepartmentValues.Contains(
                    ca.Claim.CourseAssignment.Course.Department))
            .Select(ca => new PendingClaimRow
            {
                ClaimId =
                    ca.Claim.Id,

                LecturerName =
                    ca.Claim.CourseAssignment.Lecturer.UserName,

                ContractId =
                    ca.Claim.ContractId,

                HoursClaimed =
                    ca.Claim.HoursClaimed
            })
            .OrderByDescending(c => c.ClaimId)
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