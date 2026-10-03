using System.Security.Claims;

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
public class ContractDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ContractSigningService _contractSigningService;

    public ContractDetailsModel(
        ApplicationDbContext context,
        ContractSigningService contractSigningService)
    {
        _context = context;
        _contractSigningService = contractSigningService;
    }

    public string LecturerName { get; private set; } = string.Empty;

    public ContractDetailView? Details { get; private set; }

    public string? SuccessMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    // ============================================================
    // GET  /Lecturer/ContractDetails/{id}
    // ============================================================

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var lecturerId = GetLecturerId();

        if (lecturerId is null)
            return Challenge();

        SuccessMessage =
            TempData["SuccessMessage"] as string;

        var found =
            await LoadAsync(lecturerId.Value, id);

        if (!found)
            return NotFound();

        return Page();
    }

    // ============================================================
    // SIGN
    // ============================================================

    public async Task<IActionResult> OnPostSignAsync(int id)
    {
        var lecturerId = GetLecturerId();

        if (lecturerId is null)
            return Challenge();

        var result =
            await _contractSigningService.SignAsLecturerAsync(
                id,
                lecturerId.Value,
                User.Identity?.Name ?? "Unknown",
                HttpContext.Connection.RemoteIpAddress?.ToString());

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] =
                "Your signature was recorded. " +
                "The contract will now continue through its approval workflow.";

            return RedirectToPage(new { id });
        }

        ErrorMessage = result.ErrorMessage;

        var found =
            await LoadAsync(lecturerId.Value, id);

        if (!found)
            return NotFound();

        return Page();
    }

    // ============================================================
    // CURRENT LECTURER
    // ============================================================

    private int? GetLecturerId()
    {
        return int.TryParse(
            User.FindFirstValue("UserId"),
            out var lecturerId)
                ? lecturerId
                : null;
    }

    // ============================================================
    // LOAD
    // ============================================================
    //
    // Returns false when the contract does not exist OR does not
    // belong to the logged-in lecturer (so IDs cannot be probed).
    // ============================================================

    private async Task<bool> LoadAsync(
        int lecturerId,
        int contractId)
    {
        LecturerName =
            await _context.Lecturers
                .Where(l =>
                    l.Id == lecturerId &&
                    l.IsActive)
                .Select(l => l.UserName)
                .FirstOrDefaultAsync()
            ?? string.Empty;

        var contract =
            await _context.Contracts
                .AsNoTracking()
                .Include(c => c.CourseAssignment)
                    .ThenInclude(a => a!.Course)
                .FirstOrDefaultAsync(c =>
                    c.Id == contractId &&
                    c.LecturerId == lecturerId);

        if (contract is null)
            return false;

        var signatures =
            await _context.ContractSignatures
                .AsNoTracking()
                .Include(s => s.SignedByAdminAccount)
                .Include(s => s.SignedByLecturer)
                .Where(s =>
                    s.ContractId == contract.Id)
                .OrderBy(s => s.SequenceOrder)
                .ThenBy(s => s.SignerRole)
                .ToListAsync();

        // ========================================================
        // WORKFLOW STEPS (same logic as the Dean review page)
        // ========================================================

        var hasDeclined =
            signatures.Any(s =>
                s.Decision == SignatureDecision.Declined);

        var allSigned =
            signatures.Count > 0 &&
            signatures.All(s =>
                s.Decision == SignatureDecision.Signed);

        ContractSignature? currentStep = null;

        if (hasDeclined)
        {
            currentStep =
                signatures.FirstOrDefault(s =>
                    s.Decision == SignatureDecision.Declined);
        }
        else if (!allSigned)
        {
            currentStep =
                signatures.FirstOrDefault(s =>
                    s.Decision == SignatureDecision.Pending);
        }

        var steps =
            signatures
                .Select(s => new SignatureStepRow
                {
                    SequenceOrder = s.SequenceOrder,
                    SignerRole = s.SignerRole,
                    RoleName = FormatSignerRole(s.SignerRole),
                    Decision = s.Decision,
                    SignedAtUtc = s.SignedAtUtc,
                    Comments = s.Comments,
                    IsCurrent =
                        currentStep != null &&
                        s.Id == currentStep.Id
                })
                .ToList();

        var signedByLecturer =
            signatures.Any(s =>
                s.SignerRole == SignerRole.Lecturer &&
                s.Decision == SignatureDecision.Signed);

        var isClosed =
            contract.Status is
                ContractStatus.Expired or
                ContractStatus.Terminated;

        var isFullySigned =
            contract.Status == ContractStatus.Active;

        var declinedStep =
            steps.FirstOrDefault(s =>
                s.Decision == SignatureDecision.Declined);

        // ========================================================
        // CAN THE LECTURER SIGN NOW?
        // ========================================================

        var canSign =
            !signedByLecturer &&
            !isClosed &&
            !isFullySigned &&
            declinedStep is null;

        string? blockedReason = null;

        if (!canSign && !signedByLecturer)
        {
            blockedReason =
                declinedStep is not null
                    ? $"This contract was declined by {declinedStep.RoleName} and can no longer be signed."
                    : isClosed
                        ? "This contract is closed and can no longer be signed."
                        : null;
        }

        // Contract.Content is the immutable snapshot stored when the
        // contract was generated. Its signature block is rebuilt from
        // the live ContractSignatures rows (same as HOD / Dean pages).
        var liveContent =
            ContractSignatureMarkup.ApplyLiveSignatures(
                contract.Content ?? string.Empty,
                signatures,
                LecturerName);

        Details =
            new ContractDetailView
            {
                Id = contract.Id,
                Reference = $"CON-{contract.Id:D6}",

                CourseCode =
                    contract.CourseAssignment?.Course.Code ?? "—",

                CourseTitle =
                    contract.CourseAssignment?.Course.Title
                    ?? "Unassigned course",

                AcademicYear =
                    contract.CourseAssignment?.AcademicYear ?? "—",

                Campus =
                    contract.CourseAssignment?.Campus.ToString() ?? "—",

                AllocatedHours =
                    contract.CourseAssignment?.AllocatedHours ?? 0,

                CreatedAtUtc = contract.CreatedAtUtc,

                Content = liveContent,
                Status = contract.Status,
                IsSignedByLecturer = signedByLecturer,
                IsClosed = isClosed,
                IsFullySigned = isFullySigned,
                CanSign = canSign,
                BlockedReason = blockedReason,
                Steps = steps
            };

        return true;
    }

    private static string FormatSignerRole(SignerRole role) => role switch
    {
        SignerRole.Lecturer => "Lecturer",
        SignerRole.Dean => "Dean",
        SignerRole.HROfficer => "HR Officer",
        SignerRole.DVCAR => "DVCAR",
        SignerRole.ViceChancellor => "Vice Chancellor",
        _ => role.ToString()
    };

    // ============================================================
    // VIEW MODELS
    // ============================================================

    public sealed class ContractDetailView
    {
        public int Id { get; init; }

        public string Reference { get; init; } = string.Empty;

        public string CourseCode { get; init; } = string.Empty;

        public string CourseTitle { get; init; } = string.Empty;

        public string AcademicYear { get; init; } = string.Empty;

        public string Campus { get; init; } = string.Empty;

        public decimal AllocatedHours { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public string Content { get; init; } = string.Empty;

        public ContractStatus Status { get; init; }

        public bool IsSignedByLecturer { get; init; }

        public bool IsClosed { get; init; }

        public bool IsFullySigned { get; init; }

        public bool CanSign { get; init; }

        public string? BlockedReason { get; init; }

        public List<SignatureStepRow> Steps { get; init; } = new();
    }

    public sealed class SignatureStepRow
    {
        public int SequenceOrder { get; init; }

        public SignerRole SignerRole { get; init; }

        public string RoleName { get; init; } = string.Empty;

        public SignatureDecision Decision { get; init; }

        public DateTime? SignedAtUtc { get; init; }

        public string? Comments { get; init; }

        public bool IsCurrent { get; init; }
    }
}