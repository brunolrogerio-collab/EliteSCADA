using System.Security.Claims;
using Scada.Security.Authorization;

namespace Scada.Security.Authentication;

public static class ClaimsPrincipalMapper
{
    public static SecurityPrincipal Map(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return new SecurityPrincipal(string.Empty, null, Array.Empty<string>(), IsAuthenticated: false);

        var subjectId = FirstNonBlank(
            principal.FindFirstValue("sub"),
            principal.FindFirstValue(ClaimTypes.NameIdentifier));

        var displayName = FirstNonBlank(
            principal.FindFirstValue("name"),
            principal.FindFirstValue(ClaimTypes.Name),
            principal.Identity.Name);

        var roles = principal.Claims
            .Where(claim =>
                claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase) ||
                claim.Type.Equals(ClaimTypes.Role, StringComparison.Ordinal))
            .Select(claim => claim.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SecurityPrincipal(
            subjectId ?? string.Empty,
            displayName,
            roles,
            IsAuthenticated: true);
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
