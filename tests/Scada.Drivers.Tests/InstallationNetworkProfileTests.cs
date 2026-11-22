using Microsoft.Extensions.Configuration;
using Scada.Api.Engineering;

namespace Scada.Drivers.Tests;

public sealed class InstallationNetworkProfileTests
{
    [Fact]
    public void NativeProfile_ReportsDependentPortsOnlyForRegisteredLocalServices()
    {
        var directory=Path.Combine(Path.GetTempPath(),"scadivo-native-dependent-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var config=Path.Combine(directory,"app.json");var profile=Path.Combine(directory,"profile.json");
            var topology=Path.Combine(directory,"topology.json");var pg=Path.Combine(directory,"pg.conf");var media=Path.Combine(directory,"media.yml");
            File.WriteAllText(config,"""{"Kestrel":{"Endpoints":{"Web":{"Url":"http://127.0.0.1:18080"},"Api":{"Url":"http://127.0.0.1:15120"}}}}""");
            File.WriteAllText(pg,"port=15432");File.WriteAllText(media,"apiAddress: :9997\nhlsAddress: :8888");
            File.WriteAllText(profile,System.Text.Json.JsonSerializer.Serialize(new {
                schema="scadivo-native-network-v1",platform="linux",serviceName="api",configurationFile=config,endpointName="Web",apiEndpointName="Api",topologyFile=topology,
                database=new {serviceName="database",configurationFile=pg},media=new {serviceName="media",configurationFile=media}
            }));
            File.WriteAllText(topology,"""{"schemaVersion":1,"active":{"mode":"LocalManaged"},"pending":null}""");
            var settings=new Dictionary<string,string?> {
                ["InstallationNetwork:Profile"]="native-linux",["InstallationNetwork:AdministrativeProfilePath"]=profile,
                ["Kestrel:Endpoints:Web:Url"]="http://127.0.0.1:18080",["Kestrel:Endpoints:Api:Url"]="http://127.0.0.1:15120",
                ["ConnectionStrings:EliteScada"]="Host=127.0.0.1;Port=15432;Password=must-not-leak",
                ["MediaSources:GatewayControlUrl"]="http://127.0.0.1:9997",["MediaSources:GatewayPlaybackUrl"]="http://127.0.0.1:8888"
            };
            var configuration=new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var result=InstallationNetworkProfileApi.ReadNative(configuration)!;
            Assert.Equal((15432,9997,8888),(result.DatabasePort,result.MediaControlPort,result.MediaPlaybackPort));
            var json=System.Text.Json.JsonSerializer.Serialize(result);
            Assert.DoesNotContain(directory,json);Assert.DoesNotContain("must-not-leak",json);
            File.WriteAllText(topology,"""{"schemaVersion":1,"active":{"mode":"Remote"},"pending":null}""");
            Assert.Null(InstallationNetworkProfileApi.ReadNative(configuration)!.DatabasePort);
            File.Delete(media);
            Assert.Null(InstallationNetworkProfileApi.ReadNative(configuration)!.MediaControlPort);
        }
        finally {Directory.Delete(directory,true);}
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("linux")]
    public void NativeProfile_RecognizesRegisteredArtifactAfterConfigurationReload(string platform)
    {
        var directory = Path.Combine(Path.GetTempPath(), "scadivo-profile-reload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var config = Path.Combine(directory, "app.json");
            var profile = Path.Combine(directory, "profile.json");
            File.WriteAllText(profile, System.Text.Json.JsonSerializer.Serialize(new {
                schema = "scadivo-native-network-v1", platform, serviceName = "scadivo-service",
                configurationFile = config, endpointName = "Web", apiEndpointName = "Api"
            }));
            void Write(int port) => File.WriteAllText(config, System.Text.Json.JsonSerializer.Serialize(new {
                InstallationNetwork = new { Profile = "native-" + platform, AdministrativeProfilePath = profile },
                Kestrel = new { Endpoints = new { Web = new { Url = "http://127.0.0.1:" + port }, Api = new { Url = "http://127.0.0.1:15120" } } }
            }));
            Write(18080);
            var configuration = new ConfigurationBuilder().AddJsonFile(config, optional: false, reloadOnChange: false).Build();
            using var configurationLifetime = (IDisposable)configuration;
            Assert.True(InstallationNetworkProfileApi.ReadNative(configuration)!.AdministrativeProfileAvailable);
            Write(28080);
            Assert.False(InstallationNetworkProfileApi.ReadNative(configuration)!.AdministrativeProfileAvailable);
            configuration.Reload();
            var reloaded = InstallationNetworkProfileApi.ReadNative(configuration)!;
            Assert.True(reloaded.AdministrativeProfileAvailable);
            Assert.Equal(28080, reloaded.WebPort);
            Assert.DoesNotContain(directory, System.Text.Json.JsonSerializer.Serialize(reloaded));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("http://user:private@127.0.0.1:18080")]
    [InlineData("http://127.0.0.1:18080/private")]
    [InlineData("http://127.0.0.1:18080/?token=private")]
    [InlineData("http://127.0.0.1:18080/#private")]
    public void NativeProfile_RejectsUrlsThatAreNotListenerAddresses(string invalidUrl)
    {
        foreach (var endpoint in new[] { "Web", "Api" })
        {
            var settings = new Dictionary<string, string?> {
                ["InstallationNetwork:Profile"] = "native-linux",
                ["Kestrel:Endpoints:Web:Url"] = "http://127.0.0.1:18080",
                ["Kestrel:Endpoints:Api:Url"] = "http://127.0.0.1:15120"
            };
            settings[$"Kestrel:Endpoints:{endpoint}:Url"] = invalidUrl.Replace(":18080", endpoint == "Api" ? ":15120" : ":18080");
            Assert.Null(InstallationNetworkProfileApi.ReadNative(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
        }
    }

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
        Assert.Equal(15120, profile.Api.PublishedPort);
    }

    [Fact]
    public void NativeProfile_RequiresExplicitNativeTypeAndDistinctConfiguredPorts()
    {
        var settings = new Dictionary<string, string?>
        {
            ["InstallationNetwork:Profile"] = "native-windows",
            ["Kestrel:Endpoints:Web:Url"] = "http://127.0.0.1:18080",
            ["Kestrel:Endpoints:Api:Url"] = "http://127.0.0.1:15120"
        };
        IConfiguration Build() => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var profile = Assert.IsType<InstallationNetworkProfileApi.NativeAccessProfile>(InstallationNetworkProfileApi.ReadNative(Build()));
        Assert.False(profile.AdministrativeProfileAvailable);
        Assert.Equal(("windows", 18080, 15120), (profile.Platform, profile.WebPort, profile.ApiPort));
        settings["Kestrel:Endpoints:Api:Url"] = "http://127.0.0.1:18080";
        Assert.Null(InstallationNetworkProfileApi.ReadNative(Build()));
        settings["InstallationNetwork:Profile"] = "stable-docker";
        Assert.Null(InstallationNetworkProfileApi.ReadNative(Build()));
    }

    [Fact]
    public void NativeProfile_RequiresMatchingAdministrativeArtifact()
    {
        var path = Path.GetTempFileName();
        var config = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new {
                schema = "scadivo-native-network-v1", platform = "linux", endpointName = "Web", apiEndpointName = "Api", configurationFile = config, serviceName = "scadivo.service"
            }));
            var settings = new Dictionary<string, string?> {
                ["InstallationNetwork:Profile"] = "native-linux",
                ["InstallationNetwork:AdministrativeProfilePath"] = path,
                ["Kestrel:Endpoints:Web:Url"] = "http://127.0.0.1:18080",
                ["Kestrel:Endpoints:Api:Url"] = "http://127.0.0.1:15120"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            Assert.False(InstallationNetworkProfileApi.ReadNative(configuration)!.AdministrativeProfileAvailable);
            File.WriteAllText(config, """{"Kestrel":{"Endpoints":{"Web":{"Url":"http://127.0.0.1:18080"},"Api":{"Url":"http://127.0.0.1:15120"}}}}""");
            Assert.True(InstallationNetworkProfileApi.ReadNative(configuration)!.AdministrativeProfileAvailable);
            File.WriteAllText(config, """{"Kestrel":{"Endpoints":{"Web":{"Url":"http://127.0.0.1:28080"},"Api":{"Url":"http://127.0.0.1:15120"}}}}""");
            Assert.False(InstallationNetworkProfileApi.ReadNative(configuration)!.AdministrativeProfileAvailable);
            File.WriteAllText(path, "{}");
            Assert.False(InstallationNetworkProfileApi.ReadNative(configuration)!.AdministrativeProfileAvailable);
        }
        finally { File.Delete(path); File.Delete(config); }
    }
}
