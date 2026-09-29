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
            IReadOnlyCollection<ContractSignature> signatures) =>
            ContractSignatureMarkup.ApplyLiveSignatures(
                originalContent,
                signatures);

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