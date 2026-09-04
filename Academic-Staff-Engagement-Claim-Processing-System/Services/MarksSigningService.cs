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
    /// <summary>
    /// Result returned when a marks submission is attempted.
    /// </summary>
    public class MarksSubmissionResult
    {
        public bool Succeeded { get; set; }

        public string? SubmissionReference { get; set; }

        public string? ErrorMessage { get; set; }

        public static MarksSubmissionResult Success(
            string submissionReference)
        {
            return new MarksSubmissionResult
            {
                Succeeded = true,
                SubmissionReference = submissionReference
            };
        }
        public class MarksReviewResult
        {
            public bool Succeeded { get; set; }
            public string? ErrorMessage { get; set; }
        }
        public static MarksSubmissionResult Fail(
            string message)
        {
            return new MarksSubmissionResult
            {
                Succeeded = false,
                ErrorMessage = message
            };
        }
    }


    /// <summary>
    /// Handles the secure submission of lecturer marks.
    ///
    /// Responsibilities:
    /// - Verify the authenticated lecturer
    /// - Verify course ownership
    /// - Validate academic period
    /// - Validate uploaded Excel file
    /// - Calculate SHA-256 integrity hash
    /// - Store the file privately in Cloudflare R2
    /// - Create the MarksSubmission database record
    /// - Record an audit event
    /// - Clean up the uploaded file if database persistence fails
    /// </summary>
    public class MarksSigningService
    {
        // ============================================================
        // SECURITY / FILE LIMITS
        // ============================================================

        /*
         * Maximum accepted upload size:
         *
         * 10 MB
         *
         * This protects the application from unnecessarily large
         * uploads consuming server memory and storage.
         */
        private const long MaxFileSize =
            10 * 1024 * 1024;


        /*
         * Expected MIME type for modern Excel .xlsx files.
         *
         * This value is assigned by the SERVER.
         * We do not trust the MIME type supplied by the browser.
         */
        private const string ExpectedContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";


        /*
         * XLSX files are ZIP/OpenXML packages.
         *
         * We place limits on the uncompressed package as an
         * additional defense against ZIP-bomb style uploads.
         */
        private const long MaxUncompressedPackageSize =
            50 * 1024 * 1024;


        private const int MaxZipEntries = 500;


        // ============================================================
        // DEPENDENCIES
        // ============================================================

        private readonly ApplicationDbContext _context;

        private readonly AuditLogger _auditLogger;

        private readonly IAmazonS3 _s3Client;

        private readonly IConfiguration _configuration;


        // ============================================================
        // CONSTRUCTOR
        // ============================================================

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
        // REVIEW (SIGN / DECLINE) MARKS SUBMISSION
        // ============================================================

        public async Task<MarksReviewResult> ReviewAsync(
            int marksSubmissionId,
            bool approve,
            string? comment,
            int managementId,
            string actorUsername,
            string? ipAddress)
        {
            if (marksSubmissionId <= 0)
                return new MarksReviewResult { Succeeded = false, ErrorMessage = "Invalid marks submission." };

            if (!approve && string.IsNullOrWhiteSpace(comment))
                return new MarksReviewResult { Succeeded = false, ErrorMessage = "Please provide a reason for declining this submission." };

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var submission = await _context.MarksSubmissions
                    .Include(ms => ms.Course)
                    .FirstOrDefaultAsync(ms =>
                        ms.Id == marksSubmissionId &&
                        ms.Status == MarksSubmissionStatus.Pending);

                if (submission is null)
                {
                    await transaction.RollbackAsync();
                    return new MarksReviewResult { Succeeded = false, ErrorMessage = "This submission is not available for review." };
                }

                submission.Status = approve ? MarksSubmissionStatus.Signed : MarksSubmissionStatus.Declined;
                submission.ReviewedByManagementId = managementId;
                submission.ReviewedAtUtc = DateTime.UtcNow;
                submission.ReviewComment = comment;

                await _context.SaveChangesAsync();

                await _auditLogger.LogAsync(
                    approve ? AuditAction.MarksSigned : AuditAction.MarksDeclined,
                    actorUsername,
                    "Management",
                    managementId,
                    "MarksSubmission",
                    submission.Id,
                    approve
                        ? $"Marks signed for {submission.Course.Code} ({submission.AcademicYear}, {submission.Semester})."
                        : $"Marks declined for {submission.Course.Code}: {comment}",
                    ipAddress);

                await transaction.CommitAsync();
                return new MarksReviewResult { Succeeded = true };
            }
            catch
            {
                await transaction.RollbackAsync();
                return new MarksReviewResult { Succeeded = false, ErrorMessage = "The review could not be saved." };
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
            IFormFile marksFile,
            string actorUsername,
            string? ipAddress)
        {
            // ========================================================
            // BASIC SECURITY VALIDATION
            // ========================================================

            if (lecturerId <= 0)
            {
                return MarksSubmissionResult.Fail(
                    "Invalid lecturer account.");
            }


            if (courseAssignmentId <= 0)
            {
                return MarksSubmissionResult.Fail(
                    "Invalid course assignment.");
            }


            if (string.IsNullOrWhiteSpace(academicYear))
            {
                return MarksSubmissionResult.Fail(
                    "Academic year is required.");
            }


            if (marksFile == null || marksFile.Length == 0)
            {
                return MarksSubmissionResult.Fail(
                    "Please upload the Excel marks sheet.");
            }


            if (string.IsNullOrWhiteSpace(actorUsername))
            {
                return MarksSubmissionResult.Fail(
                    "The authenticated lecturer could not be identified.");
            }


            // ========================================================
            // NORMALIZE ACADEMIC YEAR
            // ========================================================

            academicYear = academicYear.Trim();


            // ========================================================
            // VALIDATE SEMESTER
            // ========================================================

            if (!Enum.IsDefined(
                    typeof(Semester),
                    semester))
            {
                return MarksSubmissionResult.Fail(
                    "Invalid semester.");
            }


            // ========================================================
            // FILE SIZE VALIDATION
            // ========================================================

            if (marksFile.Length > MaxFileSize)
            {
                return MarksSubmissionResult.Fail(
                    "The Excel file cannot be larger than 10 MB.");
            }


            // ========================================================
            // SANITIZE ORIGINAL FILE NAME
            // ========================================================

            /*
             * Path.GetFileName() prevents an uploaded filename such as:
             *
             * ../../some-file.xlsx
             *
             * from being treated as a server path.
             */

            var originalFileName =
                Path.GetFileName(marksFile.FileName);


            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                return MarksSubmissionResult.Fail(
                    "The uploaded file has an invalid filename.");
            }


            // ========================================================
            // EXTENSION VALIDATION
            // ========================================================

            var extension =
                Path.GetExtension(originalFileName)
                    .ToLowerInvariant();


            if (extension != ".xlsx")
            {
                return MarksSubmissionResult.Fail(
                    "Only .xlsx Excel files are accepted.");
            }


            // ========================================================
            // FILE NAME LENGTH
            // ========================================================

            if (originalFileName.Length > 255)
            {
                return MarksSubmissionResult.Fail(
                    "The uploaded filename is too long.");
            }


            // ========================================================
            // AUTHENTICATED LECTURER
            // ========================================================

            /*
             * IMPORTANT:
             *
             * lecturerId must come from the authenticated UserId claim.
             *
             * This service still verifies the ID against the database.
             *
             * We never trust a CourseAssignment's LecturerId supplied
             * independently by the browser.
             */

            var lecturer =
                await _context.Lecturers
                    .FirstOrDefaultAsync(l =>
                        l.Id == lecturerId &&
                        l.IsActive);


            if (lecturer == null)
            {
                return MarksSubmissionResult.Fail(
                    "Your lecturer account could not be verified.");
            }


            // ========================================================
            // COURSE ASSIGNMENT OWNERSHIP
            // ========================================================

            /*
             * The lecturer must:
             *
             * 1. Own the assignment
             * 2. Have an active assignment
             * 3. Have an approved assignment
             * 4. Belong to an active course
             */

            var assignment =
                await _context.CourseAssignments
                    .AsNoTracking()
                    .Include(ca => ca.Course)
                    .FirstOrDefaultAsync(ca =>
                        ca.Id == courseAssignmentId &&
                        ca.LecturerId == lecturerId &&
                        ca.IsActive &&
                        ca.IsApproved &&
                        ca.Course.IsActive);


            if (assignment == null)
            {
                return MarksSubmissionResult.Fail(
                    "You are not assigned to the selected course.");
            }


            // ========================================================
            // ACADEMIC YEAR VALIDATION
            // ========================================================

            if (!string.Equals(
                    assignment.AcademicYear,
                    academicYear,
                    StringComparison.Ordinal))
            {
                return MarksSubmissionResult.Fail(
                    "The selected academic year does not match your course assignment.");
            }


            // ========================================================
            // SEMESTER VALIDATION
            // ========================================================

            if (assignment.Semester != semester)
            {
                return MarksSubmissionResult.Fail(
                    "The selected semester does not match your course assignment.");
            }


            // ========================================================
            // DUPLICATE PENDING SUBMISSION CHECK
            // ========================================================

            /*
             * Prevent the same lecturer from creating multiple pending
             * submissions for the same course assignment and academic
             * period.
             */

            bool pendingSubmissionExists =
                await _context.MarksSubmissions
                    .AsNoTracking()
                    .AnyAsync(ms =>
                        ms.LecturerId == lecturerId &&
                        ms.CourseAssignmentId == courseAssignmentId &&
                        ms.AcademicYear == academicYear &&
                        ms.Semester == semester &&
                        ms.Status == MarksSubmissionStatus.Pending);


            if (pendingSubmissionExists)
            {
                return MarksSubmissionResult.Fail(
                    "You already have a marks submission waiting for review for this course.");
            }


            // ========================================================
            // READ FILE
            // ========================================================

            byte[] fileBytes;


            try
            {
                await using var input =
                    marksFile.OpenReadStream();

                await using var memory =
                    new MemoryStream();

                await input.CopyToAsync(memory);

                fileBytes = memory.ToArray();
            }
            catch
            {
                return MarksSubmissionResult.Fail(
                    "The uploaded file could not be read.");
            }


            // ========================================================
            // DOUBLE-CHECK FILE SIZE
            // ========================================================

            /*
             * We check both the IFormFile length and the actual byte
             * array length.
             */

            if (fileBytes.LongLength > MaxFileSize)
            {
                return MarksSubmissionResult.Fail(
                    "The Excel file cannot be larger than 10 MB.");
            }


            // ========================================================
            // XLSX SIGNATURE VALIDATION
            // ========================================================

            if (!IsValidXlsxSignature(fileBytes))
            {
                return MarksSubmissionResult.Fail(
                    "The uploaded file is not a valid .xlsx workbook.");
            }


            // ========================================================
            // OPENXML PACKAGE VALIDATION
            // ========================================================

            if (!IsSafeXlsxPackage(fileBytes))
            {
                return MarksSubmissionResult.Fail(
                    "The uploaded Excel workbook is invalid or unsafe.");
            }


            // ========================================================
            // SHA-256 FILE HASH
            // ========================================================

            string fileHash;


            using (var sha256 =
                   SHA256.Create())
            {
                byte[] hash =
                    sha256.ComputeHash(fileBytes);

                fileHash =
                    Convert.ToHexString(hash)
                        .ToLowerInvariant();
            }


            // ========================================================
            // SERVER-GENERATED SUBMISSION REFERENCE
            // ========================================================

            /*
             * The browser does not control this reference.
             *
             * Example:
             *
             * MRK-20260831-7a8f...
             */

            string submissionReference =
                $"MRK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";


            // ========================================================
            // SERVER-GENERATED R2 OBJECT KEY
            // ========================================================

            /*
             * Never use the user's original filename as the storage key.
             *
             * This prevents:
             *
             * - path traversal
             * - predictable object names
             * - filename collisions
             */

            string storageKey =
                $"marks/{DateTime.UtcNow:yyyy/MM/dd}/" +
                $"{Guid.NewGuid():N}.xlsx";


            // ========================================================
            // R2 BUCKET
            // ========================================================

            string? bucketName =
                _configuration["R2:BucketName"];


            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return MarksSubmissionResult.Fail(
                    "Secure file storage is not configured.");
            }


            // ========================================================
            // UPLOAD + DATABASE PERSISTENCE
            // ========================================================

            bool fileUploaded = false;


            try
            {
                // ====================================================
                // UPLOAD TO PRIVATE R2
                // ====================================================

                await using var uploadStream =
                    new MemoryStream(fileBytes);


                var putRequest =
                    new PutObjectRequest
                    {
                        BucketName =
                            bucketName,

                        Key =
                            storageKey,

                        InputStream =
                            uploadStream,

                        /*
                         * The object must not be public.
                         */
                        CannedACL =
                            S3CannedACL.Private,

                        /*
                         * Use the server-defined MIME type.
                         */
                        ContentType =
                            ExpectedContentType,

                        AutoCloseStream =
                            false
                    };


                /*
                 * Store integrity metadata alongside the object.
                 */
                putRequest.Metadata["file-hash"] =
                    fileHash;

                putRequest.Metadata["submission-reference"] =
                    submissionReference;


                await _s3Client.PutObjectAsync(
                    putRequest);


                fileUploaded = true;


                // ====================================================
                // DATABASE TRANSACTION
                // ====================================================

                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync();


                try
                {
                    // =================================================
                    // CREATE SUBMISSION
                    // =================================================

                    var submission =
                        new MarksSubmission
                        {
                            SubmissionReference =
                                submissionReference,

                            LecturerId =
                                lecturer.Id,

                            CourseAssignmentId =
                                assignment.Id,

                            CourseId =
                                assignment.CourseId,

                            AcademicYear =
                                assignment.AcademicYear,

                            Semester =
                                assignment.Semester,

                            FileName =
                                originalFileName,

                            FilePath =
                                storageKey,

                            FileHash =
                                fileHash,

                            ContentType =
                                ExpectedContentType,

                            FileSizeBytes =
                                fileBytes.LongLength,

                            SubmittedAtUtc =
                                DateTime.UtcNow,

                            Status =
                                MarksSubmissionStatus.Pending
                        };


                    _context.MarksSubmissions.Add(
                        submission);


                    // =================================================
                    // SAVE
                    // =================================================

                    await _context.SaveChangesAsync();


                    // =================================================
                    // COMMIT DATABASE TRANSACTION
                    // =================================================

                    await transaction.CommitAsync();


                    // =================================================
                    // AUDIT
                    // =================================================

                    /*
                     * IMPORTANT:
                     *
                     * Use the actual database-generated submission.Id.
                     *
                     * Do NOT use GetHashCode() because hash codes are not
                     * stable identifiers.
                     */

                    await _auditLogger.LogAsync(
                        AuditAction.MarksSubmitted,
                        actorUsername,
                        "Lecturer",
                        lecturer.Id,
                        "MarksSubmission",
                        submission.Id,
                        $"Marks submitted for " +
                        $"{assignment.Course.Code} " +
                        $"for {assignment.AcademicYear}, " +
                        $"{assignment.Semester}.",
                        ipAddress);


                    // =================================================
                    // SUCCESS
                    // =================================================

                    return MarksSubmissionResult.Success(
                        submissionReference);
                }
                catch
                {
                    // =================================================
                    // DATABASE ROLLBACK
                    // =================================================

                    await transaction.RollbackAsync();

                    throw;
                }
            }
            catch
            {
                // ====================================================
                // DELETE ORPHANED R2 OBJECT
                // ====================================================

                /*
                 * If R2 upload succeeded but database persistence failed,
                 * remove the uploaded object.
                 *
                 * This prevents abandoned marks files from accumulating
                 * in storage.
                 */

                if (fileUploaded)
                {
                    try
                    {
                        await _s3Client.DeleteObjectAsync(
                            new DeleteObjectRequest
                            {
                                BucketName =
                                    bucketName,

                                Key =
                                    storageKey
                            });
                    }
                    catch
                    {
                        /*
                         * Do not expose cleanup errors to the lecturer.
                         *
                         * The original operation already failed.
                         */
                    }
                }


                return MarksSubmissionResult.Fail(
                    "The marks submission could not be completed. Please try again.");
            }
        }


        // ============================================================
        // XLSX SIGNATURE VALIDATION
        // ============================================================

        private static bool IsValidXlsxSignature(
            byte[] fileBytes)
        {
            /*
             * XLSX is an OpenXML ZIP package.
             *
             * Standard ZIP local-file header:
             *
             * 50 4B 03 04
             */

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
        // OPENXML / ZIP PACKAGE VALIDATION
        // ============================================================

        private static bool IsSafeXlsxPackage(
            byte[] fileBytes)
        {
            try
            {
                using var memoryStream =
                    new MemoryStream(
                        fileBytes,
                        writable: false);


                using var archive =
                    new ZipArchive(
                        memoryStream,
                        ZipArchiveMode.Read,
                        leaveOpen: false);


                // ----------------------------------------------------
                // ENTRY COUNT
                // ----------------------------------------------------

                if (archive.Entries.Count >
                    MaxZipEntries)
                {
                    return false;
                }


                bool hasContentTypes =
                    false;

                bool hasWorkbook =
                    false;

                long totalUncompressedSize =
                    0;


                // ----------------------------------------------------
                // INSPECT ZIP ENTRIES
                // ----------------------------------------------------

                foreach (var entry in archive.Entries)
                {
                    /*
                     * Reject suspicious entry paths.
                     */

                    if (string.IsNullOrWhiteSpace(
                            entry.FullName))
                    {
                        return false;
                    }


                    string normalizedPath =
                        entry.FullName
                            .Replace('\\', '/');


                    if (normalizedPath.StartsWith("/") ||
                        normalizedPath.Contains("../") ||
                        normalizedPath.Contains("/..") ||
                        normalizedPath.Contains(":/"))
                    {
                        return false;
                    }


                    /*
                     * Directory entries have zero length and are fine.
                     */

                    if (entry.Length < 0)
                    {
                        return false;
                    }


                    /*
                     * Protect against excessive decompression.
                     */

                    totalUncompressedSize +=
                        entry.Length;


                    if (totalUncompressedSize >
                        MaxUncompressedPackageSize)
                    {
                        return false;
                    }


                    if (string.Equals(
                            normalizedPath,
                            "[Content_Types].xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasContentTypes = true;
                    }


                    if (string.Equals(
                            normalizedPath,
                            "xl/workbook.xml",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasWorkbook = true;
                    }
                }


                /*
                 * A legitimate XLSX workbook should contain both
                 * OpenXML content types and the workbook definition.
                 */

                return hasContentTypes &&
                       hasWorkbook;
            }
            catch
            {
                /*
                 * Any invalid ZIP/OpenXML structure is rejected.
                 */

                return false;
            }
        }
    }
}