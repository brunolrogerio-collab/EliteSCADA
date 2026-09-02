using Scada.Api.Runtime;

namespace Scada.Api.Persistence;

/// <summary>
/// Applies the canonical Engineering Workspace compare-and-swap boundary to persisted
/// revision Apply operations. Apply replaces mutable Working state, so it must not race
/// with another Engineering mutation or silently overwrite a newer caller-observed state.
/// </summary>
public sealed class EngineeringPersistenceApplyConcurrencyFilter(
    EngineeringWorkspace workspace) : IEndpointFilter
{
    public const string WorkspaceVersionHeader = "x-elitescada-workspace-version";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var context = invocationContext.HttpContext;
        if (!IsApplyRequest(context.Request))
            return await next(invocationContext);

        if (!TryReadExpectedVersion(context.Request, out var expectedChangeVersion))
        {
            return Results.BadRequest(new
            {
                error = $"Header '{WorkspaceVersionHeader}' with a non-negative integer Workspace version is required."
            });
        }

        try
        {
            return await EngineeringPersistenceApplyGuard.ExecuteAsync(
                workspace,
                expectedChangeVersion,
                async _ => await next(invocationContext),
                context.RequestAborted);
        }
        catch (EngineeringWorkspaceVersionConflictException conflict)
        {
            return Results.Conflict(new
            {
                error = "Engineering Workspace changed before Apply. Reload before trying again.",
                expectedChangeVersion = conflict.ExpectedChangeVersion,
                currentChangeVersion = conflict.CurrentChangeVersion
            });
        }
    }

    private static bool IsApplyRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        request.Path.Value?.EndsWith("/apply", StringComparison.OrdinalIgnoreCase) == true;

    private static bool TryReadExpectedVersion(HttpRequest request, out long expectedChangeVersion)
    {
        expectedChangeVersion = default;
        return request.Headers.TryGetValue(WorkspaceVersionHeader, out var header) &&
               header.Count == 1 &&
               long.TryParse(
                   header.ToString(),
                   System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out expectedChangeVersion) &&
               expectedChangeVersion >= 0;
    }
}

internal static class EngineeringPersistenceApplyGuard
{
    public static async Task<T> ExecuteAsync<T>(
        EngineeringWorkspace workspace,
        long expectedChangeVersion,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(operation);

        await using var mutation = await workspace.AcquireMutationAsync(
            expectedChangeVersion,
            cancellationToken);
        return await operation(cancellationToken);
    }
}
