using System.Text;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104EngineeringServicesTests
{
    [Fact]
    public void Descriptor_AnnouncesAllImplementedEngineeringCapabilities()
    {
        var services = new Iec104EngineeringServices();

        Assert.True(services.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(services.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.True(services.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.FileImport));
        Assert.True(services.Descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Reconcile));
        Assert.Empty(services.Descriptor.ConfigurationSchema.TagBindingFields);
        Assert.IsAssignableFrom<ICommunicationDriverConnectionTester>(services);
        Assert.IsAssignableFrom<ICommunicationDriverBrowser>(services);
        Assert.IsAssignableFrom<ICommunicationDriverFileImporter>(services);
        Assert.IsAssignableFrom<ICommunicationDriverReconciler>(services);
    }

    [Fact]
    public async Task ImportAsync_DelegatesToMonitoredPointListImporter()
    {
        var services = new Iec104EngineeringServices();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(
            "commonAddress,informationObjectAddress,typeId,displayName\n" +
            "1,77,M_SP_NA_1,Feed breaker\n"));
        var request = new DriverImportRequest(
            Context: null,
            SourceName: "points.csv",
            ContentType: "text/csv");

        var candidates = new List<DriverImportCandidate>();
        await foreach (var candidate in services.ImportAsync(request, content))
            candidates.Add(candidate);

        var imported = Assert.Single(candidates);
        Assert.Equal("ca=1;ioa=77", imported.PortableAddress);
        Assert.Equal("Feed breaker", imported.DisplayName);
        Assert.True(imported.IsReadable);
        Assert.False(imported.IsWritable);
    }
}
