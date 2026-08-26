using System.Collections.ObjectModel;

namespace Scada.Engineering.Scripts;

public enum ScriptEngineeringScope
{
    ClientVisual,
    Server
}

public enum ScriptEngineeringEventKind
{
    Initialize,
    Dispose,
    ObjectInteraction,
    TagChanged,
    ClientMemoryChanged,
    Timer,
    PropertyChanged,
    FrameTick,
    ServerRuntimeEvent
}

public enum ScriptEngineeringDependencyKind
{
    Script,
    VisualDefinition,
    VisualObject,
    Tag,
    ClientMemoryTag,
    ServerMemoryTag,
    Resource
}

public sealed record ScriptEngineeringEntryPoint(
    ScriptEngineeringEventKind EventKind,
    string HandlerName,
    string? TargetReference = null);

public sealed record ScriptEngineeringDependency(
    ScriptEngineeringDependencyKind Kind,
    string StableReference,
    bool Required = true);

public sealed class ScriptEngineeringDefinition
{
    public ScriptEngineeringDefinition(
        Guid id,
        string path,
        string name,
        ScriptEngineeringScope scope,
        string source,
        bool enabled = true,
        string language = "python",
        string languageVersion = "3",
        IReadOnlyCollection<ScriptEngineeringEntryPoint>? entryPoints = null,
        IReadOnlyCollection<ScriptEngineeringDependency>? dependencies = null,
        string? description = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = id;
        Path = path ?? string.Empty;
        Name = name ?? string.Empty;
        Scope = scope;
        Source = source ?? string.Empty;
        Enabled = enabled;
        Language = language ?? string.Empty;
        LanguageVersion = languageVersion ?? string.Empty;
        EntryPoints = Array.AsReadOnly((entryPoints ?? Array.Empty<ScriptEngineeringEntryPoint>()).ToArray());
        Dependencies = Array.AsReadOnly((dependencies ?? Array.Empty<ScriptEngineeringDependency>()).ToArray());
        Description = description;
        Metadata = new ReadOnlyDictionary<string, string>(
            (metadata ?? new Dictionary<string, string>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public Guid Id { get; }

    public string Path { get; }

    public string Name { get; }

    public ScriptEngineeringScope Scope { get; }

    public string Source { get; }

    public bool Enabled { get; }

    public string Language { get; }

    public string LanguageVersion { get; }

    public IReadOnlyCollection<ScriptEngineeringEntryPoint> EntryPoints { get; }

    public IReadOnlyCollection<ScriptEngineeringDependency> Dependencies { get; }

    public string? Description { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>
/// Isolated Engineering association between one visual definition/object event and a Script entry point.
/// Central Screen/Popup/Dynamo schema integration is intentionally deferred to coordinator-owned contracts.
/// </summary>
public sealed record ScriptVisualEventReference(
    Guid VisualDefinitionId,
    Guid? VisualObjectId,
    ScriptEngineeringEventKind EventKind,
    Guid ScriptId,
    string EntryPoint,
    string? TargetReference = null);

public sealed class ScriptEngineeringModel
{
    public ScriptEngineeringModel(
        IReadOnlyCollection<ScriptEngineeringDefinition>? scripts = null,
        IReadOnlyCollection<ScriptVisualEventReference>? visualEventReferences = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Scripts = Array.AsReadOnly((scripts ?? Array.Empty<ScriptEngineeringDefinition>()).ToArray());
        VisualEventReferences = Array.AsReadOnly((visualEventReferences ?? Array.Empty<ScriptVisualEventReference>()).ToArray());
        Metadata = new ReadOnlyDictionary<string, string>(
            (metadata ?? new Dictionary<string, string>())
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    public IReadOnlyCollection<ScriptEngineeringDefinition> Scripts { get; }

    public IReadOnlyCollection<ScriptVisualEventReference> VisualEventReferences { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public static class ScriptEngineeringReferenceKeys
{
    public static string Script(Guid scriptId) => scriptId.ToString("D");

    public static string VisualDefinition(Guid visualDefinitionId) => visualDefinitionId.ToString("D");

    public static string VisualObject(Guid visualDefinitionId, Guid visualObjectId) =>
        $"{visualDefinitionId:D}/{visualObjectId:D}";

    public static string Tag(Guid tagId) => tagId.ToString("D");

    public static string Resource(Guid resourceId) => resourceId.ToString("D");
}
