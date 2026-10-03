using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Lecturer;

// ====================================================================
// MY CONTRACTS — LIST ONLY
// ====================================================================
//
// Reviewing and signing a single contract now lives on
// /Lecturer/ContractDetails/{id}. This page only lists the
// lecturer's contracts and links to that page.
// ====================================================================

[Authorize(Roles = "Lecturer")]
public class ContractsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ContractsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public string LecturerName { get; private set; } = string.Empty;

    public List<ContractRow> Contracts { get; private set; } = new();

    public int PendingSignatureCount =>
        Contracts.Count(c =>
            !c.IsSignedByLecturer &&
            !c.IsClosed);

    public int ActiveContractCount =>
        Contracts.Count(c =>
            c.Status == ContractStatus.Active);

    // ============================================================
    // GET
    // ============================================================

    public async Task<IActionResult> OnGetAsync()
    {
        var lecturerId = GetLecturerId();

        if (lecturerId is null)
            return Challenge();

        await LoadAsync(lecturerId.Value);

        return Page();
    }

    // ============================================================
    // CURRENT LECTURER
    // ============================================================

    private int? GetLecturerId()
    {
        return int.TryParse(
            User.FindFirstValue("UserId"),
            out var lecturerId)
                ? lecturerId
                : null;
    }

    // ============================================================
    // LOAD
    // ============================================================

    private async Task LoadAsync(int lecturerId)
    {
        LecturerName =
            await _context.Lecturers
                .Where(l =>
                    l.Id == lecturerId &&
                    l.IsActive)
                .Select(l => l.UserName)
                .FirstOrDefaultAsync()
            ?? string.Empty;

        var contracts =
            await _context.Contracts
                .AsNoTracking()
                .Include(c => c.CourseAssignment)
                    .ThenInclude(a => a!.Course)
                .Where(c =>
                    c.LecturerId == lecturerId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();

        var contractIds =
            contracts
                .Select(c => c.Id)
                .ToList();

        var signedContractIds =
            (await _context.ContractSignatures
                .AsNoTracking()
                .Where(s =>
                    contractIds.Contains(s.ContractId) &&
                    s.SignerRole == SignerRole.Lecturer &&
                    s.Decision == SignatureDecision.Signed)
                .Select(s => s.ContractId)
                .ToListAsync())
            .ToHashSet();

        Contracts =
            contracts
                .Select(c => new ContractRow
                {
                    Id = c.Id,

                    Reference = $"CON-{c.Id:D6}",

                    CourseCode =
                        c.CourseAssignment?.Course.Code ?? "—",

                    CourseTitle =
                        c.CourseAssignment?.Course.Title
                        ?? "Unassigned course",

                    AcademicYear =
                        c.CourseAssignment?.AcademicYear ?? "—",

                    Campus =
                        c.CourseAssignment?.Campus.ToString() ?? "—",

                    AllocatedHours =
                        c.CourseAssignment?.AllocatedHours ?? 0,

                    Status = c.Status,

                    IsSignedByLecturer =
                        signedContractIds.Contains(c.Id)
                })
                .ToList();
    }

    // ============================================================
    // CONTRACT ROW
    // ============================================================

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

        public bool IsClosed =>
            Status is
                ContractStatus.Expired or
                ContractStatus.Terminated;
    }
}