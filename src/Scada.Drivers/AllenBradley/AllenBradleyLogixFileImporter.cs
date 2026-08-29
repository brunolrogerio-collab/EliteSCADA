using Scada.Drivers.Abstractions;

namespace Scada.Drivers.AllenBradley;

/// <summary>
/// File-import capability dispatcher for Allen-Bradley Logix Engineering.
/// L5X remains handled by the existing XML adapter; L5K is parsed by the
/// bounded ASCII adapter. Both converge on the same DriverImportCandidate
/// contract and neither mutates canonical Engineering by itself.
/// </summary>
public sealed class AllenBradleyLogixFileImporter : ICommunicationDriverFileImporter
{
    private readonly AllenBradleyLogixEngineeringAdapter _l5xAdapter;

    public AllenBradleyLogixFileImporter(ILogixProtocolClientFactory? clientFactory = null)
    {
        _l5xAdapter = new AllenBradleyLogixEngineeringAdapter(clientFactory);
    }

    public CommunicationDriverTypeDescriptor Descriptor => _l5xAdapter.Descriptor;

    public IAsyncEnumerable<DriverImportCandidate> ImportAsync(
        DriverImportRequest request,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        return IsL5k(request)
            ? LogixL5kImporter.ImportAsync(request, content, cancellationToken: cancellationToken)
            : _l5xAdapter.ImportAsync(request, content, cancellationToken);
    }

    private static bool IsL5k(DriverImportRequest request)
    {
        if (request.SourceName.EndsWith(".l5k", StringComparison.OrdinalIgnoreCase)) return true;
        return request.ContentType?.Trim().ToLowerInvariant() is
            "application/x-logix-l5k" or
            "application/vnd.rockwell.l5k" or
            "text/x-logix-l5k";
    }
}
