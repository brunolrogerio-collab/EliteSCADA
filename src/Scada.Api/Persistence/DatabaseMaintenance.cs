using Scada.Core.Persistence;

namespace Scada.Api.Persistence;

public sealed class DatabaseMaintenanceGate : IDurableWriteAdmission
{
    private readonly object _gate = new();
    private Guid? _operationId;
    private DateTimeOffset? _expiresAtUtc;
    private int _activeWriters;
    private TaskCompletionSource<bool>? _drained;

    public bool IsActive
    {
        get
        {
            lock (_gate)
                return IsActiveUnsafe();
        }
    }

    public Guid? OperationId
    {
        get { lock (_gate) return IsActiveUnsafe() ? _operationId : null; }
    }

    public int ActiveWriterCount
    {
        get { lock (_gate) return _activeWriters; }
    }

    public DateTimeOffset? LeaseExpiresAtUtc
    {
        get { lock (_gate) return _expiresAtUtc; }
    }

    public DateTimeOffset Enter(Guid operationId, TimeSpan lease) =>
        EnterAsync(operationId, lease).GetAwaiter().GetResult();

    public async Task<DateTimeOffset> EnterAsync(
        Guid operationId,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Migration operation id is required.", nameof(operationId));
        if (lease <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lease));

        Task? drainTask = null;
        DateTimeOffset expiresAtUtc;
        lock (_gate)
        {
            if (IsActiveUnsafe() && _operationId != operationId)
                throw new InvalidOperationException("Another database maintenance operation is already active.");

            _operationId = operationId;
            _expiresAtUtc = DateTimeOffset.UtcNow.Add(lease);
            expiresAtUtc = _expiresAtUtc.Value;

            if (_activeWriters > 0)
            {
                _drained ??= new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                drainTask = _drained.Task;
            }
        }

        if (drainTask is not null)
            await drainTask.WaitAsync(cancellationToken);

        return expiresAtUtc;
    }

    public void Recover(Guid operationId, DateTimeOffset? leaseExpiresAtUtc = null)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Migration operation id is required.", nameof(operationId));

        lock (_gate)
        {
            if (IsActiveUnsafe() && _operationId != operationId)
                throw new InvalidOperationException("Another database maintenance operation is already active.");
            if (_activeWriters != 0)
                throw new InvalidOperationException("Database maintenance recovery must occur before durable writers start.");

            _operationId = operationId;
            _expiresAtUtc = leaseExpiresAtUtc;
            _drained = null;
        }
    }

    public ValueTask<IAsyncDisposable> AcquireAsync(
        string writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(writer);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (IsActiveUnsafe())
                throw new DurableWriteQuiescedException(writer.Trim(), _operationId!.Value);

            checked { _activeWriters++; }
            return ValueTask.FromResult<IAsyncDisposable>(new DurableWriterLease(this));
        }
    }

    public void Exit(Guid operationId)
    {
        lock (_gate)
        {
            if (_operationId != operationId) return;
            _operationId = null;
            _expiresAtUtc = null;
            _drained = null;
        }
    }

    private void ReleaseWriter()
    {
        TaskCompletionSource<bool>? drained = null;
        lock (_gate)
        {
            if (_activeWriters <= 0)
                throw new InvalidOperationException("Durable writer lease accounting underflow.");

            _activeWriters--;
            if (_activeWriters == 0 && IsActiveUnsafe() && _drained is not null)
            {
                drained = _drained;
                _drained = null;
            }
        }

        drained?.TrySetResult(true);
    }

    // The lease deadline is progress/status metadata, not an automatic unquiesce trigger.
    // COPY duration is unbounded; only the owning operation may release the mutation boundary.
    private bool IsActiveUnsafe() => _operationId.HasValue;

    private sealed class DurableWriterLease(DatabaseMaintenanceGate owner) : IAsyncDisposable
    {
        private DatabaseMaintenanceGate? _owner = owner;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _owner, null)?.ReleaseWriter();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class DatabaseMaintenanceMiddleware(
    RequestDelegate next,
    DatabaseMaintenanceGate gate)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!gate.IsActive ||
            HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/api/admin/database-topology") ||
            context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/ready"))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Database migration maintenance boundary is active.",
            code = "database-maintenance"
        });
    }
}
