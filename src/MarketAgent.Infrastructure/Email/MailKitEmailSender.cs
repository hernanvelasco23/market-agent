using MailKit.Net.Smtp;
using MailKit.Security;
using MarketAgent.Application.Abstractions;
using MarketAgent.Application.Models;
using MimeKit;

namespace MarketAgent.Infrastructure.Email;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailDeliveryOptions _options;

    public MailKitEmailSender(EmailDeliveryOptions options)
    {
        _options = options;
    }

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(message.FromName, message.FromEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.ToEmail));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _options.EnableSsl
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(
            _options.SmtpHost,
            _options.SmtpPort,
            socketOptions,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            await client.AuthenticateAsync(
                _options.SmtpUser,
                _options.SmtpPassword ?? string.Empty,
                cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
