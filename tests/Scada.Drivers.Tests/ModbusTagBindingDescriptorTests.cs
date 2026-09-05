using Scada.Drivers.Modbus;

namespace Scada.Drivers.Tests;

public sealed class ModbusTagBindingDescriptorTests
{
    [Fact]
    public void Descriptor_PublishesOnlyRuntimeBackedTagSettings()
    {
        var fields = ModbusTcpDriverDescriptorProvider.SharedDescriptor
            .ConfigurationSchema.TagBindingFields
            .ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(5, fields.Count);
        Assert.Equal(0d, fields["modbus.unitId"].Minimum);
        Assert.Equal(255d, fields["modbus.unitId"].Maximum);
        Assert.Contains("Float64", fields["modbus.valueType"].AllowedValues ?? Array.Empty<string>());
        Assert.Equal(
            new[] { "HighWordFirst", "LowWordFirst" },
            fields["modbus.wordOrder"].AllowedValues);
        Assert.Equal("1", fields["modbus.scale"].DefaultValue);
        Assert.Equal("0", fields["modbus.offset"].DefaultValue);

        Assert.DoesNotContain(fields.Keys, key =>
            key.Contains("bit", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("selector", StringComparison.OrdinalIgnoreCase));
    }
}
