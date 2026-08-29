using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Views;
using Scada.Engineering.VisualScripting;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlVisualDynamicPersistenceTests
{
    [Fact]
    public async Task RevisionPersistence_PreservesVisualExpressionConditionAndAnalogFill()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var tags = new InMemoryTagRegistry();
        var level = TagDefinition.Create("Level", "Plant.Level", TagDataType.Double);
        tags.Register(level);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var views = new InMemoryEngineeringViewRegistry();
        views.UpsertScreen(new ScreenEngineeringDto(
            null,
            "plant.level",
            "Plant Level",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "tank",
                    BuiltinVisualObjectSchemas.RectangleType,
                    PropertyExpressions:
                    [
                        new VisualPropertyExpressionEngineeringDto(
                            VisualPropertyKeys.Opacity,
                            new VisualExpressionEngineeringDto(
                                "level / 100",
                                VisualExpressionValueType.Number,
                                [new VisualExpressionDependencyEngineeringDto(
                                    "level",
                                    VisualExpressionDependencyKind.Tag,
                                    VisualExpressionValueType.Number,
                                    new TagValueReference(level.Id),
                                    level.Path)]))
                    ],
                    BooleanConditions:
                    [
                        new VisualBooleanConditionEngineeringDto(
                            VisualPropertyKeys.Visible,
                            VisualBooleanConditionKind.NumericInterval,
                            new VisualValueSourceEngineeringDto(
                                VisualValueSourceKind.Tag,
                                VisualExpressionValueType.Number,
                                level.Path,
                                new TagValueReference(level.Id)),
                            Minimum: 10,
                            Maximum: 90)
                    ],
                    AnalogFill: new VisualAnalogFillEngineeringDto(
                        new VisualValueSourceEngineeringDto(
                            VisualValueSourceKind.Tag,
                            VisualExpressionValueType.Number,
                            level.Path,
                            new TagValueReference(level.Id)),
                        0,
                        100,
                        "#3366FF",
                        Direction: VisualAnalogFillDirection.LeftToRight))
            ]));

        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            views);
        await using var store = new PostgreSqlEngineeringProjectStore(connectionString);
        await store.InitializeAsync();
        var persistence = new EngineeringProjectPersistenceService(exchange, store);
        var projectKey = $"visual-dynamic-{Guid.NewGuid():N}";

        var saved = await persistence.SaveCurrentAsync(projectKey, "Visual Dynamic", "follow-b-dev2");
        var stored = await store.LoadRevisionAsync(projectKey, saved.Revision);

        Assert.NotNull(stored);
        var parsed = exchange.ParseJson(stored!.EngineeringJson);
        var element = Assert.Single(Assert.Single(parsed.Screens!).Elements!);
        Assert.Equal("level / 100", Assert.Single(element.PropertyExpressions!).Expression.Text);
        Assert.Equal(10, Assert.Single(element.BooleanConditions!).Minimum);
        Assert.Equal("#3366FF", element.AnalogFill!.FillColor);
        Assert.Equal(VisualAnalogFillDirection.LeftToRight, element.AnalogFill.Direction);
        Assert.Equal(level.Id, element.AnalogFill.Source.TagReference!.TagId);
    }
}
