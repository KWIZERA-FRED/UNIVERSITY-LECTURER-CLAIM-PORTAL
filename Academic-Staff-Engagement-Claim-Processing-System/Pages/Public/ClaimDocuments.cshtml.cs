using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Public;

// Anyone holding the QR / link token can open the verification page.
// The official PDFs need a signed-in user, and a lecturer may only
// download the documents of their own claims.
[AllowAnonymous]
[EnableRateLimiting("public-documents-policy")]
public class ClaimDocumentsModel : PageModel
{
    private readonly OfficialDocumentService _documents;
    private readonly AuditLogger _auditLogger;

    public ClaimDocumentsModel(
        OfficialDocumentService documents,
        AuditLogger auditLogger)
    {
        _documents = documents;
        _auditLogger = auditLogger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public PublicClaimDocuments? Documents { get; private set; }

    public string? QrCodeDataUrl { get; private set; }

    public string BackUrl { get; private set; } = "/";

    // True when the signed-in user may download the official PDFs.
    public bool CanDownload { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplyPrivacyHeaders();

        Documents =
            await _documents.GetPublicDocumentsAsync(
                Token ?? string.Empty);

        if (Documents is null)
            return NotFound();

        BackUrl = ResolveBackUrl(Documents.ClaimId);

        CanDownload = CanDownloadDocuments(Documents);

        // Only signed-in visits are recorded. Anonymous visits are not,
        // so the public page cannot be used to flood the audit log.
        if (User.Identity?.IsAuthenticated == true)
        {
            await LogAsync(
                AuditAction.ClaimDocumentsViewed,
                Documents.ClaimId,
                "Claim documents page opened");
        }

        var url =
            Url.Page(
                "/Public/ClaimDocuments",
                null,
                new
                {
                    token = Documents.Token
                },
                Request.Scheme)
            ?? string.Empty;

        QrCodeDataUrl =
            $"data:image/png;base64," +
            $"{Convert.ToBase64String(
                _documents.CreateQrPng(url))}";

        return Page();
    }

    public async Task<IActionResult> OnGetPdfAsync(
        string token,
        string document)
    {
        ApplyPrivacyHeaders();

        // Downloads need a signed-in user.
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();

        var kind = ResolveKind(document);

        if (kind is null)
            return BadRequest();

        var documents =
            await _documents.GetPublicDocumentsAsync(
                token ?? string.Empty);

        if (documents is null)
            return NotFound();

        if (!CanDownloadDocuments(documents))
        {
            await LogAsync(
                AuditAction.ClaimDocumentDownloadDenied,
                documents.ClaimId,
                $"Download of {kind.Value} refused");

            return Forbid();
        }

        var url =
            Url.Page(
                "/Public/ClaimDocuments",
                null,
                new { token },
                Request.Scheme)
            ?? string.Empty;

        var generated =
            await _documents.GenerateAsync(
                token!,
                kind.Value,
                url);

        if (generated is null)
            return NotFound();

        await LogAsync(
            AuditAction.ClaimDocumentDownloaded,
            documents.ClaimId,
            $"{kind.Value} downloaded");

        return File(
            generated.Content,
            "application/pdf",
            generated.FileName);
    }

    // ================================================================
    // AUDIT
    // ================================================================

    // Only called for signed-in users, so the cookie always carries a
    // name, a role and a UserId.
    private Task LogAsync(
        AuditAction action,
        int claimId,
        string details)
    {
        int? actorId =
            int.TryParse(
                User.FindFirstValue("UserId"),
                out var id)
                ? id
                : null;

        return _auditLogger.LogAsync(
            action,
            User.Identity?.Name ?? "Unknown",
            User.FindFirstValue(ClaimTypes.Role) ?? "Unknown",
            actorId,
            "Claim",
            claimId,
            details,
            HttpContext.Connection.RemoteIpAddress?.ToString());
    }

    // ================================================================
    // ACCESS RULES
    // ================================================================

    private bool CanDownloadDocuments(
        PublicClaimDocuments documents)
    {
        if (User.Identity?.IsAuthenticated != true)
            return false;

        // Reviewers can open the documents of the claims they review.
        if (User.IsInRole("HOD") ||
            User.IsInRole("Dean") ||
            User.IsInRole("Management"))
        {
            return true;
        }

        // A lecturer may only download their own documents.
        // (LecturerName is Lecturer.UserName, the same value the
        // login stores in ClaimTypes.Name.)
        return User.IsInRole("Lecturer") &&
               string.Equals(
                   User.Identity?.Name,
                   documents.LecturerName,
                   StringComparison.OrdinalIgnoreCase);
    }

    // The link itself is the secret, so keep it out of caches,
    // search engines and Referer headers.
    private void ApplyPrivacyHeaders()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        Response.Headers["X-Robots-Tag"] =
            "noindex, nofollow, noarchive";

        Response.Headers["Cache-Control"] =
            "no-store, max-age=0";

        Response.Headers["Pragma"] = "no-cache";
    }

    private static OfficialDocumentKind? ResolveKind(
        string? document) =>
        document?.Trim().ToLowerInvariant() switch
        {
            "contract" =>
                OfficialDocumentKind.Contract,

            "claim-letter" =>
                OfficialDocumentKind.ClaimLetter,

            "completion-form" =>
                OfficialDocumentKind.CompletionForm,

            "attendance-report" =>
                OfficialDocumentKind.AttendanceReport,

            _ => null
        };

    private string ResolveBackUrl(int claimId)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return "/";

        var role =
            User.FindFirstValue(ClaimTypes.Role);

        return role switch
        {
            "Lecturer" =>
                Url.Page(
                    "/Lecturer/ClaimDetail",
                    null,
                    new { ClaimId = claimId })
                ?? "/Lecturer/Claims",

            "HOD" =>
                Url.Page(
                    "/HOD/ClaimDetails",
                    null,
                    new { ClaimId = claimId })
                ?? "/HOD/Claims",

            "Dean" =>
                Url.Page(
                    "/DEAN/ClaimDetails",
                    null,
                    new { ClaimId = claimId })
                ?? "/DEAN/Claims",

            "Management" =>
                ResolveManagementBackUrl(claimId),

            _ =>
                "/"
        };
    }

    private string ResolveManagementBackUrl(int claimId)
    {
        var managementTitle =
            User.FindFirstValue("ManagementTitle");

        return managementTitle switch
        {
            nameof(ManagementTitle.DirectorOfQuality) =>
                Url.Page(
                    "/Management/Claims",
                    null,
                    new { ClaimId = claimId })
                ?? "/Management/Claims",

            nameof(ManagementTitle.DVCAR) =>
                Url.Page(
                    "/Management/Claims",
                    null,
                    new { ClaimId = claimId })
                ?? "/Management/Claims",

            _ =>
                "/Management/ManagementDashboard"
        };
    }
} 