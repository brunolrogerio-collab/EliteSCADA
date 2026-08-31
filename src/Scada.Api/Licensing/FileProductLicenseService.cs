using System.Security.Cryptography;
using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

public sealed class FileProductLicenseService : IProductLicenseService, IDisposable
{
    private readonly string _licensePath;
    private readonly IReadOnlyDictionary<string, RSA> _publicKeys;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    public FileProductLicenseService(
        IMachineIdentityProvider machineIdentity,
        string licensePath,
        IReadOnlyDictionary<string, RSA> publicKeys,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(machineIdentity);
        if (string.IsNullOrWhiteSpace(licensePath))
            throw new ArgumentException("License file path is required.", nameof(licensePath));
        ArgumentNullException.ThrowIfNull(publicKeys);

        MachineFingerprint = machineIdentity.GetMachineFingerprint();
        MachineRequestCode = EliteScadaLicenseCodec.CreateMachineRequest(MachineFingerprint);
        _licensePath = Path.GetFullPath(licensePath);
        _publicKeys = publicKeys;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string MachineFingerprint { get; }
    public string MachineRequestCode { get; }

    public LicenseVerificationResult CurrentVerification
    {
        get
        {
            lock (_gate)
            {
                if (!File.Exists(_licensePath))
                    return LicenseVerificationResult.Demo();

                string code;
                try
                {
                    code = File.ReadAllText(_licensePath).Trim();
                }
                catch (Exception ex)
                {
                    return LicenseVerificationResult.Invalid($"Installed license could not be read: {ex.Message}");
                }

                return EliteScadaLicenseCodec.VerifyLicense(
                    code,
                    MachineFingerprint,
                    _publicKeys,
                    _timeProvider.GetUtcNow());
            }
        }
    }

    public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
        ProductEntitlementEvaluator.Evaluate(CurrentVerification, projectTagCount);

    public void InstallLicense(string licenseCode)
    {
        if (string.IsNullOrWhiteSpace(licenseCode))
            throw new ArgumentException("License code is required.", nameof(licenseCode));

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_licensePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporary = _licensePath + ".tmp";
            File.WriteAllText(temporary, licenseCode.Trim() + Environment.NewLine);
            File.Move(temporary, _licensePath, overwrite: true);
        }
    }

    public void RemoveLicense()
    {
        lock (_gate)
        {
            if (File.Exists(_licensePath))
                File.Delete(_licensePath);
        }
    }

    public void Dispose()
    {
        foreach (var key in _publicKeys.Values.Distinct())
            key.Dispose();
    }
}

public static class ProductLicensingConfiguration
{
    public static WebApplicationBuilder AddConfiguredProductLicensing(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IMachineIdentityProvider, DefaultMachineIdentityProvider>();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IProductLicenseService>(sp =>
        {
            var configuredPath = builder.Configuration["Licensing:LicenseFile"];
            var licensePath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(AppContext.BaseDirectory, "data", "licensing", "EliteSCADA.license")
                : configuredPath;

            var keys = new Dictionary<string, RSA>(StringComparer.Ordinal);
            foreach (var child in builder.Configuration.GetSection("Licensing:VerificationKeys").GetChildren())
            {
                if (string.IsNullOrWhiteSpace(child.Key) || string.IsNullOrWhiteSpace(child.Value))
                    continue;

                var path = Path.GetFullPath(child.Value);
                if (!File.Exists(path))
                    throw new InvalidOperationException(
                        $"Configured EliteSCADA licensing public key '{child.Key}' was not found at '{path}'.");

                var rsa = RSA.Create();
                rsa.ImportFromPem(File.ReadAllText(path));
                keys.Add(child.Key, rsa);
            }

            return new FileProductLicenseService(
                sp.GetRequiredService<IMachineIdentityProvider>(),
                licensePath,
                keys,
                sp.GetRequiredService<TimeProvider>());
        });
        builder.Services.AddSingleton<IProductRunEntitlementProvider>(sp =>
            sp.GetRequiredService<IProductLicenseService>());
        return builder;
    }
}
