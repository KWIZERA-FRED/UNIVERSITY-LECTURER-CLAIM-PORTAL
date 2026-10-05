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

    public string? SuccessMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;

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
                SequenceOrder = s.SequenceOrder,
                SignerRole = s.SignerRole,
                Decision = s.Decision,
                SignedAtUtc = s.SignedAtUtc
            })
            .ToListAsync();

        var signaturesByContract = signatures
            .GroupBy(s => s.ContractId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.SequenceOrder).ToList());

        Contracts = contracts
            .Select(c =>
            {
                signaturesByContract.TryGetValue(
                    c.Id,
                    out var contractSignatures);

                contractSignatures ??= new List<SignatureSummary>();

                var signedSteps = contractSignatures.Count(s =>
                    s.Decision == SignatureDecision.Signed);

                var isDeclined = contractSignatures.Any(s =>
                    s.Decision == SignatureDecision.Declined);

                // --- Current step = first PENDING, in sequence order ---

                SignerRole? currentRole = null;
                DateTime? waitingSince = null;

                if (!isDeclined)
                {
                    var pending = contractSignatures
                        .Where(s => s.Decision == SignatureDecision.Pending)
                        .OrderBy(s => s.SequenceOrder)
                        .FirstOrDefault();

                    if (pending is not null)
                    {
                        currentRole = pending.SignerRole;

                        // Waiting since the most recent completed signature,
                        // or since the contract was created if none yet.
                        var lastSigned = contractSignatures
                            .Where(s =>
                                s.Decision == SignatureDecision.Signed &&
                                s.SignedAtUtc.HasValue)
                            .OrderByDescending(s => s.SignedAtUtc)
                            .FirstOrDefault();

                        waitingSince =
                            lastSigned?.SignedAtUtc ?? c.CreatedAtUtc;
                    }
                }

                return new ContractRow
                {
                    ContractId = c.Id,

                    LecturerName =
                        c.Lecturer?.UserName ?? "Unknown",

                    CourseTitle =
                        c.CourseAssignment?.Course?.Title ?? "—",

                    Department =
                        c.CourseAssignment?.Course?.Department ?? "—",

                    Version = c.Version,

                    Status = c.Status,

                    SignedSteps = signedSteps,

                    TotalSteps = contractSignatures.Count,

                    IsDeclined = isDeclined,

                    IsAwaitingDean =
                        IsAwaitingDean(contractSignatures),

                    CurrentSignerRole = currentRole,

                    WaitingSinceUtc = waitingSince
                };
            })
            .ToList();

        return Page();
    }

    private static bool IsAwaitingDean(
        List<SignatureSummary> signatures)
    {
        var deanStep = signatures.FirstOrDefault(s =>
            s.SignerRole == SignerRole.Dean);

        if (deanStep is null ||
            deanStep.Decision != SignatureDecision.Pending)
        {
            return false;
        }

        return signatures
            .Where(s => s.SequenceOrder < deanStep.SequenceOrder)
            .All(s => s.Decision == SignatureDecision.Signed);
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

        public int SequenceOrder { get; init; }

        public SignerRole SignerRole { get; init; }

        public SignatureDecision Decision { get; init; }

        public DateTime? SignedAtUtc { get; init; }
    }

    public sealed class ContractRow
    {
        public int ContractId { get; init; }

        public string LecturerName { get; init; } =
            string.Empty;

        public string CourseTitle { get; init; } =
            string.Empty;

        public string Department { get; init; } =
            string.Empty;

        public string Version { get; init; } =
            string.Empty;

        public ContractStatus Status { get; init; }

        public int SignedSteps { get; init; }

        public int TotalSteps { get; init; }

        public bool IsAwaitingDean { get; init; }

        public bool IsDeclined { get; init; }

        // NEW — drives the Stage pill on the Contracts register.

        public SignerRole? CurrentSignerRole { get; init; }

        public DateTime? WaitingSinceUtc { get; init; }
    }
}