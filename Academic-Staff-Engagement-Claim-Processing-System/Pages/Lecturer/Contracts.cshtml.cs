using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;

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
public class ContractsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ContractSigningService _contractSigningService;

    public ContractsModel(
        ApplicationDbContext context,
        ContractSigningService contractSigningService)
    {
        _context = context;
        _contractSigningService = contractSigningService;
    }

    [BindProperty(SupportsGet = true)]
    public int? ContractId { get; set; }

    public string LecturerName { get; private set; } = string.Empty;

    public List<ContractRow> Contracts { get; private set; } = new();

    public ContractDetail? SelectedContract { get; private set; }

    public string? SuccessMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int PendingSignatureCount =>
        Contracts.Count(c =>
            !c.IsSignedByLecturer &&
            !c.IsClosed);

    public int ActiveContractCount =>
        Contracts.Count(c =>
            c.Status == ContractStatus.Active);

    // ============================================================
    // GET
    // ============================================================

    public async Task<IActionResult> OnGetAsync()
    {
        var lecturerId = GetLecturerId();

        if (lecturerId is null)
            return Challenge();

        SuccessMessage =
            TempData["SuccessMessage"] as string;

        await LoadAsync(lecturerId.Value);

        return Page();
    }

    // ============================================================
    // SIGN
    // ============================================================

    public async Task<IActionResult> OnPostSignAsync(
        int contractId)
    {
        var lecturerId = GetLecturerId();

        if (lecturerId is null)
            return Challenge();

        var result =
            await _contractSigningService.SignAsLecturerAsync(
                contractId,
                lecturerId.Value,
                User.Identity?.Name ?? "Unknown",
                HttpContext.Connection.RemoteIpAddress?.ToString());

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] =
                "Your signature was recorded. " +
                "The contract will now continue through its approval workflow.";

            return RedirectToPage(
                new
                {
                    ContractId = contractId
                });
        }

        ErrorMessage =
            result.ErrorMessage;

        await LoadAsync(
            lecturerId.Value);

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

    private async Task LoadAsync(
        int lecturerId)
    {
        LecturerName =
            await _context.Lecturers
                .Where(l =>
                    l.Id == lecturerId &&
                    l.IsActive)
                .Select(l => l.UserName)
                .FirstOrDefaultAsync()
            ?? string.Empty;

        var contracts =
            await _context.Contracts
                .AsNoTracking()
                .Include(c => c.CourseAssignment)
                    .ThenInclude(a => a!.Course)
                .Where(c =>
                    c.LecturerId == lecturerId)
                .OrderByDescending(
                    c => c.CreatedAtUtc)
                .ToListAsync();

        var contractIds =
            contracts
                .Select(c => c.Id)
                .ToList();

        var signedContractIds =
            (await _context.ContractSignatures
                .AsNoTracking()
                .Where(s =>
                    contractIds.Contains(s.ContractId) &&
                    s.SignerRole == SignerRole.Lecturer &&
                    s.Decision == SignatureDecision.Signed)
                .Select(s =>
                    s.ContractId)
                .ToListAsync())
            .ToHashSet();

        Contracts =
            contracts
                .Select(c => new ContractRow
                {
                    Id = c.Id,

                    Reference =
                        $"CON-{c.Id:D6}",

                    CourseCode =
                        c.CourseAssignment?.Course.Code
                        ?? "—",

                    CourseTitle =
                        c.CourseAssignment?.Course.Title
                        ?? "Unassigned course",

                    AcademicYear =
                        c.CourseAssignment?.AcademicYear
                        ?? "—",

                    Campus =
                        c.CourseAssignment?.Campus.ToString()
                        ?? "—",

                    AllocatedHours =
                        c.CourseAssignment?.AllocatedHours
                        ?? 0,

                    Status =
                        c.Status,

                    IsSignedByLecturer =
                        signedContractIds.Contains(c.Id)
                })
                .ToList();

        // ========================================================
        // SELECTED CONTRACT
        // ========================================================

        if (!ContractId.HasValue)
            return;

        var contract =
            contracts.FirstOrDefault(
                c => c.Id == ContractId.Value);

        if (contract is null)
        {
            ErrorMessage ??=
                "The requested contract was not found.";

            return;
        }

        var signatureSteps =
            await _context.ContractSignatures
                .AsNoTracking()
                .Include(s => s.SignedByAdminAccount)
                .Include(s => s.SignedByLecturer)
                .Where(s =>
                    s.ContractId == contract.Id)
                .OrderBy(s => s.SequenceOrder)
                .ThenBy(s => s.SignerRole)
                .ToListAsync();

        var signerStatuses =
            signatureSteps
                .Select(s => new SignerStatusRow
                {
                    Role =
                        s.SignerRole,

                    SequenceOrder =
                        s.SequenceOrder,

                    Decision =
                        s.Decision,

                    SignedAtUtc =
                        s.SignedAtUtc,

                    SignatureFilePath =
                        s.SignatureFilePath,

                    SignerDisplayName =
                        s.SignerRole == SignerRole.Lecturer
                            ? LecturerName
                            : s.SignedByAdminAccount?.UserName
                })
                .ToList();

        // ========================================================
        // LIVE CONTRACT CONTENT
        // ========================================================
        //
        // Contract.Content is the immutable snapshot stored when
        // the contract was generated. It contains a static
        // signature table with no images.
        //
        // We rebuild that table from the live ContractSignatures
        // rows so the rendered document shows the real signature
        // images (same behaviour as the HOD page).
        //

        var liveContent =
            BuildLiveContractContent(
                contract.Content ?? string.Empty,
                signatureSteps);

        SelectedContract =
            new ContractDetail
            {
                Id =
                    contract.Id,

                Reference =
                    $"CON-{contract.Id:D6}",

                Content =
                    liveContent,

                Status =
                    contract.Status,

                IsSignedByLecturer =
                    signedContractIds.Contains(contract.Id),

                IsClosed =
                    contract.Status is
                        ContractStatus.Expired or
                        ContractStatus.Terminated,

                IsFullySigned =
                    contract.Status ==
                    ContractStatus.Active,

                SignerStatuses =
                    signerStatuses
            };
    }

    // ================================================================
    // LIVE CONTRACT CONTENT
    // ================================================================
    //
    // Contract.Content remains the original immutable snapshot.
    //
    // We only replace the signature table for display, so the
    // rendered contract shows the real signatures from
    // ContractSignatures.SignatureFilePath.
    // ================================================================

    private static string BuildLiveContractContent(
        string originalContent,
        IReadOnlyCollection<ContractSignature> signatures)
    {
        if (string.IsNullOrWhiteSpace(originalContent))
            return string.Empty;

        if (signatures.Count == 0)
            return originalContent;

        var pattern =
            @"<table\s+class\s*=\s*[""']signature-table[""'][^>]*>.*?</table>";

        var liveSignatureTable =
            BuildLiveSignatureTable(signatures);

        return Regex.Replace(
            originalContent,
            pattern,
            liveSignatureTable,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);
    }

    // ================================================================
    // BUILD LIVE SIGNATURE TABLE
    // ================================================================

    private static string BuildLiveSignatureTable(
        IEnumerable<ContractSignature> signatures)
    {
        var orderedSignatures = signatures
            .OrderBy(s => s.SequenceOrder)
            .ThenBy(s => s.SignerRole)
            .ToList();

        var rows = string.Join(
            Environment.NewLine,
            orderedSignatures.Select(BuildSignatureRow));

        return $"""
<table class="signature-table live-signature-table">

    <thead>

        <tr>

            <th>Signatory</th>

            <th>Name</th>

            <th>Signature</th>

            <th>Date</th>

        </tr>

    </thead>

    <tbody>

        {rows}

    </tbody>

</table>
""";
    }

    // ================================================================
    // BUILD INDIVIDUAL SIGNATURE ROW
    // ================================================================

    private static string BuildSignatureRow(
        ContractSignature signature)
    {
        var roleName =
            GetSignerDisplayName(signature.SignerRole);

        var signerName =
            GetSignerName(signature);

        var safeRole =
            WebUtility.HtmlEncode(roleName);

        var safeSignerName =
            WebUtility.HtmlEncode(signerName);

        var rowClass =
            signature.Decision switch
            {
                SignatureDecision.Signed =>
                    "signature-row-signed",

                SignatureDecision.Declined =>
                    "signature-row-declined",

                _ => string.Empty
            };

        var signatureHtml =
            BuildSignatureCell(signature);

        var dateHtml =
            BuildDateCell(signature);

        return $"""
<tr class="{rowClass}">

    <td><strong>{safeRole}</strong></td>

    <td>{safeSignerName}</td>

    <td class="signature-cell">{signatureHtml}</td>

    <td>{dateHtml}</td>

</tr>
""";
    }

    // ================================================================
    // SIGNATURE CELL
    // ================================================================

    private static string BuildSignatureCell(
        ContractSignature signature)
    {
        if (signature.Decision == SignatureDecision.Signed)
        {
            if (!string.IsNullOrWhiteSpace(signature.SignatureFilePath))
            {
                var safePath =
                    WebUtility.HtmlEncode(
                        signature.SignatureFilePath);

                return $"""
<div class="signature-image-wrapper">

    <img src="{safePath}"
         alt="Electronic signature"
         class="contract-signature-image" />

</div>

<span class="signature-status signed">
    Signed
</span>
""";
            }

            // The database says Signed but no image was stored.
            return """
<span class="signature-missing">
    Signature recorded
</span>

<br />

<span class="signature-status signed">
    Signed
</span>
""";
        }

        if (signature.Decision == SignatureDecision.Declined)
        {
            var reason = string.IsNullOrWhiteSpace(signature.Comments)
                ? "No reason provided."
                : signature.Comments.Trim();

            return $"""
<span class="signature-status declined">
    Declined
</span>

<br />

<span class="signature-missing">
    {WebUtility.HtmlEncode(reason)}
</span>
""";
        }

        return """
<span class="signature-placeholder">
    Pending electronic signature
</span>

<br />

<span class="signature-status pending">
    Pending
</span>
""";
    }

    // ================================================================
    // DATE CELL
    // ================================================================

    private static string BuildDateCell(
        ContractSignature signature)
    {
        if (signature.Decision == SignatureDecision.Signed &&
            signature.SignedAtUtc.HasValue)
        {
            return $"""
<span class="signature-date">
    {signature.SignedAtUtc.Value.ToLocalTime():dd MMMM yyyy}
    <br />
    {signature.SignedAtUtc.Value.ToLocalTime():HH:mm}
</span>
""";
        }

        if (signature.Decision == SignatureDecision.Declined &&
            signature.SignedAtUtc.HasValue)
        {
            return $"""
<span class="signature-date">
    Declined
    <br />
    {signature.SignedAtUtc.Value.ToLocalTime():dd MMMM yyyy}
</span>
""";
        }

        return """
<span class="signature-placeholder">
    Pending
</span>
""";
    }

    // ================================================================
    // SIGNER ROLE DISPLAY
    // ================================================================

    private static string GetSignerDisplayName(
        SignerRole role)
    {
        return role switch
        {
            SignerRole.Lecturer =>
                "Lecturer",

            SignerRole.Dean =>
                "Dean of Faculty",

            SignerRole.HROfficer =>
                "Human Resource Officer",

            SignerRole.DVCAR =>
                "DVCAR",

            SignerRole.ViceChancellor =>
                "Vice Chancellor",

            _ =>
                role.ToString()
        };
    }

    // ================================================================
    // SIGNER NAME
    // ================================================================
    //
    // Lecturer → the lecturer who owns the contract
    // Admin    → the account that actually signed
    // Fallback → role label
    //

    private static string GetSignerName(
        ContractSignature signature)
    {
        if (signature.SignerRole == SignerRole.Lecturer)
        {
            return signature.SignedByLecturer?.UserName
                ?? "Lecturer";
        }

        return signature.SignedByAdminAccount?.UserName
            ?? GetSignerDisplayName(signature.SignerRole);
    }

    // ============================================================
    // CONTRACT ROW
    // ============================================================

    public sealed class ContractRow
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string CourseCode { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public string Campus { get; init; } =
            string.Empty;

        public decimal AllocatedHours { get; init; }

        public ContractStatus Status { get; init; }

        public bool IsSignedByLecturer { get; init; }

        public bool IsClosed =>
            Status is
                ContractStatus.Expired or
                ContractStatus.Terminated;
    }

    // ============================================================
    // CONTRACT DETAIL
    // ============================================================

    public sealed class ContractDetail
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string Content { get; init; } =
            string.Empty;

        public ContractStatus Status { get; init; }

        public bool IsSignedByLecturer { get; init; }

        public bool IsClosed { get; init; }

        public bool IsFullySigned { get; init; }

        public List<SignerStatusRow> SignerStatuses { get; init; } =
            new();
    }

    // ============================================================
    // SIGNER STATUS
    // ============================================================

    public sealed class SignerStatusRow
    {
        public SignerRole Role { get; init; }

        public int SequenceOrder { get; init; }

        public SignatureDecision Decision { get; init; }

        public DateTime? SignedAtUtc { get; init; }

        public string? SignerDisplayName { get; init; }

        // ========================================================
        // ACTUAL SIGNATURE IMAGE
        // ========================================================

        public string? SignatureFilePath { get; init; }
    }
}