using Microsoft.Extensions.Configuration;
using Scada.Api.Engineering;

namespace Scada.Drivers.Tests;

public sealed class InstallationServiceInventoryTests
{
    [Fact]
    public void Read_NativeConfiguration_ReportsPortsWithoutCredentialsOrUriSecrets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Http:Url"] = "http://127.0.0.1:25080",
            ["InstallationNetwork:WebUrl"] = "https://localhost:28443",
            ["MediaSources:GatewayControlUrl"] = "http://user:secret-value@localhost:19997/private?token=secret-value",
            ["ConnectionStrings:EliteScada"] = "Host=localhost;Port=25432;Username=admin;Password=secret-value"
        }).Build();
        var endpoints = InstallationServiceInventoryApi.Read(configuration);
        Assert.Contains(endpoints, endpoint => endpoint.Service == "API" && endpoint.Port == 25080);
        Assert.Contains(endpoints, endpoint => endpoint.Service == "EliteScada" && endpoint.Port == 25432);
        Assert.Contains(endpoints, endpoint => endpoint.Service == "MediaControl" && endpoint.Port == 19997);
        Assert.Contains(endpoints, endpoint => endpoint.Service == "MediaPlayback" && endpoint.Port == null);
        var json = System.Text.Json.JsonSerializer.Serialize(endpoints);
        Assert.DoesNotContain("secret-value", json);
        Assert.DoesNotContain("/private", json);
        Assert.DoesNotContain("Username", json);
    }
}