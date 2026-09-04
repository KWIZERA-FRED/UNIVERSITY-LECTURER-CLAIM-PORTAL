using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.HOD;

[Authorize(Roles = "HOD")]
public class ContractPreviewModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractPreviewModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int ContractId { get; set; }

    public string LecturerName { get; private set; } = string.Empty;
    public string CourseCode { get; private set; } = string.Empty;
    public string CourseTitle { get; private set; } = string.Empty;
    public string AcademicYear { get; private set; } = string.Empty;
    public string Semester { get; private set; } = string.Empty;
    public string Session { get; private set; } = string.Empty;
    public string Campus { get; private set; } = string.Empty;
    public decimal AllocatedHours { get; private set; }
    public decimal RatePerHour { get; private set; }
    public string ContractContent { get; private set; } = string.Empty;
    public ContractStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Lecturer)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a!.Course)
            .FirstOrDefaultAsync(c => c.Id == ContractId);

        if (contract is null)
        {
            ErrorMessage = "The requested contract could not be found.";
            return Page();
        }

        LecturerName = contract.Lecturer.UserName;
        CourseCode = contract.CourseAssignment?.Course.Code ?? "—";
        CourseTitle = contract.CourseAssignment?.Course.Title ?? "—";
        AcademicYear = contract.CourseAssignment?.AcademicYear ?? "—";
        Semester = contract.CourseAssignment?.Semester.ToString() ?? "—";
        Session = contract.CourseAssignment?.Session.ToString() ?? "—";
        Campus = contract.CourseAssignment?.Campus.ToString() ?? "—";
        AllocatedHours = contract.CourseAssignment?.AllocatedHours ?? 0;
        RatePerHour = contract.RatePerHour;
        ContractContent = contract.Content;
        Status = contract.Status;

        return Page();
    }
}