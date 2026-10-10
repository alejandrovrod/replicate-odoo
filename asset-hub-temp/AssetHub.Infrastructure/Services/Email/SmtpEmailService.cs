using System;
using System.Threading;
using System.Threading.Tasks;
using AssetHub.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace AssetHub.Infrastructure.Services.Email;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default)
    {
        var fromEmail = _config["Email:From"] ?? _config["Email__From"];
        var host = _config["Email:SmtpHost"] ?? _config["Email__SmtpHost"];
        var portStr = _config["Email:SmtpPort"] ?? _config["Email__SmtpPort"];
        var user = _config["Email:SmtpUser"] ?? _config["Email__SmtpUser"];
        var pass = _config["Email:SmtpPassword"] ?? _config["Email__SmtpPassword"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            throw new InvalidOperationException("La configuración SMTP está incompleta. Configure Email:SmtpHost, Email:SmtpUser y Email:SmtpPassword.");
        }

        if (!int.TryParse(portStr, out int port))
        {
            port = 587; // default port
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromEmail, fromEmail ?? "noreply@assethub.local"));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;

        var format = isHtml ? "html" : "plain";
        message.Body = new TextPart(format)
        {
            Text = body
        };

        using var client = new SmtpClient();
        try
        {
            _logger.LogInformation("Sending email to {To} via {Host}:{Port}", to, host, port);
            
            // Note: Yahoo requires SSL/TLS. MailKit's Auto will usually negotiate properly.
            await client.ConnectAsync(host, port, SecureSocketOptions.Auto, cancellationToken);
            await client.AuthenticateAsync(user, pass, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }
}
