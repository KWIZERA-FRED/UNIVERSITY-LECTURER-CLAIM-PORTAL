using System.Security.Claims;

using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Public;

[AllowAnonymous]
public class ClaimDocumentsModel : PageModel
{
    private readonly OfficialDocumentService _documents;

    public ClaimDocumentsModel(
        OfficialDocumentService documents)
    {
        _documents = documents;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public PublicClaimDocuments? Documents { get; private set; }

    public string? QrCodeDataUrl { get; private set; }

    public string BackUrl { get; private set; } = "/";

    public async Task<IActionResult> OnGetAsync()
    {
        Documents =
            await _documents.GetPublicDocumentsAsync(
                Token ?? string.Empty);

        if (Documents is null)
            return NotFound();

        BackUrl = ResolveBackUrl(Documents.ClaimId);

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
        var kind = ResolveKind(document);

        if (kind is null)
            return BadRequest();

        var url =
            Url.Page(
                "/Public/ClaimDocuments",
                null,
                new { token },
                Request.Scheme)
            ?? string.Empty;

        var generated =
            await _documents.GenerateAsync(
                token,
                kind.Value,
                url);

        return generated is null
            ? NotFound()
            : File(
                generated.Content,
                "application/pdf",
                generated.FileName);
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