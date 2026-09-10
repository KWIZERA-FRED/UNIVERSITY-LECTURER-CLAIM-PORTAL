using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ClaimsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ClaimSigningService _signingService;
    private readonly MarksSigningService _marksService;

    public ClaimsModel(
        ApplicationDbContext context,
        ClaimSigningService signingService,
        MarksSigningService marksService)
    {
        _context = context;
        _signingService = signingService;
        _marksService = marksService;
    }

    public List<PendingClaimRow> PendingClaims { get; set; } = new();

    public ClaimReviewDto? SelectedClaim { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ClaimId { get; set; }

    [BindProperty]
    public string? RejectReason { get; set; }

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }


    public class PendingClaimRow
    {
        public int ClaimId { get; set; }

        public string LecturerName { get; set; } = string.Empty;

        public int ContractId { get; set; }

        public decimal HoursClaimed { get; set; }
    }


    public async Task OnGetAsync()
    {
        await LoadPendingListAsync();

        if (ClaimId.HasValue)
        {
            SelectedClaim =
                await _signingService.GetClaimForReviewAsync(
                    ClaimId.Value,
                    ApprovalRole.HOD);

            if (SelectedClaim is null)
            {
                ErrorMessage =
                    "That claim could not be found, or is not awaiting HOD approval.";
            }
        }
    }


    public async Task<IActionResult> OnPostApproveAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage();

        var (
            actorId,
            actorUsername,
            actorRole,
            ipAddress
        ) = GetActorContext();

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
        }
        else
        {
            SuccessMessage =
                "Claim approved successfully.";
        }

        await LoadPendingListAsync();

        return Page();
    }


    public async Task<IActionResult> OnPostRejectAsync()
    {
        if (!ClaimId.HasValue)
            return RedirectToPage();

        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            ErrorMessage =
                "Please provide a reason for rejecting this claim.";

            await LoadPendingListAsync();

            SelectedClaim =
                await _signingService.GetClaimForReviewAsync(
                    ClaimId.Value,
                    ApprovalRole.HOD);

            return Page();
        }

        var (
            actorId,
            actorUsername,
            actorRole,
            ipAddress
        ) = GetActorContext();

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
        }
        else
        {
            SuccessMessage =
                "Claim rejected.";
        }

        await LoadPendingListAsync();

        return Page();
    }


    public async Task<IActionResult> OnGetDownloadMarksAsync(
        int claimId,
        int marksId)
    {
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


    private async Task LoadPendingListAsync()
    {
        PendingClaims =
            await _context.ClaimApprovals
                .Where(ca =>
                    ca.ApprovalRole == ApprovalRole.HOD &&
                    ca.Decision == ApprovalDecision.Pending)
                .Include(ca => ca.Claim)
                    .ThenInclude(c => c.CourseAssignment)
                        .ThenInclude(
                            courseAssignment =>
                                courseAssignment.Lecturer)
                .Select(ca => new PendingClaimRow
                {
                    ClaimId = ca.Claim.Id,

                    LecturerName =
                        ca.Claim.CourseAssignment
                            .Lecturer.UserName,

                    ContractId =
                        ca.Claim.ContractId,

                    HoursClaimed =
                        ca.Claim.HoursClaimed
                })
                .ToListAsync();
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
            HttpContext.Connection.RemoteIpAddress?
                .ToString();

        return (
            actorId,
            actorUsername,
            actorRole,
            ipAddress
        );
    }
}