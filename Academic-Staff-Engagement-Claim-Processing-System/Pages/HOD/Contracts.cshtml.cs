using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ContractsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int? ContractId { get; set; }

    public string HodDepartment { get; private set; } = string.Empty;
    public List<ContractRow> Contracts { get; private set; } = new();
    public ContractDetail? SelectedContract { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return RedirectToPage("/Login");

        var hod = await _context.Hods
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.UserName == username && h.IsActive);

        if (hod is null)
            return RedirectToPage("/Login");

        HodDepartment = hod.Department;

        var contracts = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .Where(c => c.CourseAssignment != null &&
                        c.CourseAssignment.Course.Department == hod.Department)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        var contractIds = contracts.Select(c => c.Id).ToList();

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s => contractIds.Contains(s.ContractId))
            .OrderBy(s => s.SequenceOrder)
            .ToListAsync();

        Contracts = contracts.Select(c => new ContractRow
        {
            Id = c.Id,
            Reference = $"CON-{c.Id:D6}",
            LecturerName = c.Lecturer.UserName,
            CourseCode = c.CourseAssignment?.Course.Code ?? "—",
            CourseTitle = c.CourseAssignment?.Course.Title ?? "—",
            AcademicYear = c.CourseAssignment?.AcademicYear ?? "—",
            Status = c.Status,
            SignedSteps = signatures.Count(s => s.ContractId == c.Id && s.Decision == SignatureDecision.Signed),
            TotalSteps = signatures.Count(s => s.ContractId == c.Id)
        }).ToList();

        if (ContractId.HasValue)
        {
            var contract = contracts.FirstOrDefault(c => c.Id == ContractId.Value);
            if (contract is null)
            {
                ErrorMessage = "That contract was not found in your department.";
                return Page();
            }

            SelectedContract = new ContractDetail
            {
                Id = contract.Id,
                Reference = $"CON-{contract.Id:D6}",
                LecturerName = contract.Lecturer.UserName,
                CourseTitle = contract.CourseAssignment?.Course.Title ?? "—",
                Content = contract.Content,
                Status = contract.Status,
                Steps = signatures
                    .Where(s => s.ContractId == contract.Id)
                    .Select(s => new SignatureStepRow
                    {
                        Role = s.SignerRole,
                        Decision = s.Decision,
                        SignedAtUtc = s.SignedAtUtc,
                        Comments = s.Comments
                    }).ToList()
            };
        }

        return Page();
    }

    public sealed class ContractRow
    {
        public int Id { get; init; }
        public string Reference { get; init; } = string.Empty;
        public string LecturerName { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
        public string AcademicYear { get; init; } = string.Empty;
        public ContractStatus Status { get; init; }
        public int SignedSteps { get; init; }
        public int TotalSteps { get; init; }
    }

    public sealed class ContractDetail
    {
        public int Id { get; init; }
        public string Reference { get; init; } = string.Empty;
        public string LecturerName { get; init; } = string.Empty;
        public string CourseTitle { get; init; } = string.Empty;
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
    }
}