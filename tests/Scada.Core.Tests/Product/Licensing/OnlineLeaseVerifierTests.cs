using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Core.Product.Licensing;

namespace Scada.Core.Tests.Product.Licensing;

public sealed class OnlineLeaseVerifierTests(OnlineLeaseTestKeys keys) : IClassFixture<OnlineLeaseTestKeys>
{
    private static readonly DateTimeOffset Issued = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly OnlineLeaseBinding Binding = new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        Guid.Parse("30000000-0000-0000-0000-000000000003"),
        Guid.Parse("40000000-0000-0000-0000-000000000004"), new string('a', 64));

    // Same string-ID/string-tier wire types and Web serializer as Portal PR #778's OnlineLeaseClaims.
    private sealed record PortalClaims(
        int SchemaVersion, string LicenseId, string OrganizationId, string ApplicationId, string ActivationId,
        string MachineFingerprint, string Tier, int ViewOnlySeats, int InteractiveSeats, bool HaRuntime,
        bool AssetProcessIntelligence, DateTimeOffset IssuedAtUtc, DateTimeOffset NotAfterUtc, string KeyId);

    private static JsonObject Claims() => JsonSerializer.SerializeToNode(new PortalClaims(
        1, Binding.LicenseId.ToString("D"), Binding.OrganizationId.ToString("D"),
        Binding.ApplicationId.ToString("D"), Binding.ActivationId.ToString("D"), Binding.MachineFingerprint,
        "Tags1000", 7, 3, true, true, Issued, Issued.AddHours(72), ProductLicenseTrustAnchors.OnlineLeaseKeyId),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();

    private string Sign(string json, RSA? key = null, RSASignaturePadding? padding = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var signature = (key ?? keys.Online).SignData(bytes, HashAlgorithmName.SHA256, padding ?? RSASignaturePadding.Pss);
        return $"ESLEASE1.{Encode(bytes)}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private OnlineLeaseVerificationResult Verify(string? code, DateTimeOffset? now = null, OnlineLeaseBinding? binding = null) =>
        OnlineLeaseVerifier.VerifyWithKey(code, binding ?? Binding, now ?? Issued.AddHours(1), keys.Online);

    private static void AssertInvalid(OnlineLeaseVerificationResult result)
    {
        Assert.Equal(OnlineLeaseVerificationStatus.Invalid, result.Status);
        Assert.Null(result.Claims);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
    }

    [Fact]
    public void PortalWireContract_VerifiesRawBytesAndExposesTypedClaims()
    {
        var json = Claims().ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var result = Verify(Sign(json));
        Assert.Equal(OnlineLeaseVerificationStatus.Valid, result.Status);
        Assert.Null(result.Diagnostic);
        var claims = Assert.IsType<OnlineLeaseClaims>(result.Claims);
        Assert.Equal(Binding.LicenseId, claims.LicenseId);
        Assert.Equal(Binding.OrganizationId, claims.OrganizationId);
        Assert.Equal(Binding.ApplicationId, claims.ApplicationId);
        Assert.Equal(Binding.ActivationId, claims.ActivationId);
        Assert.Equal(Binding.MachineFingerprint, claims.MachineFingerprint);
        Assert.Equal(LicenseTier.Tags1000, claims.Tier);
        Assert.Equal(7, claims.ViewOnlySeats);
        Assert.Equal(3, claims.InteractiveSeats);
        Assert.True(claims.HaRuntime);
        Assert.True(claims.AssetProcessIntelligence);
        Assert.Equal(Issued, claims.IssuedAtUtc);
        Assert.Equal(Issued.AddHours(72), claims.NotAfterUtc);
        Assert.Equal(ProductLicenseTrustAnchors.OnlineLeaseKeyId, claims.KeyId);
    }

    [Theory]
    [InlineData("Tags500", LicenseTier.Tags500)]
    [InlineData("Tags1000", LicenseTier.Tags1000)]
    [InlineData("Tags1500", LicenseTier.Tags1500)]
    [InlineData("Tags3000", LicenseTier.Tags3000)]
    [InlineData("Tags5000", LicenseTier.Tags5000)]
    [InlineData("Unlimited", LicenseTier.Unlimited)]
    public void AllContractTiersAreRecognized(string tier, LicenseTier expectedTier)
    {
        var json = Claims();
        json["tier"] = tier;
        Assert.Equal(expectedTier, Verify(Sign(json.ToJsonString())).Claims?.Tier);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(int.MaxValue, true)]
    public void EffectiveTotalsAndFeatureFlagsAreNotInferred(int seats, bool enabled)
    {
        var json = Claims();
        json["viewOnlySeats"] = seats;
        json["interactiveSeats"] = seats;
        json["haRuntime"] = enabled;
        json["assetProcessIntelligence"] = enabled;
        var result = Verify(Sign(json.ToJsonString()));
        Assert.Equal(OnlineLeaseVerificationStatus.Valid, result.Status);
        Assert.Equal(seats, result.Claims?.ViewOnlySeats);
        Assert.Equal(seats, result.Claims?.InteractiveSeats);
        Assert.Equal(enabled, result.Claims?.HaRuntime);
        Assert.Equal(enabled, result.Claims?.AssetProcessIntelligence);
    }

    [Theory]
    [InlineData(-1, OnlineLeaseVerificationStatus.Invalid)]
    [InlineData(0, OnlineLeaseVerificationStatus.Valid)]
    [InlineData(2591999999999L, OnlineLeaseVerificationStatus.Valid)]
    [InlineData(2592000000000L, OnlineLeaseVerificationStatus.Expired)]
    [InlineData(2592000000001L, OnlineLeaseVerificationStatus.Expired)]
    public void TimeWindowIsInclusiveAtIssueAndExclusiveAtExpiry(long ticks, OnlineLeaseVerificationStatus status)
    {
        var result = Verify(Sign(Claims().ToJsonString()), Issued.AddTicks(ticks));
        Assert.Equal(status, result.Status);
        if (status != OnlineLeaseVerificationStatus.Valid) Assert.Null(result.Claims);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2592000000001L)]
    public void NonPositiveOrExcessiveLifetimeIsInvalidEvenAfterExpiry(long ticks)
    {
        var json = Claims();
        json["notAfterUtc"] = JsonValue.Create(Issued.AddTicks(ticks));
        AssertInvalid(Verify(Sign(json.ToJsonString()), Issued.AddDays(5)));
    }

    [Fact]
    public void ShorterLeaseLimitedByCommercialExpiryIsAccepted()
    {
        var json = Claims();
        json["notAfterUtc"] = JsonValue.Create(Issued.AddMinutes(2));
        Assert.Equal(OnlineLeaseVerificationStatus.Valid, Verify(Sign(json.ToJsonString()), Issued.AddMinutes(1)).Status);
    }

    [Theory]
    [InlineData("licenseId")]
    [InlineData("organizationId")]
    [InlineData("applicationId")]
    [InlineData("activationId")]
    [InlineData("machineFingerprint")]
    public void EveryExpectedIdentityIsBound(string field)
    {
        var json = Claims();
        json[field] = field == "machineFingerprint" ? new string('b', 64) : "99999999-9999-9999-9999-999999999999";
        AssertInvalid(Verify(Sign(json.ToJsonString())));
    }

    [Fact]
    public void InvalidExpectedBindingFailsClosed()
    {
        var code = Sign(Claims().ToJsonString());
        AssertInvalid(Verify(code, binding: Binding with { ActivationId = Guid.Empty }));
        AssertInvalid(Verify(code, binding: Binding with { MachineFingerprint = "" }));
    }

    public static IEnumerable<object[]> FieldNames() => Claims().Select(property => new object[] { property.Key });

    [Theory]
    [MemberData(nameof(FieldNames))]
    public void EveryWireFieldIsRequiredAndDuplicatesAreRejected(string field)
    {
        var json = Claims();
        var duplicate = json.ToJsonString().Insert(1, $"{JsonSerializer.Serialize(field)}:{json[field]!.ToJsonString()},");
        AssertInvalid(Verify(Sign(duplicate)));
        json.Remove(field);
        AssertInvalid(Verify(Sign(json.ToJsonString())));
    }

    [Theory]
    [InlineData("schemaVersion", "\"1\"")]
    [InlineData("schemaVersion", "2")]
    [InlineData("schemaVersion", "1.5")]
    [InlineData("licenseId", "null")]
    [InlineData("licenseId", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("organizationId", "\"not-an-id\"")]
    [InlineData("applicationId", "\"{30000000-0000-0000-0000-000000000003}\"")]
    [InlineData("activationId", "123")]
    [InlineData("machineFingerprint", "null")]
    [InlineData("machineFingerprint", "\"short\"")]
    [InlineData("machineFingerprint", "\"gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg\"")]
    [InlineData("tier", "\"0\"")]
    [InlineData("tier", "0")]
    [InlineData("tier", "\"tags1000\"")]
    [InlineData("tier", "\"Tags10000\"")]
    [InlineData("viewOnlySeats", "-1")]
    [InlineData("interactiveSeats", "-1")]
    [InlineData("viewOnlySeats", "2147483648")]
    [InlineData("interactiveSeats", "1.1")]
    [InlineData("interactiveSeats", "\"3\"")]
    [InlineData("haRuntime", "1")]
    [InlineData("assetProcessIntelligence", "\"true\"")]
    [InlineData("haRuntime", "null")]
    [InlineData("issuedAtUtc", "\"bad-date\"")]
    [InlineData("issuedAtUtc", "\"2026-10-27T12:00:00\"")]
    [InlineData("issuedAtUtc", "\"2026-10-27T15:00:00+03:00\"")]
    [InlineData("notAfterUtc", "null")]
    [InlineData("notAfterUtc", "123")]
    [InlineData("keyId", "null")]
    [InlineData("keyId", "\"elite-prod-2026-01\"")]
    [InlineData("keyId", "\"unapproved-online-key\"")]
    public void StrictClaimTypesAndValues(string field, string value)
    {
        var json = Claims();
        json[field] = JsonNode.Parse(value);
        AssertInvalid(Verify(Sign(json.ToJsonString())));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{")]
    public void InvalidJsonRootIsRejected(string json) => AssertInvalid(Verify(Sign(json)));

    [Fact]
    public void UnknownOrCaseChangedPropertiesAreRejected()
    {
        var json = Claims();
        json["refreshCredential"] = "should-never-be-in-a-lease";
        var result = Verify(Sign(json.ToJsonString()));
        AssertInvalid(result);
        Assert.DoesNotContain("should-never-be-in-a-lease", result.Diagnostic!);
        json.Remove("refreshCredential");
        var code = Sign(json.ToJsonString().Replace("\"tier\"", "\"Tier\"", StringComparison.Ordinal));
        AssertInvalid(Verify(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ESLEASE1.a.b")]
    [InlineData("eslease1.e30.AA")]
    [InlineData("ESLEASE1.e30=.AA")]
    [InlineData("ESLEASE1.e3+.AA")]
    [InlineData("ESLEASE1.e3/.AA")]
    [InlineData("ESLEASE1.e3 .AA")]
    [InlineData("ESLEASE1.e30.AA.extra")]
    public void MalformedEnvelopeIsInvalidRatherThanDemo(string? code) => AssertInvalid(Verify(code));

    [Fact]
    public void BoundedEnvelopePayloadAndSignatureAreEnforced()
    {
        AssertInvalid(Verify(new string('a', OnlineLeaseVerifier.MaximumEnvelopeCharacters + 1)));
        AssertInvalid(Verify(Sign(Claims().ToJsonString() + new string(' ', 8192))));
        var code = Sign(Claims().ToJsonString());
        AssertInvalid(Verify(code[..code.LastIndexOf('.')] + "." + Encode(new byte[385])));
        AssertInvalid(Verify(code[..code.LastIndexOf('.')] + "." + Encode(new byte[383])));
    }

    [Fact]
    public void NonCanonicalBase64UrlUnusedBitsAreRejected()
    {
        var json = Claims().ToJsonString();
        while (Encoding.UTF8.GetByteCount(json) % 3 != 1) json += " ";
        var parts = Sign(json).Split('.');
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var last = alphabet.IndexOf(parts[1][^1]);
        parts[1] = parts[1][..^1] + alphabet[last + 1]; // same decoded bytes, nonzero unused bits
        AssertInvalid(Verify(string.Join('.', parts)));
    }

    [Fact]
    public void TamperingWrongKeyAndWrongPaddingFailBeforeAnyExpiredClassification()
    {
        var json = Claims().ToJsonString();
        var code = Sign(json);
        var parts = code.Split('.');
        parts[1] = Encode(Encoding.UTF8.GetBytes(json.Replace("Tags1000", "Tags5000", StringComparison.Ordinal)));
        AssertInvalid(Verify(string.Join('.', parts)));
        AssertInvalid(Verify(Sign(json, keys.Other), Issued.AddDays(5)));
        AssertInvalid(Verify(Sign(json, padding: RSASignaturePadding.Pkcs1)));
        AssertInvalid(OnlineLeaseVerifier.VerifyWithKey(Sign(json, keys.Small), Binding, Issued, keys.Small));
    }

    [Fact]
    public void ProductionEntryPointDoesNotTrustTheEphemeralTestSigner()
    {
        AssertInvalid(OnlineLeaseVerifier.Verify(Sign(Claims().ToJsonString()), Binding, Issued));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OfflineEnvelopesCannotUseReservedOnlineKeyEvenIfCallerAddsItToDictionary(int version)
    {
        var keyId = ProductLicenseTrustAnchors.OnlineLeaseKeyId;
        var id = Binding.LicenseId.ToString("D");
        var machine = Binding.MachineFingerprint;
        var code = version switch
        {
            1 => EliteScadaLicenseCodec.CreateSignedLicense(new(1, id, machine, LicenseTier.Tags500, Issued, null, keyId), keys.Online),
            2 => EliteScadaLicenseCodec.CreateSignedLicenseV2(new(2, id, machine, LicenseTier.Tags500, Issued, null, keyId, 2, 2, true), keys.Online),
            _ => EliteScadaLicenseCodec.CreateSignedLicenseV3(new(3, id, machine, LicenseTier.Tags500, Issued, null, keyId, 2, 2, true, true), keys.Online)
        };
        var result = EliteScadaLicenseCodec.VerifyLicense(code, machine, new Dictionary<string, RSA> { [keyId] = keys.Online }, Issued);
        Assert.Equal(LicenseState.Invalid, result.State);
        Assert.Null(result.License);
        Assert.Null(result.SessionEntitlements);
        Assert.Null(result.V3Entitlements);
        AssertInvalid(Verify(code));
        AssertInvalid(Verify("ESLEASE1" + code[code.IndexOf('.')..]));
    }

    [Theory]
    [InlineData("ESLIC1")]
    [InlineData("ESLIC2")]
    [InlineData("ESLIC3")]
    public void RelabelledOnlineClaimsCannotBeImportedAsOffline(string prefix)
    {
        var code = Sign(Claims().ToJsonString());
        var result = EliteScadaLicenseCodec.VerifyLicense(prefix + code[code.IndexOf('.')..], Binding.MachineFingerprint,
            new Dictionary<string, RSA> { [ProductLicenseTrustAnchors.OnlineLeaseKeyId] = keys.Online }, Issued);
        Assert.Equal(LicenseState.Invalid, result.State);
        Assert.Null(result.License);
    }
}

public sealed class OnlineLeaseTestKeys : IDisposable
{
    public RSA Online { get; } = RSA.Create(3072);
    public RSA Other { get; } = RSA.Create(3072);
    public RSA Small { get; } = RSA.Create(2048);
    public void Dispose() { Online.Dispose(); Other.Dispose(); Small.Dispose(); }
}
