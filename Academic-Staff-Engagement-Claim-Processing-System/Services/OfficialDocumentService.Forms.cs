using System.Globalization;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services;

// Claim letter, course completion approval form and lecturer
// attendance report. Split out of OfficialDocumentService.cs.
public sealed partial class OfficialDocumentService
{
    private const string Blank = "……………";

    private static readonly string[] CompletionChecklistItems =
    {
        "Lecturer's notes/Materials are properly uploaded on E-Learning platform",
        "Individual work, Group work and Participation are available on E-Learning platform",
        "Marks are available in MIS",
        "Marks are approved by HoD",
        "A hard copy mark sheets are submitted in the office of HoD and registrar's office",
        "Copy of Exam and Marking scheme are available in the office HoD",
        "List of students' class attendance",
        "Scripts of Exams (CAT and Final exams) are returned to department/examination office."
    };

    private sealed record FormSigner(
        string? Name,
        byte[]? Signature,
        DateTime? SignedAtUtc);

    // ================================================================
    // CLAIM LETTER  (mirrors the paper "Request for Payment" letter)
    // ================================================================

    private byte[] CreateClaimLetterPdf(
        Claim claim,
        string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;
        var course = assignment.Course;

        var qr = CreateQrPng(publicDocumentsUrl);

        var signature =
            GetSignatureBytes(lecturer.SignatureFilePath);

        var submitted =
            (claim.SubmittedAtUtc ?? claim.CreatedAtUtc)
                .ToLocalTime();

        var (start, end) = GetTeachingPeriod(claim);

        return Document.Create(document =>
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(60);
                page.MarginVertical(50);

                page.DefaultTextStyle(x =>
                    x.FontFamily("Times New Roman")
                     .FontSize(12)
                     .LineHeight(1.3f));

                page.Content().Column(column =>
                {
                    column.Spacing(11);

                    column.Item()
                        .Text(submitted.ToString(
                            "MMMM d, yyyy",
                            CultureInfo.InvariantCulture))
                        .Bold();

                    column.Item()
                        .PaddingTop(14)
                        .Column(identity =>
                        {
                            identity.Item()
                                .Text(lecturer.UserName)
                                .Bold();

                            identity.Item()
                                .Text($"Email: {lecturer.Email}");

                            if (!string.IsNullOrWhiteSpace(lecturer.PhoneNumber))
                            {
                                identity.Item()
                                    .Text($"Tel: {lecturer.PhoneNumber}");
                            }
                        });

                    column.Item()
                        .PaddingTop(8)
                        .Text(text =>
                        {
                            text.Span("To: ").Bold();
                            text.Span("The Finance Office UNILAK");
                        });

                    column.Item()
                        .Text("Subject: Request for Payment of Teaching Services Rendered")
                        .Bold();

                    column.Item()
                        .Text("Dear Sir/Madam,");

                    column.Item()
                        .Text(text =>
                        {
                            text.Justify();

                            text.Span(
                                "I am writing to kindly request payment for the teaching services I provided at ");

                            text.Span("UNILAK").Bold();

                            text.Span(".");
                        });

                    column.Item()
                        .Text(text =>
                        {
                            text.Justify();

                            text.Span("I taught the course ");

                            text.Span(course.Title).Bold();

                            if (start.HasValue && end.HasValue)
                            {
                                text.Span(
                                    $" from {Dmy(start.Value)} - {Dmy(end.Value)}");
                            }

                            text.Span(", the course carried ");

                            text.Span($"{Num(course.CreditHours)} credits").Bold();

                            text.Span(" and was delivered over a total of ");

                            text.Span($"{Num(claim.HoursClaimed)} teaching hours").Bold();

                            text.Span(".");
                        });

                    column.Item()
                        .Text(text =>
                        {
                            text.Justify();

                            text.Span(
                                "I respectfully request that the payment for these services be processed in " +
                                "accordance with the university's financial procedures.");
                        });

                    column.Item()
                        .Text("For your convenience, my bank details are provided below:");

                    column.Item()
                        .PaddingTop(2)
                        .Column(bank =>
                        {
                            bank.Spacing(4);

                            BankLine(bank, "Bank Name:", null);
                            BankLine(bank, "Account Number:", null);
                            BankLine(bank, "Account Name:", lecturer.UserName);
                        });

                    column.Item()
                        .PaddingTop(6)
                        .Text("Yours faithfully,");

                    if (signature is not null)
                    {
                        column.Item()
                            .Height(48)
                            .AlignLeft()
                            .Image(signature)
                            .FitArea();
                    }

                    column.Item()
                        .Text(lecturer.UserName)
                        .Bold();
                });

                page.Footer()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .AlignBottom()
                            .Text($"Claim reference: CLM-{claim.Id:D6}")
                            .FontSize(8)
                            .FontColor("#777777");

                        row.ConstantItem(46)
                            .AlignRight()
                            .Width(42)
                            .Height(42)
                            .Image(qr)
                            .FitArea();
                    });
            }))
            .GeneratePdf();
    }

    private static void BankLine(
        ColumnDescriptor column,
        string label,
        string? value) =>
        column.Item()
            .Row(row =>
            {
                row.AutoItem()
                    .PaddingTop(2)
                    .Text(label)
                    .Bold();

                row.ConstantItem(6);

                row.ConstantItem(250)
                    .Element(e => DottedField(e, value, 12));
            });

    // ================================================================
    // COURSE COMPLETION APPROVAL FORM
    // ================================================================

    private async Task<byte[]> CreateCompletionFormPdfAsync(
        Claim claim,
        string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;
        var course = assignment.Course;

        var logo = GetLogoBytes();
        var qr = CreateQrPng(publicDocumentsUrl);

        var faculty = ResolveFaculty(course.Department, lecturer.Faculty);

        var facultyLabel =
            faculty.HasValue
                ? SentenceCase(faculty.Value.ToString())
                : string.Empty;

        var departmentLabel = SentenceCase(course.Department);
        var rankText = RankLabel(lecturer.Rank);

        var hod = await ResolveSignerAsync(claim, ApprovalRole.HOD, faculty);
        var dean = await ResolveSignerAsync(claim, ApprovalRole.Dean, faculty);
        var quality = await ResolveSignerAsync(claim, ApprovalRole.DirectorOfQuality, faculty);
        var dvcar = await ResolveSignerAsync(claim, ApprovalRole.DVCAR, faculty);

        var lecturerSignature =
            GetSignatureBytes(lecturer.SignatureFilePath);

        var lecturerDate =
            Dmy((claim.SubmittedAtUtc ?? claim.CreatedAtUtc).ToLocalTime());

        var answers = GetChecklistAnswers(claim);

        return Document.Create(document =>
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);

                page.DefaultTextStyle(x =>
                    x.FontFamily("Times New Roman")
                     .FontSize(10)
                     .LineHeight(1.2f));

                page.Content().Column(column =>
                {
                    column.Spacing(0);

                    column.Item()
                        .Element(c => ContractLetterhead(c, logo));

                    column.Item()
                        .PaddingTop(4)
                        .AlignCenter()
                        .Text("P.O BOX 6392 KIGALI")
                        .Bold()
                        .FontSize(9);

                    column.Item()
                        .PaddingTop(6)
                        .AlignCenter()
                        .Text("APPROVAL FOR COURSE COMPLETION AND CLAIM FOR PAYMENT FORM")
                        .Bold()
                        .Underline()
                        .FontSize(11);

                    // ---- Lecturer / course details ----

                    column.Item()
                        .PaddingTop(10)
                        .Row(row =>
                        {
                            row.AutoItem()
                                .PaddingTop(2)
                                .Text("Names of Lecturer:");

                            row.ConstantItem(4);

                            row.RelativeItem(3)
                                .Element(e => DottedField(e, lecturer.UserName));

                            row.ConstantItem(10);

                            row.AutoItem()
                                .PaddingTop(2)
                                .Text("RSSB's Number:");

                            row.ConstantItem(4);

                            row.RelativeItem(2)
                                .Element(e => DottedField(e, lecturer.RssbNumber));
                        });

                    FormField(column, "Academic Rank:", rankText);
                    FormField(column, "Academic year:", assignment.AcademicYear);
                    FormField(column, "Faculty:", facultyLabel);
                    FormField(column, "Department:", departmentLabel);
                    FormField(column, "Course Taught:", $"{course.Code} - {course.Title}");
                    FormField(column, "Number of credits allocated to the course:", Num(course.CreditHours));
                    FormField(column, "Contact number of hours covered:", Num(claim.HoursClaimed));

                    // ---- Checklist ----

                    column.Item()
                        .PaddingTop(10)
                        .Text("Check list of minimum course requirements:")
                        .Bold();

                    column.Item()
                        .PaddingTop(4)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28);
                                c.RelativeColumn();
                                c.ConstantColumn(34);
                                c.ConstantColumn(34);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(FormHeaderCell)
                                    .AlignCenter().Text("SN").Bold().FontSize(9);

                                header.Cell().Element(FormHeaderCell)
                                    .Text("Description").Bold().FontSize(9);

                                header.Cell().Element(FormHeaderCell)
                                    .AlignCenter().Text("Yes").Bold().FontSize(9);

                                header.Cell().Element(FormHeaderCell)
                                    .AlignCenter().Text("No").Bold().FontSize(9);
                            });

                            for (var i = 0; i < CompletionChecklistItems.Length; i++)
                            {
                                var index = i;

                                table.Cell().Element(FormBodyCell)
                                    .AlignCenter()
                                    .Text((index + 1).ToString(CultureInfo.InvariantCulture))
                                    .FontSize(9);

                                table.Cell().Element(FormBodyCell)
                                    .Text(CompletionChecklistItems[index])
                                    .FontSize(9);

                                table.Cell().Element(FormBodyCell)
                                    .Element(e => Tick(
                                        e,
                                        answers is not null && answers[index],
                                        cross: false));

                                table.Cell().Element(FormBodyCell)
                                    .Element(e => Tick(
                                        e,
                                        answers is not null && !answers[index],
                                        cross: true));
                            }
                        });

                    // ---- Signatures ----

                    column.Item().PaddingTop(10);

                    SignatoryLineCore(
                        column,
                        $"Names of Lecturer: {lecturer.UserName}",
                        lecturerSignature,
                        lecturerDate,
                        boldLabel: true,
                        topPadding: 2);

                    SignatoryLineCore(
                        column,
                        $"HOD: {hod.Name ?? Blank}",
                        hod.Signature,
                        DmyUtc(hod.SignedAtUtc),
                        boldLabel: true,
                        topPadding: 2);

                    SignatoryLineCore(
                        column,
                        $"Dean: {dean.Name ?? Blank}",
                        dean.Signature,
                        DmyUtc(dean.SignedAtUtc),
                        boldLabel: true,
                        topPadding: 2);

                    CampusCoordinatorLine(column);

                    SignatoryLineCore(
                        column,
                        $"Dir. of Quality: {quality.Name ?? Blank}",
                        quality.Signature,
                        DmyUtc(quality.SignedAtUtc),
                        boldLabel: true,
                        topPadding: 2);

                    SignatoryLineCore(
                        column,
                        $"DVCAR: {dvcar.Name ?? Blank}",
                        dvcar.Signature,
                        DmyUtc(dvcar.SignedAtUtc),
                        boldLabel: true,
                        topPadding: 2);

                    // ---- N.B. ----

                    column.Item()
                        .PaddingTop(10)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Column(notes =>
                                {
                                    notes.Item()
                                        .Text(text =>
                                        {
                                            text.Span("N.B:").Bold().Underline();

                                            text.Span(
                                                " - This form should be completed and signed immediately at the end " +
                                                "of each module as evidence that the course has been successfully completed.");
                                        });

                                    notes.Item()
                                        .PaddingLeft(20)
                                        .Text(
                                            "- The faculty/ Department should keep a copy for each completed module in the folder.");
                                });

                            row.ConstantItem(56)
                                .AlignRight()
                                .AlignMiddle()
                                .Width(48)
                                .Height(48)
                                .Image(qr)
                                .FitArea();
                        });
                });
            }))
            .GeneratePdf();
    }

    private static void FormField(
        ColumnDescriptor column,
        string label,
        string? value) =>
        column.Item()
            .PaddingTop(4)
            .Row(row =>
            {
                row.AutoItem()
                    .PaddingTop(2)
                    .Text(label);

                row.ConstantItem(4);

                row.RelativeItem()
                    .Element(e => DottedField(e, value));
            });

    private static void CampusCoordinatorLine(ColumnDescriptor column) =>
        column.Item()
            .PaddingTop(2)
            .Row(row =>
            {
                row.AutoItem()
                    .AlignBottom()
                    .Text("Campus Coord.")
                    .FontSize(10)
                    .Bold();

                row.ConstantItem(6);

                row.AutoItem()
                    .AlignBottom()
                    .Text("N/A")
                    .FontSize(10);

                row.ConstantItem(18);

                row.AutoItem()
                    .AlignBottom()
                    .Text("Signature")
                    .FontSize(10);

                row.ConstantItem(6);

                row.AutoItem()
                    .AlignBottom()
                    .Text("N/A")
                    .FontSize(10);

                row.ConstantItem(18);

                row.AutoItem()
                    .AlignBottom()
                    .Text("Date")
                    .FontSize(10);

                row.RelativeItem()
                    .Height(28)
                    .Element(e => DottedCell(e, null, null));
            });

    private static IContainer FormHeaderCell(IContainer container) =>
        container
            .Border(0.7f)
            .BorderColor("#222222")
            .Background("#F1F1F1")
            .Padding(4);

    private static IContainer FormBodyCell(IContainer container) =>
        container
            .Border(0.7f)
            .BorderColor("#222222")
            .Padding(4);

    private static void Tick(
        IContainer container,
        bool show,
        bool cross)
    {
        if (!show)
            return;

        var path = cross
            ? "<path d=\"M2 2 L9 9 M9 2 L2 9\" fill=\"none\" stroke=\"#111111\" stroke-width=\"1.6\" stroke-linecap=\"round\" />"
            : "<path d=\"M1.5 6 L4.5 9 L9.5 1.8\" fill=\"none\" stroke=\"#111111\" stroke-width=\"1.6\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />";

        container
            .AlignCenter()
            .AlignMiddle()
            .Width(11)
            .Height(11)
            .Svg(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"11\" height=\"11\" viewBox=\"0 0 11 11\">" +
                path +
                "</svg>");
    }

    // The HOD page only lets a claim be approved when all eight items
    // are confirmed, so an approved HOD step means eight "Yes".
    private static bool[]? GetChecklistAnswers(Claim claim)
    {
        if (claim.Checklist is { } saved)
        {
            return new[]
            {
                saved.NotesUploadedToELearning,
                saved.IndividualGroupWorkOnELearning,
                saved.MarksAvailableInMIS,
                saved.MarksApprovedByHOD,
                saved.HardCopySubmittedToHodAndRegistrar,
                saved.ExamAndMarkingSchemeAvailable,
                saved.ClassAttendanceListAvailable,
                saved.ExamScriptsReturned
            };
        }

        var hodApproved =
            claim.Approvals.Any(a =>
                a.ApprovalRole == ApprovalRole.HOD &&
                a.Decision == ApprovalDecision.Approved);

        return hodApproved
            ? Enumerable.Repeat(true, CompletionChecklistItems.Length).ToArray()
            : null;
    }

    // ================================================================
    // LECTURER ATTENDANCE REPORT
    // ================================================================

    private async Task<byte[]> CreateAttendanceReportPdfAsync(
        Claim claim,
        string publicDocumentsUrl)
    {
        var assignment = claim.CourseAssignment;
        var lecturer = assignment.Lecturer;
        var course = assignment.Course;
        var attendance = claim.Attendance;

        var logo = GetLogoBytes();
        var qr = CreateQrPng(publicDocumentsUrl);

        var records =
            attendance?.Records
                .OrderBy(r => r.SessionDate)
                .ThenBy(r => r.Id)
                .ToList()
            ?? new List<ClaimAttendanceRecord>();

        var totalSessions = attendance?.TotalSessions ?? records.Count;
        var attendedSessions = attendance?.AttendedSessions ?? records.Count(r => r.Attended);

        var startDate = records.Count > 0 ? Dmy(records[0].SessionDate) : string.Empty;
        var endDate = records.Count > 0 ? Dmy(records[^1].SessionDate) : string.Empty;

        var faculty = ResolveFaculty(course.Department, lecturer.Faculty);
        var hod = await ResolveSignerAsync(claim, ApprovalRole.HOD, faculty);

        var lecturerSignature =
            GetSignatureBytes(lecturer.SignatureFilePath);

        var lecturerDate =
            Dmy((claim.SubmittedAtUtc ?? claim.CreatedAtUtc).ToLocalTime());

        var lecturerName = attendance?.LecturerName ?? lecturer.UserName;

        return Document.Create(document =>
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(30);

                page.DefaultTextStyle(x =>
                    x.FontSize(9)
                     .LineHeight(1.2f));

                page.Content().Column(column =>
                {
                    column.Spacing(0);

                    column.Item()
                        .Element(c => ReportLetterhead(c, logo));

                    column.Item()
                        .PaddingTop(12)
                        .AlignCenter()
                        .Text("LECTURER'S ATTENDANCE REPORT")
                        .Bold()
                        .FontSize(10.5f)
                        .FontColor("#8A8A8A");

                    // ---- Field grid ----

                    column.Item()
                        .PaddingTop(10)
                        .Row(row =>
                        {
                            row.RelativeItem(3)
                                .Column(left =>
                                {
                                    InfoRow(left, "LECTURER'S NAME", lecturerName);
                                    InfoRow(left, "ACADEMIC YEAR", assignment.AcademicYear);
                                    InfoRow(left, "MODULE NAME", course.Title);
                                    InfoRow(left, "MODULE CODE", course.Code);
                                    InfoRow(left, "SESSION", FormatEnumLabel(assignment.Session.ToString()));
                                });

                            row.ConstantItem(24);

                            row.RelativeItem(2)
                                .Column(right =>
                                {
                                    InfoRow(right, "START DATE", startDate);
                                    InfoRow(right, "END DATE", endDate);
                                    InfoRow(right, "SEMESTER", FormatEnumLabel(assignment.Semester.ToString()));
                                    InfoRow(right, "CAMPUS", FormatEnumLabel(assignment.Campus.ToString()));
                                });
                        });

                    // ---- Session log ----

                    column.Item()
                        .PaddingTop(12)
                        .Text("SESSION LOG")
                        .Bold()
                        .FontSize(8);

                    column.Item()
                        .PaddingTop(4)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(2.4f);
                                c.RelativeColumn(2f);
                                c.RelativeColumn(2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(ReportHeaderCell)
                                    .Text("SESSION");

                                header.Cell().Element(ReportHeaderCell)
                                    .AlignCenter().Text("DATE");

                                header.Cell().Element(ReportHeaderCell)
                                    .AlignCenter().Text("ATTENDANCE");
                            });

                            foreach (var record in records)
                            {
                                table.Cell().Element(ReportBodyCell)
                                    .Text(record.SessionTitle);

                                table.Cell().Element(ReportBodyCell)
                                    .AlignCenter()
                                    .Text(Dmy(record.SessionDate));

                                table.Cell().Element(ReportBodyCell)
                                    .AlignCenter()
                                    .Text(record.Attended ? "Present" : "Absent");
                            }

                            if (records.Count == 0)
                            {
                                table.Cell().ColumnSpan(3)
                                    .Element(ReportBodyCell)
                                    .AlignCenter()
                                    .Text("No attendance record is attached to this claim.");
                            }
                        });

                    // ---- Totals ----

                    column.Item()
                        .PaddingTop(6)
                        .Border(0.6f)
                        .BorderColor("#555555")
                        .Padding(6)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text(text =>
                                {
                                    text.Span("TOTAL SESSIONS: ").Bold().FontSize(8);
                                    text.Span(totalSessions.ToString(CultureInfo.InvariantCulture));
                                });

                            row.RelativeItem()
                                .Text(text =>
                                {
                                    text.Span("SESSIONS ATTENDED: ").Bold().FontSize(8);
                                    text.Span(attendedSessions.ToString(CultureInfo.InvariantCulture));
                                });

                            row.RelativeItem()
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.Span("TOTAL HOURS COVERED: ").Bold().FontSize(8);
                                    text.Span(claim.HoursClaimed.ToString("N2", CultureInfo.InvariantCulture));
                                });
                        });

                    column.Item()
                        .PaddingTop(6)
                        .Border(0.6f)
                        .BorderColor("#555555")
                        .Padding(6)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text(text =>
                                {
                                    text.Span("CREDITS: ").Bold().FontSize(8);
                                    text.Span(Num(course.CreditHours));
                                });

                            row.RelativeItem()
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.Span("ALLOCATED CONTACT HOURS: ").Bold().FontSize(8);
                                    text.Span(Num(assignment.AllocatedHours));
                                });
                        });

                    // ---- Signatures ----

                    column.Item()
                        .PaddingTop(16)
                        .ShowEntire()
                        .Row(row =>
                        {
                            row.RelativeItem(3)
                                .Column(left =>
                                {
                                    left.Item()
                                        .Text("LECTURER'S SIGNATURE")
                                        .Bold()
                                        .FontSize(7.5f);

                                    left.Item()
                                        .Height(34)
                                        .Element(e => SignatureImage(e, lecturerSignature));

                                    left.Item()
                                        .LineHorizontal(0.6f)
                                        .LineColor("#555555");

                                    left.Item()
                                        .PaddingTop(4)
                                        .Text(text =>
                                        {
                                            text.Span("DATE: ").Bold().FontSize(7.5f);
                                            text.Span(lecturerDate);
                                        });

                                    left.Item()
                                        .PaddingTop(14)
                                        .Text("HOD NAMES")
                                        .Bold()
                                        .FontSize(7.5f);

                                    left.Item()
                                        .PaddingTop(2)
                                        .PaddingBottom(2)
                                        .Text(string.IsNullOrWhiteSpace(hod.Name) ? " " : hod.Name);

                                    left.Item()
                                        .LineHorizontal(0.6f)
                                        .LineColor("#555555");

                                    left.Item()
                                        .PaddingTop(14)
                                        .Text("SIGNATURE AND STAMP")
                                        .Bold()
                                        .FontSize(7.5f);

                                    left.Item()
                                        .Height(34)
                                        .Element(e => SignatureImage(e, hod.Signature));

                                    left.Item()
                                        .LineHorizontal(0.6f)
                                        .LineColor("#555555");
                                });

                            row.ConstantItem(24);

                            row.ConstantItem(96)
                                .AlignBottom()
                                .Column(right =>
                                {
                                    right.Item()
                                        .Width(84)
                                        .Height(84)
                                        .Image(qr)
                                        .FitArea();

                                    if (attendance is not null)
                                    {
                                        right.Item()
                                            .PaddingTop(2)
                                            .Text($"MIS Ref: {attendance.MisReference}")
                                            .FontSize(6)
                                            .FontColor("#777777");
                                    }
                                });
                        });
                });
            }))
            .GeneratePdf();
    }

    private static void ReportLetterhead(
        IContainer container,
        byte[]? logo) =>
        container.Column(col =>
        {
            col.Item()
                .Row(row =>
                {
                    if (logo is not null)
                    {
                        row.ConstantItem(84)
                            .AlignMiddle()
                            .Width(72)
                            .Height(72)
                            .Image(logo)
                            .FitArea();
                    }

                    row.RelativeItem()
                        .AlignMiddle()
                        .Column(center =>
                        {
                            center.Item()
                                .AlignCenter()
                                .Text("UNIVERSITY OF")
                                .FontFamily("Times New Roman")
                                .Bold()
                                .FontSize(17)
                                .FontColor("#333333");

                            center.Item()
                                .AlignCenter()
                                .Text("LAY ADVENTISTS OF KIGALI")
                                .FontFamily("Times New Roman")
                                .Bold()
                                .FontSize(17)
                                .FontColor("#333333");

                            center.Item()
                                .PaddingTop(2)
                                .AlignCenter()
                                .Text("PO Box 6392 Kigali, Rwanda")
                                .FontSize(7.5f);
                        });

                    row.ConstantItem(110)
                        .AlignMiddle()
                        .Text(t =>
                        {
                            t.AlignRight();

                            t.DefaultTextStyle(x =>
                                x.FontSize(6.5f)
                                 .FontColor("#555555")
                                 .LineHeight(1.25f));

                            t.Line("Contact to:");
                            t.Line("+250 (0)731 743 439");
                            t.Line("+250 (0)751 743 431");
                            t.Line("www.unilak.ac.rw");
                            t.Line("info@unilak.ac.rw");
                        });
                });

            col.Item()
                .PaddingTop(6)
                .LineHorizontal(2.5f)
                .LineColor("#333333");
        });

    private static void InfoRow(
        ColumnDescriptor column,
        string label,
        string? value) =>
        column.Item()
            .PaddingTop(4)
            .Row(row =>
            {
                row.ConstantItem(86)
                    .AlignBottom()
                    .PaddingBottom(2)
                    .Text(label)
                    .Bold()
                    .FontSize(7);

                row.RelativeItem()
                    .BorderBottom(0.5f)
                    .BorderColor("#9A9A9A")
                    .PaddingBottom(2)
                    .Text(string.IsNullOrWhiteSpace(value) ? " " : value)
                    .FontSize(9);
            });

    private static IContainer ReportHeaderCell(IContainer container) =>
        container
            .Border(0.6f)
            .BorderColor("#555555")
            .Background("#EEEEEE")
            .Padding(3)
            .DefaultTextStyle(x => x.Bold().FontSize(7.5f).FontColor("#555555"));

    private static IContainer ReportBodyCell(IContainer container) =>
        container
            .Border(0.6f)
            .BorderColor("#555555")
            .Padding(3)
            .DefaultTextStyle(x => x.FontSize(8.5f));

    private static void SignatureImage(
        IContainer container,
        byte[]? image)
    {
        if (image is null)
            return;

        container
            .AlignLeft()
            .AlignBottom()
            .Width(110)
            .Height(30)
            .Image(image)
            .FitArea();
    }

    // ================================================================
    // SIGNER LOOKUP
    // ================================================================

    // Who signed this step (with signature and date) or, if it is
    // still pending, who is expected to sign it.
    private async Task<FormSigner> ResolveSignerAsync(
        Claim claim,
        ApprovalRole role,
        Faculty? faculty)
    {
        var approval =
            claim.Approvals.FirstOrDefault(a => a.ApprovalRole == role);

        var account = approval?.ApprovedByAdminAccount;

        if (approval is not null &&
            approval.Decision == ApprovalDecision.Approved &&
            account is not null)
        {
            return new FormSigner(
                account.UserName,
                GetSignatureBytes(account.SignatureFilePath),
                approval.DecidedAtUtc);
        }

        string? expectedName = null;

        switch (role)
        {
            case ApprovalRole.HOD:

                if (faculty.HasValue)
                {
                    var hodFaculty = faculty.Value;

                    expectedName = await _context.Hods
                        .AsNoTracking()
                        .Where(h => h.IsActive && h.Faculty == hodFaculty)
                        .Select(h => h.UserName)
                        .FirstOrDefaultAsync();
                }

                break;

            case ApprovalRole.Dean:

                expectedName = await _context.Deans
                    .AsNoTracking()
                    .Where(d => d.IsActive)
                    .Select(d => d.UserName)
                    .FirstOrDefaultAsync()
                    ?? DeanName;

                break;

            case ApprovalRole.DirectorOfQuality:

                expectedName = await _context.ManagementAccounts
                    .AsNoTracking()
                    .Where(m =>
                        m.IsActive &&
                        m.Title == ManagementTitle.DirectorOfQuality)
                    .Select(m => m.UserName)
                    .FirstOrDefaultAsync();

                break;

            case ApprovalRole.DVCAR:

                expectedName = await _context.ManagementAccounts
                    .AsNoTracking()
                    .Where(m =>
                        m.IsActive &&
                        m.Title == ManagementTitle.DVCAR)
                    .Select(m => m.UserName)
                    .FirstOrDefaultAsync()
                    ?? DvcarName;

                break;
        }

        return new FormSigner(expectedName, null, null);
    }

    // ================================================================
    // SMALL HELPERS
    // ================================================================

    private static (DateTime? Start, DateTime? End) GetTeachingPeriod(
        Claim claim)
    {
        var dates =
            claim.Attendance?.Records
                .Select(r => r.SessionDate)
                .ToList();

        if (dates is { Count: > 0 })
            return (dates.Min(), dates.Max());

        return (
            claim.Contract.StartDateUtc,
            claim.Contract.EndDateUtc);
    }

    private static Faculty? ResolveFaculty(
        string? courseDepartment,
        Faculty? fallback)
    {
        if (Enum.TryParse<Department>(
                courseDepartment,
                true,
                out var department))
        {
            foreach (var faculty in Enum.GetValues<Faculty>())
            {
                if (FacultyDepartments.IsValidDepartment(faculty, department))
                    return faculty;
            }
        }

        return fallback;
    }

    private static string RankLabel(LecturerRank? rank) =>
        rank switch
        {
            LecturerRank.TutorialAssistant => "Tutorial Assistant",
            LecturerRank.AssistantLecturer => "Assistant Lecturer",
            LecturerRank.LecturerWithMasters => "Lecturer with Master's",
            LecturerRank.LecturerWithPhD => "Lecturer with PhD",
            LecturerRank.SeniorLecturer => "Senior Lecturer",
            LecturerRank.AssistantProfessor => "Assistant Professor",
            LecturerRank.Professor => "Professor",
            _ => string.Empty
        };

    // "ComputingAndInformationSciences" -> "Computing and Information Sciences"
    private static string SentenceCase(string? value) =>
        FormatEnumLabel(value).Replace(" And ", " and ");

    private static string Dmy(DateTime value) =>
        value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string? DmyUtc(DateTime? utc) =>
        utc.HasValue
            ? Dmy(utc.Value.ToLocalTime())
            : null;

    private static string Num(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}