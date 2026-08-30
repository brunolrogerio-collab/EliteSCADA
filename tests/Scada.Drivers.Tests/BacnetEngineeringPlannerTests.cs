using System.IO.BACnet;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Bacnet;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class BacnetEngineeringPlannerTests
{
    [Fact]
    public void Planner_RebuildsCanonicalPointFromEngineeringAddress()
    {
        var tagId = Guid.NewGuid();
        var package = Package(
            new TagEngineeringDto(
                tagId,
                "RoomTemp",
                "HVAC.RoomTemp",
                TagDataType.Float,
                Source: "ahu-1",
                Address: "bacnet:device=1201;object=0:37;property=85",
                ReadOnly: true,
                Metadata: new Dictionary<string, string> { ["bacnet.useCov"] = "false" }),
            new DataSourceEngineeringDto(
                Guid.NewGuid(),
                "ahu-1",
                "AHU 1",
                BacnetDriverDescriptor.DriverType,
                Settings: new Dictionary<string, string> { ["deviceInstance"] = "1201" }));

        var result = BacnetEngineeringRuntimePlanner.Plan(package, package.DataSources!.Single());

        Assert.True(result.CanActivate, string.Join("; ", result.Issues.Select(x => x.Message)));
        Assert.Equal(BacnetDriverDescriptor.DriverType, result.Plan!.DriverType);
        Assert.Equal(tagId, Assert.Single(result.Plan.Tags).Id);
        var point = Assert.Single(result.Plan.Points);
        Assert.Equal(tagId, point.Tag.Id);
        Assert.Equal(1201u, point.Binding.DeviceInstance);
        Assert.Equal(37u, point.Binding.ObjectInstance);
        Assert.Equal(85u, point.Binding.PropertyIdentifier);
        Assert.False(point.Binding.UseCov);
        Assert.False(point.Writable);
    }

    [Fact]
    public void BindingSchemaIdentity_IsStableForFutureRichCommunicationBinding()
    {
        Assert.Equal("scada.driver.bacnet.ip.binding", BacnetBinding.BindingSchemaId);
        Assert.Equal(1, BacnetBinding.BindingSchemaVersion);
    }

    [Fact]
    public void Planner_RejectsTagBoundToDifferentDeviceInstance()
    {
        var package = Package(
            new TagEngineeringDto(
                Guid.NewGuid(),
                "Pressure",
                "HVAC.Pressure",
                TagDataType.Float,
                Source: "ahu-1",
                Address: "bacnet:device=2202;object=0:1;property=85"),
            new DataSourceEngineeringDto(
                Guid.NewGuid(),
                "ahu-1",
                "AHU 1",
                BacnetDriverDescriptor.DriverType,
                Settings: new Dictionary<string, string> { ["deviceInstance"] = "1201" }));

        var result = BacnetEngineeringRuntimePlanner.Plan(package, package.DataSources!.Single());

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, x => x.Code == "BACNET_TAG_DEVICE_MISMATCH" && x.IsError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Planner_RejectsMissingOrEmptyStableTagIdentity(bool useEmptyGuid)
    {
        var package = Package(
            new TagEngineeringDto(
                useEmptyGuid ? Guid.Empty : null,
                "UnstablePoint",
                "HVAC.UnstablePoint",
                TagDataType.Float,
                Source: "ahu-1",
                Address: "bacnet:device=1201;object=0:1;property=85"),
            new DataSourceEngineeringDto(
                Guid.NewGuid(),
                "ahu-1",
                "AHU 1",
                BacnetDriverDescriptor.DriverType,
                Settings: new Dictionary<string, string> { ["deviceInstance"] = "1201" }));

        var first = BacnetEngineeringRuntimePlanner.Plan(package, package.DataSources!.Single());
        var second = BacnetEngineeringRuntimePlanner.Plan(package, package.DataSources!.Single());

        Assert.False(first.CanActivate);
        Assert.Null(first.Plan);
        Assert.False(second.CanActivate);
        Assert.Null(second.Plan);
        Assert.Contains(first.Issues, x =>
            x.Code == "BACNET_TAG_STABLE_ID_REQUIRED" &&
            x.IsError &&
            x.TagPath == "HVAC.UnstablePoint");
        Assert.Equal(
            first.Issues.Select(x => (x.Code, x.Message, x.DataSourceKey, x.TagPath, x.IsError)),
            second.Issues.Select(x => (x.Code, x.Message, x.DataSourceKey, x.TagPath, x.IsError)));
    }

    [Fact]
    public void Codec_MapsBinaryPresentValueWithoutGenericBooleanCoercion()
    {
        var binary = new BacnetBinding(1, 3, 5, 85);
        Assert.True((bool)BacnetValueCodec.Decode(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, 1u), TagDataType.Boolean, binary)!);
        Assert.False((bool)BacnetValueCodec.Decode(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, 0u), TagDataType.Boolean, binary)!);
        Assert.Throws<InvalidOperationException>(() =>
            BacnetValueCodec.Decode(new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, 1u), TagDataType.Boolean, new BacnetBinding(1, 0, 5, 85)));
    }

    [Fact]
    public void Codec_EncodesBinaryPresentValueAsEnumerated()
    {
        var values = BacnetValueCodec.Encode(true, TagDataType.Boolean, new BacnetBinding(1, 4, 5, 85));
        var value = Assert.Single(values);
        Assert.Equal(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, value.Tag);
        Assert.Equal(1u, value.Value);
    }

    private static EngineeringPackage Package(TagEngineeringDto tag, DataSourceEngineeringDto dataSource)
        => new(
            "scada.engineering",
            9,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            DataSources: new[] { dataSource });
}
