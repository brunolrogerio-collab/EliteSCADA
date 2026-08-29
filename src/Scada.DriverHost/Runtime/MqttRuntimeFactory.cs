using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Mqtt;

namespace Scada.DriverHost.Runtime;

/// <summary>
/// Host-owned composition seam for MQTT runtime plans. Secret storage remains
/// outside the driver; callers may provide a resolver that turns the canonical
/// secret reference on the plan into short-lived credential material.
/// </summary>
public delegate ValueTask<MqttResolvedCredentials> MqttRuntimeCredentialResolver(
    MqttRuntimePlan plan,
    CancellationToken cancellationToken);

public sealed class MqttRuntimeFactory
{
    private readonly Func<IMqttClientTransport> _transportFactory;
    private readonly MqttRuntimeCredentialResolver? _credentialResolver;

    public MqttRuntimeFactory(
        Func<IMqttClientTransport>? transportFactory = null,
        MqttRuntimeCredentialResolver? credentialResolver = null)
    {
        _transportFactory = transportFactory ?? (() => new MqttNetClientTransport());
        _credentialResolver = credentialResolver;
    }

    public MqttDriver Create(
        MqttRuntimePlan plan,
        ICurrentTagCache cache,
        ITagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);

        plan.Connection.Validate();
        if (plan.Points.Count == 0)
            throw new ArgumentException("MQTT runtime plan must contain at least one point.", nameof(plan));

        foreach (var point in plan.Points) point.Validate();
        if (plan.Points.Select(point => point.Tag.Id).Distinct().Count() != plan.Points.Count)
            throw new ArgumentException("MQTT runtime plan contains duplicate TAG IDs.", nameof(plan));

        if (plan.PasswordSecretReference is not null && _credentialResolver is null)
        {
            throw new InvalidOperationException(
                $"MQTT data source '{plan.DataSourceKey}' references protected credentials, but the host did not provide a secret resolver.");
        }

        var transport = _transportFactory()
            ?? throw new InvalidOperationException("MQTT transport factory returned null.");

        MqttCredentialResolver credentials = async cancellationToken =>
        {
            if (_credentialResolver is null)
                return new MqttResolvedCredentials(plan.Username, ReadOnlyMemory<byte>.Empty);

            var resolved = await _credentialResolver(plan, cancellationToken);
            ValidateResolvedCredentials(plan, resolved);
            return resolved;
        };

        return new MqttDriver(
            plan.DriverId,
            plan.Name,
            plan.Connection,
            cache,
            registry,
            plan.Points,
            transport,
            credentials);
    }

    private static void ValidateResolvedCredentials(
        MqttRuntimePlan plan,
        MqttResolvedCredentials resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        if (!string.Equals(plan.Username, resolved.Username, StringComparison.Ordinal))
        {
            throw new MqttTransportException(
                $"Resolved MQTT username does not match the canonical Engineering username for data source '{plan.DataSourceKey}'.",
                isPermanent: true);
        }

        if (plan.PasswordSecretReference is not null && resolved.Password.IsEmpty)
        {
            throw new MqttTransportException(
                $"Protected MQTT credential reference for data source '{plan.DataSourceKey}' resolved to empty material.",
                isPermanent: true);
        }

        if (resolved.Username is null && !resolved.Password.IsEmpty)
        {
            throw new MqttTransportException(
                $"MQTT data source '{plan.DataSourceKey}' resolved password material without a username.",
                isPermanent: true);
        }
    }
}
