using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Runtime;
using Scada.Api.Engineering;
using Scada.Api.ProjectPackages;
using Scada.Api.Security;

namespace Scada.Drivers.Tests;

public sealed class MatterWifiPreviewEndpointRegistrationTests
{
    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    [InlineData("Development", false, false)]
    [InlineData("Development", true, true)]
    [InlineData("Test", true, true)]
    public async Task Fabric_key_route_returns_not_found_unless_explicit_isolated_preview_is_enabled(
        string environmentName,
        bool previewSetting,
        bool expectedToBeRegistered)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            Args = []
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EngineeringDriverCatalogApi.MatterWifiPreviewConfigurationKey] = previewSetting.ToString()
        });
        builder.Services.AddSingleton(new LocalIdentityRuntimeOptions(false, false, false, "test"));
        builder.Services.AddSingleton<EngineeringWorkspace>();
        builder.Services.AddSingleton<MatterWifiProtectedMaterialService>(_ => throw new InvalidOperationException("Not resolved during route registration."));
        builder.Services.AddSingleton<ApiAuthorizationService>(_ => throw new InvalidOperationException("Not resolved during route registration."));
        builder.Services.AddSingleton<ApiAuditService>(_ => throw new InvalidOperationException("Not resolved during route registration."));
        await using var app = builder.Build();

        app.MapMatterWifiProtectedMaterialEndpointsIfPreviewEnabled();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient();
            using var response = await client.GetAsync($"{address}/api/engineering/data-sources/{Guid.NewGuid():D}/matter-wifi/fabric-storage-key");

            Assert.Equal(expectedToBeRegistered ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
