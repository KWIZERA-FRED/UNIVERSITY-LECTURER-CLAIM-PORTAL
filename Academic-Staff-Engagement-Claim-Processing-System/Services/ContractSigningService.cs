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

        public ContractSigningService(ApplicationDbContext context, AuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        // --------------------------------------------------------------
        // LOAD ONE CONTRACT FOR REVIEW BY A GIVEN ROLE
        // --------------------------------------------------------------

        public async Task<ContractReviewDto?> GetContractForReviewAsync(int contractId, SignerRole role)
        {
            var contract = await _context.Contracts
                .Include(c => c.Lecturer)
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca!.Course)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract is null)
                return null;

            var thisStep = await _context.ContractSignatures
                .Where(cs => cs.ContractId == contractId && cs.SignerRole == role)
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

            if (thisStep.Decision != SignatureDecision.Pending)
            {
                dto.IsThisRolesTurn = false;
                dto.BlockedReason = $"This step has already been {thisStep.Decision.ToString().ToLower()}.";
                return dto;
            }

            bool earlierStepsComplete = !await _context.ContractSignatures
                .Where(cs => cs.ContractId == contractId && cs.SequenceOrder < thisStep.SequenceOrder)
                .AnyAsync(cs => cs.Decision != SignatureDecision.Signed);

            dto.IsThisRolesTurn = earlierStepsComplete;
            dto.BlockedReason = earlierStepsComplete
                ? null
                : "An earlier required signature on this contract is still pending.";

            return dto;
        }

        public async Task<ContractSigningResult> SignAsLecturerAsync(
            int contractId, int lecturerId, string actorUsername, string? ipAddress)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var contract = await _context.Contracts.FirstOrDefaultAsync(c =>
                    c.Id == contractId && c.LecturerId == lecturerId && c.Status == ContractStatus.PendingSignature);
                var step = await _context.ContractSignatures.FirstOrDefaultAsync(s =>
                    s.ContractId == contractId && s.SignerRole == SignerRole.Lecturer);
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.Id == lecturerId && l.IsActive);

                if (contract is null || step is null || lecturer is null || step.Decision != SignatureDecision.Pending)
                    return Fail("This contract is not available for signing.");
                if (string.IsNullOrWhiteSpace(lecturer.SignatureFileHash))
                    return Fail("A verified signature must be captured before you can sign a contract.");

                step.SignAsLecturer(lecturerId, lecturer.SignatureFileHash);
                contract.StampSignature(lecturer.SignatureFileHash);
                contract.Status = ContractStatus.PendingSignature;
                await _context.SaveChangesAsync();
                await _auditLogger.LogAsync(AuditAction.ContractSigned, actorUsername, "Lecturer", lecturerId,
                    "Contract", contractId, "Lecturer signed the contract.", ipAddress);
                await transaction.CommitAsync();
                return new ContractSigningResult { Succeeded = true };
            }
            catch
            {
                await transaction.RollbackAsync();
                return Fail("The contract signature could not be saved.");
            }
        }
        // --------------------------------------------------------------
        // SIGN
        // --------------------------------------------------------------

        public async Task<ContractSigningResult> SignAsync(
            int contractId, SignerRole role, int adminAccountId, string actorUsername, string actorRoleLabel, string? ipAddress)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var step = await _context.ContractSignatures
                    .Where(cs => cs.ContractId == contractId && cs.SignerRole == role)
                    .OrderBy(cs => cs.SequenceOrder)
                    .FirstOrDefaultAsync();

                if (step is null)
                {
                    await transaction.RollbackAsync();
                    return Fail("No signature step found for this role on this contract.");
                }

                if (step.Decision != SignatureDecision.Pending)
                {
                    await transaction.RollbackAsync();
                    return Fail("This step has already been actioned.");
                }

                bool earlierStepsComplete = !await _context.ContractSignatures
                    .Where(cs => cs.ContractId == contractId && cs.SequenceOrder < step.SequenceOrder)
                    .AnyAsync(cs => cs.Decision != SignatureDecision.Signed);

                if (!earlierStepsComplete)
                {
                    await transaction.RollbackAsync();
                    return Fail("An earlier required signature on this contract is still pending.");
                }

                var adminAccount = await _context.AdminAccounts.FirstOrDefaultAsync(a => a.Id == adminAccountId);

                if (adminAccount is null || string.IsNullOrWhiteSpace(adminAccount.SignatureFileHash))
                {
                    await transaction.RollbackAsync();
                    return Fail("Your account does not have a signature on file. Please contact an administrator.");
                }

                step.SignAsAdmin(adminAccountId, adminAccount.SignatureFileHash);
                await _context.SaveChangesAsync();

                bool anyStepsRemaining = await _context.ContractSignatures
                    .Where(cs => cs.ContractId == contractId)
                    .AnyAsync(cs => cs.Decision == SignatureDecision.Pending);

                if (!anyStepsRemaining)
                {
                    var contract = await _context.Contracts.FirstAsync(c => c.Id == contractId);
                    contract.Status = ContractStatus.Active;
                    await _context.SaveChangesAsync();
                }

                await _auditLogger.LogAsync(
                    AuditAction.ContractSigned,
                    actorUsername,
                    actorRoleLabel,
                    adminAccountId,
                    "Contract",
                    contractId,
                    $"Signed as {role}",
                    ipAddress);

                await transaction.CommitAsync();
                return new ContractSigningResult { Succeeded = true };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"CONTRACT SIGNING ERROR: {ex}");
                return Fail("The signature could not be saved.");
            }
        }

        // --------------------------------------------------------------
        // DECLINE
        // --------------------------------------------------------------

        public async Task<ContractSigningResult> DeclineAsync(
            int contractId, SignerRole role, int adminAccountId, string reason,
            string actorUsername, string actorRoleLabel, string? ipAddress)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var step = await _context.ContractSignatures
                    .Where(cs => cs.ContractId == contractId && cs.SignerRole == role)
                    .OrderBy(cs => cs.SequenceOrder)
                    .FirstOrDefaultAsync();

                if (step is null || step.Decision != SignatureDecision.Pending)
                {
                    await transaction.RollbackAsync();
                    return Fail("This step cannot be declined.");
                }

                step.Decline(reason);
                await _context.SaveChangesAsync();

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
                return new ContractSigningResult { Succeeded = true };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"CONTRACT DECLINE ERROR: {ex}");
                return Fail("The decline could not be saved.");
            }
        }

        private static ContractSigningResult Fail(string message) =>
            new() { Succeeded = false, ErrorMessage = message };
    }
}