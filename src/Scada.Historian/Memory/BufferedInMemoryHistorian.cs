using System.Collections.Concurrent;
using System.Threading.Channels;
using Scada.Core.Abstractions;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Abstractions;

namespace Scada.Historian.Memory;

public sealed class BufferedInMemoryHistorian : IHistorian
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<TagValue>> _history = new();
    private readonly Channel<TagValue> _queue;
    private readonly IDisposable _subscription;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writer;
    private long _written;
    private long _pending;

    public BufferedInMemoryHistorian(IScadaEventBus eventBus, int capacity = 100_000)
    {
        _queue = Channel.CreateBounded<TagValue>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _subscription = eventBus.Subscribe<TagValueChanged>(OnTagValueChangedAsync);
        _writer = Task.Run(() => WriterLoopAsync(_cts.Token));
    }

    public long WrittenSamples => Interlocked.Read(ref _written);
    public long PendingSamples => Math.Max(0, Interlocked.Read(ref _pending));

    public IReadOnlyList<TagValue> Query(Guid tagId, DateTimeOffset from, DateTimeOffset to, int limit = 5000)
    {
        if (!_history.TryGetValue(tagId, out var values)) return Array.Empty<TagValue>();
        return values.Where(x => x.Timestamp >= from && x.Timestamp <= to)
            .OrderBy(x => x.Timestamp)
            .Take(Math.Clamp(limit, 1, 50_000))
            .ToArray();
    }

    private ValueTask OnTagValueChangedAsync(TagValueChanged evt)
    {
        if (_queue.Writer.TryWrite(evt.Current)) Interlocked.Increment(ref _pending);
        return ValueTask.CompletedTask;
    }

    private async Task WriterLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var sample in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                Interlocked.Decrement(ref _pending);
                var series = _history.GetOrAdd(sample.TagId, _ => new ConcurrentQueue<TagValue>());
                series.Enqueue(sample);
                while (series.Count > 100_000 && series.TryDequeue(out _)) { }
                Interlocked.Increment(ref _written);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        _queue.Writer.TryComplete();
        await _cts.CancelAsync();
        try { await _writer; } catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}
