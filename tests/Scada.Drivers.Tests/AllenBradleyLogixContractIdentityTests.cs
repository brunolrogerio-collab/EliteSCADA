using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyLogixContractIdentityTests
{
    [Fact]
    public void DescriptorAndPortableAddress_UseStableDriverOwnedContractIdentity()
    {
        var descriptor = new AllenBradleyLogixEngineeringAdapter().Descriptor;

        Assert.Equal(AllenBradleyLogixContractIdentity.DriverType, descriptor.DriverType);
        Assert.Equal(AllenBradleyLogixContractIdentity.DriverContractVersion, descriptor.DriverContractVersion);
        Assert.Equal(AllenBradleyLogixContractIdentity.BindingSchemaId, descriptor.ConfigurationSchema.SchemaId);
        Assert.Equal(AllenBradleyLogixContractIdentity.BindingSchemaVersion, descriptor.ConfigurationSchema.SchemaVersion);
        Assert.Equal(LogixPortableAddress.Prefix, AllenBradleyLogixContractIdentity.PortableAddressPrefix);
    }
}
