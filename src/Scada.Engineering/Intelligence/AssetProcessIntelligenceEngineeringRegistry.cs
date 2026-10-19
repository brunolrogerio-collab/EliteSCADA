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
                Scopes = (_configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>()).ToArray(),
                Profiles = (_configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
                    .Select(profile => profile with
                    {
                        Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).ToArray()
                    }).ToArray(),
                Assignments = (_configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>()).ToArray(),
                ProcessStages = (_configuration.ProcessStages ?? Array.Empty<AssetProcessIntelligenceProcessStageEngineeringDto>())
                    .Select(stage => stage with
                    {
                        Links = (stage.Links ?? Array.Empty<AssetProcessIntelligenceProcessStageLinkEngineeringDto>()).ToArray()
                    }).ToArray(),
                OperatingContextProfiles = (_configuration.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>())
                    .Select(profile => profile with
                    {
                        Predicates = (profile.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>()).ToArray()
                    }).ToArray()
            };
    }

    public void Replace(AssetProcessIntelligenceEngineeringDto configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null, null);
        if (issues.Count != 0)
            throw new InvalidDataException(string.Join("; ", issues.Select(issue => issue.Message)));

        lock (_sync)
            _configuration = configuration with
            {
                Scopes = (configuration.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
                    .OrderBy(scope => scope.Kind)
                    .ThenBy(scope => scope.ScopeId)
                    .ToArray(),
                Profiles = (configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
                    .Select(profile => profile with
                    {
                        Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                            .OrderBy(signal => signal.Key, StringComparer.Ordinal)
                            .ThenBy(signal => signal.Id)
                            .ToArray()
                    })
                    .OrderBy(profile => profile.Key, StringComparer.Ordinal)
                    .ThenBy(profile => profile.Id)
                    .ToArray(),
                Assignments = (configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>())
                    .OrderBy(assignment => assignment.EquipmentId)
                    .ThenBy(assignment => assignment.ProfileId)
                    .ThenBy(assignment => assignment.Id)
                    .ToArray(),
                ProcessStages = (configuration.ProcessStages ?? Array.Empty<AssetProcessIntelligenceProcessStageEngineeringDto>())
                    .Select(stage => stage with
                    {
                        Links = (stage.Links ?? Array.Empty<AssetProcessIntelligenceProcessStageLinkEngineeringDto>())
                            .OrderBy(link => link.Order)
                            .ThenBy(link => link.AssignmentId)
                            .ToArray()
                    })
                    .OrderBy(stage => stage.Order)
                    .ThenBy(stage => stage.Key, StringComparer.Ordinal)
                    .ThenBy(stage => stage.Id)
                    .ToArray(),
                OperatingContextProfiles = (configuration.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>())
                    .Select(profile => profile with
                    {
                        Predicates = (profile.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>())
                            .OrderBy(predicate => predicate.TagId)
                            .ToArray()
                    })
                    .OrderBy(profile => profile.Key, StringComparer.Ordinal)
                    .ThenBy(profile => profile.Id)
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
    private static readonly System.Text.RegularExpressions.Regex StableKey = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IReadOnlyCollection<AssetProcessIntelligenceEngineeringIssue> Validate(
        AssetProcessIntelligenceEngineeringDto? configuration,
        IReadOnlyCollection<EquipmentEngineeringDto>? equipment,
        IReadOnlyCollection<LocationEngineeringDto>? locations,
        IReadOnlyCollection<TagEngineeringDto>? tags = null)
    {
        var issues = new List<AssetProcessIntelligenceEngineeringIssue>();
        if (configuration is null) return issues;
        if (configuration.Version is not (1 or 2 or 3))
        {
            issues.Add(new("INTELLIGENCE_CONFIGURATION_VERSION_UNSUPPORTED", "Asset & Process Intelligence configuration version must be 1, 2 or 3."));
            return issues;
        }

        if (configuration.Version == 1 &&
            ((configuration.Profiles?.Count ?? 0) != 0 || (configuration.Assignments?.Count ?? 0) != 0 || (configuration.ProcessStages?.Count ?? 0) != 0 || (configuration.OperatingContextProfiles?.Count ?? 0) != 0))
            issues.Add(new("INTELLIGENCE_V1_EXTENSIONS_NOT_SUPPORTED", "Version 1 Intelligence configuration may contain scopes only.", AssetProcessIntelligenceIssueEntity.Configuration));
        if (configuration.Version == 2 && (configuration.OperatingContextProfiles?.Count ?? 0) != 0)
            issues.Add(new("INTELLIGENCE_V2_CONTEXT_PROFILES_NOT_SUPPORTED", "Version 2 Intelligence configuration cannot contain operating-context profiles.", AssetProcessIntelligenceIssueEntity.Configuration));

        var equipmentIds = UniqueResourceIds(equipment?.Where(item => item is not null).Select(item => item!.Id), "EQUIPMENT", AssetProcessIntelligenceIssueEntity.Assignment, issues);
        var locationIds = UniqueResourceIds(locations?.Where(item => item is not null).Select(item => item!.Id), "LOCATION", AssetProcessIntelligenceIssueEntity.Scope, issues);
        var tagIds = UniqueResourceIds(tags?.Where(item => item is not null).Select(item => item!.Id), "TAG", AssetProcessIntelligenceIssueEntity.Profile, issues);
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
                issues.Add(new("INTELLIGENCE_SCOPE_RESOURCE_NOT_FOUND", $"Intelligence scope '{scope.Kind}:{scope.ScopeId:D}' does not resolve to a stable resource in the canonical Engineering project.", AssetProcessIntelligenceIssueEntity.Scope, scope.ScopeId));
        }

        if (configuration.Version == 1) return issues;

        var profiles = configuration.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>();
        var assignments = configuration.Assignments ?? Array.Empty<AssetProcessIntelligenceAssignmentEngineeringDto>();
        var stages = configuration.ProcessStages ?? Array.Empty<AssetProcessIntelligenceProcessStageEngineeringDto>();
        ValidateIdentitySet(profiles, profile => profile.Id, profile => profile.Key, "PROFILE", AssetProcessIntelligenceIssueEntity.Profile, issues);
        ValidateIdentitySet(assignments, assignment => assignment.Id, _ => null, "ASSIGNMENT", AssetProcessIntelligenceIssueEntity.Assignment, issues);
        ValidateIdentitySet(stages, stage => stage.Id, stage => stage.Key, "PROCESS_STAGE", AssetProcessIntelligenceIssueEntity.ProcessStage, issues);

        var profileById = profiles.Where(profile => profile is not null && profile.Id != Guid.Empty).GroupBy(profile => profile.Id).ToDictionary(group => group.Key, group => group.ToArray());
        var assignmentById = assignments.Where(assignment => assignment is not null && assignment.Id != Guid.Empty).GroupBy(assignment => assignment.Id).ToDictionary(group => group.Key, group => group.ToArray());
        var profileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            if (profile is null) continue;
            ValidateStableKey(profile.Key, "PROFILE", AssetProcessIntelligenceIssueEntity.Profile, profile.Id, issues);
            if (!string.IsNullOrWhiteSpace(profile.Key) && !profileKeys.Add(profile.Key.Trim()))
                issues.Add(new("INTELLIGENCE_PROFILE_KEY_DUPLICATE", $"Profile key '{profile.Key}' is duplicated.", AssetProcessIntelligenceIssueEntity.Profile, profile.Id));
            if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Trim().Length > 128)
                issues.Add(new("INTELLIGENCE_PROFILE_NAME_INVALID", "A profile requires a display name of 1 to 128 characters.", AssetProcessIntelligenceIssueEntity.Profile, profile.Id));

            var seenSignalIds = new HashSet<Guid>();
            var seenSignalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var signal in profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
            {
                if (signal is null || signal.Id == Guid.Empty)
                {
                    issues.Add(new("INTELLIGENCE_SIGNAL_IDENTITY_INVALID", "Every profile signal requires a stable non-empty identity.", AssetProcessIntelligenceIssueEntity.Profile, profile.Id));
                    continue;
                }
                if (!seenSignalIds.Add(signal.Id))
                    issues.Add(new("INTELLIGENCE_SIGNAL_ID_DUPLICATE", $"Signal identity '{signal.Id:D}' is duplicated in profile '{profile.Key}'.", AssetProcessIntelligenceIssueEntity.Profile, signal.Id));
                ValidateStableKey(signal.Key, "SIGNAL", AssetProcessIntelligenceIssueEntity.Profile, signal.Id, issues);
                if (!string.IsNullOrWhiteSpace(signal.Key) && !seenSignalKeys.Add(signal.Key.Trim()))
                    issues.Add(new("INTELLIGENCE_SIGNAL_KEY_DUPLICATE", $"Signal key '{signal.Key}' is duplicated in profile '{profile.Key}'.", AssetProcessIntelligenceIssueEntity.Profile, signal.Id));
                if (string.IsNullOrWhiteSpace(signal.SignalRole) || signal.SignalRole.Trim().Length > 64)
                    issues.Add(new("INTELLIGENCE_SIGNAL_ROLE_INVALID", "A signal requires domain-role metadata of 1 to 64 characters.", AssetProcessIntelligenceIssueEntity.Profile, signal.Id));
                if (signal.TagId == Guid.Empty)
                    issues.Add(new("INTELLIGENCE_SIGNAL_TAG_ID_INVALID", $"Signal '{signal.Key}' requires a stable TAG identity.", AssetProcessIntelligenceIssueEntity.Profile, signal.Id));
                else if (tagIds?.Contains(signal.TagId) == false)
                    issues.Add(new("INTELLIGENCE_SIGNAL_TAG_NOT_FOUND", $"Signal '{signal.Key}' references TAG '{signal.TagId:D}', which is missing or ambiguous in the canonical Engineering package.", AssetProcessIntelligenceIssueEntity.Profile, signal.Id));

                if (signal.SemanticBinding is not null)
                {
                    issues.AddRange(AssetProcessIntelligenceSemanticCatalogResolver.ValidateBinding(signal.SemanticBinding, signal.Id));
                    var tag = tags?.FirstOrDefault(item => item.Id == signal.TagId);
                    if (tag is not null)
                        ValidateSemanticTagBinding(signal.SemanticBinding, tag, signal.Id, issues);
                }
            }
        }

        var assignmentPairs = new HashSet<(Guid EquipmentId, Guid ProfileId)>();
        foreach (var assignment in assignments)
        {
            if (assignment is null) continue;
            if (assignment.EquipmentId == Guid.Empty || equipmentIds?.Contains(assignment.EquipmentId) == false)
                issues.Add(new("INTELLIGENCE_ASSIGNMENT_EQUIPMENT_NOT_FOUND", $"Assignment '{assignment.Id:D}' references Equipment '{assignment.EquipmentId:D}', which is missing or ambiguous.", AssetProcessIntelligenceIssueEntity.Assignment, assignment.Id));
            if (assignment.ProfileId == Guid.Empty || !profileById.ContainsKey(assignment.ProfileId))
                issues.Add(new("INTELLIGENCE_ASSIGNMENT_PROFILE_NOT_FOUND", $"Assignment '{assignment.Id:D}' references missing or ambiguous profile '{assignment.ProfileId:D}'.", AssetProcessIntelligenceIssueEntity.Assignment, assignment.Id));
            if (!assignmentPairs.Add((assignment.EquipmentId, assignment.ProfileId)))
                issues.Add(new("INTELLIGENCE_ASSIGNMENT_DUPLICATE", $"Equipment/profile assignment '{assignment.EquipmentId:D}:{assignment.ProfileId:D}' is duplicated.", AssetProcessIntelligenceIssueEntity.Assignment, assignment.Id));
        }

        var stageOrders = new HashSet<int>();
        var stageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in stages)
        {
            if (stage is null) continue;
            ValidateStableKey(stage.Key, "PROCESS_STAGE", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id, issues);
            if (!string.IsNullOrWhiteSpace(stage.Key) && !stageKeys.Add(stage.Key.Trim()))
                issues.Add(new("INTELLIGENCE_PROCESS_STAGE_KEY_DUPLICATE", $"Process stage key '{stage.Key}' is duplicated.", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id));
            if (string.IsNullOrWhiteSpace(stage.Name) || stage.Name.Trim().Length > 128)
                issues.Add(new("INTELLIGENCE_PROCESS_STAGE_NAME_INVALID", "A process stage requires a display name of 1 to 128 characters.", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id));
            if (stage.Order < 0 || !stageOrders.Add(stage.Order))
                issues.Add(new("INTELLIGENCE_PROCESS_STAGE_ORDER_INVALID", $"Process stage order '{stage.Order}' must be non-negative and unique.", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id));

            var linkOrders = new HashSet<int>();
            var linkedAssignments = new HashSet<Guid>();
            foreach (var link in stage.Links ?? Array.Empty<AssetProcessIntelligenceProcessStageLinkEngineeringDto>())
            {
                if (link is null || link.AssignmentId == Guid.Empty || !assignmentById.ContainsKey(link.AssignmentId))
                    issues.Add(new("INTELLIGENCE_PROCESS_STAGE_ASSIGNMENT_NOT_FOUND", $"Process stage '{stage.Key}' references missing or ambiguous assignment.", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id));
                else if (!linkedAssignments.Add(link.AssignmentId))
                    issues.Add(new("INTELLIGENCE_PROCESS_STAGE_ASSIGNMENT_DUPLICATE", $"Assignment '{link.AssignmentId:D}' appears more than once in process stage '{stage.Key}'.", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id));
                if (link is not null && (link.Order < 0 || !linkOrders.Add(link.Order)))
                    issues.Add(new("INTELLIGENCE_PROCESS_STAGE_LINK_ORDER_INVALID", $"Link order in process stage '{stage.Key}' must be non-negative and unique.", AssetProcessIntelligenceIssueEntity.ProcessStage, stage.Id));
            }
        }

        if (configuration.Version == 3)
        {
            var contextProfiles = configuration.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>();
            ValidateIdentitySet(contextProfiles, profile => profile.Id, profile => profile.Key, "OPERATING_CONTEXT_PROFILE", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, issues);
            var contextKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var context in contextProfiles)
            {
                if (context is null) continue;
                ValidateStableKey(context.Key, "OPERATING_CONTEXT_PROFILE", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id, issues);
                if (!string.IsNullOrWhiteSpace(context.Key) && !contextKeys.Add(context.Key.Trim()))
                    issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_PROFILE_KEY_DUPLICATE", $"Operating-context profile key '{context.Key}' is duplicated.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                if (string.IsNullOrWhiteSpace(context.DisplayLabel) || context.DisplayLabel.Trim().Length > 128)
                    issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_PROFILE_LABEL_INVALID", "An operating-context profile requires a display label of 1 to 128 characters.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                if (context.Version < 1)
                    issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_PROFILE_VERSION_INVALID", "An operating-context profile version must be positive.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                if (!assignmentById.TryGetValue(context.AssignmentId, out var matchingAssignments) || matchingAssignments.Length != 1)
                {
                    issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_ASSIGNMENT_NOT_FOUND", "An operating-context profile must reference one existing Equipment assignment.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                    continue;
                }
                var assignment = matchingAssignments[0];
                if (!profileById.TryGetValue(assignment.ProfileId, out var assignedProfiles) || assignedProfiles.Length != 1)
                {
                    issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_MEASUREMENT_PROFILE_NOT_FOUND", "The operating-context assignment has no unique measurement profile.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                    continue;
                }
                var signalTagIds = (assignedProfiles[0].Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                    .Where(signal => signal is not null)
                    .Select(signal => signal.TagId)
                    .ToHashSet();
                var predicates = context.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>();
                if (predicates.Count is < 1 or > 16)
                    issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_COUNT_INVALID", "An operating-context profile requires 1 to 16 predicates.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                var predicateTagIds = new HashSet<Guid>();
                foreach (var predicate in predicates)
                {
                    if (predicate is null || predicate.TagId == Guid.Empty)
                    {
                        issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TAG_INVALID", "Every context predicate requires a stable TAG identity.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                        continue;
                    }
                    if (!predicateTagIds.Add(predicate.TagId))
                        issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TAG_DUPLICATE", "An operating-context profile may declare only one predicate per TAG.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                    if (!signalTagIds.Contains(predicate.TagId))
                        issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_TAG_NOT_IN_ASSIGNED_PROFILE", $"Context predicate TAG '{predicate.TagId:D}' is not part of the assigned measurement profile.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                    var tag = tags?.FirstOrDefault(item => item.Id == predicate.TagId);
                    if (tagIds?.Contains(predicate.TagId) == false)
                        issues.Add(new("INTELLIGENCE_OPERATING_CONTEXT_TAG_NOT_FOUND", $"Context predicate TAG '{predicate.TagId:D}' is missing or ambiguous.", AssetProcessIntelligenceIssueEntity.OperatingContextProfile, context.Id));
                    if (tag is not null)
                        ValidateOperatingContextPredicate(predicate, tag, context.Id, issues);
                }
            }
        }

        return issues;
    }

    private static void ValidateOperatingContextPredicate(
        AssetProcessIntelligenceOperatingContextPredicateEngineeringDto predicate,
        TagEngineeringDto tag,
        Guid profileId,
        List<AssetProcessIntelligenceEngineeringIssue> issues)
    {
        void Invalid(string code, string message) => issues.Add(new(code, message, AssetProcessIntelligenceIssueEntity.OperatingContextProfile, profileId));
        switch (predicate.Kind)
        {
            case AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval:
                if (tag.DataType is not (Scada.Core.Tags.TagDataType.Int16 or Scada.Core.Tags.TagDataType.Int32 or Scada.Core.Tags.TagDataType.Int64 or Scada.Core.Tags.TagDataType.Float or Scada.Core.Tags.TagDataType.Double))
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TYPE_MISMATCH", "Numeric intervals require a numeric TAG.");
                if ((!predicate.Minimum.HasValue && !predicate.Maximum.HasValue) ||
                    (predicate.Minimum.HasValue && !double.IsFinite(predicate.Minimum.Value)) ||
                    (predicate.Maximum.HasValue && !double.IsFinite(predicate.Maximum.Value)) ||
                    (predicate.Minimum.HasValue && predicate.Maximum.HasValue && predicate.Minimum.Value > predicate.Maximum.Value) ||
                    predicate.BooleanValue.HasValue || predicate.ExactValue is not null)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_NUMERIC_BOUNDS_INVALID", "A numeric interval requires at least one finite bound, ordered inclusively, and no non-numeric value.");
                break;
            case AssetProcessIntelligenceOperatingContextPredicateKind.BooleanEquals:
                if (tag.DataType != Scada.Core.Tags.TagDataType.Boolean)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TYPE_MISMATCH", "Boolean equality requires a Boolean TAG.");
                if (!predicate.BooleanValue.HasValue || predicate.Minimum.HasValue || predicate.Maximum.HasValue || predicate.ExactValue is not null)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_BOOLEAN_VALUE_INVALID", "Boolean equality requires exactly one Boolean value.");
                break;
            case AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals:
                if (tag.DataType != Scada.Core.Tags.TagDataType.String)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TYPE_MISMATCH", "String equality requires a String TAG.");
                if (predicate.ExactValue is null || predicate.BooleanValue.HasValue || predicate.Minimum.HasValue || predicate.Maximum.HasValue)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_EXACT_VALUE_INVALID", "String equality requires exactly one ordinal value.");
                break;
            case AssetProcessIntelligenceOperatingContextPredicateKind.EnumEquals:
                if (tag.DataType != Scada.Core.Tags.TagDataType.Enum)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TYPE_MISMATCH", "Enum equality requires an Enum TAG.");
                if (predicate.ExactValue is null || predicate.BooleanValue.HasValue || predicate.Minimum.HasValue || predicate.Maximum.HasValue)
                    Invalid("INTELLIGENCE_OPERATING_CONTEXT_EXACT_VALUE_INVALID", "Enum equality requires exactly one ordinal value.");
                break;
            default:
                Invalid("INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_UNSUPPORTED", "The operating-context predicate kind is unsupported.");
                break;
        }
    }

    private static void ValidateSemanticTagBinding(
        AssetProcessIntelligenceSignalSemanticBindingEngineeringDto binding,
        TagEngineeringDto tag,
        Guid signalId,
        List<AssetProcessIntelligenceEngineeringIssue> issues)
    {
        if (tag.DataType is not (Scada.Core.Tags.TagDataType.Int16 or Scada.Core.Tags.TagDataType.Int32 or
            Scada.Core.Tags.TagDataType.Int64 or Scada.Core.Tags.TagDataType.Float or Scada.Core.Tags.TagDataType.Double))
        {
            issues.Add(new("INTELLIGENCE_SEMANTIC_TAG_TYPE_UNSUPPORTED", "A semantic process signal must reference a numeric TAG.", AssetProcessIntelligenceIssueEntity.Profile, signalId));
            return;
        }

        var unitIssues = ControlledEngineeringSemanticsResolver.ValidateUnitReference(
            tag.ControlledEngineeringUnit,
            AssetProcessIntelligenceIssueEntity.Profile,
            signalId);
        issues.AddRange(unitIssues);
        if (!ControlledEngineeringSemanticsResolver.TryResolveUnit(tag.ControlledEngineeringUnit, out var unit))
            return;
        if (binding.CatalogVersion == AssetProcessIntelligenceSemanticCatalog.CurrentCatalogVersion &&
            tag.ControlledEngineeringUnit?.CatalogVersion != ControlledEngineeringUnitCatalog.CurrentCatalogVersion)
        {
            issues.Add(new(
                "INTELLIGENCE_SEMANTIC_UNIT_CATALOG_VERSION_MISMATCH",
                "Version 1 semantic roles require a version 1 controlled unit reference.",
                AssetProcessIntelligenceIssueEntity.Profile,
                signalId));
            return;
        }
        var roleDimension = AssetProcessIntelligenceSemanticCatalogResolver.DimensionForRole(binding.CatalogVersion, binding.RoleCode);
        if (roleDimension is not null && !string.Equals(roleDimension, unit.Dimension, StringComparison.Ordinal))
            issues.Add(new(
                "INTELLIGENCE_SEMANTIC_DIMENSION_MISMATCH",
                $"Signal role '{binding.RoleCode}' expects dimension '{roleDimension}', but TAG '{tag.Path}' declares '{unit.Dimension}'.",
                AssetProcessIntelligenceIssueEntity.Profile,
                signalId));
    }

    private static HashSet<Guid>? UniqueResourceIds(
        IEnumerable<Guid?>? values,
        string label,
        AssetProcessIntelligenceIssueEntity entity,
        List<AssetProcessIntelligenceEngineeringIssue> issues)
    {
        if (values is null) return null;
        var result = new HashSet<Guid>();
        foreach (var id in values)
        {
            if (!id.HasValue || id.Value == Guid.Empty) continue;
            if (!result.Add(id.Value))
                issues.Add(new($"INTELLIGENCE_{label}_REFERENCE_AMBIGUOUS", $"Stable {label} identity '{id.Value:D}' resolves to more than one canonical resource.", entity, id.Value));
        }
        return result;
    }

    private static void ValidateIdentitySet<T>(
        IReadOnlyCollection<T> items,
        Func<T, Guid> id,
        Func<T, string?>? key,
        string label,
        AssetProcessIntelligenceIssueEntity entity,
        List<AssetProcessIntelligenceEngineeringIssue> issues)
    {
        var seenIds = new HashSet<Guid>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item is null) continue;
            var identity = id(item);
            if (identity == Guid.Empty)
                issues.Add(new($"INTELLIGENCE_{label}_ID_INVALID", $"Every {label} requires a stable non-empty identity.", entity));
            else if (!seenIds.Add(identity))
                issues.Add(new($"INTELLIGENCE_{label}_ID_DUPLICATE", $"{label} identity '{identity:D}' is duplicated.", entity, identity));
            if (key is null) continue;
            var itemKey = key(item);
            if (!string.IsNullOrWhiteSpace(itemKey) && !seenKeys.Add(itemKey.Trim()))
                issues.Add(new($"INTELLIGENCE_{label}_KEY_DUPLICATE", $"{label} key '{itemKey}' is duplicated.", entity, identity == Guid.Empty ? null : identity));
        }
    }

    private static void ValidateStableKey(
        string? key,
        string label,
        AssetProcessIntelligenceIssueEntity entity,
        Guid resourceId,
        List<AssetProcessIntelligenceEngineeringIssue> issues)
    {
        if (key is null || !StableKey.IsMatch(key) || key != key.Trim())
            issues.Add(new($"INTELLIGENCE_{label}_KEY_INVALID", $"{label} key must be 1 to 64 ASCII letters, digits, dots, underscores or hyphens, start with a letter or digit, and contain no surrounding whitespace.", entity, resourceId == Guid.Empty ? null : resourceId));
    }
}
