using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

namespace Scada.Api.Engineering;

public static class NativeWebHosting
{
    // Native release adapters supply the immutable Web artifact directory.
    // The existing ASP.NET/Kestrel endpoint configuration owns the shared Web/API port.
    public static void MapConfiguredNativeWeb(this WebApplication app)
    {
        var configured = app.Configuration["InstallationNetwork:WebRootPath"];
        if (string.IsNullOrWhiteSpace(configured)) return;
        var root = Path.GetFullPath(configured, app.Environment.ContentRootPath);
        var index = Path.Combine(root, "index.html");
        if (!File.Exists(index))
            throw new InvalidOperationException("The configured native Web artifact has no index.html.");
        var provider = new PhysicalFileProvider(root);
        app.Lifetime.ApplicationStopped.Register(provider.Dispose);
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".mjs"] = "text/javascript";
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context => SetBrowserHeaders(context.Context.Response)
        });
        app.MapFallback(async context =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/api") || path.StartsWithSegments("/ws") ||
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
