namespace Scada.Api.Persistence;

public sealed class DatabaseMaintenanceGate
{
    private readonly object _gate = new();
    private Guid? _operationId;
    private DateTimeOffset? _expiresAtUtc;

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

    public DateTimeOffset Enter(Guid operationId, TimeSpan lease)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Migration operation id is required.", nameof(operationId));
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));

        lock (_gate)
        {
            if (IsActiveUnsafe() && _operationId != operationId)
                throw new InvalidOperationException("Another database maintenance operation is already active.");
            _operationId = operationId;
            _expiresAtUtc = DateTimeOffset.UtcNow.Add(lease);
            return _expiresAtUtc.Value;
        }
    }

    public void Exit(Guid operationId)
    {
        lock (_gate)
        {
            if (_operationId != operationId) return;
            _operationId = null;
            _expiresAtUtc = null;
        }
    }

    // The lease deadline is progress/status metadata, not an automatic unquiesce trigger.
    // COPY duration is unbounded; only the owning operation may release the mutation boundary.
    private bool IsActiveUnsafe() => _operationId.HasValue;
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
