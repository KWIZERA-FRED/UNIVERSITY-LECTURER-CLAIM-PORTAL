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

    public OfficialDocumentService(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<PublicClaimDocuments?> GetPublicDocumentsAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 32)
            return null;

        var claim = await _context.Claims
            .AsNoTracking()
            .Include(c => c.Contract)
            .Include(c => c.CourseAssignment).ThenInclude(a => a.Course)
            .Include(c => c.CourseAssignment).ThenInclude(a => a.Lecturer)
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

    public async Task<GeneratedDocument?> GenerateAsync(string token, OfficialDocumentKind kind, string publicDocumentsUrl)
    {
        var claim = await LoadClaimAsync(token);
        if (claim is null)
            return null;

        return kind switch
        {
            OfficialDocumentKind.Contract => new GeneratedDocument(
                $"contract-CON-{claim.Contract.Id:D6}.pdf",
                CreateContractPdf(claim, publicDocumentsUrl)),
            OfficialDocumentKind.ClaimLetter => new GeneratedDocument(
                $"claim-letter-CLM-{claim.Id:D6}.pdf",
                CreateClaimLetterPdf(claim, publicDocumentsUrl)),
            _ => null
        };
    }

    public byte[] CreateQrPng(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(12);
    }

    private async Task<Claim?> LoadClaimAsync(string token) =>
        await _context.Claims
            .AsNoTracking()
            .Include(c => c.Contract)
            .Include(c => c.CourseAssignment).ThenInclude(a => a.Course)
            .Include(c => c.CourseAssignment).ThenInclude(a => a.Lecturer)
            .FirstOrDefaultAsync(c => c.QrCodeToken == token);

    private byte[] CreateContractPdf(Claim claim, string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;
        var qr = CreateQrPng(publicDocumentsUrl);
        var lecturerSignature = GetSignatureBytes(lecturer.SignatureFilePath);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurePage(page);
                page.Header().Element(c => OfficialHeader(c, "EMPLOYMENT PART-TIME CONTRACT", qr));
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Kigali, {DateTime.UtcNow:dd MMMM yyyy}");
                    column.Item().Text("Between the undersigned:").Bold();
                    column.Item().Text("University of Lay Adventists of Kigali (UNILAK), represented by the Vice Chancellor, on one hand;");
                    column.Item().Text($"And {lecturer.UserName}, appointed as a {lecturer.Rank?.ToString() ?? "Lecturer"}, on the other hand.");
                    Article(column, "Article 1", $"UNILAK appoints the above-named lecturer to teach {assignment.Course.Code} - {assignment.Course.Title} for the {assignment.AcademicYear} academic year, {assignment.Semester} semester, {assignment.Session} session at the {assignment.Campus} Campus.");
                    Article(column, "Article 2", $"The lecturer is allocated {assignment.AllocatedHours:N1} contact hours. The assignment covers teaching, assessment, and related academic duties for the course/module.");
                    Article(column, "Article 3", "The lecturer shall prepare course materials, conduct teaching and assessment activities, and comply with UNILAK academic policies and procedures.");
                    Article(column, "Article 4", "The lecturer shall submit required academic records and any supporting documents requested by the University within the prescribed deadlines.");
                    Article(column, "Article 5", "Course materials, marking schemes, continuous-assessment records and other teaching documents must be submitted through the appropriate University process.");
                });
                page.Footer().AlignCenter().Text(text => { text.Span("UNILAK official contract - page 1 of 2").FontSize(9); });
            });

            document.Page(page =>
            {
                ConfigurePage(page);
                page.Header().Text("EMPLOYMENT PART-TIME CONTRACT").Bold().FontSize(14).FontColor("174D3B");
                page.Content().Column(column =>
                {
                    column.Spacing(11);
                    Article(column, "Article 6", "Properly recorded marks must be submitted within the institutional deadline following the relevant examination or assessment period.");
                    Article(column, "Article 7", "Teaching staff may be evaluated on scientific competence, pedagogical competence, professional conduct, and compliance with academic responsibilities.");
                    Article(column, "Article 8", "Uninformed absence or lateness that prejudices students and academic activities may result in action under University policies.");
                    Article(column, "Article 9", "Remuneration for part-time academic work is determined in accordance with the approved University rate and applicable financial procedures.");
                    Article(column, "Article 10", "Either party may terminate the appointment by written notice in accordance with University policy and applicable law.");
                    column.Item().PaddingTop(16).Text("Signatures").Bold().FontSize(13);
                    SignatureBlock(column, "Lecturer", lecturer.UserName, lecturerSignature, claim.Contract.SignedAtUtc);
                    SignatureBlock(column, "Dean of Faculty", "", null, null);
                    SignatureBlock(column, "Human Resource Officer", "", null, null);
                    SignatureBlock(column, "DVCAR", "", null, null);
                    SignatureBlock(column, "Vice Chancellor", "", null, null);
                });
                page.Footer().AlignCenter().Text(text => { text.Span("UNILAK official contract - page 2 of 2").FontSize(9); });
            });
        }).GeneratePdf();
    }

    private byte[] CreateClaimLetterPdf(Claim claim, string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;
        var qr = CreateQrPng(publicDocumentsUrl);
        var signature = GetSignatureBytes(lecturer.SignatureFilePath);
        var submitted = claim.SubmittedAtUtc ?? claim.CreatedAtUtc;

        return Document.Create(document => document.Page(page =>
        {
            ConfigurePage(page);
            page.Header().Element(c => OfficialHeader(c, "REQUEST FOR PAYMENT OF TEACHING SERVICES RENDERED", qr));
            page.Content().Column(column =>
            {
                column.Spacing(12);
                column.Item().Text(submitted.ToString("dd MMMM yyyy"));
                column.Item().Text(lecturer.UserName).Bold();
                column.Item().Text($"Email: {lecturer.Email}");
                if (!string.IsNullOrWhiteSpace(lecturer.PhoneNumber)) column.Item().Text($"Tel: {lecturer.PhoneNumber}");
                column.Item().PaddingTop(8).Text("To: The Finance Office, UNILAK");
                column.Item().Text("Subject: Request for Payment of Teaching Services Rendered").Bold();
                column.Item().Text("Dear Sir/Madam,");
                column.Item().Text("I am writing to kindly request payment for the teaching services I provided at UNILAK.");
                column.Item().Text($"I taught the course {assignment.Course.Code} - {assignment.Course.Title} during the {assignment.AcademicYear} academic year, {assignment.Semester} semester, for a total of {claim.HoursClaimed:N1} teaching hours.");
                column.Item().Text("I respectfully request that payment for these services be processed in accordance with the University’s financial procedures. This request is supported by the attached signed contract and approved academic records.");
                column.Item().Text("Yours faithfully,");
                if (signature is not null) column.Item().Height(48).Image(signature).FitArea();
                column.Item().Text(lecturer.UserName).Bold();
                column.Item().PaddingTop(8).Text($"Claim reference: CLM-{claim.Id:D6}").FontSize(9).FontColor("60736A");
            });
            page.Footer().AlignCenter().Text("UNILAK official claim letter").FontSize(9);
        })).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(50);
        page.DefaultTextStyle(x => x.FontFamily("Times New Roman").FontSize(11));
    }

    private static void OfficialHeader(IContainer container, string title, byte[] qr) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("UNIVERSITY OF LAY ADVENTISTS OF KIGALI").Bold().FontSize(16).FontColor("174D3B");
                c.Item().Text("UNILAK - Academic Staff Engagement & Claim Processing").FontSize(8).FontColor("60736A");
                c.Item().PaddingTop(8).Text(title).Bold().FontSize(12);
            });
            row.ConstantItem(58).Image(qr).FitArea();
        });

    private static void Article(ColumnDescriptor column, string heading, string body) =>
        column.Item().Text(text => { text.Span($"{heading}: ").Bold(); text.Span(body); });

    private static void SignatureBlock(ColumnDescriptor column, string role, string name, byte[]? signature, DateTime? signedAt) =>
        column.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem(3).Text($"{role}: {name}");
            row.RelativeItem(2).Height(32).AlignMiddle().Element(c =>
            {
                if (signature is not null) c.Image(signature).FitArea();
                else c.Text("Signature: __________________").FontSize(9);
            });
            row.RelativeItem().Text(signedAt.HasValue ? $"Date: {signedAt.Value:dd MMM yyyy}" : "Date: __________").FontSize(9);
        });

    private byte[]? GetSignatureBytes(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var root = Path.GetFullPath(_environment.WebRootPath);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? File.ReadAllBytes(path)
            : null;
    }
}

public enum OfficialDocumentKind { Contract, ClaimLetter }
public sealed record GeneratedDocument(string FileName, byte[] Content);
public sealed record PublicClaimDocuments(int ClaimId, string Token, string ClaimReference, string ContractReference, string LecturerName, string CourseCode, string CourseTitle, decimal Hours, ClaimStatus Status);