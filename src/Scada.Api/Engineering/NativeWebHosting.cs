using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

namespace Scada.Api.Engineering;

public static class NativeWebHosting
{
    // Native release adapters supply the immutable Web artifact directory.
    // Named Web/Api Kestrel endpoints allow independent native access ports.
    public static void MapConfiguredNativeWeb(this WebApplication app)
    {
        var configured = app.Configuration["InstallationNetwork:WebRootPath"];
        if (string.IsNullOrWhiteSpace(configured)) return;
        var root = Path.GetFullPath(configured, app.Environment.ContentRootPath);
        var index = Path.Combine(root, "index.html");
        if (!File.Exists(index))
            throw new InvalidOperationException("The configured native Web artifact has no index.html.");
        var webEndpoint = app.Configuration["Kestrel:Endpoints:Web:Url"];
        int? webPort = null;
        if (!string.IsNullOrWhiteSpace(webEndpoint))
        {
            if (!Uri.TryCreate(webEndpoint, UriKind.Absolute, out var listener) || listener.Port < 1)
                throw new InvalidOperationException("The native Web listener must have a configured port.");
            webPort = listener.Port;
        }
        var provider = new PhysicalFileProvider(root);
        app.Lifetime.ApplicationStopped.Register(provider.Dispose);
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".mjs"] = "text/javascript";
        app.UseWhen(context => webPort is null || context.Connection.LocalPort == webPort, branch => branch.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context => SetBrowserHeaders(context.Context.Response)
        }));
        app.MapFallback(async context =>
        {
            var path = context.Request.Path;
            if ((webPort.HasValue && context.Connection.LocalPort != webPort.Value) || path.StartsWithSegments("/api") || path.StartsWithSegments("/ws") ||
                path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            SetBrowserHeaders(context.Response);
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(index, context.RequestAborted);
        });
    }
    private static void SetBrowserHeaders(HttpResponse response)
    {
        response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    }
}
