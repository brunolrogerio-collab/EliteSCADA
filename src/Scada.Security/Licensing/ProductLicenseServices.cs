using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Scada.Core.Product;

namespace Scada.Security.Licensing;

public sealed class DefaultHardwareFingerprintProvider : IHardwareFingerprintProvider
{
    private readonly Lazy<string> _fingerprint;

    public DefaultHardwareFingerprintProvider()
    {
        _fingerprint = new Lazy<string>(BuildFingerprint, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string GetHardwareFingerprint() => _fingerprint.Value;

    public string GetHardwareRequestCode() =>
        ProductLicenseCryptography.CreateHardwareRequestCode(GetHardwareFingerprint());

    private static string BuildFingerprint()
    {
        var anchors = new SortedSet<string>(StringComparer.Ordinal);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
                var machineGuid = key?.GetValue("MachineGuid") as string;
                Add(anchors, "windows-machine-guid", machineGuid);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                // Network hardware remains a fallback if the registry anchor is unavailable.
            }
        }
        else
        {
            TryAddFileAnchor(anchors, "machine-id", "/etc/machine-id");
            TryAddFileAnchor(anchors, "dbus-machine-id", "/var/lib/dbus/machine-id");
        }

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(IsStableNetworkCandidate)
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var address = nic.GetPhysicalAddress().GetAddressBytes();
                if (address.Length == 0 || address.All(value => value == 0))
                    continue;
                Add(anchors, "mac", Convert.ToHexString(address));
            }
        }
        catch (NetworkInformationException)
        {
            // Other machine anchors may still be sufficient.
        }

        Add(anchors, "os-architecture", RuntimeInformation.OSArchitecture.ToString());

        if (anchors.Count == 1)
            throw new InvalidOperationException("No stable machine identity anchor is available for licensing.");

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        foreach (var anchor in anchors)
        {
            var bytes = Encoding.UTF8.GetBytes(anchor);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        writer.Flush();

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsStableNetworkCandidate(NetworkInterface nic) =>
        nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.Wireless80211;

    private static void TryAddFileAnchor(SortedSet<string> anchors, string name, string path)
    {
        try
        {
            if (File.Exists(path)) Add(anchors, name, File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort machine identity fallback for non-Windows development hosts.
        }
    }

    private static void Add(SortedSet<string> anchors, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        anchors.Add($"{name}:{value.Trim().ToUpperInvariant()}");
    }
}

public sealed class FileProductLicenseService : IProductLicenseManager
{
    private readonly IHardwareFingerprintProvider _hardware;
    private readonly string _licensePath;
    private readonly string? _publicKeyPem;

    public FileProductLicenseService(
        IHardwareFingerprintProvider hardware,
        string licensePath,
        string? publicKeyPem)
    {
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        if (string.IsNullOrWhiteSpace(licensePath))
            throw new ArgumentException("License path is required.", nameof(licensePath));
        _licensePath = Path.GetFullPath(licensePath);
        _publicKeyPem = string.IsNullOrWhiteSpace(publicKeyPem) ? null : publicKeyPem.Trim();
    }

    public ProductLicenseSnapshot Current()
    {
        var requestCode = _hardware.GetHardwareRequestCode();
        if (!File.Exists(_licensePath))
        {
            return new ProductLicenseSnapshot(
                ProductLicenseMode.Demo,
                requestCode,
                ProductLicensePolicy.DemoMaxTags,
                false,
                ProductLicensePolicy.DemoMaxContinuousRuntime,
                Message: $"Modo de demonstração: até {ProductLicensePolicy.DemoMaxTags} TAGs e {ProductLicensePolicy.DemoMaxContinuousRuntimeMinutes} minutos contínuos por execução.");
        }

        string code;
        try
        {
            code = File.ReadAllText(_licensePath).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Invalid(requestCode, "O arquivo de licença não pôde ser lido.");
        }

        var validation = ProductLicenseCryptography.ValidateLicense(
            code,
            _publicKeyPem ?? string.Empty,
            _hardware.GetHardwareFingerprint());
        if (!validation.Valid || validation.License is null)
            return Invalid(requestCode, validation.Error);

        var license = validation.License;
        return new ProductLicenseSnapshot(
            ProductLicenseMode.Licensed,
            requestCode,
            license.MaxTags,
            license.UnlimitedTags,
            null,
            license.LicenseId,
            license.Customer,
            license.UnlimitedTags
                ? "Licença válida: TAGs ilimitadas e runtime contínuo sem limite de avaliação."
                : $"Licença válida: até {license.MaxTags} TAGs e runtime contínuo sem limite de avaliação.");
    }

    public ProductRuntimePermit EvaluateRuntime(int projectTagCount)
    {
        if (projectTagCount < 0)
            throw new ArgumentOutOfRangeException(nameof(projectTagCount));

        var license = Current();
        if (license.Mode == ProductLicenseMode.Invalid)
        {
            return new ProductRuntimePermit(
                false,
                license,
                projectTagCount,
                ProductLicensePolicy.InvalidLicenseIssueCode,
                license.Message ?? ProductLicensePolicy.InvalidLicenseMessage());
        }

        if (license.Mode == ProductLicenseMode.Demo)
        {
            if (projectTagCount > ProductLicensePolicy.DemoMaxTags)
            {
                return new ProductRuntimePermit(
                    false,
                    license,
                    projectTagCount,
                    ProductLicensePolicy.DemoTagLimitIssueCode,
                    ProductLicensePolicy.DemoTagLimitMessage(projectTagCount));
            }

            return new ProductRuntimePermit(true, license, projectTagCount);
        }

        if (!license.UnlimitedTags && (!license.MaxTags.HasValue || projectTagCount > license.MaxTags.Value))
        {
            return new ProductRuntimePermit(
                false,
                license,
                projectTagCount,
                ProductLicensePolicy.LicenseTagLimitIssueCode,
                ProductLicensePolicy.LicensedTagLimitMessage(projectTagCount, license.MaxTags ?? 0));
        }

        return new ProductRuntimePermit(true, license, projectTagCount);
    }

    public ProductLicenseInstallResult Install(string licenseCode)
    {
        var requestCode = _hardware.GetHardwareRequestCode();
        var validation = ProductLicenseCryptography.ValidateLicense(
            licenseCode,
            _publicKeyPem ?? string.Empty,
            _hardware.GetHardwareFingerprint());
        if (!validation.Valid)
        {
            var invalid = Invalid(requestCode, validation.Error);
            return new ProductLicenseInstallResult(false, invalid, invalid.Message);
        }

        var directory = Path.GetDirectoryName(_licensePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var temporaryPath = _licensePath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, licenseCode.Trim(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, _licensePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        var installed = Current();
        return new ProductLicenseInstallResult(
            installed.Mode == ProductLicenseMode.Licensed,
            installed,
            installed.Message);
    }

    public ProductLicenseSnapshot Remove()
    {
        if (File.Exists(_licensePath)) File.Delete(_licensePath);
        return Current();
    }

    private static ProductLicenseSnapshot Invalid(string requestCode, string? detail) =>
        new(
            ProductLicenseMode.Invalid,
            requestCode,
            null,
            false,
            null,
            Message: ProductLicensePolicy.InvalidLicenseMessage(detail));
}
