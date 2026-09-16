using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class ContractSigningService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;
        private readonly EmailService _emailService;

        public ContractSigningService(
            ApplicationDbContext context,
            AuditLogger auditLogger,
            EmailService emailService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _emailService = emailService;
        }

        // ============================================================
        // GET CONTRACT FOR REVIEW
        // ============================================================

        public async Task<ContractReviewDto?> GetContractForReviewAsync(
            int contractId,
            SignerRole role)
        {
            var contract = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Lecturer)
                .Include(c => c.CourseAssignment)
                    .ThenInclude(a => a!.Course)
                .FirstOrDefaultAsync(c =>
                    c.Id == contractId);

            if (contract == null)
                return null;

            var signatures = await _context.ContractSignatures
                .AsNoTracking()
                .Where(s =>
                    s.ContractId == contractId)
                .OrderBy(s =>
                    s.SequenceOrder)
                .ToListAsync();

            var roleStep =
                signatures.FirstOrDefault(
                    s =>
                        s.SignerRole == role);

            bool isThisRolesTurn =
                roleStep != null &&
                roleStep.Decision ==
                    SignatureDecision.Pending &&
                signatures
                    .Where(s =>
                        s.SequenceOrder <
                        roleStep.SequenceOrder)
                    .All(s =>
                        s.Decision ==
                        SignatureDecision.Signed);

            string? blockedReason = null;

            if (roleStep == null)
            {
                blockedReason =
                    "No signature step exists for this role.";
            }
            else if (
                roleStep.Decision ==
                SignatureDecision.Signed)
            {
                blockedReason =
                    "This contract has already been signed by this role.";
            }
            else if (
                roleStep.Decision ==
                SignatureDecision.Declined)
            {
                blockedReason =
                    "This contract has been declined.";
            }
            else if (!isThisRolesTurn)
            {
                var previousStep =
                    signatures
                        .Where(s =>
                            s.SequenceOrder <
                                roleStep.SequenceOrder &&
                            s.Decision !=
                                SignatureDecision.Signed)
                        .OrderBy(s =>
                            s.SequenceOrder)
                        .FirstOrDefault();

                if (previousStep != null)
                {
                    blockedReason =
                        $"Waiting for " +
                        $"{FormatSignerRole(previousStep.SignerRole)} " +
                        "to sign.";
                }
            }

            return new ContractReviewDto
            {
                ContractId =
                    contract.Id,

                LecturerName =
                    contract.Lecturer?.UserName ??
                    "Unknown",

                CourseTitle =
                    contract.CourseAssignment?.Course?.Title ??
                    "—",

                Department =
                    contract.CourseAssignment?.Course?.Department ??
                    "—",

                AllocatedHours =
                    contract.CourseAssignment?.AllocatedHours ??
                    0,

                ContractContent =
                    contract.Content ??
                    string.Empty,

                SignatureStepId =
                    roleStep?.Id,

                IsThisRolesTurn =
                    isThisRolesTurn,

                BlockedReason =
                    blockedReason
            };
        }

        // ============================================================
        // LECTURER SIGN
        // ============================================================

        public async Task<SigningResult> SignAsLecturerAsync(
            int contractId,
            int lecturerId,
            string actorUsername,
            string? ipAddress)
        {
            var strategy =
                _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database
                                .BeginTransactionAsync();

                        try
                        {
                            var lecturer =
                                await _context.Lecturers
                                    .FirstOrDefaultAsync(l =>
                                        l.Id ==
                                            lecturerId &&
                                        l.IsActive);

                            if (lecturer == null)
                            {
                                return SigningResult.Failed(
                                    "The lecturer account could not be found.");
                            }

                            if (string.IsNullOrWhiteSpace(
                                    lecturer.SignatureFilePath))
                            {
                                return SigningResult.Failed(
                                    "You cannot sign because your signature has not been captured.");
                            }

                            if (string.IsNullOrWhiteSpace(
                                    lecturer.SignatureFileHash))
                            {
                                return SigningResult.Failed(
                                    "Your signature hash is missing. Please contact the HOD.");
                            }

                            var contract =
                                await _context.Contracts
                                    .FirstOrDefaultAsync(c =>
                                        c.Id ==
                                            contractId &&
                                        c.LecturerId ==
                                            lecturerId);

                            if (contract == null)
                            {
                                return SigningResult.Failed(
                                    "The contract could not be found.");
                            }

                            if (contract.Status !=
                                ContractStatus.PendingSignature)
                            {
                                return SigningResult.Failed(
                                    "This contract is not awaiting signatures.");
                            }

                            var step =
                                await _context.ContractSignatures
                                    .FirstOrDefaultAsync(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.SignerRole ==
                                            SignerRole.Lecturer);

                            if (step == null)
                            {
                                return SigningResult.Failed(
                                    "The Lecturer signature step could not be found.");
                            }

                            if (step.Decision !=
                                SignatureDecision.Pending)
                            {
                                return SigningResult.Failed(
                                    "The Lecturer signature step has already been processed.");
                            }

                            var previousSteps =
                                await _context.ContractSignatures
                                    .Where(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.SequenceOrder <
                                            step.SequenceOrder)
                                    .OrderBy(s =>
                                        s.SequenceOrder)
                                    .ToListAsync();

                            if (previousSteps.Any(s =>
                                    s.Decision !=
                                    SignatureDecision.Signed))
                            {
                                return SigningResult.Failed(
                                    "The Lecturer must be the first person to sign this contract.");
                            }

                            step.SignAsLecturer(
                                lecturerId,
                                lecturer.SignatureFilePath,
                                lecturer.SignatureFileHash);

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                AuditAction.ContractSigned,
                                actorUsername,
                                "Lecturer",
                                lecturerId,
                                "Contract",
                                contract.Id,
                                $"Contract {contract.Id} signed by Lecturer.",
                                ipAddress);

                            await transaction.CommitAsync();

                            await NotifyNextSignerAsync(
                                contract.Id);

                            return SigningResult.Success(
                                "Your signature was recorded successfully.");
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
            }
            catch (Exception ex)
            {
                return SigningResult.Failed(
                    $"The contract could not be signed: {ex.Message}");
            }
        }

        // ============================================================
        // ADMIN SIGN
        // ============================================================

        public async Task<SigningResult> SignAsync(
            int contractId,
            SignerRole signerRole,
            int adminAccountId,
            string actorUsername,
            string actorRole,
            string? ipAddress)
        {
            if (signerRole == SignerRole.Lecturer)
            {
                return SigningResult.Failed(
                    "Lecturers must use the Lecturer signing process.");
            }

            var strategy =
                _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database
                                .BeginTransactionAsync();

                        try
                        {
                            var contract =
                                await _context.Contracts
                                    .FirstOrDefaultAsync(c =>
                                        c.Id ==
                                            contractId);

                            if (contract == null)
                            {
                                return SigningResult.Failed(
                                    "The contract could not be found.");
                            }

                            if (contract.Status !=
                                ContractStatus.PendingSignature)
                            {
                                return SigningResult.Failed(
                                    "This contract is not awaiting signatures.");
                            }

                            var step =
                                await _context.ContractSignatures
                                    .FirstOrDefaultAsync(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.SignerRole ==
                                            signerRole);

                            if (step == null)
                            {
                                return SigningResult.Failed(
                                    "The signature step could not be found.");
                            }

                            if (step.Decision !=
                                SignatureDecision.Pending)
                            {
                                return SigningResult.Failed(
                                    "This signature step has already been processed.");
                            }

                            var previousSteps =
                                await _context.ContractSignatures
                                    .Where(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.SequenceOrder <
                                            step.SequenceOrder)
                                    .OrderBy(s =>
                                        s.SequenceOrder)
                                    .ToListAsync();

                            var incompletePreviousStep =
                                previousSteps.FirstOrDefault(
                                    s =>
                                        s.Decision !=
                                        SignatureDecision.Signed);

                            if (incompletePreviousStep != null)
                            {
                                return SigningResult.Failed(
                                    $"This contract is still waiting for " +
                                    $"{FormatSignerRole(incompletePreviousStep.SignerRole)} " +
                                    "to sign.");
                            }

                            var adminAccount =
                                await _context.AdminAccounts
                                    .FirstOrDefaultAsync(a =>
                                        a.Id ==
                                            adminAccountId &&
                                        a.IsActive);

                            if (adminAccount == null)
                            {
                                return SigningResult.Failed(
                                    "The signing administrator account could not be found.");
                            }

                            if (!IsAuthorizedSigner(
                                    adminAccount,
                                    signerRole))
                            {
                                return SigningResult.Failed(
                                    "You are not authorized to sign this contract at this stage.");
                            }

                            if (string.IsNullOrWhiteSpace(
                                    adminAccount.SignatureFilePath))
                            {
                                return SigningResult.Failed(
                                    "Your signature has not been captured.");
                            }

                            if (string.IsNullOrWhiteSpace(
                                    adminAccount.SignatureFileHash))
                            {
                                return SigningResult.Failed(
                                    "Your signature hash is missing.");
                            }

                            step.SignAsAdmin(
                                adminAccountId,
                                adminAccount.SignatureFilePath,
                                adminAccount.SignatureFileHash);

                            await _context.SaveChangesAsync();

                            var remainingPendingSteps =
                                await _context.ContractSignatures
                                    .Where(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.Decision ==
                                            SignatureDecision.Pending)
                                    .ToListAsync();

                            if (remainingPendingSteps.Count == 0)
                            {
                                var finalStep =
                                    await _context.ContractSignatures
                                        .Where(s =>
                                            s.ContractId ==
                                                contractId)
                                        .OrderByDescending(
                                            s =>
                                                s.SequenceOrder)
                                        .FirstOrDefaultAsync();

                                if (finalStep != null &&
                                    finalStep.SignerRole ==
                                        SignerRole.ViceChancellor &&
                                    finalStep.Decision ==
                                        SignatureDecision.Signed)
                                {
                                    contract.Status =
                                        ContractStatus.Active;

                                    await _context.SaveChangesAsync();
                                }
                            }

                            await _auditLogger.LogAsync(
                                AuditAction.ContractSigned,
                                actorUsername,
                                actorRole,
                                adminAccountId,
                                "Contract",
                                contract.Id,
                                $"Contract {contract.Id} signed by " +
                                $"{FormatSignerRole(signerRole)}.",
                                ipAddress);

                            await transaction.CommitAsync();

                            await NotifyNextSignerAsync(
                                contract.Id);

                            return SigningResult.Success(
                                "Contract signed successfully.");
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
            }
            catch (Exception ex)
            {
                return SigningResult.Failed(
                    $"The contract could not be signed: {ex.Message}");
            }
        }

        // ============================================================
        // DECLINE
        // ============================================================

        public async Task<SigningResult> DeclineAsync(
            int contractId,
            SignerRole signerRole,
            int adminAccountId,
            string reason,
            string actorUsername,
            string actorRole,
            string? ipAddress)
        {
            if (signerRole == SignerRole.Lecturer)
            {
                return SigningResult.Failed(
                    "Lecturers cannot decline contracts through this process.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return SigningResult.Failed(
                    "A reason for declining the contract is required.");
            }

            var strategy =
                _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database
                                .BeginTransactionAsync();

                        try
                        {
                            var contract =
                                await _context.Contracts
                                    .FirstOrDefaultAsync(c =>
                                        c.Id ==
                                            contractId);

                            if (contract == null)
                            {
                                return SigningResult.Failed(
                                    "The contract could not be found.");
                            }

                            if (contract.Status !=
                                ContractStatus.PendingSignature)
                            {
                                return SigningResult.Failed(
                                    "This contract is not awaiting signatures.");
                            }

                            var step =
                                await _context.ContractSignatures
                                    .FirstOrDefaultAsync(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.SignerRole ==
                                            signerRole);

                            if (step == null)
                            {
                                return SigningResult.Failed(
                                    "The signature step could not be found.");
                            }

                            if (step.Decision !=
                                SignatureDecision.Pending)
                            {
                                return SigningResult.Failed(
                                    "This signature step has already been processed.");
                            }

                            var previousSteps =
                                await _context.ContractSignatures
                                    .Where(s =>
                                        s.ContractId ==
                                            contractId &&
                                        s.SequenceOrder <
                                            step.SequenceOrder)
                                    .OrderBy(s =>
                                        s.SequenceOrder)
                                    .ToListAsync();

                            var previousIncomplete =
                                previousSteps.FirstOrDefault(
                                    s =>
                                        s.Decision !=
                                        SignatureDecision.Signed);

                            if (previousIncomplete != null)
                            {
                                return SigningResult.Failed(
                                    $"You cannot act yet. " +
                                    $"The contract is still waiting for " +
                                    $"{FormatSignerRole(previousIncomplete.SignerRole)}.");
                            }

                            var adminAccount =
                                await _context.AdminAccounts
                                    .FirstOrDefaultAsync(a =>
                                        a.Id ==
                                            adminAccountId &&
                                        a.IsActive);

                            if (adminAccount == null)
                            {
                                return SigningResult.Failed(
                                    "The administrator account could not be found.");
                            }

                            if (!IsAuthorizedSigner(
                                    adminAccount,
                                    signerRole))
                            {
                                return SigningResult.Failed(
                                    "You are not authorized to decline this contract.");
                            }

                            step.Decline(
                                reason.Trim());

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                AuditAction.ContractDeclined,
                                actorUsername,
                                actorRole,
                                adminAccountId,
                                "Contract",
                                contract.Id,
                                $"Contract {contract.Id} declined by " +
                                $"{FormatSignerRole(signerRole)}. " +
                                $"Reason: {reason.Trim()}",
                                ipAddress);

                            await transaction.CommitAsync();

                            return SigningResult.Success(
                                "Contract declined successfully.");
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
            }
            catch (Exception ex)
            {
                return SigningResult.Failed(
                    $"The contract could not be declined: {ex.Message}");
            }
        }

        // ============================================================
        // NOTIFY NEXT CONTRACT SIGNER
        // ============================================================

        private async Task NotifyNextSignerAsync(
            int contractId)
        {
            var signatures =
                await _context.ContractSignatures
                    .AsNoTracking()
                    .Where(s =>
                        s.ContractId ==
                            contractId)
                    .OrderBy(s =>
                        s.SequenceOrder)
                    .ToListAsync();

            var nextStep =
                signatures.FirstOrDefault(
                    s =>
                        s.Decision ==
                        SignatureDecision.Pending);

            if (nextStep == null)
            {
                return;
            }

            var contractReference =
                $"CON-{contractId:D6}";

            // ========================================================
            // DEAN
            // ========================================================

            if (nextStep.SignerRole ==
                SignerRole.Dean)
            {
                var deans =
                    await _context.Deans
                        .AsNoTracking()
                        .Where(d =>
                            d.IsActive)
                        .Select(d =>
                            new
                            {
                                d.Email,
                                d.UserName
                            })
                        .ToListAsync();

                foreach (var dean in deans)
                {
                    if (string.IsNullOrWhiteSpace(
                            dean.Email))
                    {
                        continue;
                    }

                    try
                    {
                        await _emailService
                            .SendContractSigningNotificationAsync(
                                dean.Email,
                                dean.UserName,
                                contractReference);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[Email] Failed to notify Dean " +
                            $"{dean.Email}: {ex.Message}");
                    }
                }

                return;
            }

            // ========================================================
            // MANAGEMENT ROLES
            // ========================================================

            ManagementTitle? managementTitle =
                nextStep.SignerRole switch
                {
                    SignerRole.HROfficer =>
                        ManagementTitle.HROfficer,

                    SignerRole.DVCAR =>
                        ManagementTitle.DVCAR,

                    SignerRole.ViceChancellor =>
                        ManagementTitle.ViceChancellor,

                    _ =>
                        null
                };

            if (managementTitle.HasValue)
            {
                var managementUsers =
                    await _context.ManagementAccounts
                        .AsNoTracking()
                        .Where(m =>
                            m.IsActive &&
                            m.Title ==
                                managementTitle.Value)
                        .Select(m =>
                            new
                            {
                                m.Email,
                                m.UserName
                            })
                        .ToListAsync();

                foreach (var user in managementUsers)
                {
                    if (string.IsNullOrWhiteSpace(
                            user.Email))
                    {
                        continue;
                    }

                    try
                    {
                        await _emailService
                            .SendContractSigningNotificationAsync(
                                user.Email,
                                user.UserName,
                                contractReference);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[Email] Failed to notify " +
                            $"{managementTitle.Value} " +
                            $"{user.Email}: {ex.Message}");
                    }
                }
            }
        }

        // ============================================================
        // AUTHORIZATION
        // ============================================================

        private static bool IsAuthorizedSigner(
            AdminAccount account,
            SignerRole signerRole)
        {
            return signerRole switch
            {
                SignerRole.Dean =>
                    account is Dean,

                SignerRole.HROfficer =>
                    account is Management management &&
                    management.Title ==
                        ManagementTitle.HROfficer,

                SignerRole.DVCAR =>
                    account is Management management &&
                    management.Title ==
                        ManagementTitle.DVCAR,

                SignerRole.ViceChancellor =>
                    account is Management management &&
                    management.Title ==
                        ManagementTitle.ViceChancellor,

                _ =>
                    false
            };
        }

        // ============================================================
        // FORMAT ROLE
        // ============================================================

        private static string FormatSignerRole(
            SignerRole role)
        {
            return role switch
            {
                SignerRole.Lecturer =>
                    "Lecturer",

                SignerRole.Dean =>
                    "Dean",

                SignerRole.HROfficer =>
                    "HR Officer",

                SignerRole.DVCAR =>
                    "DVCAR",

                SignerRole.ViceChancellor =>
                    "Vice Chancellor",

                _ =>
                    role.ToString()
            };
        }
    }

    // =================================================================
    // CONTRACT REVIEW DTO
    // =================================================================

    public class ContractReviewDto
    {
        public int ContractId { get; set; }

        public string LecturerName { get; set; } =
            string.Empty;

        public string CourseTitle { get; set; } =
            string.Empty;

        public string Department { get; set; } =
            string.Empty;

        public decimal AllocatedHours { get; set; }

        public string ContractContent { get; set; } =
            string.Empty;

        public int? SignatureStepId { get; set; }

        public bool IsThisRolesTurn { get; set; }

        public string? BlockedReason { get; set; }
    }

    // =================================================================
    // SIGNING RESULT
    // =================================================================

    public class SigningResult
    {
        public bool Succeeded { get; private set; }

        public string? ErrorMessage { get; private set; }

        public string? SuccessMessage { get; private set; }

        public static SigningResult Success(
            string message)
        {
            return new SigningResult
            {
                Succeeded = true,
                SuccessMessage = message
            };
        }

        public static SigningResult Failed(
            string message)
        {
            return new SigningResult
            {
                Succeeded = false,
                ErrorMessage = message
            };
        }
    }
}