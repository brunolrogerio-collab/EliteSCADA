using System.Globalization;
using System.Text.Json;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Shelly;

public sealed class ShellyEngineeringProvider :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverDiscoverySource
{
    private readonly string _projectKey;
    private readonly string _dataSourceKey;
    private readonly string? _passwordReference;
    private readonly ICommunicationDriverProtectedMaterialResolver? _protectedMaterialResolver;
    private readonly Func<ShellyConnectionSettings, string, IShellyRpcClient> _clientFactory;

    public ShellyEngineeringProvider(
        string projectKey,
        string dataSourceKey,
        string? passwordReference,
        ICommunicationDriverProtectedMaterialResolver? protectedMaterialResolver,
        Func<ShellyConnectionSettings, string, IShellyRpcClient>? clientFactory = null)
    {
        _projectKey = string.IsNullOrWhiteSpace(projectKey) ? "engineering-draft" : projectKey.Trim();
        _dataSourceKey = dataSourceKey;
        _passwordReference = passwordReference;
        _protectedMaterialResolver = protectedMaterialResolver;
        _clientFactory = clientFactory ?? ((settings, source) => new ShellyRpcClient(settings, source));
    }

    public CommunicationDriverTypeDescriptor Descriptor => new ShellyDriverDescriptorProvider().Descriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = ParseConnection(context.Settings);
            await using var client = _clientFactory(settings, CreateSource());
            var infoJson = await client.CallHttpAsync(
                "Shelly.GetDeviceInfo", null, ReadOnlyMemory<byte>.Empty, false, cancellationToken).ConfigureAwait(false);
            var info = ShellyStateMapper.ParseDeviceInfo(infoJson);
            var legacy = ShellyDriver.IsLegacyFirmware(info.Firmware);
            using var credential = await ResolveCredentialAsync(cancellationToken).ConfigureAwait(false);
            _ = await client.CallHttpAsync(
                "Shelly.GetStatus", null, credential.Password, legacy, cancellationToken).ConfigureAwait(false);

            return new DriverConnectionTestResult(
                true,
                $"{(settings.UseTls ? "https" : "http")}://{settings.Host}:{settings.Port}",
                info.StableDeviceIdentity,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["manufacturer"] = "Shelly",
                    ["model"] = info.Model,
                    ["firmware"] = info.Firmware ?? string.Empty,
                    ["mac"] = info.Mac ?? string.Empty,
                    ["generation"] = infoJson.TryGetProperty("gen", out var gen) ? gen.ToString() : string.Empty,
                    ["authEnabled"] = infoJson.TryGetProperty("auth_en", out var auth) && auth.ValueKind == JsonValueKind.True ? "true" : "false"
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
                        "SHELLY_CONNECTION_TEST_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }
    }

    public async IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        yield return await DiscoverOneAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DriverDiscoveryCandidate> DiscoverOneAsync(
        DriverDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var settingsMap = request.Context?.Settings
            ?? request.Parameters
            ?? new Dictionary<string, string>();

        ShellyConnectionSettings settings;
        try
        {
            settings = ParseConnection(settingsMap);
        }
        catch (Exception ex)
        {
            return new DriverDiscoveryCandidate(
                "shelly-invalid-config",
                "unresolved",
                "Shelly endpoint",
                Issues:
                [
                    new DriverEngineeringIssue(
                        "SHELLY_DISCOVERY_CONFIGURATION",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }

        await using var client = _clientFactory(settings, CreateSource());
        try
        {
            var infoJson = await client.CallHttpAsync(
                "Shelly.GetDeviceInfo", null, ReadOnlyMemory<byte>.Empty, false, cancellationToken).ConfigureAwait(false);
            var info = ShellyStateMapper.ParseDeviceInfo(infoJson);
            var legacy = ShellyDriver.IsLegacyFirmware(info.Firmware);
            using var credential = await ResolveCredentialAsync(cancellationToken).ConfigureAwait(false);
            var status = await client.CallHttpAsync(
                "Shelly.GetStatus", null, credential.Password, legacy, cancellationToken).ConfigureAwait(false);
            var components = await GetAllComponentsAsync(client, credential.Password, legacy, cancellationToken).ConfigureAwait(false);
            var materialization = ShellyComponentMapper.BuildMaterialization(info, components, status);

            return new DriverDiscoveryCandidate(
                $"shelly-{info.StableDeviceIdentity}",
                info.StableDeviceIdentity,
                info.Name ?? info.StableDeviceIdentity,
                $"{(settings.UseTls ? "https" : "http")}://{settings.Host}:{settings.Port}",
                SuggestedSettings: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["host"] = settings.Host,
                    ["port"] = settings.Port.ToString(CultureInfo.InvariantCulture),
                    ["tls"] = settings.UseTls.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                    ["username"] = settings.Username
                },
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["manufacturer"] = "Shelly",
                    ["model"] = info.Model,
                    ["firmware"] = info.Firmware ?? string.Empty,
                    ["mac"] = info.Mac ?? string.Empty
                },
                Materialization: materialization);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DriverDiscoveryCandidate(
                "shelly-probe-failed",
                "unresolved",
                "Shelly endpoint",
                $"{(settings.UseTls ? "https" : "http")}://{settings.Host}:{settings.Port}",
                Issues:
                [
                    new DriverEngineeringIssue(
                        "SHELLY_DISCOVERY_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }
    }

    private async ValueTask<JsonElement> GetAllComponentsAsync(
        IShellyRpcClient client,
        ReadOnlyMemory<byte> password,
        bool legacy,
        CancellationToken cancellationToken)
    {
        var all = new List<JsonElement>();
        var offset = 0;
        var total = int.MaxValue;
        while (offset < total)
        {
            var page = await client.CallHttpAsync(
                "Shelly.GetComponents",
                new { offset },
                password,
                legacy,
                cancellationToken).ConfigureAwait(false);
            if (!page.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
                break;
            foreach (var component in components.EnumerateArray())
                all.Add(component.Clone());

            var count = components.GetArrayLength();
            total = page.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var t)
                ? t
                : offset + count;
            if (count == 0) break;
            offset += count;
        }

        return JsonSerializer.SerializeToElement(new { components = all, total = all.Count, offset = 0 });
    }

    private async ValueTask<ShellyResolvedCredential> ResolveCredentialAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_passwordReference))
            return new ShellyResolvedCredential(ReadOnlyMemory<byte>.Empty);
        if (_protectedMaterialResolver is null)
            throw new InvalidOperationException("Shelly protected password reference is configured but no host protected-material resolver is available.");

        var request = new CommunicationDriverProtectedMaterialRequest(
            _projectKey,
            _dataSourceKey,
            ShellyRpcContract.DriverType,
            ShellyRpcContract.PasswordPurpose,
            _passwordReference);
        request.Validate();
        await using var lease = await _protectedMaterialResolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        return new ShellyResolvedCredential(lease.Material);
    }

    internal static ShellyConnectionSettings ParseConnection(IReadOnlyDictionary<string, string> values)
    {
        var host = Get(values, "host") ?? throw new ArgumentException("Shelly host is required.");
        var tls = bool.TryParse(Get(values, "tls"), out var parsedTls) && parsedTls;
        var port = int.TryParse(Get(values, "port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort)
            ? parsedPort
            : tls ? 443 : 80;
        var timeout = GetInt(values, "requestTimeoutMilliseconds", 5000);
        var reconnectMin = GetInt(values, "reconnectMinimumMilliseconds", 1000);
        var reconnectMax = GetInt(values, "reconnectMaximumMilliseconds", 30000);
        var settings = new ShellyConnectionSettings(
            host,
            port,
            tls,
            Get(values, "username") ?? "admin",
            TimeSpan.FromMilliseconds(timeout),
            TimeSpan.FromMilliseconds(reconnectMin),
            TimeSpan.FromMilliseconds(reconnectMax));
        settings.Validate();
        return settings;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        int.TryParse(Get(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string CreateSource() => $"elitescada-eng-{Guid.NewGuid():N}"[..30];

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }
}
