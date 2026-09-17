using System.Security.Cryptography;
using Scada.Api.Licensing;
using Scada.Core.Product.Licensing;

namespace Scada.Drivers.Tests;

public sealed class ProductLicenseCandidateVerificationTests
{
    [Fact]
    public void VerifyCandidate_ValidEslic2_DoesNotMutateInstalledLicense()
    {
        using var trustedPrivateKey = RSA.Create(2048);
        using var trustedPublicKey = RSA.Create();
        trustedPublicKey.ImportSubjectPublicKeyInfo(trustedPrivateKey.ExportSubjectPublicKeyInfo(), out _);
        var machine = MachineFingerprint.HashIdentity("phase-a-candidate-machine");
        var directory = TempDirectory();
        var path = Path.Combine(directory, "EliteSCADA.license");

        try
        {
            using var service = new FileProductLicenseService(
                new FixedMachineIdentityProvider(machine),
                path,
                new Dictionary<string, RSA> { ["test-key"] = trustedPublicKey });

            var installed = CreateV2(machine, "test-key", trustedPrivateKey, DateTimeOffset.UtcNow.AddHours(1));
            service.InstallLicense(installed);
            var before = File.ReadAllBytes(path);

            var candidate = CreateV2(machine, "test-key", trustedPrivateKey, DateTimeOffset.UtcNow.AddHours(2));
            var verification = service.VerifyCandidate(candidate);

            Assert.Equal(LicenseState.Valid, verification.State);
            Assert.Equal(new MachineLicenseV2Entitlements(3, 2, false), verification.SessionEntitlements);
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(LicenseState.Valid, service.CurrentVerification.State);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void VerifyCandidate_InvalidFamilies_DoNotMutateInstalledLicense()
    {
        using var trustedPrivateKey = RSA.Create(2048);
        using var trustedPublicKey = RSA.Create();
        trustedPublicKey.ImportSubjectPublicKeyInfo(trustedPrivateKey.ExportSubjectPublicKeyInfo(), out _);
        using var wrongPrivateKey = RSA.Create(2048);

        var machine = MachineFingerprint.HashIdentity("phase-a-invalid-machine");
        var wrongMachine = MachineFingerprint.HashIdentity("phase-a-other-machine");
        var directory = TempDirectory();
        var path = Path.Combine(directory, "EliteSCADA.license");

        try
        {
            using var service = new FileProductLicenseService(
                new FixedMachineIdentityProvider(machine),
                path,
                new Dictionary<string, RSA> { ["test-key"] = trustedPublicKey });

            var installed = CreateV2(machine, "test-key", trustedPrivateKey, DateTimeOffset.UtcNow.AddHours(1));
            service.InstallLicense(installed);
            var before = File.ReadAllBytes(path);

            var wrongMachineCode = CreateV2(wrongMachine, "test-key", trustedPrivateKey, DateTimeOffset.UtcNow.AddHours(1));
            var wrongKeyCode = CreateV2(machine, "test-key", wrongPrivateKey, DateTimeOffset.UtcNow.AddHours(1));
            var expiredCode = CreateV2(machine, "test-key", trustedPrivateKey, DateTimeOffset.UtcNow.AddMinutes(-1));
            var tamperedCode = installed[..^1] + (installed[^1] == 'A' ? "B" : "A");

            foreach (var candidate in new[] { "not-a-license", tamperedCode, wrongKeyCode, wrongMachineCode, expiredCode })
            {
                var verification = service.VerifyCandidate(candidate);
                Assert.Equal(LicenseState.Invalid, verification.State);
                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.Equal(LicenseState.Valid, service.CurrentVerification.State);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateV2(
        string machine,
        string keyId,
        RSA signingKey,
        DateTimeOffset? notAfterUtc)
    {
        var payload = new EliteScadaLicenseV2Payload(
            EliteScadaLicenseCodec.LicenseV2SchemaVersion,
            Guid.NewGuid().ToString("D"),
            machine,
            LicenseTier.Tags1000,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            notAfterUtc,
            keyId,
            ViewOnlySeats: 3,
            InteractiveSeats: 2,
            HaRuntime: false);
        return EliteScadaLicenseCodec.CreateSignedLicenseV2(payload, signingKey);
    }

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "elitescada-phase-a-license-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedMachineIdentityProvider(string fingerprint) : IMachineIdentityProvider
    {
        public string GetMachineFingerprint() => fingerprint;
    }
}
