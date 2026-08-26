using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Core.Commands;

public enum CommandKind
{
    WriteTagValue
}

public sealed record CommandDefinition(
    Guid Id,
    string Key,
    string Name,
    CommandKind Kind,
    Guid TargetTagId,
    string TargetTagPath,
    object? Value,
    string? Description = null,
    string? Area = null,
    string? EquipmentPath = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface ICommandRegistry
{
    IReadOnlyCollection<CommandDefinition> Snapshot();
    bool TryGet(Guid id, out CommandDefinition? command);
    bool TryGetByKey(string key, out CommandDefinition? command);
    void Register(CommandDefinition command);
}

public sealed class InMemoryCommandRegistry : ICommandRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, CommandDefinition> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CommandDefinition> Snapshot()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool TryGet(Guid id, out CommandDefinition? command)
    {
        lock (_sync)
            return _byId.TryGetValue(id, out command);
    }

    public bool TryGetByKey(string key, out CommandDefinition? command)
    {
        lock (_sync)
        {
            if (_byKey.TryGetValue(key, out var id) && _byId.TryGetValue(id, out command))
                return true;

            command = null;
            return false;
        }
    }

    public void Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.TargetTagPath);

        lock (_sync)
        {
            if (_byId.ContainsKey(command.Id))
                throw new InvalidOperationException($"Command id '{command.Id}' is already registered.");
            if (_byKey.ContainsKey(command.Key))
                throw new InvalidOperationException($"Command key '{command.Key}' is already registered.");

            _byId.Add(command.Id, command);
            _byKey.Add(command.Key, command.Id);
        }
    }
}

public static class CommandValueParser
{
    public static bool TryParse(TagDataType dataType, string configuredValue, out object? value)
    {
        configuredValue ??= string.Empty;
        switch (dataType)
        {
            case TagDataType.Boolean:
                if (bool.TryParse(configuredValue, out var boolean))
                {
                    value = boolean;
                    return true;
                }

                if (configuredValue == "1")
                {
                    value = true;
                    return true;
                }

                if (configuredValue == "0")
                {
                    value = false;
                    return true;
                }
                break;

            case TagDataType.Int16:
                if (short.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int16))
                {
                    value = int16;
                    return true;
                }
                break;

            case TagDataType.Int32:
                if (int.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int32))
                {
                    value = int32;
                    return true;
                }
                break;

            case TagDataType.Int64:
                if (long.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int64))
                {
                    value = int64;
                    return true;
                }
                break;

            case TagDataType.Float:
                if (float.TryParse(configuredValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var single))
                {
                    value = single;
                    return true;
                }
                break;

            case TagDataType.Double:
                if (double.TryParse(configuredValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    value = number;
                    return true;
                }
                break;

            case TagDataType.String:
            case TagDataType.Enum:
                value = configuredValue;
                return true;

            case TagDataType.DateTime:
                if (DateTimeOffset.TryParse(configuredValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                {
                    value = timestamp;
                    return true;
                }
                break;
        }

        value = null;
        return false;
    }
}
