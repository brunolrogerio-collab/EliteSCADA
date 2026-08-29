using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaDriverDescriptorTests
{
    [Fact]
    public void Descriptor_AdvertisesOpcUaRuntimeAndEngineeringCapabilities()
    {
        var descriptor = OpcUaDriverDescriptorProvider.Definition;

        Assert.Equal("opc-ua", descriptor.DriverType);
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Read));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Write));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Subscribe));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.SourceTimestamp));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.ServerTimestamp));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Discover));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Reconcile));
        Assert.Contains(DriverAcquisitionMode.Subscription, descriptor.AcquisitionModes);
    }

    [Fact]
    public void Descriptor_UsesReferencesForSecretsAndCertificates()
    {
        var fields = OpcUaDriverDescriptorProvider.Definition.ConfigurationSchema.DataSourceFields
            .ToDictionary(field => field.Key, StringComparer.Ordinal);

        Assert.Equal(DriverConfigurationValueKind.SecretReference, fields["passwordSecretReference"].ValueKind);
        Assert.Equal(DriverConfigurationValueKind.CertificateReference, fields["clientCertificateReference"].ValueKind);
        Assert.Equal(DriverConfigurationValueKind.CertificateReference, fields["userCertificateReference"].ValueKind);
        Assert.DoesNotContain(fields.Keys, key => string.Equals(key, "password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields.Keys, key => string.Equals(key, "privateKey", StringComparison.OrdinalIgnoreCase));
    }
}
