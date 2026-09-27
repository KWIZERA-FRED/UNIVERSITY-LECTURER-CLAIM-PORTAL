using System.Security.Claims;
using System.Text;

using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ClaimDetailsModel : PageModel
{
    private readonly ClaimSigningService _signingService;
    private readonly MarksSigningService _marksService;

    public ClaimDetailsModel(
        ClaimSigningService signingService,
        MarksSigningService marksService)
    {
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


    public async Task<IActionResult> OnGetAsync()
    {
        if (!ClaimId.HasValue)
        {
            ErrorMessage = "No claim was specified.";
            return Page();
        }

        SelectedClaim =
            await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.HOD);

        if (SelectedClaim is null)
        {
            ErrorMessage =
                "That claim could not be found, or is not awaiting HOD approval.";
        }

        return Page();
    }


    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage("/HOD/Claims");

        // ============================================================
        // VERIFY ALL COURSE COMPLETION REQUIREMENTS ARE CONFIRMED
        // ============================================================

        var missing = GetMissingChecklistItems();

        if (missing.Count > 0)
        {
            ErrorMessage =
                "Please confirm all course completion requirements before approving. Missing: " +
                string.Join(" · ", missing) + ".";

            SelectedClaim =
                await _signingService.GetClaimForReviewAsync(
                    ClaimId.Value,
                    ApprovalRole.HOD);

            return Page();
        }

        // ============================================================
        // APPROVE
        // ============================================================

        var (actorId, actorUsername, actorRole, ipAddress) =
            GetActorContext();

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

            SelectedClaim =
                await _signingService.GetClaimForReviewAsync(
                    ClaimId.Value,
                    ApprovalRole.HOD);

            return Page();
        }

        TempData["SuccessMessage"] =
            "Claim approved. Course completion checklist confirmed.";

        return RedirectToPage("/HOD/Claims");
    }


    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage("/HOD/Claims");

        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            ErrorMessage =
                "Please provide a reason for rejecting this claim.";

            SelectedClaim =
                await _signingService.GetClaimForReviewAsync(
                    ClaimId.Value,
                    ApprovalRole.HOD);

            return Page();
        }

        var (actorId, actorUsername, actorRole, ipAddress) =
            GetActorContext();

        var result =
            await _signingService.RejectAsync(
                ClaimId.Value,
                ApprovalRole.HOD,
                actorId,
                RejectReason,
                actorUsername,
                actorRole,
                ipAddress);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;

            SelectedClaim =
                await _signingService.GetClaimForReviewAsync(
                    ClaimId.Value,
                    ApprovalRole.HOD);

            return Page();
        }

        TempData["SuccessMessage"] =
            "Claim rejected.";

        return RedirectToPage("/HOD/Claims");
    }


    public async Task<IActionResult> OnGetDownloadMarksAsync(
        int claimId,
        int marksId)
    {
        var claim =
            await _signingService.GetClaimForReviewAsync(
                claimId,
                ApprovalRole.HOD);

        if (claim is null || claim.MarksSubmissionId != marksId)
        {
            return NotFound();
        }

        var url =
            await _marksService.GetSignedFileDownloadUrlAsync(marksId);

        return url is null
            ? NotFound()
            : Redirect(url);
    }


    // ============================================================
    // CHECKLIST HELPERS
    // ============================================================

    private List<string> GetMissingChecklistItems()
    {
        var missing = new List<string>();

        if (!Checklist.NotesUploadedToELearning)
            missing.Add("Lecturer's notes/materials on E-Learning");

        if (!Checklist.IndividualGroupWorkOnELearning)
            missing.Add("Individual/group work on E-Learning");

        if (!Checklist.MarksAvailableInMIS)
            missing.Add("Marks available in MIS");

        if (!Checklist.MarksApprovedByHOD)
            missing.Add("Marks approved by HOD");

        if (!Checklist.HardCopySubmittedToHodAndRegistrar)
            missing.Add("Hard copy mark sheets submitted");

        if (!Checklist.ExamAndMarkingSchemeAvailable)
            missing.Add("Exam and marking scheme in HOD office");

        if (!Checklist.ClassAttendanceListAvailable)
            missing.Add("Students' class attendance list");

        if (!Checklist.ExamScriptsReturned)
            missing.Add("Exam scripts returned");

        return missing;
    }


    public string BuildChecklistSummary()
    {
        var builder = new StringBuilder();

        builder.AppendLine("Course Completion Checklist confirmed by HOD:");
        builder.AppendLine($"1. Notes/materials on E-Learning: {(Checklist.NotesUploadedToELearning ? "Yes" : "No")}");
        builder.AppendLine($"2. Individual/group work on E-Learning: {(Checklist.IndividualGroupWorkOnELearning ? "Yes" : "No")}");
        builder.AppendLine($"3. Marks available in MIS: {(Checklist.MarksAvailableInMIS ? "Yes" : "No")}");
        builder.AppendLine($"4. Marks approved by HOD: {(Checklist.MarksApprovedByHOD ? "Yes" : "No")}");
        builder.AppendLine($"5. Hard copy mark sheets submitted: {(Checklist.HardCopySubmittedToHodAndRegistrar ? "Yes" : "No")}");
        builder.AppendLine($"6. Exam and marking scheme in HOD office: {(Checklist.ExamAndMarkingSchemeAvailable ? "Yes" : "No")}");
        builder.AppendLine($"7. Students' class attendance list: {(Checklist.ClassAttendanceListAvailable ? "Yes" : "No")}");
        builder.AppendLine($"8. Exam scripts returned: {(Checklist.ExamScriptsReturned ? "Yes" : "No")}");

        return builder.ToString();
    }


    private (int actorId, string actorUsername, string actorRole, string? ipAddress)
        GetActorContext()
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

        return (actorId, actorUsername, actorRole, ipAddress);
    }


    // ============================================================
    // VIEW MODEL — Course Completion Checklist
    // ============================================================

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