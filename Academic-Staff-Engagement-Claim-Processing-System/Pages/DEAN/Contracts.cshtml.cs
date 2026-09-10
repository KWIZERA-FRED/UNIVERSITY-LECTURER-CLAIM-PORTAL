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
    public class ContractsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ContractSigningService _signingService;

        public ContractsModel(
            ApplicationDbContext context,
            ContractSigningService signingService)
        {
            _context = context;
            _signingService = signingService;
        }

        // ============================================================
        // CONTRACTS
        // ============================================================

        public List<ContractRow> Contracts { get; set; } = new();

        public ContractReviewDto? SelectedContract { get; set; }

        public List<SignatureStepRow> SelectedSignatureSteps { get; set; } =
            new();

        // ============================================================
        // BOUND PROPERTIES
        // ============================================================

        [BindProperty(SupportsGet = true)]
        public int? ContractId { get; set; }

        [BindProperty]
        public string? DeclineReason { get; set; }

        // ============================================================
        // MESSAGES
        // ============================================================

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }

        // ============================================================
        // CONTRACT ROW
        // ============================================================

        public class ContractRow
        {
            public int ContractId { get; set; }

            public string LecturerName { get; set; } =
                string.Empty;

            public string CourseTitle { get; set; } =
                string.Empty;

            public string Department { get; set; } =
                string.Empty;

            public string Version { get; set; } =
                string.Empty;

            public ContractStatus Status { get; set; }

            public string CurrentStage { get; set; } =
                string.Empty;

            public string CurrentStageCss { get; set; } =
                string.Empty;

            public DateTime CreatedAtUtc { get; set; }

            public bool IsAwaitingDean { get; set; }

            public bool IsDeclined { get; set; }

            public bool IsCompleted { get; set; }
        }

        // ============================================================
        // SIGNATURE STEP
        // ============================================================

        public class SignatureStepRow
        {
            public int SignatureId { get; set; }

            public int SequenceOrder { get; set; }

            public SignerRole SignerRole { get; set; }

            public string RoleName { get; set; } =
                string.Empty;

            public SignatureDecision Decision { get; set; }

            public string DecisionName { get; set; } =
                string.Empty;

            public string DecisionCss { get; set; } =
                string.Empty;

            public DateTime? SignedAtUtc { get; set; }

            public string? Comments { get; set; }

            // ========================================================
            // ACTUAL SIGNATURE IMAGE
            // ========================================================

            public string? SignatureFilePath { get; set; }

            public bool IsCurrent { get; set; }

            public bool IsSigned { get; set; }

            public bool IsPending { get; set; }

            public bool IsDeclined { get; set; }
        }

        // ============================================================
        // GET
        // ============================================================

        public async Task OnGetAsync()
        {
            await LoadContractsAsync();

            if (ContractId.HasValue)
            {
                await LoadSelectedContractAsync(
                    ContractId.Value);
            }
        }

        // ============================================================
        // SIGN CONTRACT
        // ============================================================

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSignAsync()
        {
            if (!ContractId.HasValue)
                return RedirectToPage();

            var (
                actorId,
                actorUsername,
                actorRole,
                ipAddress) = GetActorContext();

            var result =
                await _signingService.SignAsync(
                    ContractId.Value,
                    SignerRole.Dean,
                    actorId,
                    actorUsername,
                    actorRole,
                    ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage
                    ?? "The contract could not be signed.";
            }
            else
            {
                SuccessMessage =
                    "Contract signed successfully.";
            }

            await LoadContractsAsync();

            await LoadSelectedContractAsync(
                ContractId.Value);

            return Page();
        }

        // ============================================================
        // DECLINE CONTRACT
        // ============================================================

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!ContractId.HasValue)
                return RedirectToPage();

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage =
                    "Please provide a reason for declining this contract.";

                await LoadContractsAsync();

                await LoadSelectedContractAsync(
                    ContractId.Value);

                return Page();
            }

            var (
                actorId,
                actorUsername,
                actorRole,
                ipAddress) = GetActorContext();

            var result =
                await _signingService.DeclineAsync(
                    ContractId.Value,
                    SignerRole.Dean,
                    actorId,
                    DeclineReason.Trim(),
                    actorUsername,
                    actorRole,
                    ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage
                    ?? "The contract could not be declined.";
            }
            else
            {
                SuccessMessage =
                    "Contract declined successfully.";
            }

            await LoadContractsAsync();

            await LoadSelectedContractAsync(
                ContractId.Value);

            return Page();
        }

        // ============================================================
        // LOAD CONTRACT LIST
        // ============================================================

        private async Task LoadContractsAsync()
        {
            var contracts =
                await _context.Contracts
                    .AsNoTracking()
                    .Include(c => c.Lecturer)
                    .Include(c => c.CourseAssignment)
                        .ThenInclude(ca => ca!.Course)
                    .OrderByDescending(
                        c => c.CreatedAtUtc)
                    .ToListAsync();

            var contractIds =
                contracts
                    .Select(c => c.Id)
                    .ToList();

            var signatures =
                await _context.ContractSignatures
                    .AsNoTracking()
                    .Where(cs =>
                        contractIds.Contains(cs.ContractId))
                    .OrderBy(cs => cs.ContractId)
                    .ThenBy(cs => cs.SequenceOrder)
                    .ToListAsync();

            Contracts =
                contracts
                    .Select(contract =>
                    {
                        var contractSignatures =
                            signatures
                                .Where(cs =>
                                    cs.ContractId ==
                                    contract.Id)
                                .OrderBy(
                                    cs => cs.SequenceOrder)
                                .ToList();

                        var currentStep =
                            GetCurrentSignatureStep(
                                contractSignatures);

                        bool isDeclined =
                            contractSignatures.Any(
                                cs =>
                                    cs.Decision ==
                                    SignatureDecision.Declined);

                        bool isCompleted =
                            contractSignatures.Count > 0 &&
                            contractSignatures.All(
                                cs =>
                                    cs.Decision ==
                                    SignatureDecision.Signed);

                        bool isAwaitingDean =
                            contractSignatures.Any(
                                cs =>
                                    cs.SignerRole ==
                                        SignerRole.Dean &&
                                    cs.Decision ==
                                        SignatureDecision.Pending) &&
                            IsStepCurrentlyAvailable(
                                contractSignatures,
                                SignerRole.Dean);

                        string currentStage =
                            GetCurrentStage(
                                contractSignatures,
                                currentStep,
                                isDeclined,
                                isCompleted);

                        return new ContractRow
                        {
                            ContractId =
                                contract.Id,

                            LecturerName =
                                contract.Lecturer?.UserName
                                ?? "Unknown",

                            CourseTitle =
                                contract.CourseAssignment
                                    ?.Course?.Title
                                ?? "—",

                            Department =
                                contract.CourseAssignment
                                    ?.Course?.Department
                                ?? "—",

                            Version =
                                contract.Version,

                            Status =
                                contract.Status,

                            CurrentStage =
                                currentStage,

                            CurrentStageCss =
                                GetStageCss(
                                    currentStep,
                                    isDeclined,
                                    isCompleted),

                            CreatedAtUtc =
                                contract.CreatedAtUtc,

                            IsAwaitingDean =
                                isAwaitingDean,

                            IsDeclined =
                                isDeclined,

                            IsCompleted =
                                isCompleted
                        };
                    })
                    .ToList();
        }

        // ============================================================
        // LOAD SELECTED CONTRACT
        // ============================================================

        private async Task LoadSelectedContractAsync(
            int contractId)
        {
            SelectedContract =
                await _signingService
                    .GetContractForReviewAsync(
                        contractId,
                        SignerRole.Dean);

            if (SelectedContract == null)
            {
                ErrorMessage =
                    "That contract could not be found.";

                SelectedSignatureSteps =
                    new();

                return;
            }

            // ========================================================
            // LOAD SIGNATURE TIMELINE
            // ========================================================

            SelectedSignatureSteps =
                await LoadSignatureTimelineAsync(
                    contractId);

            // ========================================================
            // BUILD LIVE CONTRACT CONTENT
            //
            // Contract.Content remains unchanged in the database.
            // The signature table is replaced only for display.
            // ========================================================

            var signatures =
                await _context.ContractSignatures
                    .AsNoTracking()
                    .Where(cs =>
                        cs.ContractId == contractId)
                    .OrderBy(
                        cs => cs.SequenceOrder)
                    .ToListAsync();

            SelectedContract.ContractContent =
                BuildLiveContractContent(
                    SelectedContract.ContractContent,
                    signatures);
        }

        // ============================================================
        // SIGNATURE TIMELINE
        // ============================================================

        private async Task<List<SignatureStepRow>>
            LoadSignatureTimelineAsync(
                int contractId)
        {
            var signatures =
                await _context.ContractSignatures
                    .AsNoTracking()
                    .Where(cs =>
                        cs.ContractId ==
                        contractId)
                    .OrderBy(
                        cs => cs.SequenceOrder)
                    .ToListAsync();

            bool hasDeclined =
                signatures.Any(
                    cs =>
                        cs.Decision ==
                        SignatureDecision.Declined);

            bool allSigned =
                signatures.Count > 0 &&
                signatures.All(
                    cs =>
                        cs.Decision ==
                        SignatureDecision.Signed);

            ContractSignature? currentStep = null;

            if (!hasDeclined &&
                !allSigned)
            {
                currentStep =
                    signatures.FirstOrDefault(
                        cs =>
                            cs.Decision ==
                            SignatureDecision.Pending);
            }
            else if (hasDeclined)
            {
                currentStep =
                    signatures.FirstOrDefault(
                        cs =>
                            cs.Decision ==
                            SignatureDecision.Declined);
            }

            return signatures
                .Select(signature =>
                {
                    bool isSigned =
                        signature.Decision ==
                        SignatureDecision.Signed;

                    bool isPending =
                        signature.Decision ==
                        SignatureDecision.Pending;

                    bool isDeclined =
                        signature.Decision ==
                        SignatureDecision.Declined;

                    return new SignatureStepRow
                    {
                        SignatureId =
                            signature.Id,

                        SequenceOrder =
                            signature.SequenceOrder,

                        SignerRole =
                            signature.SignerRole,

                        RoleName =
                            FormatSignerRole(
                                signature.SignerRole),

                        Decision =
                            signature.Decision,

                        DecisionName =
                            FormatDecision(
                                signature.Decision),

                        DecisionCss =
                            GetDecisionCss(
                                signature.Decision),

                        SignedAtUtc =
                            signature.SignedAtUtc,

                        Comments =
                            signature.Comments,

                        SignatureFilePath =
                            signature.SignatureFilePath,

                        IsCurrent =
                            currentStep != null &&
                            signature.Id ==
                            currentStep.Id,

                        IsSigned =
                            isSigned,

                        IsPending =
                            isPending,

                        IsDeclined =
                            isDeclined
                    };
                })
                .ToList();
        }

        // ============================================================
        // LIVE CONTRACT CONTENT
        // ============================================================
        //
        // This does NOT modify Contract.Content in the database.
        //
        // It takes the original contract HTML and replaces the
        // signature-table section with the current live signature data.
        // ============================================================

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
                BuildLiveSignatureTable(
                    signatures);

            return Regex.Replace(
                originalContent,
                pattern,
                liveSignatureTable,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);
        }

        // ============================================================
        // BUILD LIVE SIGNATURE TABLE
        // ============================================================

        private static string BuildLiveSignatureTable(
            IEnumerable<ContractSignature> signatures)
        {
            var orderedSignatures =
                signatures
                    .OrderBy(s => s.SequenceOrder)
                    .ToList();

            var rows =
                string.Join(
                    Environment.NewLine,
                    orderedSignatures.Select(
                        BuildSignatureRow));

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

        // ============================================================
        // BUILD SIGNATURE ROW
        // ============================================================

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
                WebUtility.HtmlEncode(
                    roleName);

            var safeSignerName =
                WebUtility.HtmlEncode(
                    signerName);

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
                BuildSignatureCell(
                    signature);

            var dateHtml =
                BuildDateCell(
                    signature);

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

        // ============================================================
        // SIGNATURE CELL
        // ============================================================

        private static string BuildSignatureCell(
            ContractSignature signature)
        {
            // ========================================================
            // SIGNED
            // ========================================================

            if (signature.Decision ==
                SignatureDecision.Signed)
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

                // Database says signed but no image exists.
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

            // ========================================================
            // DECLINED
            // ========================================================

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

            // ========================================================
            // PENDING
            // ========================================================

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

        // ============================================================
        // DATE CELL
        // ============================================================

        private static string BuildDateCell(
            ContractSignature signature)
        {
            if (signature.Decision ==
                    SignatureDecision.Signed &&
                signature.SignedAtUtc.HasValue)
            {
                return $"""
<span class="signature-date">

    {signature.SignedAtUtc.Value
        .ToLocalTime():dd MMMM yyyy}

    <br />

    {signature.SignedAtUtc.Value
        .ToLocalTime():HH:mm}

</span>
""";
            }

            if (signature.Decision ==
                    SignatureDecision.Declined &&
                signature.SignedAtUtc.HasValue)
            {
                return $"""
<span class="signature-date">

    Declined

    <br />

    {signature.SignedAtUtc.Value
        .ToLocalTime():dd MMMM yyyy}

</span>
""";
            }

            return """
<span class="signature-placeholder">
    Pending
</span>
""";
        }

        // ============================================================
        // SIGNER ROLE DISPLAY
        // ============================================================

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

        // ============================================================
        // AUTHORIZED SIGNATORY NAME
        // ============================================================

        private static string GetAuthorizedSignerName(
            ContractSignature signature)
        {
            // ========================================================
            // LECTURER
            // ========================================================

            if (signature.SignerRole ==
                SignerRole.Lecturer)
            {
                return
                    signature.SignedByLecturer?.UserName
                    ?? "Lecturer";
            }

            // ========================================================
            // MANAGEMENT SIGNATORIES
            // ========================================================

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

        // ============================================================
        // CURRENT SIGNATURE STEP
        // ============================================================

        private static ContractSignature?
            GetCurrentSignatureStep(
                List<ContractSignature> signatures)
        {
            if (signatures.Count == 0)
                return null;

            var declinedStep =
                signatures.FirstOrDefault(
                    cs =>
                        cs.Decision ==
                        SignatureDecision.Declined);

            if (declinedStep != null)
                return declinedStep;

            var pendingStep =
                signatures.FirstOrDefault(
                    cs =>
                        cs.Decision ==
                        SignatureDecision.Pending);

            if (pendingStep != null)
                return pendingStep;

            return signatures
                .OrderByDescending(
                    cs => cs.SequenceOrder)
                .FirstOrDefault();
        }

        // ============================================================
        // CURRENT STAGE
        // ============================================================

        private static string GetCurrentStage(
            List<ContractSignature> signatures,
            ContractSignature? currentStep,
            bool isDeclined,
            bool isCompleted)
        {
            if (signatures.Count == 0)
                return "Not Started";

            if (isDeclined &&
                currentStep != null)
            {
                return
                    $"Declined by " +
                    $"{FormatSignerRole(currentStep.SignerRole)}";
            }

            if (isCompleted)
                return "Completed";

            var pendingStep =
                signatures.FirstOrDefault(
                    cs =>
                        cs.Decision ==
                        SignatureDecision.Pending);

            if (pendingStep != null)
            {
                return
                    $"Awaiting " +
                    $"{FormatSignerRole(pendingStep.SignerRole)}";
            }

            return "In Progress";
        }

        // ============================================================
        // CHECK STEP AVAILABILITY
        // ============================================================

        private static bool IsStepCurrentlyAvailable(
            List<ContractSignature> signatures,
            SignerRole role)
        {
            var step =
                signatures.FirstOrDefault(
                    cs =>
                        cs.SignerRole ==
                        role);

            if (step == null ||
                step.Decision !=
                    SignatureDecision.Pending)
            {
                return false;
            }

            return signatures
                .Where(cs =>
                    cs.SequenceOrder <
                    step.SequenceOrder)
                .All(cs =>
                    cs.Decision ==
                    SignatureDecision.Signed);
        }

        // ============================================================
        // FORMAT SIGNER ROLE
        // ============================================================

        private static string FormatSignerRole(
            SignerRole role)
        {
            return role switch
            {
                SignerRole.Lecturer =>
                    "Lecturer",

                SignerRole.Dean =>
                    "Dean",

                SignerRole.HROfficer =>
                    "HR Officer",

                SignerRole.DVCAR =>
                    "DVCAR",

                SignerRole.ViceChancellor =>
                    "Vice Chancellor",

                _ =>
                    role.ToString()
            };
        }

        // ============================================================
        // FORMAT DECISION
        // ============================================================

        private static string FormatDecision(
            SignatureDecision decision)
        {
            return decision switch
            {
                SignatureDecision.Signed =>
                    "Signed",

                SignatureDecision.Pending =>
                    "Pending",

                SignatureDecision.Declined =>
                    "Declined",

                _ =>
                    decision.ToString()
            };
        }

        // ============================================================
        // DECISION CSS
        // ============================================================

        private static string GetDecisionCss(
            SignatureDecision decision)
        {
            return decision switch
            {
                SignatureDecision.Signed =>
                    "signed",

                SignatureDecision.Pending =>
                    "pending",

                SignatureDecision.Declined =>
                    "declined",

                _ =>
                    "pending"
            };
        }

        // ============================================================
        // STAGE CSS
        // ============================================================

        private static string GetStageCss(
            ContractSignature? currentStep,
            bool isDeclined,
            bool isCompleted)
        {
            if (isDeclined)
                return "declined";

            if (isCompleted)
                return "completed";

            if (currentStep == null)
                return "not-started";

            return "pending";
        }

        // ============================================================
        // ACTOR CONTEXT
        // ============================================================

        private (
            int actorId,
            string actorUsername,
            string actorRole,
            string? ipAddress)
            GetActorContext()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int actorId);

            string actorUsername =
                User.Identity?.Name ??
                "Unknown";

            string actorRole =
                User.FindFirst(
                    ClaimTypes.Role)?.Value ??
                "Unknown";

            string? ipAddress =
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString();

            return (
                actorId,
                actorUsername,
                actorRole,
                ipAddress);
        }
    }
}