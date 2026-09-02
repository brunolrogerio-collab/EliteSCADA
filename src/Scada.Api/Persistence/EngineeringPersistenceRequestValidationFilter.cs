using System.Globalization;

namespace Scada.Api.Persistence;

public sealed class EngineeringPersistenceRequestValidationFilter : IEndpointFilter
{
    internal const int MinimumRevisionListLimit = 1;
    internal const int MaximumRevisionListLimit = 500;

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var error = Validate(context.HttpContext);
        return error is null
            ? next(context)
            : ValueTask.FromResult<object?>(
                Results.BadRequest(new EngineeringPersistenceRequestError(error)));
    }

    internal static string? Validate(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.RouteValues.TryGetValue("revision", out var rawRevision))
        {
            var revisionText = Convert.ToString(rawRevision, CultureInfo.InvariantCulture);
            if (!long.TryParse(
                    revisionText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var revision) ||
                revision < 1)
            {
                return "Revision must be greater than zero.";
            }
        }

        if (!HttpMethods.IsGet(context.Request.Method) ||
            !context.Request.Path.Value?.EndsWith("/revisions", StringComparison.OrdinalIgnoreCase) == true ||
            !context.Request.Query.TryGetValue("limit", out var rawLimit))
        {
            return null;
        }

        if (rawLimit.Count != 1 ||
            !int.TryParse(
                rawLimit[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var limit) ||
            limit is < MinimumRevisionListLimit or > MaximumRevisionListLimit)
        {
            return $"Revision list limit must be between {MinimumRevisionListLimit} and {MaximumRevisionListLimit}.";
        }

        return null;
    }
}

public sealed record EngineeringPersistenceRequestError(string Error);
