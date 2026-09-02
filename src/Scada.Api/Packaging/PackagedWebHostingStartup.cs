using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

[assembly: HostingStartup(typeof(Scada.Api.Packaging.PackagedWebHostingStartup))]

namespace Scada.Api.Packaging;

/// <summary>
/// Adds the packaged Web UI only when a release distribution places a built
/// wwwroot beside the product host. Source/development execution keeps the
/// existing Vite development-server contract because no packaged index exists.
/// </summary>
public sealed class PackagedWebHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            services.AddTransient<IStartupFilter, PackagedWebStartupFilter>());
    }
}

internal sealed class PackagedWebStartupFilter(IWebHostEnvironment environment) : IStartupFilter
{
    private static readonly PathString[] ReservedPrefixes =
    [
        new("/api"),
        new("/health"),
        new("/openapi"),
        new("/ws")
    ];

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var webRoot = environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");

            var indexPath = Path.Combine(webRoot, "index.html");
            if (!File.Exists(indexPath))
            {
                next(app);
                return;
            }

            environment.WebRootPath = webRoot;
            environment.WebRootFileProvider = new PhysicalFileProvider(webRoot);

            app.Use(async (context, nextMiddleware) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                    return Task.CompletedTask;
                });

                await nextMiddleware();
            });

            app.UseDefaultFiles();
            app.UseStaticFiles();

            next(app);

            app.Run(async context =>
            {
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (ReservedPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var requestPath = context.Request.Path.Value;
                if (!string.IsNullOrEmpty(requestPath) && Path.HasExtension(requestPath))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(indexPath, context.RequestAborted);
            });
        };
    }
}
