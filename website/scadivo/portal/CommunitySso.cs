using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class CommunitySsoOptions
{
    public bool Enabled { get; set; }
    public string DiscourseUrl { get; set; } = "";
    public string SharedSecretFile { get; set; } = "/run/secrets/community-sso/shared-secret";

    public bool Valid()
    {
        if (!Enabled) return true;
        if (!Uri.TryCreate(DiscourseUrl, UriKind.Absolute, out var forum) || forum.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(forum.Host) || !forum.IsDefaultPort || forum.UserInfo.Length != 0 ||
            forum.AbsolutePath != "/" || forum.Query.Length != 0 || forum.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(SharedSecretFile) || !File.Exists(SharedSecretFile)) return false;

        try { return Encoding.UTF8.GetByteCount(File.ReadAllText(SharedSecretFile).Trim()) >= 32; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public string LoginStartUrl => Enabled ? $"{DiscourseUrl.TrimEnd('/')}/session/sso" : "";
}

public static class DiscourseConnect
{
    private const int MaximumChallengeLength = 8192;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<PortalUser> users,
        IOptions<CommunitySsoOptions> settings)
    {
        var options = settings.Value;
        if (!options.Enabled) return Results.NotFound();

        var user = await users.GetUserAsync(context.User);
        if (user is null || string.IsNullOrWhiteSpace(user.Email) ||
            !await users.IsEmailConfirmedAsync(user) || !user.InvitationAccepted || await users.IsLockedOutAsync(user))
            return Results.Forbid();

        var isOwner = await users.IsInRoleAsync(user, "Owner");
        var isMember = await users.IsInRoleAsync(user, "Member");
        if (!isOwner && (!isMember || !user.ProfileComplete)) return Results.Forbid();

        if (!context.Request.Query.TryGetValue("sso", out var ssoValues) || ssoValues.Count != 1 ||
            !context.Request.Query.TryGetValue("sig", out var signatureValues) || signatureValues.Count != 1)
            return Results.BadRequest(new { error = "invalid-community-request" });

        var sso = ssoValues[0];
        var signature = signatureValues[0];
        if (string.IsNullOrEmpty(sso) || sso.Length > MaximumChallengeLength || string.IsNullOrEmpty(signature))
            return Results.BadRequest(new { error = "invalid-community-request" });

        string secret;
        try { secret = (await File.ReadAllTextAsync(options.SharedSecretFile, context.RequestAborted)).Trim(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!TryReadChallenge(sso, signature, secret, options.DiscourseUrl, out var nonce, out var returnUrl))
            return Results.BadRequest(new { error = "invalid-community-request" });

        var payload = EncodeQuery(
            new("nonce", nonce),
            new("email", user.Email),
            new("external_id", user.Id),
            new("username", PublicAlias(user.Id)),
            new("locale", DiscourseLocale(user.Locale)));
        var encodedPayload = Convert.ToBase64String(StrictUtf8.GetBytes(payload));
        var responseSignature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(encodedPayload))).ToLowerInvariant();
        var destination = QueryHelpers.AddQueryString(returnUrl, new Dictionary<string, string?>
        {
            ["sso"] = encodedPayload,
            ["sig"] = responseSignature
        });
        return Results.Redirect(destination);
    }

    private static bool TryReadChallenge(
        string encodedPayload,
        string encodedSignature,
        string secret,
        string discourseUrl,
        out string nonce,
        out string returnUrl)
    {
        nonce = "";
        returnUrl = "";
        if (encodedSignature.Length != 64 || secret.Length < 32 ||
            !Uri.TryCreate(discourseUrl, UriKind.Absolute, out var forum)) return false;

        byte[] suppliedSignature;
        byte[] decoded;
        try
        {
            suppliedSignature = Convert.FromHexString(encodedSignature);
            decoded = Convert.FromBase64String(encodedPayload);
        }
        catch (FormatException) { return false; }
        if (!string.Equals(Convert.ToBase64String(decoded), encodedPayload, StringComparison.Ordinal)) return false;

        var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(encodedPayload));
        if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature)) return false;

        string rawPayload;
        try { rawPayload = StrictUtf8.GetString(decoded); }
        catch (DecoderFallbackException) { return false; }

        Dictionary<string, StringValues> values;
        try { values = QueryHelpers.ParseQuery(rawPayload); }
        catch (Exception exception) when (exception is ArgumentException or FormatException) { return false; }
        if (!SingleValue(values, "nonce", out nonce) || nonce.Length is < 1 or > 256 ||
            !SingleValue(values, "return_sso_url", out returnUrl) || returnUrl.Length > 2048 ||
            !Uri.TryCreate(returnUrl, UriKind.Absolute, out var callback)) return false;

        return callback.Scheme == Uri.UriSchemeHttps && callback.Host.Equals(forum.Host, StringComparison.OrdinalIgnoreCase) &&
            callback.Port == forum.Port && callback.UserInfo.Length == 0 && callback.AbsolutePath == "/session/sso_login" &&
            callback.Query.Length == 0 && callback.Fragment.Length == 0;
    }

    private static bool SingleValue(IReadOnlyDictionary<string, StringValues> values, string key, out string value)
    {
        value = "";
        if (!values.TryGetValue(key, out var entries) || entries.Count != 1 || string.IsNullOrWhiteSpace(entries[0])) return false;
        value = entries[0]!;
        return true;
    }

    private static string EncodeQuery(params KeyValuePair<string, string>[] fields) =>
        string.Join('&', fields.Select(field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}"));

    private static string PublicAlias(string userId)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return "scadivo_" + Convert.ToHexString(digest)[..12].ToLowerInvariant();
    }

    private static string DiscourseLocale(string? locale) => locale switch
    {
        "en" => "en",
        "es" => "es",
        _ => "pt_BR"
    };
}
