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
            var emailSettings = _configuration.GetSection("EmailSettings");

            // Fail-fast validation for production configuration
            string senderName = emailSettings["SenderName"]
                ?? throw new InvalidOperationException("EmailSettings:SenderName is missing from configuration.");

            string senderEmail = emailSettings["SenderEmail"]
                ?? throw new InvalidOperationException("EmailSettings:SenderEmail is missing from configuration.");

            string smtpServer = emailSettings["SmtpServer"]
                ?? throw new InvalidOperationException("EmailSettings:SmtpServer is missing from configuration.");

            string smtpPortRaw = emailSettings["SmtpPort"]
                ?? throw new InvalidOperationException("EmailSettings:SmtpPort is missing from configuration.");

            if (!int.TryParse(smtpPortRaw, out int smtpPort))
            {
                throw new InvalidOperationException($"Invalid SmtpPort configured: '{smtpPortRaw}'. Expected a valid integer.");
            }

            string senderPassword = emailSettings["SenderPassword"]
                ?? throw new InvalidOperationException("EmailSettings:SenderPassword is missing from configuration.");

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(recipientName ?? string.Empty, recipientEmail ?? string.Empty));

            message.Subject = "Welcome to the UNILAK Staff Portal";

            message.Body = new TextPart("plain")
            {
                Text = $@"Dear {recipientName},

Your login credentials for the UNILAK Staff Portal are:

Username: {username}
Password: {password}

Welcome to the system and thank you for being part of UNILAK.

Kind regards,
UNILAK Staff Engagement Portal"
            };

            using var smtp = new SmtpClient();

            // Connect using production STARTTLS settings
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
        public async Task SendMarksSubmissionNotificationAsync(
    string recipientEmail,
    string recipientName,
    string lecturerName,
    string lecturerEmail,
    string courseCode,
    string courseTitle,
    string academicYear,
    string submissionReference)
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

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    senderName,
                    senderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName,
                    recipientEmail));

            message.Subject =
                $"Marks Submission Requires Review - {courseCode}";

            message.Body = new TextPart("plain")
            {
                Text = $@"Dear {recipientName},

A new marks submission has been received and requires review and signature from the Exam Office.

Submission Details
------------------
Submission Reference: {submissionReference}

Lecturer: {lecturerName}
Lecturer Email: {lecturerEmail}

Course: {courseCode} - {courseTitle}

Academic Year: {academicYear}

Status: Pending Exam Office Review

Please log in to the Academic Staff Engagement & Claim Processing System to review the submitted marks and complete the required approval/signature process.

Kind regards,

UNILAK Staff Engagement Portal"
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