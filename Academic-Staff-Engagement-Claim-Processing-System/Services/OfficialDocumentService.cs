using System.Globalization;
using Academic_Staff_Engagement_Claim_Processing_System.Data;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services;

public sealed partial class OfficialDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    private const string DeanName = "Prof. NYESHEJA M. Enan";
    private const string HrOfficerName = "Mr. NTAKIRUTIMANA Elison";
    private const string DvcarName = "Prof. HAKIZIMANA Emmanuel";
    private const string ViceChancellorName = "Prof. NGAMIJE Jean";
    private const string ViceChancellorPreambleName = "Prof. Jean NGAMIJE";

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

        var examSheetFileName = await _context.MarksSubmissions
            .AsNoTracking()
            .Where(ms =>
                claim.MarksSubmissionId.HasValue &&
                ms.Id == claim.MarksSubmissionId.Value &&
                ms.Status == MarksSubmissionStatus.Signed)
            .Select(ms => ms.FileName)
            .FirstOrDefaultAsync();

        return new PublicClaimDocuments(
            claim.Id,
            claim.QrCodeToken,
            $"CLM-{claim.Id:D6}",
            $"CON-{claim.ContractId:D6}",
            claim.CourseAssignment.Lecturer.UserName,
            claim.CourseAssignment.Course.Code,
            claim.CourseAssignment.Course.Title,
            claim.HoursClaimed,
            claim.Status,
            examSheetFileName);
    }

    public async Task<ExamSheetDocument?> GetExamSheetAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 32)
            return null;

        var marksSubmissionId = await _context.Claims
            .AsNoTracking()
            .Where(c =>
                c.QrCodeToken == token &&
                c.MarksSubmissionId.HasValue)
            .Select(c => c.MarksSubmissionId)
            .FirstOrDefaultAsync();

        if (!marksSubmissionId.HasValue)
            return null;

        var submission = await _context.MarksSubmissions
            .AsNoTracking()
            .Where(ms =>
                ms.Id == marksSubmissionId.Value &&
                ms.Status == MarksSubmissionStatus.Signed)
            .Select(ms => new
            {
                ms.StorageFileId,
                ms.FileName,
                ms.ContentType
            })
            .FirstOrDefaultAsync();

        if (submission is null)
            return null;

        var storedFile = await _context.StoredFiles
            .AsNoTracking()
            .Where(sf => sf.Id == submission.StorageFileId)
            .Select(sf => new
            {
                sf.Content,
                sf.ContentType,
                sf.OriginalFileName
            })
            .FirstOrDefaultAsync();

        if (storedFile is null || storedFile.Content.Length == 0)
            return null;

        var fileName =
            string.IsNullOrWhiteSpace(submission.FileName)
                ? storedFile.OriginalFileName
                : Path.GetFileName(submission.FileName);

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"exam-sheet-{marksSubmissionId.Value:D6}.xlsx";

        var contentType =
            string.IsNullOrWhiteSpace(submission.ContentType)
                ? storedFile.ContentType
                : submission.ContentType;

        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType =
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        }

        return new ExamSheetDocument(
            fileName,
            contentType,
            storedFile.Content);
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

            OfficialDocumentKind.CompletionForm => new GeneratedDocument(
                $"course-completion-form-CLM-{claim.Id:D6}.pdf",
                await CreateCompletionFormPdfAsync(
                    claim,
                    publicDocumentsUrl)),

            OfficialDocumentKind.AttendanceReport => new GeneratedDocument(
                $"attendance-report-CLM-{claim.Id:D6}.pdf",
                await CreateAttendanceReportPdfAsync(
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
            .Include(c => c.Attendance)
                .ThenInclude(a => a!.Records)
            .Include(c => c.Checklist)
            .Include(c => c.Approvals)
                .ThenInclude(a => a.ApprovedByAdminAccount)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.QrCodeToken == token);

    // ================================================================
    // CONTRACT PDF  (mirrors the UNILAK paper contract, 2 pages max)
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
                ? RankLabel(lecturer.Rank)
                : "Not Yet Assigned";

        var sessionLabel =
            FormatEnumLabel(assignment.Session.ToString());

        var semesterLabel =
            FormatEnumLabel(assignment.Semester.ToString());

        var campusLabel =
            FormatEnumLabel(assignment.Campus.ToString());

        var departmentLabel =
            SentenceCase(assignment.Course.Department);

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
            // PAGE 1 — Letterhead, preamble, Articles 1–5 (intro)
            // ---------------------------------------------------
            document.Page(page =>
            {
                ConfigurePage(page, compact: true);

                page.Content().Column(column =>
                {
                    column.Spacing(0);

                    column.Item().Element(c => ContractLetterhead(c, logo));

                    column.Item()
                        .PaddingTop(22)
                        .Text($"Kigali, {DateTime.UtcNow:dd/MM/yyyy}");

                    column.Item()
                        .PaddingTop(14)
                        .Text("EMPLOYMENT PART-TIME CONTRACT");

                    column.Item()
                        .PaddingTop(12)
                        .Text("Between the undersigned:");

                    column.Item()
                        .Text(text =>
                        {
                            text.Justify();

                            text.Span(
                                "University of Lay Adventists of Kigali (UNILAK) represented by Vice Chancellor ");

                            text.Span(ViceChancellorPreambleName)
                                .Bold();

                            text.Span(" on one hand,");
                        });

                    column.Item()
                        .PaddingTop(10)
                        .Text(text =>
                        {
                            text.Justify();

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
                        .Text("The following has been agreed:");

                    Article(
                        column,
                        "Article 1",
                        $"UNILAK employs {lecturer.UserName} as External/Internal part time lecturer in the " +
                        $"faculty of Computing and Information Sciences, Department of {departmentLabel}, " +
                        $"Session {sessionLabel}, to teach the course of {assignment.Course.Code} - {assignment.Course.Title}, " +
                        $"Academic year {assignment.AcademicYear}, {semesterLabel} semester, {campusLabel} Campus.");

                    Article(
                        column,
                        "Article 2",
                        "The number of contact hours allocated to the course/module if the course is taught " +
                        $"through face-to-face mode is {assignment.AllocatedHours:N0} hours and this includes the " +
                        "theory, practical as well as examinations. " +
                        $"The rate per hour will be {claim.Contract.RatePerHour:N0} RWF (gross).");

                    Article(
                        column,
                        "Article 3",
                        "The numbers of classes combined if the module/course is taught through online teaching " +
                        "mode: …………… and the total number of hours allocated to those combined classes taught " +
                        "by one academic staff: ……………");

                    Article(
                        column,
                        "Article 4",
                        "The employee is required to hand into the Deputy Vice Chancellor for Academic and Research " +
                        "office his/her application letter, CV, notarized copy of the degree, Equivalence if the degree " +
                        "is offered from a foreign country, as well as his/her nomination papers for his previous academic rank.");

                    Article(
                        column,
                        "Article 5",
                        "The Lecturer is required to submit to the Head of the Department the following documents:");
                });

                page.Footer()
                    .Element(f => ContractFooter(f, qr, firstPage: true));
            });

            // ---------------------------------------------------
            // PAGE 2 — Article 5 items, Articles 6–10, signatories
            // ---------------------------------------------------
            document.Page(page =>
            {
                ConfigurePage(page, compact: true);

                page.Content().Column(column =>
                {
                    column.Spacing(0);

                    BulletRow(
                        column,
                        "Course materials such as Handout/syllabuses and other supporting documents must be " +
                        "uploaded to the UNILAK online teaching platform and submitted to the Head of Department " +
                        "office before starting the class,",
                        "✓");

                    BulletRow(column, "Final exam and marking scheme,", "✓");

                    BulletRow(column, "Continuous assessment papers: assignments/quiz/test.", "✓");

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

                    column.Item().PaddingTop(10);

                    SignatoryLine(
                        column,
                        $"Lecturer's Name: {lecturer.UserName}",
                        lecturerSignature);

                    SignatoryLine(
                        column,
                        $"Dean of Faculty: {DeanName}",
                        deanSignature);

                    SignatoryLine(
                        column,
                        $"Human Resource Officer: {HrOfficerName}",
                        hrSignature);

                    SignatoryLine(
                        column,
                        $"DVCAR: {DvcarName}",
                        dvcarSignature);

                    SignatoryLine(
                        column,
                        $"Vice Chancellor: {ViceChancellorName}",
                        viceChancellorSignature);
                });

                page.Footer()
                    .Element(f => ContractFooter(f, qr, firstPage: false));
            });
        }).GeneratePdf();
    }

    // ================================================================
    // CONTRACT: LETTERHEAD + FOOTER
    // ================================================================

    private static void ContractLetterhead(
        IContainer container,
        byte[]? logo) =>
        container.Row(row =>
        {
            if (logo is not null)
            {
                row.ConstantItem(84)
                    .AlignMiddle()
                    .Width(76)
                    .Height(76)
                    .Image(logo)
                    .FitArea();
            }

            row.RelativeItem()
                .PaddingLeft(8)
                .AlignMiddle()
                .Column(col =>
                {
                    col.Item()
                        .Text("UNIVERSITY OF LAY ADVENTISTS OF KIGALI")
                        .Bold()
                        .FontSize(14)
                        .FontColor("#5F5F5F");

                    col.Item()
                        .PaddingTop(6)
                        .Row(info =>
                        {
                            info.RelativeItem()
                                .Text(t =>
                                {
                                    t.DefaultTextStyle(x =>
                                        x.FontSize(7.5f)
                                         .FontColor("#8A8A8A")
                                         .LineHeight(1.3f));

                                    t.Line("P.O. Box 6392 Kigali, Rwanda");
                                    t.Line("Phone: +250 (0)731 743 439 / +250 (0)751 743 431");
                                });

                            info.ConstantItem(140)
                                .Text(t =>
                                {
                                    t.AlignRight();

                                    t.DefaultTextStyle(x =>
                                        x.FontSize(7.5f)
                                         .FontColor("#8A8A8A")
                                         .LineHeight(1.3f));

                                    t.Line("Website: www.unilak.ac.rw");
                                    t.Line("E-mail: info@unilak.ac.rw");
                                });
                        });
                });
        });

    private static void ContractFooter(
        IContainer container,
        byte[]? qr,
        bool firstPage) =>
        container
            .PaddingTop(6)
            .Row(row =>
            {
                row.ConstantItem(24)
                    .AlignBottom()
                    .Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10));
                        t.CurrentPageNumber();
                    });

                if (firstPage)
                {
                    row.RelativeItem()
                        .AlignMiddle()
                        .Background("#8C8C8C")
                        .Padding(6)
                        .AlignCenter()
                        .Text(
                            "Accredited by Ministerial Order N° 002/09 of 09/04/2009 granting the Definitive Operating Licence.")
                        .Bold()
                        .FontSize(8)
                        .FontColor(Colors.White);

                    if (qr is not null)
                    {
                        row.ConstantItem(52)
                            .AlignRight()
                            .AlignMiddle()
                            .Width(40)
                            .Height(40)
                            .Image(qr)
                            .FitArea();
                    }
                    else
                    {
                        row.ConstantItem(52);
                    }
                }
                else
                {
                    row.RelativeItem();
                }
            });

    // ================================================================
    // SIGNATORY LINES  (one line: name ... Signature ..... Date .....)
    // ================================================================

    private void SignatoryLine(
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
                : null;

        SignatoryLineCore(column, label, signatureImage, signedDate);
    }

    // Shared by the contract and the completion form.
    private static void SignatoryLineCore(
        ColumnDescriptor column,
        string label,
        byte[]? signatureImage,
        string? signedDate,
        bool boldLabel = false,
        float topPadding = 6)
    {
        column.Item()
            .PaddingTop(topPadding)
            .ShowEntire()
            .Row(row =>
            {
                var labelSpan = row.AutoItem()
                    .AlignBottom()
                    .Text(label)
                    .FontSize(10);

                if (boldLabel)
                    labelSpan.Bold();

                row.ConstantItem(4);

                row.AutoItem()
                    .AlignBottom()
                    .Text("Signature")
                    .FontSize(10);

                row.RelativeItem()
                    .Height(28)
                    .Element(e => DottedCell(e, signatureImage, null));

                row.ConstantItem(4);

                row.AutoItem()
                    .AlignBottom()
                    .Text("Date")
                    .FontSize(10);

                row.ConstantItem(4);

                row.ConstantItem(84)
                    .Height(28)
                    .Element(e => DottedCell(e, null, signedDate));
            });
    }

    private static void DottedCell(
        IContainer container,
        byte[]? image,
        string? text) =>
        container.Layers(layers =>
        {
            layers.Layer()
                .Svg(size => BuildDotsSvg(size.Width, size.Height));

            layers.PrimaryLayer()
                .PaddingBottom(4)
                .AlignBottom()
                .Element(e =>
                {
                    if (image is not null)
                    {
                        e.AlignLeft()
                         .Width(100)
                         .Height(20)
                         .Image(image)
                         .FitArea();
                    }
                    else if (!string.IsNullOrWhiteSpace(text))
                    {
                        e.AlignLeft()
                         .Text(text)
                         .FontSize(10);
                    }
                });
        });

    private static void DottedField(
        IContainer container,
        string? text,
        float fontSize = 10) =>
        container.Layers(layers =>
        {
            layers.Layer()
                .Svg(size => BuildDotsSvg(size.Width, size.Height));

            layers.PrimaryLayer()
                .PaddingTop(2)
                .PaddingBottom(4)
                .Text(string.IsNullOrWhiteSpace(text) ? " " : text)
                .FontSize(fontSize);
        });

    private static string BuildDotsSvg(float width, float height)
    {
        var inv = CultureInfo.InvariantCulture;

        var y = (height - 3.5f).ToString("0.##", inv);

        var dots = new System.Text.StringBuilder();

        for (var x = 1f; x < width; x += 3.2f)
        {
            dots.Append(
                $"<circle cx=\"{x.ToString("0.##", inv)}\" cy=\"{y}\" r=\"0.55\" fill=\"#333333\" />");
        }

        return
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" " +
            $"width=\"{width.ToString("0.##", inv)}\" " +
            $"height=\"{height.ToString("0.##", inv)}\" " +
            $"viewBox=\"0 0 {width.ToString("0.##", inv)} {height.ToString("0.##", inv)}\">" +
            dots +
            "</svg>";
    }

    // ================================================================
    // LAYOUT HELPERS
    // ================================================================

    private static void ConfigurePage(
        PageDescriptor page,
        bool compact = false)
    {
        page.Size(PageSizes.A4);

        if (compact)
        {
            page.MarginHorizontal(50);
            page.MarginVertical(36);
        }
        else
        {
            page.Margin(50);
        }

        page.DefaultTextStyle(x =>
            x.FontFamily("Times New Roman")
             .FontSize(compact ? 11.5f : 11)
             .LineHeight(compact ? 1.25f : 1.4f));
    }

    private static void Article(
        ColumnDescriptor column,
        string heading,
        string body) =>
        column.Item()
            .PaddingTop(12)
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
            .PaddingTop(12)
            .Column(article =>
            {
                article.Item()
                    .Text(text =>
                    {
                        text.Justify();

                        text.Span($"{heading}: ")
                            .Bold();

                        text.Span(intro);
                    });

                foreach (var item in items)
                {
                    BulletRow(article, item, bullet);
                }
            });

    private static void BulletRow(
        ColumnDescriptor column,
        string item,
        string bullet) =>
        column.Item()
            .PaddingLeft(16)
            .Row(row =>
            {
                row.ConstantItem(14)
                    .Text(bullet);

                row.RelativeItem()
                    .Text(text =>
                    {
                        text.Justify();
                        text.Span(item);
                    });
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
    ClaimLetter,
    CompletionForm,
    AttendanceReport
}

public sealed record GeneratedDocument(
    string FileName,
    byte[] Content);

public sealed record ExamSheetDocument(
    string FileName,
    string ContentType,
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
    ClaimStatus Status,
    string? ExamSheetFileName = null);