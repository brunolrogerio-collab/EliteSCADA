using Scada.Api.Security;
using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

public static class OnlineLicenseDeviceConfiguration
{
    public static void AddConfiguredOnlineLicenseDevice(this WebApplicationBuilder builder)
    {
        var origin = builder.Configuration["Licensing:Online:PortalOrigin"];
        if (string.IsNullOrWhiteSpace(origin)) return;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new InvalidOperationException("Online licensing requires an HTTPS Portal origin without path or credentials.");
        var configuredPath = builder.Configuration["Licensing:Online:StateDirectory"];
        var statePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "data", "licensing", "online") : Path.GetFullPath(configuredPath);
        builder.Services.AddSingleton(sp => new FileOnlineLicenseStateStore(statePath,
            sp.GetRequiredService<IProductLicenseService>().MachineFingerprint, uri,
            sp.GetRequiredService<IProtectedMaterialAuthority>(), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<OnlineLicenseDeviceClient>();
        builder.Services.AddSingleton<OnlineLicenseDeviceOrchestrator>();
        builder.Services.AddHostedService<OnlineLicenseRenewalHostedService>();
    }
}
