using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Licensing;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class OnlineLicenseApiEndpointTests
{
    [Fact]
    public async Task PreparationRequiresEngineeringModifyAndFailsClosedWhenDeviceIsNotConfigured()
    {
        var authority = new InMemoryAuthorityPolicyStore([
            new SecurityRoleEngineeringDto(Guid.NewGuid(), "reader", "Reader", Grants: [new(SecurityCapability.EngineeringView)]),
            new SecurityRoleEngineeringDto(Guid.NewGuid(), "editor", "Editor", Grants: [new(SecurityCapability.EngineeringModify)])
        ]);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test", Args = [] });
        builder.Services.AddSingleton<IAuthorityPolicyStore>(authority);
        builder.Services.AddSingleton<IProductLicenseService>(_ => throw new InvalidOperationException("not used by this test"));
        builder.Services.AddSingleton<AssetProcessIntelligenceExecutionGate>(_ => throw new InvalidOperationException("not used by this test"));
        builder.Services.AddSingleton(sp => new ApiAuthorizationService(sp, authority,
            sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()));
        await using var app = builder.Build();
        app.Use(async (context, next) => {
            if (context.Request.Headers.TryGetValue("X-Test-Role", out var role))
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user"), new Claim("role", role.ToString())], "test"));
            await next();
        });
        app.MapOnlineLicenseConfirmationEndpoints();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        async Task<HttpResponseMessage> Send(string? role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/licensing/online/prepare") {
                Content = JsonContent.Create(new { username = "user", password = "fictitious-secret", licenseCode = "SCV-TEST" }) };
            if (role is not null) request.Headers.Add("X-Test-Role", role);
            return await http.SendAsync(request);
        }
        using var anonymous = await Send(null);
        using var reader = await Send("reader");
        using var editor = await Send("editor");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, editor.StatusCode);
        Assert.Contains("no-store", editor.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("fictitious-secret", await editor.Content.ReadAsStringAsync());
    }
}
