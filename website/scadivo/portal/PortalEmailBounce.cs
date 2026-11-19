using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class PortalEmailBounceOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 993;
    public int PollSeconds { get; set; } = 60;

    public bool Valid(PortalEmailOptions smtp) => !Enabled ||
        (smtp.Enabled && !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 &&
            PollSeconds is >= 10 and <= 300 && File.Exists(smtp.PasswordFile) &&
            string.Equals(smtp.Username, smtp.FromAddress, StringComparison.OrdinalIgnoreCase));
}

public sealed class PortalEmailBounceProcessor(PortalDb db)
{
    private static readonly Regex PortalMessageId = new(@"^<?([0-9a-f]{32})@scadivo\.com>?$", RegexOptions.IgnoreCase);
    private static readonly Regex FailureStatus = new(@"^[45]\.[0-9]{1,3}\.[0-9]{1,3}$");

    internal static string Fingerprint(Guid id, string address) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}\n{address.Trim().ToUpperInvariant()}")));

    public async Task<bool> RecordAsync(MimeMessage message, CancellationToken token)
    {
        if (message.Body is not Multipart report || !report.ContentType.IsMimeType("multipart", "report") ||
            !string.Equals(report.ContentType.Parameters["report-type"], "delivery-status", StringComparison.OrdinalIgnoreCase) ||
            report.Count < 3 || report[1] is not MessageDeliveryStatus delivery)
            return false;

        var originalId = (report[2] as MessagePart)?.Message?.MessageId;
        var parsedId = PortalMessageId.Match(originalId?.Trim() ?? "");
        if (!parsedId.Success || !Guid.TryParseExact(parsedId.Groups[1].Value, "N", out var id)) return false;

        var failed = delivery.StatusGroups.Skip(1)
            .Where(group => string.Equals(group["Action"]?.Trim(), "failed", StringComparison.OrdinalIgnoreCase) &&
                FailureStatus.IsMatch(group["Status"]?.Trim() ?? ""))
            .Select(group => Recipient(group["Original-Recipient"] ?? group["Final-Recipient"]))
            .OfType<string>()
            .Select(address => Fingerprint(id, address))
            .ToHashSet(StringComparer.Ordinal);
        if (failed.Count == 0) return false;

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // The sending worker holds this lock through SMTP acceptance and saving the fingerprint.
        var rows = await db.EmailOutbox.FromSqlInterpolated($"""
            SELECT * FROM "EmailOutbox" WHERE "Id" = {id} FOR UPDATE
            """).AsNoTracking().ToListAsync(token);
        var mail = rows.SingleOrDefault();
        if (mail is not { Status: "sent", SentAtUtc: not null, DeliveryRecipientFingerprint: not null } ||
            !failed.Contains(mail.DeliveryRecipientFingerprint))
            return false;

        await db.EmailOutbox.Where(row => row.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(row => row.Status, "bounced")
            .SetProperty(row => row.BouncedAtUtc, DateTimeOffset.UtcNow)
            .SetProperty(row => row.LastErrorCode, "smtp-bounce"), token);
        await transaction.CommitAsync(token);
        return true;
    }

    private static string? Recipient(string? field)
    {
        if (field is null) return null;
        var separator = field.IndexOf(';');
        if (separator < 0 || !field[..separator].Trim().Equals("rfc822", StringComparison.OrdinalIgnoreCase)) return null;
        return MailboxAddress.TryParse(field[(separator + 1)..].Trim(), out var address) ? address.Address : null;
    }
}

public sealed class PortalEmailBounceWorker(
    IServiceScopeFactory scopes, IConfiguration configuration, IOptions<PortalEmailBounceOptions> options,
    IOptions<PortalEmailOptions> email, ILogger<PortalEmailBounceWorker> logger) : BackgroundService
{
    private const int MaxMessageBytes = 1024 * 1024;
    private sealed record Cursor(string Mailbox, uint Validity, uint Uid);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning("Portal could not check SMTP delivery returns ({ErrorType}).", error.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollSeconds), stoppingToken);
        }
    }

    private async Task PollAsync(CancellationToken token)
    {
        var settings = options.Value;
        var smtp = email.Value;
        var path = Path.Combine(configuration["Portal:KeyDirectory"] ?? "/app/keys", "email-bounce-position.json");
        var mailbox = $"{settings.Host}:{settings.Port}/{smtp.Username}";
        var cursor = File.Exists(path) ? JsonSerializer.Deserialize<Cursor>(await File.ReadAllTextAsync(path, token)) : null;

        using var imap = new ImapClient { Timeout = 15000 };
        await imap.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.SslOnConnect, token);
        var password = (await File.ReadAllTextAsync(smtp.PasswordFile, token)).TrimEnd('\r', '\n');
        await imap.AuthenticateAsync(smtp.Username, password, token);
        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, token);
        var last = cursor is not null && cursor.Mailbox == mailbox && cursor.Validity == inbox.UidValidity ? cursor.Uid : 0;
        if (last == uint.MaxValue) return;

        var range = new UniqueIdRange(inbox.UidValidity, last + 1, uint.MaxValue);
        var query = SearchQuery.Uids(range).And(SearchQuery.HeaderContains("Content-Type", "multipart/report"));
        if (last == 0) query = query.And(SearchQuery.DeliveredAfter(DateTime.UtcNow.Date.AddDays(-7)));
        var uids = (await inbox.SearchAsync(query, token)).Where(uid => uid.Id > last).OrderBy(uid => uid.Id).Take(50).ToArray();
        if (uids.Length == 0) return;

        var summaries = await inbox.FetchAsync(uids, MessageSummaryItems.UniqueId | MessageSummaryItems.Size, token);
        var byUid = summaries.ToDictionary(item => item.UniqueId);
        await using var scope = scopes.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<PortalEmailBounceProcessor>();
        foreach (var uid in uids)
        {
            if (byUid.TryGetValue(uid, out var summary) && summary.Size is > 0 and <= MaxMessageBytes)
            {
                try
                {
                    using var message = await inbox.GetMessageAsync(uid, token);
                    await processor.RecordAsync(message, token);
                }
                catch (MessageNotFoundException) { }
                catch (FormatException) { }
            }
            var temporary = path + "." + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Cursor(mailbox, inbox.UidValidity, uid.Id)), token);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
