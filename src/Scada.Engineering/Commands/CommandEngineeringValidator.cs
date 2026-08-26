using Scada.Core.Commands;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Commands;

public static class CommandEngineeringValidator
{
    public static IReadOnlyCollection<ImportIssue> Validate(CommandEngineeringDto command)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(command.Key) ? command.Name : command.Key;

        if (string.IsNullOrWhiteSpace(command.Key))
            issues.Add(Error("COMMAND_KEY_REQUIRED", "Command key is required.", key));
        if (command.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("COMMAND_KEY_WHITESPACE", "Command key cannot contain whitespace.", key));
        if (string.IsNullOrWhiteSpace(command.Name))
            issues.Add(Error("COMMAND_NAME_REQUIRED", "Command name is required.", key));
        if (command.TargetTagId is null && string.IsNullOrWhiteSpace(command.TargetTagPath))
            issues.Add(Error("COMMAND_TARGET_TAG_REQUIRED", "Command must reference a target TAG by TargetTagId or TargetTagPath.", key));
        if (command.TargetTagPath?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("COMMAND_TARGET_TAG_PATH_WHITESPACE", "Command target TAG path cannot contain whitespace.", key));
        if (command.EquipmentPath?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("COMMAND_EQUIPMENT_PATH_WHITESPACE", "Command equipment path cannot contain whitespace.", key));
        if (command.Value is null)
            issues.Add(Error("COMMAND_VALUE_REQUIRED", "Command configured value is required.", key));
        if (!Enum.IsDefined(command.Kind))
            issues.Add(Error("COMMAND_KIND_INVALID", $"Command kind '{command.Kind}' is not supported.", key));

        return issues;
    }

    public static ImportIssue? ValidateTargetValue(CommandEngineeringDto command, TagDefinition tag)
    {
        if (command.Kind != CommandKind.WriteTagValue)
            return Error("COMMAND_KIND_INVALID", $"Command kind '{command.Kind}' is not supported.", command.Key);

        if (tag.ReadOnly)
            return Error(
                "COMMAND_TARGET_TAG_READ_ONLY",
                $"Command '{command.Key}' targets read-only TAG '{tag.Path}'.",
                command.Key);

        if (!CommandValueParser.TryParse(tag.DataType, command.Value, out _))
        {
            return Error(
                "COMMAND_VALUE_INVALID",
                $"Command '{command.Key}' value '{command.Value}' cannot be converted to target TAG data type '{tag.DataType}'.",
                command.Key);
        }

        return null;
    }

    private static ImportIssue Error(string code, string message, string key) =>
        new(code, message, ImportEntityKind.Command, key, true);
}
