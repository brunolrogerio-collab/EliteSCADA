using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Historian.Memory;

namespace Scada.Drivers.Tests;

public sealed class ServerScriptQualifiedQualityIntegrationTests
{
    [Theory]
    [InlineData("Bad", TagQuality.Bad)]
    [InlineData("Stale", TagQuality.Stale)]
    [InlineData("Unavailable", TagQuality.Unavailable)]
    public async Task ServerScript_QualifiedServerMemorySample_PropagatesThroughRuntime(
        string qualityName,
        TagQuality expectedQuality)
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        TagValueChanged? realtimeEvent = null;
        using var subscription = eventBus.Subscribe<TagValueChanged>(evt =>
        {
            if (evt.Tag.Id == tagId && evt.Current.Quality == expectedQuality)
                realtimeEvent = evt;
            return ValueTask.CompletedTask;
        });
        await using var historian = new BufferedInMemoryHistorian(eventBus);
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        try
        {
            var activated = await manager.ActivateRuntimeAsync(
                "server-script-qualified-quality",
                1,
                QualityPackage(tagId, qualityName, value: 41));
            Assert.True(activated.Activated);

            await WaitUntilAsync(() =>
                runtime.TryGetCurrent(tagId, out var current) &&
                current!.Quality == expectedQuality &&
                Convert.ToInt32(current.Value) == 41,
                TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => realtimeEvent is not null, TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => historian.WrittenSamples > 0, TimeSpan.FromSeconds(2));

            Assert.True(runtime.TryGetCurrent(tagId, out var current));
            Assert.Equal(expectedQuality, current!.Quality);
            Assert.Equal(41, Convert.ToInt32(current.Value));

            Assert.NotNull(realtimeEvent);
            Assert.Equal(tagId, realtimeEvent!.Tag.Id);
            Assert.Equal(expectedQuality, realtimeEvent.Current.Quality);
            Assert.Equal(41, Convert.ToInt32(realtimeEvent.Current.Value));

            Assert.Contains(runtime.Alarms(activeOnly: true), alarm =>
                alarm.TagId == tagId &&
                alarm.Type == AlarmType.Communication &&
                alarm.State == AlarmState.Active);

            Assert.Contains(
                historian.Query(
                    tagId,
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    DateTimeOffset.UtcNow.AddMinutes(1)),
                sample =>
                    sample.Quality == expectedQuality &&
                    Convert.ToInt32(sample.Value) == 41);
        }
        finally
        {
            await manager.DisposeAsync();
        }
    }

    [Fact]
    public async Task ServerScript_OrdinaryServerMemoryWrite_RemainsGood()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        try
        {
            var activated = await manager.ActivateRuntimeAsync(
                "server-script-value-only-quality",
                1,
                ValueOnlyPackage(tagId, value: 7));
            Assert.True(activated.Activated);

            await WaitUntilAsync(() =>
                runtime.TryGetCurrent(tagId, out var current) &&
                Convert.ToInt32(current!.Value) == 7,
                TimeSpan.FromSeconds(5));

            Assert.True(runtime.TryGetCurrent(tagId, out var current));
            Assert.Equal(TagQuality.Good, current!.Quality);
            Assert.Equal(7, Convert.ToInt32(current.Value));
        }
        finally
        {
            await manager.DisposeAsync();
        }
    }

    [Fact]
    public async Task QualifiedPublish_RequiresDeclaredServerMemoryTagCapability()
    {
        var eventBus = new InMemoryScadaEventBus();
        var tagId = Guid.NewGuid();
        await using var runtime = CreateRuntime(eventBus);
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, Configuration());

        try
        {
            var activated = await manager.ActivateRuntimeAsync(
                "server-script-quality-capability",
                1,
                QualityPackage(
                    tagId,
                    "Bad",
                    value: 99,
                    ScriptEngineeringDependencyKind.Tag));
            Assert.True(activated.Activated);

            await Task.Delay(250);
            Assert.True(runtime.TryGetCurrent(tagId, out var current));
            Assert.Equal(TagQuality.Good, current!.Quality);
            Assert.Equal(0, Convert.ToInt32(current.Value));

            var diagnostics = Assert.Single(manager.Snapshot().Scripts).Diagnostics;
            Assert.True(diagnostics.TotalFailures > 0);
        }
        finally
        {
            await manager.DisposeAsync();
        }
    }

    private static EngineeringRuntimeCoordinator CreateRuntime(InMemoryScadaEventBus eventBus) =>
        new(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2),
            new InMemoryServerMemoryRetentionStore());

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServerScripts:HandlerTimeoutMs"] = "2000",
                ["ServerScripts:MinimumTimerIntervalMs"] = "10",
                ["ServerScripts:MaxQueuedEvents"] = "16",
                ["ServerScripts:MaxConsecutiveFailuresBeforeThrottle"] = "5"
            })
            .Build();

    private static EngineeringPackage QualityPackage(
        Guid tagId,
        string qualityName,
        int value,
        ScriptEngineeringDependencyKind dependencyKind = ScriptEngineeringDependencyKind.ServerMemoryTag)
    {
        var tagReference = tagId.ToString("D");
        var source = $"""
def initialize(event):
    publish_server_memory_sample("{tagReference}", {value}, "{qualityName}")
""";
        return Package(
            tagId,
            source,
            new ScriptEngineeringDependency(dependencyKind, tagReference),
            communicationAlarm: true);
    }

    private static EngineeringPackage ValueOnlyPackage(Guid tagId, int value)
    {
        var tagReference = tagId.ToString("D");
        var source = $"""
def initialize(event):
    write_server_memory("{tagReference}", {value})
""";
        return Package(
            tagId,
            source,
            new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.ServerMemoryTag,
                tagReference),
            communicationAlarm: false);
    }

    private static EngineeringPackage Package(
        Guid tagId,
        string source,
        ScriptEngineeringDependency dependency,
        bool communicationAlarm)
    {
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "Scripts.Quality",
            "Generic Qualified Sample",
            ScriptEngineeringScope.Server,
            source,
            entryPoints: new[]
            {
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.Initialize,
                    "initialize")
            },
            dependencies: new[] { dependency });

        var alarms = communicationAlarm
            ? new[]
            {
                new AlarmEngineeringDto(
                    null,
                    "Process communication",
                    tagId,
                    "Simulation.ProcessValue",
                    AlarmType.Communication,
                    AlarmPriority.High)
            }
            : Array.Empty<AlarmEngineeringDto>();

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "ProcessValue",
                    "Simulation.ProcessValue",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    Historian: new HistorianSettingsDto(true, "on-change"),
                    InitialValue: new MemoryInitialValueDto(
                        TagDataType.Int32,
                        JsonSerializer.SerializeToElement(0)))
            },
            alarms,
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            },
            Scripts: new[] { script });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(condition());
    }
}
