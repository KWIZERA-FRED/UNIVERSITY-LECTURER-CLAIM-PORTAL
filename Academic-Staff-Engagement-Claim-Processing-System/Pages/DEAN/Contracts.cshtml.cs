using System.Security.Claims;
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
        // CONTRACT LIST
        // ============================================================

        public List<ContractRow> Contracts { get; set; } = new();

        // ============================================================
        // SELECTED CONTRACT
        // ============================================================

        public ContractReviewDto? SelectedContract { get; set; }

        public List<SignatureStepRow> SelectedSignatureSteps { get; set; } = new();

        // ============================================================
        // BINDINGS
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
        // CONTRACT LIST ROW
        // ============================================================

        public class ContractRow
        {
            public int ContractId { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public string Department { get; set; } = string.Empty;

            public string Version { get; set; } = string.Empty;

            public ContractStatus Status { get; set; }

            public string CurrentStage { get; set; } = string.Empty;

            public string CurrentStageCss { get; set; } = string.Empty;

            public DateTime CreatedAtUtc { get; set; }

            public bool IsAwaitingDean { get; set; }

            public bool IsDeclined { get; set; }

            public bool IsCompleted { get; set; }
        }

        // ============================================================
        // SIGNATURE TIMELINE ROW
        // ============================================================

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
                await LoadSelectedContractAsync(ContractId.Value);
            }
        }

        // ============================================================
        // SIGN
        // ============================================================

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSignAsync()
        {
            if (!ContractId.HasValue)
            {
                return RedirectToPage();
            }

            var (actorId, actorUsername, actorRole, ipAddress) =
                GetActorContext();

            var result = await _signingService.SignAsync(
                ContractId.Value,
                SignerRole.Dean,
                actorId,
                actorUsername,
                actorRole,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage =
                    result.ErrorMessage ??
                    "The contract could not be signed.";
            }
            else
            {
                SuccessMessage = "Contract signed successfully.";
            }

            await LoadContractsAsync();
            await LoadSelectedContractAsync(ContractId.Value);

            return Page();
        }

        // ============================================================
        // DECLINE
        // ============================================================

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!ContractId.HasValue)
            {
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage =
                    "Please provide a reason for declining this contract.";

                await LoadContractsAsync();
                await LoadSelectedContractAsync(ContractId.Value);

                return Page();
            }

            var (actorId, actorUsername, actorRole, ipAddress) =
                GetActorContext();

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
                ErrorMessage =
                    result.ErrorMessage ??
                    "The contract could not be declined.";
            }
            else
            {
                SuccessMessage = "Contract declined successfully.";
            }

            await LoadContractsAsync();
            await LoadSelectedContractAsync(ContractId.Value);

            return Page();
        }

        // ============================================================
        // LOAD ALL CONTRACTS
        // ============================================================

        private async Task LoadContractsAsync()
        {
            var contracts = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Lecturer)
                .Include(c => c.CourseAssignment)
                    .ThenInclude(ca => ca!.Course)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();

            var contractIds = contracts
                .Select(c => c.Id)
                .ToList();

            var signatures = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs => contractIds.Contains(cs.ContractId))
                .OrderBy(cs => cs.ContractId)
                .ThenBy(cs => cs.SequenceOrder)
                .ToListAsync();

            Contracts = contracts
                .Select(contract =>
                {
                    var contractSignatures = signatures
                        .Where(cs => cs.ContractId == contract.Id)
                        .OrderBy(cs => cs.SequenceOrder)
                        .ToList();

                    var currentStep =
                        GetCurrentSignatureStep(contractSignatures);

                    bool isDeclined =
                        contractSignatures.Any(cs =>
                            cs.Decision == SignatureDecision.Declined);

                    bool isCompleted =
                        contractSignatures.Count > 0 &&
                        contractSignatures.All(cs =>
                            cs.Decision == SignatureDecision.Signed);

                    bool isAwaitingDean =
                        contractSignatures.Any(cs =>
                            cs.SignerRole == SignerRole.Dean &&
                            cs.Decision == SignatureDecision.Pending) &&
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
                        ContractId = contract.Id,

                        LecturerName =
                            contract.Lecturer?.UserName ?? "Unknown",

                        CourseTitle =
                            contract.CourseAssignment?.Course?.Title
                            ?? "—",

                        Department =
                            contract.CourseAssignment?.Course?.Department
                            ?? "—",

                        Version = contract.Version,

                        Status = contract.Status,

                        CurrentStage = currentStage,

                        CurrentStageCss =
                            GetStageCss(
                                currentStep,
                                isDeclined,
                                isCompleted),

                        CreatedAtUtc = contract.CreatedAtUtc,

                        IsAwaitingDean = isAwaitingDean,

                        IsDeclined = isDeclined,

                        IsCompleted = isCompleted
                    };
                })
                .ToList();
        }

        // ============================================================
        // LOAD SELECTED CONTRACT
        // ============================================================

        private async Task LoadSelectedContractAsync(int contractId)
        {
            SelectedContract =
                await _signingService.GetContractForReviewAsync(
                    contractId,
                    SignerRole.Dean);

            if (SelectedContract == null)
            {
                ErrorMessage =
                    "That contract could not be found.";

                SelectedSignatureSteps = new();

                return;
            }

            SelectedSignatureSteps =
                await LoadSignatureTimelineAsync(contractId);
        }

        // ============================================================
        // LOAD SIGNATURE TIMELINE
        // ============================================================

        private async Task<List<SignatureStepRow>>
            LoadSignatureTimelineAsync(int contractId)
        {
            var signatures = await _context.ContractSignatures
                .AsNoTracking()
                .Where(cs => cs.ContractId == contractId)
                .OrderBy(cs => cs.SequenceOrder)
                .ToListAsync();

            bool hasDeclined =
                signatures.Any(cs =>
                    cs.Decision == SignatureDecision.Declined);

            bool allSigned =
                signatures.Count > 0 &&
                signatures.All(cs =>
                    cs.Decision == SignatureDecision.Signed);

            ContractSignature? currentStep = null;

            if (!hasDeclined && !allSigned)
            {
                currentStep = signatures
                    .FirstOrDefault(cs =>
                        cs.Decision == SignatureDecision.Pending);
            }
            else if (hasDeclined)
            {
                currentStep = signatures
                    .FirstOrDefault(cs =>
                        cs.Decision == SignatureDecision.Declined);
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
                        SignatureId = signature.Id,

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

                        IsCurrent =
                            currentStep != null &&
                            signature.Id == currentStep.Id,

                        IsSigned = isSigned,

                        IsPending = isPending,

                        IsDeclined = isDeclined
                    };
                })
                .ToList();
        }

        // ============================================================
        // CURRENT SIGNATURE STEP
        // ============================================================

        private static ContractSignature?
            GetCurrentSignatureStep(
                List<ContractSignature> signatures)
        {
            if (signatures.Count == 0)
            {
                return null;
            }

            var declinedStep = signatures
                .FirstOrDefault(cs =>
                    cs.Decision == SignatureDecision.Declined);

            if (declinedStep != null)
            {
                return declinedStep;
            }

            var pendingStep = signatures
                .FirstOrDefault(cs =>
                    cs.Decision == SignatureDecision.Pending);

            if (pendingStep != null)
            {
                return pendingStep;
            }

            return signatures
                .OrderByDescending(cs => cs.SequenceOrder)
                .FirstOrDefault();
        }

        // ============================================================
        // CURRENT STAGE TEXT
        // ============================================================

        private static string GetCurrentStage(
            List<ContractSignature> signatures,
            ContractSignature? currentStep,
            bool isDeclined,
            bool isCompleted)
        {
            if (signatures.Count == 0)
            {
                return "Not Started";
            }

            if (isDeclined && currentStep != null)
            {
                return $"Declined by {FormatSignerRole(currentStep.SignerRole)}";
            }

            if (isCompleted)
            {
                return "Completed";
            }

            var pendingStep = signatures
                .FirstOrDefault(cs =>
                    cs.Decision == SignatureDecision.Pending);

            if (pendingStep != null)
            {
                return $"Awaiting {FormatSignerRole(pendingStep.SignerRole)}";
            }

            return "In Progress";
        }

        // ============================================================
        // CHECK WHETHER A ROLE'S STEP IS CURRENTLY AVAILABLE
        // ============================================================

        private static bool IsStepCurrentlyAvailable(
            List<ContractSignature> signatures,
            SignerRole role)
        {
            var step = signatures
                .FirstOrDefault(cs =>
                    cs.SignerRole == role);

            if (step == null ||
                step.Decision != SignatureDecision.Pending)
            {
                return false;
            }

            return signatures
                .Where(cs =>
                    cs.SequenceOrder < step.SequenceOrder)
                .All(cs =>
                    cs.Decision == SignatureDecision.Signed);
        }

        // ============================================================
        // SIGNER ROLE DISPLAY
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
        // DECISION DISPLAY
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
            {
                return "declined";
            }

            if (isCompleted)
            {
                return "completed";
            }

            if (currentStep == null)
            {
                return "not-started";
            }

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
                User.Identity?.Name ?? "Unknown";

            string actorRole =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? "Unknown";

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