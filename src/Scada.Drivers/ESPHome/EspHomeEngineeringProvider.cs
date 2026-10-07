using System.Globalization;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.ESPHome;

public sealed class EspHomeEngineeringProvider :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverDiscoverySource
{
    private readonly string _projectKey;
    private readonly string _dataSourceKey;
    private readonly string? _encryptionKeyReference;
    private readonly ICommunicationDriverProtectedMaterialResolver? _protectedMaterialResolver;
    private readonly Func<EspHomeConnectionSettings, EspHomeNoiseKeyProvider, IEspHomeNativeClient> _clientFactory;

    public EspHomeEngineeringProvider(
        string projectKey,
        string dataSourceKey,
        string? encryptionKeyReference,
        ICommunicationDriverProtectedMaterialResolver? protectedMaterialResolver,
        Func<EspHomeConnectionSettings, EspHomeNoiseKeyProvider, IEspHomeNativeClient>? clientFactory = null)
    {
        _projectKey = string.IsNullOrWhiteSpace(projectKey) ? "engineering-draft" : projectKey.Trim();
        _dataSourceKey = dataSourceKey;
        _encryptionKeyReference = encryptionKeyReference;
        _protectedMaterialResolver = protectedMaterialResolver;
        _clientFactory = clientFactory ?? ((settings, keyProvider) => new EspHomeNativeClient(settings, keyProvider));
    }

    public CommunicationDriverTypeDescriptor Descriptor => new EspHomeDriverDescriptorProvider().Descriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        IEspHomeNativeClient? client = null;
        try
        {
            var settings = ParseConnection(context.Settings);
            var keyProvider = CreateKeyProvider(settings);
            client = _clientFactory(settings, keyProvider);
            var inventory = await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            return new DriverConnectionTestResult(
                true,
                settings.SanitizedEndpoint,
                inventory.Device.StableDeviceIdentity,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = inventory.Device.Name,
                    ["friendlyName"] = inventory.Device.FriendlyName,
                    ["mac"] = inventory.Device.OriginalMacAddress,
                    ["esphomeVersion"] = inventory.Device.ESPHomeVersion,
                    ["model"] = inventory.Device.Model,
                    ["manufacturer"] = inventory.Device.Manufacturer,
                    ["apiVersion"] = inventory.NegotiatedVersion.ToString(),
                    ["entityCount"] = inventory.Entities.Count.ToString(CultureInfo.InvariantCulture),
                    ["encryptionMode"] = settings.EncryptionMode.ToString().ToLowerInvariant(),
                    ["deepSleepPolicy"] = settings.DeepSleepPolicy.ToString().ToLowerInvariant(),
                    ["deviceReportsDeepSleep"] = inventory.Device.HasDeepSleep.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()
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
                        "ESPHOME_CONNECTION_TEST_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }
        finally
        {
            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
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

        EspHomeConnectionSettings settings;
        try
        {
            settings = ParseConnection(settingsMap);
            EspHomeSecurityPolicy.Validate(settings.EncryptionMode, _encryptionKeyReference);
        }
        catch (Exception ex)
        {
            return new DriverDiscoveryCandidate(
                "esphome-invalid-config",
                "unresolved",
                "ESPHome endpoint",
                Issues:
                [
                    new DriverEngineeringIssue(
                        "ESPHOME_DISCOVERY_CONFIGURATION",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }

        await using var client = _clientFactory(settings, CreateKeyProvider(settings));
        try
        {
            var inventory = await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            var materialization = EspHomeEntityMapper.BuildMaterialization(inventory);
            return new DriverDiscoveryCandidate(
                $"esphome-{inventory.Device.StableDeviceIdentity}",
                inventory.Device.StableDeviceIdentity,
                string.IsNullOrWhiteSpace(inventory.Device.FriendlyName)
                    ? inventory.Device.Name
                    : inventory.Device.FriendlyName,
                settings.SanitizedEndpoint,
                SuggestedSettings: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["host"] = settings.Host,
                    ["port"] = settings.Port.ToString(CultureInfo.InvariantCulture),
                    ["encryptionMode"] = settings.EncryptionMode.ToString().ToLowerInvariant()
                },
                Metadata: materialization.Equipment.Metadata,
                Materialization: materialization);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DriverDiscoveryCandidate(
                "esphome-probe-failed",
                "unresolved",
                "ESPHome endpoint",
                settings.SanitizedEndpoint,
                Issues:
                [
                    new DriverEngineeringIssue(
                        "ESPHOME_DISCOVERY_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }
    }

    public static EspHomeConnectionSettings ParseConnection(IReadOnlyDictionary<string, string> values)
    {
        var host = Get(values, "host") ?? throw new ArgumentException("ESPHome host is required.");
        var port = GetInt(values, "port", 6053);
        var mode = Get(values, "encryptionMode")?.ToLowerInvariant() switch
        {
            null or "" or "noise" => EspHomeNativeEncryptionMode.Noise,
            "plaintext" => EspHomeNativeEncryptionMode.Plaintext,
            var unsupported => throw new ArgumentException($"ESPHome encryptionMode '{unsupported}' is unsupported.")
        };
        var deepSleepPolicy = Get(values, "deepSleepPolicy")?.ToLowerInvariant() switch
        {
            null or "" or "normal" => EspHomeDeepSleepPolicy.Normal,
            "expected" => EspHomeDeepSleepPolicy.Expected,
            var unsupported => throw new ArgumentException($"ESPHome deepSleepPolicy '{unsupported}' is unsupported.")
        };
        var reconnectMinimum = GetOptionalInt(values, "reconnectMinimumMilliseconds");
        var reconnectMaximum = GetOptionalInt(values, "reconnectMaximumMilliseconds");
        var keepAlive = GetOptionalInt(values, "keepAliveMilliseconds");
        var settings = new EspHomeConnectionSettings(
            host,
            port,
            mode,
            TimeSpan.FromMilliseconds(GetInt(values, "requestTimeoutMilliseconds", 5000)),
            TimeSpan.FromMilliseconds(GetInt(values, "writeReconcileTimeoutMilliseconds", 3000)),
            reconnectMinimum.HasValue ? TimeSpan.FromMilliseconds(reconnectMinimum.Value) : null,
            reconnectMaximum.HasValue ? TimeSpan.FromMilliseconds(reconnectMaximum.Value) : null,
            keepAlive.HasValue ? TimeSpan.FromMilliseconds(keepAlive.Value) : null,
            deepSleepPolicy);
        settings.Validate();
        return settings;
    }

    private EspHomeNoiseKeyProvider CreateKeyProvider(EspHomeConnectionSettings settings)
    {
        return async cancellationToken =>
        {
            if (settings.EncryptionMode == EspHomeNativeEncryptionMode.Plaintext)
                return null;
            EspHomeSecurityPolicy.Validate(settings.EncryptionMode, _encryptionKeyReference);
            if (_protectedMaterialResolver is null)
                throw new InvalidOperationException("ESPHome Noise key reference is configured but no host protected-material resolver is available.");

            return await EspHomeNoiseKeyResolver.ResolveAsync(
                _protectedMaterialResolver,
                _projectKey,
                _dataSourceKey,
                EspHomeNativeContract.DriverType,
                _encryptionKeyReference!,
                cancellationToken).ConfigureAwait(false);
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        int.TryParse(Get(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static int? GetOptionalInt(IReadOnlyDictionary<string, string> values, string key)
    {
        var raw = Get(values, key);
        if (raw is null) return null;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException($"ESPHome setting '{key}' must be an integer.");
        return parsed;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 512 ? clean : clean[..512];
    }
}
