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
            var webRoot = ResolvePackagedWebRoot(environment, AppContext.BaseDirectory);
            if (webRoot is null)
            {
                next(app);
                return;
            }

            var indexPath = Path.Combine(webRoot, "index.html");
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

    internal static string? ResolvePackagedWebRoot(
        IWebHostEnvironment environment,
        string applicationBaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);

        var candidates = new[]
        {
            Path.Combine(applicationBaseDirectory, "wwwroot"),
            environment.WebRootPath,
            Path.Combine(environment.ContentRootPath, "wwwroot")
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "index.html")))
                return fullPath;
        }

        return null;
    }
}
