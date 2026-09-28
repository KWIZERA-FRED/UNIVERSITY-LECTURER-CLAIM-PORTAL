using System.Net;
using System.Text;
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
public class ContractDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractDetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int? ContractId { get; set; }

    public string HodFaculty { get; private set; } = string.Empty;

    public ContractDetail? Contract { get; private set; }

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

        HodFaculty = hod.Faculty.ToString();

        if (!ContractId.HasValue)
        {
            ErrorMessage = "No contract was specified.";
            return Page();
        }

        var facultyDepartments = FacultyDepartments
            .GetDepartments(hod.Faculty)
            .Select(d => d.ToString())
            .ToList();

        if (facultyDepartments.Count == 0)
        {
            ErrorMessage =
                "No departments are configured for your faculty.";

            return Page();
        }

        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .FirstOrDefaultAsync(c =>
                c.Id == ContractId.Value &&
                c.CourseAssignment != null &&
                c.CourseAssignment.Course != null &&
                facultyDepartments.Contains(
                    c.CourseAssignment.Course.Department));

        if (contract is null)
        {
            ErrorMessage =
                "That contract was not found within your faculty.";

            return Page();
        }

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s => s.ContractId == contract.Id)
            .OrderBy(s => s.SequenceOrder)
            .ToListAsync();

        var liveContent = BuildLiveContractContent(
            contract.Content ?? string.Empty,
            signatures,
            contract.Lecturer?.UserName);

        Contract = new ContractDetail
        {
            Id = contract.Id,

            Reference =
                $"CON-{contract.Id:D6}",

            LecturerName =
                contract.Lecturer?.UserName ?? "—",

            CourseTitle =
                contract.CourseAssignment?.Course?.Title ?? "—",

            AcademicYear =
                contract.CourseAssignment?.AcademicYear ?? "—",

            Content =
                liveContent,

            Status =
                contract.Status,

            Steps =
                signatures
                    .Select(s => new SignatureStepRow
                    {
                        Role =
                            s.SignerRole,

                        Decision =
                            s.Decision,

                        SignedAtUtc =
                            s.SignedAtUtc,

                        Comments =
                            s.Comments,

                        SignatureFilePath =
                            s.SignatureFilePath
                    })
                    .ToList()
        };

        return Page();
    }

    private static string BuildLiveContractContent(
    string originalContent,
    IReadOnlyCollection<ContractSignature> signatures,
    string? lecturerName)
    {
        if (string.IsNullOrWhiteSpace(originalContent))
            return string.Empty;

        if (signatures.Count == 0)
            return originalContent;

        var content = originalContent;

        content =
            RemoveOldSignatureTable(content);

        var orderedSignatures = signatures
            .OrderBy(s => s.SequenceOrder)
            .ToList();

        foreach (var signature in orderedSignatures)
        {
            content =
                ReplacePaperSignatureLine(
                    content,
                    signature,
                    lecturerName);
        }

        return content;
    }

    private static string RemoveOldSignatureTable(
        string html)
    {
        var pattern =
            @"<table\s+class\s*=\s*[""']signature-table[^""']*[""'][^>]*>.*?</table>";

        return Regex.Replace(
            html,
            pattern,
            string.Empty,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);
    }

    private static string ReplacePaperSignatureLine(
        string html,
        ContractSignature signature,
        string? lecturerName)
    {
        var roleClass =
            GetRoleCssClass(signature.SignerRole);

        var pattern =
            $@"<div\s+class\s*=\s*[""']paper-signature-line\s+{Regex.Escape(roleClass)}[""'][^>]*>.*?</div>";

        var replacement =
            BuildPaperSignatureLine(
                signature,
                lecturerName);

        return Regex.Replace(
            html,
            pattern,
            replacement,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);
    }

    private static string BuildPaperSignatureLine(
        ContractSignature signature,
        string? lecturerName)
    {
        var signerName =
            GetAuthorizedSignerName(
                signature,
                lecturerName);

        var safeSignerName =
            WebUtility.HtmlEncode(
                signerName);

        var signatureContent =
            BuildSignatureContent(
                signature);

        var dateContent =
            BuildDateContent(
                signature);

        var roleClass =
            GetRoleCssClass(
                signature.SignerRole);

        return $"""
<div class="paper-signature-line {roleClass}">

    <span class="paper-signature-name">
        {safeSignerName}
    </span>

    <span class="paper-signature-field paper-signature-area">
        {signatureContent}
    </span>

    <span class="paper-signature-field paper-date-area">
        {dateContent}
    </span>

</div>
""";
    }

    private static string BuildSignatureContent(
        ContractSignature signature)
    {
        if (signature.Decision == SignatureDecision.Signed &&
            !string.IsNullOrWhiteSpace(
                signature.SignatureFilePath))
        {
            var safePath =
                WebUtility.HtmlEncode(
                    signature.SignatureFilePath);

            return $"""
<img
    src="{safePath}"
    alt="Electronic signature"
    class="contract-signature-image" />
""";
        }

        if (signature.Decision == SignatureDecision.Signed)
        {
            return """
<span class="signature-recorded">
    Signed
</span>
""";
        }

        return string.Empty;
    }

    private static string BuildDateContent(
        ContractSignature signature)
    {
        if (signature.Decision == SignatureDecision.Signed &&
            signature.SignedAtUtc.HasValue)
        {
            return
                signature.SignedAtUtc
                    .Value
                    .ToLocalTime()
                    .ToString("dd/MM/yyyy");
        }

        return string.Empty;
    }

    private static string GetRoleCssClass(
        SignerRole role)
    {
        return role switch
        {
            SignerRole.Lecturer =>
                "lecturer-signature-line",

            SignerRole.Dean =>
                "dean-signature-line",

            SignerRole.HROfficer =>
                "hr-signature-line",

            SignerRole.DVCAR =>
                "dvcar-signature-line",

            SignerRole.ViceChancellor =>
                "vc-signature-line",

            _ =>
                "unknown-signature-line"
        };
    }

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

    private static string GetAuthorizedSignerName(
        ContractSignature signature,
        string? lecturerName)
    {
        if (signature.SignerRole ==
            SignerRole.Lecturer)
        {
            if (!string.IsNullOrWhiteSpace(
                    signature.SignedByLecturer?.UserName))
            {
                return signature
                    .SignedByLecturer!
                    .UserName;
            }

            if (!string.IsNullOrWhiteSpace(
                    lecturerName))
            {
                return lecturerName;
            }

            return "Lecturer";
        }

        return signature.SignerRole switch
        {
            SignerRole.Dean =>
                "Dean of Faculty: Prof. NYESHEJA M. Enan",

            SignerRole.HROfficer =>
                "Human Resource Officer Mr. NTAKIRUTIMANA Elison",

            SignerRole.DVCAR =>
                "DVCAR Prof. HAKIZIMANA Emmanuel",

            SignerRole.ViceChancellor =>
                "Vice Chancellor Prof. NGAMIJE Jean",

            _ =>
                GetSignerDisplayName(
                    signature.SignerRole)
        };
    }

    public sealed class ContractDetail
    {
        public int Id { get; init; }

        public string Reference { get; init; } = string.Empty;

        public string LecturerName { get; init; } = string.Empty;

        public string CourseTitle { get; init; } = string.Empty;

        public string AcademicYear { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public ContractStatus Status { get; init; }

        public List<SignatureStepRow> Steps { get; init; } = new();
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