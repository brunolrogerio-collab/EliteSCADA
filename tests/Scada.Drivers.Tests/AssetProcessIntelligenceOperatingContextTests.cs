using Scada.Api.Licensing;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceOperatingContextTests
{
    [Fact]
    public void ValidatorAcceptsInclusiveNumericPredicateAndExactTypedPredicates()
    {
        var fixture = CreateFixture();
        var configuration = fixture.Configuration(
        [
            new(fixture.NumericTagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 5, 12),
            new(fixture.BooleanTagId, AssetProcessIntelligenceOperatingContextPredicateKind.BooleanEquals, BooleanValue: true),
            new(fixture.StringTagId, AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals, ExactValue: "RUN"),
            new(fixture.EnumTagId, AssetProcessIntelligenceOperatingContextPredicateKind.EnumEquals, ExactValue: "ModeA")
        ]);

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(
            configuration, fixture.Equipment, null, fixture.Tags);

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(double.NaN, null)]
    [InlineData(null, double.PositiveInfinity)]
    [InlineData(10d, 2d)]
    [InlineData(null, null)]
    public void ValidatorRejectsInvalidNumericBounds(double? minimum, double? maximum)
    {
        var fixture = CreateFixture();
        var configuration = fixture.Configuration(
        [new(fixture.NumericTagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, minimum, maximum)]);

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(
            configuration, fixture.Equipment, null, fixture.Tags);

        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_NUMERIC_BOUNDS_INVALID");
    }

    [Fact]
    public void ValidatorRejectsPredicateTypeMismatchUnknownTagAndTagOutsideAssignedMeasurementProfile()
    {
        var fixture = CreateFixture();
        var unknownTag = Guid.NewGuid();
        var configuration = fixture.Configuration(
        [
            new(fixture.BooleanTagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 0, 1),
            new(unknownTag, AssetProcessIntelligenceOperatingContextPredicateKind.BooleanEquals, BooleanValue: false),
            new(fixture.UnassignedTagId, AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals, ExactValue: "x")
        ]);

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(
            configuration, fixture.Equipment, null, fixture.Tags);

        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TYPE_MISMATCH");
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_TAG_NOT_FOUND");
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_TAG_NOT_IN_ASSIGNED_PROFILE");
    }

    [Fact]
    public void ValidatorRejectsDateTimePredicateAndInvalidContextIdentityOrVersion()
    {
        var fixture = CreateFixture();
        var dateTag = fixture.AddTag(TagDataType.DateTime);
        var profile = fixture.ContextProfile(
            [new(dateTag, AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals, ExactValue: "2026-01-01T00:00:00Z")]) with
            { Id = Guid.Empty, Key = "bad key", DisplayLabel = " ", Version = 0 };
        var configuration = fixture.Configuration([new(fixture.NumericTagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 0, 1)]) with
            { OperatingContextProfiles = [profile] };

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(
            configuration, fixture.Equipment, null, fixture.Tags);

        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_ID_INVALID");
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_KEY_INVALID");
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_LABEL_INVALID");
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_VERSION_INVALID");
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_OPERATING_CONTEXT_PREDICATE_TYPE_MISMATCH");
    }

    [Fact]
    public void Version2CannotCarryContextProfilesAndVersion3DeepCopiesThem()
    {
        var fixture = CreateFixture();
        var predicates = new List<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>
        {
            new(fixture.NumericTagId, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 1, 2)
        };
        var contextProfile = fixture.ContextProfile(predicates);
        var configuration = fixture.Configuration(predicates) with { Version = 2, OperatingContextProfiles = [contextProfile] };

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(configuration, fixture.Equipment, null, fixture.Tags);
        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_V2_CONTEXT_PROFILES_NOT_SUPPORTED");

        var valid = configuration with { Version = 3 };
        var registry = new InMemoryAssetProcessIntelligenceEngineeringRegistry();
        registry.Replace(valid);
        predicates[0] = predicates[0] with { Minimum = 999 };
        var snapshot = registry.Snapshot();

        Assert.Equal(1, Assert.Single(snapshot.OperatingContextProfiles!).Version);
        Assert.Equal(1, Assert.Single(snapshot.OperatingContextProfiles!.Single().Predicates!).Minimum);
    }

    [Fact]
    public void EligibilityRequiresExactSynchronizedTimestampAndEveryPredicateAtThatTimestamp()
    {
        var window = new AssetProcessIntelligenceAnalyticsWindow(
            DateTimeOffset.Parse("2026-10-19T00:00:00Z"), DateTimeOffset.Parse("2026-10-19T01:00:00Z"));
        var targetTag = TagDefinition.Create("Target", "Plant.Target", TagDataType.Double);
        var conditionTag = TagDefinition.Create("Mode", "Plant.Mode", TagDataType.Boolean);
        var targetSignal = new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "target", "process", targetTag.Id);
        var conditionSignal = new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "mode", "condition", conditionTag.Id);
        var sourceAt = DateTimeOffset.Parse("2026-10-19T00:10:00Z");
        var rows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetSignal.Id] = [Observation(sourceAt, HistoricalQueryValue.FromDouble(10)), Observation(sourceAt.AddMinutes(1), HistoricalQueryValue.FromDouble(11))],
            [conditionSignal.Id] = [Observation(sourceAt, HistoricalQueryValue.FromBoolean(true)), Observation(sourceAt.AddMinutes(1), HistoricalQueryValue.FromBoolean(false))]
        };
        var predicate = new AssetProcessIntelligenceOperatingContextPredicateEngineeringDto(
            conditionTag.Id, AssetProcessIntelligenceOperatingContextPredicateKind.BooleanEquals, BooleanValue: true);

        var result = AssetProcessIntelligenceContextEligibilityV1Calculator.Calculate(
            [(targetSignal, targetTag), (conditionSignal, conditionTag)], [predicate], window, rows);

        Assert.Equal(1, result.Signals[targetSignal.Id].ContextEligibleObservationCount);
        Assert.Equal(2, result.Signals[targetSignal.Id].AcceptedSynchronizedUniqueCount);
        Assert.Equal(1, result.PredicateMatched[conditionTag.Id]);
        Assert.Equal(0, result.Signals[conditionSignal.Id].ContextEligibleObservationCount);
    }

    [Fact]
    public void NumericIntervalUsesInclusiveFiniteBounds()
    {
        var window = new AssetProcessIntelligenceAnalyticsWindow(
            DateTimeOffset.Parse("2026-10-19T00:00:00Z"), DateTimeOffset.Parse("2026-10-19T01:00:00Z"));
        var tag = TagDefinition.Create("Pressure", "Plant.Pressure", TagDataType.Double);
        var signal = new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "pressure", "process", tag.Id);
        var timestamps = new[]
        {
            DateTimeOffset.Parse("2026-10-19T00:10:00Z"),
            DateTimeOffset.Parse("2026-10-19T00:11:00Z"),
            DateTimeOffset.Parse("2026-10-19T00:12:00Z")
        };
        var rows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [signal.Id] =
            [
                Observation(timestamps[0], HistoricalQueryValue.FromDouble(5)),
                Observation(timestamps[1], HistoricalQueryValue.FromDouble(12)),
                Observation(timestamps[2], HistoricalQueryValue.FromDouble(12.0001))
            ]
        };
        var predicate = new AssetProcessIntelligenceOperatingContextPredicateEngineeringDto(
            tag.Id, AssetProcessIntelligenceOperatingContextPredicateKind.NumericInterval, 5, 12);

        var result = AssetProcessIntelligenceContextEligibilityV1Calculator.Calculate(
            [(signal, tag)], [predicate], window, rows);

        Assert.Equal(2, result.PredicateMatched[tag.Id]);
        Assert.Equal(2, result.Signals[signal.Id].ContextEligibleObservationCount);
    }

    [Fact]
    public void EligibilityRejectsDuplicateUnsynchronizedAndPoorQualityEvidenceWithoutImputation()
    {
        var window = new AssetProcessIntelligenceAnalyticsWindow(
            DateTimeOffset.Parse("2026-10-19T00:00:00Z"), DateTimeOffset.Parse("2026-10-19T01:00:00Z"));
        var targetTag = TagDefinition.Create("Target", "Plant.Target", TagDataType.Double);
        var conditionTag = TagDefinition.Create("Mode", "Plant.Mode", TagDataType.String);
        var targetSignal = new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "target", "process", targetTag.Id);
        var conditionSignal = new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "mode", "condition", conditionTag.Id);
        var t1 = DateTimeOffset.Parse("2026-10-19T00:10:00Z");
        var t2 = t1.AddMinutes(1);
        var duplicate = t1.AddMinutes(2);
        var rows = new Dictionary<Guid, IReadOnlyList<AssetProcessIntelligenceContextualDeviationVectorRawObservation>>
        {
            [targetSignal.Id] = [Observation(t1, HistoricalQueryValue.FromDouble(10)), Observation(t2, HistoricalQueryValue.FromDouble(11), evidence: "Unsynchronized"), Observation(duplicate, HistoricalQueryValue.FromDouble(12)), Observation(duplicate, HistoricalQueryValue.FromDouble(13))],
            [conditionSignal.Id] = [Observation(t1, HistoricalQueryValue.FromString("RUN"), quality: "Bad")]
        };
        var predicate = new AssetProcessIntelligenceOperatingContextPredicateEngineeringDto(
            conditionTag.Id, AssetProcessIntelligenceOperatingContextPredicateKind.StringEquals, ExactValue: "RUN");

        var result = AssetProcessIntelligenceContextEligibilityV1Calculator.Calculate(
            [(targetSignal, targetTag), (conditionSignal, conditionTag)], [predicate], window, rows);

        Assert.Equal(0, result.Signals[targetSignal.Id].ContextEligibleObservationCount);
        Assert.Equal(1, result.Signals[targetSignal.Id].AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, result.Signals[targetSignal.Id].AmbiguousSourceTimestampRowCount);
        Assert.Equal(0, result.PredicateMatched[conditionTag.Id]);
        Assert.Equal(1, result.Signals[conditionSignal.Id].RejectionCounts["poor_or_missing_quality"]);
    }

    private static AssetProcessIntelligenceContextualDeviationVectorRawObservation Observation(
        DateTimeOffset timestamp,
        HistoricalQueryValue value,
        string quality = "Good",
        string evidence = "Synchronized") => new(timestamp, quality, value, timestamp, evidence);

    private static TestFixture CreateFixture()
    {
        var equipmentId = Guid.NewGuid();
        var equipment = new EquipmentEngineeringDto(equipmentId, "Plant.Pump", "Pump");
        var numericTagId = Guid.NewGuid();
        var booleanTagId = Guid.NewGuid();
        var stringTagId = Guid.NewGuid();
        var enumTagId = Guid.NewGuid();
        var unassignedTagId = Guid.NewGuid();
        var tags = new List<TagEngineeringDto>
        {
            Tag(numericTagId, "Numeric", TagDataType.Double),
            Tag(booleanTagId, "Boolean", TagDataType.Boolean),
            Tag(stringTagId, "String", TagDataType.String),
            Tag(enumTagId, "Enum", TagDataType.Enum),
            Tag(unassignedTagId, "Unassigned", TagDataType.String)
        };
        var profileId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var measurement = new AssetProcessIntelligenceProfileEngineeringDto(profileId, "pump-measurements", "Pump measurements",
        [
            new(Guid.NewGuid(), "numeric", "process", numericTagId),
            new(Guid.NewGuid(), "boolean", "state", booleanTagId),
            new(Guid.NewGuid(), "string", "state", stringTagId),
            new(Guid.NewGuid(), "enum", "state", enumTagId)
        ]);
        var assignment = new AssetProcessIntelligenceAssignmentEngineeringDto(assignmentId, equipmentId, profileId);
        return new TestFixture(equipmentId, equipment, numericTagId, booleanTagId, stringTagId, enumTagId,
            unassignedTagId, tags, measurement, assignment);
    }

    private static TagEngineeringDto Tag(Guid id, string name, TagDataType type) =>
        new(id, name, $"Plant.Pump.{name}", type, ReadOnly: true);

    private sealed record TestFixture(
        Guid EquipmentId,
        EquipmentEngineeringDto EquipmentResource,
        Guid NumericTagId,
        Guid BooleanTagId,
        Guid StringTagId,
        Guid EnumTagId,
        Guid UnassignedTagId,
        List<TagEngineeringDto> Tags,
        AssetProcessIntelligenceProfileEngineeringDto Measurement,
        AssetProcessIntelligenceAssignmentEngineeringDto Assignment)
    {
        public IReadOnlyCollection<EquipmentEngineeringDto> Equipment => [EquipmentResource];

        public AssetProcessIntelligenceOperatingContextProfileEngineeringDto ContextProfile(
            IReadOnlyCollection<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates) =>
            new(Guid.NewGuid(), "operating", "Operating", Assignment.Id, 1, predicates);

        public AssetProcessIntelligenceEngineeringDto Configuration(
            IReadOnlyCollection<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto> predicates) =>
            new(3, [], [Measurement], [Assignment], [], [ContextProfile(predicates)]);

        public Guid AddTag(TagDataType dataType)
        {
            var tagId = Guid.NewGuid();
            Tags.Add(Tag(tagId, $"DateTime-{tagId:N}", dataType));
            return tagId;
        }
    }
}
