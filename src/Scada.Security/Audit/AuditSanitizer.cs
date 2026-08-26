using System.Text.RegularExpressions;

namespace Scada.Security.Audit;

public static partial class AuditSanitizer
{
    public const int MaximumDetailCount = 32;
    public const int MaximumDetailKeyLength = 100;
    public const int MaximumDetailValueLength = 1024;

    private static readonly string[] SensitiveKeyFragments =
    {
        "password",
        "passwd",
        "pwd",
        "token",
        "jwt",
        "secret",
        "privatekey",
        "signingkey",
        "apikey",
        "authorization",
        "credential",
        "cookie",
        "hash",
        "salt"
    };

    public static AuditEvent Normalize(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return auditEvent with
        {
            TimestampUtc = auditEvent.TimestampUtc.ToUniversalTime(),
            SubjectId = NormalizeRequired(auditEvent.SubjectId),
            DisplayName = NormalizeOptional(auditEvent.DisplayName),
            Action = NormalizeRequired(auditEvent.Action),
            TargetKind = NormalizeRequired(auditEvent.TargetKind),
            TargetId = NormalizeRequired(auditEvent.TargetId),
            Details = SanitizeDetails(auditEvent.Details),
            CorrelationId = NormalizeOptional(auditEvent.CorrelationId),
            Area = NormalizeOptional(auditEvent.Area),
            ProjectKey = NormalizeOptional(auditEvent.ProjectKey),
            Roles = SanitizeRoles(auditEvent.Roles),
            Source = NormalizeOptional(auditEvent.Source)
        };
    }

    public static IReadOnlyDictionary<string, string>? SanitizeDetails(
        IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0) return null;

        var safe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in details)
        {
            if (safe.Count >= MaximumDetailCount) break;

            var key = NormalizeOptional(pair.Key);
            if (key is null || key.Length > MaximumDetailKeyLength || IsSensitiveKey(key))
                continue;

            var value = pair.Value ?? string.Empty;
            value = LooksSensitiveValue(value) ? "[REDACTED]" : value.Trim();
            if (value.Length > MaximumDetailValueLength)
                value = value[..MaximumDetailValueLength];

            safe[key] = value;
        }

        return safe.Count == 0 ? null : safe;
    }

    public static bool IsSensitiveKey(string key)
    {
        var normalized = new string(key.Where(char.IsLetterOrDigit).ToArray());
        return SensitiveKeyFragments.Any(fragment =>
            normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyCollection<string>? SanitizeRoles(IReadOnlyCollection<string>? roles)
    {
        if (roles is null || roles.Count == 0) return null;
        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(64)
            .ToArray();
    }

    private static bool LooksSensitiveValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Contains("access_token=", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Contains("refresh_token=", StringComparison.OrdinalIgnoreCase)) return true;
        return JwtLikeValue().IsMatch(trimmed);
    }

    private static string NormalizeRequired(string value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}$", RegexOptions.CultureInvariant)]
    private static partial Regex JwtLikeValue();
}
