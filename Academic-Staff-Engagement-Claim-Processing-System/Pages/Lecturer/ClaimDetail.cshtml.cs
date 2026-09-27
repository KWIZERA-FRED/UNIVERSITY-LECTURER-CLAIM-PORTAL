using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer;

[Authorize(Roles = "Lecturer")]
public class ClaimDetailModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ClaimDetailModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int ClaimId { get; set; }

    public ClaimDetailView? Claim { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var lecturerId = GetLecturerId();

        if (lecturerId is null)
            return Challenge();

        if (ClaimId <= 0)
        {
            ErrorMessage =
                "The requested claim could not be identified.";

            return Page();
        }

        var claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Course)
            .Include(c => c.Contract)
            .Include(c => c.MarksSubmission)
                .ThenInclude(m => m!.ReviewedByManagement)
            .Include(c => c.Attendance)
                .ThenInclude(a => a!.Records)
            .Include(c => c.Approvals)
                .ThenInclude(a => a.ApprovedByAdminAccount)
            .FirstOrDefaultAsync(c =>
                c.Id == ClaimId &&
                c.CourseAssignment.LecturerId == lecturerId.Value);

        if (claim is null)
        {
            ErrorMessage =
                "That claim could not be found, or does not belong to you.";

            return Page();
        }

        var publicUrl =
            Url.Page(
                "/Public/ClaimDocuments",
                null,
                new
                {
                    token = claim.QrCodeToken
                },
                Request.Scheme);

        Claim = new ClaimDetailView
        {
            Id = claim.Id,

            Reference =
                $"CLM-{claim.Id:D6}",

            ContractReference =
                $"CON-{claim.ContractId:D6}",

            CourseCode =
                claim.CourseAssignment.Course.Code,

            CourseTitle =
                claim.CourseAssignment.Course.Title,

            AcademicYear =
                claim.CourseAssignment.AcademicYear,

            Campus =
                claim.CourseAssignment.Campus.ToString(),

            HoursClaimed =
                claim.HoursClaimed,

            Description =
                claim.Description,

            Status =
                claim.Status,

            SubmittedAtUtc =
                claim.SubmittedAtUtc,

            PublicDocumentsUrl =
                publicUrl,

            QrCodeToken =
                claim.QrCodeToken,

            Marks =
                claim.MarksSubmission is null
                    ? null
                    : new MarksEvidenceView
                    {
                        SubmissionId =
                            claim.MarksSubmission.Id,

                        Reference =
                            claim.MarksSubmission.SubmissionReference,

                        FileName =
                            claim.MarksSubmission.FileName,

                        Status =
                            claim.MarksSubmission.Status,

                        SignedAtUtc =
                            claim.MarksSubmission.ReviewedAtUtc,

                        SignedBy =
                            claim.MarksSubmission
                                .ReviewedByManagement
                                ?.UserName,

                        StorageFileId =
                            claim.MarksSubmission.StorageFileId
                    },

            Attendance =
                claim.Attendance is null
                    ? null
                    : new AttendanceEvidenceView
                    {
                        MisReference =
                            claim.Attendance.MisReference,

                        LecturerName =
                            claim.Attendance.LecturerName,

                        CourseCode =
                            claim.Attendance.CourseCode,

                        CourseTitle =
                            claim.Attendance.CourseTitle,

                        AcademicYear =
                            claim.Attendance.AcademicYear,

                        Semester =
                            claim.Attendance.Semester,

                        TotalSessions =
                            claim.Attendance.TotalSessions,

                        AttendedSessions =
                            claim.Attendance.AttendedSessions,

                        RetrievedAtUtc =
                            claim.Attendance.RetrievedAtUtc,

                        Records =
                            claim.Attendance.Records
                                .OrderBy(r => r.SessionDate)
                                .Select(r =>
                                    new AttendanceRecordView
                                    {
                                        SessionDate =
                                            r.SessionDate,

                                        SessionTitle =
                                            r.SessionTitle,

                                        Attended =
                                            r.Attended
                                    })
                                .ToList()
                    },

            Steps =
                claim.Approvals
                    .OrderBy(a => a.SequenceOrder)
                    .Select(a =>
                        new ApprovalStepView
                        {
                            Role =
                                a.ApprovalRole,

                            Decision =
                                a.Decision,

                            ApproverName =
                                a.ApprovedByAdminAccount
                                    ?.UserName,

                            DecidedAtUtc =
                                a.DecidedAtUtc,

                            Comments =
                                a.Comments
                        })
                    .ToList()
        };

        return Page();
    }

    private int? GetLecturerId()
    {
        var userId =
            User.FindFirstValue("UserId");

        return int.TryParse(
            userId,
            out var lecturerId)
                ? lecturerId
                : null;
    }

    public sealed class ClaimDetailView
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string ContractReference { get; init; } =
            string.Empty;

        public string CourseCode { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public string Campus { get; init; } =
            string.Empty;

        public decimal HoursClaimed { get; init; }

        public string Description { get; init; } =
            string.Empty;

        public ClaimStatus Status { get; init; }

        public DateTime? SubmittedAtUtc { get; init; }

        public string? PublicDocumentsUrl { get; init; }

        public string QrCodeToken { get; init; } =
            string.Empty;

        public MarksEvidenceView? Marks { get; init; }

        public AttendanceEvidenceView? Attendance { get; init; }

        public List<ApprovalStepView> Steps { get; init; } =
            new();

        public bool IsFullyApproved =>
            Status == ClaimStatus.Approved ||
            Status == ClaimStatus.Paid;

        public bool IsRejected =>
            Status == ClaimStatus.Rejected;
    }

    public sealed class MarksEvidenceView
    {
        public int SubmissionId { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string FileName { get; init; } =
            string.Empty;

        public MarksSubmissionStatus Status { get; init; }

        public DateTime? SignedAtUtc { get; init; }

        public string? SignedBy { get; init; }

        public Guid StorageFileId { get; init; }
    }

    public sealed class AttendanceEvidenceView
    {
        public string MisReference { get; init; } =
            string.Empty;

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseCode { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public string Semester { get; init; } =
            string.Empty;

        public int TotalSessions { get; init; }

        public int AttendedSessions { get; init; }

        public DateTime RetrievedAtUtc { get; init; }

        public List<AttendanceRecordView> Records { get; init; } =
            new();
    }

    public sealed class AttendanceRecordView
    {
        public DateTime SessionDate { get; init; }

        public string SessionTitle { get; init; } =
            string.Empty;

        public bool Attended { get; init; }
    }

    public sealed class ApprovalStepView
    {
        public ApprovalRole Role { get; init; }

        public ApprovalDecision Decision { get; init; }

        public string? ApproverName { get; init; }

        public DateTime? DecidedAtUtc { get; init; }

        public string? Comments { get; init; }

        public string RoleLabel =>
            Role switch
            {
                ApprovalRole.Dean => "Dean",
                ApprovalRole.HROfficer => "HR Officer",
                ApprovalRole.DVCAR => "DVCAR",
                ApprovalRole.ViceChancellor => "Vice Chancellor",
                ApprovalRole.HOD => "HOD",
                ApprovalRole.Management => "Management",
                ApprovalRole.DirectorOfQuality => "Director of Quality",
                _ => Role.ToString()
            };

        public string BadgeClass =>
            Decision switch
            {
                ApprovalDecision.Approved => "badge-active",
                ApprovalDecision.Rejected => "badge-closed",
                _ => "badge-pending"
            };
    }
}