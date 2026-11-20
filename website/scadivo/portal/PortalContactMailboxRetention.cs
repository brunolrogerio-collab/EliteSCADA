using System.Globalization;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace Scadivo.Portal;

public sealed class PortalContactMailboxRetentionOptions
{
    public bool Enabled { get; set; }
    public bool Apply { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 993;
    public string Username { get; set; } = "";
    public string PasswordFile { get; set; } = "";

    public bool Valid(PortalEmailOptions smtp) => !Enabled ||
        (!string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 &&
            !string.IsNullOrWhiteSpace(Username) && File.Exists(PasswordFile) &&
            !Username.Equals(smtp.Username, StringComparison.OrdinalIgnoreCase) &&
            !Username.Equals(smtp.FromAddress, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(smtp.PasswordFile) ||
                !Path.GetFullPath(PasswordFile).Equals(Path.GetFullPath(smtp.PasswordFile), StringComparison.OrdinalIgnoreCase)));
}

public sealed record ContactMailboxRetentionResult(int Eligible, int Purged);

public sealed class PortalContactMailboxRetention(IOptions<PortalContactMailboxRetentionOptions> options, TimeProvider clock)
{
    public const string ClosedRoot = "SCADIVO-Encerrados";

    public async Task<ContactMailboxRetentionResult> RunAsync(CancellationToken token)
    {
        var settings = options.Value;
        if (!settings.Enabled) return new(0, 0);
        using var imap = new ImapClient { Timeout = 15000 };
        await imap.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.SslOnConnect, token);
        var password = (await File.ReadAllTextAsync(settings.PasswordFile, token)).TrimEnd('\r', '\n');
        await imap.AuthenticateAsync(settings.Username, password, token);
        if (settings.Apply && !imap.Capabilities.HasFlag(ImapCapabilities.UidPlus))
            throw new NotSupportedException("UID-scoped expunge is required.");

        var personal = imap.GetFolder(imap.PersonalNamespaces.First());
        IMailFolder root;
        try { root = await personal.GetSubfolderAsync(ClosedRoot, token); }
        catch (FolderNotFoundException) { return new(0, 0); }
        var cutoff = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddMonths(-12);
        var eligible = 0;
        var purged = 0;
        var folders = await root.GetSubfoldersAsync(false, token);
        foreach (var folder in folders)
        {
            if (!DateOnly.TryParseExact(folder.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closedOn) ||
                closedOn >= cutoff || folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                continue;

            await folder.OpenAsync(settings.Apply ? FolderAccess.ReadWrite : FolderAccess.ReadOnly, token);
            var uids = await folder.SearchAsync(SearchQuery.All, token);
            eligible += uids.Count;
            if (settings.Apply)
            {
                foreach (var batch in uids.Chunk(100))
                {
                    await folder.AddFlagsAsync(batch, MessageFlags.Deleted, silent: true, token);
                    await folder.ExpungeAsync(batch, token);
                    purged += batch.Length;
                }
            }
            await folder.CloseAsync(expunge: false, token);
        }
        await imap.DisconnectAsync(quit: true, token);
        return new(eligible, purged);
    }
}
