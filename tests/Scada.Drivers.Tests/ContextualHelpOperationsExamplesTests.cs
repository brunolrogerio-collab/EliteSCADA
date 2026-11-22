using System.Net;
using System.Text.Json;
using Scada.Api.Engineering;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpOperationsExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public async Task ExactPublishedReachabilityRequestUsesOneServerTargetWithoutSpeakingDeviceProtocol(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "diagnostics.overview");
        var example = Assert.Single(guide.Sections, s => s.Code is not null).Code!;
        var request = JsonSerializer.Deserialize<NetworkReachabilityProbeRequest>(example,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("127.0.0.1", request.Host);
        Assert.Equal(502, request.Port);
        Assert.Equal(3000, request.TimeoutMilliseconds);
        var attempts = new List<(IPAddress Address, int Port)>();
        // Record the target instead of opening a TCP device socket. The ordinary ICMP check is loopback-only.
        var probe = new NetworkReachabilityProbe((address, port, _) =>
        {
            attempts.Add((address, port));
            return Task.CompletedTask;
        });
        var result = await probe.ProbeAsync(request);
        var attempt = Assert.Single(attempts);
        Assert.Equal(IPAddress.Loopback, attempt.Address);
        Assert.Equal(request.Port, attempt.Port);
        Assert.Equal("Connected", result.Tcp.Status);
        Assert.Contains("protocol health was not tested", result.Tcp.Detail!, StringComparison.Ordinal);
        Assert.Contains(result.Icmp.Status, new[] { "Success", "Timeout", "Unavailable" });
        Assert.Equal("EliteSCADA.Api.DriverHost", result.Authority);
    }

    [Theory]
    [InlineData("http://127.0.0.1", 502)]
    [InlineData("127.0.0.1/path", 502)]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", 65536)]
    public async Task PublishedHostAndPortBoundsRejectInvalidInputsBeforeAnyConnection(string host, int port)
    {
        var attempted = false;
        var probe = new NetworkReachabilityProbe((_, _, _) => { attempted = true; return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<ArgumentException>(() => probe.ProbeAsync(new(host, port, 3000)));
        Assert.False(attempted);
    }

    [Fact]
    public void ExistingTopicLinksAndLocalizedFieldKeysRemainStableForAllThreeOperationalGuides()
    {
        var required = new Dictionary<string, string[]>
        {
            ["runtime.session-classes"] = ["viewOnly", "interactive", "requestedClass", "grantedClass", "admissionReasonCode", "capacityReasonCode", "end"],
            ["diagnostics.overview"] = ["query", "filter", "autoRefresh", "refresh", "observedAtUtc", "state", "dataAge", "tagQuality", "host", "port", "tcp", "icmp"],
            ["recovery.backup-system-recovery"] = ["applicationFile", "authorityFile", "authorityPassword", "licenseFile", "validateBackups", "restoreAuthority", "validateApplication", "restoreApplication", "continueRecovery", "continueWithoutLicense"]
        };
        var baseline = ContextualHelpCatalog.Build("pt-BR");
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            foreach (var (id, keys) in required)
            {
                var guide = Assert.Single(catalog.Topics, t => t.Id == id);
                Assert.Contains(guide.Sections, section => section.Body.StartsWith("1. ", StringComparison.Ordinal));
                Assert.True(guide.Sections.Count >= 6);
                var fields = guide.Sections.SelectMany(s => s.Fields ?? []).ToArray();
                foreach (var key in keys) Assert.Contains(fields, f => f.Key == key);
                Assert.Equal(fields.Length, fields.Select(f => f.Key).Distinct().Count());
                Assert.All(fields, f =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(f.Description));
                    Assert.False(string.IsNullOrWhiteSpace(f.Constraints));
                });
                var reference = baseline.Topics.Single(t => t.Id == id);
                Assert.Equal(reference.Sections.Count, guide.Sections.Count);
                Assert.Equal(reference.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key), fields.Select(f => f.Key));
                Assert.All(guide.RelatedTopicIds!, related => Assert.Contains(catalog.Topics, t => t.Id == related));
            }
        }
    }
}
