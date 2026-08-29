using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;

namespace Scada.Drivers.Tests;

public sealed class Dnp3VendorIsolationTests
{
    [Fact]
    public void CoreDriverAssembly_DoesNotReferenceStepFunctionPackage()
    {
        var references = typeof(Dnp3Driver).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => string.Equals(reference.Name, "dnp3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StepFunctionAdapter_IsSeparateAssemblyAndOwnsVendorDependency()
    {
        var coreAssembly = typeof(Dnp3Driver).Assembly;
        var adapterAssembly = typeof(StepFunctionDnp3MasterSessionFactory).Assembly;

        Assert.NotEqual(coreAssembly, adapterAssembly);
        Assert.Contains(
            adapterAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, "dnp3", StringComparison.OrdinalIgnoreCase));
    }
}
