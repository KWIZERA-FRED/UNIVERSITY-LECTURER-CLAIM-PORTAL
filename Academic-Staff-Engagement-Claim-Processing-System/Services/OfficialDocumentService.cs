using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services;

public sealed class OfficialDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    private const string DeanName = "Prof. NYESHEJA M. Enan";
    private const string HrOfficerName = "Mr. NTAKIRUTIMANA Elison";
    private const string DvcarName = "Prof. HAKIZIMANA Emmanuel";
    private const string ViceChancellorName = "Prof. NGAMIJE Jean";

    public OfficialDocumentService(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<PublicClaimDocuments?> GetPublicDocumentsAsync(
        string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 32)
            return null;

        var claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.Contract)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Course)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Lecturer)
            .FirstOrDefaultAsync(c => c.QrCodeToken == token);

        if (claim is null)
            return null;

        return new PublicClaimDocuments(
            claim.Id,
            claim.QrCodeToken,
            $"CLM-{claim.Id:D6}",
            $"CON-{claim.ContractId:D6}",
            claim.CourseAssignment.Lecturer.UserName,
            claim.CourseAssignment.Course.Code,
            claim.CourseAssignment.Course.Title,
            claim.HoursClaimed,
            claim.Status);
    }

    public async Task<GeneratedDocument?> GenerateAsync(
        string token,
        OfficialDocumentKind kind,
        string publicDocumentsUrl)
    {
        var claim = await LoadClaimAsync(token);

        if (claim is null)
            return null;

        return kind switch
        {
            OfficialDocumentKind.Contract => new GeneratedDocument(
                $"contract-CON-{claim.Contract.Id:D6}.pdf",
                await CreateContractPdfAsync(
                    claim,
                    publicDocumentsUrl)),

            OfficialDocumentKind.ClaimLetter => new GeneratedDocument(
                $"claim-letter-CLM-{claim.Id:D6}.pdf",
                CreateClaimLetterPdf(
                    claim,
                    publicDocumentsUrl)),

            _ => null
        };
    }

    public byte[] CreateQrPng(string url)
    {
        using var generator = new QRCodeGenerator();

        using var data = generator.CreateQrCode(
            url,
            QRCodeGenerator.ECCLevel.H);

        return new PngByteQRCode(data)
            .GetGraphic(
                pixelsPerModule: 20,
                darkColorRgba: new byte[] { 0, 0, 0, 255 },
                lightColorRgba: new byte[] { 255, 255, 255, 255 },
                drawQuietZones: true);
    }

    private async Task<Claim?> LoadClaimAsync(string token) =>
        await _context.Claims
            .AsNoTracking()
            .Include(c => c.Contract)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Course)
            .Include(c => c.CourseAssignment)
                .ThenInclude(a => a.Lecturer)
            .FirstOrDefaultAsync(c => c.QrCodeToken == token);

    // ================================================================
    // CONTRACT PDF
    // ================================================================

    private async Task<byte[]> CreateContractPdfAsync(
        Claim claim,
        string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;

        var qr = CreateQrPng(publicDocumentsUrl);
        var logo = GetLogoBytes();

        var idDisplay =
            string.IsNullOrWhiteSpace(lecturer.GovernmentIdEncrypted)
                ? "……………………………"
                : lecturer.GovernmentIdEncrypted;

        var rankLabel =
            lecturer.Rank.HasValue
                ? FormatEnumLabel(lecturer.Rank.Value.ToString())
                : "Not Yet Assigned";

        var sessionLabel =
            FormatEnumLabel(assignment.Session.ToString());

        var semesterLabel =
            FormatEnumLabel(assignment.Semester.ToString());

        var campusLabel =
            FormatEnumLabel(assignment.Campus.ToString());

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s => s.ContractId == claim.Contract.Id)
            .OrderBy(s => s.SequenceOrder)
            .ToListAsync();

        var lecturerSignature =
            signatures.FirstOrDefault(s => s.SignerRole == SignerRole.Lecturer);

        var deanSignature =
            signatures.FirstOrDefault(s => s.SignerRole == SignerRole.Dean);

        var hrSignature =
            signatures.FirstOrDefault(s => s.SignerRole == SignerRole.HROfficer);

        var dvcarSignature =
            signatures.FirstOrDefault(s => s.SignerRole == SignerRole.DVCAR);

        var viceChancellorSignature =
            signatures.FirstOrDefault(s => s.SignerRole == SignerRole.ViceChancellor);

        return Document.Create(document =>
        {
            // ---------------------------------------------------
            // PAGE 1 — Letterhead, preamble, Articles 1–5
            // ---------------------------------------------------
            document.Page(page =>
            {
                ConfigurePage(page);

                page.Content().Column(column =>
                {
                    column.Spacing(9);

                    column.Item().Element(c => OfficialHeader(c, logo, qr));

                    column.Item()
                        .PaddingTop(14)
                        .Text($"Kigali, {DateTime.UtcNow:dd/MM/yyyy}")
                        .FontSize(11);

                    column.Item()
                        .PaddingTop(6)
                        .Text("EMPLOYMENT PART-TIME CONTRACT")
                        .Bold()
                        .FontSize(13);

                    column.Item()
                        .PaddingTop(10)
                        .Text("Between the undersigned:");

                    column.Item()
                        .Text(text =>
                        {
                            text.Span(
                                "University of Lay Adventists of Kigali (UNILAK) represented by Vice Chancellor ");

                            text.Span(ViceChancellorName)
                                .Bold();

                            text.Span(" on one hand,");
                        });

                    column.Item()
                        .PaddingTop(6)
                        .Text(text =>
                        {
                            text.Span("And the Employee, ");

                            text.Span(lecturer.UserName)
                                .Bold();

                            text.Span(", having the Academic rank of ");

                            text.Span(rankLabel)
                                .Bold();

                            text.Span(" with identity card/Passport No. ");

                            text.Span(idDisplay)
                                .Italic();

                            text.Span(", on the other hand;");
                        });

                    column.Item()
                        .PaddingTop(4)
                        .Text("The following has been agreed:")
                        .Bold();

                    Article(
                        column,
                        "Article 1",
                        $"UNILAK employs {lecturer.UserName} as External/Internal part time lecturer in the " +
                        $"faculty of Computing and Information Sciences, Department of {assignment.Course.Department}, " +
                        $"Session {sessionLabel}, to teach the course of {assignment.Course.Code} - {assignment.Course.Title}, " +
                        $"Academic year {assignment.AcademicYear}, {semesterLabel} semester, {campusLabel} Campus.");

                    Article(
                        column,
                        "Article 2",
                        $"The number of contact hours allocated to the course/module is {assignment.AllocatedHours:N0} hours " +
                        "and this includes the theory, practical as well as examinations. " +
                        $"The rate per hour will be {claim.Contract.RatePerHour:N0} RWF (gross), applicable to the " +
                        $"{rankLabel} category, in accordance with the University's approved part-time lecturer rate scale.");

                    Article(
                        column,
                        "Article 3",
                        "The number of classes combined, if the module/course is taught through online teaching mode, " +
                        "and the total number of hours allocated to those combined classes taught by one academic staff " +
                        "member, shall be as specified in the course assignment record.");

                    Article(
                        column,
                        "Article 4",
                        "The employee is required to hand into the Deputy Vice Chancellor for Academic and Research " +
                        "office his/her application letter, CV, notarized copy of the degree, Equivalence if the degree " +
                        "is offered from a foreign country, as well as his/her nomination papers for his previous academic rank.");

                    ArticleWithBullets(
                        column,
                        "Article 5",
                        "The Lecturer is required to submit to the Head of the Department the following documents:",
                        new[]
                        {
                            "Course materials such as Handout/syllabuses and other supporting documents must be " +
                            "uploaded to the UNILAK online teaching platform and submitted to the Head of Department " +
                            "office before starting the class,",

                            "Final exam and marking scheme,",

                            "Continuous assessment papers: assignments/quiz/test."
                        },
                        "✓");
                });

                page.Footer()
                    .AlignCenter()
                    .Text(
                        "Accredited by Ministerial Order N° 002/09 of 09/04/2009 granting the Definitive Operating Licence.")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });

            // ---------------------------------------------------
            // PAGE 2 — Articles 6–10 and signature block
            // ---------------------------------------------------
            document.Page(page =>
            {
                ConfigurePage(page);

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    Article(
                        column,
                        "Article 6",
                        "The sheet of marks properly recorded should be submitted within fifteen days from the " +
                        "date of the exam; in case of urgency the institution is entitled to shorten this deadline.");

                    ArticleWithBullets(
                        column,
                        "Article 7",
                        "Any teaching staff member is evaluated at the end of the course and at the end of the " +
                        "academic year by the hierarchy based on:",
                        new[]
                        {
                            "His/her scientific competence (handling of the course contents, scientific articles and papers publishing);",

                            "His/her pedagogic competence (methodology, techniques, and strategies applied in transmitting efficiently the course contents);",

                            "His/her moral aptitudes (punctuality, objectivity, sense of responsibility, commitment to students' education, etc.);",

                            "In order to maintain or keep his/her course, a teacher must get at least 70% of the mark of the evaluation done by the hierarchy."
                        },
                        "•");

                    Article(
                        column,
                        "Article 8",
                        "A non-informed absence (or late informed) that brings prejudice to the students in many " +
                        "regards, disturbs the functioning of the teaching activities, and seriously spoils the " +
                        "reputation of the institution, cannot be tolerated.");

                    Article(
                        column,
                        "Article 9",
                        "The wage of the part-time employee will be set in accordance with his/her academic rank.");

                    Article(
                        column,
                        "Article 10",
                        "Each party may terminate the appointment by giving to the other party 15 days notice in " +
                        "writing. However, the University reserves the right to cancel the present contract without " +
                        "prior notice in case the employee seems to be inefficient, immoral, or absent without informing the HOD.");

                    column.Item()
                        .PaddingTop(24)
                        .Column(signatureColumn =>
                        {
                            SignatureBlock(
                                signatureColumn,
                                $"Lecturer's Name: {lecturer.UserName}",
                                lecturerSignature);

                            SignatureBlock(
                                signatureColumn,
                                "Dean of Faculty: Prof. NYESHEJA M. Enan",
                                deanSignature);

                            SignatureBlock(
                                signatureColumn,
                                "Human Resource Officer: Mr. NTAKIRUTIMANA Elison",
                                hrSignature);

                            SignatureBlock(
                                signatureColumn,
                                "DVCAR: Prof. HAKIZIMANA Emmanuel",
                                dvcarSignature);

                            SignatureBlock(
                                signatureColumn,
                                "Vice Chancellor: Prof. NGAMIJE Jean",
                                viceChancellorSignature);
                        });
                });

                page.Footer()
                    .AlignCenter()
                    .Text("Page 2 of 2")
                    .FontSize(8);
            });
        }).GeneratePdf();
    }

    // ================================================================
    // SIGNATURE BLOCK
    // ================================================================

    private void SignatureBlock(
        ColumnDescriptor column,
        string label,
        ContractSignature? signature)
    {
        var signatureImage =
            signature?.Decision == SignatureDecision.Signed
                ? GetSignatureBytes(signature.SignatureFilePath)
                : null;

        var signedDate =
            signature?.Decision == SignatureDecision.Signed &&
            signature.SignedAtUtc.HasValue
                ? signature.SignedAtUtc
                    .Value
                    .ToLocalTime()
                    .ToString("dd/MM/yyyy")
                : string.Empty;

        column.Item()
            .PaddingTop(22)
            .Column(block =>
            {
                block.Item()
                    .Text(label)
                    .Bold()
                    .FontSize(10);

                block.Item()
                    .PaddingTop(20)
                    .Row(row =>
                    {
                        row.RelativeItem(62)
                            .Column(sigCol =>
                            {
                                sigCol.Item()
                                    .Height(30)
                                    .Element(e =>
                                    {
                                        if (signatureImage is not null)
                                        {
                                            e.AlignLeft()
                                             .AlignBottom()
                                             .Width(120)
                                             .Height(26)
                                             .Image(signatureImage)
                                             .FitArea();
                                        }
                                    });

                                sigCol.Item()
                                    .PaddingTop(2)
                                    .LineHorizontal(0.6f)
                                    .LineColor("#B8B8B8");

                                sigCol.Item()
                                    .PaddingTop(3)
                                    .Text("Signature")
                                    .FontSize(8)
                                    .FontColor("#8A8A8A");
                            });

                        row.ConstantItem(28);

                        row.RelativeItem()
                            .Column(dateCol =>
                            {
                                dateCol.Item()
                                    .Height(30)
                                    .AlignBottom()
                                    .Text(signedDate)
                                    .FontSize(11);

                                dateCol.Item()
                                    .PaddingTop(2)
                                    .LineHorizontal(0.6f)
                                    .LineColor("#B8B8B8");

                                dateCol.Item()
                                    .PaddingTop(3)
                                    .Text("Date")
                                    .FontSize(8)
                                    .FontColor("#8A8A8A");
                            });
                    });
            });
    }

    // ================================================================
    // CLAIM LETTER PDF
    // ================================================================

    private byte[] CreateClaimLetterPdf(
        Claim claim,
        string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;

        var qr = CreateQrPng(publicDocumentsUrl);
        var signature =
            GetSignatureBytes(
                lecturer.SignatureFilePath);

        var submitted =
            claim.SubmittedAtUtc ??
            claim.CreatedAtUtc;

        var logo = GetLogoBytes();

        return Document.Create(document =>
            document.Page(page =>
            {
                ConfigurePage(page);

                page.Header()
                    .Element(c =>
                        OfficialHeader(
                            c,
                            logo,
                            qr,
                            "REQUEST FOR PAYMENT OF TEACHING SERVICES RENDERED"));

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    column.Item()
                        .Text(
                            submitted.ToString(
                                "dd MMMM yyyy"));

                    column.Item()
                        .Text(lecturer.UserName)
                        .Bold();

                    column.Item()
                        .Text(
                            $"Email: {lecturer.Email}");

                    if (!string.IsNullOrWhiteSpace(
                            lecturer.PhoneNumber))
                    {
                        column.Item()
                            .Text(
                                $"Tel: {lecturer.PhoneNumber}");
                    }

                    column.Item()
                        .PaddingTop(8)
                        .Text(
                            "To: The Finance Office, UNILAK");

                    column.Item()
                        .Text(
                            "Subject: Request for Payment of Teaching Services Rendered")
                        .Bold();

                    column.Item()
                        .Text(
                            "Dear Sir/Madam,");

                    column.Item()
                        .Text(
                            "I am writing to kindly request payment for the teaching services I provided at UNILAK.");

                    column.Item()
                        .Text(
                            $"I taught the course {assignment.Course.Code} - {assignment.Course.Title} during the {assignment.AcademicYear} academic year, {assignment.Semester} semester, for a total of {claim.HoursClaimed:N1} teaching hours.");

                    column.Item()
                        .Text(
                            "I respectfully request that payment for these services be processed in accordance with the University's financial procedures. This request is supported by the attached signed contract and approved academic records.");

                    column.Item()
                        .Text(
                            "Yours faithfully,");

                    if (signature is not null)
                    {
                        column.Item()
                            .Height(48)
                            .Image(signature)
                            .FitArea();
                    }

                    column.Item()
                        .Text(lecturer.UserName)
                        .Bold();

                    column.Item()
                        .PaddingTop(8)
                        .Text(
                            $"Claim reference: CLM-{claim.Id:D6}")
                        .FontSize(9)
                        .FontColor("60736A");
                });

                page.Footer()
                    .AlignCenter()
                    .Text(
                        "UNILAK official claim letter")
                    .FontSize(9);
            }))
            .GeneratePdf();
    }

    // ================================================================
    // LAYOUT HELPERS
    // ================================================================

    private static void ConfigurePage(
        PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(50);

        page.DefaultTextStyle(x =>
            x.FontFamily("Times New Roman")
             .FontSize(11)
             .LineHeight(1.4f));
    }

    private static void OfficialHeader(
        IContainer container,
        byte[]? logo,
        byte[]? qr,
        string? title = null) =>
        container.Column(col =>
        {
            col.Item()
                .Row(row =>
                {
                    if (logo is not null)
                    {
                        row.ConstantItem(90)
                            .AlignMiddle()
                            .Element(e =>
                            {
                                e.Width(72)
                                 .Height(72)
                                 .Image(logo)
                                 .FitArea();
                            });
                    }

                    row.RelativeItem()
                        .PaddingLeft(12)
                        .PaddingRight(12)
                        .Column(center =>
                        {
                            center.Item()
                                .AlignCenter()
                                .Text("UNIVERSITY OF LAY ADVENTISTS OF KIGALI")
                                .Bold()
                                .FontSize(14)
                                .FontColor("#000000");

                            center.Item()
                                .PaddingTop(4)
                                .AlignCenter()
                                .Text("P.O. Box 6392 Kigali, Rwanda")
                                .FontSize(8)
                                .FontColor("#000000");

                            center.Item()
                                .PaddingTop(1)
                                .AlignCenter()
                                .Text("Phone: +250 (0)731 743 439 / +250 (0)751 743 431")
                                .FontSize(8)
                                .FontColor("#000000");
                        });

                    if (qr is not null)
                    {
                        row.ConstantItem(56)
                            .AlignMiddle()
                            .Element(e =>
                            {
                                e.Width(46)
                                 .Height(46)
                                 .Image(qr)
                                 .FitArea();
                            });
                    }
                });

            if (!string.IsNullOrWhiteSpace(title))
            {
                col.Item()
                    .PaddingTop(10)
                    .Text(title)
                    .Bold()
                    .FontSize(12);
            }
        });

    private static void Article(
        ColumnDescriptor column,
        string heading,
        string body) =>
        column.Item()
            .PaddingTop(4)
            .Text(text =>
            {
                text.Span($"{heading}: ")
                    .Bold();

                text.Span(body);
            });

    private static void ArticleWithBullets(
        ColumnDescriptor column,
        string heading,
        string intro,
        IReadOnlyList<string> items,
        string bullet) =>
        column.Item()
            .PaddingTop(4)
            .Column(article =>
            {
                article.Item()
                    .Text(text =>
                    {
                        text.Span($"{heading}: ")
                            .Bold();

                        text.Span(intro);
                    });

                foreach (var item in items)
                {
                    article.Item()
                        .PaddingLeft(16)
                        .Text(text =>
                        {
                            text.Span($"{bullet} ")
                                .Bold();

                            text.Span(item);
                        });
                }
            });

    private static string FormatEnumLabel(
        string? enumValue)
    {
        if (string.IsNullOrWhiteSpace(enumValue))
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(
            enumValue,
            "(?<!^)([A-Z])",
            " $1");
    }

    private byte[]? GetLogoBytes()
    {
        var path = Path.Combine(
            _environment.WebRootPath,
            "images",
            "PNG_LOGO-_UNILAK-removebg-preview.png");

        return File.Exists(path)
            ? File.ReadAllBytes(path)
            : null;
    }

    private byte[]? GetSignatureBytes(
        string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var root =
            Path.GetFullPath(
                _environment.WebRootPath);

        var path =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    relativePath
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar)));

        return path.StartsWith(
                   root,
                   StringComparison.OrdinalIgnoreCase) &&
               File.Exists(path)
            ? File.ReadAllBytes(path)
            : null;
    }
}

public enum OfficialDocumentKind
{
    Contract,
    ClaimLetter
}

public sealed record GeneratedDocument(
    string FileName,
    byte[] Content);

public sealed record PublicClaimDocuments(
    int ClaimId,
    string Token,
    string ClaimReference,
    string ContractReference,
    string LecturerName,
    string CourseCode,
    string CourseTitle,
    decimal Hours,
    ClaimStatus Status);