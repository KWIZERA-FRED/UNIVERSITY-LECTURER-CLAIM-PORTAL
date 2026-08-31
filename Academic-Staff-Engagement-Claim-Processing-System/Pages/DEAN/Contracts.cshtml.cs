using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
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

        public ContractsModel(ApplicationDbContext context, ContractSigningService signingService)
        {
            _context = context;
            _signingService = signingService;
        }

        public List<PendingContractRow> PendingContracts { get; set; } = new();
        public ContractReviewDto? SelectedContract { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ContractId { get; set; }

        [BindProperty]
        public string? DeclineReason { get; set; }

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public class PendingContractRow
        {
            public int ContractId { get; set; }
            public string LecturerName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await LoadPendingListAsync();

            if (ContractId.HasValue)
            {
                SelectedContract = await _signingService.GetContractForReviewAsync(ContractId.Value, SignerRole.Dean);

                if (SelectedContract is null)
                {
                    ErrorMessage = "That contract could not be found, or is not awaiting a Dean signature.";
                }
            }
        }

        public async Task<IActionResult> OnPostSignAsync()
        {
            if (!ContractId.HasValue)
                return RedirectToPage();

            var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

            var result = await _signingService.SignAsync(
                ContractId.Value, SignerRole.Dean, actorId, actorUsername, actorRole, ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
            }
            else
            {
                SuccessMessage = "Contract signed successfully.";
            }

            await LoadPendingListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeclineAsync()
        {
            if (!ContractId.HasValue)
                return RedirectToPage();

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage = "Please provide a reason for declining this contract.";
                await LoadPendingListAsync();
                SelectedContract = await _signingService.GetContractForReviewAsync(ContractId.Value, SignerRole.Dean);
                return Page();
            }

            var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

            var result = await _signingService.DeclineAsync(
                ContractId.Value, SignerRole.Dean, actorId, DeclineReason, actorUsername, actorRole, ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
            }
            else
            {
                SuccessMessage = "Contract declined.";
            }

            await LoadPendingListAsync();
            return Page();
        }

        private async Task LoadPendingListAsync()
        {
            PendingContracts = await _context.ContractSignatures
                .Where(cs => cs.SignerRole == SignerRole.Dean && cs.Decision == SignatureDecision.Pending)
                .Include(cs => cs.Contract)
                    .ThenInclude(c => c.Lecturer)
                .Include(cs => cs.Contract)
                    .ThenInclude(c => c.CourseAssignment)
                        .ThenInclude(ca => ca!.Course)
                .Select(cs => new PendingContractRow
                {
                    ContractId = cs.Contract.Id,
                    LecturerName = cs.Contract.Lecturer.UserName,
                    CourseTitle = cs.Contract.CourseAssignment != null ? cs.Contract.CourseAssignment.Course.Title : "—"
                })
                .ToListAsync();
        }

        private (int actorId, string actorUsername, string actorRole, string? ipAddress) GetActorContext()
        {
            int.TryParse(User.FindFirst("UserId")?.Value, out int actorId);
            string actorUsername = User.Identity?.Name ?? "Unknown";
            string actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            return (actorId, actorUsername, actorRole, ipAddress);
        }
    }
}