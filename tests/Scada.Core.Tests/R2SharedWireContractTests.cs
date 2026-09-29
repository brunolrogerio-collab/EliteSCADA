using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class R2SharedWireContractTests
{
    [Fact]
    public void SchemaV20_RoundTripsFrozenC0WireContracts()
    {
        var profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var queryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var alarmViewId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var profile = new HistorianCaptureProfileEngineeringDto(
            profileId,
            "analog-change",
            "Analog change",
            HistorianCaptureStrategy.OnChangeDeadbandMaxInterval,
            Deadband: 0.5,
            MaximumIntervalMilliseconds: 60_000);

        var query = new DataQueryEngineeringDto(
            queryId,
            "trend-default",
            "Default trend",
            "historical",
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600)),
            SelectedFields: ["timestamp", "value", "quality"],
            Parameters:
            [
                new DataQueryParameterEngineeringDto(
                    "window",
                    "Window",
                    DataQueryParameterType.DurationSeconds,
                    new DataQueryParameterValue(DataQueryParameterType.DurationSeconds, "3600"))
            ],
            ParameterBindings:
            [
                new DataQueryParameterBindingEngineeringDto(
                    "window",
                    DataQueryParameterTarget.RelativeDurationSeconds)
            ],
            HistorianRetrieval: new HistorianRetrievalEngineeringDto(
                HistorianRetrievalMode.SampledFixedStep,
                StepMilliseconds: 60_000,
                MaximumGapMilliseconds: 300_000));

        var alarmView = new AlarmViewEngineeringDto(
            alarmViewId,
            "critical-unacked",
            "Critical unacknowledged",
            new AlarmViewFilterEngineeringDto(
                Priorities: [AlarmPriority.Critical],
                Active: AlarmViewMatchState.Yes,
                Acknowledged: AlarmViewMatchState.No));

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.Parse("2026-09-29T15:00:00+00:00"),
            [
                new TagEngineeringDto(
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    "Pressure",
                    "Plant.Pressure",
                    TagDataType.Double,
                    HistorianCaptureProfileId: profileId)
            ],
            Array.Empty<AlarmEngineeringDto>(),
            HistorianCaptureProfiles: [profile],
            DataQueries: [query],
            AlarmViews: [alarmView]);

        var json = JsonSerializer.Serialize(package, JsonOptions());

        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var parsed = service.ParseJson(json);

        Assert.Equal(20, EngineeringExchangeService.CurrentSchemaVersion);
        Assert.Equal(profileId, Assert.Single(parsed.Tags).HistorianCaptureProfileId);
        Assert.Equal(
            HistorianCaptureStrategy.OnChangeDeadbandMaxInterval,
            Assert.Single(parsed.HistorianCaptureProfiles!).Strategy);
        Assert.Equal(
            HistorianRetrievalMode.SampledFixedStep,
            Assert.Single(parsed.DataQueries!).HistorianRetrieval!.Mode);
        Assert.Equal(
            AlarmViewMatchState.No,
            Assert.Single(parsed.AlarmViews!).Filter.Acknowledged);

        Assert.Contains("\"strategy\":\"onChangeDeadbandMaxInterval\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"sampledFixedStep\"", json, StringComparison.Ordinal);
        Assert.Contains("\"historianCaptureProfileId\":\"11111111-1111-1111-1111-111111111111\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaV19_MissingF0Fields_RemainsReadableAndNormalizesCollections()
    {
        const string json =
            """
            {
              "schema": "scada.engineering",
              "schemaVersion": 19,
              "exportedAt": "2026-09-29T15:00:00+00:00",
              "tags": [],
              "alarms": []
            }
            """;

        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);

        var parsed = service.ParseJson(json);

        Assert.Equal(19, parsed.SchemaVersion);
        Assert.Empty(parsed.HistorianCaptureProfiles!);
        Assert.Empty(parsed.DataQueries!);
        Assert.Empty(parsed.AlarmViews!);
    }

    [Fact]
    public void FragmentAndLibraryWireEnums_UseFrozenStableNames()
    {
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.Parse("2026-09-29T15:00:00+00:00"),
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>());

        var root = new EngineeringFragmentEntityReference(
            ImportEntityKind.Screen,
            "screen.main",
            Guid.Parse("55555555-5555-5555-5555-555555555555"));

        var envelope = new EngineeringFragmentEnvelope(
            EngineeringFragmentContract.Schema,
            EngineeringFragmentContract.SchemaVersion,
            DateTimeOffset.Parse("2026-09-29T15:00:00+00:00"),
            new EngineeringFragmentManifest([root]),
            package);

        var update = new ReusableLibraryResourceUpdateEngineeringDto(
            new ReusableLibrarySourceProvenanceEngineeringDto(
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                "2.5.0",
                "sha256:abc"),
            ReusableLibraryUpdateState.UpdateAvailable,
            "sha256:def");

        var json = JsonSerializer.Serialize(
            new
            {
                envelope,
                preview = new EngineeringFragmentPreviewItem(root, EngineeringFragmentPlanOperation.ReuseIdentical),
                update
            },
            JsonOptions());

        Assert.Equal(".escadafrag", EngineeringFragmentContract.FileExtension);
        Assert.Contains("\"schema\":\"scada.engineering.fragment\"", json, StringComparison.Ordinal);
        Assert.Contains("\"operation\":\"reuseIdentical\"", json, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"updateAvailable\"", json, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
