using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class CommandEngineeringHandler
{
    private readonly ICommandEngineeringRegistry _commands;
    private readonly ITagRegistry _tags;
    private readonly IEngineeringAssetRegistry _assets;

    public CommandEngineeringHandler(
        ICommandEngineeringRegistry commands,
        ITagRegistry tags,
        IEngineeringAssetRegistry assets)
    {
        _commands = commands;
        _tags = tags;
        _assets = assets;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var commands = package.Commands ?? Array.Empty<CommandEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(commands.Select(x => x.Key));

        foreach (var command in commands)
        {
            var key = command.Key ?? string.Empty;
            var issues = Validate(command, package);
            if (duplicateKeys.Contains(key))
            {
                issues.Add(new ImportIssue(
                    "COMMAND_DUPLICATE_IN_FILE",
                    $"Command key '{key}' appears more than once in the import package.",
                    ImportEntityKind.Command,
                    key,
                    true));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Command,
                key,
                ResolveExisting(command) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var command in package.Commands ?? Array.Empty<CommandEngineeringDto>())
        {
            var existing = ResolveExisting(command);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            var target = ResolveTarget(command, package);
            var normalized = command with
            {
                Id = existing?.Id ?? command.Id ?? Guid.NewGuid(),
                TagId = target.Id,
                TagPath = target.Path
            };
            _commands.Upsert(normalized);
            if (existing is null) created++; else updated++;
        }
    }

    private List<ImportIssue> Validate(CommandEngineeringDto command, EngineeringPackage package)
    {
        var issues = new List<ImportIssue>();
        var key = command.Key ?? string.Empty;

        if (string.IsNullOrWhiteSpace(command.Key))
            issues.Add(Issue("COMMAND_KEY_REQUIRED", "Command key is required.", key));
        if (string.IsNullOrWhiteSpace(command.Name))
            issues.Add(Issue("COMMAND_NAME_REQUIRED", "Command name is required.", key));
        if (string.IsNullOrWhiteSpace(command.Value))
            issues.Add(Issue("COMMAND_VALUE_REQUIRED", "Command value is required.", key));
        if (!command.TagId.HasValue && string.IsNullOrWhiteSpace(command.TagPath))
            issues.Add(Issue("COMMAND_TAG_REQUIRED", "Command must reference a target TAG by id or path.", key));

        var resolved = TryResolveTarget(command, package, out var target, out var conflict);
        if (conflict)
        {
            issues.Add(Issue(
                "COMMAND_TAG_REFERENCE_CONFLICT",
                "Command TAG id and path resolve to different TAGs.",
                key));
        }
        else if (!resolved || target is null)
        {
            issues.Add(Issue(
                "COMMAND_TAG_NOT_FOUND",
                $"Target TAG '{command.TagPath ?? command.TagId?.ToString() ?? "<missing>"}' was not found.",
                key));
        }
        else
        {
            if (target.ReadOnly)
            {
                issues.Add(Issue(
                    "COMMAND_TAG_READ_ONLY",
                    $"Target TAG '{target.Path}' is read-only and cannot execute a command write.",
                    key));
            }

            if (!TagValueTextParser.TryParse(target.DataType, command.Value, out _))
            {
                issues.Add(Issue(
                    "COMMAND_VALUE_INVALID",
                    $"Value '{command.Value}' cannot be parsed as {target.DataType} for TAG '{target.Path}'.",
                    key));
            }
        }

        if (!string.IsNullOrWhiteSpace(command.EquipmentPath) &&
            _assets.FindEquipmentByPath(command.EquipmentPath) is null &&
            !(package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
                .Any(x => x.Path.Equals(command.EquipmentPath, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Issue(
                "COMMAND_EQUIPMENT_NOT_FOUND",
                $"Equipment '{command.EquipmentPath}' referenced by command '{key}' was not found.",
                key));
        }

        return issues;
    }

    private bool TryResolveTarget(
        CommandEngineeringDto command,
        EngineeringPackage package,
        out ResolvedTag? target,
        out bool conflict)
    {
        var byId = command.TagId.HasValue ? FindTagById(command.TagId.Value, package) : null;
        var byPath = !string.IsNullOrWhiteSpace(command.TagPath) ? FindTagByPath(command.TagPath, package) : null;

        conflict = byId is not null && byPath is not null && byId.Id != byPath.Id;
        target = conflict ? null : byId ?? byPath;
        return target is not null;
    }

    private ResolvedTag ResolveTarget(CommandEngineeringDto command, EngineeringPackage package)
    {
        if (!TryResolveTarget(command, package, out var target, out var conflict) || target is null || conflict)
            throw new InvalidOperationException($"Command '{command.Key}' target TAG could not be resolved after successful preview.");
        return target;
    }

    private ResolvedTag? FindTagById(Guid id, EngineeringPackage package)
    {
        if (_tags.TryGet(id, out var current) && current is not null)
            return new(current.Id, current.Path, current.DataType, current.ReadOnly);

        var dto = package.Tags.FirstOrDefault(x => x.Id == id);
        return dto is null ? null : new(dto.Id ?? id, dto.Path, dto.DataType, dto.ReadOnly);
    }

    private ResolvedTag? FindTagByPath(string path, EngineeringPackage package)
    {
        if (_tags.TryGetByPath(path, out var current) && current is not null)
            return new(current.Id, current.Path, current.DataType, current.ReadOnly);

        var dto = package.Tags.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        return dto is null ? null : new(dto.Id ?? Guid.Empty, dto.Path, dto.DataType, dto.ReadOnly);
    }

    private CommandEngineeringDto? ResolveExisting(CommandEngineeringDto command)
    {
        if (command.Id.HasValue)
        {
            var byId = _commands.Find(command.Id.Value);
            if (byId is not null) return byId;
        }

        return string.IsNullOrWhiteSpace(command.Key) ? null : _commands.FindByKey(command.Key);
    }

    private static ImportIssue Issue(string code, string message, string key) =>
        new(code, message, ImportEntityKind.Command, key, true);

    private sealed record ResolvedTag(Guid Id, string Path, TagDataType DataType, bool ReadOnly);
}
