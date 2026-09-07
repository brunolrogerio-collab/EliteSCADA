namespace Scada.Api.Security;

/// <summary>
/// Serializes initial-installation mutations inside one EliteSCADA host process.
/// Durable stores still own their own transaction/locking boundaries; this gate does not
/// claim distributed coordination across multiple application hosts.
/// </summary>
public sealed class InitialInstallationGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Lease(_gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
