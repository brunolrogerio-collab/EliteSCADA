namespace Scada.Core.Product.Licensing;

public interface IProductRunEntitlementProvider
{
    RunEntitlementDecision EvaluateRun(int projectTagCount);
}

public interface IProductLicenseService : IProductRunEntitlementProvider
{
    string MachineFingerprint { get; }
    string MachineRequestCode { get; }
    LicenseVerificationResult CurrentVerification { get; }
    void InstallLicense(string licenseCode);
    void RemoveLicense();
}

public sealed class DemoProductRunEntitlementProvider : IProductRunEntitlementProvider
{
    public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
        ProductEntitlementEvaluator.Evaluate(LicenseVerificationResult.Demo(), projectTagCount);
}
