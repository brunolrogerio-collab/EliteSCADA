using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpEndpointTests
{
    [Fact]
    public async Task StaticHelpCatalogIsAvailableToAnonymousRuntimeVisitors()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test", Args = [] });
        builder.Configuration["Authentication:Enabled"] = "true";
        await using var app = builder.Build();
        app.MapContextualHelpEndpoints();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        using var http = new HttpClient
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };

        using var response = await http.GetAsync("/api/help?locale=pt-BR&topic=runtime.overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("runtime.overview", content, StringComparison.Ordinal);
        Assert.Contains("Runtime", content, StringComparison.OrdinalIgnoreCase);
    }
}
