using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
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
            .FirstOrDefaultAsync(c =>
                c.Id == ClaimId &&
                c.CourseAssignment != null &&
                c.CourseAssignment.LecturerId == lecturer.Id);

        if (claim is null)
        {
            ErrorMessage = "The requested claim could not be found.";
            return Page();
        }

        var publicUrl = Url.Page(
            "/Lecturer/ClaimDocuments",
            null,
            new { claimId = claim.Id },
            Request.Scheme);

        var status = claim.Status.ToString();

        var isRejected = status.Equals(
            "Rejected",
            StringComparison.OrdinalIgnoreCase);

        var isApproved = status.Equals(
            "Approved",
            StringComparison.OrdinalIgnoreCase);

        var isSubmitted = claim.SubmittedAtUtc.HasValue;

        Claim = new ClaimDetailsViewModel
        {
            Id = claim.Id,

            Reference = $"CLM-{claim.Id:D6}",

            ContractReference =
                $"CON-{claim.ContractId:D6}",

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
                publicUrl,

            QrCodeToken =
                claim.QrCodeToken ?? string.Empty,

            IsRejected =
                isRejected,

            IsFullyApproved =
                isApproved,

            Marks = new MarksViewModel
            {
                Reference = "—",
                FileName = "—",
                Status = "Not submitted",
                SignedBy = "—",
                SignedAtUtc = null
            },

            Attendance = new AttendanceViewModel
            {
                MisReference = "—",
                TotalSessions = 0,
                AttendedSessions = 0,
                RetrievedAtUtc = null,
                Records = new List<AttendanceRecordViewModel>()
            },

            Steps = BuildSteps(
                isSubmitted,
                isApproved,
                isRejected)
        };

        return Page();
    }

    private static List<ClaimStepViewModel> BuildSteps(
        bool isSubmitted,
        bool isApproved,
        bool isRejected)
    {
        var decision = isRejected
            ? "Rejected"
            : isApproved
                ? "Approved"
                : "Pending";

        var decidedAt = isApproved || isRejected
            ? DateTime.UtcNow
            : (DateTime?)null;

        return new List<ClaimStepViewModel>
        {
            new()
            {
                Name = "Claim Submitted",
                Decision = isSubmitted ? "Submitted" : "Pending",
                DecidedAtUtc = null,
                ApproverName = "Lecturer",
                RoleLabel = "Lecturer",
                Comments = string.Empty
            },

            new()
            {
                Name = "Dean Review",
                Decision = decision,
                DecidedAtUtc = decidedAt,
                ApproverName = "Dean",
                RoleLabel = "Dean",
                Comments = string.Empty
            },

            new()
            {
                Name = "DVCAR Review",
                Decision = decision,
                DecidedAtUtc = decidedAt,
                ApproverName = "DVCAR",
                RoleLabel = "DVCAR",
                Comments = string.Empty
            },

            new()
            {
                Name = "Payment Processing",
                Decision = isApproved
                    ? "Approved"
                    : isRejected
                        ? "Rejected"
                        : "Pending",
                DecidedAtUtc = decidedAt,
                ApproverName = "Finance",
                RoleLabel = "Finance",
                Comments = string.Empty
            }
        };
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

        public MarksViewModel Marks { get; init; } = new();

        public AttendanceViewModel Attendance { get; init; } = new();

        public List<ClaimStepViewModel> Steps { get; init; } = new();
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

        public TimeSpan StartTime { get; init; }

        public TimeSpan EndTime { get; init; }

        public decimal Hours { get; init; }

        public string DateDisplay =>
            SessionDate.ToString("dd MMM yyyy");

        public string TimeDisplay =>
            $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    }

    public sealed class ClaimStepViewModel
    {
        public string Name { get; init; } = string.Empty;

        public string Decision { get; init; } = string.Empty;

        public DateTime? DecidedAtUtc { get; init; }

        public string ApproverName { get; init; } = string.Empty;

        public string RoleLabel { get; init; } = string.Empty;

        public string Comments { get; init; } = string.Empty;

        public string BadgeClass
        {
            get
            {
                return Decision.ToLowerInvariant() switch
                {
                    "approved" => "bg-success",
                    "submitted" => "bg-primary",
                    "rejected" => "bg-danger",
                    "pending" => "bg-warning text-dark",
                    _ => "bg-secondary"
                };
            }
        }
    }
}