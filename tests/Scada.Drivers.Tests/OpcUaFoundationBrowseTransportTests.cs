using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaFoundationBrowseTransportTests
{
    [Fact]
    public async Task PublicSurface_DoesNotExposeOpcFoundationTypes()
    {
        var publicTypes = typeof(OpcUaFoundationBrowseTransport)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(GetReferencedTypes)
            .Where(type => type is not null)
            .Cast<Type>()
            .Distinct()
            .ToArray();

        Assert.DoesNotContain(
            publicTypes,
            type => type.Namespace?.StartsWith("Opc.Ua", StringComparison.Ordinal) == true);

        await using var transport = new OpcUaFoundationBrowseTransport(new NeverResolvingSecurityProvider());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(601000)]
    public void Constructor_RejectsUnsafeContinuationLifetime(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OpcUaFoundationBrowseTransport(
                new NeverResolvingSecurityProvider(),
                TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(257)]
    public void Constructor_RejectsUnsafeContinuationCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OpcUaFoundationBrowseTransport(
                new NeverResolvingSecurityProvider(),
                maximumActiveContinuations: capacity));
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
            throw new InvalidOperationException("Security material must not be resolved by constructor-only tests.");

        public ValueTask<X509Certificate2> ResolveCertificateAsync(
            string certificateReference,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Security material must not be resolved by constructor-only tests.");
    }
}
