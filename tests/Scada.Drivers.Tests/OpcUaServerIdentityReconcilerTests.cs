using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaServerIdentityReconcilerTests
{
    private const string Basic256Sha256 = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256";

    [Fact]
    public void Reconcile_FirstSecureContactRequiresExplicitApproval()
    {
        var observed = CreateObserved("urn:plant:plc01", "AA:BB:CC");

        var result = OpcUaServerIdentityReconciler.Reconcile(
            new OpcUaServerIdentityExpectation(null, null),
            observed);

        Assert.Equal(OpcUaServerIdentityReconcileStatus.ApprovalRequired, result.Status);
        Assert.False(result.CanProceed);
        Assert.Equal("AABBCC", result.ObservedServerCertificateSha256);
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_SERVER_IDENTITY_APPROVAL_REQUIRED");
    }

    [Fact]
    public void Reconcile_AcceptsMatchingApprovedIdentity()
    {
        var observed = CreateObserved("urn:plant:plc01", "AA-BB-CC");

        var result = OpcUaServerIdentityReconciler.Reconcile(
            new OpcUaServerIdentityExpectation("urn:plant:plc01", "aa bb cc"),
            observed);

        Assert.Equal(OpcUaServerIdentityReconcileStatus.Match, result.Status);
        Assert.True(result.CanProceed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Reconcile_FailsClosedWhenApplicationUriChanges()
    {
        var observed = CreateObserved("urn:plant:replacement", "AABBCC");

        var result = OpcUaServerIdentityReconciler.Reconcile(
            new OpcUaServerIdentityExpectation("urn:plant:plc01", "AABBCC"),
            observed);

        Assert.Equal(OpcUaServerIdentityReconcileStatus.ApplicationIdentityChanged, result.Status);
        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_SERVER_APPLICATION_IDENTITY_CHANGED");
    }

    [Fact]
    public void Reconcile_FailsClosedWhenCertificateChangesEvenIfObservedCertificateIsTrusted()
    {
        var observed = CreateObserved("urn:plant:plc01", "DDEEFF", trusted: true);

        var result = OpcUaServerIdentityReconciler.Reconcile(
            new OpcUaServerIdentityExpectation("urn:plant:plc01", "AABBCC"),
            observed);

        Assert.Equal(OpcUaServerIdentityReconcileStatus.CertificateChanged, result.Status);
        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_SERVER_CERTIFICATE_CHANGED");
    }

    [Fact]
    public void Reconcile_FailsClosedWhenSecureEndpointHasNoCertificate()
    {
        var observed = CreateObserved("urn:plant:plc01", null);

        var result = OpcUaServerIdentityReconciler.Reconcile(
            new OpcUaServerIdentityExpectation("urn:plant:plc01", null),
            observed);

        Assert.Equal(OpcUaServerIdentityReconcileStatus.MissingCertificate, result.Status);
        Assert.False(result.CanProceed);
        Assert.Contains(result.Issues, issue => issue.Code == "OPCUA_SERVER_CERTIFICATE_MISSING");
    }

    [Fact]
    public void FromSettings_ReadsOnlyApprovedIdentityMetadata()
    {
        var expectation = OpcUaServerIdentityReconciler.FromSettings(new Dictionary<string, string>
        {
            ["serverApplicationUri"] = "urn:plant:plc01",
            ["serverCertificateSha256"] = "AA:BB:CC",
            ["passwordSecretReference"] = "secret://opcua/password"
        });

        Assert.Equal("urn:plant:plc01", expectation.ApplicationUri);
        Assert.Equal("AA:BB:CC", expectation.ServerCertificateSha256);
    }

    private static OpcUaEndpointDiscoveryEvidence CreateObserved(
        string? applicationUri,
        string? fingerprint,
        bool? trusted = null) =>
        new(
            EndpointUrl: "opc.tcp://plc01:4840",
            ApplicationUri: applicationUri,
            ApplicationName: "Plant OPC UA",
            ProductUri: "urn:vendor:product",
            TransportProfileUri: "http://opcfoundation.org/UA-Profile/Transport/uatcp-uasc-uabinary",
            SecurityMode: "SignAndEncrypt",
            SecurityPolicyUri: Basic256Sha256,
            UserTokenTypes: ["Anonymous"],
            ServerCertificateThumbprint: fingerprint,
            IsServerCertificateTrusted: trusted);
}
