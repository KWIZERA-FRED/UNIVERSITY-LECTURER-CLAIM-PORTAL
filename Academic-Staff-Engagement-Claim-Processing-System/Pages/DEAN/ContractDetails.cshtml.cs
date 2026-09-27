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

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.DEAN
{
    [Authorize(Roles = "Dean")]
    public class ContractDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ContractSigningService _signingService;

        public ContractDetailsModel(
            ApplicationDbContext context,
            ContractSigningService signingService)
        {
            _context = context;
            _signingService = signingService;
        }

        public ContractReviewDto? SelectedContract { get; set; }

        public List<SignatureStepRow> SelectedSignatureSteps { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? ContractId { get; set; }

        [BindProperty]
        public string? DeclineReason { get; set; }

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }


        public class SignatureStepRow
        {
            public int SignatureId { get; set; }
            public int SequenceOrder { get; set; }
            public SignerRole SignerRole { get; set; }
            public string RoleName { get; set; } = string.Empty;
            public SignatureDecision Decision { get; set; }
            public string DecisionName { get; set; } = string.Empty;
            public string DecisionCss { get; set; } = string.Empty;
            public DateTime? SignedAtUtc { get; set; }
            public string? Comments { get; set; }
            public string? SignatureFilePath { get; set; }
            public bool IsCurrent { get; set; }
            public bool IsSigned { get; set; }
            public bool IsPending { get; set; }
            public bool IsDeclined { get; set; }
        }


        public async Task<IActionResult> OnGetAsync()
        {
            SuccessMessage = TempData["SuccessMessage"] as string;
            ErrorMessage = TempData["ErrorMessage"] as string;

            if (!ContractId.HasValue)
            {
                ErrorMessage = "No contract was specified.";
                return Page();
            }

            await LoadSelectedContractAsync(ContractId.Value);

            if (SelectedContract == null &&
                string.IsNullOrWhiteSpace(ErrorMessage))
            {
                ErrorMessage = "That contract could not be found.";
            }

            return Page();
        }


        public async Task<IActionResult> OnPostSignAsync()
        {
            if (!ContractId.HasValue)
                return RedirectToPage("/DEAN/Contracts");

            var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

            var result = await _signingService.SignAsync(
                ContractId.Value,
                SignerRole.Dean,
                actorId,
                actorUsername,
                actorRole,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "The contract could not be signed.";

                await LoadSelectedContractAsync(ContractId.Value);

                return Page();
            }

            TempData["SuccessMessage"] = "Contract signed successfully.";

            return RedirectToPage("/DEAN/Contracts");
        }


        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!ContractId.HasValue)
                return RedirectToPage("/DEAN/Contracts");

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage = "Please provide a reason for declining this contract.";

                await LoadSelectedContractAsync(ContractId.Value);

                return Page();
            }

            var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

            var result = await _signingService.DeclineAsync(
                ContractId.Value,
                SignerRole.Dean,
                actorId,
                DeclineReason.Trim(),
                actorUsername,
                actorRole,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "The contract could not be declined.";

                await LoadSelectedContractAsync(ContractId.Value);

                return Page();
            }

            TempData["SuccessMessage"] = "Contract declined successfully.";

            return RedirectToPage("/DEAN/Contracts");
        }


        private async Task LoadSelectedContractAsync(int contractId)
        {
            SelectedContract = await _signingService
                .GetContractForReviewAsync(contractId, SignerRole.Dean);

            if (SelectedContract == null)
            {
                SelectedSignatureSteps = new();
                return;
            }

            SelectedSignatureSteps = await LoadSignatureTimelineAsync(contractId);

            var signatures = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs => cs.ContractId == contractId)
                .OrderBy(cs => cs.SequenceOrder)
                .ToListAsync();

            SelectedContract.ContractContent = BuildLiveContractContent(
                SelectedContract.ContractContent, signatures);
        }


        private async Task<List<SignatureStepRow>> LoadSignatureTimelineAsync(
            int contractId)
        {
            var signatures = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs => cs.ContractId == contractId)
                .OrderBy(cs => cs.SequenceOrder)
                .ToListAsync();

            bool hasDeclined = signatures.Any(cs =>
                cs.Decision == SignatureDecision.Declined);

            bool allSigned = signatures.Count > 0 &&
                signatures.All(cs => cs.Decision == SignatureDecision.Signed);

            ContractSignature? currentStep = null;

            if (!hasDeclined && !allSigned)
            {
                currentStep = signatures.FirstOrDefault(cs =>
                    cs.Decision == SignatureDecision.Pending);
            }
            else if (hasDeclined)
            {
                currentStep = signatures.FirstOrDefault(cs =>
                    cs.Decision == SignatureDecision.Declined);
            }

            return signatures.Select(signature =>
            {
                bool isSigned = signature.Decision == SignatureDecision.Signed;
                bool isPending = signature.Decision == SignatureDecision.Pending;
                bool isDeclined = signature.Decision == SignatureDecision.Declined;

                return new SignatureStepRow
                {
                    SignatureId = signature.Id,
                    SequenceOrder = signature.SequenceOrder,
                    SignerRole = signature.SignerRole,
                    RoleName = FormatSignerRole(signature.SignerRole),
                    Decision = signature.Decision,
                    DecisionName = FormatDecision(signature.Decision),
                    DecisionCss = GetDecisionCss(signature.Decision),
                    SignedAtUtc = signature.SignedAtUtc,
                    Comments = signature.Comments,
                    SignatureFilePath = signature.SignatureFilePath,
                    IsCurrent = currentStep != null && signature.Id == currentStep.Id,
                    IsSigned = isSigned,
                    IsPending = isPending,
                    IsDeclined = isDeclined
                };
            }).ToList();
        }


        // ============================================================
        // LIVE CONTRACT CONTENT (unchanged from your original)
        // ============================================================

        private static string BuildLiveContractContent(
            string originalContent,
            IReadOnlyCollection<ContractSignature> signatures)
        {
            if (string.IsNullOrWhiteSpace(originalContent)) return string.Empty;
            if (signatures.Count == 0) return originalContent;

            var pattern =
                @"<table\s+class\s*=\s*[""']signature-table[""'][^>]*>.*?</table>";

            var liveSignatureTable = BuildLiveSignatureTable(signatures);

            return Regex.Replace(
                originalContent, pattern, liveSignatureTable,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        private static string BuildLiveSignatureTable(
            IEnumerable<ContractSignature> signatures)
        {
            var ordered = signatures.OrderBy(s => s.SequenceOrder).ToList();

            var rows = string.Join(
                Environment.NewLine,
                ordered.Select(BuildSignatureRow));

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

        private static string BuildSignatureRow(ContractSignature signature)
        {
            var roleName = GetSignerDisplayName(signature.SignerRole);
            var signerName = GetAuthorizedSignerName(signature);

            var safeRole = WebUtility.HtmlEncode(roleName);
            var safeSignerName = WebUtility.HtmlEncode(signerName);

            var rowClass = signature.Decision switch
            {
                SignatureDecision.Signed => "signature-row-signed",
                SignatureDecision.Declined => "signature-row-declined",
                _ => string.Empty
            };

            var signatureHtml = BuildSignatureCell(signature);
            var dateHtml = BuildDateCell(signature);

            return $"""
<tr class="{rowClass}">
    <td><strong>{safeRole}</strong></td>
    <td>{safeSignerName}</td>
    <td class="signature-cell">{signatureHtml}</td>
    <td>{dateHtml}</td>
</tr>
""";
        }

        private static string BuildSignatureCell(ContractSignature signature)
        {
            if (signature.Decision == SignatureDecision.Signed)
            {
                if (!string.IsNullOrWhiteSpace(signature.SignatureFilePath))
                {
                    var safePath = WebUtility.HtmlEncode(signature.SignatureFilePath);

                    return $"""
<div class="signature-image-wrapper">
    <img src="{safePath}" alt="Electronic signature" class="contract-signature-image" />
</div>

<span class="signature-status signed">Signed</span>
""";
                }

                return """
<span class="signature-missing">Signature recorded</span>
<br />
<span class="signature-status signed">Signed</span>
""";
            }

            if (signature.Decision == SignatureDecision.Declined)
            {
                var reason = string.IsNullOrWhiteSpace(signature.Comments)
                    ? "No reason provided."
                    : signature.Comments.Trim();

                return $"""
<span class="signature-status declined">Declined</span>
<br />
<span class="signature-missing">{WebUtility.HtmlEncode(reason)}</span>
""";
            }

            return """
<span class="signature-placeholder">Pending electronic signature</span>
<br />
<span class="signature-status pending">Pending</span>
""";
        }

        private static string BuildDateCell(ContractSignature signature)
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
<span class="signature-placeholder">Pending</span>
""";
        }

        private static string GetSignerDisplayName(SignerRole role) => role switch
        {
            SignerRole.Lecturer => "Lecturer",
            SignerRole.Dean => "Dean of Faculty",
            SignerRole.HROfficer => "Human Resource Officer",
            SignerRole.DVCAR => "DVCAR",
            SignerRole.ViceChancellor => "Vice Chancellor",
            _ => role.ToString()
        };

        private static string GetAuthorizedSignerName(ContractSignature signature)
        {
            if (signature.SignerRole == SignerRole.Lecturer)
                return signature.SignedByLecturer?.UserName ?? "Lecturer";

            return signature.SignerRole switch
            {
                SignerRole.Dean => "Prof. NYESHEJA M. Enan",
                SignerRole.HROfficer => "Mr. NTAKIRUTIMANA Elison",
                SignerRole.DVCAR => "Prof. HAKIZIMANA Emmanuel",
                SignerRole.ViceChancellor => "Prof. NGAMIJE Jean",
                _ => "Authorized Signatory"
            };
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

        private static string FormatDecision(SignatureDecision decision) => decision switch
        {
            SignatureDecision.Signed => "Signed",
            SignatureDecision.Pending => "Pending",
            SignatureDecision.Declined => "Declined",
            _ => decision.ToString()
        };

        private static string GetDecisionCss(SignatureDecision decision) => decision switch
        {
            SignatureDecision.Signed => "signed",
            SignatureDecision.Pending => "pending",
            SignatureDecision.Declined => "declined",
            _ => "pending"
        };

        private (int actorId, string actorUsername, string actorRole, string? ipAddress)
            GetActorContext()
        {
            int.TryParse(User.FindFirst("UserId")?.Value, out int actorId);
            string actorUsername = User.Identity?.Name ?? "Unknown";
            string actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            return (actorId, actorUsername, actorRole, ipAddress);
        }
    }
}