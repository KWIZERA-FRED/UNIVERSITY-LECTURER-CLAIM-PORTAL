using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }


        // ============================================================
        // WELCOME EMAIL
        // ============================================================

        public async Task SendWelcomeEmailAsync(
            string recipientEmail,
            string recipientName,
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var settings = GetSettings();

            recipientName =
                string.IsNullOrWhiteSpace(recipientName)
                    ? "Staff Member"
                    : recipientName.Trim();

            username =
                string.IsNullOrWhiteSpace(username)
                    ? "your username"
                    : username.Trim();

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    settings.SenderName,
                    settings.SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName,
                    recipientEmail));

            message.Subject =
                "Welcome to the UNILAK Staff Portal";

            message.Body =
                new TextPart("plain")
                {
                    Text =
                        string.Join(
                            Environment.NewLine,

                            $"Dear {recipientName},",

                            "",

                            "Your login credentials for the UNILAK Staff Portal are:",

                            "",

                            $"Username: {username}",

                            $"Password: {password}",

                            "",

                            "Welcome to the system and thank you for being part of UNILAK.",

                            "",

                            "Kind regards,",

                            "",

                            "UNILAK Staff Engagement Portal")
                };

            await SendAsync(
                message,
                settings);

            _logger.LogInformation(
                "Welcome email sent to {RecipientEmail}.",
                recipientEmail);
        }


        // ============================================================
        // CONTRACT SIGNING NOTIFICATION
        // ============================================================

        public async Task SendContractSigningNotificationAsync(
            string recipientEmail,
            string recipientName,
            string contractReference)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var settings = GetSettings();

            recipientName =
                string.IsNullOrWhiteSpace(recipientName)
                    ? "Staff Member"
                    : recipientName.Trim();

            contractReference =
                string.IsNullOrWhiteSpace(contractReference)
                    ? "the contract"
                    : contractReference.Trim();

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    settings.SenderName,
                    settings.SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName,
                    recipientEmail));

            message.Subject =
                "Contract Ready for Signature";

            message.Body =
                new TextPart("plain")
                {
                    Text =
                        string.Join(
                            Environment.NewLine,

                            $"Dear {recipientName},",

                            "",

                            "You have a contract waiting for your electronic signature.",

                            "",

                            $"Contract: {contractReference}",

                            "",

                            "Please log in to the UNILAK Staff Engagement Portal to review and sign the contract.",

                            "",

                            "Kind regards,",

                            "",

                            "UNILAK Staff Engagement Portal")
                };

            await SendAsync(
                message,
                settings);

            _logger.LogInformation(
                "Contract signing notification sent to {RecipientEmail} for {ContractReference}.",
                recipientEmail,
                contractReference);
        }


        // ============================================================
        // MARKS SUBMISSION NOTIFICATION (Exam Office)
        // ============================================================

        public async Task SendMarksSubmissionNotificationAsync(
            string recipientEmail,
            string recipientName,
            string lecturerName,
            string courseName,
            string academicYear,
            string semester,
            string submissionReference)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var settings = GetSettings();

            recipientName =
                string.IsNullOrWhiteSpace(recipientName)
                    ? "Exam Office"
                    : recipientName.Trim();

            lecturerName =
                string.IsNullOrWhiteSpace(lecturerName)
                    ? "Lecturer"
                    : lecturerName.Trim();

            courseName =
                string.IsNullOrWhiteSpace(courseName)
                    ? "Assigned Course"
                    : courseName.Trim();

            academicYear =
                string.IsNullOrWhiteSpace(academicYear)
                    ? "N/A"
                    : academicYear.Trim();

            semester =
                string.IsNullOrWhiteSpace(semester)
                    ? "N/A"
                    : semester.Trim();

            submissionReference =
                string.IsNullOrWhiteSpace(submissionReference)
                    ? "N/A"
                    : submissionReference.Trim();

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    settings.SenderName,
                    settings.SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName,
                    recipientEmail));

            message.Subject =
                "New Marks Submission Requires Review";

            message.Body =
                new TextPart("plain")
                {
                    Text =
                        string.Join(
                            Environment.NewLine,

                            $"Dear {recipientName},",

                            "",

                            "A new marks submission has been received and is waiting for review and signing.",

                            "",

                            "Submission Details",

                            "---------------------------",

                            $"Lecturer: {lecturerName}",

                            $"Course: {courseName}",

                            $"Academic Year: {academicYear}",

                            $"Semester: {semester}",

                            $"Submission Reference: {submissionReference}",

                            "",

                            "Please log in to the UNILAK Staff Engagement Portal to review and sign the submitted marks.",

                            "",

                            "Kind regards,",

                            "",

                            "UNILAK Staff Engagement Portal")
                };

            await SendAsync(
                message,
                settings);

            _logger.LogInformation(
                "Marks submission notification sent to {RecipientEmail} for submission {SubmissionReference}.",
                recipientEmail,
                submissionReference);
        }


        // ============================================================
        // MARKS SIGNED NOTIFICATION (Lecturer)
        // ============================================================

        public async Task SendMarksSignedNotificationAsync(
            string recipientEmail,
            string recipientName,
            string courseName,
            string submissionReference)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var settings = GetSettings();

            recipientName =
                string.IsNullOrWhiteSpace(recipientName)
                    ? "Lecturer"
                    : recipientName.Trim();

            courseName =
                string.IsNullOrWhiteSpace(courseName)
                    ? "your course"
                    : courseName.Trim();

            submissionReference =
                string.IsNullOrWhiteSpace(submissionReference)
                    ? "N/A"
                    : submissionReference.Trim();

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    settings.SenderName,
                    settings.SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName,
                    recipientEmail));

            message.Subject =
                "Your Marks Submission Has Been Signed";

            message.Body =
                new TextPart("plain")
                {
                    Text =
                        string.Join(
                            Environment.NewLine,

                            $"Dear {recipientName},",

                            "",

                            "Your marks submission has been reviewed and signed by the Exam Office.",

                            "",

                            "Submission Details",

                            "---------------------------",

                            $"Course: {courseName}",

                            $"Submission Reference: {submissionReference}",

                            "",

                            "You can now log in to the UNILAK Staff Engagement Portal and submit a payment claim for this course.",

                            "",

                            "Kind regards,",

                            "",

                            "UNILAK Staff Engagement Portal")
                };

            await SendAsync(
                message,
                settings);

            _logger.LogInformation(
                "Marks signed notification sent to {RecipientEmail} for submission {SubmissionReference}.",
                recipientEmail,
                submissionReference);
        }


        // ============================================================
        // MARKS DECLINED NOTIFICATION (Lecturer)
        // ============================================================

        public async Task SendMarksDeclinedNotificationAsync(
            string recipientEmail,
            string recipientName,
            string courseName,
            string submissionReference,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var settings = GetSettings();

            recipientName =
                string.IsNullOrWhiteSpace(recipientName)
                    ? "Lecturer"
                    : recipientName.Trim();

            courseName =
                string.IsNullOrWhiteSpace(courseName)
                    ? "your course"
                    : courseName.Trim();

            submissionReference =
                string.IsNullOrWhiteSpace(submissionReference)
                    ? "N/A"
                    : submissionReference.Trim();

            reason =
                string.IsNullOrWhiteSpace(reason)
                    ? "No reason was provided."
                    : reason.Trim();

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    settings.SenderName,
                    settings.SenderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName,
                    recipientEmail));

            message.Subject =
                "Your Marks Submission Was Declined";

            message.Body =
                new TextPart("plain")
                {
                    Text =
                        string.Join(
                            Environment.NewLine,

                            $"Dear {recipientName},",

                            "",

                            "Your marks submission has been reviewed by the Exam Office and was declined.",

                            "",

                            "Submission Details",

                            "---------------------------",

                            $"Course: {courseName}",

                            $"Submission Reference: {submissionReference}",

                            "",

                            "Reason for declining",

                            "---------------------------",

                            reason,

                            "",

                            "Please correct the issue and resubmit the marks sheet through the UNILAK Staff Engagement Portal.",

                            "",

                            "Kind regards,",

                            "",

                            "UNILAK Staff Engagement Portal")
                };

            await SendAsync(
                message,
                settings);

            _logger.LogInformation(
                "Marks declined notification sent to {RecipientEmail} for submission {SubmissionReference}.",
                recipientEmail,
                submissionReference);
        }


        // ============================================================
        // SHARED HELPERS
        // ============================================================

        private EmailSettings GetSettings()
        {
            var emailSettings =
                _configuration.GetSection("EmailSettings");

            string senderName =
                emailSettings["SenderName"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SenderName is missing from configuration.");

            string senderEmail =
                emailSettings["SenderEmail"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SenderEmail is missing from configuration.");

            string smtpServer =
                emailSettings["SmtpServer"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SmtpServer is missing from configuration.");

            string smtpPortRaw =
                emailSettings["SmtpPort"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SmtpPort is missing from configuration.");

            if (!int.TryParse(
                    smtpPortRaw,
                    out int smtpPort))
            {
                throw new InvalidOperationException(
                    $"Invalid SmtpPort configured: '{smtpPortRaw}'.");
            }

            string senderPassword =
                emailSettings["SenderPassword"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SenderPassword is missing from configuration.");

            return new EmailSettings
            {
                SenderName = senderName,
                SenderEmail = senderEmail,
                SmtpServer = smtpServer,
                SmtpPort = smtpPort,
                SenderPassword = senderPassword
            };
        }


        private async Task SendAsync(
            MimeMessage message,
            EmailSettings settings)
        {
            using var smtp =
                new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(
                settings.SmtpServer,
                settings.SmtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                settings.SenderEmail,
                settings.SenderPassword);

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }


        private sealed class EmailSettings
        {
            public string SenderName { get; init; } = string.Empty;
            public string SenderEmail { get; init; } = string.Empty;
            public string SmtpServer { get; init; } = string.Empty;
            public int SmtpPort { get; init; }
            public string SenderPassword { get; init; } = string.Empty;
        }
    }
}