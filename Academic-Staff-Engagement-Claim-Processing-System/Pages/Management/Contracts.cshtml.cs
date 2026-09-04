using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Management
{
    [Authorize(Roles = "Management")]
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
        public string RoleLabel { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? ContractId { get; set; }

        [BindProperty]
        public string? DeclineReason { get; set; }

        public class PendingContractRow
        {
            public int ContractId { get; set; }
            public string LecturerName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var role = await ResolveSignerRoleAsync();
            if (role is null)
                return RedirectToPage("/ManagementDashboard");

            await LoadPendingListAsync(role.Value);

            if (ContractId.HasValue)
            {
                SelectedContract = await _signingService.GetContractForReviewAsync(ContractId.Value, role.Value);
                if (SelectedContract is null)
                    ErrorMessage = $"That contract could not be found, or is not awaiting a {RoleLabel} signature.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSignAsync()
        {
            var role = await ResolveSignerRoleAsync();
            if (role is null || !ContractId.HasValue)
                return RedirectToPage("/ManagementDashboard");

            var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

            var result = await _signingService.SignAsync(
                ContractId.Value, role.Value, actorId, actorUsername, actorRole, ipAddress);

            if (!result.Succeeded)
                ErrorMessage = result.ErrorMessage;
            else
                SuccessMessage = "Contract signed successfully.";

            await LoadPendingListAsync(role.Value);
            return Page();
        }

        public async Task<IActionResult> OnPostDeclineAsync()
        {
            var role = await ResolveSignerRoleAsync();
            if (role is null || !ContractId.HasValue)
                return RedirectToPage("/ManagementDashboard");

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage = "Please provide a reason for declining this contract.";
                await LoadPendingListAsync(role.Value);
                SelectedContract = await _signingService.GetContractForReviewAsync(ContractId.Value, role.Value);
                return Page();
            }

            var (actorId, actorUsername, actorRole, ipAddress) = GetActorContext();

            var result = await _signingService.DeclineAsync(
                ContractId.Value, role.Value, actorId, DeclineReason, actorUsername, actorRole, ipAddress);

            if (!result.Succeeded)
                ErrorMessage = result.ErrorMessage;
            else
                SuccessMessage = "Contract declined.";

            await LoadPendingListAsync(role.Value);
            return Page();
        }

        private async Task<SignerRole?> ResolveSignerRoleAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var management = await _context.ManagementAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserName == username && m.IsActive);

            if (management is null || management.Title == ManagementTitle.ExamOffice)
                return null;

            RoleLabel = management.Title.ToString();
            return ManagementDashboardModel.MapTitleToSignerRole(management.Title);
        }

        private async Task LoadPendingListAsync(SignerRole role)
        {
            PendingContracts = await _context.ContractSignatures
                .Where(cs => cs.SignerRole == role && cs.Decision == SignatureDecision.Pending)
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