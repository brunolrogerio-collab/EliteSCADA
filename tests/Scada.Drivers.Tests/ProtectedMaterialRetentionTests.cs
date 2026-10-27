using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Api.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ProtectedMaterialRetentionTests
{
    private static readonly ProtectedMaterialScope Scope = new("deployment:product-licensing", "ProductLicensing", "machine", "OnlineDeviceState");
    private static ProtectedMaterialMutationContext Admin(bool allowed = true) =>
        new(new SecurityPrincipal("retention-test", "Retention Test", []),
            new AuthorizationDecision(allowed, SecurityCapability.EngineeringModify, "test decision", []));

    [Fact]
    public async Task CollectionIsBatchedScopedAndAuditedWithoutExposingMaterial()
    {
        using var f = new Fixture();
        var current = await f.Store();
        for (var i = 0; i < 129; i++) await f.Store();
        var other = await f.Authority.StoreAsync(Scope with { ResourceId = "other-machine" }, "foreign"u8.ToArray(), Admin());
        Assert.Equal(128, await f.Authority.CollectUnreferencedAsync(Scope, current, Admin()));
        Assert.Equal(1, await f.Authority.CollectUnreferencedAsync(Scope, current, Admin()));
        Assert.Equal(0, await f.Authority.CollectUnreferencedAsync(Scope, current, Admin()));
        await using var selected = await f.Authority.ResolveAsync(Scope, current);
        await using var foreign = await f.Authority.ResolveAsync(Scope with { ResourceId = "other-machine" }, other.Reference);
        var events = f.Audit.Snapshot();
        Assert.Equal(129, events.Count(e => e.Action == ProtectedMaterialAuditActions.Delete));
        Assert.DoesNotContain("fictitious-retention-secret", JsonSerializer.Serialize(events));
    }

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("wrong-scope")]
    [InlineData("missing")]
    [InlineData("invalid-reference")]
    [InlineData("wrong-key")]
    public async Task CollectionRequiresAuthenticatedAnchorAndAuthorizationBeforeDeletion(string failure)
    {
        using var f = new Fixture();
        var current = await f.Store();
        await f.Store();
        using var wrongKey = new FileHostProtectedMaterialAuthority(
            new ProtectedMaterialAuthorityOptions(f.Root, "FICTITIOUS_TEST_KEY", null),
            RandomNumberGenerator.GetBytes(32), f.Audit);
        var authority = failure == "wrong-key" ? wrongKey : f.Authority;
        var scope = failure == "wrong-scope" ? Scope with { Purpose = "Password" } : Scope;
        var reference = failure switch
        {
            "missing" => FileHostProtectedMaterialAuthority.ReferencePrefix + Guid.NewGuid().ToString("N"),
            "invalid-reference" => "../not-a-reference",
            _ => current
        };
        await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
            await authority.CollectUnreferencedAsync(scope, reference, Admin(failure != "unauthorized")));
        Assert.Equal(2, Directory.GetFiles(f.Root, "*.json").Length);
        await using var retained = await f.Authority.ResolveAsync(Scope, current);
    }

    [Fact]
    public async Task TamperedObsoleteCiphertextIsNotDeletedAndCurrentRemainsReadable()
    {
        using var f = new Fixture();
        var current = await f.Store();
        var obsolete = await f.Store();
        var path = Path.Combine(f.Root, obsolete[FileHostProtectedMaterialAuthority.ReferencePrefix.Length..] + ".json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        node["tag"] = Convert.ToBase64String(new byte[16]);
        await File.WriteAllTextAsync(path, node.ToJsonString());
        await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
            await f.Authority.CollectUnreferencedAsync(Scope, current, Admin()));
        Assert.True(File.Exists(path));
        await using var selected = await f.Authority.ResolveAsync(Scope, current);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "scadivo-retention-test-" + Guid.NewGuid().ToString("N"));
        public InMemoryAuditSink Audit { get; } = new();
        public FileHostProtectedMaterialAuthority Authority { get; }
        public Fixture() => Authority = new(new ProtectedMaterialAuthorityOptions(Root, "FICTITIOUS_TEST_KEY", null),
            RandomNumberGenerator.GetBytes(32), Audit);
        public async Task<string> Store() => (await Authority.StoreAsync(Scope,
            Encoding.UTF8.GetBytes("fictitious-retention-secret"), Admin())).Reference;
        public void Dispose()
        {
            Authority.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
