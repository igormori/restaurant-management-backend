using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MimeKit;
using RestaurantManagement.Api.Options;

namespace RestaurantManagement.Api.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly EmailOptions _emailOptions;
        private readonly ILogger<EmailService> _logger;
        private readonly IHostEnvironment _env;

        public EmailService(
            IOptions<EmailOptions> emailOptions,
            ILogger<EmailService> logger,
            IHostEnvironment env)
        {
            _emailOptions = emailOptions.Value;
            _logger = logger;
            _env = env;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailOptions.FromName, _emailOptions.FromEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                
                // For development, we might not have a real SMTP server.
                // We'll log the email content if SMTP is not configured or in Dev (optional logic)
                // But generally, we try to connect.
                
                if (_env.IsDevelopment() && string.IsNullOrEmpty(_emailOptions.SmtpHost)) 
                {
                    _logger.LogWarning($"[DEV MODE] Email to {to}");
                    _logger.LogWarning($"[Subject]: {subject}");
                    _logger.LogWarning($"[Body]: {body}");
                    return;
                }

                _logger.LogInformation("Connecting to SMTP server {Host}:{Port}...", _emailOptions.SmtpHost, _emailOptions.SmtpPort);
                await client.ConnectAsync(_emailOptions.SmtpHost, _emailOptions.SmtpPort, 
                    _emailOptions.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                if (!string.IsNullOrEmpty(_emailOptions.SmtpUser))
                {
                    _logger.LogInformation("Authenticating SMTP user {User}...", _emailOptions.SmtpUser);
                    await client.AuthenticateAsync(_emailOptions.SmtpUser, _emailOptions.SmtpPass);
                }

                _logger.LogInformation("Sending email to {To}...", to);
                await client.SendAsync(message);
                _logger.LogInformation("Email sent successfully.");
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {to}");
                // In production, we might want to throw or handle queueing
                // For now, allow failure but log error.
                // Re-throwing might be safer to ensure API knows it failed.
                 throw;
            }
        }

        public async Task SendVerificationEmailAsync(string to, string code)
        {
            var subject = "Verify your email";
            var body = $@"
                <html>
                <body>
                    <h2>Welcome!</h2>
                    <p>Your verification code is: <strong>{code}</strong></p>
                    <p>This code expires in {_emailOptions.SmtpPort} minutes.</p> 
                    <p>Wait, mixing options.. expires in configured minutes.</p>
                </body>
                </html>";
            
            // Note: expiration minute logic was in SecurityOptions, which we don't inject here yet.
            // Simplified body:
            
            body = $@"
                <html>
                <body>
                    <h2>Verify your account</h2>
                    <p>Your verification code is:</p>
                    <h1>{code}</h1>
                    <p>Please enter this code in the app to verify your email.</p>
                </body>
                </html>";

            await SendEmailAsync(to, subject, body);
        }
    }
}
