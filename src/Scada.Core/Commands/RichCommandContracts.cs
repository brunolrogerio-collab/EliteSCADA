using Scada.Core.Interactions;

namespace Scada.Core.Commands;

public sealed record RichCommandParameterDefinition(
    string Key,
    InteractionScalarSchema Schema,
    bool Required = false,
    string? Description = null);

public sealed record RichCommandParameterValue(
    string Key,
    InteractionScalarValue Value);

public sealed record RichCommandDefinition(
    Guid CommandId,
    string SemanticKey,
    IReadOnlyList<RichCommandParameterDefinition> Parameters,
    string? Description = null);

public sealed record RichCommandInvocation(
    Guid InvocationId,
    Guid CommandId,
    IReadOnlyList<RichCommandParameterValue> Parameters,
    DateTimeOffset RequestedAt,
    InteractionCausalityContext? Causality = null);

public enum RichCommandOutcome
{
    Rejected,
    Accepted,
    Completed,
    Failed,
    TimedOut,
    Unknown
}

/// <summary>
/// Protocol-neutral command result. Accepted only means that the command was
/// accepted at the relevant command boundary; it does not confirm process state.
/// Completed records command-execution completion evidence and still does not
/// replace truthful TAG state. Unknown represents an ambiguous physical/remote
/// outcome.
/// </summary>
public sealed record RichCommandResult(
    Guid InvocationId,
    Guid CommandId,
    RichCommandOutcome Outcome,
    DateTimeOffset ObservedAt,
    string? Code = null,
    string? Message = null);

public sealed record DriverCommandBindingSetting(
    string Key,
    string Value);

/// <summary>
/// Server-owned binding from one canonical Rich Command to a driver-side semantic
/// operation. Callers do not supply this envelope when invoking a command.
/// Protected material is intentionally absent and must remain under the existing
/// host authority.
/// </summary>
public sealed record DriverCommandBinding(
    Guid CommandId,
    Guid DataSourceId,
    string StableDeviceIdentity,
    string SemanticOperationKey,
    IReadOnlyList<DriverCommandBindingSetting>? Settings = null,
    Guid? EquipmentId = null,
    string? CapabilityId = null,
    int Version = 1);

public static class RichCommandContract
{
    public const int Version = 1;
    public const int DriverBindingVersion = 1;
    public const int MaximumParameterCount = 32;
    public const int MaximumParameterKeyLength = 128;
    public const int MaximumSemanticKeyLength = 160;
    public const int MaximumDescriptionLength = 1000;
    public const int MaximumResultCodeLength = 160;
    public const int MaximumResultMessageLength = 2000;
    public const int MaximumBindingSettingCount = 32;
    public const int MaximumBindingSettingKeyLength = 128;
    public const int MaximumBindingSettingValueLength = 1024;
    public const int MaximumSourceIdentityLength = 500;
    public const int MaximumCapabilityIdLength = 160;

    private static readonly HashSet<string> ReservedBindingTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "driver",
        "drivertype",
        "protocol",
        "datasource",
        "datasourceid",
        "topic",
        "mqtt",
        "service",
        "homeassistant",
        "zigbee",
        "zwave",
        "cluster",
        "commandclass",
        "rpc",
        "method",
        "endpoint",
        "url",
        "uri",
        "secret",
        "secretref",
        "secretreference",
        "password",
        "credential",
        "credentials",
        "token",
        "apikey",
        "principal",
        "role",
        "roles",
        "project",
        "projectid",
        "revision",
        "revisionid"
    };

    private static readonly HashSet<string> ReservedOperationTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "mqtt",
        "topic",
        "homeassistant",
        "service",
        "zigbee",
        "zwave",
        "cluster",
        "commandclass",
        "rpc",
        "method"
    };

    private static readonly string[] ProtectedMaterialPrefixes =
    [
        "secret:",
        "secret://",
        "protected:",
        "protected://",
        "vault:",
        "vault://"
    ];

    public static RichCommandDefinition NormalizeDefinition(RichCommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.CommandId == Guid.Empty)
            throw new ArgumentException("Rich Command CommandId is required.", nameof(definition));

        var semanticKey = BoundedKey(
            definition.SemanticKey,
            MaximumSemanticKeyLength,
            "Rich Command semantic key",
            nameof(definition));

        if (definition.Parameters is null)
            throw new ArgumentException("Rich Command parameter schema is required.", nameof(definition));
        if (definition.Parameters.Count > MaximumParameterCount)
            throw new ArgumentException(
                $"Rich Command supports at most {MaximumParameterCount} parameters.",
                nameof(definition));

        var normalizedParameters = new List<RichCommandParameterDefinition>(definition.Parameters.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in definition.Parameters)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            var key = BoundedKey(
                parameter.Key,
                MaximumParameterKeyLength,
                "Rich Command parameter key",
                nameof(definition));
            if (!keys.Add(key))
                throw new ArgumentException($"Rich Command parameter '{key}' is duplicated.", nameof(definition));

            normalizedParameters.Add(parameter with
            {
                Key = key,
                Schema = InteractionScalarContract.NormalizeSchema(parameter.Schema),
                Description = Optional(
                    parameter.Description,
                    MaximumDescriptionLength,
                    "Rich Command parameter description")
            });
        }

        normalizedParameters.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));

        return definition with
        {
            SemanticKey = semanticKey,
            Parameters = normalizedParameters,
            Description = Optional(definition.Description, MaximumDescriptionLength, "Rich Command description")
        };
    }

    public static RichCommandInvocation ValidateInvocation(
        RichCommandDefinition definition,
        RichCommandInvocation invocation)
    {
        var normalizedDefinition = NormalizeDefinition(definition);
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation.InvocationId == Guid.Empty)
            throw new ArgumentException("Rich Command InvocationId is required.", nameof(invocation));
        if (invocation.CommandId != normalizedDefinition.CommandId)
            throw new ArgumentException("Rich Command invocation CommandId does not match the definition.", nameof(invocation));
        if (invocation.RequestedAt == default)
            throw new ArgumentException("Rich Command RequestedAt is required.", nameof(invocation));
        if (invocation.Parameters is null)
            throw new ArgumentException("Rich Command invocation parameters are required.", nameof(invocation));
        if (invocation.Parameters.Count > MaximumParameterCount)
            throw new ArgumentException(
                $"Rich Command invocation supports at most {MaximumParameterCount} parameters.",
                nameof(invocation));

        var supplied = new Dictionary<string, RichCommandParameterValue>(StringComparer.Ordinal);
        foreach (var parameterValue in invocation.Parameters)
        {
            ArgumentNullException.ThrowIfNull(parameterValue);
            var key = BoundedKey(
                parameterValue.Key,
                MaximumParameterKeyLength,
                "Rich Command parameter key",
                nameof(invocation));
            if (!supplied.TryAdd(key, parameterValue with { Key = key }))
                throw new ArgumentException($"Rich Command parameter '{key}' is duplicated.", nameof(invocation));
        }

        var schemaByKey = normalizedDefinition.Parameters.ToDictionary(parameter => parameter.Key, StringComparer.Ordinal);
        foreach (var suppliedKey in supplied.Keys)
        {
            if (!schemaByKey.ContainsKey(suppliedKey))
                throw new ArgumentException(
                    $"Rich Command parameter '{suppliedKey}' is not defined.",
                    nameof(invocation));
        }

        foreach (var parameter in normalizedDefinition.Parameters)
        {
            if (!supplied.TryGetValue(parameter.Key, out var value))
            {
                if (parameter.Required)
                    throw new ArgumentException(
                        $"Rich Command required parameter '{parameter.Key}' is missing.",
                        nameof(invocation));
                continue;
            }

            supplied[parameter.Key] = value with
            {
                Value = InteractionScalarContract.Validate(parameter.Schema, value.Value)
            };
        }

        return invocation with
        {
            Parameters = supplied.Values
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToArray(),
            RequestedAt = invocation.RequestedAt.ToUniversalTime(),
            Causality = invocation.Causality is null
                ? null
                : InteractionCausalityContract.Validate(invocation.Causality)
        };
    }

    public static RichCommandResult ValidateResult(
        RichCommandInvocation invocation,
        RichCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(result);

        if (result.InvocationId == Guid.Empty || result.InvocationId != invocation.InvocationId)
            throw new ArgumentException("Rich Command result InvocationId does not match the invocation.", nameof(result));
        if (result.CommandId == Guid.Empty || result.CommandId != invocation.CommandId)
            throw new ArgumentException("Rich Command result CommandId does not match the invocation.", nameof(result));
        if (!Enum.IsDefined(typeof(RichCommandOutcome), result.Outcome))
            throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unsupported Rich Command outcome.");
        if (result.ObservedAt == default)
            throw new ArgumentException("Rich Command result ObservedAt is required.", nameof(result));

        var code = OptionalKey(
            result.Code,
            MaximumResultCodeLength,
            "Rich Command result code",
            nameof(result));
        var message = Optional(result.Message, MaximumResultMessageLength, "Rich Command result message");

        return result with
        {
            ObservedAt = result.ObservedAt.ToUniversalTime(),
            Code = code,
            Message = message
        };
    }

    public static DriverCommandBinding ValidateBinding(
        RichCommandDefinition definition,
        DriverCommandBinding binding)
    {
        var normalizedDefinition = NormalizeDefinition(definition);
        var normalizedBinding = NormalizeBinding(binding);
        if (normalizedBinding.CommandId != normalizedDefinition.CommandId)
            throw new ArgumentException("Driver Command Binding CommandId does not match the Rich Command definition.", nameof(binding));
        return normalizedBinding;
    }

    public static DriverCommandBinding NormalizeBinding(DriverCommandBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.CommandId == Guid.Empty)
            throw new ArgumentException("Driver Command Binding CommandId is required.", nameof(binding));
        if (binding.DataSourceId == Guid.Empty)
            throw new ArgumentException("Driver Command Binding DataSourceId is required.", nameof(binding));
        if (binding.EquipmentId == Guid.Empty)
            throw new ArgumentException("Driver Command Binding EquipmentId cannot be empty when supplied.", nameof(binding));
        if (binding.Version != DriverBindingVersion)
            throw new ArgumentException(
                $"Driver Command Binding version '{binding.Version}' is unsupported; expected {DriverBindingVersion}.",
                nameof(binding));

        var stableDeviceIdentity = Required(
            binding.StableDeviceIdentity,
            MaximumSourceIdentityLength,
            "Driver Command Binding stable device identity");

        var semanticOperationKey = BoundedKey(
            binding.SemanticOperationKey,
            MaximumSemanticKeyLength,
            "Driver Command Binding semantic operation key",
            nameof(binding));
        RejectReservedTokens(
            semanticOperationKey,
            ReservedOperationTokens,
            "Driver Command Binding semantic operation key cannot encode raw protocol routing.",
            nameof(binding));

        var capabilityId = Optional(
            binding.CapabilityId,
            MaximumCapabilityIdLength,
            "Driver Command Binding CapabilityId");
        if (capabilityId is not null && binding.EquipmentId is null)
            throw new ArgumentException(
                "Driver Command Binding CapabilityId requires EquipmentId.",
                nameof(binding));

        var settings = binding.Settings ?? Array.Empty<DriverCommandBindingSetting>();
        if (settings.Count > MaximumBindingSettingCount)
            throw new ArgumentException(
                $"Driver Command Binding supports at most {MaximumBindingSettingCount} settings.",
                nameof(binding));

        var normalizedSettings = new List<DriverCommandBindingSetting>(settings.Count);
        var settingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setting in settings)
        {
            ArgumentNullException.ThrowIfNull(setting);
            var key = BoundedKey(
                setting.Key,
                MaximumBindingSettingKeyLength,
                "Driver Command Binding setting key",
                nameof(binding));

            if (!settingKeys.Add(key))
                throw new ArgumentException($"Driver Command Binding setting '{key}' is duplicated.", nameof(binding));

            RejectReservedTokens(
                key,
                ReservedBindingTokens,
                $"Driver Command Binding setting '{key}' is reserved for host authority or raw protocol routing.",
                nameof(binding));

            if (setting.Value is null)
                throw new ArgumentException($"Driver Command Binding setting '{key}' value is required.", nameof(binding));
            if (setting.Value.Length > MaximumBindingSettingValueLength)
                throw new ArgumentException(
                    $"Driver Command Binding setting '{key}' exceeds {MaximumBindingSettingValueLength} characters.",
                    nameof(binding));
            if (setting.Value.Any(char.IsControl))
                throw new ArgumentException(
                    $"Driver Command Binding setting '{key}' contains control characters.",
                    nameof(binding));
            if (ProtectedMaterialPrefixes.Any(prefix =>
                    setting.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $"Driver Command Binding setting '{key}' cannot carry protected-material references.",
                    nameof(binding));
            }

            normalizedSettings.Add(setting with { Key = key });
        }

        normalizedSettings.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));

        return binding with
        {
            StableDeviceIdentity = stableDeviceIdentity,
            SemanticOperationKey = semanticOperationKey,
            Settings = normalizedSettings,
            CapabilityId = capabilityId
        };
    }

    private static void RejectReservedTokens(
        string value,
        HashSet<string> reserved,
        string message,
        string parameterName)
    {
        var tokens = value
            .Replace('_', '.')
            .Replace('-', '.')
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Any(reserved.Contains))
            throw new ArgumentException(message, parameterName);
    }

    private static string BoundedKey(
        string? value,
        int maximumLength,
        string field,
        string parameterName)
    {
        var normalized = Required(value, maximumLength, field);
        if (!normalized.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '-' or ':'))
        {
            throw new ArgumentException($"{field} contains unsupported characters.", parameterName);
        }

        return normalized;
    }

    private static string? OptionalKey(
        string? value,
        int maximumLength,
        string field,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return BoundedKey(value, maximumLength, field, parameterName);
    }

    private static string Required(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"{field} exceeds {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Required(value, maximumLength, field);
    }
}
