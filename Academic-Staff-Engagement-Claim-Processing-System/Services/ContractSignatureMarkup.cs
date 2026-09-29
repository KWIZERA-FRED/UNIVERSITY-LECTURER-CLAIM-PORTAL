using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services;

/// <summary>
/// Single place that builds and applies the paper-style signature block
/// of a contract, so every page renders exactly one block.
/// </summary>
public static class ContractSignatureMarkup
{
    private const string AccreditationMarker =
        "<p class=\"contract-accreditation-note\">";

    private const RegexOptions Options =
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.Compiled;

    // Matches the whole wrapper INCLUDING its nested line divs.
    // (A plain ".*?</div>" stops at the first inner </div>.)
    private static readonly Regex PaperSignaturesRegex = new(
        @"<div\s+class\s*=\s*[""']paper-signatures[""'][^>]*>(?:\s*<div\b[^>]*>.*?</div>)*\s*</div>",
        Options);

    private static readonly Regex LegacySignatureTableRegex = new(
        @"<table\s+class\s*=\s*[""']signature-table[^""']*[""'][^>]*>.*?</table>",
        Options);

    private static readonly (SignerRole Role, string CssClass, string? Label)[] Lines =
    {
        (SignerRole.Lecturer, "lecturer-signature-line", null),
        (SignerRole.Dean, "dean-signature-line", "Dean of Faculty: Prof. NYESHEJA M. Enan"),
        (SignerRole.HROfficer, "hr-signature-line", "Human Resource Officer Mr. NTAKIRUTIMANA Elison"),
        (SignerRole.DVCAR, "dvcar-signature-line", "DVCAR Prof. HAKIZIMANA Emmanuel"),
        (SignerRole.ViceChancellor, "vc-signature-line", "Vice Chancellor Prof. NGAMIJE Jean")
    };

    /// <summary>
    /// Removes every signature block from stored contract HTML:
    /// paper-signatures blocks (any number of them) and the legacy table.
    /// </summary>
    public static string RemoveSignatureBlocks(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var result = PaperSignaturesRegex.Replace(html, string.Empty);

        return LegacySignatureTableRegex.Replace(result, string.Empty);
    }

    /// <summary>
    /// Returns the stored contract HTML with exactly one live signature
    /// block, built from the current ContractSignatures rows.
    /// </summary>
    public static string ApplyLiveSignatures(
        string originalContent,
        IReadOnlyCollection<ContractSignature> signatures,
        string? lecturerName = null)
    {
        if (string.IsNullOrWhiteSpace(originalContent))
            return string.Empty;

        var stripped = RemoveSignatureBlocks(originalContent);

        var liveSection =
            BuildLiveSignatureSection(signatures, lecturerName);

        var accreditationIndex =
            stripped.IndexOf(
                AccreditationMarker,
                StringComparison.OrdinalIgnoreCase);

        if (accreditationIndex >= 0)
        {
            return stripped.Insert(
                accreditationIndex,
                liveSection + Environment.NewLine);
        }

        var closingDivIndex =
            stripped.LastIndexOf(
                "</div>",
                StringComparison.OrdinalIgnoreCase);

        if (closingDivIndex >= 0)
        {
            return stripped.Insert(
                closingDivIndex,
                liveSection + Environment.NewLine);
        }

        return stripped + Environment.NewLine + liveSection;
    }

    private static string BuildLiveSignatureSection(
        IReadOnlyCollection<ContractSignature> signatures,
        string? lecturerName)
    {
        var html = new StringBuilder();

        html.AppendLine("<div class=\"paper-signatures\">");

        foreach (var (role, cssClass, label) in Lines)
        {
            var signature =
                signatures
                    .Where(s => s.SignerRole == role)
                    .OrderBy(s => s.SequenceOrder)
                    .FirstOrDefault();

            var displayName = label;

            if (role == SignerRole.Lecturer)
            {
                var name =
                    !string.IsNullOrWhiteSpace(lecturerName)
                        ? lecturerName
                        : signature?.SignedByLecturer?.UserName;

                displayName =
                    string.IsNullOrWhiteSpace(name)
                        ? "Lecturer’s Name"
                        : $"Lecturer’s Name: {name}";
            }

            html.AppendLine(
                BuildLine(
                    cssClass,
                    displayName ?? string.Empty,
                    signature));
        }

        html.AppendLine("</div>");

        return html.ToString();
    }

    private static string BuildLine(
        string cssClass,
        string displayName,
        ContractSignature? signature)
    {
        var isSigned =
            signature?.Decision == SignatureDecision.Signed;

        var imageHtml = string.Empty;

        if (isSigned &&
            !string.IsNullOrWhiteSpace(signature!.SignatureFilePath))
        {
            var src =
                WebUtility.HtmlEncode(
                    NormalizeImageSource(
                        signature.SignatureFilePath));

            imageHtml =
                "<span class=\"paper-signature-image-wrapper\">" +
                $"<img src=\"{src}\" alt=\"Electronic signature\" " +
                "class=\"contract-signature-image\" />" +
                "</span>";
        }

        var dateHtml = string.Empty;

        if (isSigned && signature!.SignedAtUtc.HasValue)
        {
            var formattedDate =
                WebUtility.HtmlEncode(
                    signature.SignedAtUtc.Value
                        .ToLocalTime()
                        .ToString("dd/MM/yyyy"));

            dateHtml =
                $"<span class=\"paper-date-value\">{formattedDate}</span>";
        }

        var safeName = WebUtility.HtmlEncode(displayName);

        var line = new StringBuilder();

        line.AppendLine($"<div class=\"paper-signature-line {cssClass}\">");
        line.AppendLine($"    <span class=\"paper-signature-name\">{safeName}</span>");
        line.AppendLine(
            "    <span class=\"paper-signature-field paper-signature-area\">" +
            $"Signature........................{imageHtml}</span>");
        line.AppendLine(
            "    <span class=\"paper-signature-field paper-date-area\">" +
            $"Date.................{dateHtml}</span>");
        line.AppendLine("</div>");

        return line.ToString();
    }

    // A stored path such as "uploads/signatures/x.png" (no leading slash)
    // is resolved relative to the current page URL and shows as a broken
    // image. Make it root-relative.
    private static string NormalizeImageSource(string path)
    {
        var trimmed = path.Trim();

        if (trimmed.StartsWith("/") ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return "/" + trimmed.Replace('\\', '/');
    }
}