using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixL5kImporterTests
{
    [Fact]
    public async Task Import_PreservesControllerProgramTypeAccessAndConstantEvidence()
    {
        const string l5k = """
            IE_VER := 2.20;
            CONTROLLER DemoController
            TAG
            TankLevel : DINT (RADIX := Decimal, ExternalAccess := Read/Write, Constant := No) := 123;
            FrozenSetpoint : REAL (RADIX := Float, ExternalAccess := Read Only, Constant := Yes) := 42.5;
            END_TAG

            PROGRAM Packaging (MODE := 0, DisableFlag := 0)
            TAG
            Running : BOOL (RADIX := Binary, ExternalAccess := Read/Write) := 0;
            RecipeName : STRING (ExternalAccess := Read Only) := [0,''];
            END_TAG
            END_PROGRAM
            END_CONTROLLER
            """;

        var candidates = await ImportAsync(l5k);

        Assert.Equal(4, candidates.Count);

        var level = Assert.Single(candidates, static candidate => candidate.DisplayName == "TankLevel");
        Assert.Equal("controller:TankLevel", level.StableIdentity);
        Assert.True(level.IsReadable);
        Assert.True(level.IsWritable);
        Assert.Equal(TagDataType.Int32, level.SuggestedDataType);
        Assert.Equal("ReadWrite", level.Metadata!["externalAccess"]);

        var frozen = Assert.Single(candidates, static candidate => candidate.DisplayName == "FrozenSetpoint");
        Assert.True(frozen.IsReadable);
        Assert.False(frozen.IsWritable);
        Assert.Equal("True", frozen.Metadata!["constant"]);

        var running = Assert.Single(candidates, static candidate => candidate.DisplayName == "Running");
        Assert.Equal("program:Packaging:Running", running.StableIdentity);
        Assert.True(running.IsReadable);
        Assert.False(running.IsWritable);
        Assert.Contains(running.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_BOOL_DIRECT_WRITE_DEFERRED");

        var stringTag = Assert.Single(candidates, static candidate => candidate.DisplayName == "RecipeName");
        Assert.Equal(TagDataType.String, stringTag.SuggestedDataType);
        Assert.False(stringTag.IsReadable);
        Assert.Contains(stringTag.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_TYPE_RUNTIME_UNSUPPORTED");
    }

    [Fact]
    public async Task Import_PreservesArraysAndStructuresWithoutPublishingRuntimeStateOrForceData()
    {
        const string l5k = """
            CONTROLLER Demo
            TAG
            Samples : INT[3] (RADIX := Decimal, ExternalAccess := Read/Write) := [1,2,3];
            ForceMe : DINT (RADIX := Decimal, ExternalAccess := Read/Write) := 77,
                TagForceData := [0,0,0,0,1,0,-1,-1,1,0,-72,34];
            Motor : MotorUDT (ExternalAccess := Read/Write) := [1,2,3];
            END_TAG
            END_CONTROLLER
            """;

        var candidates = await ImportAsync(l5k);

        var array = Assert.Single(candidates, static candidate => candidate.DisplayName == "Samples");
        Assert.False(array.IsReadable);
        Assert.False(array.IsWritable);
        Assert.Equal("3", array.Metadata!["dimensions"]);
        Assert.Contains(array.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_ARRAY_BINDING_REQUIRES_ELEMENT");

        var force = Assert.Single(candidates, static candidate => candidate.DisplayName == "ForceMe");
        Assert.True(force.IsReadable);
        Assert.True(force.IsWritable);
        Assert.DoesNotContain(force.Metadata!.Keys, static key => key.Contains("force", StringComparison.OrdinalIgnoreCase) || key.Contains("value", StringComparison.OrdinalIgnoreCase));

        var udt = Assert.Single(candidates, static candidate => candidate.DisplayName == "Motor");
        Assert.False(udt.IsReadable);
        Assert.False(udt.IsWritable);
        Assert.Contains(udt.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_L5K_TYPE_UNSUPPORTED");
    }

    [Fact]
    public async Task Import_ResolvesOnlySimpleAliasesAndFailsClosedForMemberAliases()
    {
        const string l5k = """
            CONTROLLER Demo
            TAG
            BaseWord : DINT (RADIX := Decimal, ExternalAccess := Read Only) := 0;
            SimpleAlias OF BaseWord (RADIX := Decimal);
            BitAlias OF BaseWord.0 (RADIX := Binary);
            END_TAG
            END_CONTROLLER
            """;

        var candidates = await ImportAsync(l5k);

        var simple = Assert.Single(candidates, static candidate => candidate.DisplayName == "SimpleAlias");
        Assert.Equal("controller:SimpleAlias", simple.StableIdentity);
        Assert.True(simple.IsReadable);
        Assert.False(simple.IsWritable);
        Assert.Equal(TagDataType.Int32, simple.SuggestedDataType);
        Assert.Equal("BaseWord", simple.Metadata!["aliasFor"]);
        Assert.Contains(simple.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_L5K_ALIAS_RESOLVED");

        var bit = Assert.Single(candidates, static candidate => candidate.DisplayName == "BitAlias");
        Assert.False(bit.IsReadable);
        Assert.False(bit.IsWritable);
        Assert.Equal("BaseWord.0", bit.Metadata!["aliasFor"]);
        Assert.Contains(bit.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_L5K_ALIAS_REQUIRES_RESOLUTION");
    }

    [Fact]
    public async Task Import_FailsClosedForSafetyAndBoundViolations()
    {
        const string safety = """
            CONTROLLER Demo
            TAG
            SafeValue : DINT (Class := Safety, ExternalAccess := Read/Write) := 1;
            END_TAG
            END_CONTROLLER
            """;

        var safetyCandidates = await ImportAsync(safety);
        var safe = Assert.Single(safetyCandidates);
        Assert.False(safe.IsReadable);
        Assert.False(safe.IsWritable);
        Assert.Contains(safe.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_SAFETY_TAG_UNSUPPORTED");

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(safety));
        var bounded = new List<DriverImportCandidate>();
        await foreach (var candidate in LogixL5kImporter.ImportAsync(
                           new DriverImportRequest(null, "too-large.L5K", "text/plain"),
                           stream,
                           maximumSourceChars: 16))
        {
            bounded.Add(candidate);
        }

        var error = Assert.Single(bounded);
        Assert.Contains(error.Issues ?? Array.Empty<DriverEngineeringIssue>(), static issue => issue.Code == "LOGIX_L5K_SOURCE_TOO_LARGE" && issue.Severity == DriverEngineeringIssueSeverity.Error);
    }

    private static async Task<List<DriverImportCandidate>> ImportAsync(string source)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var candidates = new List<DriverImportCandidate>();
        await foreach (var candidate in LogixL5kImporter.ImportAsync(
                           new DriverImportRequest(null, "demo.L5K", "text/plain"),
                           stream))
        {
            candidates.Add(candidate);
        }
        return candidates;
    }
}
