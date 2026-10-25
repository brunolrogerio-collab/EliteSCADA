using System.Text.RegularExpressions;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public static class PortalDownloadCatalog
{
    public static readonly string[] Platforms = ["windows-x64", "linux-x64", "linux-arm64", "docker"];
    public static bool ValidVersion(string? value) => value is not null && Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9._+-]{0,79}\z");
    public static bool ValidHash(string? value) => value is not null && Regex.IsMatch(value, @"\A[a-fA-F0-9]{64}\z");

    public static bool IsOfficialUrl(string value)
    {
        const string prefix = "/brunolrogerio-collab/EliteSCADA/releases/download/";
        if (value.Length > 2000 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "github.com" || !uri.IsDefaultPort ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            !uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var parts = uri.AbsolutePath[prefix.Length..].Split('/');
        return parts.Length == 2 && parts.All(part =>
        {
            var decoded = Uri.UnescapeDataString(part);
            return decoded.Length > 0 && decoded is not ("." or "..") &&
                !decoded.Any(character => char.IsControl(character) || character is '/' or '\\');
        });
    }

    public static bool Ready(DownloadRelease release) =>
        ValidVersion(release.Version) && Platforms.Contains(release.Platform) &&
        IsOfficialUrl(release.OfficialUrl) && ValidHash(release.Sha256) &&
        !string.IsNullOrWhiteSpace(release.NotesPt) && !string.IsNullOrWhiteSpace(release.NotesEn) && !string.IsNullOrWhiteSpace(release.NotesEs);
}
