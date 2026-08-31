using System.Security.Cryptography;
using Scada.Core.Product.Licensing;

namespace Scada.Core.Tests.Product.Licensing;

public sealed class LicenseContractsTests
{
    private static readonly string MachineA = MachineFingerprint.HashIdentity("machine-a");
    private static readonly string MachineB = MachineFingerprint.HashIdentity("machine-b");

    [Fact]
    public void MachineRequest_RoundTripsCanonicalFingerprint()
    {
        var code = EliteScadaLicenseCodec.CreateMachineRequest(MachineA);

        var ok = EliteScadaLicenseCodec.TryParseMachineRequest(code, out var request, out var diagnostic);

        Assert.True(ok, diagnostic);
        Assert.NotNull(request);
        Assert.Equal(EliteScadaLicenseCodec.CurrentSchemaVersion, request.SchemaVersion);
        Assert.Equal(MachineA, request.MachineFingerprint);
    }

    [Fact]
    public void SignedLicense_ValidatesForMatchingMachineAndKey()
    {
        using var privateKey = RSA.Create(2048);
        using var publicKey = RSA.Create();
        publicKey.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);
        var payload = NewLicense(MachineA, LicenseTier.Tags1000, "preview-1");
        var code = EliteScadaLicenseCodec.CreateSignedLicense(payload, privateKey);

        var result = EliteScadaLicenseCodec.VerifyLicense(
            code,
            MachineA,
            new Dictionary<string, RSA> { ["preview-1"] = publicKey },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal(LicenseTier.Tags1000, result.License?.Tier);
    }

    [Fact]
    public void SignedLicense_TamperedPayloadFailsClosed()
    {
        using var privateKey = RSA.Create(2048);
        using var publicKey = RSA.Create();
        publicKey.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);
        var payload = NewLicense(MachineA, LicenseTier.Tags500, "preview-1");
        var code = EliteScadaLicenseCodec.CreateSignedLicense(payload, privateKey);
        var parts = code.Split('.');
        parts[1] = MutateBase64Url(parts[1]);

        var result = EliteScadaLicenseCodec.VerifyLicense(
            string.Join('.', parts),
            MachineA,
            new Dictionary<string, RSA> { ["preview-1"] = publicKey },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.Equal(LicenseState.Invalid, result.State);
    }

    [Fact]
    public void SignedLicense_WrongHardwareFailsClosed()
    {
        using var key = RSA.Create(2048);
        var payload = NewLicense(MachineA, LicenseTier.Unlimited, "preview-1");
        var code = EliteScadaLicenseCodec.CreateSignedLicense(payload, key);

        var result = EliteScadaLicenseCodec.VerifyLicense(
            code,
            MachineB,
            new Dictionary<string, RSA> { ["preview-1"] = key },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.Equal(LicenseState.Invalid, result.State);
        Assert.Contains("different hardware", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingLicense_IsDemoAndRunGateIs200Tags()
    {
        var verification = EliteScadaLicenseCodec.VerifyLicense(
            null,
            MachineA,
            new Dictionary<string, RSA>(),
            DateTimeOffset.UtcNow);

        var atLimit = ProductEntitlementEvaluator.Evaluate(verification, 200);
        var aboveLimit = ProductEntitlementEvaluator.Evaluate(verification, 201);

        Assert.Equal(LicenseState.Demo, verification.State);
        Assert.True(atLimit.Allowed);
        Assert.Equal(TimeSpan.FromMinutes(300), atLimit.MaximumContinuousRun);
        Assert.False(aboveLimit.Allowed);
        Assert.Equal(200, aboveLimit.MaximumTags);
    }

    [Theory]
    [InlineData(LicenseTier.Tags500, 500, true)]
    [InlineData(LicenseTier.Tags500, 501, false)]
    [InlineData(LicenseTier.Tags1000, 1000, true)]
    [InlineData(LicenseTier.Tags1500, 1501, false)]
    [InlineData(LicenseTier.Tags3000, 3000, true)]
    [InlineData(LicenseTier.Tags5000, 5001, false)]
    [InlineData(LicenseTier.Unlimited, 100000, true)]
    public void LicensedRun_EnforcesSignedTier(LicenseTier tier, int tagCount, bool expectedAllowed)
    {
        var payload = NewLicense(MachineA, tier, "preview-1");
        var decision = ProductEntitlementEvaluator.Evaluate(LicenseVerificationResult.Valid(payload), tagCount);

        Assert.Equal(expectedAllowed, decision.Allowed);
        Assert.Null(decision.MaximumContinuousRun);
    }

    [Fact]
    public void InvalidInstalledLicense_BlocksRunInsteadOfFallingBackToDemo()
    {
        var decision = ProductEntitlementEvaluator.Evaluate(
            LicenseVerificationResult.Invalid("signature invalid"),
            1);

        Assert.False(decision.Allowed);
        Assert.Equal(LicenseState.Invalid, decision.LicenseState);
        Assert.Null(decision.MaximumContinuousRun);
    }

    [Fact]
    public void DemoRunSession_UsesMonotonicTimeAndFreshRunStartsFreshWindow()
    {
        var clock = new ManualTimeProvider();
        var first = new DemoRunSession(clock);

        clock.Advance(TimeSpan.FromMinutes(299));
        Assert.False(first.IsExpired);
        Assert.Equal(TimeSpan.FromMinutes(1), first.Remaining);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(first.IsExpired);
        Assert.Equal(TimeSpan.Zero, first.Remaining);

        var restarted = new DemoRunSession(clock);
        Assert.False(restarted.IsExpired);
        Assert.Equal(TimeSpan.FromMinutes(300), restarted.Remaining);
    }

    [Fact]
    public void ExpiredLicense_FailsClosed()
    {
        using var key = RSA.Create(2048);
        var issued = DateTimeOffset.Parse("2026-08-31T12:00:00Z");
        var payload = NewLicense(MachineA, LicenseTier.Tags500, "preview-1", issued, issued.AddHours(1));
        var code = EliteScadaLicenseCodec.CreateSignedLicense(payload, key);

        var result = EliteScadaLicenseCodec.VerifyLicense(
            code,
            MachineA,
            new Dictionary<string, RSA> { ["preview-1"] = key },
            issued.AddHours(2));

        Assert.Equal(LicenseState.Invalid, result.State);
        Assert.Contains("expired", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static EliteScadaLicensePayload NewLicense(
        string machine,
        LicenseTier tier,
        string keyId,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? notAfter = null) =>
        new(
            EliteScadaLicenseCodec.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            machine,
            tier,
            issuedAt ?? DateTimeOffset.UtcNow,
            notAfter,
            keyId);

    private static string MutateBase64Url(string value)
    {
        var chars = value.ToCharArray();
        var index = chars.Length / 2;
        chars[index] = chars[index] == 'A' ? 'B' : 'A';
        return new string(chars);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan amount) => _timestamp += amount.Ticks;
    }
}
