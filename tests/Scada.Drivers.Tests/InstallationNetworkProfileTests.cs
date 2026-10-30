using Microsoft.Extensions.Configuration;
using Scada.Api.Engineering;

namespace Scada.Drivers.Tests;

public sealed class InstallationNetworkProfileTests
{
    [Fact]
    public void Read_StableDockerProfile_ReportsPublishedAndInternalPortsWithoutSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "stable-docker",
                ["InstallationNetwork:InstanceId"] = "plant-a",
                ["InstallationNetwork:WebBindAddress"] = "127.0.0.1",
                ["InstallationNetwork:WebPublishedPort"] = "28080",
                ["InstallationNetwork:ApiBindAddress"] = "0.0.0.0",
                ["InstallationNetwork:ApiPublishedPort"] = "25080",
                ["InstallationNetwork:DatabasePublishedPort"] = "25432",
                ["InstallationNetwork:MediaControlPort"] = "19997",
                ["InstallationNetwork:MediaPlaybackPort"] = "18888",
                ["ConnectionStrings:EliteScada"] = "Host=db;Password=must-not-leak"
            })
            .Build();

        var profile = InstallationNetworkProfileApi.Read(configuration);

        Assert.NotNull(profile);
        Assert.Equal("plant-a", profile.InstanceId);
        Assert.Equal(("127.0.0.1", 28080, 80),
            (profile.Web.BindAddress, profile.Web.PublishedPort, profile.Web.InternalPort));
        Assert.Equal(("0.0.0.0", 25080, 5080),
            (profile.Api.BindAddress, profile.Api.PublishedPort, profile.Api.InternalPort));
        Assert.Equal(("127.0.0.1", 25432, 5432),
            (profile.Database.BindAddress, profile.Database.PublishedPort, profile.Database.InternalPort));
        Assert.False(profile.MediaControl.PublishedToHost);
        Assert.False(profile.MediaPlayback.PublishedToHost);
        Assert.Equal((19997, false), (profile.MediaControl.InternalPort, profile.MediaControl.PublishedToHost));
        Assert.Equal((18888, false), (profile.MediaPlayback.InternalPort, profile.MediaPlayback.PublishedToHost));
        Assert.False(profile.ApplyThroughUi);
        Assert.False(profile.HostMappingIndependentlyVerified);
        Assert.DoesNotContain("must-not-leak", System.Text.Json.JsonSerializer.Serialize(profile));
    }

    [Fact]
    public void Read_UnsupportedOrMalformedProfile_FailsClosedToUnsupportedOrKnownDefaults()
    {
        var unsupported = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "native-service"
            })
            .Build();
        Assert.Null(InstallationNetworkProfileApi.Read(unsupported));

        var malformed = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "stable-docker",
                ["InstallationNetwork:WebPublishedPort"] = "70000",
                ["InstallationNetwork:ApiPublishedPort"] = "not-a-port"
            })
            .Build();
        var profile = Assert.IsType<InstallationNetworkProfileView>(InstallationNetworkProfileApi.Read(malformed));
        Assert.Equal(18080, profile.Web.PublishedPort);
        Assert.Equal(15080, profile.Api.PublishedPort);
    }
}
