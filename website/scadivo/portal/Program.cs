using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal;
using Scadivo.Portal.Data;

if (args.Contains("--healthcheck"))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var response = await client.GetAsync("http://127.0.0.1:8080/portal/health");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (TaskCanceledException) { Environment.ExitCode = 1; }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
builder.Services.AddDbContext<PortalDb>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Portal") ?? throw new InvalidOperationException("Portal database is not configured.")));
builder.Services.AddIdentity<PortalUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = false; // The bootstrap owner can be configured before choosing business email.
}).AddEntityFrameworkStores<PortalDb>().AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Secure-scadivo.portal";
    options.Cookie.Path = "/portal";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options => options.AddPolicy("Owner", policy => policy.RequireRole("Owner")));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Secure-scadivo.portal.csrf";
    options.Cookie.Path = "/portal";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddDataProtection().SetApplicationName("SCADIVO.Portal")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Portal:KeyDirectory"] ?? "/app/keys"));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var cidr in (builder.Configuration["Portal:TrustedProxyNetworks"] ?? "10.11.0.0/16").Split(',', StringSplitOptions.RemoveEmptyEntries))
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr.Trim()));
    options.ForwardLimit = 2;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("writes", context => HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
        ? RateLimitPartition.GetNoLimiter("read")
        : RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddScoped<PortalOperations>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UsePathBase("/portal");
app.UseRouting();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "invalid-request-token" });
            return;
        }
    }
    await next();
});
app.MapGet("/health", async (PortalDb db) => await db.Database.CanConnectAsync()
    ? Results.Ok(new { service = "scadivo-portal", status = "ok" }) : Results.StatusCode(503));
app.MapPortalEndpoints();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache" });
app.MapFallback((HttpContext context) => context.Request.Path.StartsWithSegments("/api")
    ? Results.NotFound(new { error = "not-found" })
    : Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html"));

if (!app.Configuration.GetValue<bool>("Portal:SkipDatabaseInitialization"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
    await db.Database.MigrateAsync();
    await PortalSeed.InitializeAsync(scope.ServiceProvider, app.Configuration);
}
await app.RunAsync();

public partial class Program { }
