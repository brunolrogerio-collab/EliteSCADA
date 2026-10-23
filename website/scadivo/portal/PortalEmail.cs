using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
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

    public bool Valid() => !Enabled ||
        (!string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 &&
         Security is "SslOnConnect" or "StartTls" && !string.IsNullOrWhiteSpace(Username) &&
         !string.IsNullOrWhiteSpace(PasswordFile) && File.Exists(PasswordFile) &&
         Address(FromAddress) && Address(ReplyToAddress) && PollSeconds is >= 10 and <= 300);

    private static bool Address(string value) => !value.Contains('\r') && !value.Contains('\n') &&
        MailboxAddress.TryParse(value, out var address) && address.Address == value;
}

public interface IPortalEmailTransport
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}

public sealed class SmtpPortalEmailTransport(IOptions<PortalEmailOptions> settings) : IPortalEmailTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        if (!options.Enabled || !options.Valid()) throw new InvalidOperationException("Email transport is not configured.");
        var password = (await File.ReadAllTextAsync(options.PasswordFile, cancellationToken)).TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Email credential file is empty.");
        using var client = new SmtpClient { Timeout = 30000 };
        await client.ConnectAsync(options.Host, options.Port,
            options.Security == "StartTls" ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect, cancellationToken);
        await client.AuthenticateAsync(options.Username, password, cancellationToken);
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

        var user = await users.FindByIdAsync(item.UserId);
        var template = await db.EmailTemplates.FindAsync([item.TemplateKey], cancellationToken);
        var reason = item.TemplateKey is not ("account.invitation" or "support.created" or "support.replied") ? "unsupported-template"
            : user?.Email is null || !MailboxAddress.TryParse(user.Email, out _) ? "recipient-missing"
            : template is null ? "template-missing"
            : item.TemplateKey == "account.invitation" && user.InvitationAccepted ? "invitation-accepted" : null;
        if (reason is not null)
        {
            item.Status = "skipped";
            item.LastErrorCode = reason;
        }
        else
        {
            item.Attempts++;
            try
            {
                var publicUrl = configuration["Portal:PublicUrl"] ?? "https://scadivo.com/portal/";
                var link = item.TemplateKey == "account.invitation"
                    ? await PortalInvitations.LinkAsync(users, user!, publicUrl) : publicUrl;
                var english = user!.Locale == "en";
                string Render(string text) => text.Replace("{{name}}", user.DisplayName, StringComparison.Ordinal)
                    .Replace("{{reference}}", item.Reference ?? "", StringComparison.Ordinal)
                    .Replace("{{link}}", link, StringComparison.Ordinal);
                var body = Render(english ? template!.BodyEn : template!.BodyPt);
                // Existing editable invitation templates predate SMTP and may omit {{link}}.
                if (item.TemplateKey == "account.invitation" && !body.Contains(link, StringComparison.Ordinal)) body += "\n\n" + link;
                using var message = CreateMessage(settings.Value, item.Id, user.Email!,
                    Render(english ? template.SubjectEn : template.SubjectPt), body);
                await transport.SendAsync(message, cancellationToken);
                item.Status = "sent";
                item.SentAtUtc = DateTimeOffset.UtcNow;
                item.NextAttemptAtUtc = null;
                item.LastErrorCode = null;
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
                logger.LogWarning("Portal email {NotificationId} attempt {Attempt} failed ({Code}).", item.Id, item.Attempts, item.LastErrorCode);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public static MimeMessage CreateMessage(PortalEmailOptions settings, Guid id, string recipient, string subject, string body)
    {
        var message = new MimeMessage
        {
            MessageId = $"{id:N}@scadivo.com",
            Subject = subject.Replace('\r', ' ').Replace('\n', ' '),
            Body = new TextPart("plain") { Text = body }
        };
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.ReplyTo.Add(MailboxAddress.Parse(settings.ReplyToAddress));
        return message;
    }
}

public sealed class PortalEmailWorker(IServiceScopeFactory scopes, IOptions<PortalEmailOptions> settings,
    ILogger<PortalEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.Value.PollSeconds));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PortalEmailDispatcher>().ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogError("Portal email worker could not process the queue ({ErrorType}).", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
