using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Scada.Api.Engineering;

namespace Scada.Drivers.Tests;

public sealed class NativeWebHostingTests
{
    [Fact]
    public async Task NativeWeb_ServesArtifactAndSpaWithoutMaskingApiOrMissingAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-native-web-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "<html>native-web-test</html>");
            await File.WriteAllTextAsync(Path.Combine(root, "asset.js"), "console.log('native');");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["InstallationNetwork:WebRootPath"] = root;
            await using var app = builder.Build();
            app.MapConfiguredNativeWeb();
            app.MapGet("/api/test", () => "api-test");
            await app.StartAsync();
            var server = (IServer)app.Services.GetService(typeof(IServer))!;
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var page = await client.GetAsync("/engineering");
            Assert.Contains("native-web-test", await page.Content.ReadAsStringAsync());
            Assert.Equal("same-origin", page.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
            Assert.Equal("require-corp", page.Headers.GetValues("Cross-Origin-Embedder-Policy").Single());
            Assert.Contains("native", await client.GetStringAsync("/asset.js"));
            Assert.Equal("api-test", await client.GetStringAsync("/api/test"));
            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/api/missing")).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/missing.js")).StatusCode);
            await app.StopAsync();
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}