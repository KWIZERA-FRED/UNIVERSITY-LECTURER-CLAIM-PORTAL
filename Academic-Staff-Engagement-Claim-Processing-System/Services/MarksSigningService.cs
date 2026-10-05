using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class MarksSigningService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;
        private readonly EmailService _emailService;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<MarksSigningService> _logger;

        public MarksSigningService(
            ApplicationDbContext context,
            AuditLogger auditLogger,
            EmailService emailService,
            IFileStorageService fileStorage,
            ILogger<MarksSigningService> logger)
        {
            _context = context;
            _auditLogger = auditLogger;
            _emailService = emailService;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<MarksSubmissionResult> SubmitAsync(
            int lecturerId,
            int courseAssignmentId,
            string academicYear,
            Semester semester,
            IFormFile marksFile,
            string actorUsername,
            string? ipAddress)
        {
            if (lecturerId <= 0)
            {
                return Failure("Invalid lecturer.");
            }

            if (courseAssignmentId <= 0)
            {
                return Failure("Please select a course.");
            }

            if (marksFile == null || marksFile.Length == 0)
            {
                return Failure("Please upload the Excel marks sheet.");
            }

            const long maximumFileSize = 10 * 1024 * 1024;

            if (marksFile.Length > maximumFileSize)
            {
                return Failure("The marks file must not exceed 10 MB.");
            }

            if (!string.Equals(
                    Path.GetExtension(marksFile.FileName),
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Only XLSX Excel files are allowed.");
            }

            var lecturer = await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.Id == lecturerId &&
                    l.IsActive);

            if (lecturer == null)
            {
                return Failure("The lecturer account is invalid or inactive.");
            }

            var assignment = await _context.CourseAssignments
                .Include(ca => ca.Course)
                .FirstOrDefaultAsync(ca =>
                    ca.Id == courseAssignmentId &&
                    ca.LecturerId == lecturerId &&
                    ca.IsActive &&
                    ca.Course != null &&
                    ca.Course.IsActive);

            if (assignment == null)
            {
                return Failure(
                    "The selected course assignment is invalid or is no longer available.");
            }

            string assignmentAcademicYear =
                assignment.AcademicYear ?? string.Empty;

            if (!string.Equals(
                    academicYear?.Trim(),
                    assignmentAcademicYear.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    "The academic year does not match the selected course assignment.");
            }

            if (semester != assignment.Semester)
            {
                return Failure(
                    "The semester does not match the selected course assignment.");
            }

            var existingSubmission = await _context.MarksSubmissions
                .FirstOrDefaultAsync(ms =>
                    ms.CourseAssignmentId == assignment.Id &&
                    ms.LecturerId == lecturerId &&
                    ms.Status != MarksSubmissionStatus.Signed);

            if (existingSubmission != null)
            {
                return Failure(
                    "There is already a marks submission waiting for review for this course assignment.");
            }

            byte[] fileBytes;

            try
            {
                await using var inputStream =
                    marksFile.OpenReadStream();

                await using var memoryStream =
                    new MemoryStream();

                await inputStream.CopyToAsync(memoryStream);

                fileBytes = memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to read marks file for lecturer {LecturerId}.",
                    lecturerId);

                return Failure(
                    "The marks file could not be read. Please try again.");
            }

            if (fileBytes.Length == 0)
            {
                return Failure("The uploaded marks file is empty.");
            }

            if (fileBytes.Length > maximumFileSize)
            {
                return Failure("The marks file must not exceed 10 MB.");
            }

            if (fileBytes.Length < 4 ||
                fileBytes[0] != 0x50 ||
                fileBytes[1] != 0x4B ||
                fileBytes[2] != 0x03 ||
                fileBytes[3] != 0x04)
            {
                return Failure("The uploaded file is not a valid XLSX workbook.");
            }

            try
            {
                using var validationStream = new MemoryStream(fileBytes);

                using var archive = new ZipArchive(
                    validationStream,
                    ZipArchiveMode.Read,
                    false);

                if (archive.Entries.Count > 500)
                {
                    return Failure(
                        "The Excel file contains too many internal entries.");
                }

                long uncompressedSize = 0;

                foreach (var entry in archive.Entries)
                {
                    string normalizedPath =
                        entry.FullName.Replace('\\', '/');

                    if (normalizedPath.Contains("../", StringComparison.Ordinal) ||
                        normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
                        Path.IsPathRooted(entry.FullName))
                    {
                        return Failure(
                            "The Excel file contains an invalid internal path.");
                    }

                    uncompressedSize += entry.Length;

                    if (uncompressedSize > 50 * 1024 * 1024)
                    {
                        return Failure(
                            "The Excel file contains too much uncompressed data.");
                    }
                }

                bool hasContentTypes = archive.Entries.Any(e =>
                    string.Equals(
                        e.FullName,
                        "[Content_Types].xml",
                        StringComparison.OrdinalIgnoreCase));

                bool hasWorkbook = archive.Entries.Any(e =>
                    string.Equals(
                        e.FullName,
                        "xl/workbook.xml",
                        StringComparison.OrdinalIgnoreCase));

                if (!hasContentTypes || !hasWorkbook)
                {
                    return Failure(
                        "The uploaded file is not a valid Excel XLSX workbook.");
                }
            }
            catch (InvalidDataException)
            {
                return Failure(
                    "The uploaded file is not a valid XLSX workbook.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "XLSX validation failed for lecturer {LecturerId}.",
                    lecturerId);

                return Failure("The Excel file could not be validated.");
            }

            string fileHash;

            using (var sha256 = SHA256.Create())
            {
                fileHash = Convert.ToHexString(
                    sha256.ComputeHash(fileBytes)).ToLowerInvariant();
            }

            string submissionReference =
                $"MRK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

            string storageKey;

            try
            {
                storageKey = await _fileStorage.SaveAsync(
                    folder: "marks",
                    preferredFileName: marksFile.FileName,
                    content: fileBytes,
                    contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save marks file to storage.");

                return Failure(
                    "The marks file could not be saved. Please try again.");
            }

            var executionStrategy =
                _context.Database.CreateExecutionStrategy();

            try
            {
                await executionStrategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database.BeginTransactionAsync();

                        try
                        {
                            var submission = new MarksSubmission
                            {
                                LecturerId = lecturerId,

                                CourseAssignmentId = assignment.Id,

                                CourseId = assignment.CourseId,

                                AcademicYear = assignmentAcademicYear,

                                Semester = assignment.Semester,

                                FileName = Path.GetFileName(marksFile.FileName),

                                StorageFileId = Guid.Parse(storageKey),

                                FileHash = fileHash,

                                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",

                                FileSizeBytes = fileBytes.Length,

                                SubmissionReference = submissionReference,

                                Status = MarksSubmissionStatus.Pending,

                                SubmittedAtUtc = DateTime.UtcNow
                            };

                            _context.MarksSubmissions.Add(submission);

                            await _context.SaveChangesAsync();

                            await _auditLogger.LogAsync(
                                action: AuditAction.MarksSubmitted,
                                actorUsername: actorUsername,
                                actorRole: "Lecturer",
                                actorId: lecturerId,
                                entityType: "MarksSubmission",
                                entityId: submission.Id,
                                details:
                                    $"Marks submission {submissionReference} submitted for {assignment.Course.Code} - {assignment.Course.Title}.",
                                ipAddress: ipAddress);

                            await transaction.CommitAsync();
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
                _logger.LogError(
                    ex,
                    "Failed to save marks submission {SubmissionReference}.",
                    submissionReference);

                try
                {
                    await _fileStorage.DeleteAsync(storageKey);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(
                        deleteEx,
                        "Failed to remove marks file after database failure.");
                }

                string databaseError =
                    ex.InnerException?.Message ?? ex.Message;

                return Failure($"Database error: {databaseError}");
            }

            try
            {
                var examOffices = await _context.ManagementAccounts
                    .AsNoTracking()
                    .Where(m =>
                        m.IsActive &&
                        m.Title == ManagementTitle.ExamOffice &&
                        !string.IsNullOrWhiteSpace(m.Email))
                    .ToListAsync();

                foreach (var examOffice in examOffices)
                {
                    try
                    {
                        await _emailService
                            .SendMarksSubmissionNotificationAsync(
                                examOffice.Email,
                                examOffice.UserName,
                                lecturer.UserName,
                                $"{assignment.Course.Code} - {assignment.Course.Title}",
                                assignmentAcademicYear,
                                assignment.Semester.ToString(),
                                submissionReference);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(
                            emailEx,
                            "Failed to send marks notification to Exam Office {ManagementId}.",
                            examOffice.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to retrieve Exam Office accounts.");
            }

            return new MarksSubmissionResult
            {
                Succeeded = true,
                SubmissionReference = submissionReference
            };
        }

        public async Task<MarksReviewResult> ReviewAsync(
            int actorId,
            int submissionId,
            bool approve,
            string? remarks,
            string actorUsername,
            string? ipAddress)
        {
            return await ProcessReviewAsync(
                actorId,
                submissionId,
                approve,
                remarks,
                actorUsername,
                ipAddress);
        }

        public async Task<MarksReviewResult> ReviewAsync(
            int actorId,
            bool approve,
            string? remarks,
            int submissionId,
            string actorUsername,
            string? ipAddress)
        {
            return await ProcessReviewAsync(
                actorId,
                submissionId,
                approve,
                remarks,
                actorUsername,
                ipAddress);
        }

        private async Task<MarksReviewResult> ProcessReviewAsync(
            int actorId,
            int submissionId,
            bool approve,
            string? remarks,
            string actorUsername,
            string? ipAddress)
        {
            var examOffice = await _context.ManagementAccounts
                .FirstOrDefaultAsync(m =>
                    m.Id == actorId &&
                    m.IsActive &&
                    m.Title == ManagementTitle.ExamOffice);

            if (examOffice == null)
            {
                return ReviewFailure(
                    "Only the Exam Office is authorized to review marks submissions.");
            }

            var submission = await _context.MarksSubmissions
                .Include(ms => ms.Lecturer)
                .Include(ms => ms.CourseAssignment)
                    .ThenInclude(ca => ca.Course)
                .FirstOrDefaultAsync(ms => ms.Id == submissionId);

            if (submission == null)
            {
                return ReviewFailure(
                    "The marks submission could not be found.");
            }

            if (submission.Status != MarksSubmissionStatus.Pending)
            {
                return ReviewFailure(
                    "This marks submission is no longer awaiting review.");
            }

            if (!approve)
            {
                if (string.IsNullOrWhiteSpace(remarks))
                {
                    return ReviewFailure(
                        "A reason is required when declining a marks submission.");
                }

                submission.Status = MarksSubmissionStatus.Declined;

                submission.ReviewComment = remarks.Trim();

                submission.ReviewedAtUtc = DateTime.UtcNow;

                submission.ReviewedByManagementId = examOffice.Id;

                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync(
                    action: AuditAction.MarksDeclined,
                    actorUsername: actorUsername,
                    actorRole: "Exam Office",
                    actorId: examOffice.Id,
                    entityType: "MarksSubmission",
                    entityId: submission.Id,
                    details:
                        $"Marks submission {submission.SubmissionReference} declined. Reason: {remarks.Trim()}",
                    ipAddress: ipAddress);

                await NotifyLecturerAsync(
                    submission,
                    approved: false,
                    reason: remarks.Trim());

                return new MarksReviewResult { Succeeded = true };
            }

            submission.Status = MarksSubmissionStatus.Signed;

            submission.SignedAtUtc = DateTime.UtcNow;

            submission.ReviewedAtUtc = DateTime.UtcNow;

            submission.ReviewedByManagementId = examOffice.Id;

            submission.ReviewComment = null;

            await _context.SaveChangesAsync();

            await _auditLogger.LogAsync(
                action: AuditAction.MarksSigned,
                actorUsername: actorUsername,
                actorRole: "Exam Office",
                actorId: examOffice.Id,
                entityType: "MarksSubmission",
                entityId: submission.Id,
                details:
                    $"Marks submission {submission.SubmissionReference} signed by Exam Office.",
                ipAddress: ipAddress);

            await NotifyLecturerAsync(
                submission,
                approved: true,
                reason: null);

            return new MarksReviewResult { Succeeded = true };
        }

        private async Task NotifyLecturerAsync(
            MarksSubmission submission,
            bool approved,
            string? reason)
        {
            if (submission.Lecturer == null)
            {
                return;
            }

            string? lecturerEmail = submission.Lecturer.Email;

            if (string.IsNullOrWhiteSpace(lecturerEmail))
            {
                _logger.LogWarning(
                    "Lecturer {LecturerId} has no email address; skipping notification.",
                    submission.LecturerId);

                return;
            }

            string courseLabel =
                submission.CourseAssignment?.Course != null
                    ? $"{submission.CourseAssignment.Course.Code} - {submission.CourseAssignment.Course.Title}"
                    : "your course";

            try
            {
                if (approved)
                {
                    await _emailService.SendMarksSignedNotificationAsync(
                        lecturerEmail,
                        submission.Lecturer.UserName,
                        courseLabel,
                        submission.SubmissionReference);
                }
                else
                {
                    await _emailService.SendMarksDeclinedNotificationAsync(
                        lecturerEmail,
                        submission.Lecturer.UserName,
                        courseLabel,
                        submission.SubmissionReference,
                        reason ?? "No reason provided.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send marks review notification to lecturer {LecturerId}.",
                    submission.LecturerId);
            }
        }

        public async Task<MarksFileDownloadResult?> GetSignedFileAsync(
            int submissionId)
        {
            var submission = await _context.MarksSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(ms =>
                    ms.Id == submissionId &&
                    ms.Status == MarksSubmissionStatus.Signed);

            if (submission == null)
            {
                return null;
            }

            if (submission.StorageFileId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Signed marks submission {SubmissionId} has an empty storage file ID.",
                    submissionId);

                return null;
            }

            var storageKey =
                submission.StorageFileId.ToString("D");

            bool exists;

            try
            {
                exists = await _fileStorage.ExistsAsync(storageKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to verify storage file for signed marks submission {SubmissionId}.",
                    submissionId);

                return null;
            }

            if (!exists)
            {
                _logger.LogWarning(
                    "Storage file {StorageKey} for signed marks submission {SubmissionId} does not exist.",
                    storageKey,
                    submissionId);

                return null;
            }

            byte[] content;

            try
            {
                content = await _fileStorage.ReadAsync(storageKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to read storage file for signed marks submission {SubmissionId}.",
                    submissionId);

                return null;
            }

            if (content == null || content.Length == 0)
            {
                _logger.LogWarning(
                    "Storage file {StorageKey} for signed marks submission {SubmissionId} is empty.",
                    storageKey,
                    submissionId);

                return null;
            }

            return new MarksFileDownloadResult
            {
                Content = content,
                ContentType = string.IsNullOrWhiteSpace(submission.ContentType)
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : submission.ContentType,
                FileName = string.IsNullOrWhiteSpace(submission.FileName)
                    ? $"Marks-{submission.SubmissionReference}.xlsx"
                    : Path.GetFileName(submission.FileName)
            };
        }

        public async Task<MarksFileDownloadResult?> GetSignedFileAsync(
            int actorId,
            int submissionId)
        {
            var examOffice = await _context.ManagementAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.Id == actorId &&
                    m.IsActive &&
                    m.Title == ManagementTitle.ExamOffice);

            if (examOffice == null)
            {
                return null;
            }

            return await GetSignedFileAsync(submissionId);
        }

        private static MarksSubmissionResult Failure(string message)
        {
            return new MarksSubmissionResult
            {
                Succeeded = false,
                ErrorMessage = message
            };
        }

        private static MarksReviewResult ReviewFailure(string message)
        {
            return new MarksReviewResult
            {
                Succeeded = false,
                ErrorMessage = message
            };
        }
    }

    public class MarksFileDownloadResult
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public string ContentType { get; set; } =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public string FileName { get; set; } = "marks.xlsx";
    }

    public class MarksSubmissionResult
    {
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SubmissionReference { get; set; }
    }

    public class MarksReviewResult
    {
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
    }
}