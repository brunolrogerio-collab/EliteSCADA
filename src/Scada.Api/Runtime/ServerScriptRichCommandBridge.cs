using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Core.Commands;
using Scada.Core.Interactions;
using Scada.DriverHost.Runtime;
using Scada.Engineering.VisualScripting;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>
/// Binds Server Scripts to the existing Active Rich Command Runtime. The Python
/// process supplies only a declared command ID and scalar parameters; identity,
/// authorization, audit admission and physical execution stay host-owned.
/// </summary>
internal static class ServerScriptRichCommandBridge
{
    private static readonly ConditionalWeakTable<ServerScriptRuntimeManager, BindingSlot> Bindings = new();

    public static void Bind(
        ServerScriptRuntimeManager host,
        IRichCommandDefinitionResolver definitions,
        IRichCommandRuntime commands,
        ApiAuthorizationService authorization,
        ApiAuditService audit)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(audit);

        var slot = Bindings.GetValue(host, static _ => new BindingSlot());
        lock (slot.Sync)
        {
            if ((slot.Definitions is not null && !ReferenceEquals(slot.Definitions, definitions)) ||
                (slot.Commands is not null && !ReferenceEquals(slot.Commands, commands)) ||
                (slot.Authorization is not null && !ReferenceEquals(slot.Authorization, authorization)) ||
                (slot.Audit is not null && !ReferenceEquals(slot.Audit, audit)))
            {
                throw new InvalidOperationException(
                    "Server Script Rich Command bridge cannot be rebound to different runtime authorities.");
            }

            slot.Definitions = definitions;
            slot.Commands = commands;
            slot.Authorization = authorization;
            slot.Audit = audit;
        }
    }

    public static async ValueTask<RichCommandResult> InvokeAsync(
        ServerScriptRuntimeManager host,
        string projectKey,
        long revision,
        PythonScriptDefinition script,
        Guid commandId,
        JsonElement? parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(script);
        if (commandId == Guid.Empty || !IsDeclared(script, commandId))
            throw new ScriptExecutionDiagnosticException(
                "Python handler attempted to invoke an undeclared Rich Command dependency.");

        if (!Bindings.TryGetValue(host, out var slot) ||
            slot.Definitions is null ||
            slot.Commands is null ||
            slot.Authorization is null ||
            slot.Audit is null)
        {
            throw new ScriptExecutionDiagnosticException(
                "Rich Command authority is unavailable for this Server Script host.");
        }

        if (!slot.Definitions.TryResolve(commandId, out var definition) || definition is null)
        {
            throw new ScriptExecutionDiagnosticException(
                $"Rich Command definition '{commandId:D}' is not active in the current Engineering revision.");
        }

        var invocationId = Guid.NewGuid();
        var invocation = BuildInvocation(definition, invocationId, parameters);
        var authorization = slot.Authorization.CheckServerScriptCommand(
            projectKey,
            revision,
            script.Id,
            commandId);
        if (!authorization.Allowed)
        {
            var denied = new RichCommandResult(
                invocationId,
                commandId,
                RichCommandOutcome.Rejected,
                DateTimeOffset.UtcNow,
                "authorization.denied",
                "Server Script is not authorized to execute this Rich Command.");
            await slot.Audit.RecordServerScriptCommandOutcomeAsync(
                    authorization.Principal,
                    projectKey,
                    revision,
                    script.Id,
                    denied)
                .ConfigureAwait(false);
            return denied;
        }

        await slot.Audit.RecordServerScriptCommandAdmissionAsync(
                authorization.Principal,
                projectKey,
                revision,
                script.Id,
                commandId,
                invocationId,
                cancellationToken)
            .ConfigureAwait(false);

        var result = await slot.Commands.InvokeAsync(invocation, cancellationToken)
            .ConfigureAwait(false);
        await slot.Audit.RecordServerScriptCommandOutcomeAsync(
                authorization.Principal,
                projectKey,
                revision,
                script.Id,
                result)
            .ConfigureAwait(false);
        return result;
    }

    private static bool IsDeclared(PythonScriptDefinition script, Guid commandId) =>
        script.Dependencies.Any(dependency =>
            (dependency.Kind.Equals("RichCommand", StringComparison.OrdinalIgnoreCase) ||
             dependency.Kind.Equals("rich-command", StringComparison.OrdinalIgnoreCase)) &&
            Guid.TryParse(dependency.StableReference, out var declaredId) &&
            declaredId == commandId);

    private static RichCommandInvocation BuildInvocation(
        RichCommandDefinition definition,
        Guid invocationId,
        JsonElement? parameters)
    {
        JsonElement supplied;
        if (parameters.HasValue && parameters.Value.ValueKind == JsonValueKind.Object)
            supplied = parameters.Value;
        else if (!parameters.HasValue || parameters.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            supplied = default;
        else
            throw new ScriptExecutionDiagnosticException(
                "Rich Command parameters must be a dictionary of scalar values.");

        var values = new List<RichCommandParameterValue>();
        if (supplied.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in supplied.EnumerateObject())
            {
                if (values.Count >= RichCommandContract.MaximumParameterCount)
                    throw new ScriptExecutionDiagnosticException("Rich Command parameter count exceeds the supported limit.");

                var parameter = definition.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Key, property.Name, StringComparison.Ordinal));
                if (parameter is null)
                    throw new ScriptExecutionDiagnosticException("Rich Command contains an undefined parameter key.");

                values.Add(new RichCommandParameterValue(
                    property.Name,
                    ToScalar(parameter.Schema, property.Value)));
            }
        }

        var invocation = new RichCommandInvocation(
            invocationId,
            definition.CommandId,
            values,
            DateTimeOffset.UtcNow,
            new InteractionCausalityContext(invocationId, null, InteractionOrigin.ServerScript));
        try
        {
            return RichCommandContract.ValidateInvocation(definition, invocation);
        }
        catch (ArgumentException ex)
        {
            throw new ScriptExecutionDiagnosticException(
                $"Rich Command parameters failed canonical validation ({ex.GetType().Name}).");
        }
    }

    private static InteractionScalarValue ToScalar(
        InteractionScalarSchema schema,
        JsonElement value)
    {
        try
        {
            return schema.Kind switch
            {
                InteractionScalarKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                    InteractionScalarValue.Boolean(value.GetBoolean()),
                InteractionScalarKind.Integer when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer) =>
                    InteractionScalarValue.Integer(integer),
                InteractionScalarKind.Number when value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) =>
                    InteractionScalarValue.Number(number),
                InteractionScalarKind.Percentage when value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var percentage) =>
                    InteractionScalarValue.Percentage(percentage),
                InteractionScalarKind.String when value.ValueKind == JsonValueKind.String =>
                    InteractionScalarValue.String(value.GetString()!),
                InteractionScalarKind.Enum when value.ValueKind == JsonValueKind.String =>
                    InteractionScalarValue.Enum(value.GetString()!),
                InteractionScalarKind.Duration when value.ValueKind == JsonValueKind.String &&
                    TimeSpan.TryParseExact(value.GetString(), "c", CultureInfo.InvariantCulture, out var duration) =>
                    InteractionScalarValue.Duration(duration),
                _ => throw new ArgumentException("Scalar JSON value does not match its declared schema.")
            };
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new ScriptExecutionDiagnosticException(
                $"Rich Command parameter failed canonical scalar validation ({ex.GetType().Name}).");
        }
    }

    private sealed class BindingSlot
    {
        public object Sync { get; } = new();
        public IRichCommandDefinitionResolver? Definitions { get; set; }
        public IRichCommandRuntime? Commands { get; set; }
        public ApiAuthorizationService? Authorization { get; set; }
        public ApiAuditService? Audit { get; set; }
    }
}
