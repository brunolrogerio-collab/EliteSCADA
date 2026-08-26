using System.Threading.Channels;

namespace Scada.Security.Audit;

public sealed record AuditBufferPolicy(
    int Capacity = 1024,
    TimeSpan? RetryDelay = null,
    TimeSpan? ShutdownFlushTimeout = null)
{
    public TimeSpan EffectiveRetryDelay => RetryDelay ?? TimeSpan.FromSeconds(1);
    public TimeSpan EffectiveShutdownFlushTimeout => ShutdownFlushTimeout ?? TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (Capacity is < 1 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(Capacity), "Audit buffer capacity must be between 1 and 100000.");
        if (EffectiveRetryDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryDelay), "Audit retry delay must be positive.");
        if (EffectiveShutdownFlushTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ShutdownFlushTimeout), "Audit shutdown flush timeout must be positive.");
    }
}

public sealed record AuditBufferHealthSnapshot(
    int QueueDepth,
    long SuccessfullyForwardedCount,
    long ForwardFailureCount,
    long RejectedCount,
    long DroppedOnShutdownCount,
    DateTimeOffset? LastForwardedAtUtc,
    DateTimeOffset? LastFailureAtUtc);

public sealed class AuditBufferFullException : InvalidOperationException
{
    public AuditBufferFullException(int capacity)
        : base($"Audit buffer capacity {capacity} was exhausted. The event was rejected and must be diagnosed explicitly.")
    {
        Capacity = capacity;
    }

    public int Capacity { get; }
}

public sealed class BufferedAuditSink : IAuditSink, IAsyncDisposable
{
    private readonly IAuditSink _inner;
    private readonly AuditBufferPolicy _policy;
    private readonly Channel<AuditEvent> _channel;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private int _queueDepth;
    private long _successCount;
    private long _failureCount;
    private long _rejectedCount;
    private long _droppedOnShutdownCount;
    private long _lastForwardedUtcTicks;
    private long _lastFailureUtcTicks;
    private int _disposed;

    public BufferedAuditSink(IAuditSink inner, AuditBufferPolicy? policy = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? new AuditBufferPolicy();
        _policy.Validate();

        _channel = Channel.CreateBounded<AuditEvent>(new BoundedChannelOptions(_policy.Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessAsync);
    }

    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = AuditSanitizer.Normalize(auditEvent);
        if (!_channel.Writer.TryWrite(normalized))
        {
            Interlocked.Increment(ref _rejectedCount);
            throw new AuditBufferFullException(_policy.Capacity);
        }

        Interlocked.Increment(ref _queueDepth);
        return ValueTask.CompletedTask;
    }

    public AuditBufferHealthSnapshot GetHealthSnapshot() => new(
        Math.Max(0, Volatile.Read(ref _queueDepth)),
        Interlocked.Read(ref _successCount),
        Interlocked.Read(ref _failureCount),
        Interlocked.Read(ref _rejectedCount),
        Interlocked.Read(ref _droppedOnShutdownCount),
        ReadTimestamp(ref _lastForwardedUtcTicks),
        ReadTimestamp(ref _lastFailureUtcTicks));

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var auditEvent in _channel.Reader.ReadAllAsync(_stop.Token))
            {
                while (!_stop.IsCancellationRequested)
                {
                    try
                    {
                        await _inner.WriteAsync(auditEvent, _stop.Token);
                        Interlocked.Increment(ref _successCount);
                        Interlocked.Exchange(ref _lastForwardedUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
                        Interlocked.Decrement(ref _queueDepth);
                        break;
                    }
                    catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                    {
                        return;
                    }
                    catch
                    {
                        Interlocked.Increment(ref _failureCount);
                        Interlocked.Exchange(ref _lastFailureUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
                        try
                        {
                            await Task.Delay(_policy.EffectiveRetryDelay, _stop.Token);
                        }
                        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _channel.Writer.TryComplete();
        var completed = await Task.WhenAny(
            _worker,
            Task.Delay(_policy.EffectiveShutdownFlushTimeout));

        if (completed != _worker)
            _stop.Cancel();

        try
        {
            await _worker;
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        finally
        {
            var dropped = Interlocked.Exchange(ref _queueDepth, 0);
            if (dropped > 0) Interlocked.Add(ref _droppedOnShutdownCount, dropped);
            _stop.Dispose();
        }
    }

    private static DateTimeOffset? ReadTimestamp(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTimeOffset(value, TimeSpan.Zero);
    }
}
