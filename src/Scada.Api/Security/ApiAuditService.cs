using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed class ApiAuditService(
    IAuditSink sink,
    ILogger<ApiAuditService> logger)
{
    private static readonly string[] SensitiveKeyFragments =
    {
        "password",
        "token",
        "secret",
        "privatekey",
        "private_key",
        "signingkey",
        "signing_key",
        "authorization"
    };

    public async ValueTask RecordAsync(
        HttpContext context,
        SecurityPrincipal principal,
        string action,
        AuditOutcome outcome,
        string targetKind,
        string targetId,
        IReadOnlyDictionary<string, string>? details = null)
    {
        var subjectId = principal.IsAuthenticated && !string.IsNullOrWhiteSpace(principal.SubjectId)
            ? principal.SubjectId.Trim()
            : "anonymous";
        var displayName = principal.IsAuthenticated ? principal.DisplayName : null;
        var safeDetails = Sanitize(details);

        try
        {
            await sink.WriteAsync(
                AuditEvent.Create(
                    subjectId,
                    displayName,
                    action,
                    outcome,
                    targetKind,
                    targetId,
                    safeDetails,
                    context.TraceIdentifier),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A failed audit write must never be mistaken for a failed or retried process command.
            // Durable delivery hardening can add buffering/outbox semantics without changing endpoint results.
            logger.LogError(
                ex,
                "Failed to persist audit event {Action} {Outcome} for {TargetKind}/{TargetId} and subject {SubjectId}.",
                action,
                outcome,
                targetKind,
                targetId,
                subjectId);
        }
    }

    public ValueTask RecordAuthorizationDeniedAsync(
        HttpContext context,
        ApiAuthorizationCheck authorization,
        string action,
        string targetKind,
        string targetId,
        IReadOnlyDictionary<string, string>? details = null) =>
        RecordAsync(
            context,
            authorization.Principal,
            action,
            AuditOutcome.Denied,
            targetKind,
            targetId,
            details);

    private static IReadOnlyDictionary<string, string>? Sanitize(
        IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0) return null;

        var safe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in details)
        {
            if (SensitiveKeyFragments.Any(fragment =>
                    pair.Key.Replace("-", string.Empty, StringComparison.Ordinal)
                        .Contains(fragment.Replace("_", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            safe[pair.Key] = pair.Value;
        }

        return safe.Count == 0 ? null : safe;
    }
}
