using System.Security.Claims;
using Scada.Security.Authentication;

namespace Scada.Security.Tests;

public sealed class ClaimsPrincipalMapperTests
{
    [Fact]
    public void Map_ProducesStableSubjectDisplayNameAndDistinctRoles()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", "user-123"),
                new Claim("name", "Test Engineer"),
                new Claim("role", "developer"),
                new Claim("role", "Developer"),
                new Claim(ClaimTypes.Role, "operator")
            },
            authenticationType: "test");

        var principal = ClaimsPrincipalMapper.Map(new ClaimsPrincipal(identity));

        Assert.True(principal.IsAuthenticated);
        Assert.Equal("user-123", principal.SubjectId);
        Assert.Equal("Test Engineer", principal.DisplayName);
        Assert.Equal(new[] { "developer", "operator" }, principal.Roles);
    }

    [Fact]
    public void Map_UnauthenticatedIdentityRemainsUnauthenticated()
    {
        var principal = ClaimsPrincipalMapper.Map(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(principal.IsAuthenticated);
        Assert.Equal(string.Empty, principal.SubjectId);
        Assert.Empty(principal.Roles);
    }

    [Fact]
    public void Map_AuthenticatedIdentityWithoutSubjectIsStillRejectedByAuthorizationLayerLater()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("role", "developer") },
            authenticationType: "test");

        var principal = ClaimsPrincipalMapper.Map(new ClaimsPrincipal(identity));

        Assert.True(principal.IsAuthenticated);
        Assert.Equal(string.Empty, principal.SubjectId);
        Assert.Equal(new[] { "developer" }, principal.Roles);
    }
}
