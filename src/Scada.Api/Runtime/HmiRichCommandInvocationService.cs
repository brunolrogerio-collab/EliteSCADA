using System.Globalization;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Core.Commands;
using Scada.Core.Interactions;
using Scada.DriverHost.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record HmiRichCommandInvocationRequest(
    Guid CommandId,
    IReadOnlyDictionary<string, JsonElement>? Parameters = null);

public sealed record HmiRichCommandInvocationResponse(
    Guid InvocationId,
    Guid CommandId,
    string Outcome,
    DateTimeOffset ObservedAt,
    string? Code,
    string? Message);

public sealed record HmiRichCommandParameterSchema(
    string Kind,
    decimal? Minimum,
    decimal? Maximum,
    int? MaximumLength,
    IReadOnlyList<string>? EnumValues,
    string? Unit);

public sealed record HmiRichCommandParameterDefinition(
    string Key,
    bool Required,
    string? Description,
    HmiRichCommandParameterSchema Schema);

public sealed record HmiRichCommandDefinition(
    Guid CommandId,
    string SemanticKey,
    string? Description,
    IReadOnlyList<HmiRichCommandParameterDefinition> Parameters);

public sealed record HmiRichCommandDefinitionLookup(
    int StatusCode,
    HmiRichCommandDefinition? Definition,
    string? Error = null);

public interface IRichCommandHmiAuthorization
{
    SecurityPrincipal GetPrincipal(HttpContext context);

    Task<ApiAuthorizationCheck> CheckAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        RichCommandDefinition definition,
        bool requireRuntimeSession,
        CancellationToken cancellationToken);
}

public sealed class ApiRichCommandHmiAuthorization(ApiAuthorizationService security)
    : IRichCommandHmiAuthorization
{
    public SecurityPrincipal GetPrincipal(HttpContext context) =>
        security.GetPrincipal(context);

    public Task<ApiAuthorizationCheck> CheckAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        RichCommandDefinition definition,
        bool requireRuntimeSession,
        CancellationToken cancellationToken)
    {
        var resource = new AuthorizationResource(
            CommandKey: definition.SemanticKey,
            ResourceKind: AuthorizationResourceKind.Command,
            ResourceId: definition.CommandId);
        return requireRuntimeSession
            ? security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.CommandExecute,
                resource,
                cancellationToken)
            : security.CheckRuntimeAsync(
                security.GetPrincipal(context),
                runtime,
                SecurityCapability.CommandExecute,
                resource,
                cancellationToken);
    }
}

/// <summary>
/// Human HMI caller boundary for Active Rich Commands. The browser supplies only
/// the canonical CommandId and typed scalar values; definition, binding, identity,
/// authorization and dispatch remain server-owned.
/// </summary>
public sealed class HmiRichCommandInvocationService(
    IRichCommandDefinitionResolver definitions,
    IRichCommandBindingResolver bindings,
    IRichCommandRuntime commands,
    IRichCommandHmiAuthorization authorization,
    ApiAuditService audit)
{
    public async Task<HmiRichCommandDefinitionLookup> GetActiveDefinitionAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty ||
            !definitions.TryResolve(commandId, out var definition) ||
            definition is null)
        {
            return new(StatusCodes.Status404NotFound, null, "Active Rich Command was not found.");
        }

        var normalized = RichCommandContract.NormalizeDefinition(definition);
        var before = runtime.Describe();
        var check = await authorization.CheckAsync(context, runtime, normalized, requireRuntimeSession: false, cancellationToken)
            .ConfigureAwait(false);
        if (!check.Allowed)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                check,
                AuditActions.CommandExecute,
                "rich-command",
                commandId.ToString("D"),
                new Dictionary<string, string> { ["commandOutcome"] = "Rejected" })
                .ConfigureAwait(false);
            return new(
                check.IsAuthenticated ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized,
                null,
                "Rich Command invocation is not authorized.");
        }

        if (!SameRuntime(before, runtime.Describe()) ||
            !definitions.TryResolve(commandId, out var activeDefinition) ||
            !ReferenceEquals(definition, activeDefinition))
        {
            return new(StatusCodes.Status409Conflict, null, "Active Rich Command changed during authorization.");
        }

        return new(StatusCodes.Status200OK, ToResponse(normalized));
    }

    public async Task<HmiRichCommandInvocationResponse> InvokeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        Guid routeCommandId,
        HmiRichCommandInvocationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var invocationId = Guid.NewGuid();
        var principal = authorization.GetPrincipal(context);

        if (routeCommandId == Guid.Empty ||
            request is null ||
            request.CommandId != routeCommandId)
        {
            var mismatch = Rejected(
                invocationId,
                routeCommandId,
                "invocation.command_id_mismatch",
                "The Rich Command identity is invalid.");
            await RecordOutcomeAsync(context, principal, mismatch).ConfigureAwait(false);
            return mismatch;
        }

        if (!definitions.TryResolve(routeCommandId, out var definition) || definition is null)
        {
            var missing = Rejected(
                invocationId,
                routeCommandId,
                "definition.not_found",
                "The Rich Command is not Active.");
            await RecordOutcomeAsync(context, principal, missing).ConfigureAwait(false);
            return missing;
        }

        var normalized = RichCommandContract.NormalizeDefinition(definition);
        var before = runtime.Describe();
        var hasBinding = bindings.TryResolve(routeCommandId, out var binding);
        var check = await authorization.CheckAsync(context, runtime, normalized, requireRuntimeSession: true, cancellationToken)
            .ConfigureAwait(false);
        if (!check.Allowed)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                check,
                AuditActions.CommandExecute,
                "rich-command",
                routeCommandId.ToString("D"),
                new Dictionary<string, string>
                {
                    ["invocationId"] = invocationId.ToString("D"),
                    ["commandOutcome"] = "Rejected"
                }).ConfigureAwait(false);
            return Rejected(
                invocationId,
                routeCommandId,
                "authorization.denied",
                "The authenticated user is not authorized to execute this Rich Command.");
        }

        if (!SameRuntime(before, runtime.Describe()) ||
            !definitions.TryResolve(routeCommandId, out var activeDefinition) ||
            !ReferenceEquals(definition, activeDefinition))
        {
            var changed = Rejected(
                invocationId,
                routeCommandId,
                "runtime.changed_during_authorization",
                "The Active Runtime changed during authorization.");
            await RecordOutcomeAsync(context, check.Principal, changed).ConfigureAwait(false);
            return changed;
        }

        RichCommandInvocation invocation;
        try
        {
            invocation = BuildInvocation(normalized, invocationId, request.Parameters);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            var invalid = Rejected(
                invocationId,
                routeCommandId,
                "invocation.invalid",
                "Typed Rich Command parameters are invalid.");
            await RecordOutcomeAsync(context, check.Principal, invalid).ConfigureAwait(false);
            return invalid;
        }

        if (!hasBinding || binding is null)
        {
            var missingBinding = Rejected(
                invocationId,
                routeCommandId,
                "binding.not_found",
                "The Active Rich Command has no authoritative binding.");
            await RecordOutcomeAsync(context, check.Principal, missingBinding).ConfigureAwait(false);
            return missingBinding;
        }

        try
        {
            _ = RichCommandContract.ValidateBinding(normalized, binding);
        }
        catch (ArgumentException)
        {
            var invalidBinding = Rejected(
                invocationId,
                routeCommandId,
                "binding.invalid",
                "The Active Rich Command binding is invalid.");
            await RecordOutcomeAsync(context, check.Principal, invalidBinding).ConfigureAwait(false);
            return invalidBinding;
        }

        if (!bindings.TryResolve(routeCommandId, out var activeBinding) ||
            !ReferenceEquals(binding, activeBinding))
        {
            var changedBinding = Rejected(
                invocationId,
                routeCommandId,
                "binding.changed_during_authorization",
                "The Active Rich Command binding changed before dispatch.");
            await RecordOutcomeAsync(context, check.Principal, changedBinding).ConfigureAwait(false);
            return changedBinding;
        }

        RichCommandResult result;
        try
        {
            result = await commands.InvokeAsync(invocation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Once the Runtime boundary is called, an unclassified exception cannot prove
            // whether dispatch occurred. Return Unknown and never ask the browser to retry.
            result = new RichCommandResult(
                invocationId,
                routeCommandId,
                RichCommandOutcome.Unknown,
                DateTimeOffset.UtcNow,
                "execution.exception_ambiguous",
                "The Rich Command outcome is unknown.");
        }

        var response = ToResponse(result);
        await RecordOutcomeAsync(context, check.Principal, response).ConfigureAwait(false);
        return response;
    }

    private async ValueTask RecordOutcomeAsync(
        HttpContext context,
        SecurityPrincipal principal,
        HmiRichCommandInvocationResponse response)
    {
        var outcome = response.Outcome switch
        {
            "Accepted" or "Completed" => AuditOutcome.Succeeded,
            "Rejected" when response.Code?.StartsWith("authorization.", StringComparison.Ordinal) == true =>
                AuditOutcome.Denied,
            _ => AuditOutcome.Failed
        };
        var details = new Dictionary<string, string>
        {
            ["invocationId"] = response.InvocationId.ToString("D"),
            ["commandOutcome"] = response.Outcome
        };
        if (!string.IsNullOrWhiteSpace(response.Code))
            details["resultCode"] = response.Code;

        await audit.RecordAsync(
            context,
            principal,
            AuditActions.CommandExecute,
            outcome,
            "rich-command",
            response.CommandId.ToString("D"),
            details).ConfigureAwait(false);
    }

    private static HmiRichCommandInvocationResponse Rejected(
        Guid invocationId,
        Guid commandId,
        string code,
        string message) =>
        new(invocationId, commandId, RichCommandOutcome.Rejected.ToString(), DateTimeOffset.UtcNow, code, message);

    private static HmiRichCommandInvocationResponse ToResponse(RichCommandResult result) =>
        new(
            result.InvocationId,
            result.CommandId,
            result.Outcome.ToString(),
            result.ObservedAt,
            result.Code,
            result.Message);

    private static HmiRichCommandDefinition ToResponse(RichCommandDefinition definition) =>
        new(
            definition.CommandId,
            definition.SemanticKey,
            definition.Description,
            definition.Parameters.Select(parameter => new HmiRichCommandParameterDefinition(
                parameter.Key,
                parameter.Required,
                parameter.Description,
                new HmiRichCommandParameterSchema(
                    parameter.Schema.Kind.ToString(),
                    parameter.Schema.Minimum,
                    parameter.Schema.Maximum,
                    parameter.Schema.MaximumLength,
                    parameter.Schema.EnumValues?.ToArray(),
                    parameter.Schema.Unit))).ToArray());

    private static RichCommandInvocation BuildInvocation(
        RichCommandDefinition definition,
        Guid invocationId,
        IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if (parameters is { Count: > RichCommandContract.MaximumParameterCount })
            throw new ArgumentException("Rich Command parameter count exceeds the supported limit.", nameof(parameters));

        var values = new List<RichCommandParameterValue>();
        foreach (var pair in parameters ?? new Dictionary<string, JsonElement>())
        {
            var parameter = definition.Parameters.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, pair.Key, StringComparison.Ordinal));
            if (parameter is null)
                throw new ArgumentException("Rich Command contains an undefined parameter key.", nameof(parameters));

            values.Add(new RichCommandParameterValue(
                pair.Key,
                ToScalar(parameter.Schema, pair.Value)));
        }

        return RichCommandContract.ValidateInvocation(
            definition,
            new RichCommandInvocation(
                invocationId,
                definition.CommandId,
                values,
                DateTimeOffset.UtcNow,
                new InteractionCausalityContext(invocationId, null, InteractionOrigin.Hmi)));
    }

    private static InteractionScalarValue ToScalar(InteractionScalarSchema schema, JsonElement value)
    {
        return schema.Kind switch
        {
            InteractionScalarKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                InteractionScalarValue.Boolean(value.GetBoolean()),
            InteractionScalarKind.Integer when TryInt64(value, out var integer) =>
                InteractionScalarValue.Integer(integer),
            InteractionScalarKind.Number when TryDecimal(value, out var number) =>
                InteractionScalarValue.Number(number),
            InteractionScalarKind.Percentage when TryDecimal(value, out var percentage) =>
                InteractionScalarValue.Percentage(percentage),
            InteractionScalarKind.String when value.ValueKind == JsonValueKind.String =>
                InteractionScalarValue.String(value.GetString()!),
            InteractionScalarKind.Enum when value.ValueKind == JsonValueKind.String =>
                InteractionScalarValue.Enum(value.GetString()!),
            InteractionScalarKind.Duration when value.ValueKind == JsonValueKind.String &&
                TimeSpan.TryParseExact(value.GetString(), "c", CultureInfo.InvariantCulture, out var duration) =>
                InteractionScalarValue.Duration(duration),
            _ => throw new ArgumentException(
                $"Typed Rich Command parameter does not match scalar kind '{schema.Kind}'.",
                nameof(value))
        };
    }

    private static bool TryInt64(JsonElement value, out long result)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt64(out result);

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
                   result.ToString(CultureInfo.InvariantCulture) == text;
        }

        result = default;
        return false;
    }

    private static bool TryDecimal(JsonElement value, out decimal result)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetDecimal(out result);

        if (value.ValueKind == JsonValueKind.String)
            return decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out result);

        result = default;
        return false;
    }

    private static bool SameRuntime(ScadaRuntimeDescriptor left, ScadaRuntimeDescriptor right) =>
        left.Revision == right.Revision &&
        left.ActivatedAtUtc == right.ActivatedAtUtc &&
        string.Equals(left.Mode, right.Mode, StringComparison.Ordinal) &&
        string.Equals(left.ProjectKey, right.ProjectKey, StringComparison.OrdinalIgnoreCase);
}

public static class HmiRichCommandEndpointExtensions
{
    public static WebApplication MapHmiRichCommandEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runtime/rich-commands/{id:guid}/definition", async (
            Guid id,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            HmiRichCommandInvocationService commands,
            CancellationToken cancellationToken) =>
        {
            var result = await commands.GetActiveDefinitionAsync(context, runtime, id, cancellationToken);
            if (result.StatusCode == StatusCodes.Status200OK)
                return Results.Json(result.Definition);
            return Results.Json(new { error = result.Error }, statusCode: result.StatusCode);
        });

        app.MapPost("/api/runtime/rich-commands/{id:guid}/execute", async (
            Guid id,
            HmiRichCommandInvocationRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            HmiRichCommandInvocationService commands,
            CancellationToken cancellationToken) =>
        {
            var result = await commands.InvokeAsync(context, runtime, id, request, cancellationToken);
            return Results.Json(result);
        });

        return app;
    }
}
