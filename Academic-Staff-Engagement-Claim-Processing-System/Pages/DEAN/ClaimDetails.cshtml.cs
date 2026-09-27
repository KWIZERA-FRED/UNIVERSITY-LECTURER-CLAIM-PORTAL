using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN;

[Authorize(Roles = "Dean")]
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

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }


    public async Task<IActionResult> OnGetAsync()
    {
        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;

        if (!ClaimId.HasValue)
        {
            ErrorMessage = "No claim was specified.";
            return Page();
        }

        SelectedClaim = await _signingService.GetClaimForReviewAsync(
            ClaimId.Value,
            ApprovalRole.Dean);

        if (SelectedClaim is null)
        {
            ErrorMessage =
                "That claim could not be found, or is not awaiting Dean approval.";
        }

        return Page();
    }


    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage("/DEAN/Claims");

        var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

        var result = await _signingService.ApproveAsync(
            ClaimId.Value,
            ApprovalRole.Dean,
            actorId,
            actorUsername,
            actorRole,
            ipAddress);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;

            SelectedClaim = await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.Dean);

            return Page();
        }

        TempData["SuccessMessage"] = "Claim approved successfully.";

        return RedirectToPage("/DEAN/Claims");
    }


    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage("/DEAN/Claims");

        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            ErrorMessage = "Please provide a reason for rejecting this claim.";

            SelectedClaim = await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.Dean);

            return Page();
        }

        var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

        var result = await _signingService.RejectAsync(
            ClaimId.Value,
            ApprovalRole.Dean,
            actorId,
            RejectReason,
            actorUsername,
            actorRole,
            ipAddress);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;

            SelectedClaim = await _signingService.GetClaimForReviewAsync(
                ClaimId.Value,
                ApprovalRole.Dean);

            return Page();
        }

        TempData["SuccessMessage"] = "Claim rejected.";

        return RedirectToPage("/DEAN/Claims");
    }


    public async Task<IActionResult> OnGetDownloadMarksAsync(
        int claimId,
        int marksId)
    {
        var claim = await _signingService.GetClaimForReviewAsync(
            claimId,
            ApprovalRole.Dean);

        if (claim is null || claim.MarksSubmissionId != marksId)
            return NotFound();

        var url = await _marksService.GetSignedFileDownloadUrlAsync(marksId);

        return url is null
            ? NotFound()
            : Redirect(url);
    }


    private (int actorId, string actorUsername, string actorRole, string? ipAddress)
        GetActorContext()
    {
        int.TryParse(User.FindFirst("UserId")?.Value, out int actorId);
        string actorUsername = User.Identity?.Name ?? "Unknown";
        string actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        return (actorId, actorUsername, actorRole, ipAddress);
    }
}