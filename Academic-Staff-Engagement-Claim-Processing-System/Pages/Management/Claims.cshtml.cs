using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Management
{
    [Authorize]
    public class ClaimsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ClaimSigningService _signingService;

        public ClaimsModel(
            ApplicationDbContext context,
            ClaimSigningService signingService)
        {
            _context = context;
            _signingService = signingService;
        }

        public List<PendingClaimRow> PendingClaims { get; set; } = new();

        public ClaimReviewDto? SelectedClaim { get; set; }

        public string RoleLabel { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ClaimId { get; set; }

        [BindProperty]
        public string? RejectReason { get; set; }

        public class PendingClaimRow
        {
            public int ClaimId { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public int ContractId { get; set; }

            public decimal HoursClaimed { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var role = await ResolveApprovalRoleAsync();

            if (role is null)
                return RedirectToPage("/ManagementDashboard");

            await LoadPendingListAsync(role.Value);

            if (ClaimId.HasValue)
            {
                SelectedClaim =
                    await _signingService.GetClaimForReviewAsync(
                        ClaimId.Value,
                        role.Value);

                if (SelectedClaim is null)
                {
                    ErrorMessage =
                        $"That claim could not be found, or is not awaiting a {RoleLabel} approval.";
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync()
        {
            var role = await ResolveApprovalRoleAsync();

            if (role is null || !ClaimId.HasValue)
                return RedirectToPage("/ManagementDashboard");

            var (
                actorId,
                actorUsername,
                actorRole,
                ipAddress
            ) = GetActorContext();

            if (actorId <= 0)
            {
                ErrorMessage = "Your account could not be identified.";

                await LoadPendingListAsync(role.Value);

                return Page();
            }

            var result = await _signingService.ApproveAsync(
                ClaimId.Value,
                role.Value,
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
                        role.Value);
            }
            else
            {
                SuccessMessage = "Claim approved successfully.";
            }

            await LoadPendingListAsync(role.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var role = await ResolveApprovalRoleAsync();

            if (role is null || !ClaimId.HasValue)
                return RedirectToPage("/ManagementDashboard");

            if (string.IsNullOrWhiteSpace(RejectReason))
            {
                ErrorMessage =
                    "Please provide a reason for rejecting this claim.";

                await LoadPendingListAsync(role.Value);

                SelectedClaim =
                    await _signingService.GetClaimForReviewAsync(
                        ClaimId.Value,
                        role.Value);

                return Page();
            }

            var (
                actorId,
                actorUsername,
                actorRole,
                ipAddress
            ) = GetActorContext();

            if (actorId <= 0)
            {
                ErrorMessage = "Your account could not be identified.";

                await LoadPendingListAsync(role.Value);

                SelectedClaim =
                    await _signingService.GetClaimForReviewAsync(
                        ClaimId.Value,
                        role.Value);

                return Page();
            }

            var result = await _signingService.RejectAsync(
                ClaimId.Value,
                role.Value,
                actorId,
                RejectReason.Trim(),
                actorUsername,
                actorRole,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;

                SelectedClaim =
                    await _signingService.GetClaimForReviewAsync(
                        ClaimId.Value,
                        role.Value);
            }
            else
            {
                SuccessMessage = "Claim rejected.";
            }

            await LoadPendingListAsync(role.Value);

            return Page();
        }

        private async Task<ApprovalRole?> ResolveApprovalRoleAsync()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int accountId);

            if (accountId <= 0)
                return null;

            var account = await _context.AdminAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == accountId &&
                    a.IsActive);

            if (account is null)
                return null;

            switch (account)
            {
                case Hod:
                    RoleLabel = "HOD";
                    return ApprovalRole.HOD;

                case Dean:
                    RoleLabel = "Dean";
                    return ApprovalRole.Dean;

                case Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Management management:
                    return ResolveManagementApprovalRole(management);

                default:
                    return null;
            }
        }

        private ApprovalRole? ResolveManagementApprovalRole(
            Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Management management)
        {
            return management.Title switch
            {
                ManagementTitle.DirectorOfQuality =>
                    SetRoleLabelAndReturn(
                        "Director of Quality",
                        ApprovalRole.DirectorOfQuality),

                ManagementTitle.DVCAR =>
                    SetRoleLabelAndReturn(
                        "DVCAR",
                        ApprovalRole.DVCAR),

                ManagementTitle.ExamOffice =>
                    null,

                _ => null
            };
        }

        private ApprovalRole SetRoleLabelAndReturn(
            string label,
            ApprovalRole role)
        {
            RoleLabel = label;
            return role;
        }

        private async Task LoadPendingListAsync(ApprovalRole role)
        {
            PendingClaims = await _context.ClaimApprovals
                .AsNoTracking()
                .Where(ca =>
                    ca.ApprovalRole == role &&
                    ca.Decision == ApprovalDecision.Pending)
                .Select(ca => new PendingClaimRow
                {
                    ClaimId = ca.Claim.Id,

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
                User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            string? ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            return (
                actorId,
                actorUsername,
                actorRole,
                ipAddress);
        }
    }
}