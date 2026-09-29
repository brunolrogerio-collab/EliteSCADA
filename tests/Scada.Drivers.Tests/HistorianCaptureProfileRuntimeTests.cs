using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class HistorianCaptureProfileRuntimeTests
{
    [Fact]
    public void Resolve_TagsAssignedToSameProfileReceiveSameEffectivePolicy()
    {
        var profileId = Guid.NewGuid();
        var profile = Profile(
            profileId,
            HistorianCaptureStrategy.OnChangeDeadbandMaxInterval,
            deadband: 0.5d,
            maximumIntervalMilliseconds: 30_000);
        var first = Tag("Plant.PressureA", TagDataType.Double, profileId);
        var second = Tag("Plant.PressureB", TagDataType.Double, profileId);
        var package = Package([first, second], [profile]);

        var result = HistorianCaptureProfileRuntimeResolver.Resolve(package);

        Assert.True(result.CanActivate);
        Assert.Empty(result.Issues);
        var tags = result.Package.Tags.OrderBy(x => x.Path).ToArray();
        Assert.All(tags, tag =>
        {
            Assert.NotNull(tag.Historian);
            Assert.True(tag.Historian!.Enabled);
            Assert.Equal("onChangeDeadbandMaxInterval", tag.Historian.Strategy);
            Assert.Equal(0.5d, tag.Historian.Deadband);
            Assert.Equal(30_000, tag.Historian.MaximumPeriodMilliseconds);
        });
    }

    [Fact]
    public void Resolve_TagWithoutProfilePreservesLegacyInlineHistorianSettings()
    {
        var inline = new HistorianSettingsDto(
            Enabled: true,
            Strategy: "change",
            Deadband: 1.25d,
            PeriodMilliseconds: 500,
            MaximumPeriodMilliseconds: 5_000);
        var tag = Tag("Plant.Legacy", TagDataType.Double, profileId: null) with { Historian = inline };

        var result = HistorianCaptureProfileRuntimeResolver.Resolve(Package([tag], []));

        Assert.True(result.CanActivate);
        Assert.Same(inline, Assert.Single(result.Package.Tags).Historian);
    }

    [Fact]
    public void Resolve_RejectsDeadbandProfileAssignedToBooleanTag()
    {
        var profileId = Guid.NewGuid();
        var profile = Profile(
            profileId,
            HistorianCaptureStrategy.OnChangeDeadband,
            deadband: 1d);
        var tag = Tag("Plant.Running", TagDataType.Boolean, profileId);

        var result = HistorianCaptureProfileRuntimeResolver.Resolve(Package([tag], [profile]));

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "HISTORIAN_CAPTURE_DEADBAND_TYPE_INCOMPATIBLE" &&
            issue.TagPath == tag.Path);
    }

    [Fact]
    public void Resolve_WorkingProfileEditDoesNotMutateAlreadyResolvedActivePolicy()
    {
        var profileId = Guid.NewGuid();
        var tag = Tag("Plant.Pressure", TagDataType.Double, profileId);
        var activePackage = Package(
            [tag],
            [Profile(profileId, HistorianCaptureStrategy.OnChangeDeadband, deadband: 1d)]);

        var active = HistorianCaptureProfileRuntimeResolver.Resolve(activePackage);
        Assert.True(active.CanActivate);
        Assert.Equal(1d, Assert.Single(active.Package.Tags).Historian!.Deadband);

        // Represents a Working/Preview edit. It is only data until lifecycle activation
        // calls the resolver again; the already resolved Active package is immutable.
        var working = activePackage with
        {
            HistorianCaptureProfiles =
            [
                Profile(profileId, HistorianCaptureStrategy.OnChangeDeadband, deadband: 5d)
            ]
        };

        Assert.Equal(1d, Assert.Single(active.Package.Tags).Historian!.Deadband);

        var nextActivation = HistorianCaptureProfileRuntimeResolver.Resolve(working);
        Assert.True(nextActivation.CanActivate);
        Assert.Equal(5d, Assert.Single(nextActivation.Package.Tags).Historian!.Deadband);
        Assert.Equal(1d, Assert.Single(active.Package.Tags).Historian!.Deadband);
    }

    [Fact]
    public void Resolve_ProfileReferenceMustExistInActivePackage()
    {
        var missingId = Guid.NewGuid();
        var tag = Tag("Plant.Orphan", TagDataType.Double, missingId);

        var result = HistorianCaptureProfileRuntimeResolver.Resolve(Package([tag], []));

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, issue => issue.Code == "HISTORIAN_CAPTURE_PROFILE_NOT_FOUND");
    }

    private static HistorianCaptureProfileEngineeringDto Profile(
        Guid id,
        HistorianCaptureStrategy strategy,
        int? periodMilliseconds = null,
        double? deadband = null,
        int? maximumIntervalMilliseconds = null) =>
        new(
            id,
            $"profile.{id:N}",
            $"Profile {id:N}",
            strategy,
            PeriodMilliseconds: periodMilliseconds,
            Deadband: deadband,
            MaximumIntervalMilliseconds: maximumIntervalMilliseconds);

    private static TagEngineeringDto Tag(string path, TagDataType dataType, Guid? profileId) =>
        new(
            Guid.NewGuid(),
            path.Split('.').Last(),
            path,
            dataType,
            Source: "memory.server",
            ReadOnly: false,
            HistorianCaptureProfileId: profileId);

    private static EngineeringPackage Package(
        IReadOnlyCollection<TagEngineeringDto> tags,
        IReadOnlyCollection<HistorianCaptureProfileEngineeringDto> profiles) =>
        new(
            "scada.engineering",
            20,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            HistorianCaptureProfiles: profiles);
}
