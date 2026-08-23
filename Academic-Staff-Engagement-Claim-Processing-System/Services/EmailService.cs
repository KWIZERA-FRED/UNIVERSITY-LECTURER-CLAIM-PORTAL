using MailKit.Net.Smtp;
using MailKit.Security;
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

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                emailSettings["SenderName"],
                emailSettings["SenderEmail"]));

            message.To.Add(new MailboxAddress(
                recipientName,
                recipientEmail));

            message.Subject = "Welcome to the UNILAK Staff Portal";

            message.Body = new TextPart("plain")
            {
                Text =
$@"Dear {recipientName},

Your login credentials for the UNILAK Staff Portal are:

Username: {username}
Password: {password}

Welcome to the system and thank you for being part of UNILAK.

Kind regards,
UNILAK Staff Engagement Portal"
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                emailSettings["SmtpServer"],
                int.Parse(emailSettings["SmtpPort"]!),
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                emailSettings["SenderEmail"],
                emailSettings["SenderPassword"]);

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }
    }
}