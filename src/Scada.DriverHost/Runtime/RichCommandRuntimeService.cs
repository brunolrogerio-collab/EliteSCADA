using Scada.Core.Commands;

namespace Scada.DriverHost.Runtime;

/// <summary>
/// Runtime-only authoritative resolver for canonical Rich Command definitions.
/// Engineering persistence/lifecycle remains outside S2.
/// </summary>
public interface IRichCommandDefinitionResolver
{
    bool TryResolve(Guid commandId, out RichCommandDefinition? definition);
}

/// <summary>
/// Runtime-only authoritative resolver for server-owned Driver Command bindings.
/// Callers never supply a binding to the invocation boundary.
/// </summary>
public interface IRichCommandBindingResolver
{
    bool TryResolve(Guid commandId, out DriverCommandBinding? binding);
}

/// <summary>
/// Optional protocol-neutral executor capability resolved by authoritative Data Source identity.
/// No raw driver or protocol object crosses the Rich Command Runtime boundary.
/// </summary>
public interface IRichCommandDriverExecutor
{
    ValueTask<RichCommandResult> ExecuteAsync(
        RichCommandInvocation invocation,
        DriverCommandBinding binding,
        RichCommandExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Server-owned lookup from authoritative Data Source identity to an optional Rich Command executor.
/// </summary>
public interface IRichCommandDriverExecutorResolver
{
    bool TryResolve(Guid dataSourceId, out IRichCommandDriverExecutor? executor);
}

public sealed record RichCommandExecutionContext(
    DateTimeOffset AdmittedAt,
    DateTimeOffset DispatchedAt,
    TimeSpan ExecutionTimeout);

/// <summary>
/// Bounded host policy for Rich Command execution. The defaults are the S2 canonical policy:
/// 64 admitted invocations, at most 8 physical executions, and a 30 second cooperative
/// executor timeout. Tests may supply a smaller policy without changing product defaults.
/// </summary>
public sealed record RichCommandRuntimePolicy(
    int AdmissionCapacity,
    int MaxPhysicalConcurrency,
    TimeSpan ExecutionTimeout)
{
    public const int DefaultAdmissionCapacity = 64;
    public const int DefaultMaxPhysicalConcurrency = 8;

    public static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromSeconds(30);

    public static RichCommandRuntimePolicy Default { get; } =
        new(DefaultAdmissionCapacity, DefaultMaxPhysicalConcurrency, DefaultExecutionTimeout);

    public RichCommandRuntimePolicy Validate()
    {
        if (AdmissionCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(AdmissionCapacity), "Rich Command admission capacity must be at least one.");
        if (MaxPhysicalConcurrency < 1 || MaxPhysicalConcurrency > AdmissionCapacity)
            throw new ArgumentOutOfRangeException(
                nameof(MaxPhysicalConcurrency),
                "Rich Command physical concurrency must be between one and the admission capacity.");
        if (ExecutionTimeout <= TimeSpan.Zero || ExecutionTimeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(
                nameof(ExecutionTimeout),
                "Rich Command execution timeout must be positive and no greater than ten minutes.");
        return this;
    }
}

/// <summary>
/// Host-owned protocol-neutral Rich Command invocation boundary.
/// STATE != EVENT != COMMAND: this service never reads, writes or synthesizes TAG state.
/// </summary>
public interface IRichCommandRuntime
{
    ValueTask<RichCommandResult> InvokeAsync(
        RichCommandInvocation invocation,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryRichCommandDefinitionResolver : IRichCommandDefinitionResolver
{
    private readonly IReadOnlyDictionary<Guid, RichCommandDefinition> _definitions;

    public InMemoryRichCommandDefinitionResolver(IEnumerable<RichCommandDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var normalized = new Dictionary<Guid, RichCommandDefinition>();
        foreach (var definition in definitions)
        {
            var candidate = RichCommandContract.NormalizeDefinition(definition);
            if (!normalized.TryAdd(candidate.CommandId, candidate))
                throw new ArgumentException(
                    $"Rich Command definition '{candidate.CommandId}' is duplicated.",
                    nameof(definitions));
        }

        _definitions = normalized;
    }

    public bool TryResolve(Guid commandId, out RichCommandDefinition? definition) =>
        _definitions.TryGetValue(commandId, out definition);
}

public sealed class InMemoryRichCommandBindingResolver : IRichCommandBindingResolver
{
    private readonly IReadOnlyDictionary<Guid, DriverCommandBinding> _bindings;

    public InMemoryRichCommandBindingResolver(IEnumerable<DriverCommandBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var normalized = new Dictionary<Guid, DriverCommandBinding>();
        foreach (var binding in bindings)
        {
            var candidate = RichCommandContract.NormalizeBinding(binding);
            if (!normalized.TryAdd(candidate.CommandId, candidate))
                throw new ArgumentException(
                    $"Driver Command Binding '{candidate.CommandId}' is duplicated.",
                    nameof(bindings));
        }

        _bindings = normalized;
    }

    public bool TryResolve(Guid commandId, out DriverCommandBinding? binding) =>
        _bindings.TryGetValue(commandId, out binding);
}

public sealed class InMemoryRichCommandDriverExecutorResolver : IRichCommandDriverExecutorResolver
{
    private readonly IReadOnlyDictionary<Guid, IRichCommandDriverExecutor> _executors;

    public InMemoryRichCommandDriverExecutorResolver(
        IEnumerable<KeyValuePair<Guid, IRichCommandDriverExecutor>> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        var normalized = new Dictionary<Guid, IRichCommandDriverExecutor>();
        foreach (var entry in executors)
        {
            if (entry.Key == Guid.Empty)
                throw new ArgumentException("Rich Command executor DataSourceId cannot be empty.", nameof(executors));
            ArgumentNullException.ThrowIfNull(entry.Value);
            if (!normalized.TryAdd(entry.Key, entry.Value))
                throw new ArgumentException(
                    $"Rich Command executor DataSourceId '{entry.Key}' is duplicated.",
                    nameof(executors));
        }

        _executors = normalized;
    }

    public bool TryResolve(Guid dataSourceId, out IRichCommandDriverExecutor? executor) =>
        _executors.TryGetValue(dataSourceId, out executor);
}

/// <summary>
/// Bounded Rich Command Runtime core.
///
/// Admission is immediate/fail-closed: when the configured capacity is exhausted the invocation
/// is Rejected before physical dispatch. Admitted work is ordered FIFO per authoritative
/// (DataSourceId, StableDeviceIdentity) target, while independent targets may execute concurrently
/// up to MaxPhysicalConcurrency.
///
/// The effectAuthority delegate is host-owned and must be the same Active/external-effect authority
/// used by CommunicationDriverRuntimeServices.CanOwnExternalEffects. It is never invocation input.
/// </summary>
public sealed class RichCommandRuntimeService : IRichCommandRuntime, IDisposable
{
    private readonly IRichCommandDefinitionResolver _definitions;
    private readonly IRichCommandBindingResolver _bindings;
    private readonly IRichCommandDriverExecutorResolver _executors;
    private readonly Func<bool> _effectAuthority;
    private readonly RichCommandRuntimePolicy _policy;
    private readonly SemaphoreSlim _admission;
    private readonly SemaphoreSlim _physicalConcurrency;
    private readonly object _orderingSync = new();
    private readonly Dictionary<TargetKey, TargetOrderState> _targetOrders = new();
    private int _disposed;

    public RichCommandRuntimeService(
        IRichCommandDefinitionResolver definitions,
        IRichCommandBindingResolver bindings,
        IRichCommandDriverExecutorResolver executors,
        Func<bool> effectAuthority,
        RichCommandRuntimePolicy? policy = null)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        _executors = executors ?? throw new ArgumentNullException(nameof(executors));
        _effectAuthority = effectAuthority ?? throw new ArgumentNullException(nameof(effectAuthority));
        _policy = (policy ?? RichCommandRuntimePolicy.Default).Validate();
        _admission = new SemaphoreSlim(_policy.AdmissionCapacity, _policy.AdmissionCapacity);
        _physicalConcurrency = new SemaphoreSlim(_policy.MaxPhysicalConcurrency, _policy.MaxPhysicalConcurrency);
    }

    public async ValueTask<RichCommandResult> InvokeAsync(
        RichCommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ThrowIfDisposed();

        if (cancellationToken.IsCancellationRequested)
            return Result(invocation, RichCommandOutcome.Rejected, "runtime.cancelled_before_dispatch");

        if (!_definitions.TryResolve(invocation.CommandId, out var definition) || definition is null)
            return Result(invocation, RichCommandOutcome.Rejected, "definition.not_found");

        RichCommandInvocation validatedInvocation;
        try
        {
            validatedInvocation = RichCommandContract.ValidateInvocation(definition, invocation);
        }
        catch (ArgumentException)
        {
            return Result(invocation, RichCommandOutcome.Rejected, "invocation.invalid");
        }

        if (!_bindings.TryResolve(validatedInvocation.CommandId, out var binding) || binding is null)
            return Result(validatedInvocation, RichCommandOutcome.Rejected, "binding.not_found");

        DriverCommandBinding validatedBinding;
        try
        {
            validatedBinding = RichCommandContract.ValidateBinding(definition, binding);
        }
        catch (ArgumentException)
        {
            return Result(validatedInvocation, RichCommandOutcome.Rejected, "binding.invalid");
        }

        if (!_executors.TryResolve(validatedBinding.DataSourceId, out var executor) || executor is null)
            return Result(validatedInvocation, RichCommandOutcome.Rejected, "executor.unsupported");

        if (!CanOwnExternalEffects())
            return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.non_authoritative");

        if (!_admission.Wait(0))
            return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.saturated");

        var admittedAt = DateTimeOffset.UtcNow;
        var ticket = EnqueueTarget(validatedBinding);
        try
        {
            // Do not bypass an earlier same-target invocation. Caller cancellation is observed
            // at the first safe pre-dispatch boundary once the predecessor leaves the target.
            await ticket.Predecessor.ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.cancelled_before_dispatch");

            if (!CanOwnExternalEffects())
                return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.non_authoritative");

            var physicalSlotOwned = false;
            try
            {
                try
                {
                    await _physicalConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                    physicalSlotOwned = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.cancelled_before_dispatch");
                }

                if (cancellationToken.IsCancellationRequested)
                    return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.cancelled_before_dispatch");

                if (!CanOwnExternalEffects())
                    return Result(validatedInvocation, RichCommandOutcome.Rejected, "runtime.non_authoritative");

                var dispatchedAt = DateTimeOffset.UtcNow;
                using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                execution.CancelAfter(_policy.ExecutionTimeout);
                var context = new RichCommandExecutionContext(admittedAt, dispatchedAt, _policy.ExecutionTimeout);

                try
                {
                    // Crossing this boundary is the single physical dispatch attempt. There is
                    // deliberately no retry here for cancellation, timeout, exception or Unknown.
                    var result = await executor.ExecuteAsync(
                        validatedInvocation,
                        validatedBinding,
                        context,
                        execution.Token).ConfigureAwait(false);

                    try
                    {
                        return RichCommandContract.ValidateResult(validatedInvocation, result);
                    }
                    catch (ArgumentException)
                    {
                        return Result(validatedInvocation, RichCommandOutcome.Unknown, "executor.invalid_result");
                    }
                }
                catch (OperationCanceledException) when (execution.IsCancellationRequested)
                {
                    return Result(
                        validatedInvocation,
                        RichCommandOutcome.Unknown,
                        cancellationToken.IsCancellationRequested
                            ? "execution.cancelled_ambiguous"
                            : "execution.timeout_ambiguous");
                }
                catch (Exception)
                {
                    // Once the executor boundary has been crossed, an unclassified exception cannot
                    // prove whether the remote/physical action happened. Fail safe as Unknown.
                    return Result(validatedInvocation, RichCommandOutcome.Unknown, "execution.exception_ambiguous");
                }
            }
            finally
            {
                if (physicalSlotOwned)
                    _physicalConcurrency.Release();
            }
        }
        finally
        {
            CompleteTarget(ticket);
            _admission.Release();
        }
    }

    private bool CanOwnExternalEffects()
    {
        try
        {
            return _effectAuthority();
        }
        catch
        {
            return false;
        }
    }

    private TargetTicket EnqueueTarget(DriverCommandBinding binding)
    {
        var key = new TargetKey(binding.DataSourceId, binding.StableDeviceIdentity);
        lock (_orderingSync)
        {
            if (!_targetOrders.TryGetValue(key, out var state))
            {
                state = new TargetOrderState();
                _targetOrders.Add(key, state);
            }

            var predecessor = state.Tail;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            state.Tail = completion.Task;
            state.References++;
            return new TargetTicket(key, state, predecessor, completion);
        }
    }

    private void CompleteTarget(TargetTicket ticket)
    {
        ticket.Completion.TrySetResult(true);
        lock (_orderingSync)
        {
            ticket.State.References--;
            if (ticket.State.References == 0 &&
                ReferenceEquals(ticket.State.Tail, ticket.Completion.Task))
            {
                _targetOrders.Remove(ticket.Key);
            }
        }
    }

    private static RichCommandResult Result(
        RichCommandInvocation invocation,
        RichCommandOutcome outcome,
        string code) =>
        new(
            invocation.InvocationId,
            invocation.CommandId,
            outcome,
            DateTimeOffset.UtcNow,
            code);

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RichCommandRuntimeService));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _admission.Dispose();
        _physicalConcurrency.Dispose();
    }

    private readonly record struct TargetKey(Guid DataSourceId, string StableDeviceIdentity);

    private sealed class TargetOrderState
    {
        public Task Tail { get; set; } = Task.CompletedTask;
        public int References { get; set; }
    }

    private sealed record TargetTicket(
        TargetKey Key,
        TargetOrderState State,
        Task Predecessor,
        TaskCompletionSource<bool> Completion);
}
