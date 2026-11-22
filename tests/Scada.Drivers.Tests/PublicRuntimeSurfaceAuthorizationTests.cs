using Microsoft.AspNetCore.Http;
using Scada.Api.Security;

namespace Scada.Drivers.Tests;

public sealed class PublicRuntimeSurfaceAuthorizationTests
{
    [Theory]
    [InlineData("GET", "/api/runtime/application", true)]
    [InlineData("GET", "/api/runtime/visual-assets/859b8b76-d8b8-490d-a1aa-c3aa32930ad9/content", true)]
    [InlineData("GET", "/api/auth/effective-capabilities", true)]
    [InlineData("GET", "/api/runtime/reports", false)]
    [InlineData("GET", "/api/runtime/contract", false)]
    [InlineData("GET", "/api/runtime/media-sources/859b8b76-d8b8-490d-a1aa-c3aa32930ad9/info", false)]
    [InlineData("POST", "/api/runtime/application", false)]
    [InlineData("PUT", "/api/runtime/application", false)]
    [InlineData("PATCH", "/api/runtime/application", false)]
    [InlineData("DELETE", "/api/runtime/application", false)]
    [InlineData("HEAD", "/api/runtime/application", false)]
    [InlineData("OPTIONS", "/api/runtime/application", false)]
    [InlineData("GET", "/api/runtime/application/working", false)]
    [InlineData("GET", "/api/runtime/visual-assets/not-a-guid/content", false)]
    public void Public_view_only_surface_allows_only_published_runtime_reads(
        string method,
        string path,
        bool expected)
    {
        var context = Context(method, path);

        Assert.Equal(expected, ApiAuthorizationService.IsPublicRuntimeViewRequest(context));
    }

    [Theory]
    [InlineData("GET", "/api/tags", true)]
    [InlineData("GET", "/api/tags/current", true)]
    [InlineData("GET", "/api/tags/by-path/Area/Pump/Speed", true)]
    [InlineData("GET", "/api/history/859b8b76-d8b8-490d-a1aa-c3aa32930ad9", false)]
    [InlineData("GET", "/api/alarms", false)]
    [InlineData("GET", "/api/alarms/definitions", false)]
    [InlineData("POST", "/api/tags/859b8b76-d8b8-490d-a1aa-c3aa32930ad9/write", false)]
    [InlineData("HEAD", "/api/tags/current", false)]
    [InlineData("GET", "/api/tags/by-path", false)]
    [InlineData("GET", "/api/tags/by-path/", false)]
    public void Public_tag_reads_exclude_history_alarm_and_mutation_routes(
        string method,
        string path,
        bool expected)
    {
        var context = Context(method, path);

        Assert.Equal(expected, ApiAuthorizationService.IsPublicRuntimeTagReadRequest(context));
    }

    private static DefaultHttpContext Context(string method, string path) => new()
    {
        Request = { Method = method, Path = path }
    };
}
