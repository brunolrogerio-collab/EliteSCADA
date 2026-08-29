using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoEngineeringTests
{
    [Fact]
    public void Descriptor_AdvertisesOnlyImplementedEngineeringCapabilities()
    {
        var descriptor = new S7IsoEngineeringAdapter().Descriptor;

        Assert.Equal("siemens.s7.iso", descriptor.DriverType);
        Assert.Equal(
            DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.FileImport,
            descriptor.EngineeringCapabilities);
        Assert.Contains(DriverAcquisitionMode.Polling, descriptor.AcquisitionModes);
        Assert.DoesNotContain(descriptor.ConfigurationSchema.DataSourceFields, field => field.ValueKind == DriverConfigurationValueKind.SecretReference);
        Assert.Contains(descriptor.ConfigurationSchema.TagBindingFields, field => field.Key == "valueOrder");
    }

    [Fact]
    public void Settings_RequireCpuFamilyAndConnectionMode()
    {
        var settings = new Dictionary<string, string>
        {
            ["host"] = "plc"
        };

        Assert.False(S7IsoEngineeringAdapter.TryCreateOptions(settings, out var options, out var issues));
        Assert.Null(options);
        Assert.Contains(issues, issue => issue.FieldKey == "cpuFamily");
        Assert.Contains(issues, issue => issue.FieldKey == "connectionMode");
    }

    [Fact]
    public void ExplicitTsap_SettingsDoNotInventRackOrSlot()
    {
        var settings = new Dictionary<string, string>
        {
            ["host"] = "plc",
            ["cpuFamily"] = nameof(S7CpuFamily.S7400),
            ["connectionMode"] = nameof(S7IsoConnectionMode.ExplicitTsap),
            ["sourceTsap"] = "0x0100",
            ["destinationTsap"] = "03.03"
        };

        Assert.True(S7IsoEngineeringAdapter.TryCreateOptions(settings, out var options, out var issues));
        Assert.Empty(issues);
        Assert.Equal((ushort)0x0303, options!.EffectiveDestinationTsap);
        Assert.Equal(S7IsoConnectionMode.ExplicitTsap, options.ConnectionMode);
    }

    [Fact]
    public async Task ConnectionTest_ReportsNegotiatedPduWithoutCreatingRuntimeDriver()
    {
        await using var server = new TestS7IsoServer();
        var settings = new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["port"] = server.Port.ToString(),
            ["cpuFamily"] = nameof(S7CpuFamily.S71500),
            ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
            ["rack"] = "0",
            ["slot"] = "1",
            ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
            ["reconnectDelayMs"] = "0"
        };
        var context = new DriverEngineeringDataSourceContext(
            "s7-eng",
            "S7 Engineering",
            "siemens.s7.iso",
            settings,
            new Dictionary<string, string>());

        var result = await new S7IsoEngineeringAdapter().TestConnectionAsync(context);

        Assert.True(result.Succeeded, string.Join(" | ", result.Issues?.Select(issue => issue.Message) ?? Array.Empty<string>()));
        Assert.Equal("480", result.ObservedProperties!["negotiatedPduSize"]);
        Assert.Equal("0x0301", result.ObservedProperties["destinationTsap"]);
    }
}
