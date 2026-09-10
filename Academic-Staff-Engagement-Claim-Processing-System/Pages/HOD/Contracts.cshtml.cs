
using System.Net;
using System.Text.RegularExpressions;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ContractsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int? ContractId { get; set; }

    public string HodDepartment { get; private set; } = string.Empty;

    public List<ContractRow> Contracts { get; private set; } = new();

    public ContractDetail? SelectedContract { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var hod = await _context.Hods
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.UserName == username &&
                h.IsActive);

        if (hod is null)
            return RedirectToPage("/Login");

        HodDepartment = hod.Department;

        // ============================================================
        // LOAD CONTRACTS
        // ============================================================

        var contracts = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .Where(c =>
                c.CourseAssignment != null &&
                c.CourseAssignment.Course.Department == hod.Department)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        var contractIds = contracts
            .Select(c => c.Id)
            .ToList();

        // ============================================================
        // LOAD LIVE SIGNATURES
        // ============================================================

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s => contractIds.Contains(s.ContractId))
            .OrderBy(s => s.ContractId)
            .ThenBy(s => s.SequenceOrder)
            .ToListAsync();

        // ============================================================
        // CONTRACT LIST
        // ============================================================

        Contracts = contracts
            .Select(c => new ContractRow
            {
                Id = c.Id,

                Reference = $"CON-{c.Id:D6}",

                LecturerName =
                    c.Lecturer?.UserName ?? "—",

                CourseCode =
                    c.CourseAssignment?.Course?.Code ?? "—",

                CourseTitle =
                    c.CourseAssignment?.Course?.Title ?? "—",

                AcademicYear =
                    c.CourseAssignment?.AcademicYear ?? "—",

                Status = c.Status,

                SignedSteps = signatures.Count(s =>
                    s.ContractId == c.Id &&
                    s.Decision == SignatureDecision.Signed),

                TotalSteps = signatures.Count(s =>
                    s.ContractId == c.Id)
            })
            .ToList();

        // ============================================================
        // SELECTED CONTRACT
        // ============================================================

        if (ContractId.HasValue)
        {
            var contract = contracts
                .FirstOrDefault(c => c.Id == ContractId.Value);

            if (contract is null)
            {
                ErrorMessage =
                    "That contract was not found in your department.";

                return Page();
            }

            var contractSignatures = signatures
                .Where(s => s.ContractId == contract.Id)
                .OrderBy(s => s.SequenceOrder)
                .ToList();

            // --------------------------------------------------------
            // BUILD LIVE CONTRACT HTML
            // --------------------------------------------------------

            var liveContent = BuildLiveContractContent(
                contract.Content ?? string.Empty,
                contractSignatures);

            SelectedContract = new ContractDetail
            {
                Id = contract.Id,

                Reference = $"CON-{contract.Id:D6}",

                LecturerName =
                    contract.Lecturer?.UserName ?? "—",

                CourseTitle =
                    contract.CourseAssignment?.Course?.Title ?? "—",

                AcademicYear =
                    contract.CourseAssignment?.AcademicYear ?? "—",

                // IMPORTANT:
                // This is now the LIVE contract content.
                Content = liveContent,

                Status = contract.Status,

                Steps = contractSignatures
                    .Select(s => new SignatureStepRow
                    {
                        Role = s.SignerRole,

                        Decision = s.Decision,

                        SignedAtUtc = s.SignedAtUtc,

                        Comments = s.Comments,

                        SignatureFilePath = s.SignatureFilePath
                    })
                    .ToList()
            };
        }

        return Page();
    }

    // ================================================================
    // LIVE CONTRACT CONTENT
    // ================================================================
    //
    // Contract.Content remains the original immutable snapshot.
    //
    // We only replace the signature table for display.
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
            .ToList();

        var rows = string.Join(
            Environment.NewLine,
            orderedSignatures.Select(BuildSignatureRow));

        return $"""
<table class="signature-table live-signature-table">

    <thead>

        <tr>

            <th>
                Signatory
            </th>

            <th>
                Name
            </th>

            <th>
                Signature
            </th>

            <th>
                Date
            </th>

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
            GetAuthorizedSignerName(signature);

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

    <td>
        <strong>{safeRole}</strong>
    </td>

    <td>
        {safeSignerName}
    </td>

    <td class="signature-cell">
        {signatureHtml}
    </td>

    <td>
        {dateHtml}
    </td>

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

    <img
        src="{safePath}"
        alt="Electronic signature"
        class="contract-signature-image" />

</div>

<span class="signature-status signed">
    Signed
</span>
""";
            }

            // The database says Signed but there is no image.
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
    // AUTHORIZED SIGNATORY NAME
    // ================================================================

    private static string GetAuthorizedSignerName(
        ContractSignature signature)
    {
        if (signature.SignerRole == SignerRole.Lecturer)
        {
            return signature.SignedByLecturer?.UserName
                ?? "Lecturer";
        }

        return signature.SignerRole switch
        {
            SignerRole.Dean =>
                "Prof. NYESHEJA M. Enan",

            SignerRole.HROfficer =>
                "Mr. NTAKIRUTIMANA Elison",

            SignerRole.DVCAR =>
                "Prof. HAKIZIMANA Emmanuel",

            SignerRole.ViceChancellor =>
                "Prof. NGAMIJE Jean",

            _ =>
                "Authorized Signatory"
        };
    }

    // ================================================================
    // VIEW MODELS
    // ================================================================

    public sealed class ContractRow
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseCode { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public ContractStatus Status { get; init; }

        public int SignedSteps { get; init; }

        public int TotalSteps { get; init; }
    }

    public sealed class ContractDetail
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public string Content { get; init; } =
            string.Empty;

        public ContractStatus Status { get; init; }

        public List<SignatureStepRow> Steps { get; init; } =
            new();
    }

    public sealed class SignatureStepRow
    {
        public SignerRole Role { get; init; }

        public SignatureDecision Decision { get; init; }

        public DateTime? SignedAtUtc { get; init; }

        public string? Comments { get; init; }

        public string? SignatureFilePath { get; init; }
    }
}

