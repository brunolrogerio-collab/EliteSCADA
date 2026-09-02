using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Scada.Api.Packaging;

namespace Scada.Drivers.Tests;

public sealed class PackagedWebHostingStartupTests
{
    [Fact]
    public void ResolvePackagedWebRoot_PrefersPayloadBesideExecutableOverWorkingDirectory()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"elitescada-packaged-web-{Guid.NewGuid():N}");
        var applicationRoot = Path.Combine(testRoot, "application");
        var contentRoot = Path.Combine(testRoot, "working-directory");
        var applicationWebRoot = Path.Combine(applicationRoot, "wwwroot");
        var contentWebRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            Directory.CreateDirectory(applicationWebRoot);
            Directory.CreateDirectory(contentWebRoot);
            File.WriteAllText(Path.Combine(applicationWebRoot, "index.html"), "packaged");
            File.WriteAllText(Path.Combine(contentWebRoot, "index.html"), "working-directory");

            var environment = new TestWebHostEnvironment
            {
                ContentRootPath = contentRoot,
                WebRootPath = contentWebRoot
            };

            var resolved = PackagedWebStartupFilter.ResolvePackagedWebRoot(environment, applicationRoot);

            Assert.Equal(Path.GetFullPath(applicationWebRoot), resolved);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Scada.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
}
