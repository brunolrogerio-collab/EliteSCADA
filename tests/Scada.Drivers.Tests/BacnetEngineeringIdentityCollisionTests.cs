using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Bacnet;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class BacnetEngineeringIdentityCollisionTests
{
    [Fact]
    public void Planner_RejectsDuplicateStableTagIdentityDeterministically()
    {
        var duplicateId = Guid.Parse("2a589d2d-3777-4f15-bdcc-376188345b3b");
        var dataSource = new DataSourceEngineeringDto(
            Guid.Parse("a0c44650-0745-438d-a0b8-b053e32074f7"),
            "ahu-1",
            "AHU 1",
            BacnetDriverDescriptor.DriverType,
            Settings: new Dictionary<string, string> { ["deviceInstance"] = "1201" });
        var tags = new[]
        {
            new TagEngineeringDto(
                duplicateId,
                "TemperatureA",
                "HVAC.A.Temperature",
                TagDataType.Float,
                Source: dataSource.Key,
                Address: "bacnet:device=1201;object=0:1;property=85"),
            new TagEngineeringDto(
                duplicateId,
                "TemperatureB",
                "HVAC.B.Temperature",
                TagDataType.Float,
                Source: dataSource.Key,
                Address: "bacnet:device=1201;object=0:2;property=85")
        };
        var package = new EngineeringPackage(
            "scada.engineering",
            9,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            DataSources: new[] { dataSource });

        var first = BacnetEngineeringRuntimePlanner.Plan(package, dataSource);
        var second = BacnetEngineeringRuntimePlanner.Plan(package, dataSource);

        Assert.False(first.CanActivate);
        Assert.Null(first.Plan);
        var issue = Assert.Single(first.Issues.Where(x => x.Code == "BACNET_TAG_STABLE_ID_DUPLICATE"));
        Assert.True(issue.IsError);
        Assert.Equal(dataSource.Key, issue.DataSourceKey);
        Assert.Equal("HVAC.B.Temperature", issue.TagPath);
        Assert.Contains("HVAC.A.Temperature", issue.Message, StringComparison.Ordinal);
        Assert.Contains(duplicateId.ToString("D"), issue.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            first.Issues.Select(x => (x.Code, x.Message, x.DataSourceKey, x.TagPath, x.IsError)),
            second.Issues.Select(x => (x.Code, x.Message, x.DataSourceKey, x.TagPath, x.IsError)));
    }
}
