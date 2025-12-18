using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace STASIS.Services;

public class EmailSettings
{
    public string MailServer { get; set; }
    public int MailPort { get; set; }
    public string SenderName { get; set; }
    public string SenderEmail { get; set; }
    public string Password { get; set; }
    public bool EnableSsl { get; set; }
}

public class EmailSender : IEmailSender
{
    private readonly EmailSettings _emailSettings;

    public EmailSender(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        try
        {
            using (var client = new SmtpClient(_emailSettings.MailServer, _emailSettings.MailPort))
            {
                client.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.Password);
                client.EnableSsl = _emailSettings.EnableSsl;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
            }
        }
        catch (Exception ex)
        {
            // Log exception or handle appropriately
            // For now, we allow it to fail silently or throw depending on config
            throw new InvalidOperationException($"Could not send email: {ex.Message}");
        }
    }
}
