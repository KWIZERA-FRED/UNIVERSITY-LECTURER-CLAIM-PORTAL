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
            if (string.IsNullOrWhiteSpace(
                    recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var emailSettings =
                _configuration
                    .GetSection("EmailSettings");

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

            recipientName =
                string.IsNullOrWhiteSpace(
                    recipientName)
                    ? "Staff Member"
                    : recipientName.Trim();

            username =
                string.IsNullOrWhiteSpace(
                    username)
                    ? "your username"
                    : username.Trim();

            var message =
                new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    senderName,
                    senderEmail));

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

            using var smtp =
                new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(
                smtpServer,
                smtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                senderEmail,
                senderPassword);

            await smtp.SendAsync(
                message);

            await smtp.DisconnectAsync(
                true);

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
            if (string.IsNullOrWhiteSpace(
                    recipientEmail))
            {
                throw new ArgumentException(
                    "Recipient email is required.",
                    nameof(recipientEmail));
            }

            var emailSettings =
                _configuration
                    .GetSection("EmailSettings");

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

            recipientName =
                string.IsNullOrWhiteSpace(
                    recipientName)
                    ? "Staff Member"
                    : recipientName.Trim();

            contractReference =
                string.IsNullOrWhiteSpace(
                    contractReference)
                    ? "the contract"
                    : contractReference.Trim();

            var message =
                new MimeMessage();

            message.From.Add(
                new MailboxAddress(
                    senderName,
                    senderEmail));

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

            using var smtp =
                new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(
                smtpServer,
                smtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                senderEmail,
                senderPassword);

            await smtp.SendAsync(
                message);

            await smtp.DisconnectAsync(
                true);

            _logger.LogInformation(
                "Contract signing notification sent to {RecipientEmail} for {ContractReference}.",
                recipientEmail,
                contractReference);
        }
    }
}