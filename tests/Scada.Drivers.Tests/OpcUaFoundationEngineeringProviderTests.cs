using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Scada.Drivers.Abstractions;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaFoundationEngineeringProviderTests
{
    [Fact]
    public async Task Provider_ImplementsEveryAdvertisedEngineeringCapabilitySurface()
    {
        await using var provider = new OpcUaFoundationEngineeringProvider(new NeverResolvingSecurityProvider());

        Assert.IsAssignableFrom<ICommunicationDriverConnectionTester>(provider);
        Assert.IsAssignableFrom<ICommunicationDriverDiscoverySource>(provider);
        Assert.IsAssignableFrom<ICommunicationDriverBrowser>(provider);
        Assert.IsAssignableFrom<ICommunicationDriverReconciler>(provider);

        var capabilities = provider.Descriptor.EngineeringCapabilities;
        Assert.True(capabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(capabilities.HasFlag(DriverEngineeringCapabilities.Discover));
        Assert.True(capabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.True(capabilities.HasFlag(DriverEngineeringCapabilities.Reconcile));
    }

    [Fact]
    public async Task PublicSurface_DoesNotExposeOpcFoundationTypes()
    {
        var publicTypes = typeof(OpcUaFoundationEngineeringProvider)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(GetReferencedTypes)
            .Where(type => type is not null)
            .Cast<Type>()
            .Distinct()
            .ToArray();

        Assert.DoesNotContain(
            publicTypes,
            type => type.Namespace?.StartsWith("Opc.Ua", StringComparison.Ordinal) == true);

        await using var provider = new OpcUaFoundationEngineeringProvider(new NeverResolvingSecurityProvider());
    }

    [Fact]
    public async Task DisposedProvider_RejectsFurtherEngineeringOperations()
    {
        var provider = new OpcUaFoundationEngineeringProvider(new NeverResolvingSecurityProvider());
        await provider.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() =>
            provider.DiscoverAsync(new DriverDiscoveryRequest()).GetAsyncEnumerator());
    }

    private static IEnumerable<Type?> GetReferencedTypes(MemberInfo member)
    {
        switch (member)
        {
            case ConstructorInfo constructor:
                foreach (ParameterInfo parameter in constructor.GetParameters()) yield return parameter.ParameterType;
                break;
            case MethodInfo method:
                yield return method.ReturnType;
                foreach (ParameterInfo parameter in method.GetParameters()) yield return parameter.ParameterType;
                break;
            case PropertyInfo property:
                yield return property.PropertyType;
                break;
            case EventInfo eventInfo:
                yield return eventInfo.EventHandlerType;
                break;
            case FieldInfo field:
                yield return field.FieldType;
                break;
        }
    }

    private sealed class NeverResolvingSecurityProvider : IOpcUaRuntimeSecurityMaterialProvider
    {
        public ValueTask<string> ResolveSecretAsync(
            string secretReference,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Security material must not be resolved during provider composition tests.");

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Security material must not be resolved during provider composition tests.");
    }
}
