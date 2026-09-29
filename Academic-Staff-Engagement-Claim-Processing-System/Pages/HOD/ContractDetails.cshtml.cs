using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ContractDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractDetailsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public ContractDetailsViewModel? Contract { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
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

        var facultyDepartmentValues =
            GetFacultyCourseDepartmentValues(hod.Faculty);

        if (facultyDepartmentValues.Count == 0)
        {
            ErrorMessage =
                "No course departments are configured for your faculty.";

            return Page();
        }

        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.CourseAssignment != null &&
                c.CourseAssignment.Course != null &&
                facultyDepartmentValues.Contains(
                    c.CourseAssignment.Course.Department));

        if (contract is null)
            return NotFound();

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Include(s => s.SignedByLecturer)
            .Where(s => s.ContractId == contract.Id)
            .OrderBy(s => s.SequenceOrder)
            .ToListAsync();

        var steps = signatures
            .Select(s => new ContractSignatureViewModel
            {
                Id = s.Id,
                SequenceOrder = s.SequenceOrder,
                Role = s.SignerRole,
                Decision = s.Decision,
                Comments = s.Comments,
                SignedAtUtc = s.SignedAtUtc
            })
            .ToList();

        // Contract.Content is the snapshot stored when the contract was
        // generated. Rebuild its signature block from the live
        // ContractSignatures rows so exactly one block is shown, with the
        // real signature images and dates.
        var liveContent =
            ContractSignatureMarkup.ApplyLiveSignatures(
                contract.Content ?? string.Empty,
                signatures,
                contract.Lecturer?.UserName);

        Contract = new ContractDetailsViewModel
        {
            Id = contract.Id,

            Reference =
                $"CON-{contract.Id:D6}",

            LecturerName =
                contract.Lecturer?.UserName ?? "—",

            CourseTitle =
                contract.CourseAssignment?.Course?.Title ?? "—",

            AcademicYear =
                contract.CourseAssignment?.AcademicYear ?? "—",

            Status =
                contract.Status,

            Content =
                liveContent,

            Steps =
                steps
        };

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

    public sealed class ContractDetailsViewModel
    {
        public int Id { get; init; }

        public string Reference { get; init; } =
            string.Empty;

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string AcademicYear { get; init; } =
            string.Empty;

        public ContractStatus Status { get; init; }

        public string Content { get; init; } =
            string.Empty;

        public List<ContractSignatureViewModel> Steps { get; init; } =
            new();
    }

    public sealed class ContractSignatureViewModel
    {
        public int Id { get; init; }

        public int SequenceOrder { get; init; }

        public SignerRole Role { get; init; }

        public SignatureDecision Decision { get; init; }

        public string? Comments { get; init; }

        public DateTime? SignedAtUtc { get; init; }
    }
}