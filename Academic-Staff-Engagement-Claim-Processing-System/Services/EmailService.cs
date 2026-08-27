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
    }
}