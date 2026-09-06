using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO.Compression;
using System.Security.Cryptography;
using static Academic_Staff_Engagement_Claim_Processing_System.Services.MarksSubmissionResult;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class MarksSubmissionResult
    {
        public bool Succeeded { get; set; }
        public string? SubmissionReference { get; set; }
        public string? ErrorMessage { get; set; }

        public static MarksSubmissionResult Success(string reference)
        {
            return new MarksSubmissionResult
            {
                Succeeded = true,
                SubmissionReference = reference
            };
        }

        public class MarksReviewResult
        {
            public bool Succeeded { get; set; }
            public string? ErrorMessage { get; set; }

            public static MarksReviewResult Success()
            {
                return new MarksReviewResult
                {
                    Succeeded = true
                };
            }

            public static MarksReviewResult Fail(string error)
            {
                return new MarksReviewResult
                {
                    Succeeded = false,
                    ErrorMessage = error
                };
            }
        }
    }

    public class MarksSigningService
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditLogger _auditLogger;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;

        private const long MaxFileSize = 10 * 1024 * 1024;

        private const string ExpectedXlsxContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private const long MaxUncompressedPackageSize =
            50 * 1024 * 1024;

        private const int MaxZipEntries = 500;

        public MarksSigningService(
            ApplicationDbContext context,
            AuditLogger auditLogger,
            IAmazonS3 s3Client,
            IConfiguration configuration)
        {
            _context = context;
            _auditLogger = auditLogger;
            _s3Client = s3Client;
            _configuration = configuration;
        }

        // ============================================================
        // REVIEW / SIGN / DECLINE MARKS
        // ============================================================

        public async Task<MarksReviewResult> ReviewAsync(
            int marksSubmissionId,
            bool approve,
            string? declineComment,
            int actorId,
            string actorUsername,
            string? ipAddress)
        {
            if (marksSubmissionId <= 0)
            {
                return MarksReviewResult.Fail(
                    "Invalid marks submission.");
            }

            if (actorId <= 0)
            {
                return MarksReviewResult.Fail(
                    "Invalid management account.");
            }

            if (string.IsNullOrWhiteSpace(actorUsername))
            {
                return MarksReviewResult.Fail(
                    "Invalid actor.");
            }

            if (!approve &&
                string.IsNullOrWhiteSpace(declineComment))
            {
                return MarksReviewResult.Fail(
                    "A decline comment is required when declining marks.");
            }

            // ========================================================
            // AUTHORIZATION
            // ONLY EXAM OFFICE CAN REVIEW MARKS
            // ========================================================

            var management =
                await _context.ManagementAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.Id == actorId &&
                        m.IsActive &&
                        m.Title == ManagementTitle.ExamOffice);

            if (management is null)
            {
                return MarksReviewResult.Fail(
                    "You are not authorized to review marks submissions.");
            }

            // ========================================================
            // TRANSACTION
            // ========================================================

            var executionStrategy =
                _context.Database.CreateExecutionStrategy();

            try
            {
                return await executionStrategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database.BeginTransactionAsync();

                        try
                        {
                            var submission =
                                await _context.MarksSubmissions
                                    .Include(ms => ms.Course)
                                    .FirstOrDefaultAsync(ms =>
                                        ms.Id == marksSubmissionId);

                            if (submission is null)
                            {
                                await transaction.RollbackAsync();

                                return MarksReviewResult.Fail(
                                    "Marks submission was not found.");
                            }

                            if (submission.Status !=
                                MarksSubmissionStatus.Pending)
                            {
                                await transaction.RollbackAsync();

                                return MarksReviewResult.Fail(
                                    "Only pending marks submissions can be reviewed.");
                            }

                            // ====================================================
                            // APPLY DECISION
                            // ====================================================

                            if (approve)
                            {
                                submission.Status =
                                    MarksSubmissionStatus.Signed;

                                submission.ReviewComment = null;
                            }
                            else
                            {
                                submission.Status =
                                    MarksSubmissionStatus.Declined;

                                submission.ReviewComment =
                                    declineComment!.Trim();
                            }

                            submission.ReviewedByManagementId =
                                management.Id;

                            submission.ReviewedAtUtc =
                                DateTime.UtcNow;

                            await _context.SaveChangesAsync();

                            // ====================================================
                            // AUDIT
                            // ====================================================

                            await _auditLogger.LogAsync(
                                approve
                                    ? AuditAction.MarksSigned
                                    : AuditAction.MarksDeclined,
                                actorUsername,
                                "Management",
                                actorId,
                                nameof(MarksSubmission),
                                submission.Id,
                                approve
                                    ? "Marks submission signed by Exam Office."
                                    : "Marks submission declined by Exam Office.",
                                ipAddress);

                            await transaction.CommitAsync();

                            return MarksReviewResult.Success();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
            }
            catch
            {
                return MarksReviewResult.Fail(
                    "The marks review could not be completed. Please try again.");
            }
        }

        // ============================================================
        // SUBMIT MARKS
        // ============================================================

        public async Task<MarksSubmissionResult> SubmitAsync(
            int lecturerId,
            int courseAssignmentId,
            string academicYear,
            Semester semester,
            IFormFile file,
            string actorUsername,
            string? ipAddress)
        {
            if (lecturerId <= 0)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Invalid lecturer."
                };
            }

            if (courseAssignmentId <= 0)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Invalid course assignment."
                };
            }

            if (string.IsNullOrWhiteSpace(academicYear))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Academic year is required."
                };
            }

            if (file is null || file.Length <= 0)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Please select an XLSX file."
                };
            }

            if (string.IsNullOrWhiteSpace(actorUsername))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Invalid actor."
                };
            }

            // ========================================================
            // NORMALIZE ACADEMIC YEAR
            // ========================================================

            academicYear = academicYear.Trim();

            // ========================================================
            // VALIDATE SEMESTER
            // ========================================================

            if (!Enum.IsDefined(typeof(Semester), semester))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Invalid semester."
                };
            }

            // ========================================================
            // FILE SIZE
            // ========================================================

            if (file.Length > MaxFileSize)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The marks file exceeds the maximum allowed size of 10 MB."
                };
            }

            // ========================================================
            // FILE NAME
            // ========================================================

            var safeFileName =
                Path.GetFileName(file.FileName);

            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Invalid file name."
                };
            }

            if (safeFileName.Length > 255)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "The file name is too long."
                };
            }

            if (!string.Equals(
                    Path.GetExtension(safeFileName),
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Only XLSX files are allowed."
                };
            }

            // ========================================================
            // LOAD LECTURER
            // ========================================================

            var lecturer =
                await _context.Lecturers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l =>
                        l.Id == lecturerId &&
                        l.IsActive);

            if (lecturer is null)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "Lecturer account was not found or is inactive."
                };
            }

            // ========================================================
            // LOAD COURSE ASSIGNMENT
            // ========================================================

            var assignment =
                await _context.CourseAssignments
                    .AsNoTracking()
                    .Include(ca => ca.Course)
                    .FirstOrDefaultAsync(ca =>
                        ca.Id == courseAssignmentId &&
                        ca.LecturerId == lecturerId);

            if (assignment is null)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The course assignment does not belong to this lecturer."
                };
            }

            if (!assignment.IsActive)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "This course assignment is no longer active."
                };
            }

            if (!assignment.IsApproved)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The course assignment has not been approved."
                };
            }

            if (assignment.Course is null ||
                !assignment.Course.IsActive)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The assigned course is not active."
                };
            }

            // ========================================================
            // VERIFY ACADEMIC YEAR
            // ========================================================

            if (!string.Equals(
                    assignment.AcademicYear,
                    academicYear,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The academic year does not match the course assignment."
                };
            }

            // ========================================================
            // VERIFY SEMESTER
            // ========================================================

            if (assignment.Semester != semester)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The semester does not match the course assignment."
                };
            }

            // ========================================================
            // PREVENT DUPLICATE PENDING SUBMISSIONS
            // ========================================================

            var duplicatePending =
                await _context.MarksSubmissions
                    .AsNoTracking()
                    .AnyAsync(ms =>
                        ms.LecturerId == lecturerId &&
                        ms.CourseAssignmentId == courseAssignmentId &&
                        ms.AcademicYear == academicYear &&
                        ms.Status ==
                        MarksSubmissionStatus.Pending);

            if (duplicatePending)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "There is already a pending marks submission for this course."
                };
            }

            // ========================================================
            // READ FILE
            // ========================================================

            byte[] fileBytes;

            await using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);

                fileBytes = memoryStream.ToArray();
            }

            if (fileBytes.LongLength > MaxFileSize)
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The uploaded file exceeds the maximum allowed size."
                };
            }

            // ========================================================
            // XLSX MAGIC-BYTE VALIDATION
            // ========================================================

            if (!IsValidXlsxSignature(fileBytes))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The uploaded file is not a valid XLSX package."
                };
            }

            // ========================================================
            // ZIP PACKAGE VALIDATION
            // ========================================================

            if (!IsSafeXlsxPackage(fileBytes))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The XLSX package failed security validation."
                };
            }

            // ========================================================
            // SHA-256 HASH
            // ========================================================

            var fileHashBytes =
                SHA256.HashData(fileBytes);

            var fileHash =
                Convert.ToHexString(fileHashBytes)
                    .ToLowerInvariant();

            // ========================================================
            // SUBMISSION REFERENCE
            // ========================================================

            var submissionReference =
                $"MRK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

            // ========================================================
            // R2 OBJECT KEY
            // ========================================================

            var objectKey =
                $"marks/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}.xlsx";

            var bucketName =
                _configuration["R2:BucketName"];

            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "Storage configuration is missing."
                };
            }

            var uploadedToStorage = false;

            try
            {
                // ====================================================
                // UPLOAD TO R2
                // ====================================================

                await using var uploadStream =
                    new MemoryStream(fileBytes);

                var putRequest =
                    new PutObjectRequest
                    {
                        BucketName = bucketName,
                        Key = objectKey,
                        InputStream = uploadStream,
                        ContentType = ExpectedXlsxContentType,
                        AutoCloseStream = false
                    };

                putRequest.Metadata["file-hash"] =
                    fileHash;

                putRequest.Metadata["submission-reference"] =
                    submissionReference;

                await _s3Client.PutObjectAsync(putRequest);

                uploadedToStorage = true;

                // ====================================================
                // DATABASE TRANSACTION
                // ====================================================

                var executionStrategy =
                    _context.Database.CreateExecutionStrategy();

                await executionStrategy.ExecuteAsync(
                    async () =>
                    {
                        await using var transaction =
                            await _context.Database.BeginTransactionAsync();

                        try
                        {
                            var submission =
                                new MarksSubmission
                                {
                                    SubmissionReference =
                                        submissionReference,

                                    LecturerId =
                                        lecturerId,

                                    CourseAssignmentId =
                                        courseAssignmentId,

                                    CourseId =
                                        assignment.CourseId,

                                    AcademicYear =
                                        academicYear,

                                    Semester =
                                        semester,

                                    FileName =
                                        safeFileName,

                                    FilePath =
                                        objectKey,

                                    FileHash =
                                        fileHash,

                                    ContentType =
                                        ExpectedXlsxContentType,

                                    Status =
                                        MarksSubmissionStatus.Pending,

                                    SubmittedAtUtc =
                                        DateTime.UtcNow
                                };

                            _context.MarksSubmissions.Add(submission);

                            await _context.SaveChangesAsync();

                            // ====================================================
                            // AUDIT
                            // ====================================================

                            await _auditLogger.LogAsync(
                                AuditAction.MarksSubmitted,
                                actorUsername,
                                "Lecturer",
                                lecturerId,
                                nameof(MarksSubmission),
                                submission.Id,
                                "Marks submitted successfully.",
                                ipAddress);

                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });

                return new MarksSubmissionResult
                {
                    Succeeded = true,
                    SubmissionReference =
                        submissionReference
                };
            }
            catch
            {
                // ====================================================
                // CLEAN UP ORPHANED R2 OBJECT
                // ====================================================

                if (uploadedToStorage)
                {
                    try
                    {
                        await _s3Client.DeleteObjectAsync(
                            new DeleteObjectRequest
                            {
                                BucketName = bucketName,
                                Key = objectKey
                            });
                    }
                    catch
                    {
                        // Deliberately ignored.
                        // The original failure must remain the
                        // returned error.
                    }
                }

                return new MarksSubmissionResult
                {
                    Succeeded = false,
                    ErrorMessage =
                        "The marks submission could not be completed. Please try again."
                };
            }
        }

        // ============================================================
        // SIGNED FILE DOWNLOAD URL
        // ============================================================

        public async Task<string?> GetSignedFileDownloadUrlAsync(
            int marksSubmissionId)
        {
            if (marksSubmissionId <= 0)
            {
                return null;
            }

            var submission =
                await _context.MarksSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ms =>
                        ms.Id == marksSubmissionId &&
                        ms.Status == MarksSubmissionStatus.Signed);

            if (submission is null)
            {
                return null;
            }

            var bucketName =
                _configuration["R2:BucketName"];

            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return null;
            }

            var request =
                new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = submission.FilePath,
                    Verb = HttpVerb.GET,
                    Expires =
                        DateTime.UtcNow.AddMinutes(5)
                };

            return _s3Client.GetPreSignedURL(request);
        }

        // ============================================================
        // XLSX MAGIC-BYTE VALIDATION
        // ============================================================

        private static bool IsValidXlsxSignature(
            byte[] fileBytes)
        {
            if (fileBytes.Length < 4)
            {
                return false;
            }

            return fileBytes[0] == 0x50 &&
                   fileBytes[1] == 0x4B &&
                   fileBytes[2] == 0x03 &&
                   fileBytes[3] == 0x04;
        }

        // ============================================================
        // SAFE XLSX ZIP VALIDATION
        // ============================================================

        private static bool IsSafeXlsxPackage(
            byte[] fileBytes)
        {
            try
            {
                using var stream =
                    new MemoryStream(fileBytes);

                using var archive =
                    new ZipArchive(
                        stream,
                        ZipArchiveMode.Read,
                        leaveOpen: false);

                if (archive.Entries.Count > MaxZipEntries)
                {
                    return false;
                }

                long totalUncompressedSize = 0;

                bool hasContentTypes = false;
                bool hasWorkbook = false;

                foreach (var entry in archive.Entries)
                {
                    var fullName =
                        entry.FullName.Replace('\\', '/');

                    // =================================================
                    // PATH TRAVERSAL PROTECTION
                    // =================================================

                    if (fullName.StartsWith("/") ||
                        fullName.Contains("../") ||
                        fullName.Contains("/..") ||
                        fullName.Contains(":/") ||
                        fullName.Contains(":\\"))
                    {
                        return false;
                    }

                    if (fullName.Contains('\0'))
                    {
                        return false;
                    }

                    // =================================================
                    // ZIP BOMB PROTECTION
                    // =================================================

                    if (entry.Length < 0)
                    {
                        return false;
                    }

                    totalUncompressedSize += entry.Length;

                    if (totalUncompressedSize >
                        MaxUncompressedPackageSize)
                    {
                        return false;
                    }

                    if (string.Equals(
                            fullName,
                            "[Content_Types].xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasContentTypes = true;
                    }

                    if (string.Equals(
                            fullName,
                            "xl/workbook.xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasWorkbook = true;
                    }
                }

                return hasContentTypes && hasWorkbook;
            }
            catch
            {
                return false;
            }
        }
    }
}