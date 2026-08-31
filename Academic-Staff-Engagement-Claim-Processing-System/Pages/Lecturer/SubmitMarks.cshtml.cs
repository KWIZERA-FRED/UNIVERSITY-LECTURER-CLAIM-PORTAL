using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using LecturerModel =
    Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Lecturer;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer
{
    public class SubmitMarksModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public SubmitMarksModel(
            ApplicationDbContext context,
            EmailService emailService,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _environment = environment;
            _configuration = configuration;
        }

        // =====================================================
        // FORM
        // =====================================================

        [BindProperty]
        public int SelectedCourseAssignmentId { get; set; }

        [BindProperty]
        public string AcademicYear { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? MarksFile { get; set; }

        // =====================================================
        // MESSAGES
        // =====================================================

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        // =====================================================
        // LECTURER
        // =====================================================

        public LecturerModel? CurrentLecturer { get; set; }

        public string LecturerName { get; set; } = string.Empty;

        public string LecturerId { get; set; } = string.Empty;

        // =====================================================
        // ASSIGNMENTS
        // =====================================================

        public List<CourseAssignment> CourseAssignments { get; set; }
            = new List<CourseAssignment>();

        // =====================================================
        // GET
        // =====================================================

        public async Task<IActionResult> OnGetAsync(
            string? success = null)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                return RedirectToPage("/Login");
            }

            var lecturer = await GetCurrentLecturerAsync();

            if (lecturer == null)
            {
                return RedirectToPage("/Login");
            }

            CurrentLecturer = lecturer;

            LecturerName = lecturer.UserName;
            LecturerId = lecturer.Id.ToString();

            await LoadCourseAssignmentsAsync(lecturer.Id);

            if (!string.IsNullOrWhiteSpace(success))
            {
                SuccessMessage =
                    $"Marks submission {success} was successfully uploaded and sent to the Exam Office for review.";
            }

            return Page();
        }

        // =====================================================
        // POST
        // =====================================================

        public async Task<IActionResult> OnPostAsync()
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                return RedirectToPage("/Login");
            }

            // -------------------------------------------------
            // Get logged-in lecturer
            // -------------------------------------------------

            var lecturer = await GetCurrentLecturerAsync();

            if (lecturer == null)
            {
                return RedirectToPage("/Login");
            }

            CurrentLecturer = lecturer;

            LecturerName = lecturer.UserName;
            LecturerId = lecturer.Id.ToString();

            await LoadCourseAssignmentsAsync(lecturer.Id);

            // -------------------------------------------------
            // Validate course assignment
            // -------------------------------------------------

            if (SelectedCourseAssignmentId <= 0)
            {
                ErrorMessage =
                    "Please select the course.";

                return Page();
            }

            // -------------------------------------------------
            // Validate academic year
            // -------------------------------------------------

            if (string.IsNullOrWhiteSpace(AcademicYear))
            {
                ErrorMessage =
                    "Please select the academic year.";

                return Page();
            }

            AcademicYear = AcademicYear.Trim();

            // -------------------------------------------------
            // Validate file
            // -------------------------------------------------

            if (MarksFile == null || MarksFile.Length == 0)
            {
                ErrorMessage =
                    "Please upload the Excel marks sheet.";

                return Page();
            }

            // -------------------------------------------------
            // Validate extension
            // -------------------------------------------------

            string extension =
                Path.GetExtension(MarksFile.FileName)
                    .ToLowerInvariant();

            if (extension != ".xlsx")
            {
                ErrorMessage =
                    "Only Excel .xlsx files are accepted.";

                return Page();
            }

            // -------------------------------------------------
            // Validate size
            // -------------------------------------------------

            const long maxFileSize =
                10 * 1024 * 1024;

            if (MarksFile.Length > maxFileSize)
            {
                ErrorMessage =
                    "The Excel file cannot be larger than 10 MB.";

                return Page();
            }

            // -------------------------------------------------
            // Find assignment belonging to this lecturer
            // -------------------------------------------------

            var courseAssignment =
                await _context.CourseAssignments
                    .Include(ca => ca.Course)
                    .FirstOrDefaultAsync(ca =>
                        ca.Id == SelectedCourseAssignmentId &&
                        ca.LecturerId == lecturer.Id &&
                        ca.IsActive);

            if (courseAssignment == null)
            {
                ErrorMessage =
                    "The selected course assignment could not be found.";

                return Page();
            }

            // -------------------------------------------------
            // Check course active
            // -------------------------------------------------

            if (!courseAssignment.Course.IsActive)
            {
                ErrorMessage =
                    "The selected course is no longer active.";

                return Page();
            }

            // -------------------------------------------------
            // Prevent duplicate pending submission
            // -------------------------------------------------

            bool existingSubmission =
                await _context.MarksSubmissions.AnyAsync(ms =>
                    ms.LecturerId == lecturer.Id &&
                    ms.CourseAssignmentId ==
                        courseAssignment.Id &&
                    ms.AcademicYear == AcademicYear &&
                    ms.Status == "PendingExamOffice");

            if (existingSubmission)
            {
                ErrorMessage =
                    "You already have a marks submission pending with the Exam Office for this course and academic year.";

                return Page();
            }

            // -------------------------------------------------
            // Generate reference
            // -------------------------------------------------

            string submissionReference =
                "MRK-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                "-" +
                Guid.NewGuid()
                    .ToString("N")[..8]
                    .ToUpperInvariant();

            // -------------------------------------------------
            // Secure storage
            //
            // Outside wwwroot
            // -------------------------------------------------

            string marksDirectory =
                Path.Combine(
                    _environment.ContentRootPath,
                    "App_Data",
                    "Marks");

            Directory.CreateDirectory(marksDirectory);

            // -------------------------------------------------
            // Server-side filename
            // -------------------------------------------------

            string storedFileName =
                submissionReference + ".xlsx";

            string storedFilePath =
                Path.Combine(
                    marksDirectory,
                    storedFileName);

            // -------------------------------------------------
            // Save file
            // -------------------------------------------------

            try
            {
                await using var fileStream =
                    new FileStream(
                        storedFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);

                await MarksFile.CopyToAsync(fileStream);
            }
            catch
            {
                ErrorMessage =
                    "The marks file could not be uploaded. Please try again.";

                return Page();
            }

            // -------------------------------------------------
            // Create database record
            // -------------------------------------------------

            var submission = new MarksSubmission
            {
                LecturerId =
                    lecturer.Id,

                CourseAssignmentId =
                    courseAssignment.Id,

                AcademicYear =
                    AcademicYear,

                OriginalFileName =
                    Path.GetFileName(
                        MarksFile.FileName),

                StoredFilePath =
                    storedFilePath,

                FileSize =
                    MarksFile.Length,

                SubmissionReference =
                    submissionReference,

                Status =
                    "PendingExamOffice",

                SubmittedAtUtc =
                    DateTime.UtcNow
            };

            _context.MarksSubmissions.Add(submission);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                if (System.IO.File.Exists(
                    storedFilePath))
                {
                    System.IO.File.Delete(
                        storedFilePath);
                }

                ErrorMessage =
                    "The marks submission could not be saved. Please try again.";

                return Page();
            }

            // =================================================
            // EXAM OFFICE EMAIL
            // =================================================

            try
            {
                string examOfficeEmail =
                    _configuration[
                        "ExamOffice:Email"]
                    ?? throw new InvalidOperationException(
                        "ExamOffice:Email is missing.");

                string examOfficeName =
                    _configuration[
                        "ExamOffice:Name"]
                    ?? "Exam Office";

                await _emailService
                    .SendMarksSubmissionNotificationAsync(
                        examOfficeEmail,
                        examOfficeName,
                        lecturer.UserName,
                        lecturer.Email,
                        courseAssignment.Course.Code,
                        courseAssignment.Course.Title,
                        AcademicYear,
                        submissionReference);
            }
            catch
            {
                // ---------------------------------------------
                // Important:
                //
                // The submission remains in the database.
                // We do NOT delete the marks because the upload
                // succeeded.
                // ---------------------------------------------

                ErrorMessage =
                    "The marks were uploaded successfully, but the Exam Office notification could not be sent. Please contact the administrator.";

                return Page();
            }

            // =================================================
            // SUCCESS
            // =================================================

            return RedirectToPage(
                "/Lecturer/SubmitMarks",
                new
                {
                    success =
                        submissionReference
                });
        }

        // =====================================================
        // LOAD ASSIGNMENTS
        // =====================================================

        private async Task LoadCourseAssignmentsAsync(
            int lecturerId)
        {
            CourseAssignments =
                await _context.CourseAssignments
                    .Include(ca => ca.Course)
                    .Where(ca =>
                        ca.LecturerId == lecturerId &&
                        ca.IsActive &&
                        ca.Course.IsActive)
                    .OrderBy(ca => ca.Course.Code)
                    .ToListAsync();
        }

        // =====================================================
        // CURRENT LECTURER
        // =====================================================

        private async Task<LecturerModel?>
            GetCurrentLecturerAsync()
        {
            // Login.cshtml.cs stores:
            //
            // "UserId"
            //
            // in the authentication cookie.

            string? userId =
                User.FindFirst("UserId")?.Value;

            if (int.TryParse(
                userId,
                out int lecturerId))
            {
                return await _context.Lecturers
                    .FirstOrDefaultAsync(l =>
                        l.Id == lecturerId &&
                        l.IsActive);
            }

            // Fallback to username

            string? username =
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(
                username))
            {
                return null;
            }

            return await _context.Lecturers
                .FirstOrDefaultAsync(l =>
                    l.UserName == username &&
                    l.IsActive);
        }
    }
}