using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Academic_Staff_Engagement_Claim_Processing_System.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendWelcomeEmailAsync(
            string recipientEmail,
            string recipientName,
            string username,
            string password)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));

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

            if (!int.TryParse(smtpPortRaw, out int smtpPort))
            {
                throw new InvalidOperationException(
                    $"Invalid SmtpPort configured: '{smtpPortRaw}'.");
            }

            string senderPassword =
                emailSettings["SenderPassword"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SenderPassword is missing from configuration.");

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    senderName,
                    senderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName ?? string.Empty,
                    recipientEmail));

            message.Subject =
                "Welcome to the UNILAK Staff Portal";

            message.Body = new TextPart("plain")
            {
                Text =
                    "Dear " + (recipientName ?? "Lecturer") + "," +
                    Environment.NewLine +
                    Environment.NewLine +

                    "Your login credentials for the UNILAK Staff Portal are:" +
                    Environment.NewLine +
                    Environment.NewLine +

                    "Username: " + (username ?? string.Empty) +
                    Environment.NewLine +

                    "Password: " + (password ?? string.Empty) +
                    Environment.NewLine +
                    Environment.NewLine +

                    "Welcome to the system and thank you for being part of UNILAK." +
                    Environment.NewLine +
                    Environment.NewLine +

                    "Kind regards," +
                    Environment.NewLine +
                    "UNILAK Staff Engagement Portal"
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpServer,
                smtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                senderEmail,
                senderPassword);

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }

        public async Task SendContractNotificationAsync(
            string recipientEmail,
            string recipientName,
            string contractNumber,
            string courseTitle,
            string academicYear,
            string semester)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));

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

            if (!int.TryParse(smtpPortRaw, out int smtpPort))
            {
                throw new InvalidOperationException(
                    $"Invalid SmtpPort configured: '{smtpPortRaw}'.");
            }

            string senderPassword =
                emailSettings["SenderPassword"]
                ?? throw new InvalidOperationException(
                    "EmailSettings:SenderPassword is missing from configuration.");

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    senderName,
                    senderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName ?? string.Empty,
                    recipientEmail));

            message.Subject =
                "UNILAK Part-Time Lecturer Contract Ready for Signature";

            string body =
                "Dear " + (recipientName ?? "Lecturer") +
                "," +
                Environment.NewLine +
                Environment.NewLine +

                "A part-time lecturer contract has been created for you " +
                "in the UNILAK Staff Portal." +
                Environment.NewLine +
                Environment.NewLine +

                "Contract Number: " +
                (contractNumber ?? "Not specified") +
                Environment.NewLine +

                "Course: " +
                (courseTitle ?? "Not specified") +
                Environment.NewLine +

                "Academic Year: " +
                (academicYear ?? "Not specified") +
                Environment.NewLine +

                "Semester: " +
                (semester ?? "Not specified") +
                Environment.NewLine +
                Environment.NewLine +

                "Please log in to the UNILAK Staff Portal to review " +
                "the contract and complete your electronic signature." +
                Environment.NewLine +
                Environment.NewLine +

                "You are the first person in the contract approval " +
                "workflow. Once you sign the contract, the next authorized " +
                "signatory will be notified automatically." +
                Environment.NewLine +
                Environment.NewLine +

                "Kind regards," +
                Environment.NewLine +
                "UNILAK Staff Engagement & Claim Processing System";

            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpServer,
                smtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                senderEmail,
                senderPassword);

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }
    }
}

