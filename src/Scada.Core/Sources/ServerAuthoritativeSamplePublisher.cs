using Scada.Core.Tags;

namespace Scada.Core.Sources;

/// <summary>
/// Canonical server-side publication boundary for internal/simulated Sources
/// that are explicitly allowed to originate TAG quality. It deliberately has
/// no HTTP/client concern and refuses RuntimeClient-owned Sources.
/// </summary>
public sealed class ServerAuthoritativeSamplePublisher
{
    private readonly IQualifiedSourceProvider _source;
    private readonly ICurrentTagCache _cache;

    public ServerAuthoritativeSamplePublisher(
        IQualifiedSourceProvider source,
        ICurrentTagCache cache)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cache);

        if (source.Descriptor.OwnerScope != SourceProviderOwnerScope.Server ||
            !source.Descriptor.HasSingleServerAuthoritativeValue)
        {
            throw new InvalidOperationException(
                $"Source '{source.InstanceKey}' is not a server-authoritative Source and cannot originate explicit TAG quality.");
        }

        _source = source;
        _cache = cache;
    }

    public async ValueTask<TagValue> PublishAsync(
        TagDefinition tag,
        QualifiedSourceSample sample,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(sample);

        if (!_source.Tags.Any(candidate => candidate.Id == tag.Id))
        {
            throw new InvalidOperationException(
                $"TAG '{tag.Path}' is not owned by server Source '{_source.InstanceKey}'.");
        }

        await _source.PublishSampleAsync(tag.Id, sample, cancellationToken);
        var current = await _source.ReadAsync(tag.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Server Source '{_source.InstanceKey}' did not expose TAG '{tag.Path}' after publishing a sample.");

        await _cache.UpdateAsync(tag, current, cancellationToken);
        return current;
    }
}
