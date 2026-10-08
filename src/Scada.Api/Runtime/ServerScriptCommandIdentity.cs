using System.Security.Cryptography;
using System.Text;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>Stable Authority identity assigned by the host to one Server Script.</summary>
public static class ServerScriptCommandIdentity
{
    public static string RoleKey(string projectKey, Guid scriptId)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (scriptId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(scriptId));

        var projectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectKey.Trim())))
            .ToLowerInvariant()[..24];
        return $"server-script.{projectHash}.{scriptId:N}";
    }

    public static SecurityPrincipal CreatePrincipal(
        string projectKey,
        long revision,
        Guid scriptId)
    {
        if (revision <= 0)
            throw new ArgumentOutOfRangeException(nameof(revision));

        var role = RoleKey(projectKey, scriptId);
        var subject = $"server-script:{role}:revision:{revision}";
        return new SecurityPrincipal(subject, $"Server Script {scriptId:D}", [role]);
    }
}
