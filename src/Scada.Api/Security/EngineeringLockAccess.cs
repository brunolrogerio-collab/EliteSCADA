using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;

namespace Scada.Api.Security;

public static class EngineeringLockAccess
{
    public static EngineeringLockEngineeringDto Current(IEngineeringExchangeService exchange)
    {
        try
        {
            return EngineeringLockContract.Normalize(exchange.ExportPackage().EngineeringLock);
        }
        catch (InvalidDataException)
        {
            // Callers must treat malformed protection metadata as locked/fail-closed.
            return new EngineeringLockEngineeringDto(
                Locked: true,
                Verifier: new EngineeringLockVerifierDto(
                    EngineeringLockContract.Algorithm,
                    EngineeringLockContract.VerifierVersion,
                    EngineeringLockContract.CurrentIterations,
                    Convert.ToBase64String(new byte[EngineeringLockContract.SaltByteLength]),
                    Convert.ToBase64String(new byte[EngineeringLockContract.HashByteLength])));
        }
    }

    public static bool IsLocked(IEngineeringExchangeService exchange) => Current(exchange).Locked;

    public static IResult? ProtectedEngineeringFailure(IEngineeringExchangeService exchange) =>
        IsLocked(exchange)
            ? Results.Json(
                new { error = "Engineering is locked." },
                statusCode: StatusCodes.Status403Forbidden)
            : null;

    public static void Replace(
        IEngineeringExchangeService exchange,
        EngineeringLockEngineeringDto? state)
    {
        var current = exchange.ExportPackage();
        var normalized = EngineeringLockContract.Normalize(state);
        var lockOnly = new EngineeringPackage(
            current.Schema,
            current.SchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            EngineeringLock: normalized);

        var preview = exchange.Preview(lockOnly, ImportMode.UpdateExisting);
        if (!preview.CanApply)
            throw new InvalidOperationException("Engineering Lock state could not pass canonical Engineering validation.");

        var result = exchange.Apply(lockOnly, ImportMode.UpdateExisting);
        if (result.Issues.Any(issue => issue.IsError))
            throw new InvalidOperationException("Engineering Lock state could not be applied to canonical Engineering state.");
    }

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
