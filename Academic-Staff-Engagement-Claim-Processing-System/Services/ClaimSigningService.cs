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

        public ClaimSigningService(
            ApplicationDbContext context,
            AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        public async Task<ClaimReviewDto?> GetClaimForReviewAsync(
            int claimId,
            ApprovalRole role)
        {
            var claim = await _context.Claims
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca.Lecturer)
                .Include(c => c.Contract)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim is null)
                return null;

            var thisStep = await _context.ClaimApprovals
                .Where(ca =>
                    ca.ClaimId == claimId &&
                    ca.ApprovalRole == role)
                .OrderBy(ca => ca.SequenceOrder)
                .FirstOrDefaultAsync();

            if (thisStep is null)
                return null;

            var marks = await _context.MarksSubmissions
                .Where(ms =>
                    ms.CourseAssignmentId == claim.CourseAssignmentId &&
                    ms.Status == MarksSubmissionStatus.Signed)
                .Include(ms => ms.ReviewedByManagement)
                .OrderByDescending(ms => ms.ReviewedAtUtc)
                .FirstOrDefaultAsync();

            var dto = new ClaimReviewDto
            {
                ClaimId = claim.Id,
                LecturerName = claim.CourseAssignment.Lecturer.UserName,
                ContractId = claim.ContractId,
                HoursClaimed = claim.HoursClaimed,
                Description = claim.Description ?? string.Empty,
                ApprovalStepId = thisStep.Id,
                QrCodeToken = claim.QrCodeToken,

                ContractSigned =
                    claim.Contract.Status == ContractStatus.Active,

                ContractSignedAtUtc =
                    claim.Contract.SignedAtUtc,

                MarksSubmissionId =
                    marks?.Id,

                MarksReference =
                    marks?.SubmissionReference,

                MarksFileName =
                    marks?.FileName,

                MarksSigned =
                    marks is not null,

                MarksSignedAtUtc =
                    marks?.ReviewedAtUtc,

                MarksSignedByName =
                    marks?.ReviewedByManagement?.UserName
            };

            if (thisStep.Decision != ApprovalDecision.Pending)
            {
                dto.IsThisRolesTurn = false;

                dto.BlockedReason =
                    $"This step has already been {thisStep.Decision.ToString().ToLower()}.";

                return dto;
            }

            bool earlierStepsComplete =
                !await _context.ClaimApprovals
                    .Where(ca =>
                        ca.ClaimId == claimId &&
                        ca.SequenceOrder < thisStep.SequenceOrder)
                    .AnyAsync(ca =>
                        ca.Decision != ApprovalDecision.Approved);

            dto.IsThisRolesTurn = earlierStepsComplete;

            dto.BlockedReason = earlierStepsComplete
                ? null
                : "An earlier required approval on this claim is still pending.";

            return dto;
        }

        public async Task<ClaimSigningResult> ApproveAsync(
            int claimId,
            ApprovalRole role,
            int adminAccountId,
            string actorUsername,
            string actorRoleLabel,
            string? ipAddress)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    var claim = await _context.Claims
                        .FirstOrDefaultAsync(c => c.Id == claimId);

                    if (claim is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "The claim could not be found.");
                    }

                    var step = await _context.ClaimApprovals
                        .Where(ca =>
                            ca.ClaimId == claimId &&
                            ca.ApprovalRole == role)
                        .OrderBy(ca => ca.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "No approval step found for this role on this claim.");
                    }

                    if (step.Decision != ApprovalDecision.Pending)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "This step has already been actioned.");
                    }

                    bool earlierStepsComplete =
                        !await _context.ClaimApprovals
                            .Where(ca =>
                                ca.ClaimId == claimId &&
                                ca.SequenceOrder < step.SequenceOrder)
                            .AnyAsync(ca =>
                                ca.Decision != ApprovalDecision.Approved);

                    if (!earlierStepsComplete)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "An earlier required approval on this claim is still pending.");
                    }

                    var adminAccount =
                        await _context.AdminAccounts
                            .FirstOrDefaultAsync(a =>
                                a.Id == adminAccountId &&
                                a.IsActive);

                    if (adminAccount is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account could not be found or is inactive.");
                    }

                    if (string.IsNullOrWhiteSpace(
                        adminAccount.SignatureFileHash))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account does not have a signature on file. Please contact an administrator.");
                    }

                    if (!await IsAuthorizedApproverAsync(
                        claimId,
                        adminAccount,
                        role))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account is not authorized to approve this step.");
                    }

                    step.Approve(
                        adminAccountId,
                        adminAccount.SignatureFileHash);

                    await _context.SaveChangesAsync();

                    var nextPendingRole =
                        await _context.ClaimApprovals
                            .Where(ca =>
                                ca.ClaimId == claimId &&
                                ca.Decision == ApprovalDecision.Pending)
                            .OrderBy(ca => ca.SequenceOrder)
                            .Select(ca => (ApprovalRole?)ca.ApprovalRole)
                            .FirstOrDefaultAsync();

                    claim.Status = nextPendingRole switch
                    {
                        ApprovalRole.HOD =>
                            ClaimStatus.PendingHODApproval,

                        ApprovalRole.Dean =>
                            ClaimStatus.PendingDeanApproval,

                        ApprovalRole.DirectorOfQuality =>
                            ClaimStatus.PendingDirectorOfQualityApproval,

                        ApprovalRole.DVCAR =>
                            ClaimStatus.PendingDVCARApproval,

                        null =>
                            ClaimStatus.Approved,

                        _ =>
                            claim.Status
                    };

                    claim.UpdatedAtUtc = DateTime.UtcNow;

                    if (nextPendingRole is null)
                    {
                        claim.CompletedAtUtc = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();

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

                    return new ClaimSigningResult
                    {
                        Succeeded = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.WriteLine(
                        $"CLAIM APPROVAL ERROR: {ex}");

                    return Fail(
                        "The approval could not be saved.");
                }
            });
        }

        public async Task<ClaimSigningResult> RejectAsync(
            int claimId,
            ApprovalRole role,
            int adminAccountId,
            string reason,
            string actorUsername,
            string actorRoleLabel,
            string? ipAddress)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    var step = await _context.ClaimApprovals
                        .Where(ca =>
                            ca.ClaimId == claimId &&
                            ca.ApprovalRole == role)
                        .OrderBy(ca => ca.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null ||
                        step.Decision != ApprovalDecision.Pending)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "This step cannot be rejected.");
                    }

                    var adminAccount =
                        await _context.AdminAccounts
                            .FirstOrDefaultAsync(a =>
                                a.Id == adminAccountId &&
                                a.IsActive);

                    if (adminAccount is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account could not be found or is inactive.");
                    }

                    if (!await IsAuthorizedApproverAsync(
                        claimId,
                        adminAccount,
                        role))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account is not authorized to reject this step.");
                    }

                    step.Reject(
                        adminAccountId,
                        reason);

                    var claim = await _context.Claims
                        .FirstOrDefaultAsync(c =>
                            c.Id == claimId);

                    if (claim is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "The claim could not be found.");
                    }

                    claim.Status = ClaimStatus.Rejected;
                    claim.UpdatedAtUtc = DateTime.UtcNow;
                    claim.CompletedAtUtc = DateTime.UtcNow;

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

                    return new ClaimSigningResult
                    {
                        Succeeded = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.WriteLine(
                        $"CLAIM REJECTION ERROR: {ex}");

                    return Fail(
                        "The rejection could not be saved.");
                }
            });
        }

        private async Task<bool> IsAuthorizedApproverAsync(
            int claimId,
            Data.Models.AdminAccount account,
            ApprovalRole role)
        {
            switch (role)
            {
                case ApprovalRole.HOD:

                    if (account is not Data.Models.Hod hod)
                        return false;

                    var facultyDepartments =
                        FacultyDepartments
                            .GetDepartments(hod.Faculty)
                            .Select(d => d.ToString())
                            .ToHashSet(
                                StringComparer.OrdinalIgnoreCase);

                    if (facultyDepartments.Count == 0)
                        return false;

                    return await _context.Claims
                        .AsNoTracking()
                        .Where(c =>
                            c.Id == claimId &&
                            c.Status == ClaimStatus.PendingHODApproval &&
                            c.CourseAssignment.Course != null)
                        .AnyAsync(c =>
                            facultyDepartments.Contains(
                                c.CourseAssignment.Course.Department));

                case ApprovalRole.Dean:

                    return account is Data.Models.Dean;

                case ApprovalRole.DirectorOfQuality:

                    return account is Data.Models.Management m1 &&
                           m1.Title ==
                           ManagementTitle.DirectorOfQuality;

                case ApprovalRole.DVCAR:

                    return account is Data.Models.Management m2 &&
                           m2.Title ==
                           ManagementTitle.DVCAR;

                default:

                    return false;
            }
        }

        private static ClaimSigningResult Fail(
            string message) =>
            new()
            {
                Succeeded = false,
                ErrorMessage = message
            };
    }
}