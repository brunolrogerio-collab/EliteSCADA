using Scada.Drivers.Abstractions;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// Single host-facing Engineering provider for the Foundation OPC UA implementation.
/// It composes discovery, secure connection test, paged browse and reconciliation while
/// keeping OPC Foundation SDK types behind the driver module boundary.
/// </summary>
public sealed class OpcUaFoundationEngineeringProvider :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverDiscoverySource,
    ICommunicationDriverBrowser,
    ICommunicationDriverReconciler,
    IAsyncDisposable
{
    private readonly OpcUaEngineeringConnectionTester _connectionTester;
    private readonly OpcUaEngineeringAdapter _adapter;
    private readonly OpcUaFoundationBrowseTransport _browseTransport;
    private readonly OpcUaEngineeringReconciler _reconciler;
    private int _disposed;

    public OpcUaFoundationEngineeringProvider(
        IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider)
    {
        ArgumentNullException.ThrowIfNull(securityMaterialProvider);

        var discoveryTransport = new OpcUaFoundationEndpointDiscoveryTransport();
        _browseTransport = new OpcUaFoundationBrowseTransport(securityMaterialProvider);
        _connectionTester = new OpcUaEngineeringConnectionTester(securityMaterialProvider);
        _adapter = new OpcUaEngineeringAdapter(discoveryTransport, _browseTransport);
        _reconciler = new OpcUaEngineeringReconciler(
            new OpcUaFoundationNodeInspectionTransport(securityMaterialProvider));
    }

    public CommunicationDriverTypeDescriptor Descriptor => OpcUaDriverDescriptorProvider.Definition;

    public ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _connectionTester.TestConnectionAsync(context, cancellationToken);
    }

    public IAsyncEnumerable<DriverDiscoveryCandidate> DiscoverAsync(
        DriverDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _adapter.DiscoverAsync(request, cancellationToken);
    }

    public ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _adapter.BrowseAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<DriverReconcileResult> ReconcileAsync(
        DriverReconcileRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _reconciler.ReconcileAsync(request, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(OpcUaFoundationEngineeringProvider));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _browseTransport.DisposeAsync().ConfigureAwait(false);
    }
}
