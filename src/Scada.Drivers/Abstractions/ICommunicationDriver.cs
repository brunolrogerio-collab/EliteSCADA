using Scada.Core.Tags;

namespace Scada.Drivers.Abstractions;

public interface ICommunicationDriver : IAsyncDisposable
{
    string DriverId { get; }
    string Name { get; }
    DriverCapabilities Capabilities { get; }
    DriverStatus Status { get; }
    IReadOnlyCollection<TagDefinition> Tags { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default);
}
