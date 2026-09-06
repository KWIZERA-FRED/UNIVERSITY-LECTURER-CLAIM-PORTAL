using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class ContractReviewDto
    {
        public int ContractId { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public decimal AllocatedHours { get; set; }
        public string ContractContent { get; set; } = string.Empty;
        public int SignatureStepId { get; set; }
        public bool IsThisRolesTurn { get; set; }
        public string? BlockedReason { get; set; }
    }

    public class ContractSigningResult
    {
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ContractSigningService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;

        public ContractSigningService(
            ApplicationDbContext context,
            AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        // ==============================================================
        // LOAD ONE CONTRACT FOR REVIEW BY A GIVEN ROLE
        // ==============================================================

        public async Task<ContractReviewDto?> GetContractForReviewAsync(
            int contractId,
            SignerRole role)
        {
            var contract = await _context.Contracts
                .Include(c => c.Lecturer)
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca!.Course)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract is null)
                return null;

            var thisStep = await _context.ContractSignatures
                .Where(cs =>
                    cs.ContractId == contractId &&
                    cs.SignerRole == role)
                .OrderBy(cs => cs.SequenceOrder)
                .FirstOrDefaultAsync();

            if (thisStep is null)
                return null;

            var dto = new ContractReviewDto
            {
                ContractId = contract.Id,
                LecturerName = contract.Lecturer.UserName,
                CourseTitle = contract.CourseAssignment?.Course.Title ?? "—",
                Department = contract.CourseAssignment?.Course.Department ?? "—",
                AllocatedHours = contract.CourseAssignment?.AllocatedHours ?? 0,
                ContractContent = contract.Content,
                SignatureStepId = thisStep.Id
            };

            // ----------------------------------------------------------
            // This role has already acted on the contract
            // ----------------------------------------------------------

            if (thisStep.Decision != SignatureDecision.Pending)
            {
                dto.IsThisRolesTurn = false;
                dto.BlockedReason =
                    $"This step has already been {thisStep.Decision.ToString().ToLower()}.";

                return dto;
            }

            // ----------------------------------------------------------
            // Make sure every earlier signature step is complete
            // ----------------------------------------------------------

            bool earlierStepsComplete =
                !await _context.ContractSignatures
                    .Where(cs =>
                        cs.ContractId == contractId &&
                        cs.SequenceOrder < thisStep.SequenceOrder)
                    .AnyAsync(cs =>
                        cs.Decision != SignatureDecision.Signed);

            dto.IsThisRolesTurn = earlierStepsComplete;

            dto.BlockedReason = earlierStepsComplete
                ? null
                : "An earlier required signature on this contract is still pending.";

            return dto;
        }

        // ==============================================================
        // SIGN AS LECTURER
        //
        // Required contract sequence:
        //
        // Lecturer
        //     ↓
        // Dean
        //     ↓
        // HR Officer
        //     ↓
        // DVCAR
        //     ↓
        // Vice Chancellor
        //     ↓
        // ACTIVE
        // ==============================================================

        public async Task<ContractSigningResult> SignAsLecturerAsync(
            int contractId,
            int lecturerId,
            string actorUsername,
            string? ipAddress)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    // --------------------------------------------------
                    // Verify lecturer
                    // --------------------------------------------------

                    var lecturer = await _context.Lecturers
                        .FirstOrDefaultAsync(l =>
                            l.Id == lecturerId &&
                            l.IsActive);

                    if (lecturer is null)
                    {
                        await transaction.RollbackAsync();
                        return Fail("The lecturer account is not active.");
                    }

                    // --------------------------------------------------
                    // Verify contract belongs to this lecturer
                    // --------------------------------------------------

                    var contract = await _context.Contracts
                        .FirstOrDefaultAsync(c =>
                            c.Id == contractId &&
                            c.LecturerId == lecturerId);

                    if (contract is null)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            "This contract does not belong to the current lecturer.");
                    }

                    // --------------------------------------------------
                    // Contract must still be awaiting signatures
                    // --------------------------------------------------

                    if (contract.Status != ContractStatus.PendingSignature)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            "This contract is no longer available for signing.");
                    }

                    // --------------------------------------------------
                    // Lecturer must have a captured signature
                    // --------------------------------------------------

                    if (string.IsNullOrWhiteSpace(lecturer.SignatureFileHash))
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            "A verified signature must be captured before you can sign a contract.");
                    }

                    // --------------------------------------------------
                    // Find lecturer signature step
                    // --------------------------------------------------

                    var step = await _context.ContractSignatures
                        .Where(cs =>
                            cs.ContractId == contractId &&
                            cs.SignerRole == SignerRole.Lecturer)
                        .OrderBy(cs => cs.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            "The lecturer signature step was not found for this contract.");
                    }

                    // --------------------------------------------------
                    // Prevent duplicate signing
                    // --------------------------------------------------

                    if (step.Decision != SignatureDecision.Pending)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            "The lecturer has already actioned this contract.");
                    }

                    // --------------------------------------------------
                    // Lecturer must be the FIRST signature step
                    // --------------------------------------------------

                    bool earlierStepsExist =
                        await _context.ContractSignatures
                            .AnyAsync(cs =>
                                cs.ContractId == contractId &&
                                cs.SequenceOrder < step.SequenceOrder);

                    if (earlierStepsExist)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            "The lecturer signature must be the first step in the contract signing process.");
                    }

                    // --------------------------------------------------
                    // Sign as lecturer
                    // --------------------------------------------------

                    step.SignAsLecturer(
                        lecturerId,
                        lecturer.SignatureFileHash);

                    // IMPORTANT:
                    //
                    // Do NOT call contract.StampSignature() here.
                    //
                    // StampSignature() changes the contract status to
                    // Active, which must only happen after the Vice
                    // Chancellor completes the final signature.
                    //
                    // The lecturer signature is stored in the
                    // ContractSignature record instead.

                    await _context.SaveChangesAsync();

                    // --------------------------------------------------
                    // Audit
                    // --------------------------------------------------

                    await _auditLogger.LogAsync(
                        AuditAction.ContractSigned,
                        actorUsername,
                        "Lecturer",
                        lecturerId,
                        "Contract",
                        contractId,
                        "Lecturer signed the contract.",
                        ipAddress);

                    await transaction.CommitAsync();

                    return new ContractSigningResult
                    {
                        Succeeded = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.WriteLine(
                        $"CONTRACT LECTURER SIGNING ERROR: {ex}");

                    return Fail(
                        "The contract signature could not be saved.");
                }
            });
        }

        // ==============================================================
        // SIGN AS ADMIN
        //
        // Allowed administrative signature roles:
        //
        // Dean
        // HR Officer
        // DVCAR
        // Vice Chancellor
        // ==============================================================

        public async Task<ContractSigningResult> SignAsync(
            int contractId,
            SignerRole role,
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
                    // --------------------------------------------------
                    // Lecturer cannot use the admin signing method
                    // --------------------------------------------------

                    if (role == SignerRole.Lecturer)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Lecturers must use the lecturer signing process.");
                    }

                    // --------------------------------------------------
                    // Find contract
                    // --------------------------------------------------

                    var contract = await _context.Contracts
                        .FirstOrDefaultAsync(c => c.Id == contractId);

                    if (contract is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "The contract could not be found.");
                    }

                    // --------------------------------------------------
                    // Contract must still be in signing process
                    // --------------------------------------------------

                    if (contract.Status != ContractStatus.PendingSignature)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "This contract is no longer available for signing.");
                    }

                    // --------------------------------------------------
                    // Find signature step for this role
                    // --------------------------------------------------

                    var step = await _context.ContractSignatures
                        .Where(cs =>
                            cs.ContractId == contractId &&
                            cs.SignerRole == role)
                        .OrderBy(cs => cs.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "No signature step was found for this role on this contract.");
                    }

                    // --------------------------------------------------
                    // Prevent duplicate signing
                    // --------------------------------------------------

                    if (step.Decision != SignatureDecision.Pending)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "This signature step has already been actioned.");
                    }

                    // --------------------------------------------------
                    // STRICT SEQUENCE CHECK
                    //
                    // Every previous signature must be Signed.
                    // --------------------------------------------------

                    bool earlierStepsComplete =
                        !await _context.ContractSignatures
                            .Where(cs =>
                                cs.ContractId == contractId &&
                                cs.SequenceOrder < step.SequenceOrder)
                            .AnyAsync(cs =>
                                cs.Decision != SignatureDecision.Signed);

                    if (!earlierStepsComplete)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "An earlier required signature on this contract is still pending.");
                    }

                    // --------------------------------------------------
                    // Find the actual admin account
                    // --------------------------------------------------

                    var adminAccount =
                        await _context.AdminAccounts
                            .FirstOrDefaultAsync(a =>
                                a.Id == adminAccountId);

                    if (adminAccount is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "The administrator account could not be found.");
                    }

                    // --------------------------------------------------
                    // STRICT ROLE AUTHORIZATION
                    //
                    // The account's actual type/title must match the
                    // requested signature role.
                    // --------------------------------------------------

                    if (!IsAuthorizedSigner(adminAccount, role))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account is not authorized to sign this contract step.");
                    }

                    // --------------------------------------------------
                    // Signer must have a verified signature
                    // --------------------------------------------------

                    if (string.IsNullOrWhiteSpace(
                        adminAccount.SignatureFileHash))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "A verified signature must be available before signing.");
                    }

                    // --------------------------------------------------
                    // Sign the step
                    // --------------------------------------------------

                    step.SignAsAdmin(
                        adminAccountId,
                        adminAccount.SignatureFileHash);

                    await _context.SaveChangesAsync();

                    // --------------------------------------------------
                    // Determine whether this was the FINAL step
                    // --------------------------------------------------

                    bool anyStepsRemaining =
                        await _context.ContractSignatures
                            .AnyAsync(cs =>
                                cs.ContractId == contractId &&
                                cs.Decision == SignatureDecision.Pending);

                    // --------------------------------------------------
                    // Contract becomes ACTIVE only after all five
                    // required signatures have been completed.
                    //
                    // Lecturer → Dean → HR → DVCAR → VC
                    // --------------------------------------------------

                    if (!anyStepsRemaining)
                    {
                        var finalStep = await _context.ContractSignatures
                            .Where(cs =>
                                cs.ContractId == contractId)
                            .OrderByDescending(cs => cs.SequenceOrder)
                            .FirstOrDefaultAsync();

                        if (finalStep is null ||
                            finalStep.SignerRole != SignerRole.ViceChancellor ||
                            finalStep.Decision != SignatureDecision.Signed)
                        {
                            await transaction.RollbackAsync();

                            return Fail(
                                "The contract cannot become active because the Vice Chancellor signature has not been completed.");
                        }

                        contract.Status = ContractStatus.Active;
                        contract.UpdatedAtUtc = DateTime.UtcNow;

                        await _context.SaveChangesAsync();
                    }

                    // --------------------------------------------------
                    // Audit
                    // --------------------------------------------------

                    await _auditLogger.LogAsync(
                        AuditAction.ContractSigned,
                        actorUsername,
                        actorRoleLabel,
                        adminAccountId,
                        "Contract",
                        contractId,
                        $"Signed as {role}.",
                        ipAddress);

                    await transaction.CommitAsync();

                    return new ContractSigningResult
                    {
                        Succeeded = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.WriteLine(
                        $"CONTRACT SIGNING ERROR: {ex}");

                    return Fail(
                        "The contract signature could not be saved.");
                }
            });
        }

        // ==============================================================
        // DECLINE CONTRACT
        // ==============================================================

        public async Task<ContractSigningResult> DeclineAsync(
            int contractId,
            SignerRole role,
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
                    // --------------------------------------------------
                    // Lecturer cannot use the admin decline method
                    // --------------------------------------------------

                    if (role == SignerRole.Lecturer)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Lecturers must use the lecturer-specific contract process.");
                    }

                    // --------------------------------------------------
                    // Validate reason
                    // --------------------------------------------------

                    if (string.IsNullOrWhiteSpace(reason))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "A reason is required when declining a contract.");
                    }

                    // --------------------------------------------------
                    // Find contract
                    // --------------------------------------------------

                    var contract = await _context.Contracts
                        .FirstOrDefaultAsync(c => c.Id == contractId);

                    if (contract is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "The contract could not be found.");
                    }

                    // --------------------------------------------------
                    // Contract must still be awaiting signatures
                    // --------------------------------------------------

                    if (contract.Status != ContractStatus.PendingSignature)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "This contract is no longer available for review.");
                    }

                    // --------------------------------------------------
                    // Find the signature step
                    // --------------------------------------------------

                    var step = await _context.ContractSignatures
                        .Where(cs =>
                            cs.ContractId == contractId &&
                            cs.SignerRole == role)
                        .OrderBy(cs => cs.SequenceOrder)
                        .FirstOrDefaultAsync();

                    if (step is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "No signature step was found for this role on this contract.");
                    }

                    // --------------------------------------------------
                    // Prevent duplicate action
                    // --------------------------------------------------

                    if (step.Decision != SignatureDecision.Pending)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "This signature step has already been actioned.");
                    }

                    // --------------------------------------------------
                    // STRICT ROLE AUTHORIZATION
                    // --------------------------------------------------

                    var adminAccount =
                        await _context.AdminAccounts
                            .FirstOrDefaultAsync(a =>
                                a.Id == adminAccountId);

                    if (adminAccount is null)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "The administrator account could not be found.");
                    }

                    if (!IsAuthorizedSigner(adminAccount, role))
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "Your account is not authorized to decline this contract step.");
                    }

                    // --------------------------------------------------
                    // The current role may only decline when it is
                    // actually that role's turn.
                    // --------------------------------------------------

                    bool earlierStepsComplete =
                        !await _context.ContractSignatures
                            .Where(cs =>
                                cs.ContractId == contractId &&
                                cs.SequenceOrder < step.SequenceOrder)
                            .AnyAsync(cs =>
                                cs.Decision != SignatureDecision.Signed);

                    if (!earlierStepsComplete)
                    {
                        await transaction.RollbackAsync();

                        return Fail(
                            "An earlier required signature on this contract is still pending.");
                    }

                    // --------------------------------------------------
                    // Decline
                    // --------------------------------------------------

                    step.Decline(reason);

                    await _context.SaveChangesAsync();

                    // --------------------------------------------------
                    // Audit
                    //
                    // Preserve the existing AuditAction enum member
                    // because we have not yet verified whether the
                    // project contains ContractDeclined.
                    // --------------------------------------------------

                    await _auditLogger.LogAsync(
                        AuditAction.ContractSigned,
                        actorUsername,
                        actorRoleLabel,
                        adminAccountId,
                        "Contract",
                        contractId,
                        $"Declined as {role}: {reason}",
                        ipAddress);

                    await transaction.CommitAsync();

                    return new ContractSigningResult
                    {
                        Succeeded = true
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.WriteLine(
                        $"CONTRACT DECLINE ERROR: {ex}");

                    return Fail(
                        "The contract decline could not be saved.");
                }
            });
        }

        // ==============================================================
        // STRICT SIGNER AUTHORIZATION
        //
        // Lecturer is intentionally excluded because Lecturer signing
        // is handled by SignAsLecturerAsync().
        // ==============================================================

        private static bool IsAuthorizedSigner(
            AdminAccount account,
            SignerRole role)
        {
            return role switch
            {
                // Dean must actually be a Dean account.
                SignerRole.Dean =>
                    account is Dean,

                // HR must be a Management account whose title is HR.
                SignerRole.HROfficer =>
                    account is Management management &&
                    management.Title == ManagementTitle.HROfficer,

                // DVCAR must be a Management account whose title is DVCAR.
                SignerRole.DVCAR =>
                    account is Management management &&
                    management.Title == ManagementTitle.DVCAR,

                // Vice Chancellor must be a Management account whose
                // title is Vice Chancellor.
                SignerRole.ViceChancellor =>
                    account is Management management &&
                    management.Title == ManagementTitle.ViceChancellor,

                // Lecturer is never authorized through SignAsync().
                SignerRole.Lecturer =>
                    false,

                _ =>
                    false
            };
        }

        // ==============================================================
        // FAILURE RESULT
        // ==============================================================

        private static ContractSigningResult Fail(string message)
        {
            return new ContractSigningResult
            {
                Succeeded = false,
                ErrorMessage = message
            };
        }
    }
} 