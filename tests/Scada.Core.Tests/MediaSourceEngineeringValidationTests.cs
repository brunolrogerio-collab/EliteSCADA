using Scada.Engineering.Contracts;
using Scada.Engineering.Media;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Gateways;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

using System.Text.Json;

namespace Scada.Core.Tests;

public sealed class MediaSourceEngineeringValidationTests
{
    [Theory]
    [InlineData("stable", true)]
    [InlineData("missing", false)]
    [InlineData("https://camera.example.test/live", false)]
    public void VideoPlayer_PreviewUsesStableProspectiveMediaSourceIdentity(string reference, bool accepted)
    {
        var source = Source(MediaSourceProtocol.Hls, "https://media.example.local/live.m3u8");
        var id = reference == "stable" ? source.Id!.Value.ToString() : reference == "missing" ? Guid.NewGuid().ToString() : reference;
        var service = CreateExchange(new InMemoryMediaSourceEngineeringRegistry());
        var package = service.ExportPackage() with
        {
            MediaSources = [source],
            Screens = [new ScreenEngineeringDto(Guid.NewGuid(), "media", "Media", Elements:
            [new VisualElementEngineeringDto("camera", "core.videoPlayer", Properties: new Dictionary<string, JsonElement>
            {
                ["mediaSourceId"] = JsonSerializer.SerializeToElement(id),
                ["mediaMuted"] = JsonSerializer.SerializeToElement(true)
            })])]
        };
        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        Assert.Equal(accepted, preview.CanApply);
        if (!accepted) Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code.StartsWith("VISUAL_MEDIA_SOURCE_", StringComparison.Ordinal));
    }

    [Fact]
    public void MediaPayloadInspector_AcceptsPdfAndBrowserVideoContainersBySignature()
    {
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        var mp4 = new byte[] { 0, 0, 0, 16, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0 };
        var webm = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x93, 0x42, 0x82, 0x84 };

        Assert.Equal(VisualAssetContentInspector.PdfMediaType,
            VisualAssetContentInspector.InspectAndCanonicalize(pdf).MediaType);
        Assert.Equal(VisualAssetContentInspector.Mp4MediaType,
            VisualAssetContentInspector.InspectAndCanonicalize(mp4).MediaType);
        Assert.Equal(VisualAssetContentInspector.WebmMediaType,
            VisualAssetContentInspector.InspectAndCanonicalize(webm).MediaType);
        Assert.Throws<InvalidDataException>(() => VisualAssetContentInspector.InspectAndCanonicalize("not actually a pdf"u8));
    }

    [Fact]
    public void VisualAssetMediaLimits_KeepImagesSmallAndAllowBoundedDocumentAndVideoPayloads()
    {
        Assert.Equal(16L * 1024 * 1024, VisualAssetEngineeringValidator.MaximumBytesFor("image/png"));
        Assert.Equal(32L * 1024 * 1024, VisualAssetEngineeringValidator.MaximumBytesFor(VisualAssetContentInspector.PdfMediaType));
        Assert.Equal(64L * 1024 * 1024, VisualAssetEngineeringValidator.MaximumBytesFor(VisualAssetContentInspector.Mp4MediaType));
        Assert.Equal(64L * 1024 * 1024, VisualAssetEngineeringValidator.MaximumBytesFor(VisualAssetContentInspector.WebmMediaType));
    }

    [Theory]
    [InlineData(MediaSourceProtocol.Http, "https://media.example.local/camera/live")]
    [InlineData(MediaSourceProtocol.Hls, "https://media.example.local/plant/stream.m3u8")]
    [InlineData(MediaSourceProtocol.Mjpeg, "http://camera.example.local/video.mjpg")]
    [InlineData(MediaSourceProtocol.Rtsp, "rtsp://camera.example.local:554/stream1")]
    [InlineData(MediaSourceProtocol.Rtsp, "rtsps://camera.example.local/stream1")]
    public void Validate_AcceptsProtocolCompatibleCredentialFreeEndpoint(
        MediaSourceProtocol protocol,
        string endpoint)
    {
        var issues = MediaSourceEngineeringValidation.Validate(Source(protocol, endpoint));

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(MediaSourceProtocol.Rtsp, "https://camera.example.local/stream")]
    [InlineData(MediaSourceProtocol.Hls, "rtsp://camera.example.local/stream")]
    public void Validate_RejectsEndpointSchemeThatDoesNotMatchProtocol(
        MediaSourceProtocol protocol,
        string endpoint)
    {
        var issues = MediaSourceEngineeringValidation.Validate(Source(protocol, endpoint));

        Assert.Contains(issues, issue => issue.Code == "MEDIA_SOURCE_SCHEME_MISMATCH");
    }

    [Theory]
    [InlineData("rtsp://user:secret@camera.example.local/stream", "MEDIA_SOURCE_INLINE_CREDENTIALS")]
    [InlineData("https://camera.example.local/live?token=secret", "MEDIA_SOURCE_QUERY_NOT_ALLOWED")]
    [InlineData("https://camera.example.local/live#fragment", "MEDIA_SOURCE_FRAGMENT_NOT_ALLOWED")]
    public void Validate_RejectsCredentialBearingOrAmbiguousEndpoint(
        string endpoint,
        string expectedCode)
    {
        var issues = MediaSourceEngineeringValidation.Validate(Source(MediaSourceProtocol.Rtsp, endpoint));

        Assert.Contains(issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Validate_RejectsMalformedIdentityAndOversizedEndpoint()
    {
        var source = Source(MediaSourceProtocol.Http, $"https://media.example.local/{new string('a', 2050)}") with
        {
            Key = "invalid key",
            Name = "\n"
        };

        var issues = MediaSourceEngineeringValidation.Validate(source);

        Assert.Contains(issues, issue => issue.Code == "MEDIA_SOURCE_KEY_INVALID");
        Assert.Contains(issues, issue => issue.Code == "MEDIA_SOURCE_NAME_INVALID");
        Assert.Contains(issues, issue => issue.Code == "MEDIA_SOURCE_ENDPOINT_INVALID");
    }

    [Fact]
    public void EngineeringPackage_RoundTripsMediaSourceWithoutCredentialFields()
    {
        var sources = new InMemoryMediaSourceEngineeringRegistry();
        var service = CreateExchange(sources);
        var source = Source(MediaSourceProtocol.Hls, "https://media.example.local/stream.m3u8");
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            MediaSources: [source]);

        Assert.True(service.Preview(package, ImportMode.CreateAndUpdate).CanApply);
        var result = service.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Created);

        var exported = service.ParseJson(service.ExportJson());
        var restored = Assert.Single(exported.MediaSources!);
        Assert.Equal(source.Protocol, restored.Protocol);
        Assert.Equal(source.Endpoint, restored.Endpoint);
        using var exportDocument = System.Text.Json.JsonDocument.Parse(service.ExportJson());
        Assert.Equal("hls", exportDocument.RootElement.GetProperty("mediaSources")[0].GetProperty("protocol").GetString());
        Assert.DoesNotContain("secret", service.ExportJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_SchemaV20PackageWithoutMediaSources_NormalizesToEmptyCollection()
    {
        var service = CreateExchange(new InMemoryMediaSourceEngineeringRegistry());
        const string json = """
            {"schema":"scada.engineering","schemaVersion":20,"exportedAt":"2026-10-04T00:00:00Z","tags":[],"alarms":[]}
            """;

        var parsed = service.ParseJson(json);

        Assert.Empty(parsed.MediaSources!);
        Assert.Equal(21, service.ExportPackage().SchemaVersion);
    }

    [Fact]
    public void Registry_RejectsKeyCollisionWithoutChangingEitherStableIdentity()
    {
        var registry = new InMemoryMediaSourceEngineeringRegistry();
        var first = Source(MediaSourceProtocol.Http, "https://media.example.local/first");
        var second = Source(MediaSourceProtocol.Http, "https://media.example.local/second") with { Key = "CAMERA-02" };
        registry.Upsert(first);
        registry.Upsert(second);

        Assert.Throws<InvalidOperationException>(() => registry.Upsert(second with { Key = first.Key }));

        Assert.Equal(first.Id, registry.FindByKey(first.Key)!.Id);
        Assert.Equal(second.Id, registry.FindByKey(second.Key)!.Id);
        Assert.Equal(2, registry.Snapshot().Count);
    }

    [Fact]
    public void Registry_ChangesAdvanceWorkspaceCallbackOnlyWhenTheCollectionChanges()
    {
        var changeCount = 0;
        var registry = new InMemoryMediaSourceEngineeringRegistry(() => changeCount++);
        var source = Source(MediaSourceProtocol.Http, "https://media.example.local/camera");

        registry.Upsert(source);
        registry.Upsert(source with { Name = "Renamed camera" });
        Assert.Equal(2, changeCount);
        Assert.True(registry.Remove(source.Id!.Value));
        Assert.False(registry.Remove(source.Id.Value));
        registry.Clear();

        Assert.Equal(3, changeCount);
    }

    [Fact]
    public void EngineeringPackage_ResolvesMediaSourcesByStableIdAndHonorsCreateOnly()
    {
        var registry = new InMemoryMediaSourceEngineeringRegistry();
        var service = CreateExchange(registry);
        var existing = Source(MediaSourceProtocol.Http, "https://media.example.local/old");
        registry.Upsert(existing);
        var incoming = existing with { Name = "Updated camera", Endpoint = "https://media.example.local/new" };
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            MediaSources: [incoming]);

        var createOnlyPreview = service.Preview(package, ImportMode.CreateOnly);
        Assert.True(createOnlyPreview.CanApply);
        Assert.Contains(createOnlyPreview.Items, item =>
            item.EntityKind == ImportEntityKind.MediaSource && item.Operation == ImportOperation.Skip);
        Assert.Equal(0, service.Apply(package, ImportMode.CreateOnly).Updated);
        Assert.Equal("https://media.example.local/old", registry.Find(existing.Id!.Value)!.Endpoint);

        var update = service.Apply(package, ImportMode.UpdateExisting);
        Assert.Equal(1, update.Updated);
        Assert.Equal(existing.Id, registry.FindByKey(existing.Key)!.Id);
        Assert.Equal("https://media.example.local/new", registry.Find(existing.Id.Value)!.Endpoint);
    }

    [Fact]
    public void EngineeringPackage_RejectsStableIdAndKeyPointingToDifferentMediaSources()
    {
        var registry = new InMemoryMediaSourceEngineeringRegistry();
        var service = CreateExchange(registry);
        var first = Source(MediaSourceProtocol.Http, "https://media.example.local/one");
        var second = Source(MediaSourceProtocol.Http, "https://media.example.local/two") with { Key = "CAMERA-02" };
        registry.Upsert(first);
        registry.Upsert(second);
        var conflicting = second with { Key = first.Key };
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            MediaSources: [conflicting]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue => issue.Code == "MEDIA_SOURCE_KEY_CONFLICT");
        Assert.Equal("https://media.example.local/one", registry.Find(first.Id!.Value)!.Endpoint);
        Assert.Equal("https://media.example.local/two", registry.Find(second.Id!.Value)!.Endpoint);
    }

    private static EngineeringExchangeService CreateExchange(IMediaSourceEngineeringRegistry mediaSources)
    {
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        return new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            mediaSources: mediaSources);
    }

    private static MediaSourceEngineeringDto Source(MediaSourceProtocol protocol, string endpoint) =>
        new(Guid.NewGuid(), "CAMERA-01", "Camera 01", protocol, endpoint);

    [Fact]
    public void RuntimeHeader_RoundTripsAndRejectsBrokenMobileReferences()
    {
        var exchange = CreateExchange(new InMemoryMediaSourceEngineeringRegistry());
        using var projectFontWeight = System.Text.Json.JsonDocument.Parse("700");
        using var screenFontWeight = System.Text.Json.JsonDocument.Parse("500");
        var package = exchange.ParseJson(exchange.ExportJson(indented: false)) with {
            RuntimePresentation = new RuntimePresentationEngineeringDto(Header: new RuntimeHeaderEngineeringDto(
                Enabled: false,
                Height: 168,
                BackgroundColor: "#223344",
                TitlePosition: "center",
                ControlsPosition: "left",
                ControlsOrder: 3,
                ShowScreenName: true,
                TitleStyle: new RuntimeHeaderTextStyleEngineeringDto("system-ui", 24, projectFontWeight.RootElement.Clone(), "#ffffff"),
                ScreenNameStyle: new RuntimeHeaderTextStyleEngineeringDto("Arial, sans-serif", 14, screenFontWeight.RootElement.Clone()),
                DateTime: new RuntimeHeaderDateTimeEngineeringDto("dateTime", "right", 4, "yyyy-MM-dd", "12h"),
                AlarmsVisible: false))
        };
        Assert.True(exchange.Preview(package, ImportMode.CreateAndUpdate).CanApply);
        var result = exchange.Apply(package, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        var reopened = exchange.ParseJson(exchange.ExportJson(indented: false));
        Assert.Equal(package.RuntimePresentation, reopened.RuntimePresentation);
        var invalid = package with { RuntimePresentation = package.RuntimePresentation! with {
            MobileScreens = new Dictionary<string, string> { ["missing"] = "also-missing" }
        }};
        Assert.Contains(exchange.Preview(invalid, ImportMode.CreateAndUpdate).Items.SelectMany(item => item.Issues),
            issue => issue.Code == "RUNTIME_PRESENTATION_REFERENCE_INVALID" && issue.IsError);
        var invalidHeader = package with { RuntimePresentation = package.RuntimePresentation! with {
            Header = package.RuntimePresentation.Header! with {
                ControlsPosition = "middle",
                DateTime = new RuntimeHeaderDateTimeEngineeringDto("dateTime", "right", 21, "dd/MM/yyyy", "24h")
            }
        }};
        Assert.Contains(exchange.Preview(invalidHeader, ImportMode.CreateAndUpdate).Items.SelectMany(item => item.Issues),
            issue => issue.Code == "RUNTIME_PRESENTATION_REFERENCE_INVALID" && issue.IsError);
    }
}
