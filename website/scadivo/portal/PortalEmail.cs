using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Json;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class PortalEmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 465;
    public string Security { get; set; } = "SslOnConnect";
    public string Username { get; set; } = "";
    public string PasswordFile { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@scadivo.com";
    public string FromName { get; set; } = "SCADIVO";
    public string ReplyToAddress { get; set; } = "support@scadivo.com";
    public int PollSeconds { get; set; } = 30;

    public bool Valid(bool allowLocalPlaintext = false)
    {
        if (!Enabled) return true;
        var secure = (Security is "SslOnConnect" or "StartTls") && !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(PasswordFile) && File.Exists(PasswordFile);
        var localSink = allowLocalPlaintext && Security == "None" &&
            string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(PasswordFile);
        return !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 && (secure || localSink) &&
            Address(FromAddress) && Address(ReplyToAddress) && PollSeconds is >= 10 and <= 300;
    }

    private static bool Address(string value) => !value.Contains('\r') && !value.Contains('\n') &&
        MailboxAddress.TryParse(value, out var address) && address.Address == value;
}

public interface IPortalEmailTransport
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}

public sealed class SmtpPortalEmailTransport(IOptions<PortalEmailOptions> settings, IHostEnvironment environment) : IPortalEmailTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        if (!options.Enabled || !options.Valid(environment.IsDevelopment())) throw new InvalidOperationException("Email transport is not configured.");
        using var client = new SmtpClient { Timeout = 30000 };
        await client.ConnectAsync(options.Host, options.Port,
            options.Security switch
            {
                "StartTls" => SecureSocketOptions.StartTls,
                "None" when environment.IsDevelopment() => SecureSocketOptions.None,
                _ => SecureSocketOptions.SslOnConnect
            }, cancellationToken);
        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            var password = (await File.ReadAllTextAsync(options.PasswordFile, cancellationToken)).TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Email credential file is empty.");
            await client.AuthenticateAsync(options.Username, password, cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        // SMTP has accepted the message. A failed QUIT must not trigger a duplicate retry.
        try { await client.DisconnectAsync(true, cancellationToken); }
        catch (Exception) { /* Dispose still closes the accepted SMTP session. */ }
    }
}

public sealed class PortalEmailDispatcher(
    PortalDb db, UserManager<PortalUser> users, IOptions<PortalEmailOptions> settings,
    IPortalEmailTransport transport, IConfiguration configuration, ILogger<PortalEmailDispatcher> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        if (!settings.Value.Enabled) return false;
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Hold this row lock through SMTP acceptance. Other portal instances skip it.
        var rows = await db.EmailOutbox.FromSqlInterpolated($"""
            SELECT * FROM "EmailOutbox"
            WHERE "Status" IN ('queued', 'retry')
              AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now})
            ORDER BY "CreatedAtUtc"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var item = rows.SingleOrDefault();
        if (item is null) return false;

        var user = item.UserId is null ? null : await users.FindByIdAsync(item.UserId);
        var template = await db.EmailTemplates.FindAsync([item.TemplateKey], cancellationToken);
        var recipient = item.RecipientAddress ?? user?.Email;
        var reason = item.TemplateKey is not ("account.invitation" or "account.confirmation" or "account.password-reset" or "support.created" or "support.replied" or "contact.received" or "contact.notification") ? "unsupported-template"
            : string.IsNullOrWhiteSpace(recipient) || !MailboxAddress.TryParse(recipient, out _) ? "recipient-missing"
            : template is null ? "template-missing"
            : item.TemplateKey == "account.invitation" && user?.InvitationAccepted == true ? "invitation-accepted"
            : item.TemplateKey == "account.confirmation" && (user is null || user.EmailConfirmed || user.InvitationAccepted) ? "confirmation-not-required"
            : item.TemplateKey == "account.password-reset" && (user is null || !user.EmailConfirmed || !user.InvitationAccepted) ? "password-reset-not-eligible" : null;
        if (reason is not null)
        {
            item.Status = "skipped";
            item.LastErrorCode = reason;
            if (item.TemplateKey.StartsWith("contact.", StringComparison.Ordinal)) ClearContactData(item);
        }
        else
        {
            item.Attempts++;
            try
            {
                var publicUrl = configuration["Portal:PublicUrl"] ?? "https://scadivo.com/portal/";
                var link = item.TemplateKey switch
                {
                    "account.invitation" => await PortalInvitations.LinkAsync(users, user!, publicUrl),
                    "account.confirmation" => await PortalInvitations.ConfirmationLinkAsync(users, user!, publicUrl),
                    "account.password-reset" => await PortalInvitations.PasswordResetLinkAsync(users, user!, publicUrl),
                    _ => publicUrl
                };
                var data = string.IsNullOrWhiteSpace(item.TemplateDataJson)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(item.TemplateDataJson) ?? new Dictionary<string, string>();
                data.TryAdd("name", user?.DisplayName ?? "");
                data.TryAdd("reference", item.Reference ?? "");
                data.TryAdd("link", link);
                data.TryAdd("email", user?.Email ?? "");
                data.TryAdd("category", "");
                data.TryAdd("subject", "");
                data.TryAdd("message", "");
                var english = data.GetValueOrDefault("locale") == "en" || user?.Locale == "en";
                string Render(string text)
                {
                    foreach (var (key, value) in data)
                        text = text.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
                    return text;
                }
                var body = Render(english ? template!.BodyEn : template!.BodyPt);
                // Keep single-use links visible in older editable templates without duplicating links they already include.
                if ((item.TemplateKey is "account.invitation" or "account.confirmation" or "account.password-reset") && !body.Contains(link, StringComparison.Ordinal)) body += "\n\n" + link;
                if (item.TemplateKey == "account.password-reset" &&
                    !body.Contains(english ? "24 hours" : "24 horas", StringComparison.OrdinalIgnoreCase))
                    body += english
                        ? "\n\nThis link expires in 24 hours and can only be used once."
                        : "\n\nO link expira em 24 horas e pode ser usado uma única vez.";
                var replyToSubmitter = item.TemplateKey == "contact.notification";
                using var message = CreateMessage(settings.Value, item.Id, recipient!,
                    Render(english ? template!.SubjectEn : template.SubjectPt), body,
                    replyToSubmitter ? item.ReplyToAddress : null, english ? "en" : "pt-BR", replyToSubmitter);
                await transport.SendAsync(message, cancellationToken);
                item.Status = "sent";
                item.SentAtUtc = DateTimeOffset.UtcNow;
                item.NextAttemptAtUtc = null;
                item.LastErrorCode = null;
                // Contact details are needed only while delivery is pending.
                if (item.TemplateKey.StartsWith("contact.", StringComparison.Ordinal)) ClearContactData(item);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                // SMTP responses and exception messages can contain addresses or credentials.
                // Persist/log only a fixed category and the internal notification ID.
                item.LastErrorCode = exception is MailKit.Security.AuthenticationException ? "smtp-authentication"
                    : exception is SslHandshakeException ? "smtp-tls" : "smtp-delivery";
                item.Status = item.Attempts >= 5 ? "failed" : "retry";
                item.NextAttemptAtUtc = item.Status == "retry"
                    ? now.AddMinutes(Math.Min(60, Math.Pow(3, item.Attempts - 1))) : null;
                if (item.Status == "failed")
                {
                    if (item.TemplateKey.StartsWith("contact.", StringComparison.Ordinal)) ClearContactData(item);
                }
                logger.LogWarning("Portal email {NotificationId} attempt {Attempt} failed ({Code}).", item.Id, item.Attempts, item.LastErrorCode);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public static MimeMessage CreateMessage(PortalEmailOptions settings, Guid id, string recipient, string subject,
        string body, string? replyTo = null, string locale = "pt-BR", bool replyToSubmitter = false)
    {
        var safeSubject = subject.Replace('\r', ' ').Replace('\n', ' ');
        var supportLink = locale == "en"
            ? "https://scadivo.com/contact?area=support"
            : "https://scadivo.com/contato?area=suporte";
        var footerText = locale == "en"
            ? "Please do not reply to this email. If you need help, visit our support page:"
            : "Não responda a este e-mail. Se precisar de apoio, acesse nossa página de suporte:";
        var plainBody = replyToSubmitter ? body : $"{body.TrimEnd()}\n\n{footerText}\n{supportLink}";
        var htmlBody = RenderHtml(safeSubject, body, locale, supportLink, footerText, replyToSubmitter);
        var message = new MimeMessage
        {
            MessageId = $"{id:N}@scadivo.com",
            Subject = safeSubject,
            Body = new MultipartAlternative
            {
                new TextPart("plain") { Text = plainBody },
                new TextPart("html") { Text = htmlBody }
            }
        };
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        if (!string.IsNullOrWhiteSpace(replyTo)) message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        return message;
    }

    private static void ClearContactData(EmailOutbox item)
    {
        item.TemplateDataJson = null;
        item.RecipientAddress = null;
        item.ReplyToAddress = null;
    }

    private static string RenderHtml(string subject, string body, string locale, string supportLink, string footerText,
        bool replyToSubmitter)
    {
        static string Encode(string value) => HtmlEncoder.Default.Encode(value);
        string Format(string value)
        {
            var encoded = Encode(value);
            return Regex.Replace(encoded, @"https://[^\s<]+", match =>
            {
                var url = match.Value.TrimEnd('.', ',', ';', ')', ']');
                var suffix = match.Value[url.Length..];
                return $"<a href=\"{url}\" style=\"color:#087f7e;text-decoration:underline\">{url}</a>{suffix}";
            });
        }
        var paragraphs = string.Join("</p><p style=\"margin:0 0 16px\">", body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.None).Select(part => Format(part).Replace("\n", "<br>", StringComparison.Ordinal)));
        var language = locale == "en" ? "en" : "pt-BR";
        var supportLabel = locale == "en" ? "SCADIVO support" : "suporte SCADIVO";
        var footer = replyToSubmitter
            ? string.Empty
            : $"{Encode(footerText)} <a href=\"{Encode(supportLink)}\" style=\"color:#087f7e;text-decoration:underline\">{Encode(supportLabel)}</a>.";
        var tagline = locale == "en" ? "SCADA & industrial automation at every scale" : "Supervisão e automação em qualquer escala";
        return $"""<!doctype html><html lang="{language}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head><body style="margin:0;padding:28px 12px;background:#edf2f5;font-family:Arial,Helvetica,sans-serif;color:#182635"><table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;margin:0 auto;background:#fff;border:1px solid #d9e2e8;border-radius:12px;overflow:hidden"><tr><td style="padding:24px 30px;background:#101c2a;border-bottom:4px solid #21d0c3"><table role="presentation" cellspacing="0" cellpadding="0"><tr><td style="width:38px;height:38px;text-align:center;vertical-align:middle;border-radius:10px;background:#14283a;color:#21d0c3;font-size:28px;font-weight:bold">S</td><td style="padding-left:12px;color:#f4f8fc;font-size:20px;font-weight:bold;letter-spacing:1px">SCADIVO<div style="margin-top:3px;color:#9fb2c5;font-size:10px;font-weight:normal;letter-spacing:1px">SCADA &amp; INDUSTRIAL AUTOMATION</div></td></tr></table></td></tr><tr><td style="padding:32px 30px 22px"><h1 style="margin:0 0 22px;color:#142436;font-size:23px;line-height:1.35">{Encode(subject)}</h1><div style="color:#33475b;font-size:15px;line-height:1.75"><p style="margin:0 0 16px">{paragraphs}</p></div></td></tr><tr><td style="padding:18px 30px;background:#f5f8fa;border-top:1px solid #e1e8ed;color:#738395;font-size:12px;line-height:1.6">{footer}<br><span style="color:#0b8d86;font-weight:bold">SCADIVO</span> · {Encode(tagline)}</td></tr></table></body></html>""";
    }
}

public sealed class PortalEmailSignal
{
    private readonly SemaphoreSlim signal = new(0, 1);
    public void Notify()
    {
        try { signal.Release(); }
        catch (SemaphoreFullException) { }
    }
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => signal.WaitAsync(timeout, cancellationToken);
}

public sealed class PortalEmailWorker(IServiceScopeFactory scopes, IOptions<PortalEmailOptions> settings, PortalEmailSignal signal,
    ILogger<PortalEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<PortalEmailDispatcher>();
                while (await dispatcher.ProcessNextAsync(stoppingToken)) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError("Portal email worker could not process the queue ({ErrorType}).", exception.GetType().Name);
            }
            await signal.WaitAsync(TimeSpan.FromSeconds(settings.Value.PollSeconds), stoppingToken);
        }
    }
}
