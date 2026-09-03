using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages.Public;

[AllowAnonymous]
public class ClaimDocumentsModel : PageModel
{
    private readonly OfficialDocumentService _documents;

    public ClaimDocumentsModel(OfficialDocumentService documents) => _documents = documents;

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public PublicClaimDocuments? Documents { get; private set; }
    public string? QrCodeDataUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Documents = await _documents.GetPublicDocumentsAsync(Token ?? string.Empty);
        if (Documents is null) return NotFound();

        var url = Url.Page("/Public/ClaimDocuments", null, new { token = Documents.Token }, Request.Scheme) ?? string.Empty;
        QrCodeDataUrl = $"data:image/png;base64,{Convert.ToBase64String(_documents.CreateQrPng(url))}";
        return Page();
    }

    public async Task<IActionResult> OnGetPdfAsync(string token, string document)
    {
        var kind = document.Equals("contract", StringComparison.OrdinalIgnoreCase)
            ? OfficialDocumentKind.Contract
            : document.Equals("claim-letter", StringComparison.OrdinalIgnoreCase)
                ? OfficialDocumentKind.ClaimLetter
                : (OfficialDocumentKind?)null;

        if (kind is null) return BadRequest();

        var url = Url.Page("/Public/ClaimDocuments", null, new { token }, Request.Scheme) ?? string.Empty;
        var generated = await _documents.GenerateAsync(token, kind.Value, url);
        return generated is null
            ? NotFound()
            : File(generated.Content, "application/pdf", generated.FileName);
    }
}
