using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>Only persisted Active source identities are playable; never accepts a browser-supplied URL.</summary>
public static class RuntimeMediaSourceApi
{
    private static readonly SemaphoreSlim Streams = new(8, 8);
    private static readonly HttpClient Client = new(new SocketsHttpHandler {
        AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(8),
        MaxConnectionsPerServer = 4
    }) { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRuntimeMediaSourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/runtime/media-sources/{id:guid}/{operation}", async (
            Guid id, string operation, HttpContext context, ScadaRuntimeFacade runtime,
            ApiAuthorizationService security, IEngineeringProjectPersistenceService persistence,
            MediaSourceProtectedCredentialService credentials, CancellationToken cancellationToken) =>
        {
            if (operation is not ("info" or "content")) return Results.NotFound();
            var authorization = await security.CheckRuntimeAsync(context, runtime, SecurityCapability.View, cancellationToken: cancellationToken);
            if (security.AuthenticationEnabled && authorization.FailureResult() is { } denied) return denied;
            var before = runtime.Describe();
            if (string.IsNullOrWhiteSpace(before.ProjectKey) || !before.Revision.HasValue) return Results.Conflict(new { state = "offline" });
            var active = await persistence.LoadActiveAsync(before.ProjectKey, cancellationToken);
            var after = runtime.Describe();
            if (active is null || active.Revision != before.Revision || after.ProjectKey != before.ProjectKey || after.Revision != before.Revision) return Results.Conflict(new { state = "revision-changed" });
            using var document = JsonDocument.Parse(active.EngineeringJson);
            if (!document.RootElement.TryGetProperty("mediaSources", out var collection)) return Results.NotFound();
            var source = collection.Deserialize<MediaSourceEngineeringDto[]>(Json)?.FirstOrDefault(item => item.Id == id && item.Enabled);
            if (source is null) return Results.NotFound();
            if (operation == "info") return Results.Ok(new { source.Id, source.Name, source.Protocol, state = source.Protocol is MediaSourceProtocol.Rtsp or MediaSourceProtocol.Hls ? "unsupported" : "ready" });
            // HLS needs same-origin segment rewriting; RTSP needs a provisioned transcoder. Never leak the endpoint to the browser.
            if (source.Protocol is MediaSourceProtocol.Rtsp or MediaSourceProtocol.Hls)
                return Results.Json(new { state = "unsupported", error = "This source requires a browser-compatible media gateway." }, statusCode: 415);
            if (!Uri.TryCreate(source.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return Results.BadRequest();
            if (!await Streams.WaitAsync(0, cancellationToken)) return Results.Json(new { state = "busy" }, statusCode: 429);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (RangeHeaderValue.TryParse(context.Request.Headers.Range.ToString(), out var range)) request.Headers.Range = range;
                var scope = new ProtectedMaterialScope("project:" + active.ProjectKey, ProtectedMaterialResourceKinds.MediaSource, id.ToString("D"), ProtectedMaterialPurposes.ConnectionCredential);
                if ((await credentials.GetPublicStateAsync(scope, deadline.Token)).Configured)
                {
                    await using var lease = await credentials.ResolveAsync(scope, deadline.Token);
                    var credential = JsonSerializer.Deserialize<MediaSourceCredentialRequest>(lease.Material.Span, Json);
                    if (credential is not null)
                        request.Headers.Authorization = !string.IsNullOrEmpty(credential.BearerToken)
                            ? new AuthenticationHeaderValue("Bearer", credential.BearerToken)
                            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credential.Username + ":" + credential.Password)));
                }
                using var connect = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                connect.CancelAfter(TimeSpan.FromSeconds(15));
                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connect.Token);
                if (!response.IsSuccessStatusCode)
                    return Results.Json(new { state = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "auth" : "offline" }, statusCode: 502);
                var type = response.Content.Headers.ContentType;
                if (type is null || !(type.MediaType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true || type.MediaType is "multipart/x-mixed-replace" or "image/jpeg"))
                    return Results.Json(new { state = "unsupported" }, statusCode: 415);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = type.ToString();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Accel-Buffering"] = "no";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                if (response.Content.Headers.ContentRange is { } contentRange) context.Response.Headers.ContentRange = contentRange.ToString();
                if (response.Content.Headers.ContentLength is { } length) context.Response.ContentLength = length;
                await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                await input.CopyToAsync(context.Response.Body, deadline.Token);
                return Results.Empty;
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or ProtectedMaterialException or IOException)
            {
                if (context.Response.HasStarted) { context.Abort(); return Results.Empty; }
                return Results.Json(new { state = "offline" }, statusCode: 502);
            }
            finally { Streams.Release(); }
        });
        return endpoints;
    }
}
