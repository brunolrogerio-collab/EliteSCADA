using Scada.Security.Audit;
using Scada.Security.Authorization;
using Scada.Core.Commands;

namespace Scada.Api.Security;

public sealed class AuditAdmissionUnavailableException : InvalidOperationException
{
    public AuditAdmissionUnavailableException(Exception innerException)
        : base("Audit persistence is unavailable for a protected mutation.", innerException)
    {
    }
}

public sealed class ApiAuditService(
    IAuditSink sink,
    IAuditStore store,
    ILogger<ApiAuditService> logger)
{
    private static readonly string[] SensitiveKeyFragments =
    {
        "password",
        "token",
        "secret",
        "privatekey",
        "signingkey",
        "authorization"
    };

    public async ValueTask RecordMutationAdmissionAsync(
        HttpContext context,
        SecurityPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var subjectId = SubjectId(principal);
        var displayName = principal.IsAuthenticated ? principal.DisplayName : null;
        var path = context.Request.Path.Value ?? "/api";
        var method = context.Request.Method.ToUpperInvariant();

        try
        {
            await store.WriteAsync(
                AuditEvent.Create(
                    subjectId,
                    displayName,
                    AuditActions.ProtectedMutationAdmission,
                    AuditOutcome.Succeeded,
                    "api-route",
                    path,
                    new Dictionary<string, string>
                    {
                        ["method"] = method,
                        ["path"] = path
                    },
                    context.TraceIdentifier,
                    roles: principal.Roles,
                    source: "api-admission"),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Protected API mutation admission could not be persisted for {Method} {Path} and subject {SubjectId}.",
                method,
                path,
                subjectId);
            throw new AuditAdmissionUnavailableException(ex);
        }
    }

    public async ValueTask RecordServerScriptCommandAdmissionAsync(
        SecurityPrincipal principal,
        string projectKey,
        long revision,
        Guid scriptId,
        Guid commandId,
        Guid invocationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await store.WriteAsync(
                AuditEvent.Create(
                    SubjectId(principal),
                    principal.DisplayName,
                    AuditActions.ProtectedMutationAdmission,
                    AuditOutcome.Succeeded,
                    "rich-command",
                    commandId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["scriptId"] = scriptId.ToString("D"),
                        ["invocationId"] = invocationId.ToString("D")
                    },
                    invocationId.ToString("D"),
                    projectKey: projectKey,
                    revision: revision,
                    roles: principal.Roles,
                    source: "server-script-admission"),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Server Script Rich Command admission could not be persisted for project {ProjectKey}, script {ScriptId}, command {CommandId}.",
                projectKey,
                scriptId,
                commandId);
            throw new AuditAdmissionUnavailableException(ex);
        }
    }

    public async ValueTask RecordServerScriptCommandOutcomeAsync(
        SecurityPrincipal principal,
        string projectKey,
        long revision,
        Guid scriptId,
        RichCommandResult result)
    {
        var outcome = result.Outcome switch
        {
            RichCommandOutcome.Accepted or RichCommandOutcome.Completed => AuditOutcome.Succeeded,
            RichCommandOutcome.Rejected when result.Code?.StartsWith("authorization.", StringComparison.Ordinal) == true => AuditOutcome.Denied,
            _ => AuditOutcome.Failed
        };
        var details = new Dictionary<string, string>
        {
            ["scriptId"] = scriptId.ToString("D"),
            ["invocationId"] = result.InvocationId.ToString("D"),
            ["commandOutcome"] = result.Outcome.ToString()
        };
        if (!string.IsNullOrWhiteSpace(result.Code))
            details["resultCode"] = result.Code;

        try
        {
            await sink.WriteAsync(
                AuditEvent.Create(
                    SubjectId(principal),
                    principal.DisplayName,
                    AuditActions.CommandExecute,
                    outcome,
                    "rich-command",
                    result.CommandId.ToString("D"),
                    Sanitize(details),
                    result.InvocationId.ToString("D"),
                    projectKey: projectKey,
                    revision: revision,
                    roles: principal.Roles,
                    source: "server-script"),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist Server Script Rich Command outcome for project {ProjectKey}, script {ScriptId}, command {CommandId}, invocation {InvocationId}.",
                projectKey,
                scriptId,
                result.CommandId,
                result.InvocationId);
        }
    }

    public async ValueTask RecordAsync(
        HttpContext context,
        SecurityPrincipal principal,
        string action,
        AuditOutcome outcome,
        string targetKind,
        string targetId,
        IReadOnlyDictionary<string, string>? details = null)
    {
        var subjectId = SubjectId(principal);
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
            // Do not turn a post-action audit failure into an endpoint failure: a client could retry a
            // process command that already happened. Unsafe /api requests are admitted only after a
            // direct append-only store write, so a missing detailed outcome leaves durable ambiguity
            // evidence instead of making the protected action disappear from the audit trail.
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

    private static string SubjectId(SecurityPrincipal principal) =>
        principal.IsAuthenticated && !string.IsNullOrWhiteSpace(principal.SubjectId)
            ? principal.SubjectId.Trim()
            : "anonymous";

    private static IReadOnlyDictionary<string, string>? Sanitize(
        IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0) return null;

        var safe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in details)
        {
            var normalizedKey = NormalizeKey(pair.Key);
            if (SensitiveKeyFragments.Any(fragment =>
                    normalizedKey.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            safe[pair.Key] = pair.Value;
        }

        return safe.Count == 0 ? null : safe;
    }

    private static string NormalizeKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray());
}
