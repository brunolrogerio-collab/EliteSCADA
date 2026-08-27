using Scada.Api.Security;
using Scada.Core.Tags;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record ServerMemoryRetentionResetRequest(bool ConfirmReset = false);

public static class InternalMemoryApi
{
    public static void MapInternalMemoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/internal-memory/client/definitions", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            CancellationToken cancellationToken) =>
        {
            var sources = runtime.ClientMemorySources();
            if (sources.Count == 0)
                return Results.Ok(Array.Empty<object>());

            var definitions = sources
                .SelectMany(source => source.Tags.Select(tag => tag.Tag))
                .ToArray();
            var access = await security.GetReadableRuntimeTagDefinitionsAsync(
                context,
                runtime,
                definitions,
                cancellationToken);
            var failure = access.FailureResult();
            if (failure is not null) return failure;

            var readableIds = access.Tags.Select(tag => tag.Id).ToHashSet();
            var result = sources
                .Select(source => new
                {
                    source.DataSourceKey,
                    source.Name,
                    tags = source.Tags
                        .Where(tag => readableIds.Contains(tag.Tag.Id))
                        .Select(tag => new
                        {
                            tag.Tag.Id,
                            tag.Tag.Name,
                            tag.Tag.Path,
                            dataType = tag.Tag.DataType.ToString(),
                            tag.Tag.ReadOnly,
                            initialValue = tag.InitialValue.Value
                        })
                        .ToArray()
                })
                .Where(source => source.tags.Length > 0)
                .ToArray();

            return Results.Ok(result);
        });

        app.MapPost("/api/internal-memory/server/{id:guid}/reset-retained", async (
            Guid id,
            ServerMemoryRetentionResetRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken cancellationToken) =>
        {
            if (!request.ConfirmReset)
                return Results.BadRequest(new { error = "Explicit retained-value reset confirmation is required." });

            if (!runtime.IsServerMemoryTag(id) || !runtime.TryGetTag(id, out var tag) || tag is null)
                return Results.NotFound();

            var principal = security.GetPrincipal(context);
            if (security.AuthenticationEnabled)
            {
                var authorization = await security.CheckRuntimeTagAsync(
                    context,
                    runtime,
                    tag,
                    TagAccessOperation.Configure,
                    cancellationToken);
                var failure = authorization.FailureResult();
                if (failure is not null)
                {
                    await audit.RecordAuthorizationDeniedAsync(
                        context,
                        authorization,
                        AuditActions.ServerMemoryRetentionReset,
                        "server-memory-tag",
                        tag.Path,
                        new Dictionary<string, string> { ["tagId"] = tag.Id.ToString() });
                    return failure;
                }

                principal = authorization.Principal;
            }

            try
            {
                await runtime.ResetServerMemoryRetainedValueAsync(id, cancellationToken);
                await audit.RecordAsync(
                    context,
                    principal,
                    AuditActions.ServerMemoryRetentionReset,
                    AuditOutcome.Succeeded,
                    "server-memory-tag",
                    tag.Path,
                    new Dictionary<string, string> { ["tagId"] = tag.Id.ToString() });
                return Results.Ok(new { reset = true, tag.Id, tag.Path });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                await audit.RecordAsync(
                    context,
                    principal,
                    AuditActions.ServerMemoryRetentionReset,
                    AuditOutcome.Failed,
                    "server-memory-tag",
                    tag.Path,
                    new Dictionary<string, string>
                    {
                        ["tagId"] = tag.Id.ToString(),
                        ["errorType"] = ex.GetType().Name
                    });
                throw;
            }
        });
    }
}
