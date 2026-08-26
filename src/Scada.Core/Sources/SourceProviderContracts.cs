using Scada.Core.Tags;

namespace Scada.Core.Sources;

public enum SourceProviderOwnerScope
{
    Server,
    RuntimeClient
}

public sealed record SourceProviderDescriptor(
    string TypeKey,
    SourceProviderOwnerScope OwnerScope,
    bool Retentive,
    bool HasNetworkTransport,
    bool HasSingleServerAuthoritativeValue);

public static class BuiltInSourceProviderDescriptors
{
    public static SourceProviderDescriptor ServerMemory { get; } = new(
        "builtin.memory.server",
        SourceProviderOwnerScope.Server,
        Retentive: true,
        HasNetworkTransport: false,
        HasSingleServerAuthoritativeValue: true);

    public static SourceProviderDescriptor ClientMemory { get; } = new(
        "builtin.memory.client",
        SourceProviderOwnerScope.RuntimeClient,
        Retentive: false,
        HasNetworkTransport: false,
        HasSingleServerAuthoritativeValue: false);
}

/// <summary>
/// Common runtime boundary for a source that owns TAG values without implying
/// that the source is a network communication driver.
/// </summary>
public interface ISourceProvider
{
    SourceProviderDescriptor Descriptor { get; }
    string InstanceKey { get; }
    IReadOnlyCollection<TagDefinition> Tags { get; }

    ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default);
}
