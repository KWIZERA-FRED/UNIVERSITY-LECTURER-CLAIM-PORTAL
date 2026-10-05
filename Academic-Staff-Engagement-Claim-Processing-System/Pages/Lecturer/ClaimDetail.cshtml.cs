using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer;

[Authorize(Roles = "Lecturer")]
public class ClaimDetailModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly OfficialDocumentService _officialDocumentService;
    private readonly AuditLogger _auditLogger;

    public ClaimDetailModel(
        ApplicationDbContext context,
        OfficialDocumentService officialDocumentService,
        AuditLogger auditLogger)
    {
        _context = context;
        _officialDocumentService = officialDocumentService;
        _auditLogger = auditLogger;
    }

    // Shown once after the lecturer regenerates the claim link.
    [TempData]
    public string? LinkMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ClaimId { get; set; }

    public ClaimDetailsViewModel? Claim { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var lecturer = await _context.Lecturers
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.UserName == username &&
                l.IsActive);

        if (lecturer is null)
            return RedirectToPage("/Login");

        var claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .Include(c => c.MarksSubmission)
                .ThenInclude(m => m!.StorageFile)
            .Include(c => c.Attendance)
                .ThenInclude(a => a!.Records)
            .FirstOrDefaultAsync(c =>
                c.Id == ClaimId &&
                c.CourseAssignment != null &&
                c.CourseAssignment.LecturerId == lecturer.Id);

        if (claim is null)
        {
            ErrorMessage = "The requested claim could not be found.";
            return Page();
        }

        var publicDocumentsUrl = Url.Page(
            "/Public/ClaimDocuments",
            null,
            new { token = claim.QrCodeToken },
            Request.Scheme);

        var status = claim.Status.ToString();

        var isRejected = status.Equals(
            "Rejected",
            StringComparison.OrdinalIgnoreCase);

        var isApproved = status.Equals(
            "Approved",
            StringComparison.OrdinalIgnoreCase);

        Claim = new ClaimDetailsViewModel
        {
            Id = claim.Id,

            Reference = $"CLM-{claim.Id:D6}",

            ContractReference = $"CON-{claim.ContractId:D6}",

            CourseCode =
                claim.CourseAssignment?.Course?.Code ?? "—",

            CourseTitle =
                claim.CourseAssignment?.Course?.Title ?? "—",

            AcademicYear =
                claim.CourseAssignment?.AcademicYear ?? "—",

            Campus =
                claim.CourseAssignment?.Campus.ToString() ?? "—",

            HoursClaimed =
                claim.HoursClaimed,

            Description =
                claim.Description ?? string.Empty,

            Status =
                status,

            SubmittedAtUtc =
                claim.SubmittedAtUtc,

            PublicDocumentsUrl =
                publicDocumentsUrl,

            QrCodeToken =
                claim.QrCodeToken ?? string.Empty,

            IsFullyApproved =
                isApproved,

            IsRejected =
                isRejected,

            HasMarks =
                claim.MarksSubmission is not null,

            HasAttendance =
                claim.Attendance is not null,

            Marks =
                claim.MarksSubmission is null
                    ? null
                    : new MarksViewModel
                    {
                        Reference =
                            claim.MarksSubmission.SubmissionReference,

                        FileName =
                            claim.MarksSubmission.FileName,

                        Status =
                            claim.MarksSubmission.Status.ToString(),

                        SignedBy =
                            claim.MarksSubmission.ReviewedByManagementId.HasValue
                                ? "Management"
                                : "Exam Office",

                        SignedAtUtc =
                            claim.MarksSubmission.SignedAtUtc
                    },

            Attendance =
                claim.Attendance is null
                    ? null
                    : new AttendanceViewModel
                    {
                        MisReference =
                            claim.Attendance.MisReference,

                        TotalSessions =
                            claim.Attendance.TotalSessions,

                        AttendedSessions =
                            claim.Attendance.AttendedSessions,

                        RetrievedAtUtc =
                            claim.Attendance.RetrievedAtUtc,

                        Records =
                            claim.Attendance.Records
                                .OrderBy(r => r.SessionDate)
                                .Select(r => new AttendanceRecordViewModel
                                {
                                    SessionDate =
                                        r.SessionDate,

                                    SessionTitle =
                                        r.SessionTitle,

                                    Attended =
                                        r.Attended
                                })
                                .ToList()
                    }
        };

        return Page();
    }

    public async Task<IActionResult> OnGetMarksAsync(
        int claimId,
        bool download = false)
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var lecturer = await _context.Lecturers
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.UserName == username &&
                l.IsActive);

        if (lecturer is null)
            return RedirectToPage("/Login");

        var claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.CourseAssignment)
            .Include(c => c.MarksSubmission)
                .ThenInclude(m => m!.StorageFile)
            .FirstOrDefaultAsync(c =>
                c.Id == claimId &&
                c.CourseAssignment != null &&
                c.CourseAssignment.LecturerId == lecturer.Id);

        if (claim?.MarksSubmission?.StorageFile is null)
            return NotFound();

        var storedFile = claim.MarksSubmission.StorageFile;

        if (download)
        {
            await _auditLogger.LogAsync(
                AuditAction.MarksDownloaded,
                lecturer.UserName,
                "Lecturer",
                lecturer.Id,
                "Claim",
                claim.Id,
                $"Signed marks downloaded ({claim.MarksSubmission.FileName})",
                HttpContext.Connection.RemoteIpAddress?.ToString());

            return File(
                storedFile.Content,
                storedFile.ContentType,
                claim.MarksSubmission.FileName);
        }

        return File(
            storedFile.Content,
            storedFile.ContentType);
    }

    public async Task<IActionResult> OnGetAttendanceAsync(
        int claimId)
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var lecturer = await _context.Lecturers
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.UserName == username &&
                l.IsActive);

        if (lecturer is null)
            return RedirectToPage("/Login");

        var claim = await _context.Claims
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == claimId &&
                c.CourseAssignment != null &&
                c.CourseAssignment.LecturerId == lecturer.Id);

        if (claim is null)
            return NotFound();

        var publicDocumentsUrl = Url.Page(
            "/Public/ClaimDocuments",
            null,
            new { token = claim.QrCodeToken },
            Request.Scheme);

        if (string.IsNullOrWhiteSpace(publicDocumentsUrl))
            return NotFound();

        var document =
            await _officialDocumentService.GenerateAsync(
                claim.QrCodeToken,
                OfficialDocumentKind.AttendanceReport,
                publicDocumentsUrl);

        if (document is null)
            return NotFound();

        await _auditLogger.LogAsync(
            AuditAction.ClaimDocumentDownloaded,
            lecturer.UserName,
            "Lecturer",
            lecturer.Id,
            "Claim",
            claim.Id,
            "Attendance report downloaded",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return File(
            document.Content,
            "application/pdf",
            document.FileName);
    }

    // ================================================================
    // REGENERATE CLAIM LINK
    // ================================================================
    //
    // Replaces the QR / public-link token. Use this if the link was
    // shared with the wrong person. The old link and every printed QR
    // code that carries it stop working immediately.
    // ================================================================

    public async Task<IActionResult> OnPostRegenerateLinkAsync(
        int claimId)
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var lecturer = await _context.Lecturers
            .AsNoTracking()
            .FirstOrDefaultAsync(l =>
                l.UserName == username &&
                l.IsActive);

        if (lecturer is null)
            return RedirectToPage("/Login");

        // Ownership check: the claim must belong to this lecturer.
        var claim = await _context.Claims
            .FirstOrDefaultAsync(c =>
                c.Id == claimId &&
                c.CourseAssignment != null &&
                c.CourseAssignment.LecturerId == lecturer.Id);

        if (claim is null)
            return NotFound();

        claim.RegenerateQrToken();

        // Saved in the same SaveChanges as the new token.
        _auditLogger.Add(
            AuditAction.ClaimLinkRegenerated,
            lecturer.UserName,
            "Lecturer",
            lecturer.Id,
            "Claim",
            claim.Id,
            "Claim document link regenerated; the previous link " +
            "and printed QR codes no longer work.",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        await _context.SaveChangesAsync();

        LinkMessage =
            "Your claim link was regenerated. The old link and any " +
            "previously printed QR codes no longer work.";

        return RedirectToPage(new { claimId });
    }

    public sealed class ClaimDetailsViewModel
    {
        public int Id { get; init; }

        public string Reference { get; init; } = string.Empty;

        public string ContractReference { get; init; } = string.Empty;

        public string CourseCode { get; init; } = string.Empty;

        public string CourseTitle { get; init; } = string.Empty;

        public string AcademicYear { get; init; } = string.Empty;

        public string Campus { get; init; } = string.Empty;

        public decimal HoursClaimed { get; init; }

        public string Description { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public DateTime? SubmittedAtUtc { get; init; }

        public string? PublicDocumentsUrl { get; init; }

        public string QrCodeToken { get; init; } = string.Empty;

        public bool IsFullyApproved { get; init; }

        public bool IsRejected { get; init; }

        public bool HasMarks { get; init; }

        public bool HasAttendance { get; init; }

        public MarksViewModel? Marks { get; init; }

        public AttendanceViewModel? Attendance { get; init; }
    }

    public sealed class MarksViewModel
    {
        public string Reference { get; init; } = string.Empty;

        public string FileName { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string SignedBy { get; init; } = string.Empty;

        public DateTime? SignedAtUtc { get; init; }
    }

    public sealed class AttendanceViewModel
    {
        public string MisReference { get; init; } = string.Empty;

        public int TotalSessions { get; init; }

        public int AttendedSessions { get; init; }

        public DateTime? RetrievedAtUtc { get; init; }

        public List<AttendanceRecordViewModel> Records { get; init; } = new();
    }

    public sealed class AttendanceRecordViewModel
    {
        public DateTime SessionDate { get; init; }

        public string SessionTitle { get; init; } = string.Empty;

        public bool Attended { get; init; }
    }
}