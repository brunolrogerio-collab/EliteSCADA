using Scada.Security.Authorization;

namespace Scada.Security.Tests;

public sealed class AuthorityPolicyContractTests
{
    [Fact]
    public void ContractPublishesStableVersionedCapabilityIds()
    {
        Assert.Equal("elitescada.authority-policy", AuthorityPolicyContract.Schema);
        Assert.Equal(1, AuthorityPolicyContract.SchemaVersion);
        Assert.Equal("CommandExecute", AuthorityPolicyContract.GetCapabilityId(SecurityCapability.CommandExecute));
        Assert.Equal("ProcessValueWrite", AuthorityPolicyContract.GetCapabilityId(SecurityCapability.ProcessValueWrite));
        Assert.Equal("processValueWrite", AuthorityPolicyContract.GetEngineeringWireId(SecurityCapability.ProcessValueWrite));
        Assert.Contains("EngineeringView", AuthorityPolicyContract.CapabilityIds);
        Assert.Contains("HighAvailabilityObserve", AuthorityPolicyContract.CapabilityIds);
        Assert.Equal(
            Enum.GetValues<SecurityCapability>().Length,
            AuthorityPolicyContract.CapabilityIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ExistingOrdinalsArePinnedAndUnknownIdsFailClosed()
    {
        Assert.Equal(0, (int)SecurityCapability.View);
        Assert.Equal(8, (int)SecurityCapability.EngineeringModify);
        Assert.Equal(10, (int)SecurityCapability.SystemAdmin);
        Assert.Equal(11, (int)SecurityCapability.EngineeringView);

        Assert.True(AuthorityPolicyContract.TryParseCapabilityId(
            "ProcessValueWrite",
            out var capability));
        Assert.Equal(SecurityCapability.ProcessValueWrite, capability);
        Assert.False(AuthorityPolicyContract.TryParseCapabilityId("FutureUnknownCapability", out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuthorityPolicyContract.GetCapabilityId((SecurityCapability)999));
    }
}
