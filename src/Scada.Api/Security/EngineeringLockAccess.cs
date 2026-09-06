using Scada.Engineering.Security;

namespace Scada.Api.Security;

public static class EngineeringLockAccess
{
    public static bool IsLocked(IEngineeringLockRegistry registry)
    {
        try
        {
            return EngineeringLockContract.Normalize(registry.Snapshot()).Locked;
        }
        catch (InvalidDataException)
        {
            // Malformed protection metadata must never fail open.
            return true;
        }
    }

    public static IResult? ProtectedEngineeringFailure(IEngineeringLockRegistry registry) =>
        IsLocked(registry)
            ? Results.Json(
                new { error = "Engineering is locked." },
                statusCode: StatusCodes.Status403Forbidden)
            : null;

    public static bool IsWorkspaceReadExempt(HttpRequest request)
    {
        var path = request.Path;
        return path.StartsWithSegments("/api/project-package") ||
               path.StartsWithSegments("/api/engineering/export") ||
               path.StartsWithSegments("/api/engineering/import") ||
               path.StartsWithSegments("/api/engineering/lock");
    }

    public static bool IsPersistenceRecoveryExempt(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Equals("/api/engineering/persistence/status", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/engineering/persistence/projects/first", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.EndsWith("/checkout", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/preview", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/apply", StringComparison.OrdinalIgnoreCase);
    }
}
