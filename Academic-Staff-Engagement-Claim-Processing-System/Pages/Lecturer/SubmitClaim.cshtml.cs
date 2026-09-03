using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer;

[Authorize(Roles = "Lecturer")]
public class SubmitClaimModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ClaimSubmissionService _claimSubmissionService;

    public SubmitClaimModel(ApplicationDbContext context, ClaimSubmissionService claimSubmissionService)
    {
        _context = context;
        _claimSubmissionService = claimSubmissionService;
    }

    [BindProperty]
    public int? SelectedCourseAssignmentId { get; set; }

    [BindProperty]
    public decimal Hours { get; set; }

    public string LecturerName { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public List<CourseItem> Courses { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId is null)
            return Challenge();

        await LoadPageDataAsync(lecturerId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var lecturerId = GetLecturerId();
        if (lecturerId is null)
            return Challenge();

        await LoadPageDataAsync(lecturerId.Value);

        if (!SelectedCourseAssignmentId.HasValue)
        {
            ErrorMessage = "Please select one of your course assignments.";
            return Page();
        }

        var result = await _claimSubmissionService.SubmitAsync(
            lecturerId.Value,
            SelectedCourseAssignmentId.Value,
            Hours,
            description: null,
            actorUsername: User.Identity?.Name ?? "Unknown",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        TempData["SuccessMessage"] = "Your claim was submitted and sent for approval.";
        return Redirect("/Claims");
    }

    private int? GetLecturerId() =>
        int.TryParse(User.FindFirstValue("UserId"), out var lecturerId) ? lecturerId : null;

    private async Task LoadPageDataAsync(int lecturerId)
    {
        LecturerName = await _context.Lecturers
            .Where(l => l.Id == lecturerId && l.IsActive)
            .Select(l => l.UserName)
            .FirstOrDefaultAsync() ?? string.Empty;

        Courses = await _context.CourseAssignments
            .AsNoTracking()
            .Include(a => a.Course)
            .Where(a => a.LecturerId == lecturerId && a.IsActive)
            .OrderBy(a => a.Course.Code)
            .Select(a => new CourseItem(
                a.Id,
                a.Course.Code,
                a.Course.Title,
                a.AcademicYear,
                a.AllocatedHours))
            .ToListAsync();
    }

    public sealed record CourseItem(
        int AssignmentId,
        string Code,
        string Name,
        string AcademicYear,
        decimal AllocatedHours);
}
