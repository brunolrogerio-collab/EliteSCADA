using Scada.Engineering.Contracts;
using Scada.Engineering.Libraries;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;

namespace Scada.Engineering.ImportExport;

internal static class EngineeringFragmentPackageBuilder
{
    public static EngineeringPackage Build(
        EngineeringPackage source,
        IReadOnlyCollection<EngineeringFragmentEntityReference> closure)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(closure);

        var identities = closure
            .Select(EngineeringFragmentReferenceComparer.Identity)
            .ToHashSet();

        bool Included(ImportEntityKind kind, Guid? id) =>
            id is { } value && value != Guid.Empty && identities.Contains((kind, value));

        var scripts = (source.Scripts ?? Array.Empty<ScriptEngineeringDefinition>())
            .Where(script => Included(ImportEntityKind.Script, script.Id))
            .Select(script => ReusableScriptDependencyAnalyzer.WithMetadata(
                script,
                ReusableLibraryProvenance.WithoutOrigin(script.Metadata)))
            .ToArray();
        var scriptIds = scripts.Select(script => script.Id).ToHashSet();

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            source.Tags
                .Where(tag => Included(ImportEntityKind.Tag, tag.Id))
                .ToArray(),
            source.Alarms
                .Where(alarm => Included(ImportEntityKind.Alarm, alarm.Id))
                .ToArray(),
            DataSources: (source.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
                .Where(item => Included(ImportEntityKind.DataSource, item.Id))
                .Select(SanitizeDataSource)
                .ToArray(),
            Templates: (source.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Template, item.Id))
                .Select(item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) })
                .ToArray(),
            Equipment: (source.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Equipment, item.Id))
                .ToArray(),
            Dynamos: (source.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Dynamo, item.Id))
                .Select(item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) })
                .ToArray(),
            Screens: (source.Screens ?? Array.Empty<ScreenEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Screen, item.Id))
                .Select(item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) })
                .ToArray(),
            Popups: (source.Popups ?? Array.Empty<PopupEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Popup, item.Id))
                .Select(item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) })
                .ToArray(),
            SecurityRoles: Array.Empty<SecurityRoleEngineeringDto>(),
            Commands: (source.Commands ?? Array.Empty<CommandEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Command, item.Id))
                .ToArray(),
            Gateways: (source.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Gateway, item.Id))
                .ToArray(),
            Scripts: scripts,
            ScriptVisualEventReferences: (source.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
                .Where(reference => scriptIds.Contains(reference.ScriptId))
                .ToArray(),
            VisualAssets: (source.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
                .Where(item => Included(ImportEntityKind.VisualAsset, item.Id))
                .Select(item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) })
                .ToArray(),
            Reports: (source.Reports ?? Array.Empty<ReportEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Report, item.Id))
                .ToArray(),
            OperationalEvents: (source.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>())
                .Where(item => Included(ImportEntityKind.OperationalEvent, item.Id))
                .ToArray(),
            StartupScreenId: null,
            EngineeringLock: null,
            SecurityScopes: Array.Empty<SecurityScopeEngineeringDto>(),
            AuthorityPolicyReference: null,
            HistorianCaptureProfiles: (source.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
                .Where(item => Included(ImportEntityKind.HistorianCaptureProfile, item.Id))
                .ToArray(),
            DataQueries: (source.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
                .Where(item => Included(ImportEntityKind.DataQuery, item.Id))
                .ToArray(),
            AlarmViews: (source.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
                .Where(item => Included(ImportEntityKind.AlarmView, item.Id))
                .ToArray());
    }

    private static DataSourceEngineeringDto SanitizeDataSource(DataSourceEngineeringDto source) =>
        source with
        {
            Settings = SanitizeSecretLikeDictionary(source.Settings),
            Metadata = SanitizeSecretLikeDictionary(source.Metadata),
            SecretReferences = source.SecretReferences is null
                ? null
                : new Dictionary<string, string>(source.SecretReferences, StringComparer.Ordinal)
        };

    private static Dictionary<string, string>? SanitizeSecretLikeDictionary(
        IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
            return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (!LooksSecretLike(pair.Key, pair.Value))
                result[pair.Key] = pair.Value;
        }

        return result.Count == 0 ? null : result;
    }

    private static bool LooksSecretLike(string key, string value)
    {
        var normalized = new string((key ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        if (normalized.Contains("password", StringComparison.Ordinal) ||
            normalized.Contains("passwd", StringComparison.Ordinal) ||
            normalized.Contains("pwd", StringComparison.Ordinal) ||
            normalized.Contains("secret", StringComparison.Ordinal) ||
            normalized.Contains("token", StringComparison.Ordinal) ||
            normalized.Contains("credential", StringComparison.Ordinal) ||
            normalized.Contains("apikey", StringComparison.Ordinal) ||
            normalized.Contains("privatekey", StringComparison.Ordinal) ||
            normalized.Contains("connectionstring", StringComparison.Ordinal))
            return true;

        var candidate = value ?? string.Empty;
        return candidate.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("passwd=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("pwd=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("token=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("apikey=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("api_key=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("clientsecret=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("client_secret=", StringComparison.OrdinalIgnoreCase);
    }
}
