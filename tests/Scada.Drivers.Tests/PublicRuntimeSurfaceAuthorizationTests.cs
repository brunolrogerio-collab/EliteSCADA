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
