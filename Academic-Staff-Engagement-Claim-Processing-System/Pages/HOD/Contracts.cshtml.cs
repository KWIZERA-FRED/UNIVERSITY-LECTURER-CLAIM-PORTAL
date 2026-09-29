using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
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

    public Faculty HodFaculty { get; private set; }

    public string HodFacultyName =>
        HodFaculty.ToString();

    public List<ContractRow> Contracts { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var username = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(username))
            return Challenge();

        var hod = await _context.Hods
            .AsNoTracking()
            .Where(h =>
                h.UserName == username &&
                h.IsActive)
            .Select(h => new
            {
                h.Faculty
            })
            .FirstOrDefaultAsync();

        if (hod is null)
            return Challenge();

        HodFaculty = hod.Faculty;

        var facultyDepartmentValues =
            GetFacultyCourseDepartmentValues(HodFaculty);

        if (facultyDepartmentValues.Count == 0)
        {
            ErrorMessage =
                "No course departments are configured for your faculty.";

            return Page();
        }

        var contracts = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .Where(c =>
                c.CourseAssignment != null &&
                c.CourseAssignment.Course != null &&
                facultyDepartmentValues.Contains(
                    c.CourseAssignment.Course.Department))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        if (contracts.Count == 0)
            return Page();

        var contractIds = contracts
            .Select(c => c.Id)
            .ToList();

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s => contractIds.Contains(s.ContractId))
            .Select(s => new SignatureSummary
            {
                ContractId = s.ContractId,
                Decision = s.Decision
            })
            .ToListAsync();

        var signaturesByContract = signatures
            .GroupBy(s => s.ContractId)
            .ToDictionary(
                g => g.Key,
                g => g.ToList());

        Contracts = contracts
            .Select(c =>
            {
                signaturesByContract.TryGetValue(
                    c.Id,
                    out var contractSignatures);

                contractSignatures ??= new List<SignatureSummary>();

                return new ContractRow
                {
                    Id = c.Id,

                    Reference =
                        $"CON-{c.Id:D6}",

                    LecturerName =
                        c.Lecturer?.UserName ?? "—",

                    CourseCode =
                        c.CourseAssignment?.Course?.Code ?? "—",

                    CourseTitle =
                        c.CourseAssignment?.Course?.Title ?? "—",

                    AcademicYear =
                        c.CourseAssignment?.AcademicYear ?? "—",

                    Status =
                        c.Status,

                    SignedSteps =
                        contractSignatures.Count(s =>
                            s.Decision == SignatureDecision.Signed),

                    TotalSteps =
                        contractSignatures.Count
                };
            })
            .ToList();

        return Page();
    }

    private static HashSet<string> GetFacultyCourseDepartmentValues(
        Faculty faculty)
    {
        return FacultyDepartments
            .GetDepartments(faculty)
            .Select(d => d.ToString())
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SignatureSummary
    {
        public int ContractId { get; init; }

        public SignatureDecision Decision { get; init; }
    }

    public sealed class ContractRow
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseCode { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public ContractStatus Status { get; init; }

        public int SignedSteps { get; init; }

        public int TotalSteps { get; init; }
    }
}