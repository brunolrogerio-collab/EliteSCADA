using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3EngineeringContractTests
{
    [Fact]
    public void TcpConnectionOptions_ValidateIndividualAddressesAndSanitizedEndpoint()
    {
        var options = new Dnp3TcpConnectionOptions
        {
            Host = "2001:db8::10",
            Port = 20000,
            MasterAddress = 1,
            OutstationAddress = 1024,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

        options.Validate();
        Assert.Equal("[2001:db8::10]:20000", options.SanitizedEndpoint);

        Assert.Throws<ArgumentException>(() => (options with { OutstationAddress = 1 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (options with { MasterAddress = 0xFFF0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (options with { OutstationAddress = ushort.MaxValue }).Validate());
        Assert.Throws<ArgumentException>(() => (options with { Host = " station.example " }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (options with { Port = 0 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (options with { ConnectTimeout = TimeSpan.Zero }).Validate());
    }

    [Theory]
    [InlineData(Dnp3PointKind.BinaryInput, 0, "dnp3:binaryInput:0")]
    [InlineData(Dnp3PointKind.DoubleBitBinaryInput, 7, "dnp3:doubleBitBinaryInput:7")]
    [InlineData(Dnp3PointKind.AnalogInput, 42, "dnp3:analogInput:42")]
    [InlineData(Dnp3PointKind.Counter, 65535, "dnp3:counter:65535")]
    [InlineData(Dnp3PointKind.FrozenCounter, 9, "dnp3:frozenCounter:9")]
    [InlineData(Dnp3PointKind.BinaryOutputStatus, 3, "dnp3:binaryOutputStatus:3")]
    [InlineData(Dnp3PointKind.AnalogOutputStatus, 5, "dnp3:analogOutputStatus:5")]
    public void PortableAddress_IsCanonicalAndRoundTrips(Dnp3PointKind kind, int index, string expected)
    {
        var address = new Dnp3PortableAddress(kind, checked((ushort)index));

        Assert.Equal(expected, address.ToString());
        Assert.True(Dnp3PortableAddress.TryParse(expected, out var parsed));
        Assert.Equal(address, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" dnp3:binaryInput:1")]
    [InlineData("DNP3:binaryInput:1")]
    [InlineData("dnp3:BinaryInput:1")]
    [InlineData("dnp3:binaryInput:01")]
    [InlineData("dnp3:binaryInput:+1")]
    [InlineData("dnp3:unknown:1")]
    [InlineData("dnp3:binaryInput:65536")]
    [InlineData("dnp3:binaryInput")]
    [InlineData("dnp3:binaryInput:1:extra")]
    public void PortableAddress_RejectsNonCanonicalOrUnknownText(string value)
    {
        Assert.False(Dnp3PortableAddress.TryParse(value, out _));
        Assert.Throws<FormatException>(() => Dnp3PortableAddress.Parse(value));
    }

    [Fact]
    public void Descriptor_AdvertisesOnlyImplementedDriverSdkSurface()
    {
        var descriptor = Dnp3DriverDescriptorProvider.SharedDescriptor;

        Assert.Equal("dnp3.master", descriptor.DriverType);
        Assert.Equal(1, descriptor.DriverContractVersion);
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Read));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Write));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Subscribe));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Diagnostics));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.SourceTimestamp));
        Assert.Equal(DriverEngineeringCapabilities.None, descriptor.EngineeringCapabilities);
        Assert.Equal(DriverAcquisitionMode.Hybrid, Assert.Single(descriptor.AcquisitionModes));
        Assert.False(descriptor.SupportsSharedTransportInfrastructure);
        Assert.Equal("elitescada.driver.dnp3.master", descriptor.ConfigurationSchema.SchemaId);
        Assert.Equal(1, descriptor.ConfigurationSchema.SchemaVersion);

        Assert.Equal(
            descriptor.ConfigurationSchema.DataSourceFields.Count,
            descriptor.ConfigurationSchema.DataSourceFields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            descriptor.ConfigurationSchema.TagBindingFields.Count,
            descriptor.ConfigurationSchema.TagBindingFields.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count());

        var transport = Assert.Single(descriptor.ConfigurationSchema.DataSourceFields, field => field.Key == "transport");
        Assert.Equal("tcp", transport.DefaultValue);
        Assert.Equal(new[] { "tcp" }, transport.AllowedValues);
        Assert.DoesNotContain(descriptor.ConfigurationSchema.DataSourceFields, field => field.Key.Contains("tls", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(descriptor.ConfigurationSchema.DataSourceFields, field => field.ValueKind is DriverConfigurationValueKind.SecretReference or DriverConfigurationValueKind.CertificateReference);
    }

    [Fact]
    public void Descriptor_KeepsPointIdentitySeparateFromCommandConfiguration()
    {
        var fields = Dnp3DriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.TagBindingFields.ToDictionary(field => field.Key);

        Assert.True(fields["pointKind"].Required);
        Assert.True(fields["index"].Required);
        Assert.Equal(0d, fields["index"].Minimum);
        Assert.Equal((double)ushort.MaxValue, fields["index"].Maximum);
        Assert.Contains("doubleBitBinaryInput", fields["pointKind"].AllowedValues!);
        Assert.Contains("binaryOutputStatus", fields["pointKind"].AllowedValues!);

        Assert.False(fields["writable"].Required);
        Assert.Equal("false", fields["writable"].DefaultValue);
        Assert.Equal(new[] { "selectBeforeOperate", "directOperate" }, fields["commandMode"].AllowedValues);
        Assert.DoesNotContain("directOperateNoResponse", fields["commandMode"].AllowedValues!);
        Assert.Equal(new[] { "int32", "int16", "float32", "float64" }, fields["analogCommandVariation"].AllowedValues);
    }
}
