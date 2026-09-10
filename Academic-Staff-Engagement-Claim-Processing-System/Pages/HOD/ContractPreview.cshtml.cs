
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
public class ContractPreviewModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractPreviewModel(
        ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int ContractId { get; set; }

    public string LecturerName { get; private set; } =
        string.Empty;

    public string CourseCode { get; private set; } =
        string.Empty;

    public string CourseTitle { get; private set; } =
        string.Empty;

    public string AcademicYear { get; private set; } =
        string.Empty;

    public string Semester { get; private set; } =
        string.Empty;

    public string Session { get; private set; } =
        string.Empty;

    public string Campus { get; private set; } =
        string.Empty;

    public decimal AllocatedHours { get; private set; }

    public decimal RatePerHour { get; private set; }

    public string ContractContent { get; private set; } =
        string.Empty;

    public ContractStatus Status { get; private set; }

    public string? ErrorMessage { get; private set; }

    public List<SignatureStepRow> SignatureSteps { get; private set; } =
        new();

    // ================================================================
    // GET
    // ================================================================

    public async Task<IActionResult> OnGetAsync()
    {
        // ============================================================
        // LOAD CONTRACT
        // ============================================================

        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .FirstOrDefaultAsync(c =>
                c.Id == ContractId);

        if (contract is null)
        {
            ErrorMessage =
                "The requested contract could not be found.";

            return Page();
        }

        // ============================================================
        // CONTRACT DETAILS
        // ============================================================

        LecturerName =
            contract.Lecturer?.UserName ?? "—";

        CourseCode =
            contract.CourseAssignment?.Course?.Code ?? "—";

        CourseTitle =
            contract.CourseAssignment?.Course?.Title ?? "—";

        AcademicYear =
            contract.CourseAssignment?.AcademicYear ?? "—";

        Semester =
            contract.CourseAssignment?.Semester.ToString() ?? "—";

        Session =
            contract.CourseAssignment?.Session.ToString() ?? "—";

        Campus =
            contract.CourseAssignment?.Campus.ToString() ?? "—";

        AllocatedHours =
            contract.CourseAssignment?.AllocatedHours ?? 0;

        RatePerHour =
            contract.RatePerHour;

        Status =
            contract.Status;

        // ============================================================
        // LOAD LIVE SIGNATURES
        // ============================================================

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s =>
                s.ContractId == contract.Id)
            .OrderBy(s => s.SequenceOrder)
            .ToListAsync();

        // ============================================================
        // LIVE SIGNATURE STEPS
        // ============================================================

        SignatureSteps = signatures
            .Select(s => new SignatureStepRow
            {
                SequenceOrder = s.SequenceOrder,

                Role = s.SignerRole,

                Decision = s.Decision,

                SignedAtUtc = s.SignedAtUtc,

                Comments = s.Comments,

                SignatureFilePath =
                    s.SignatureFilePath
            })
            .ToList();

        // ============================================================
        // BUILD LIVE CONTRACT
        // ============================================================
        //
        // DO NOT overwrite contract.Content.
        //
        // The database continues to contain the original contract
        // snapshot. We create a display version containing the latest
        // signature state.
        // ============================================================

        ContractContent =
            BuildLiveContractContent(
                contract.Content ?? string.Empty,
                signatures);

        return Page();
    }

    // ================================================================
    // LIVE CONTRACT CONTENT
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
    // BUILD SIGNATURE ROW
    // ================================================================

    private static string BuildSignatureRow(
        ContractSignature signature)
    {
        var roleName =
            GetSignerDisplayName(
                signature.SignerRole);

        var signerName =
            GetAuthorizedSignerName(
                signature);

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

                _ =>
                    string.Empty
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
            if (!string.IsNullOrWhiteSpace(
                signature.SignatureFilePath))
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

        if (signature.Decision ==
            SignatureDecision.Declined)
        {
            var reason =
                string.IsNullOrWhiteSpace(
                    signature.Comments)
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
        if (signature.Decision ==
                SignatureDecision.Signed &&
            signature.SignedAtUtc.HasValue)
        {
            var localDate =
                signature.SignedAtUtc
                    .Value
                    .ToLocalTime();

            return $"""
<span class="signature-date">
    {localDate:dd MMMM yyyy}
    <br />
    {localDate:HH:mm}
</span>
""";
        }

        if (signature.Decision ==
                SignatureDecision.Declined &&
            signature.SignedAtUtc.HasValue)
        {
            var localDate =
                signature.SignedAtUtc
                    .Value
                    .ToLocalTime();

            return $"""
<span class="signature-date">
    Declined
    <br />
    {localDate:dd MMMM yyyy}
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
    // ROLE DISPLAY NAME
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
    // AUTHORIZED SIGNER NAME
    // ================================================================

    private static string GetAuthorizedSignerName(
        ContractSignature signature)
    {
        if (signature.SignerRole ==
            SignerRole.Lecturer)
        {
            return signature
                .SignedByLecturer?
                .UserName
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
    // VIEW MODEL
    // ================================================================

    public sealed class SignatureStepRow
    {
        public int SequenceOrder { get; init; }

        public SignerRole Role { get; init; }

        public SignatureDecision Decision { get; init; }

        public DateTime? SignedAtUtc { get; init; }

        public string? Comments { get; init; }

        public string? SignatureFilePath { get; init; }
    }
}

