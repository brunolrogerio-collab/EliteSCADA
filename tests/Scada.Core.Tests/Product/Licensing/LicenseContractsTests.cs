using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        Assert.Null(result.SessionEntitlements);
    }

    [Fact]
    public void V1SignedLicense_RemainsEslic1AndHasNoInferredSessionEntitlements()
    {
        using var key = RSA.Create(2048);
        var payload = NewLicense(MachineA, LicenseTier.Tags500, "preview-1");

        var code = EliteScadaLicenseCodec.CreateSignedLicense(payload, key);
        var result = EliteScadaLicenseCodec.VerifyLicense(
            code,
            MachineA,
            new Dictionary<string, RSA> { ["preview-1"] = key },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.StartsWith("ESLIC1.", code, StringComparison.Ordinal);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Null(result.SessionEntitlements);
    }

    [Fact]
    public void V2SignedLicense_ValidatesAndExposesExplicitSessionEntitlements()
    {
        using var privateKey = RSA.Create(2048);
        using var publicKey = RSA.Create();
        publicKey.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);
        var payload = NewV2License(MachineA, "v2-key", 7, 3, haRuntime: true);

        var code = EliteScadaLicenseCodec.CreateSignedLicenseV2(payload, privateKey);
        var result = EliteScadaLicenseCodec.VerifyLicense(
            code,
            MachineA,
            new Dictionary<string, RSA> { ["v2-key"] = publicKey },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.StartsWith("ESLIC2.", code, StringComparison.Ordinal);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal(LicenseTier.Tags1000, result.License?.Tier);
        Assert.Equal(new MachineLicenseV2Entitlements(7, 3, true), result.SessionEntitlements);
        Assert.Null(result.V3Entitlements);
        Assert.False(result.HasAssetProcessIntelligence);
    }

    [Fact]
    public void V3SignedLicense_ValidatesExplicitIntelligenceEntitlementWithoutChangingV2()
    {
        using var key = RSA.Create(2048);
        var payload = NewV3License(MachineA, "v3-key", assetProcessIntelligence: true);
        var code = EliteScadaLicenseCodec.CreateSignedLicenseV3(payload, key);

        var result = EliteScadaLicenseCodec.VerifyLicense(
            code,
            MachineA,
            new Dictionary<string, RSA> { ["v3-key"] = key },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.StartsWith("ESLIC3.", code, StringComparison.Ordinal);
        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal(3, result.License?.SchemaVersion);
        Assert.Equal(new MachineLicenseV2Entitlements(4, 2, false), result.SessionEntitlements);
        Assert.Equal(new MachineLicenseV3Entitlements(4, 2, false, true), result.V3Entitlements);
        Assert.True(result.HasAssetProcessIntelligence);
    }

    [Fact]
    public void V1AndV2LicensesNeverInferIntelligenceEntitlement()
    {
        using var key = RSA.Create(2048);
        var v1 = NewLicense(MachineA, LicenseTier.Tags500, "v1-key");
        var v2 = NewV2License(MachineA, "v2-key", 1, 1, haRuntime: false);
        var keys = new Dictionary<string, RSA> { ["v1-key"] = key, ["v2-key"] = key };

        var v1Result = EliteScadaLicenseCodec.VerifyLicense(
            EliteScadaLicenseCodec.CreateSignedLicense(v1, key), MachineA, keys, v1.IssuedAtUtc.AddMinutes(1));
        var v2Result = EliteScadaLicenseCodec.VerifyLicense(
            EliteScadaLicenseCodec.CreateSignedLicenseV2(v2, key), MachineA, keys, v2.IssuedAtUtc.AddMinutes(1));

        Assert.False(v1Result.HasAssetProcessIntelligence);
        Assert.False(v2Result.HasAssetProcessIntelligence);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("assetProcessIntelligence")]
    [InlineData("haRuntime")]
    public void V3RawSignedLicense_MissingRequiredFieldFailsClosed(string omittedProperty)
    {
        using var key = RSA.Create(2048);

        var result = VerifyRawV3(key, RawV3Json(omit: omittedProperty));

        Assert.Equal(LicenseState.Invalid, result.State);
        Assert.False(result.HasAssetProcessIntelligence);
    }

    [Theory]
    [InlineData("assetProcessIntelligence", "\"true\"")]
    [InlineData("assetProcessIntelligence", "0")]
    [InlineData("assetProcessIntelligence", "null")]
    [InlineData("assetProcessIntelligence", "false")]
    public void V3RawSignedLicense_MalformedOrFalseIntelligenceFailsClosedOrRemainsDenied(
        string property,
        string rawValue)
    {
        using var key = RSA.Create(2048);

        var result = VerifyRawV3(key, RawV3Json(replacementProperty: property, replacementValue: rawValue));

        if (rawValue == "false")
        {
            Assert.Equal(LicenseState.Valid, result.State);
            Assert.False(result.HasAssetProcessIntelligence);
        }
        else
        {
            Assert.Equal(LicenseState.Invalid, result.State);
            Assert.False(result.HasAssetProcessIntelligence);
        }
    }

    [Fact]
    public void V3RawSignedLicense_DuplicateUnknownAndUnsupportedSchemaFailClosed()
    {
        using var key = RSA.Create(2048);

        var duplicate = VerifyRawV3(key, RawV3Json(extraProperty: "\"assetProcessIntelligence\":false"));
        var unknown = VerifyRawV3(key, RawV3Json(extraProperty: "\"unrecognized\":true"));
        var unsupportedSchema = VerifyRawV3(key, RawV3Json(replacementProperty: "schemaVersion", replacementValue: "2"));

        Assert.Equal(LicenseState.Invalid, duplicate.State);
        Assert.Equal(LicenseState.Invalid, unknown.State);
        Assert.Equal(LicenseState.Invalid, unsupportedSchema.State);
    }

    [Fact]
    public void V2RawSignedLicense_ExplicitZeroSeatsAndFalseAreValidEffectiveTotals()
    {
        using var key = RSA.Create(2048);

        var result = VerifyRawV2(key, RawV2Json());
        var atTagLimit = ProductEntitlementEvaluator.Evaluate(result, 1000);
        var aboveTagLimit = ProductEntitlementEvaluator.Evaluate(result, 1001);

        Assert.Equal(LicenseState.Valid, result.State);
        Assert.Equal(new MachineLicenseV2Entitlements(0, 0, false), result.SessionEntitlements);
        Assert.True(atTagLimit.Allowed);
        Assert.False(aboveTagLimit.Allowed);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("licenseId")]
    [InlineData("machineFingerprint")]
    [InlineData("tier")]
    [InlineData("issuedAtUtc")]
    [InlineData("keyId")]
    [InlineData("viewOnlySeats")]
    [InlineData("interactiveSeats")]
    [InlineData("haRuntime")]
    public void V2RawSignedLicense_MissingRequiredFieldFailsClosed(string omittedProperty)
    {
        using var key = RSA.Create(2048);

        var result = VerifyRawV2(key, RawV2Json(omit: omittedProperty));

        Assert.Equal(LicenseState.Invalid, result.State);
    }

    [Theory]
    [InlineData("viewOnlySeats", "-1")]
    [InlineData("interactiveSeats", "-1")]
    [InlineData("viewOnlySeats", "\"0\"")]
    [InlineData("interactiveSeats", "\"0\"")]
    [InlineData("haRuntime", "\"false\"")]
    [InlineData("haRuntime", "0")]
    [InlineData("licenseId", "null")]
    [InlineData("machineFingerprint", "null")]
    [InlineData("tier", "null")]
    [InlineData("issuedAtUtc", "null")]
    [InlineData("keyId", "null")]
    [InlineData("schemaVersion", "null")]
    [InlineData("viewOnlySeats", "null")]
    [InlineData("haRuntime", "null")]
    public void V2RawSignedLicense_MalformedRequiredValueFailsClosed(string property, string rawValue)
    {
        using var key = RSA.Create(2048);

        var result = VerifyRawV2(key, RawV2Json(replacementProperty: property, replacementValue: rawValue));

        Assert.Equal(LicenseState.Invalid, result.State);
    }

    [Fact]
    public void V2RawSignedLicense_DuplicateUnknownAndUnsupportedSchemaFailClosed()
    {
        using var key = RSA.Create(2048);

        var duplicate = VerifyRawV2(key, RawV2Json(extraProperty: "\"viewOnlySeats\":0"));
        var unknown = VerifyRawV2(key, RawV2Json(extraProperty: "\"unrecognized\":true"));
        var unsupportedSchema = VerifyRawV2(key, RawV2Json(replacementProperty: "schemaVersion", replacementValue: "3"));

        Assert.Equal(LicenseState.Invalid, duplicate.State);
        Assert.Equal(LicenseState.Invalid, unknown.State);
        Assert.Equal(LicenseState.Invalid, unsupportedSchema.State);
    }

    [Fact]
    public void V2SignedLicense_EntitlementMutationInvalidatesSignature()
    {
        using var privateKey = RSA.Create(2048);
        var payload = NewV2License(MachineA, "v2-key", 2, 1, haRuntime: false);
        var code = EliteScadaLicenseCodec.CreateSignedLicenseV2(payload, privateKey);
        var parts = code.Split('.');
        var decoded = JsonSerializer.Deserialize<EliteScadaLicenseV2Payload>(Base64UrlDecode(parts[1]), JsonOptions)!;
        parts[1] = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(decoded with { InteractiveSeats = 99 }, JsonOptions));

        var result = EliteScadaLicenseCodec.VerifyLicense(
            string.Join('.', parts),
            MachineA,
            new Dictionary<string, RSA> { ["v2-key"] = privateKey },
            payload.IssuedAtUtc.AddMinutes(1));

        Assert.Equal(LicenseState.Invalid, result.State);
        Assert.Contains("signature", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V2SignedLicense_WrongKeyMachineAndExpiryFailClosed()
    {
        using var privateKey = RSA.Create(2048);
        using var wrongKey = RSA.Create(2048);
        var issuedAt = DateTimeOffset.Parse("2026-09-16T10:00:00Z");
        var payload = NewV2License(MachineA, "v2-key", 1, 1, haRuntime: false, issuedAt, issuedAt.AddHours(1));
        var code = EliteScadaLicenseCodec.CreateSignedLicenseV2(payload, privateKey);

        var wrongKeyResult = EliteScadaLicenseCodec.VerifyLicense(
            code, MachineA, new Dictionary<string, RSA> { ["v2-key"] = wrongKey }, issuedAt.AddMinutes(1));
        var wrongMachineResult = EliteScadaLicenseCodec.VerifyLicense(
            code, MachineB, new Dictionary<string, RSA> { ["v2-key"] = privateKey }, issuedAt.AddMinutes(1));
        var expiredResult = EliteScadaLicenseCodec.VerifyLicense(
            code, MachineA, new Dictionary<string, RSA> { ["v2-key"] = privateKey }, issuedAt.AddHours(2));

        Assert.Equal(LicenseState.Invalid, wrongKeyResult.State);
        Assert.Equal(LicenseState.Invalid, wrongMachineResult.State);
        Assert.Equal(LicenseState.Invalid, expiredResult.State);
        Assert.Contains("signature", wrongKeyResult.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("different hardware", wrongMachineResult.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expired", expiredResult.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void V2SignedLicense_RejectsNegativeSeatValues(int viewOnlySeats, int interactiveSeats)
    {
        using var key = RSA.Create(2048);
        var payload = NewV2License(MachineA, "v2-key", viewOnlySeats, interactiveSeats, haRuntime: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => EliteScadaLicenseCodec.CreateSignedLicenseV2(payload, key));
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
        Assert.Null(atLimit.MaximumContinuousRun);
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

    private static EliteScadaLicenseV2Payload NewV2License(
        string machine,
        string keyId,
        int viewOnlySeats,
        int interactiveSeats,
        bool haRuntime,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? notAfter = null) =>
        new(
            EliteScadaLicenseCodec.LicenseV2SchemaVersion,
            Guid.NewGuid().ToString("D"),
            machine,
            LicenseTier.Tags1000,
            issuedAt ?? DateTimeOffset.UtcNow,
            notAfter,
            keyId,
            viewOnlySeats,
            interactiveSeats,
            haRuntime);

    private static EliteScadaLicenseV3Payload NewV3License(
        string machine,
        string keyId,
        bool assetProcessIntelligence,
        DateTimeOffset? issuedAt = null) =>
        new(
            EliteScadaLicenseCodec.LicenseV3SchemaVersion,
            Guid.NewGuid().ToString("D"),
            machine,
            LicenseTier.Tags1000,
            issuedAt ?? DateTimeOffset.UtcNow,
            null,
            keyId,
            4,
            2,
            false,
            assetProcessIntelligence);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static LicenseVerificationResult VerifyRawV2(RSA key, string payloadJson) =>
        EliteScadaLicenseCodec.VerifyLicense(
            SignRawV2(key, payloadJson),
            MachineA,
            new Dictionary<string, RSA> { ["v2-key"] = key },
            DateTimeOffset.Parse("2026-09-16T12:01:00Z"));

    private static LicenseVerificationResult VerifyRawV3(RSA key, string payloadJson) =>
        EliteScadaLicenseCodec.VerifyLicense(
            SignRawV3(key, payloadJson),
            MachineA,
            new Dictionary<string, RSA> { ["v3-key"] = key },
            DateTimeOffset.Parse("2026-09-16T12:01:00Z"));

    private static string SignRawV3(RSA key, string payloadJson)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(payloadJson);
        var signature = key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return $"ESLIC3.{Base64UrlEncode(payload)}.{Base64UrlEncode(signature)}";
    }

    private static string SignRawV2(RSA key, string payloadJson)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(payloadJson);
        var signature = key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return $"ESLIC2.{Base64UrlEncode(payload)}.{Base64UrlEncode(signature)}";
    }

    private static string RawV2Json(
        string? omit = null,
        string? replacementProperty = null,
        string? replacementValue = null,
        string? extraProperty = null)
    {
        var properties = new (string Name, string Value)[]
        {
            ("schemaVersion", "2"),
            ("licenseId", "\"00000000-0000-0000-0000-000000000002\""),
            ("machineFingerprint", $"\"{MachineA}\""),
            ("tier", "\"Tags1000\""),
            ("issuedAtUtc", "\"2026-09-16T12:00:00Z\""),
            ("keyId", "\"v2-key\""),
            ("viewOnlySeats", "0"),
            ("interactiveSeats", "0"),
            ("haRuntime", "false")
        };

        var fields = properties
            .Where(property => !string.Equals(property.Name, omit, StringComparison.Ordinal))
            .Select(property => $"\"{property.Name}\":{(string.Equals(property.Name, replacementProperty, StringComparison.Ordinal) ? replacementValue : property.Value)}")
            .ToList();
        if (extraProperty is not null)
            fields.Add(extraProperty);
        return "{" + string.Join(',', fields) + "}";
    }

    private static string RawV3Json(
        string? omit = null,
        string? replacementProperty = null,
        string? replacementValue = null,
        string? extraProperty = null)
    {
        var properties = new (string Name, string Value)[]
        {
            ("schemaVersion", "3"),
            ("licenseId", "\"00000000-0000-0000-0000-000000000003\""),
            ("machineFingerprint", $"\"{MachineA}\""),
            ("tier", "\"Tags1000\""),
            ("issuedAtUtc", "\"2026-09-16T12:00:00Z\""),
            ("keyId", "\"v3-key\""),
            ("viewOnlySeats", "4"),
            ("interactiveSeats", "2"),
            ("haRuntime", "false"),
            ("assetProcessIntelligence", "true")
        };

        var fields = properties
            .Where(property => !string.Equals(property.Name, omit, StringComparison.Ordinal))
            .Select(property => $"\"{property.Name}\":{(string.Equals(property.Name, replacementProperty, StringComparison.Ordinal) ? replacementValue : property.Value)}")
            .ToList();
        if (extraProperty is not null)
            fields.Add(extraProperty);
        return "{" + string.Join(',', fields) + "}";
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("Invalid Base64Url length.")
        };
        return Convert.FromBase64String(base64);
    }

    private static string MutateBase64Url(string value)
    {
        var chars = value.ToCharArray();
        var index = chars.Length / 2;
        chars[index] = chars[index] == 'A' ? 'B' : 'A';
        return new string(chars);
    }

}
