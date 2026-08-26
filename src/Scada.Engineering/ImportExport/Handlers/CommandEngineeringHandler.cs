using Scada.Core.Commands;
using Scada.Core.Tags;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class CommandEngineeringHandler
{
    private readonly ICommandEngineeringRegistry _registry;
    private readonly ITagRegistry _tags;

    public CommandEngineeringHandler(ICommandEngineeringRegistry registry, ITagRegistry tags)
    {
        _registry = registry;
        _tags = tags;
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var commands = package.Commands ?? Array.Empty<CommandEngineeringDto>();
        var duplicates = EngineeringHandlerSupport.Duplicates(commands.Select(x => x.Key));

        foreach (var command in commands)
        {
            var issues = CommandEngineeringValidator.Validate(command).ToList();
            if (duplicates.Contains(command.Key))
            {
                issues.Add(new ImportIssue(
                    "COMMAND_DUPLICATE_IN_FILE",
                    $"Command key '{command.Key}' appears more than once in the import package.",
                    ImportEntityKind.Command,
                    command.Key,
                    true));
            }

            var target = ResolveTarget(package, command, issues);
            if (target is not null && issues.All(x => x.Code != "COMMAND_TARGET_TAG_MISMATCH"))
            {
                var tag = new TagDefinition(
                    target.Id,
                    target.Name,
                    target.Path,
                    target.DataType,
                    target.Source,
                    target.EngineeringUnit,
                    target.Description,
                    target.ReadOnly,
                    target.Metadata,
                    target.AccessPolicy);
                var valueIssue = CommandEngineeringValidator.ValidateTargetValue(command, tag);
                if (valueIssue is not null) issues.Add(valueIssue);
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.Command,
                command.Key,
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

            _registry.Upsert(command with { Id = existing?.Id ?? command.Id ?? Guid.NewGuid() });
            if (existing is null) created++; else updated++;
        }
    }

    private CommandEngineeringDto? ResolveExisting(CommandEngineeringDto command)
    {
        if (command.Id.HasValue)
        {
            var byId = _registry.Find(command.Id.Value);
            if (byId is not null) return byId;
        }

        return _registry.FindByKey(command.Key);
    }

    private TagTarget? ResolveTarget(
        EngineeringPackage package,
        CommandEngineeringDto command,
        List<ImportIssue> issues)
    {
        var byId = command.TargetTagId.HasValue
            ? FindPackageTagById(package, command.TargetTagId.Value) ?? FindRegistryTagById(command.TargetTagId.Value)
            : null;
        var byPath = !string.IsNullOrWhiteSpace(command.TargetTagPath)
            ? FindPackageTagByPath(package, command.TargetTagPath) ?? FindRegistryTagByPath(command.TargetTagPath)
            : null;

        if (byId is not null && byPath is not null &&
            byId.Id != byPath.Id &&
            !byId.Path.Equals(byPath.Path, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ImportIssue(
                "COMMAND_TARGET_TAG_MISMATCH",
                $"Command '{command.Key}' TargetTagId and TargetTagPath resolve to different TAGs.",
                ImportEntityKind.Command,
                command.Key,
                true));
            return null;
        }

        var target = byId ?? byPath;
        if (target is null)
        {
            issues.Add(new ImportIssue(
                "COMMAND_TARGET_TAG_NOT_FOUND",
                $"Command '{command.Key}' references a target TAG that does not exist in the workspace or import package.",
                ImportEntityKind.Command,
                command.Key,
                true));
        }

        return target;
    }

    private static TagTarget? FindPackageTagById(EngineeringPackage package, Guid id)
    {
        var dto = package.Tags.FirstOrDefault(x => x.Id == id);
        return dto is null ? null : FromDto(dto);
    }

    private static TagTarget? FindPackageTagByPath(EngineeringPackage package, string path)
    {
        var dto = package.Tags.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        return dto is null ? null : FromDto(dto);
    }

    private TagTarget? FindRegistryTagById(Guid id) =>
        _tags.TryGet(id, out var tag) && tag is not null ? FromDefinition(tag) : null;

    private TagTarget? FindRegistryTagByPath(string path) =>
        _tags.TryGetByPath(path, out var tag) && tag is not null ? FromDefinition(tag) : null;

    private static TagTarget FromDto(TagEngineeringDto dto) => new(
        dto.Id ?? DeterministicTemporaryId(dto.Path),
        dto.Name,
        dto.Path,
        dto.DataType,
        dto.Source,
        dto.EngineeringUnit,
        dto.Description,
        dto.ReadOnly,
        dto.Metadata,
        dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(dto.AccessPolicy.ReadRoles, dto.AccessPolicy.WriteRoles, dto.AccessPolicy.ConfigureRoles));

    private static TagTarget FromDefinition(TagDefinition tag) => new(
        tag.Id,
        tag.Name,
        tag.Path,
        tag.DataType,
        tag.Source,
        tag.EngineeringUnit,
        tag.Description,
        tag.ReadOnly,
        tag.Metadata,
        tag.AccessPolicy);

    private static Guid DeterministicTemporaryId(string path)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToUpperInvariant()));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record TagTarget(
        Guid Id,
        string Name,
        string Path,
        TagDataType DataType,
        string? Source,
        string? EngineeringUnit,
        string? Description,
        bool ReadOnly,
        IReadOnlyDictionary<string, string>? Metadata,
        TagAccessPolicy? AccessPolicy);
}
