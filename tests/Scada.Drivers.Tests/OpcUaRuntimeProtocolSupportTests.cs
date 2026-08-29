using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaRuntimeProtocolSupportTests
{
    [Fact]
    public void ResolveSessionNodeId_RemapsPersistedNamespaceIndexUsingNamespaceUri()
    {
        var identity = new OpcUaNodeIdentity("ns=2;s=Motor.Speed", "urn:machine:model");

        var resolved = OpcUaRuntimeProtocolSupport.ResolveSessionNodeId(
            identity,
            uri => uri == "urn:machine:model" ? 7 : -1);

        Assert.Equal("ns=7;s=Motor.Speed", resolved);
    }

    [Fact]
    public void ResolveSessionNodeId_NeverCastsMissingNamespaceMinusOneToUInt16()
    {
        var identity = new OpcUaNodeIdentity("ns=2;i=42", "urn:missing");

        var error = Assert.Throws<InvalidOperationException>(() =>
            OpcUaRuntimeProtocolSupport.ResolveSessionNodeId(identity, _ => -1));

        Assert.Contains("not present", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveSessionNodeId_LeavesIndexBasedAddressUntouchedWithoutNamespaceUri()
    {
        var identity = new OpcUaNodeIdentity("ns=3;s=Legacy.Address");
        var resolverCalled = false;

        var resolved = OpcUaRuntimeProtocolSupport.ResolveSessionNodeId(
            identity,
            _ =>
            {
                resolverCalled = true;
                return 9;
            });

        Assert.Equal("ns=3;s=Legacy.Address", resolved);
        Assert.False(resolverCalled);
    }

    [Fact]
    public void CertificatePin_IsExactSha256OfServerCertificateBytes()
    {
        var certificateBytes = new byte[] { 1, 2, 3, 4, 5 };
        var pin = OpcUaRuntimeProtocolSupport.ComputeCertificateSha256(certificateBytes);

        Assert.Equal(64, pin.Length);
        Assert.True(OpcUaRuntimeProtocolSupport.CertificateMatchesSha256Pin(certificateBytes, pin));
        Assert.False(OpcUaRuntimeProtocolSupport.CertificateMatchesSha256Pin(new byte[] { 1, 2, 3, 4, 6 }, pin));
    }

    [Theory]
    [InlineData(0x00000000u, TagQuality.Good)]
    [InlineData(0x40000000u, TagQuality.Uncertain)]
    [InlineData(0x80000000u, TagQuality.Bad)]
    [InlineData(0xC0000000u, TagQuality.Bad)]
    public void MapStatusCode_UsesOpcUaSeverityBits(uint statusCode, TagQuality expected)
    {
        Assert.Equal(expected, OpcUaRuntimeProtocolSupport.MapStatusCode(statusCode));
    }

    [Fact]
    public void NormalizeProtocolTimestamp_PreservesUtcAndTreatsDefaultAsAbsent()
    {
        var timestamp = new DateTime(2026, 8, 29, 20, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTimeOffset(timestamp), OpcUaRuntimeProtocolSupport.NormalizeProtocolTimestamp(timestamp));
        Assert.Null(OpcUaRuntimeProtocolSupport.NormalizeProtocolTimestamp(default));
    }
}
