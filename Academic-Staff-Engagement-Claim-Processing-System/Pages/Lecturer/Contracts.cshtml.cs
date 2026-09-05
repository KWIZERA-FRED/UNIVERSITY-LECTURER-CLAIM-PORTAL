using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer;

[Authorize(Roles = "Lecturer")]
public class ContractsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ContractSigningService _contractSigningService;

    public ContractsModel(ApplicationDbContext context, ContractSigningService contractSigningService)
    {
        _context = context;
        _contractSigningService = contractSigningService;
    }

    [BindProperty(SupportsGet = true)]
    public int? ContractId { get; set; }

    public string LecturerName { get; private set; } = string.Empty;
    public List<ContractRow> Contracts { get; private set; } = new();
    public ContractDetail? SelectedContract { get; private set; }
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public int PendingSignatureCount => Contracts.Count(c => !c.IsSignedByLecturer && !c.IsClosed);
    public int ActiveContractCount => Contracts.Count(c => c.Status == ContractStatus.Active);

    public async Task<IActionResult> OnGetAsync()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId is null)
            return Challenge();

        SuccessMessage = TempData["SuccessMessage"] as string;
        await LoadAsync(lecturerId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostSignAsync(int contractId)
    {
        var lecturerId = GetLecturerId();
        if (lecturerId is null)
            return Challenge();

        var result = await _contractSigningService.SignAsLecturerAsync(
            contractId,
            lecturerId.Value,
            User.Identity?.Name ?? "Unknown",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Your signature was recorded. The contract will now continue through its approval workflow.";
            return RedirectToPage(new { ContractId = contractId });
        }

        ErrorMessage = result.ErrorMessage;
        await LoadAsync(lecturerId.Value);
        return Page();
    }

    private int? GetLecturerId() =>
        int.TryParse(User.FindFirstValue("UserId"), out var lecturerId) ? lecturerId : null;

    private async Task LoadAsync(int lecturerId)
    {
        LecturerName = await _context.Lecturers
            .Where(l => l.Id == lecturerId && l.IsActive)
            .Select(l => l.UserName)
            .FirstOrDefaultAsync() ?? string.Empty;

        var contracts = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .Where(c => c.LecturerId == lecturerId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        var contractIds = contracts.Select(c => c.Id).ToList();
        var signedContractIds = (await _context.ContractSignatures
            .AsNoTracking()
            .Where(s => contractIds.Contains(s.ContractId) &&
                        s.SignerRole == SignerRole.Lecturer &&
                        s.Decision == SignatureDecision.Signed)
            .Select(s => s.ContractId)
            .ToListAsync())
            .ToHashSet();

        Contracts = contracts.Select(c => new ContractRow
        {
            Id = c.Id,
            Reference = $"CON-{c.Id:D6}",
            CourseCode = c.CourseAssignment?.Course.Code ?? "—",
            CourseTitle = c.CourseAssignment?.Course.Title ?? "Unassigned course",
            AcademicYear = c.CourseAssignment?.AcademicYear ?? "—",
            Campus = c.CourseAssignment?.Campus.ToString() ?? "—",
            AllocatedHours = c.CourseAssignment?.AllocatedHours ?? 0,
            Status = c.Status,
            IsSignedByLecturer = signedContractIds.Contains(c.Id)
        }).ToList();

        if (ContractId.HasValue)
        {
            var contract = contracts.FirstOrDefault(c => c.Id == ContractId.Value);
            if (contract is null)
            {
                ErrorMessage ??= "The requested contract was not found.";
                return;
            }

            // Ordered by SequenceOrder so parallel steps (Dean/HR share the
            // same number) render side by side, not implying one comes
            // before the other.
            var signatureSteps = await _context.ContractSignatures
                .AsNoTracking()
                .Include(s => s.SignedByAdminAccount)
                .Where(s => s.ContractId == contract.Id)
                .OrderBy(s => s.SequenceOrder)
                .ThenBy(s => s.SignerRole)
                .ToListAsync();

            var signerStatuses = signatureSteps.Select(s => new SignerStatusRow
            {
                Role = s.SignerRole,
                SequenceOrder = s.SequenceOrder,
                Decision = s.Decision,
                SignedAtUtc = s.SignedAtUtc,
                SignerDisplayName = s.SignerRole == SignerRole.Lecturer
                    ? LecturerName
                    : s.SignedByAdminAccount?.UserName
            }).ToList();

            SelectedContract = new ContractDetail
            {
                Id = contract.Id,
                Reference = $"CON-{contract.Id:D6}",
                Content = contract.Content,
                Status = contract.Status,
                IsSignedByLecturer = signedContractIds.Contains(contract.Id),
                IsClosed = contract.Status is ContractStatus.Expired or ContractStatus.Terminated,
                IsFullySigned = contract.Status == ContractStatus.Active,
                SignerStatuses = signerStatuses
            };
        }
    }

    public sealed class ContractRow
    {
        public int Id { get; init; }
        public string Reference { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
        public string AcademicYear { get; init; } = string.Empty;
        public string Campus { get; init; } = string.Empty;
        public decimal AllocatedHours { get; init; }
        public ContractStatus Status { get; init; }
        public bool IsSignedByLecturer { get; init; }
        public bool IsClosed => Status is ContractStatus.Expired or ContractStatus.Terminated;
    }

    public sealed class ContractDetail
    {
        public int Id { get; init; }
        public string Reference { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public ContractStatus Status { get; init; }
        public bool IsSignedByLecturer { get; init; }
        public bool IsClosed { get; init; }
        public bool IsFullySigned { get; init; }
        public List<SignerStatusRow> SignerStatuses { get; init; } = new();
    }

    public sealed class SignerStatusRow
    {
        public SignerRole Role { get; init; }
        public int SequenceOrder { get; init; }
        public SignatureDecision Decision { get; init; }
        public DateTime? SignedAtUtc { get; init; }
        public string? SignerDisplayName { get; init; }
    }
}