using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services;

public sealed class ClaimSubmissionService
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogger _auditLogger;

    public ClaimSubmissionService(ApplicationDbContext context, AuditLogger auditLogger)
    {
        _context = context;
        _auditLogger = auditLogger;
    }

    public async Task<ClaimSubmissionResult> SubmitAsync(
        int lecturerId,
        int courseAssignmentId,
        decimal hoursClaimed,
        string? description,
        string actorUsername,
        string? ipAddress)
    {
        if (hoursClaimed <= 0)
            return ClaimSubmissionResult.Fail("Claimed hours must be greater than zero.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var assignment = await _context.CourseAssignments
                .Include(a => a.Lecturer)
                .FirstOrDefaultAsync(a => a.Id == courseAssignmentId && a.LecturerId == lecturerId && a.IsActive);

            if (assignment is null)
                return ClaimSubmissionResult.Fail("The selected course assignment does not belong to you or is no longer active.");

            if (!assignment.IsApproved)
                return ClaimSubmissionResult.Fail("The course assignment has not been approved by the HOD.");

            if (hoursClaimed != assignment.AllocatedHours)
                return ClaimSubmissionResult.Fail("A claim must cover the verified allocated teaching hours for this assignment.");

            var contract = await _context.Contracts
                .Include(c => c.CourseAssignment)
                .FirstOrDefaultAsync(c => c.LecturerId == lecturerId && c.CourseAssignmentId == courseAssignmentId && c.Status == ContractStatus.Active);

            if (contract is null)
                return ClaimSubmissionResult.Fail("A fully signed active contract is required before a claim can be submitted.");

            var requiredContractSignatures = new[]
            {
                SignerRole.Lecturer, SignerRole.Dean, SignerRole.HROfficer,
                SignerRole.DVCAR, SignerRole.ViceChancellor
            };

            var completedSignatures = await _context.ContractSignatures
                .Where(s => s.ContractId == contract.Id && s.Decision == SignatureDecision.Signed)
                .Select(s => s.SignerRole)
                .ToListAsync();

            if (requiredContractSignatures.Except(completedSignatures).Any())
                return ClaimSubmissionResult.Fail("The contract is not fully signed.");

            var approvedMarks = await _context.MarksSubmissions.AnyAsync(m =>
                m.LecturerId == lecturerId &&
                m.CourseAssignmentId == courseAssignmentId &&
                m.Status == MarksSubmissionStatus.Signed);

            if (!approvedMarks)
                return ClaimSubmissionResult.Fail("Exam Office must sign the marks sheet before a claim can be submitted.");

            var existingOpenClaim = await _context.Claims.AnyAsync(c =>
                c.CourseAssignmentId == courseAssignmentId &&
                c.Status != ClaimStatus.Rejected && c.Status != ClaimStatus.Paid);

            if (existingOpenClaim)
                return ClaimSubmissionResult.Fail("An active claim already exists for this course assignment.");

            var claim = new Claim(0, courseAssignmentId, contract.Id)
            {
                HoursClaimed = hoursClaimed,
                Description = (description ?? string.Empty).Trim(),
                Status = ClaimStatus.Submitted,
                SubmittedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _context.Claims.Add(claim);
            await _context.SaveChangesAsync();

            _context.ClaimApprovals.AddRange(
                new ClaimApproval(0, claim.Id, 1, ApprovalRole.Dean),
                new ClaimApproval(0, claim.Id, 2, ApprovalRole.HROfficer),
                new ClaimApproval(0, claim.Id, 3, ApprovalRole.DVCAR),
                new ClaimApproval(0, claim.Id, 4, ApprovalRole.ViceChancellor));

            await _context.SaveChangesAsync();
            await _auditLogger.LogAsync(AuditAction.ClaimSubmitted, actorUsername, "Lecturer", lecturerId,
                "Claim", claim.Id, $"Submitted claim for assignment {courseAssignmentId}.", ipAddress);

            await transaction.CommitAsync();
            return ClaimSubmissionResult.Success(claim.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ClaimSubmissionResult.Fail("The assignment changed while your claim was being submitted. Please refresh and try again.");
        }
        catch
        {
            await transaction.RollbackAsync();
            return ClaimSubmissionResult.Fail("The claim could not be submitted. No approval was created.");
        }
    }
}

public sealed record ClaimSubmissionResult(bool Succeeded, int? ClaimId, string? ErrorMessage)
{
    public static ClaimSubmissionResult Success(int claimId) => new(true, claimId, null);
    public static ClaimSubmissionResult Fail(string message) => new(false, null, message);
}