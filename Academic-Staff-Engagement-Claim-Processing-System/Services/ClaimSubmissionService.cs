using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public sealed class ClaimSubmissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;
        private readonly IMisAttendanceService _misAttendanceService;

    public ClaimSubmissionService(
        ApplicationDbContext context,
        AuditLogger auditLogger,
        IMisAttendanceService misAttendanceService)
        {
            _context = context;
            _auditLogger = auditLogger;
            _misAttendanceService = misAttendanceService;
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
                return ClaimSubmissionResult.Fail(
                    "Claimed hours must be greater than zero.");

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync();

                try
                {
                    var assignment = await _context.CourseAssignments
                        .Include(a => a.Lecturer)
                        .Include(a => a.Course)
                        .FirstOrDefaultAsync(a =>
                            a.Id == courseAssignmentId &&
                            a.LecturerId == lecturerId &&
                            a.IsActive &&
                            a.Course.IsActive);

                    if (assignment is null)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "The selected course assignment does not belong to you or is no longer active.");
                    }

                    if (hoursClaimed != assignment.AllocatedHours)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "A claim must cover the verified allocated teaching hours for this assignment.");
                    }

                    var contract = await _context.Contracts
                        .FirstOrDefaultAsync(c =>
                            c.LecturerId == lecturerId &&
                            c.CourseAssignmentId == courseAssignmentId &&
                            c.Status == ContractStatus.Active);

                    if (contract is null)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "A fully signed active contract is required before a claim can be submitted.");
                    }

                    var requiredContractSignatures = new[]
                    {
                    SignerRole.Lecturer,
                    SignerRole.Dean,
                    SignerRole.HROfficer,
                    SignerRole.DVCAR,
                    SignerRole.ViceChancellor
                };

                    var completedSignatures =
                        await _context.ContractSignatures
                            .Where(s =>
                                s.ContractId == contract.Id &&
                                s.Decision == SignatureDecision.Signed)
                            .Select(s => s.SignerRole)
                            .ToListAsync();

                    if (requiredContractSignatures
                        .Except(completedSignatures)
                        .Any())
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "The contract is not fully signed.");
                    }

                    var signedMarks = await _context.MarksSubmissions
                        .Include(m => m.ReviewedByManagement)
                        .Where(m =>
                            m.LecturerId == lecturerId &&
                            m.CourseAssignmentId == courseAssignmentId &&
                            m.Status == MarksSubmissionStatus.Signed)
                        .OrderByDescending(m => m.ReviewedAtUtc)
                        .FirstOrDefaultAsync();

                    if (signedMarks is null)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "Exam Office must sign the marks sheet before a claim can be submitted.");
                    }

                    var existingOpenClaim = await _context.Claims
                        .AnyAsync(c =>
                            c.CourseAssignmentId == courseAssignmentId &&
                            c.Status != ClaimStatus.Rejected &&
                            c.Status != ClaimStatus.Paid);

                    if (existingOpenClaim)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "An active claim already exists for this course assignment.");
                    }

                    var attendance =
                        await _misAttendanceService.GetAttendanceAsync(
                            lecturerId,
                            courseAssignmentId);

                    if (attendance is null)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "Attendance could not be retrieved from MIS. The claim cannot be created.");
                    }

                    if (attendance.Records is null ||
                        attendance.Records.Count == 0)
                    {
                        await transaction.RollbackAsync();

                        return ClaimSubmissionResult.Fail(
                            "MIS returned no attendance records. The claim cannot be created.");
                    }

                    var claim = new Claim(
                        0,
                        courseAssignmentId,
                        contract.Id)
                    {
                        MarksSubmissionId = signedMarks.Id,
                        HoursClaimed = hoursClaimed,
                        Description = (description ?? string.Empty).Trim(),
                        Status = ClaimStatus.Submitted,
                        SubmittedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    _context.Claims.Add(claim);

                    await _context.SaveChangesAsync();

                    var claimAttendance = new ClaimAttendance
                    {
                        ClaimId = claim.Id,
                        MisReference = attendance.MisReference,
                        LecturerName = attendance.LecturerName,
                        CourseCode = attendance.CourseCode,
                        CourseTitle = attendance.CourseTitle,
                        AcademicYear = attendance.AcademicYear,
                        Semester = attendance.Semester,
                        TotalSessions = attendance.TotalSessions,
                        AttendedSessions = attendance.AttendedSessions,
                        RetrievedAtUtc = attendance.RetrievedAtUtc
                    };

                    foreach (var record in attendance.Records)
                    {
                        claimAttendance.Records.Add(
                            new ClaimAttendanceRecord
                            {
                                SessionDate = record.SessionDate,
                                SessionTitle = record.SessionTitle,
                                Attended = record.Attended
                            });
                    }

                    _context.ClaimAttendances.Add(claimAttendance);

                    _context.ClaimApprovals.AddRange(
                        new ClaimApproval(
                            0,
                            claim.Id,
                            1,
                            ApprovalRole.HOD),

                        new ClaimApproval(
                            0,
                            claim.Id,
                            2,
                            ApprovalRole.Dean),

                        new ClaimApproval(
                            0,
                            claim.Id,
                            3,
                            ApprovalRole.DirectorOfQuality),

                        new ClaimApproval(
                            0,
                            claim.Id,
                            4,
                            ApprovalRole.DVCAR));

                    await _context.SaveChangesAsync();

                    await _auditLogger.LogAsync(
                        AuditAction.ClaimSubmitted,
                        actorUsername,
                        "Lecturer",
                        lecturerId,
                        "Claim",
                        claim.Id,
                        $"Submitted claim for assignment {courseAssignmentId}. Marks submission {signedMarks.SubmissionReference}. MIS attendance {attendance.MisReference}.",
                        ipAddress);

                    await transaction.CommitAsync();

                    return ClaimSubmissionResult.Success(claim.Id);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();

                    return ClaimSubmissionResult.Fail(
                        "The assignment changed while your claim was being submitted. Please refresh and try again.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    Console.WriteLine(
                        $"CLAIM SUBMISSION ERROR: {ex}");

                    return ClaimSubmissionResult.Fail(
                        "The claim could not be submitted. No approval was created.");
                }
            });
        }
    }

    public sealed record ClaimSubmissionResult(
        bool Succeeded,
        int? ClaimId,
        string? ErrorMessage)
    {
        public static ClaimSubmissionResult Success(
            int claimId) =>
            new(true, claimId, null);

        public static ClaimSubmissionResult Fail(
            string message) =>
            new(false, null, message);
    }

}
