using System.Security.Claims;
using System.Text;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ClaimDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ClaimSigningService _signingService;
    private readonly MarksSigningService _marksService;

    public ClaimDetailsModel(
        ApplicationDbContext context,
        ClaimSigningService signingService,
        MarksSigningService marksService)
    {
        _context = context;
        _signingService = signingService;
        _marksService = marksService;
    }

    public ClaimReviewDto? SelectedClaim { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ClaimId { get; set; }

    [BindProperty]
    public string? RejectReason { get; set; }

    [BindProperty]
    public CompletionChecklist Checklist { get; set; } = new();

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    // True when the claim is in this HOD's faculty AND currently at the HOD approval step.
    // When false, the page renders in read-only mode with no decision forms.
    public bool IsActionable { get; private set; }

    // Which approval role currently holds the claim. Null when the claim is not found
    // or the faculty check failed.
    public ApprovalRole? CurrentApprover { get; private set; }

    // Which approval role currently holds the claim, in display form.
    public string CurrentApproverLabel =>
        CurrentApprover switch
        {
            ApprovalRole.HOD => "HOD",
            ApprovalRole.Dean => "Dean",
            ApprovalRole.DirectorOfQuality => "Director of Quality",
            ApprovalRole.DVCAR => "DVCAR",
            ApprovalRole.HROfficer => "HR Officer",
            ApprovalRole.ViceChancellor => "Vice Chancellor",
            ApprovalRole.Management => "Management",
            null => "—",
            _ => CurrentApprover.Value.ToString()
        };

    // Whether the HOD has already approved this claim (and it moved downstream).
    public bool HodAlreadyApproved { get; private set; }

    // When the HOD approved it, if applicable.
    public DateTime? HodApprovedAtUtc { get; private set; }


    // ============================================================
    // GET
    // ============================================================

    public async Task<IActionResult> OnGetAsync()
    {
        if (!ClaimId.HasValue)
        {
            ErrorMessage = "No claim was specified.";
            return Page();
        }

        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorMessage = "Your account could not be identified.";
            return Page();
        }

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
            ErrorMessage = "Your account could not be identified.";
            return Page();
        }

        var facultyDepartments =
            GetFacultyCourseDepartmentValues(hod.Faculty);

        if (facultyDepartments.Count == 0)
        {
            ErrorMessage = "Your faculty has no configured departments.";
            return Page();
        }

        // --- Faculty check: is this claim in the HOD's scope at all? ---

        var claimContext = await _context.Claims
            .AsNoTracking()
            .Where(c => c.Id == ClaimId.Value)
            .Select(c => new
            {
                c.Id,
                c.Status,
                Department = c.CourseAssignment.Course.Department
            })
            .FirstOrDefaultAsync();

        if (claimContext is null)
        {
            ErrorMessage = "That claim could not be found.";
            return Page();
        }

        if (!facultyDepartments.Contains(claimContext.Department))
        {
            ErrorMessage =
                "You are not authorized to review this claim. " +
                "The claim does not belong to your faculty.";

            return Page();
        }

        // --- In-faculty. Load the full review DTO. ---

        SelectedClaim =
            await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.HOD);

        if (SelectedClaim is null)
        {
            ErrorMessage = "That claim could not be loaded.";
            return Page();
        }

        // --- Determine whether the HOD can act right now. ---

        IsActionable = SelectedClaim.IsThisRolesTurn;

        // --- Work out who currently holds the claim. ---

        var pendingApproval = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(a =>
                a.ClaimId == ClaimId.Value &&
                a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.SequenceOrder)
            .Select(a => (ApprovalRole?)a.ApprovalRole)
            .FirstOrDefaultAsync();

        CurrentApprover = pendingApproval;

        // --- Has the HOD already had their turn? ---

        var hodApproval = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(a =>
                a.ClaimId == ClaimId.Value &&
                a.ApprovalRole == ApprovalRole.HOD &&
                a.Decision != ApprovalDecision.Pending)
            .OrderByDescending(a => a.DecidedAtUtc)
            .Select(a => new
            {
                a.Decision,
                a.DecidedAtUtc
            })
            .FirstOrDefaultAsync();

        if (hodApproval is not null &&
            hodApproval.Decision == ApprovalDecision.Approved)
        {
            HodAlreadyApproved = true;
            HodApprovedAtUtc = hodApproval.DecidedAtUtc;
        }

        return Page();
    }


    // ============================================================
    // APPROVE
    // ============================================================

    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage("/HOD/Claims");

        if (!await IsCurrentHodAuthorizedForClaimAsync(ClaimId.Value))
        {
            ErrorMessage =
                "You are not authorized to approve this claim. " +
                "The claim does not belong to your faculty.";

            await LoadReadOnlyContextAsync(ClaimId.Value);

            return Page();
        }

        SelectedClaim =
            await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.HOD);

        if (SelectedClaim is null)
        {
            ErrorMessage = "That claim could not be loaded.";
            return Page();
        }

        IsActionable = SelectedClaim.IsThisRolesTurn;

        if (!IsActionable)
        {
            ErrorMessage =
                $"This claim is currently with {CurrentApproverLabel}. " +
                "No action is required from you.";

            await LoadReadOnlyContextAsync(ClaimId.Value);

            return Page();
        }

        var missing = GetMissingChecklistItems();

        if (missing.Count > 0)
        {
            ErrorMessage =
                "Please confirm all course completion requirements before approving. Missing: " +
                string.Join(" · ", missing) + ".";

            return Page();
        }

        var (actorId, actorUsername, actorRole, ipAddress) =
            GetActorContext();

        if (actorId <= 0)
        {
            ErrorMessage = "Your account could not be identified.";
            return Page();
        }

        var result =
            await _signingService.ApproveAsync(
                ClaimId.Value,
                ApprovalRole.HOD,
                actorId,
                actorUsername,
                actorRole,
                ipAddress);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;

            await LoadReadOnlyContextAsync(ClaimId.Value);

            return Page();
        }

        TempData["SuccessMessage"] =
            "Claim approved. Course completion checklist confirmed.";

        return RedirectToPage("/HOD/Claims");
    }


    // ============================================================
    // REJECT
    // ============================================================

    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage("/HOD/Claims");

        if (!await IsCurrentHodAuthorizedForClaimAsync(ClaimId.Value))
        {
            ErrorMessage =
                "You are not authorized to reject this claim. " +
                "The claim does not belong to your faculty.";

            await LoadReadOnlyContextAsync(ClaimId.Value);

            return Page();
        }

        SelectedClaim =
            await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.HOD);

        if (SelectedClaim is null)
        {
            ErrorMessage = "That claim could not be loaded.";
            return Page();
        }

        IsActionable = SelectedClaim.IsThisRolesTurn;

        if (!IsActionable)
        {
            ErrorMessage =
                $"This claim is currently with {CurrentApproverLabel}. " +
                "No action is required from you.";

            await LoadReadOnlyContextAsync(ClaimId.Value);

            return Page();
        }

        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            ErrorMessage =
                "Please provide a reason for rejecting this claim.";

            return Page();
        }

        var (actorId, actorUsername, actorRole, ipAddress) =
            GetActorContext();

        if (actorId <= 0)
        {
            ErrorMessage = "Your account could not be identified.";
            return Page();
        }

        var result =
            await _signingService.RejectAsync(
                ClaimId.Value,
                ApprovalRole.HOD,
                actorId,
                RejectReason.Trim(),
                actorUsername,
                actorRole,
                ipAddress);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;

            await LoadReadOnlyContextAsync(ClaimId.Value);

            return Page();
        }

        TempData["SuccessMessage"] = "Claim rejected.";

        return RedirectToPage("/HOD/Claims");
    }


    // ============================================================
    // DOWNLOAD MARKS
    // ============================================================

    public async Task<IActionResult> OnGetDownloadMarksAsync(
        int claimId,
        int marksId)
    {
        if (!await IsCurrentHodAuthorizedForClaimAsync(claimId))
            return Forbid();

        var claim =
            await _signingService.GetClaimForReviewAsync(
                claimId,
                ApprovalRole.HOD);

        if (claim is null ||
            claim.MarksSubmissionId != marksId)
        {
            return NotFound();
        }

        var url =
            await _marksService.GetSignedFileDownloadUrlAsync(
                marksId);

        return url is null
            ? NotFound()
            : Redirect(url);
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private async Task<bool> IsCurrentHodAuthorizedForClaimAsync(
        int claimId)
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return false;

        var hod = await _context.Hods
            .AsNoTracking()
            .Where(h =>
                h.UserName == username &&
                h.IsActive)
            .Select(h => new
            {
                h.Faculty
            })
            .FirstOrDefaultAsync();

        if (hod is null)
            return false;

        var facultyDepartmentValues =
            GetFacultyCourseDepartmentValues(hod.Faculty);

        if (facultyDepartmentValues.Count == 0)
            return false;

        return await _context.Claims
            .AsNoTracking()
            .Where(c =>
                c.Id == claimId &&
                c.CourseAssignment.Course != null)
            .AnyAsync(c =>
                facultyDepartmentValues.Contains(
                    c.CourseAssignment.Course.Department));
    }

    // Re-loads the read-only state fields (CurrentApprover, HodAlreadyApproved,
    // HodApprovedAtUtc) after a POST path decides the page is not actionable.
    private async Task LoadReadOnlyContextAsync(int claimId)
    {
        var pendingApproval = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(a =>
                a.ClaimId == claimId &&
                a.Decision == ApprovalDecision.Pending)
            .OrderBy(a => a.SequenceOrder)
            .Select(a => (ApprovalRole?)a.ApprovalRole)
            .FirstOrDefaultAsync();

        CurrentApprover = pendingApproval;

        var hodApproval = await _context.ClaimApprovals
            .AsNoTracking()
            .Where(a =>
                a.ClaimId == claimId &&
                a.ApprovalRole == ApprovalRole.HOD &&
                a.Decision != ApprovalDecision.Pending)
            .OrderByDescending(a => a.DecidedAtUtc)
            .Select(a => new
            {
                a.Decision,
                a.DecidedAtUtc
            })
            .FirstOrDefaultAsync();

        if (hodApproval is not null &&
            hodApproval.Decision == ApprovalDecision.Approved)
        {
            HodAlreadyApproved = true;
            HodApprovedAtUtc = hodApproval.DecidedAtUtc;
        }
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

    private List<string> GetMissingChecklistItems()
    {
        var missing = new List<string>();

        if (!Checklist.NotesUploadedToELearning)
            missing.Add(
                "Lecturer's notes/materials on E-Learning");

        if (!Checklist.IndividualGroupWorkOnELearning)
            missing.Add(
                "Individual/group work on E-Learning");

        if (!Checklist.MarksAvailableInMIS)
            missing.Add(
                "Marks available in MIS");

        if (!Checklist.MarksApprovedByHOD)
            missing.Add(
                "Marks approved by HOD");

        if (!Checklist.HardCopySubmittedToHodAndRegistrar)
            missing.Add(
                "Hard copy mark sheets submitted");

        if (!Checklist.ExamAndMarkingSchemeAvailable)
            missing.Add(
                "Exam and marking scheme in HOD office");

        if (!Checklist.ClassAttendanceListAvailable)
            missing.Add(
                "Students' class attendance list");

        if (!Checklist.ExamScriptsReturned)
            missing.Add(
                "Exam scripts returned");

        return missing;
    }

    public string BuildChecklistSummary()
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "Course Completion Checklist confirmed by HOD:");

        builder.AppendLine(
            $"1. Notes/materials on E-Learning: " +
            $"{(Checklist.NotesUploadedToELearning ? "Yes" : "No")}");

        builder.AppendLine(
            $"2. Individual/group work on E-Learning: " +
            $"{(Checklist.IndividualGroupWorkOnELearning ? "Yes" : "No")}");

        builder.AppendLine(
            $"3. Marks available in MIS: " +
            $"{(Checklist.MarksAvailableInMIS ? "Yes" : "No")}");

        builder.AppendLine(
            $"4. Marks approved by HOD: " +
            $"{(Checklist.MarksApprovedByHOD ? "Yes" : "No")}");

        builder.AppendLine(
            $"5. Hard copy mark sheets submitted: " +
            $"{(Checklist.HardCopySubmittedToHodAndRegistrar ? "Yes" : "No")}");

        builder.AppendLine(
            $"6. Exam and marking scheme in HOD office: " +
            $"{(Checklist.ExamAndMarkingSchemeAvailable ? "Yes" : "No")}");

        builder.AppendLine(
            $"7. Students' class attendance list: " +
            $"{(Checklist.ClassAttendanceListAvailable ? "Yes" : "No")}");

        builder.AppendLine(
            $"8. Exam scripts returned: " +
            $"{(Checklist.ExamScriptsReturned ? "Yes" : "No")}");

        return builder.ToString();
    }

    private (
        int actorId,
        string actorUsername,
        string actorRole,
        string? ipAddress
    ) GetActorContext()
    {
        int.TryParse(
            User.FindFirst("UserId")?.Value,
            out int actorId);

        string actorUsername =
            User.Identity?.Name ?? "Unknown";

        string actorRole =
            User.FindFirst(ClaimTypes.Role)?.Value
            ?? "Unknown";

        string? ipAddress =
            HttpContext.Connection.RemoteIpAddress?.ToString();

        return (
            actorId,
            actorUsername,
            actorRole,
            ipAddress);
    }

    public sealed class CompletionChecklist
    {
        public bool NotesUploadedToELearning { get; set; }

        public bool IndividualGroupWorkOnELearning { get; set; }

        public bool MarksAvailableInMIS { get; set; }

        public bool MarksApprovedByHOD { get; set; }

        public bool HardCopySubmittedToHodAndRegistrar { get; set; }

        public bool ExamAndMarkingSchemeAvailable { get; set; }

        public bool ClassAttendanceListAvailable { get; set; }

        public bool ExamScriptsReturned { get; set; }

        public bool AllConfirmed =>
            NotesUploadedToELearning &&
            IndividualGroupWorkOnELearning &&
            MarksAvailableInMIS &&
            MarksApprovedByHOD &&
            HardCopySubmittedToHodAndRegistrar &&
            ExamAndMarkingSchemeAvailable &&
            ClassAttendanceListAvailable &&
            ExamScriptsReturned;
    }
}