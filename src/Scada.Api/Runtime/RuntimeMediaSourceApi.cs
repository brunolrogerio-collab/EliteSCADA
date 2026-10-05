using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Persistence;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>Only persisted Active source identities are playable; never accepts a browser-supplied URL.</summary>
public static class RuntimeMediaSourceApi
{
    private static readonly SemaphoreSlim Streams = new(8, 8);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRuntimeMediaSourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/runtime/media-sources/{id:guid}/{operation}", async (
            Guid id, string operation, HttpContext context, ScadaRuntimeFacade runtime,
            ApiAuthorizationService security, [FromServices] IEngineeringProjectPersistenceService? persistence,
            MediaSourceProtectedCredentialService credentials, RuntimeMediaRelay relay, CancellationToken cancellationToken) =>
        {
            if (operation is not ("info" or "content" or "resource" or "probe")) return Results.NotFound();
            var authorization = await security.CheckRuntimeAsync(context, runtime, SecurityCapability.View, cancellationToken: cancellationToken);
            if (security.AuthenticationEnabled && authorization.FailureResult() is { } denied) return denied;
            if (persistence is null) return Results.Json(new { state = "offline" }, statusCode: 503);
            var before = runtime.Describe();
            if (string.IsNullOrWhiteSpace(before.ProjectKey) || !before.Revision.HasValue) return Results.Conflict(new { state = "offline" });
            var active = await persistence.LoadActiveAsync(before.ProjectKey, cancellationToken);
            var after = runtime.Describe();
            if (active is null || active.Revision != before.Revision || after.ProjectKey != before.ProjectKey || after.Revision != before.Revision) return Results.Conflict(new { state = "revision-changed" });
            using var document = JsonDocument.Parse(active.EngineeringJson);
            if (!document.RootElement.TryGetProperty("mediaSources", out var collection)) return Results.NotFound();
            var source = collection.Deserialize<MediaSourceEngineeringDto[]>(Json)?.FirstOrDefault(item => item.Id == id && item.Enabled);
            if (source is null) return Results.NotFound();
            if (operation == "info") return Results.Ok(new { source.Id, source.Name, protocol = source.Protocol == MediaSourceProtocol.Rtsp ? "hls" : source.Protocol.ToString().ToLowerInvariant(), state = source.Protocol == MediaSourceProtocol.Rtsp && !relay.GatewayConfigured ? "unsupported" : "ready" });
            if (!Uri.TryCreate(source.Endpoint, UriKind.Absolute, out var origin) || !string.IsNullOrEmpty(origin.UserInfo)) return Results.BadRequest();
            var ticketScope = active.ProjectKey + ":" + active.Revision + ":" + id.ToString("D");
            if (!await Streams.WaitAsync(0, cancellationToken)) return Results.Json(new { state = "busy" }, statusCode: 429);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            try
            {
                MediaSourceCredentialRequest? credential = null;
                var scope = new ProtectedMaterialScope("project:" + active.ProjectKey, ProtectedMaterialResourceKinds.MediaSource, id.ToString("D"), ProtectedMaterialPurposes.ConnectionCredential);
                if ((await credentials.GetPublicStateAsync(scope, deadline.Token)).Configured)
                {
                    await using var lease = await credentials.ResolveAsync(scope, deadline.Token);
                    credential = JsonSerializer.Deserialize<MediaSourceCredentialRequest>(lease.Material.Span, Json);
                }
                var trustedGateway = source.Protocol == MediaSourceProtocol.Rtsp;
                Uri uri;
                if (operation == "resource") {
                    if (source.Protocol is not (MediaSourceProtocol.Hls or MediaSourceProtocol.Rtsp)) return Results.NotFound();
                    uri = relay.ReadTicket(ticketScope, context.Request.Query["ticket"].ToString());
                    if (trustedGateway) {
                        var gatewayOrigin = new Uri(context.RequestServices.GetRequiredService<IConfiguration>()["MediaSources:GatewayPlaybackUrl"]!);
                        if (uri.Scheme != gatewayOrigin.Scheme || uri.Host != gatewayOrigin.Host || uri.Port != gatewayOrigin.Port) return Results.BadRequest();
                        origin = gatewayOrigin;
                    } else if (uri.Scheme != origin.Scheme || uri.Host != origin.Host || uri.Port != origin.Port) return Results.BadRequest();
                } else if (trustedGateway) {
                    if (!string.IsNullOrEmpty(credential?.BearerToken)) return Results.Json(new { state = "auth", error = "RTSP requires Basic camera credentials." }, statusCode: 415);
                    uri = await relay.PrepareRtspAsync(source, ticketScope, credential?.Username, credential?.Password, deadline.Token);
                    origin = uri;
                } else uri = origin;
                if (uri.Scheme is not ("http" or "https")) return Results.BadRequest();
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (RangeHeaderValue.TryParse(context.Request.Headers.Range.ToString(), out var range)) request.Headers.Range = range;
                if (!trustedGateway && credential is not null)
                    request.Headers.Authorization = !string.IsNullOrEmpty(credential.BearerToken)
                        ? new AuthenticationHeaderValue("Bearer", credential.BearerToken)
                        : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credential.Username + ":" + credential.Password)));
                using var connect = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                connect.CancelAfter(TimeSpan.FromSeconds(15));
                using var response = await relay.SendAsync(request, trustedGateway, connect.Token);
                if (!response.IsSuccessStatusCode)
                    return Results.Json(new { state = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "auth" : "offline" }, statusCode: 502);
                if (operation == "probe") return Results.Ok(new { state = "ready" });
                var type = response.Content.Headers.ContentType;
                var hls = source.Protocol is MediaSourceProtocol.Hls or MediaSourceProtocol.Rtsp;
                var playlist = hls && (uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || type?.MediaType is "application/vnd.apple.mpegurl" or "application/x-mpegURL");
                if (playlist) {
                    var bytes = await RuntimeMediaRelay.ReadBoundedAsync(response.Content, 1024 * 1024, connect.Token);
                    var rewritten = relay.RewritePlaylist(Encoding.UTF8.GetString(bytes), uri, origin, ticketScope, $"/api/runtime/media-sources/{id:D}/resource");
                    context.Response.Headers.CacheControl = "no-store";
                    return Results.Text(rewritten, "application/vnd.apple.mpegurl");
                }
                if (type is null || !(type.MediaType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true || type.MediaType is "multipart/x-mixed-replace" or "image/jpeg" || hls && type.MediaType is "application/octet-stream" or "audio/aac" or "audio/mp4"))
                    return Results.Json(new { state = "unsupported" }, statusCode: 415);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = type.ToString();
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Accel-Buffering"] = "no";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                if (response.Content.Headers.ContentRange is { } contentRange) context.Response.Headers.ContentRange = contentRange.ToString();
                if (hls) {
                    var bytes = await RuntimeMediaRelay.ReadBoundedAsync(response.Content, 32 * 1024 * 1024, connect.Token);
                    await context.Response.Body.WriteAsync(bytes, deadline.Token);
                    return Results.Empty;
                }
                if (response.Content.Headers.ContentLength is { } length) context.Response.ContentLength = length;
                await using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                await input.CopyToAsync(context.Response.Body, deadline.Token);
                return Results.Empty;
            }
            catch (NotSupportedException) { return Results.Json(new { state = "unsupported" }, statusCode: 415); }
            catch (Exception exception) when (exception is CryptographicException or FormatException) { return Results.BadRequest(new { state = "invalid-ticket" }); }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or ProtectedMaterialException or IOException or SocketException)
            {
                if (context.Response.HasStarted) { context.Abort(); return Results.Empty; }
                return Results.Json(new { state = "offline" }, statusCode: 502);
            }
            finally { Streams.Release(); }
        });
        return endpoints;
    }
}
