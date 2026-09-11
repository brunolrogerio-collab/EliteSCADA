using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Single registration surface for IEC-104 Engineering capabilities. The Coordinator/host may register
/// this object under the feature-specific common interfaces without needing to know the internal split
/// between connection test, bounded GI browse, CSV point-list import and reconciliation.
/// No canonical Engineering mutation is performed by this service.
/// </summary>
public sealed class Iec104EngineeringServices :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverBrowser,
    ICommunicationDriverFileImporter,
    ICommunicationDriverReconciler
{
    private readonly Iec104EngineeringProvider _provider;
    private readonly Iec104PointListImporter _importer;
    private readonly Iec104EngineeringReconciler _reconciler;

    public Iec104EngineeringServices(Func<IIec104ClientAdapter>? adapterFactory = null)
    {
        _provider = new Iec104EngineeringProvider(adapterFactory);
        _importer = new Iec104PointListImporter();
        _reconciler = new Iec104EngineeringReconciler(adapterFactory);

        Descriptor = Iec104DriverDescriptorProvider.Enrich(_provider.Descriptor with
        {
            EngineeringCapabilities =
                _provider.Descriptor.EngineeringCapabilities |
                DriverEngineeringCapabilities.FileImport |
                DriverEngineeringCapabilities.Reconcile,
            Description = "IEC 60870-5-104 Engineering services: connection test, bounded GI observation browse, monitored point-list CSV import and bounded reconciliation. All results remain transient until canonical Engineering validate/preview/apply."
        });
    }

    public CommunicationDriverTypeDescriptor Descriptor { get; }

    public ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default) =>
        _provider.TestConnectionAsync(context, cancellationToken);

    public ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default) =>
        _provider.BrowseAsync(request, cancellationToken);

    public IAsyncEnumerable<DriverImportCandidate> ImportAsync(
        DriverImportRequest request,
        Stream content,
        CancellationToken cancellationToken = default) =>
        _importer.ImportAsync(request, content, cancellationToken);

    public IAsyncEnumerable<DriverReconcileResult> ReconcileAsync(
        DriverReconcileRequest request,
        CancellationToken cancellationToken = default) =>
        _reconciler.ReconcileAsync(request, cancellationToken);
}
