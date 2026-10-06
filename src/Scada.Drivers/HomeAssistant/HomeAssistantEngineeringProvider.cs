using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.HomeAssistant;

public sealed class HomeAssistantDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        HomeAssistantContract.DriverType,
        "Home Assistant local bridge",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.Discover,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            HomeAssistantContract.SchemaId,
            HomeAssistantContract.SchemaVersion,
            DataSourceFields:
            [
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Home Assistant host"),
                new("port", DriverConfigurationValueKind.Port, DefaultValue: "8123", Minimum: 1, Maximum: 65535),
                new("tls", DriverConfigurationValueKind.Boolean, DefaultValue: "false"),
                new("basePath", DriverConfigurationValueKind.String, Advanced: true),
                new("accessToken", DriverConfigurationValueKind.SecretReference, Required: true, DisplayName: "Access token secret reference"),
                new("connectTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "10000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "10000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("reconnectMinimumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "1000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("reconnectMaximumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "30000", Minimum: 100, Maximum: 3600000, Advanced: true)
            ],
            TagBindingFields:
            [
                new("address", DriverConfigurationValueKind.String, Required: true, DisplayName: "Selected Home Assistant entity binding")
            ]),
        Description: "Direct local/reachable Home Assistant WebSocket bridge for explicitly selected entities. No whole-instance auto import.",
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential, IntegrationDomain.IoT],
        ConnectionModel: DriverConnectionModel.DirectNetwork,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn)]);
}

public sealed class HomeAssistantEngineeringProvider :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverDiscoverySource
{
    public const string SelectedEntityIdsParameter = "selectedEntityIds";
    public const string AccessTokenReferenceKey = "accessToken";

    private readonly string _projectKey;
    private readonly ICommunicationDriverProtectedMaterialResolver _protectedMaterialResolver;
    private readonly Func<HomeAssistantConnectionSettings, IHomeAssistantClient> _clientFactory;

    public HomeAssistantEngineeringProvider(
        string projectKey,
        ICommunicationDriverProtectedMaterialResolver protectedMaterialResolver,
        Func<HomeAssistantConnectionSettings, IHomeAssistantClient>? clientFactory = null)
    {
        _projectKey = string.IsNullOrWhiteSpace(projectKey) ? "engineering-draft" : projectKey.Trim();
        _protectedMaterialResolver = protectedMaterialResolver
            ?? throw new ArgumentNullException(nameof(protectedMaterialResolver));
        _clientFactory = clientFactory ?? (settings => new HomeAssistantWebSocketClient(settings));
    }

    public CommunicationDriverTypeDescriptor Descriptor => new HomeAssistantDriverDescriptorProvider().Descriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var settings = ParseConnection(context.Settings);
            await using var client = _clientFactory(settings);
            await AuthenticateAsync(client, context, cancellationToken).ConfigureAwait(false);
            var states = await client.GetStatesAsync(cancellationToken).ConfigureAwait(false);
            return new DriverConnectionTestResult(
                true,
                SanitizeEndpoint(settings),
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["haVersion"] = client.HomeAssistantVersion ?? string.Empty,
                    ["stateCount"] = states.Count.ToString(CultureInfo.InvariantCulture)
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DriverConnectionTestResult(
                false,
                null,
                null,
                Issues:
                [
                    new DriverEngineeringIssue(
                        "HA_CONNECTION_TEST_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        SafeFailure(ex))
                ]);
        }
    }

    public async IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Context is null)
        {
            yield return Failure(
                "HA_DISCOVERY_CONTEXT_REQUIRED",
                "Home Assistant discovery requires an Engineering Data Source context.");
            yield break;
        }

        HomeAssistantConnectionSettings settings;
        try
        {
            settings = ParseConnection(request.Context.Settings);
        }
        catch (Exception ex)
        {
            yield return Failure("HA_DISCOVERY_CONFIGURATION", SafeFailure(ex));
            yield break;
        }

        var selectedIds = ParseSelectedEntityIds(request.Parameters);
        if (request.MaximumResults is > 0)
            selectedIds = selectedIds.Take(request.MaximumResults.Value).ToArray();

        await using var client = _clientFactory(settings);
        try
        {
            await AuthenticateAsync(client, request.Context, cancellationToken).ConfigureAwait(false);
            var states = await client.GetStatesAsync(cancellationToken).ConfigureAwait(false);
            var registry = await client.GetEntityRegistryForDisplayAsync(cancellationToken).ConfigureAwait(false);
            var inventory = HomeAssistantSelectedInventoryBuilder.Build(states, registry, selectedIds);
            var materialized = HomeAssistantEntityMapper.Build(request.Context.DataSourceKey, inventory);

            if (materialized.Candidates.Count == 0)
            {
                yield return new DriverDiscoveryCandidate(
                    "ha-selection-empty",
                    "unresolved",
                    "Home Assistant selected entities",
                    SanitizeEndpoint(settings),
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["haVersion"] = client.HomeAssistantVersion ?? string.Empty,
                        ["selectedEntityCount"] = selectedIds.Count.ToString(CultureInfo.InvariantCulture)
                    },
                    Issues: materialized.Issues);
                yield break;
            }

            foreach (var candidate in materialized.Candidates)
            {
                yield return new DriverDiscoveryCandidate(
                    candidate.Equipment.CandidateId,
                    candidate.Equipment.StableDeviceIdentity,
                    candidate.Equipment.Name,
                    SanitizeEndpoint(settings),
                    SuggestedSettings: SafeSuggestedSettings(settings),
                    Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["haVersion"] = client.HomeAssistantVersion ?? string.Empty,
                        ["selectedEntityCount"] = selectedIds.Count.ToString(CultureInfo.InvariantCulture),
                        ["entityIdentity"] = "entity_id_mutable"
                    },
                    Issues: materialized.Issues,
                    Materialization: candidate);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            yield return Failure(
                "HA_DISCOVERY_FAILED",
                SafeFailure(ex),
                SanitizeEndpoint(settings));
        }
    }

    internal static HomeAssistantConnectionSettings ParseConnection(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var host = Get(values, "host") ?? throw new ArgumentException("Home Assistant host is required.");
        var tls = bool.TryParse(Get(values, "tls"), out var parsedTls) && parsedTls;
        var port = int.TryParse(Get(values, "port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort)
            ? parsedPort
            : tls ? 443 : 8123;
        var settings = new HomeAssistantConnectionSettings(
            host,
            port,
            tls,
            Get(values, "basePath") ?? string.Empty,
            TimeSpan.FromMilliseconds(GetInt(values, "connectTimeoutMilliseconds", 10000)),
            TimeSpan.FromMilliseconds(GetInt(values, "requestTimeoutMilliseconds", 10000)),
            TimeSpan.FromMilliseconds(GetInt(values, "reconnectMinimumMilliseconds", 1000)),
            TimeSpan.FromMilliseconds(GetInt(values, "reconnectMaximumMilliseconds", 30000)));
        settings.Validate();
        return settings;
    }

    private async ValueTask AuthenticateAsync(
        IHomeAssistantClient client,
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken)
    {
        if (!context.SecretReferences.TryGetValue(AccessTokenReferenceKey, out var reference) ||
            string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidOperationException("Home Assistant access token secret reference is required.");
        }

        var request = HomeAssistantProtectedMaterial.CreateAccessTokenRequest(
            _projectKey,
            context.DataSourceKey,
            reference);
        await using var lease = await _protectedMaterialResolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        if (lease.Material.IsEmpty)
            throw new InvalidOperationException("Home Assistant protected access token is empty.");
        await client.ConnectAndAuthenticateAsync(lease.Material, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ParseSelectedEntityIds(IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null ||
            !parameters.TryGetValue(SelectedEntityIdsParameter, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> SafeSuggestedSettings(HomeAssistantConnectionSettings settings) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = settings.Host,
            ["port"] = settings.Port.ToString(CultureInfo.InvariantCulture),
            ["tls"] = settings.UseTls.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            ["basePath"] = settings.BasePath
        };

    private static string SanitizeEndpoint(HomeAssistantConnectionSettings settings) =>
        $"{(settings.UseTls ? "https" : "http")}://{settings.Host}:{settings.Port}{NormalizeBasePath(settings.BasePath)}";

    private static string NormalizeBasePath(string basePath) =>
        string.IsNullOrWhiteSpace(basePath) ? string.Empty : $"/{basePath.Trim('/')}";

    private static DriverDiscoveryCandidate Failure(string code, string message, string? endpoint = null) =>
        new(
            "ha-discovery-failed",
            "unresolved",
            "Home Assistant endpoint",
            endpoint,
            Issues:
            [
                new DriverEngineeringIssue(
                    code,
                    DriverEngineeringIssueSeverity.Error,
                    message)
            ]);

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        int.TryParse(Get(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string SafeFailure(Exception exception) =>
        exception is HomeAssistantAuthenticationException
            ? "Home Assistant authentication failed."
            : "Home Assistant operation failed.";
}
