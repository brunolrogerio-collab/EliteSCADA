using System.IO.BACnet;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetEngineeringProviderTests
{
    [Fact]
    public async Task BrowseAsync_PresentValueCarriesObservedEngineeringUnit()
    {
        var provider = new BacnetEngineeringProvider(new UnitSessionFactory(" degrees-celsius "));

        var page = await provider.BrowseAsync(new DriverBrowseRequest(
            Context(),
            ParentNodeId: "object:0:1"));

        var node = Assert.Single(page.Nodes);
        Assert.Equal("degrees-celsius", node.EngineeringUnit);
        Assert.Equal(TagDataType.Float, node.SuggestedDataType);
        Assert.Equal(new BacnetBinding(100, 0, 1, 85).PortableAddress, node.PortableAddress);
    }

    [Fact]
    public async Task ReconcileAsync_PreservesObservedEngineeringUnitAsMetadata()
    {
        var provider = new BacnetEngineeringProvider(new UnitSessionFactory("degrees-celsius"));
        var address = new BacnetBinding(100, 0, 1, 85).PortableAddress;
        var results = new List<DriverReconcileResult>();

        await foreach (var result in provider.ReconcileAsync(new DriverReconcileRequest(
                           Context(),
                           new[] { address })))
        {
            results.Add(result);
        }

        var reconciled = Assert.Single(results);
        Assert.Equal(DriverReconcileStatus.Unchanged, reconciled.Status);
        Assert.NotNull(reconciled.Metadata);
        Assert.Equal("degrees-celsius", reconciled.Metadata!["engineeringUnit"]);
    }

    private static DriverEngineeringDataSourceContext Context()
        => new(
            "bacnet-units",
            "BACnet Units",
            BacnetDriverDescriptor.DriverType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["deviceInstance"] = "100"
            },
            new Dictionary<string, string>(StringComparer.Ordinal));

    private sealed class UnitSessionFactory(string engineeringUnit) : IBacnetSessionFactory
    {
        public IBacnetSession Create(BacnetSessionOptions options) => new UnitSession(engineeringUnit);
    }

    private sealed class UnitSession(string engineeringUnit) : IBacnetSession
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<BacnetDeviceObservation> ResolveDeviceAsync(
            uint deviceInstance,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(
            int? maximumResults = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BacnetPropertyReadResult> ReadAsync(
            BacnetBinding binding,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BacnetPropertyReadResult(
                binding,
                new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, 12.5f) },
                DateTimeOffset.UtcNow,
                new BacnetObjectState(Reliability: 0, Units: engineeringUnit),
                UsedReadPropertyMultiple: true));

        public Task WriteAsync(
            BacnetBinding binding,
            IReadOnlyCollection<BacnetValue> values,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IDisposable?> TrySubscribeCovAsync(
            BacnetBinding binding,
            Func<BacnetPropertyReadResult, ValueTask> onNotification,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IDisposable?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
