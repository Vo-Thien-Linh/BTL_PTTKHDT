using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace BTL_PTTKHDT.Services;

public sealed class GmailSmtpOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Hệ thống Tín dụng";
}

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}

public sealed class GmailEmailSender : IEmailSender
{
    private readonly GmailSmtpOptions _options;

    public GmailEmailSender(IOptions<GmailSmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException("Chưa cấu hình Gmail SMTP trong appsettings.");
        }

        var fromEmail = string.IsNullOrWhiteSpace(_options.FromEmail) ? _options.UserName : _options.FromEmail;
        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };

        using var registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(message);
    }
}
