using System.Collections.Concurrent;

namespace Scada.Core.InternalMemory;

public sealed record RetainedMemoryValue(
    Guid TagId,
    TypedTagValue TypedValue,
    DateTimeOffset StoredAt)
{
    public RetainedMemoryValue : this(TagId, TypedValue, StoredAt)
    {
        if (TagId == Guid.Empty)
            throw new ArgumentException("Retained memory TAG ID cannot be empty.", nameof(TagId));
        ArgumentNullException.ThrowIfNull(TypedValue);
    }
}

public interface IServerMemoryRetentionStore
{
    ValueTask<RetainedMemoryValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(RetainedMemoryValue value, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Guid tagId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deterministic retention implementation intended for focused tests and local
/// foundation use. It intentionally has no transport/timeout/reconnect model.
/// </summary>
public sealed class InMemoryServerMemoryRetentionStore : IServerMemoryRetentionStore
{
    private readonly ConcurrentDictionary<Guid, RetainedMemoryValue> _values = new();

    public InMemoryServerMemoryRetentionStore(IEnumerable<RetainedMemoryValue>? seed = null)
    {
        if (seed is null) return;

        foreach (var value in seed)
        {
            ArgumentNullException.ThrowIfNull(value);
            _values[value.TagId] = value;
        }
    }

    public ValueTask<RetainedMemoryValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tagId == Guid.Empty)
            throw new ArgumentException("TAG ID cannot be empty.", nameof(tagId));

        _values.TryGetValue(tagId, out var value);
        return ValueTask.FromResult<RetainedMemoryValue?>(value);
    }

    public ValueTask WriteAsync(RetainedMemoryValue value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(value);
        _values[value.TagId] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tagId == Guid.Empty)
            throw new ArgumentException("TAG ID cannot be empty.", nameof(tagId));

        _values.TryRemove(tagId, out _);
        return ValueTask.CompletedTask;
    }

    public IReadOnlyCollection<RetainedMemoryValue> Snapshot() =>
        _values.Values.OrderBy(x => x.TagId).ToArray();
}
