using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Intelligence;

public interface IAssetProcessIntelligenceEngineeringRegistry
{
    AssetProcessIntelligenceEngineeringDto Snapshot();
    void Replace(AssetProcessIntelligenceEngineeringDto configuration);
    bool SetActive(AssetProcessIntelligenceScopeKind kind, Guid scopeId, bool active);
    void Clear();
}

public sealed class InMemoryAssetProcessIntelligenceEngineeringRegistry(
    Action? changed = null) : IAssetProcessIntelligenceEngineeringRegistry
{
    private readonly object _sync = new();
    private AssetProcessIntelligenceEngineeringDto _configuration = new();

    public AssetProcessIntelligenceEngineeringDto Snapshot()
    {
        lock (_sync)
            return _configuration with
            {
                Scopes = (_configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>()).ToArray()
            };
    }

    public void Replace(AssetProcessIntelligenceEngineeringDto configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null);
        if (issues.Count != 0)
            throw new InvalidDataException(string.Join("; ", issues.Select(issue => issue.Message)));

        lock (_sync)
            _configuration = configuration with
            {
                Scopes = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
                    .OrderBy(scope => scope.Kind)
                    .ThenBy(scope => scope.ScopeId)
                    .ToArray()
            };
        changed?.Invoke();
    }

    public bool SetActive(AssetProcessIntelligenceScopeKind kind, Guid scopeId, bool active)
    {
        if (!Enum.IsDefined(kind) || scopeId == Guid.Empty)
            return false;

        lock (_sync)
        {
            var scopes = (_configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>()).ToArray();
            var index = Array.FindIndex(scopes, scope => scope.Kind == kind && scope.ScopeId == scopeId);
            if (index < 0) return false;
            if (scopes[index].Active == active) return true;
            scopes[index] = scopes[index] with { Active = active };
            _configuration = _configuration with { Scopes = scopes };
        }

        changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_sync) _configuration = new AssetProcessIntelligenceEngineeringDto();
        changed?.Invoke();
    }
}

public static class AssetProcessIntelligenceEngineeringValidator
{
    public static IReadOnlyCollection<AssetProcessIntelligenceEngineeringIssue> Validate(
        AssetProcessIntelligenceEngineeringDto? configuration,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment,
        IReadOnlyCollection<LocationEngineeringDto>? locations)
    {
        var issues = new List<AssetProcessIntelligenceEngineeringIssue>();
        if (configuration is null) return issues;
        if (configuration.Version != 1)
        {
            issues.Add(new("INTELLIGENCE_SCOPE_VERSION_UNSUPPORTED", "Asset & Process Intelligence scope configuration version must be 1."));
            return issues;
        }

        var equipmentIds = equipment?.Where(item => item?.Id is { } id && id != Guid.Empty)
            .Select(item => item!.Id!.Value).ToHashSet();
        var locationIds = locations?.Where(item => item?.Id is { } id && id != Guid.Empty)
            .Select(item => item!.Id!.Value).ToHashSet();
        var seen = new HashSet<(AssetProcessIntelligenceScopeKind Kind, Guid Id)>();

        foreach (var scope in configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
        {
            if (scope is null || !Enum.IsDefined(scope.Kind) || scope.ScopeId == Guid.Empty)
            {
                issues.Add(new("INTELLIGENCE_SCOPE_IDENTITY_INVALID", "Every Intelligence scope requires a supported kind and a stable non-empty identity."));
                continue;
            }
            if (!seen.Add((scope.Kind, scope.ScopeId)))
                issues.Add(new("INTELLIGENCE_SCOPE_DUPLICATE", $"Intelligence scope '{scope.Kind}:{scope.ScopeId:D}' is listed more than once."));

            var exists = scope.Kind switch
            {
                AssetProcessIntelligenceScopeKind.Equipment => equipmentIds?.Contains(scope.ScopeId),
                AssetProcessIntelligenceScopeKind.Location => locationIds?.Contains(scope.ScopeId),
                _ => false
            };
            if (exists == false)
                issues.Add(new("INTELLIGENCE_SCOPE_RESOURCE_NOT_FOUND", $"Intelligence scope '{scope.Kind}:{scope.ScopeId:D}' does not resolve to a stable resource in the canonical Engineering project."));
        }

        return issues;
    }
}

public sealed record AssetProcessIntelligenceEngineeringIssue(string Code, string Message);
