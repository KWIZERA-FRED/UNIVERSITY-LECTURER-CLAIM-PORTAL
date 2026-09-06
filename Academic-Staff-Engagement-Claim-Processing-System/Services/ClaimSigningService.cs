using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class ClaimReviewDto
    {
        public int ClaimId { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public int ContractId { get; set; }
        public decimal HoursClaimed { get; set; }
        public string Description { get; set; } = string.Empty;
        public int ApprovalStepId { get; set; }
        public bool IsThisRolesTurn { get; set; }
        public string? BlockedReason { get; set; }

        public string QrCodeToken { get; set; } = string.Empty;

        public bool ContractSigned { get; set; }
        public DateTime? ContractSignedAtUtc { get; set; }

        public int? MarksSubmissionId { get; set; }
        public string? MarksReference { get; set; }
        public string? MarksFileName { get; set; }
        public bool MarksSigned { get; set; }
        public DateTime? MarksSignedAtUtc { get; set; }
        public string? MarksSignedByName { get; set; }
    }

    public class ClaimSigningResult
    {
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ClaimSigningService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;

        public ClaimSigningService(ApplicationDbContext context, AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        // --------------------------------------------------------------
        // LOAD ONE CLAIM FOR REVIEW BY A GIVEN ROLE
        // --------------------------------------------------------------

        public async Task<ClaimReviewDto?> GetClaimForReviewAsync(int claimId, ApprovalRole role)
        {
            var claim = await _context.Claims
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca.Lecturer)
                .Include(c => c.Contract)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim is null)
                return null;

            var thisStep = await _context.ClaimApprovals
                .Where(ca => ca.ClaimId == claimId && ca.ApprovalRole == role)
                .OrderBy(ca => ca.SequenceOrder)
                .FirstOrDefaultAsync();

            if (thisStep is null)
                return null;

            var marks = await _context.MarksSubmissions
                .Where(ms => ms.CourseAssignmentId == claim.CourseAssignmentId
                             && ms.Status == MarksSubmissionStatus.Signed)
                .Include(ms => ms.ReviewedByManagement)
                .OrderByDescending(ms => ms.ReviewedAtUtc)
                .FirstOrDefaultAsync();

            var dto = new ClaimReviewDto
            {
                ClaimId = claim.Id,
                LecturerName = claim.CourseAssignment.Lecturer.UserName,
                ContractId = claim.ContractId,
                HoursClaimed = claim.HoursClaimed,
                Description = claim.Description,
                ApprovalStepId = thisStep.Id,
                QrCodeToken = claim.QrCodeToken,

                ContractSigned = claim.Contract.Status == ContractStatus.Active,
                ContractSignedAtUtc = claim.Contract.SignedAtUtc,

                MarksSubmissionId = marks?.Id,
                MarksReference = marks?.SubmissionReference,
                MarksFileName = marks?.FileName,
                MarksSigned = marks is not null,
                MarksSignedAtUtc = marks?.ReviewedAtUtc,
                MarksSignedByName = marks?.ReviewedByManagement?.UserName
            };

            if (thisStep.Decision != ApprovalDecision.Pending)
            {
                dto.IsThisRolesTurn = false;
                dto.BlockedReason = $"This step has already been {thisStep.Decision.ToString().ToLower()}.";
                return dto;
            }

            bool earlierStepsComplete = !await _context.ClaimApprovals
                .Where(ca => ca.ClaimId == claimId && ca.SequenceOrder < thisStep.SequenceOrder)
                .AnyAsync(ca => ca.Decision != ApprovalDecision.Approved);

            dto.IsThisRolesTurn = earlierStepsComplete;
            dto.BlockedReason = earlierStepsComplete
                ? null
                : "An earlier required approval on this claim is still pending.";

            return dto;
        }

        // --------------------------------------------------------------
        // APPROVE
        // --------------------------------------------------------------

        public async Task<ClaimSigningResult> ApproveAsync(
            int claimId, ApprovalRole role, int adminAccountId, string actorUsername, string actorRoleLabel, string? ipAddress)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var step = await _context.ClaimApprovals
                        .Where(ca => ca.ClaimId == claimId && ca.ApprovalRole == role)
                        .OrderBy(ca => ca.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null)
                    {
                        await transaction.RollbackAsync();
                        return Fail("No approval step found for this role on this claim.");
                    }

                    if (step.Decision != ApprovalDecision.Pending)
                    {
                        await transaction.RollbackAsync();
                        return Fail("This step has already been actioned.");
                    }

                    bool earlierStepsComplete = !await _context.ClaimApprovals
                        .Where(ca => ca.ClaimId == claimId && ca.SequenceOrder < step.SequenceOrder)
                        .AnyAsync(ca => ca.Decision != ApprovalDecision.Approved);

                    if (!earlierStepsComplete)
                    {
                        await transaction.RollbackAsync();
                        return Fail("An earlier required approval on this claim is still pending.");
                    }

                    var adminAccount = await _context.AdminAccounts.FirstOrDefaultAsync(a => a.Id == adminAccountId);

                    if (adminAccount is null || string.IsNullOrWhiteSpace(adminAccount.SignatureFileHash))
                    {
                        await transaction.RollbackAsync();
                        return Fail("Your account does not have a signature on file. Please contact an administrator.");
                    }

                    if (!await IsAuthorizedApproverAsync(claimId, adminAccount, role))
                    {
                        await transaction.RollbackAsync();
                        return Fail("Your account is not authorized to approve this step.");
                    }

                    step.Approve(adminAccountId, adminAccount.SignatureFileHash);
                    await _context.SaveChangesAsync();

                    bool anyStepsRemaining = await _context.ClaimApprovals
                        .Where(ca => ca.ClaimId == claimId)
                        .AnyAsync(ca => ca.Decision == ApprovalDecision.Pending);

                    if (!anyStepsRemaining)
                    {
                        var claim = await _context.Claims.FirstAsync(c => c.Id == claimId);
                        claim.Status = ClaimStatus.Approved;
                        await _context.SaveChangesAsync();
                    }

                    await _auditLogger.LogAsync(
                        AuditAction.ClaimApproved,
                        actorUsername,
                        actorRoleLabel,
                        adminAccountId,
                        "Claim",
                        claimId,
                        $"Approved as {role}",
                        ipAddress);

                    await transaction.CommitAsync();
                    return new ClaimSigningResult { Succeeded = true };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"CLAIM APPROVAL ERROR: {ex}");
                    return Fail("The approval could not be saved.");
                }
            });
        }

        // --------------------------------------------------------------
        // REJECT
        // --------------------------------------------------------------

        public async Task<ClaimSigningResult> RejectAsync(
            int claimId, ApprovalRole role, int adminAccountId, string reason,
            string actorUsername, string actorRoleLabel, string? ipAddress)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var step = await _context.ClaimApprovals
                        .Where(ca => ca.ClaimId == claimId && ca.ApprovalRole == role)
                        .OrderBy(ca => ca.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null || step.Decision != ApprovalDecision.Pending)
                    {
                        await transaction.RollbackAsync();
                        return Fail("This step cannot be rejected.");
                    }

                    var adminAccount = await _context.AdminAccounts.FirstOrDefaultAsync(a => a.Id == adminAccountId);

                    if (adminAccount is null || !await IsAuthorizedApproverAsync(claimId, adminAccount, role))
                    {
                        await transaction.RollbackAsync();
                        return Fail("Your account is not authorized to reject this step.");
                    }

                    step.Reject(adminAccountId, reason);
                    await _context.SaveChangesAsync();

                    var claim = await _context.Claims.FirstAsync(c => c.Id == claimId);
                    claim.Status = ClaimStatus.Rejected;
                    await _context.SaveChangesAsync();

                    await _auditLogger.LogAsync(
                        AuditAction.ClaimRejected,
                        actorUsername,
                        actorRoleLabel,
                        adminAccountId,
                        "Claim",
                        claimId,
                        $"Rejected as {role}: {reason}",
                        ipAddress);

                    await transaction.CommitAsync();
                    return new ClaimSigningResult { Succeeded = true };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"CLAIM REJECTION ERROR: {ex}");
                    return Fail("The rejection could not be saved.");
                }
            });
        }

        // --------------------------------------------------------------
        // AUTHORIZATION
        // --------------------------------------------------------------
        // For HOD specifically, authorization isn't just "any active HOD" —
        // it must be the exact HOD who approved the CourseAssignment behind
        // this claim, since that's the person who actually knows the
        // context needed to check the claim's requirements checklist.
        private async Task<bool> IsAuthorizedApproverAsync(int claimId, Data.Models.AdminAccount account, ApprovalRole role)
        {
            switch (role)
            {
                case ApprovalRole.HOD:
                    if (account is not Data.Models.Hod)
                        return false;

                    int? approvingHodId = await _context.Claims
                        .Where(c => c.Id == claimId)
                        .Select(c => c.CourseAssignment.ApprovedByHodId)
                        .FirstOrDefaultAsync();

                    return approvingHodId.HasValue && approvingHodId.Value == account.Id;

                case ApprovalRole.Dean:
                    return account is Data.Models.Dean;

                case ApprovalRole.DirectorOfQuality:
                    return account is Data.Models.Management m1 && m1.Title == ManagementTitle.DirectorOfQuality;

                case ApprovalRole.DVCAR:
                    return account is Data.Models.Management m2 && m2.Title == ManagementTitle.DVCAR;

                default:
                    return false;
            }
        }

        private static ClaimSigningResult Fail(string message) =>
            new() { Succeeded = false, ErrorMessage = message };
    }
}