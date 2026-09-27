using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
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
        // STATE
        // ============================================================

        public List<ContractRow> Contracts { get; set; } = new();

        public string? SuccessMessage { get; set; }

        public string? ErrorMessage { get; set; }


        // ============================================================
        // CONTRACT ROW
        // ============================================================

        public class ContractRow
        {
            public int ContractId { get; set; }

            public string LecturerName { get; set; } = string.Empty;

            public string CourseTitle { get; set; } = string.Empty;

            public string Department { get; set; } = string.Empty;

            // NOTE: Contract.Version is a string in the entity,
            // so this must also be a string.
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
        // GET
        // ============================================================

        public async Task OnGetAsync()
        {
            SuccessMessage = TempData["SuccessMessage"] as string;
            ErrorMessage = TempData["ErrorMessage"] as string;

            await LoadContractsAsync();
        }


        // ============================================================
        // LOAD CONTRACTS
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

                    bool isDeclined = contractSignatures.Any(cs =>
                        cs.Decision == SignatureDecision.Declined);

                    bool isCompleted = contractSignatures.Count > 0 &&
                        contractSignatures.All(cs =>
                            cs.Decision == SignatureDecision.Signed);

                    bool isAwaitingDean =
                        contractSignatures.Any(cs =>
                            cs.SignerRole == SignerRole.Dean &&
                            cs.Decision == SignatureDecision.Pending) &&
                        IsStepCurrentlyAvailable(
                            contractSignatures,
                            SignerRole.Dean);

                    string currentStage = GetCurrentStage(
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
                            contract.CourseAssignment?.Course?.Title ?? "—",

                        Department =
                            contract.CourseAssignment?.Course?.Department ?? "—",

                        Version = contract.Version,

                        Status = contract.Status,

                        CurrentStage = currentStage,

                        CurrentStageCss = GetStageCss(
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
        // HELPERS
        // ============================================================

        private static ContractSignature? GetCurrentSignatureStep(
            List<ContractSignature> signatures)
        {
            if (signatures.Count == 0)
                return null;

            var declinedStep = signatures.FirstOrDefault(cs =>
                cs.Decision == SignatureDecision.Declined);

            if (declinedStep != null)
                return declinedStep;

            var pendingStep = signatures.FirstOrDefault(cs =>
                cs.Decision == SignatureDecision.Pending);

            if (pendingStep != null)
                return pendingStep;

            return signatures
                .OrderByDescending(cs => cs.SequenceOrder)
                .FirstOrDefault();
        }


        private static string GetCurrentStage(
            List<ContractSignature> signatures,
            ContractSignature? currentStep,
            bool isDeclined,
            bool isCompleted)
        {
            if (signatures.Count == 0)
                return "Not Started";

            if (isDeclined && currentStep != null)
            {
                return $"Declined by " +
                    $"{FormatSignerRole(currentStep.SignerRole)}";
            }

            if (isCompleted)
                return "Completed";

            var pendingStep = signatures.FirstOrDefault(cs =>
                cs.Decision == SignatureDecision.Pending);

            if (pendingStep != null)
            {
                return $"Awaiting " +
                    $"{FormatSignerRole(pendingStep.SignerRole)}";
            }

            return "In Progress";
        }


        private static bool IsStepCurrentlyAvailable(
            List<ContractSignature> signatures,
            SignerRole role)
        {
            var step = signatures.FirstOrDefault(cs =>
                cs.SignerRole == role);

            if (step == null ||
                step.Decision != SignatureDecision.Pending)
            {
                return false;
            }

            return signatures
                .Where(cs => cs.SequenceOrder < step.SequenceOrder)
                .All(cs => cs.Decision == SignatureDecision.Signed);
        }


        private static string FormatSignerRole(SignerRole role)
        {
            return role switch
            {
                SignerRole.Lecturer => "Lecturer",
                SignerRole.Dean => "Dean",
                SignerRole.HROfficer => "HR Officer",
                SignerRole.DVCAR => "DVCAR",
                SignerRole.ViceChancellor => "Vice Chancellor",
                _ => role.ToString()
            };
        }


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
    }
}