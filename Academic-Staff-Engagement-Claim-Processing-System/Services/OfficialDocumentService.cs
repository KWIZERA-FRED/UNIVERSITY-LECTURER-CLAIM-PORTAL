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
            QRCodeGenerator.ECCLevel.Q);

        return new PngByteQRCode(data)
            .GetGraphic(12);
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
            string.IsNullOrWhiteSpace(
                lecturer.GovernmentIdEncrypted)
                ? "……………………………"
                : lecturer.GovernmentIdEncrypted;

        var rankLabel =
            lecturer.Rank.HasValue
                ? FormatEnumLabel(
                    lecturer.Rank.Value.ToString())
                : "Not Yet Assigned";

        var sessionLabel =
            FormatEnumLabel(
                assignment.Session.ToString());

        var semesterLabel =
            FormatEnumLabel(
                assignment.Semester.ToString());

        var campusLabel =
            FormatEnumLabel(
                assignment.Campus.ToString());

        var signatures = await _context.ContractSignatures
            .AsNoTracking()
            .Where(s =>
                s.ContractId == claim.Contract.Id)
            .OrderBy(s =>
                s.SequenceOrder)
            .ToListAsync();

        var lecturerSignature =
            signatures.FirstOrDefault(
                s => s.SignerRole == SignerRole.Lecturer);

        var deanSignature =
            signatures.FirstOrDefault(
                s => s.SignerRole == SignerRole.Dean);

        var hrSignature =
            signatures.FirstOrDefault(
                s => s.SignerRole == SignerRole.HROfficer);

        var dvcarSignature =
            signatures.FirstOrDefault(
                s => s.SignerRole == SignerRole.DVCAR);

        var viceChancellorSignature =
            signatures.FirstOrDefault(
                s => s.SignerRole == SignerRole.ViceChancellor);

        return Document.Create(document =>
        {
            // ---------------------------------------------------
            // PAGE 1 — Header, preamble, Articles 1–5
            // ---------------------------------------------------
            document.Page(page =>
            {
                ConfigurePage(page);

                page.Header()
                    .Element(c =>
                        OfficialHeader(
                            c,
                            logo,
                            qr));

                page.Content().Column(column =>
                {
                    column.Spacing(9);

                    column.Item()
                        .AlignRight()
                        .Text(
                            $"Kigali, {DateTime.UtcNow:dd/MM/yyyy}")
                        .FontSize(11);

                    column.Item()
                        .PaddingTop(6)
                        .AlignCenter()
                        .Text("EMPLOYMENT PART-TIME CONTRACT")
                        .Bold()
                        .FontSize(15)
                        .Underline();

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

                            text.Span(
                                ", having the Academic rank of ");

                            text.Span(rankLabel)
                                .Bold();

                            text.Span(
                                " with identity card/Passport No. ");

                            text.Span(idDisplay)
                                .Italic();

                            text.Span(
                                ", on the other hand;");
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
                    .Column(col =>
                    {
                        col.Item()
                            .PaddingTop(10)
                            .AlignCenter()
                            .Text(
                                "Accredited by Ministerial Order N° 002/09 of 09/04/2009 granting the Definitive Operating Licence.")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);

                        col.Item()
                            .AlignCenter()
                            .Text("Page 1 of 2")
                            .FontSize(8);
                    });
            });

            // ---------------------------------------------------
            // PAGE 2 — Articles 6–10 and paper-style signatures
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
                        .PaddingTop(16)
                        .Column(signatureColumn =>
                        {
                            AddPaperSignatureLine(
                                signatureColumn,
                                "Lecturer’s Name:",
                                lecturer.UserName,
                                lecturerSignature);

                            AddPaperSignatureLine(
                                signatureColumn,
                                "Dean of Faculty: Prof. NYESHEJA M. Enan",
                                null,
                                deanSignature);

                            AddPaperSignatureLine(
                                signatureColumn,
                                "Human Resource Officer Mr. NTAKIRUTIMANA Elison",
                                null,
                                hrSignature);

                            AddPaperSignatureLine(
                                signatureColumn,
                                "DVCAR Prof. HAKIZIMANA Emmanuel",
                                null,
                                dvcarSignature);

                            AddPaperSignatureLine(
                                signatureColumn,
                                "Vice Chancellor Prof. NGAMIJE Jean",
                                null,
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
    // REAL-WORLD PAPER CONTRACT SIGNATURE LINE
    // ================================================================

    private void AddPaperSignatureLine(
        ColumnDescriptor column,
        string label,
        string? actualName,
        ContractSignature? signature)
    {
        var signatureImage =
            signature?.Decision == SignatureDecision.Signed
                ? GetSignatureBytes(
                    signature.SignatureFilePath)
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
            .Height(27)
            .Layers(layers =>
            {
                layers.PrimaryLayer()
                    .Row(row =>
                    {
                        row.RelativeItem(4.5f)
                            .Text(
                                $"{label} .........................")
                            .FontSize(8.8f);

                        row.RelativeItem(3.0f)
                            .Text(
                                "Signature .........................")
                            .FontSize(8.8f);

                        row.RelativeItem(2.0f)
                            .Text(
                                "Date ................")
                            .FontSize(8.8f);
                    });

                layers.Layer()
                    .Row(row =>
                    {
                        row.RelativeItem(4.5f)
                            .PaddingLeft(
                                GetNameOverlayOffset(label))
                            .TranslateY(-3)
                            .Text(
                                actualName ?? string.Empty)
                            .FontSize(8.8f);

                        row.RelativeItem(3.0f)
                            .PaddingLeft(57)
                            .TranslateY(-3)
                            .AlignBottom()
                            .Element(container =>
                            {
                                if (signatureImage is not null)
                                {
                                    container
                                        .Height(19)
                                        .Width(75)
                                        .AlignCenter()
                                        .AlignBottom()
                                        .Image(signatureImage)
                                        .FitArea();
                                }
                            });

                        row.RelativeItem(2.0f)
                            .PaddingLeft(30)
                            .TranslateY(-3)
                            .Text(signedDate)
                            .FontSize(8.8f);
                    });
            });
    }

    private static float GetNameOverlayOffset(string label)
    {
        return label switch
        {
            "Lecturer’s Name:" => 73,

            "Dean of Faculty: Prof. NYESHEJA M. Enan" => 0,

            "Human Resource Officer Mr. NTAKIRUTIMANA Elison" => 0,

            "DVCAR Prof. HAKIZIMANA Emmanuel" => 0,

            "Vice Chancellor Prof. NGAMIJE Jean" => 0,

            _ => 0
        };
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
        byte[] qr,
        string? title = null) =>
        container.Column(col =>
        {
            col.Item()
                .Row(row =>
                {
                    if (logo is not null)
                    {
                        row.ConstantItem(60)
                            .Height(60)
                            .Image(logo)
                            .FitArea();
                    }

                    row.RelativeItem()
                        .PaddingLeft(10)
                        .Column(c =>
                        {
                            c.Item()
                                .Text(
                                    "UNIVERSITY OF LAY ADVENTISTS OF KIGALI")
                                .Bold()
                                .FontSize(15)
                                .FontColor("174D3B");

                            c.Item()
                                .Text(
                                    "P.O. Box 6392 Kigali, Rwanda")
                                .FontSize(8);

                            c.Item()
                                .Text(
                                    "Phone: +250 (0)731 743 439 / +250 (0)751 743 431")
                                .FontSize(8);

                            c.Item()
                                .Text(
                                    "Website: www.unilak.ac.rw   E-mail: info@unilak.ac.rw")
                                .FontSize(8);
                        });

                    row.ConstantItem(58)
                        .Image(qr)
                        .FitArea();
                });

            col.Item()
                .PaddingTop(4)
                .LineHorizontal(2)
                .LineColor("174D3B");

            if (!string.IsNullOrWhiteSpace(title))
            {
                col.Item()
                    .PaddingTop(6)
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
                text.Justify();

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