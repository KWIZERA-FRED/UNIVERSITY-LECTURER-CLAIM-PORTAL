using System.Security.Claims;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
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
        private readonly IWebHostEnvironment _environment;

        public ContractsModel(
            ApplicationDbContext context,
            ContractSigningService signingService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _signingService = signingService;
            _environment = environment;
        }

        public List<PendingContractRow> PendingContracts { get; set; } = new();

        public ContractReviewDto? SelectedContract { get; set; }

        public List<SignatureStepViewModel> SelectedSignatureSteps { get; set; } = new();

        public string RoleLabel { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public string? SuccessMessage { get; set; }

        public string RenderedContractContent { get; private set; } = string.Empty;

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

        public class SignatureStepViewModel
        {
            public int SignatureId { get; set; }

            public int SequenceOrder { get; set; }

            public SignerRole SignerRole { get; set; }

            public SignatureDecision Decision { get; set; }

            public string? SignatureFilePath { get; set; }

            public string? SignedByName { get; set; }

            public int? SignedById { get; set; }

            public DateTime? SignedAtUtc { get; set; }

            public string? Comments { get; set; }

            public bool IsCurrent { get; set; }

            public bool SignatureAvailable { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var role = await ResolveSignerRoleAsync();

            if (role is null)
            {
                return RedirectToPage("/ManagementDashboard");
            }

            await LoadPendingListAsync(role.Value);

            if (!ContractId.HasValue)
            {
                return Page();
            }

            SelectedContract =
                await _signingService.GetContractForReviewAsync(
                    ContractId.Value,
                    role.Value);

            if (SelectedContract is null)
            {
                ErrorMessage =
                    $"That contract could not be found, or is not awaiting a {RoleLabel} signature.";

                return Page();
            }

            await LoadSignatureStepsAsync(
                ContractId.Value,
                role.Value);

            BuildRenderedContract();

            return Page();
        }

        public async Task<IActionResult> OnPostSignAsync()
        {
            var role = await ResolveSignerRoleAsync();

            if (role is null || !ContractId.HasValue)
            {
                return RedirectToPage("/ManagementDashboard");
            }

            var (
                actorId,
                actorUsername,
                actorRole,
                ipAddress
            ) = GetActorContext();

            var result = await _signingService.SignAsync(
                ContractId.Value,
                role.Value,
                actorId,
                actorUsername,
                actorRole,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
            }
            else
            {
                SuccessMessage =
                    "Contract signed successfully.";
            }

            await LoadPendingListAsync(role.Value);

            if (ContractId.HasValue)
            {
                SelectedContract =
                    await _signingService.GetContractForReviewAsync(
                        ContractId.Value,
                        role.Value);

                if (SelectedContract is not null)
                {
                    await LoadSignatureStepsAsync(
                        ContractId.Value,
                        role.Value);

                    BuildRenderedContract();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeclineAsync()
        {
            var role = await ResolveSignerRoleAsync();

            if (role is null || !ContractId.HasValue)
            {
                return RedirectToPage("/ManagementDashboard");
            }

            if (string.IsNullOrWhiteSpace(DeclineReason))
            {
                ErrorMessage =
                    "Please provide a reason for declining this contract.";

                await LoadPendingListAsync(role.Value);

                SelectedContract =
                    await _signingService.GetContractForReviewAsync(
                        ContractId.Value,
                        role.Value);

                if (SelectedContract is not null)
                {
                    await LoadSignatureStepsAsync(
                        ContractId.Value,
                        role.Value);

                    BuildRenderedContract();
                }

                return Page();
            }

            var (
                actorId,
                actorUsername,
                actorRole,
                ipAddress
            ) = GetActorContext();

            var result = await _signingService.DeclineAsync(
                ContractId.Value,
                role.Value,
                actorId,
                DeclineReason,
                actorUsername,
                actorRole,
                ipAddress);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
            }
            else
            {
                SuccessMessage =
                    "Contract declined.";
            }

            await LoadPendingListAsync(role.Value);

            if (ContractId.HasValue)
            {
                SelectedContract =
                    await _signingService.GetContractForReviewAsync(
                        ContractId.Value,
                        role.Value);

                if (SelectedContract is not null)
                {
                    await LoadSignatureStepsAsync(
                        ContractId.Value,
                        role.Value);

                    BuildRenderedContract();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnGetSignatureAsync(
            int contractId,
            int signatureId)
        {
            var role = await ResolveSignerRoleAsync();

            if (role is null)
            {
                return Forbid();
            }

            var signature = await _context.ContractSignatures
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s =>
                        s.Id == signatureId &&
                        s.ContractId == contractId &&
                        s.Decision == SignatureDecision.Signed);

            if (signature is null ||
                string.IsNullOrWhiteSpace(signature.SignatureFilePath))
            {
                return NotFound();
            }

            var rootPath =
                Path.GetFullPath(_environment.WebRootPath);

            var relativePath =
                signature.SignatureFilePath
                    .TrimStart('/')
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar);

            var fullPath =
                Path.GetFullPath(
                    Path.Combine(
                        rootPath,
                        relativePath));

            if (!fullPath.StartsWith(
                    rootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var extension =
                Path.GetExtension(fullPath)
                    .ToLowerInvariant();

            var contentType = extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return PhysicalFile(
                fullPath,
                contentType);
        }

        private async Task<SignerRole?> ResolveSignerRoleAsync()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            var management =
                await _context.ManagementAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        m =>
                            m.UserName == username &&
                            m.IsActive);

            if (management is null ||
                management.Title == ManagementTitle.ExamOffice)
            {
                return null;
            }

            RoleLabel =
                management.Title switch
                {
                    ManagementTitle.HROfficer =>
                        "Human Resource Officer",

                    ManagementTitle.DVCAR =>
                        "DVCAR",

                    ManagementTitle.ViceChancellor =>
                        "Vice Chancellor",

                    _ =>
                        management.Title.ToString()
                };

            return ManagementDashboardModel
                .MapTitleToSignerRole(management.Title);
        }

        private async Task LoadPendingListAsync(
            SignerRole role)
        {
            PendingContracts =
                await _context.ContractSignatures
                    .AsNoTracking()
                    .Where(
                        cs =>
                            cs.SignerRole == role &&
                            cs.Decision == SignatureDecision.Pending)
                    .Include(cs => cs.Contract)
                        .ThenInclude(c => c.Lecturer)
                    .Include(cs => cs.Contract)
                        .ThenInclude(c => c.CourseAssignment)
                            .ThenInclude(ca => ca!.Course)
                    .Select(
                        cs =>
                            new PendingContractRow
                            {
                                ContractId =
                                    cs.Contract.Id,

                                LecturerName =
                                    cs.Contract.Lecturer.UserName,

                                CourseTitle =
                                    cs.Contract.CourseAssignment != null
                                        ? cs.Contract.CourseAssignment.Course.Title
                                        : "—"
                            })
                    .Distinct()
                    .OrderByDescending(
                        c => c.ContractId)
                    .ToListAsync();
        }

        private async Task LoadSignatureStepsAsync(
            int contractId,
            SignerRole currentRole)
        {
            var signatures =
                await _context.ContractSignatures
                    .AsNoTracking()
                    .Where(
                        s =>
                            s.ContractId == contractId)
                    .Include(s => s.SignedByLecturer)
                    .Include(s => s.SignedByAdminAccount)
                    .OrderBy(s => s.SequenceOrder)
                    .ToListAsync();

            var currentStep =
                signatures
                    .FirstOrDefault(
                        s =>
                            s.SignerRole == currentRole &&
                            s.Decision == SignatureDecision.Pending);

            SelectedSignatureSteps =
                signatures
                    .Select(
                        s =>
                            new SignatureStepViewModel
                            {
                                SignatureId = s.Id,

                                SequenceOrder =
                                    s.SequenceOrder,

                                SignerRole =
                                    s.SignerRole,

                                Decision =
                                    s.Decision,

                                SignatureFilePath =
                                    s.SignatureFilePath,

                                SignedById =
                                    s.SignedByLecturerId ??
                                    s.SignedByAdminAccountId,

                                SignedByName =
                                    s.SignedByLecturer != null
                                        ? s.SignedByLecturer.UserName
                                        : s.SignedByAdminAccount != null
                                            ? s.SignedByAdminAccount.UserName
                                            : null,

                                SignedAtUtc =
                                    s.SignedAtUtc,

                                Comments =
                                    s.Comments,

                                IsCurrent =
                                    currentStep != null &&
                                    currentStep.Id == s.Id,

                                SignatureAvailable =
                                    s.Decision == SignatureDecision.Signed &&
                                    !string.IsNullOrWhiteSpace(
                                        s.SignatureFilePath)
                            })
                    .ToList();
        }

        // ============================================================
        // BUILD RENDERED CONTRACT
        // ============================================================
        //
        // Concatenates the stored Contract.Content with a freshly
        // generated signature table. If Content is empty (which
        // happens when the contract generator saved a row without
        // content), a visible fallback is rendered so the page
        // never appears blank.
        //

        private void BuildRenderedContract()
        {
            if (SelectedContract is null)
            {
                RenderedContractContent = string.Empty;
                return;
            }

            var content = SelectedContract.ContractContent;

            if (string.IsNullOrWhiteSpace(content))
            {
                content =
                    """
                    <div class="contract-content-missing">

                        <h2>Contract Content Unavailable</h2>

                        <p>
                            This contract has no stored content. The
                            <code>Contract.Content</code> column is empty
                            in the database for this record.
                        </p>

                        <p>
                            The signing workflow below is still active
                            and reflects the current state of the
                            contract. Please contact the system
                            administrator to regenerate the document
                            body.
                        </p>

                    </div>
                    """;
            }
            else
            {
                content = RemoveSignaturePlaceholders(content);
            }

            var signatureTable = BuildOfficialSignatureTable();

            RenderedContractContent = content + signatureTable;
        }

        private string RemoveSignaturePlaceholders(
            string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            html =
                System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"<[^>]*class\s*=\s*[""'][^""']*signature-placeholder[^""']*[""'][^>]*>.*?</[^>]+>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Singleline);

            html =
                System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"<[^>]*class\s*=\s*[""'][^""']*signature-pending[^""']*[""'][^>]*>.*?</[^>]+>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Singleline);

            return html;
        }

        // ============================================================
        // BUILD OFFICIAL SIGNATURE TABLE
        // ============================================================
        //
        // The contract id is captured once before the LINQ query so
        // it can be interpolated into each row. No local function
        // after return — the value is a plain captured variable.
        //

        private string BuildOfficialSignatureTable()
        {
            if (SelectedContract is null)
            {
                return string.Empty;
            }

            var contractId = SelectedContract.ContractId;

            var rows =
                SelectedSignatureSteps
                    .OrderBy(s => s.SequenceOrder)
                    .Select(
                        step =>
                        {
                            var role =
                                GetRoleLabel(step.SignerRole);

                            var status =
                                step.Decision ==
                                    SignatureDecision.Signed
                                    ? "Signed"
                                    : step.Decision ==
                                        SignatureDecision.Declined
                                        ? "Declined"
                                        : "Pending";

                            var signatureHtml =
                                step.Decision ==
                                    SignatureDecision.Signed &&
                                step.SignatureAvailable
                                    ? $"""
                                        <img
                                            src="/Management/Contracts?handler=Signature&contractId={contractId}&signatureId={step.SignatureId}"
                                            alt="{System.Net.WebUtility.HtmlEncode(role)} electronic signature"
                                            class="official-signature-image" />
                                      """
                                    : $"""
                                        <span class="official-signature-status">
                                            {status}
                                        </span>
                                      """;

                            return $"""
                                <tr>
                                    <td>{System.Net.WebUtility.HtmlEncode(role)}</td>
                                    <td class="official-signature-cell">
                                        {signatureHtml}
                                    </td>
                                    <td>
                                        {System.Net.WebUtility.HtmlEncode(
                                            step.SignedByName ?? "—")}
                                    </td>
                                    <td>
                                        {(step.SignedAtUtc.HasValue
                                            ? step.SignedAtUtc.Value
                                                .ToLocalTime()
                                                .ToString("dd MMM yyyy, HH:mm")
                                            : "—")}
                                    </td>
                                </tr>
                                """;
                        });

            return $"""
                <section class="official-signature-section">
                    <h3>Electronic Signatures</h3>

                    <p class="official-signature-introduction">
                        The following signatures are the electronic signatures
                        recorded against this contract.
                    </p>

                    <table class="official-signature-table">
                        <thead>
                            <tr>
                                <th>Signing Role</th>
                                <th>Signature</th>
                                <th>Signed By</th>
                                <th>Date</th>
                            </tr>
                        </thead>

                        <tbody>
                            {string.Join(Environment.NewLine, rows)}
                        </tbody>
                    </table>
                </section>
                """;
        }

        private static string GetRoleLabel(
            SignerRole role)
        {
            return role switch
            {
                SignerRole.Lecturer =>
                    "Lecturer",

                SignerRole.Dean =>
                    "Dean of Faculty",

                SignerRole.HROfficer =>
                    "Human Resource Officer",

                SignerRole.DVCAR =>
                    "DVCAR",

                SignerRole.ViceChancellor =>
                    "Vice Chancellor",

                _ =>
                    role.ToString()
            };
        }

        private (
            int actorId,
            string actorUsername,
            string actorRole,
            string? ipAddress
        ) GetActorContext()
        {
            int.TryParse(
                User.FindFirst("UserId")?.Value,
                out int actorId);

            string actorUsername =
                User.Identity?.Name ??
                "Unknown";

            string actorRole =
                User.FindFirst(
                    ClaimTypes.Role)?.Value ??
                "Unknown";

            string? ipAddress =
                HttpContext
                    .Connection
                    .RemoteIpAddress?
                    .ToString();

            return (
                actorId,
                actorUsername,
                actorRole,
                ipAddress);
        }
    }
}