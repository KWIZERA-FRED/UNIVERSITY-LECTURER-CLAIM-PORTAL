using System.Net;
using System.Security.Claims;
using System.Text;
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
                .Include(cs => cs.SignedByLecturer)
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

        private static string BuildLiveContractContent(
    string originalContent,
    IReadOnlyCollection<ContractSignature> signatures)
        {
            if (string.IsNullOrWhiteSpace(originalContent)) return string.Empty;
            if (signatures.Count == 0) return originalContent;

            var liveSignatureSection = BuildLiveSignatureSection(signatures);

            var paperSignaturesPattern =
                @"<div\s+class\s*=\s*[""']paper-signatures[""'][^>]*>.*?</div>";

            var updatedContent = Regex.Replace(
                originalContent, paperSignaturesPattern, liveSignatureSection,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var oldSignatureTablePattern =
                @"<table\s+class\s*=\s*[""']signature-table[^""']*[""'][^>]*>.*?</table>";

            updatedContent = Regex.Replace(
                updatedContent, oldSignatureTablePattern, liveSignatureSection,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return updatedContent;
        }

        private static string BuildLiveSignatureSection(
            IEnumerable<ContractSignature> signatures)
        {
            var ordered = signatures.OrderBy(s => s.SequenceOrder).ToList();

            var lecturer = ordered.FirstOrDefault(s => s.SignerRole == SignerRole.Lecturer);
            var dean = ordered.FirstOrDefault(s => s.SignerRole == SignerRole.Dean);
            var hr = ordered.FirstOrDefault(s => s.SignerRole == SignerRole.HROfficer);
            var dvcar = ordered.FirstOrDefault(s => s.SignerRole == SignerRole.DVCAR);
            var viceChancellor = ordered.FirstOrDefault(s => s.SignerRole == SignerRole.ViceChancellor);

            var html = new StringBuilder();

            html.AppendLine("<div class=\"paper-signatures\">");

            html.AppendLine(BuildPaperSignatureLine(lecturer));
            html.AppendLine(BuildPaperSignatureLine(dean));
            html.AppendLine(BuildPaperSignatureLine(hr));
            html.AppendLine(BuildPaperSignatureLine(dvcar));
            html.AppendLine(BuildPaperSignatureLine(viceChancellor));

            html.AppendLine("</div>");

            return html.ToString();
        }

        private static string BuildPaperSignatureLine(ContractSignature? signature)
        {
            var safeName = WebUtility.HtmlEncode(GetAuthorizedSignerName(signature));
            var signatureContent = BuildSignatureImage(signature);
            var dateContent = BuildSignatureDate(signature);

            var roleClass = signature?.SignerRole switch
            {
                SignerRole.Lecturer => "lecturer-signature-line",
                SignerRole.Dean => "dean-signature-line",
                SignerRole.HROfficer => "hr-signature-line",
                SignerRole.DVCAR => "dvcar-signature-line",
                SignerRole.ViceChancellor => "vc-signature-line",
                _ => string.Empty
            };

            return $"""
<div class="paper-signature-line {roleClass}">

    <span class="paper-signature-name">
        {safeName}
    </span>

    <span class="paper-signature-field paper-signature-area">
        Signature........................
        {signatureContent}
    </span>

    <span class="paper-signature-field paper-date-area">
        Date.................
        {dateContent}
    </span>

</div>
""";
        }

        private static string BuildSignatureImage(ContractSignature? signature)
        {
            if (signature?.Decision != SignatureDecision.Signed) return string.Empty;
            if (string.IsNullOrWhiteSpace(signature.SignatureFilePath)) return string.Empty;

            var safePath = WebUtility.HtmlEncode(signature.SignatureFilePath);

            return $"""
<span class="paper-signature-image-wrapper">
    <img
        src="{safePath}"
        alt="Electronic signature"
        class="contract-signature-image" />
</span>
""";
        }

        private static string BuildSignatureDate(ContractSignature? signature)
        {
            if (signature?.Decision != SignatureDecision.Signed) return string.Empty;
            if (!signature.SignedAtUtc.HasValue) return string.Empty;

            return WebUtility.HtmlEncode(
                signature.SignedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy"));
        }

        private static string GetAuthorizedSignerName(ContractSignature? signature)
        {
            if (signature?.SignerRole == SignerRole.Lecturer)
            {
                var name = signature.SignedByLecturer?.UserName;

                return string.IsNullOrWhiteSpace(name)
                    ? "Lecturer’s Name"
                    : $"Lecturer’s Name: {name}";
            }

            return signature?.SignerRole switch
            {
                SignerRole.Dean => "Dean of Faculty: Prof. NYESHEJA M. Enan",
                SignerRole.HROfficer => "Human Resource Officer Mr. NTAKIRUTIMANA Elison",
                SignerRole.DVCAR => "DVCAR Prof. HAKIZIMANA Emmanuel",
                SignerRole.ViceChancellor => "Vice Chancellor Prof. NGAMIJE Jean",
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