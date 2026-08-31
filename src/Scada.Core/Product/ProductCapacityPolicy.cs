namespace Scada.Core.Product;

public static class ProductLicensePolicy
{
    public const string DemoEditionName = "Demo";
    public const int DemoMaxTags = 200;
    public const int DemoMaxContinuousRuntimeMinutes = 300;

    public const string DemoTagLimitIssueCode = "DEMO_TAG_LIMIT_EXCEEDED";
    public const string LicenseTagLimitIssueCode = "LICENSE_TAG_LIMIT_EXCEEDED";
    public const string InvalidLicenseIssueCode = "LICENSE_INVALID";
    public const string DemoRuntimeExpiredIssueCode = "DEMO_RUNTIME_TIME_EXPIRED";

    public static IReadOnlyCollection<int> LicensedTagTiers { get; } =
        new[] { 500, 1000, 1500, 3000, 5000 };

    public static TimeSpan DemoMaxContinuousRuntime =>
        TimeSpan.FromMinutes(DemoMaxContinuousRuntimeMinutes);

    public static bool IsSupportedLicensedTagTier(int tagCount) =>
        LicensedTagTiers.Contains(tagCount);

    public static string DemoTagLimitMessage(int requestedCount) =>
        $"O limite do modo de demonstração de {DemoMaxTags} TAGs foi atingido. " +
        $"O projeto possui {requestedCount} TAGs. Para executar este projeto, instale uma licença compatível com a quantidade de TAGs.";

    public static string LicensedTagLimitMessage(int requestedCount, int maximumCount) =>
        $"A licença instalada permite até {maximumCount} TAGs, mas o projeto possui {requestedCount} TAGs.";

    public static string InvalidLicenseMessage(string? detail = null) =>
        string.IsNullOrWhiteSpace(detail)
            ? "A licença instalada é inválida para este computador. O runtime não pode ser iniciado."
            : $"A licença instalada é inválida para este computador. O runtime não pode ser iniciado. {detail}";

    public static string DemoRuntimeExpiredMessage() =>
        $"O período contínuo de avaliação de {DemoMaxContinuousRuntimeMinutes} minutos expirou. " +
        "Inicie o runtime novamente para continuar a avaliação.";
}

public enum ProductLicenseMode
{
    Demo,
    Licensed,
    Invalid
}

public sealed record ProductLicenseSnapshot(
    ProductLicenseMode Mode,
    string HardwareRequestCode,
    int? MaxTags,
    bool UnlimitedTags,
    TimeSpan? MaxContinuousRuntime,
    string? LicenseId = null,
    string? Customer = null,
    string? Message = null)
{
    public bool IsRuntimeLicensed => Mode == ProductLicenseMode.Licensed;
}

public sealed record ProductRuntimePermit(
    bool Allowed,
    ProductLicenseSnapshot License,
    int ProjectTagCount,
    string? IssueCode = null,
    string? Message = null)
{
    public TimeSpan? MaxContinuousRuntime => Allowed ? License.MaxContinuousRuntime : null;
}

public sealed record ProductLicenseInstallResult(
    bool Installed,
    ProductLicenseSnapshot License,
    string? Message = null);

public interface IProductLicenseService
{
    ProductLicenseSnapshot Current();
    ProductRuntimePermit EvaluateRuntime(int projectTagCount);
}

public interface IProductLicenseManager : IProductLicenseService
{
    ProductLicenseInstallResult Install(string licenseCode);
    ProductLicenseSnapshot Remove();
}

public interface IHardwareFingerprintProvider
{
    string GetHardwareFingerprint();
    string GetHardwareRequestCode();
}
