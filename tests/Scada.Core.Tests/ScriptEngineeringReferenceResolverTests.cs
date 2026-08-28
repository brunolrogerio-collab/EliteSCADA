using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Scripts;

namespace Scada.Core.Tests;

public sealed class ScriptEngineeringReferenceResolverTests
{
    [Fact]
    public void Create_ClassifiesSharedClientMemoryServerMemoryAndVisualReferencesDeterministically()
    {
        var processTagId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var clientTagId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var serverTagId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var screenId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        var resolver = ScriptEngineeringReferenceResolver.Create(
            [
                new TagEngineeringDto(serverTagId, "Server State", "Server.State", TagDataType.Int32, "server-memory"),
                new TagEngineeringDto(processTagId, "Pressure", "Plant.Pressure", TagDataType.Double, "plc-a"),
                new TagEngineeringDto(clientTagId, "Selected Pump", "Client.SelectedPump", TagDataType.String, "client-memory")
            ],
            [
                new DataSourceEngineeringDto(Guid.NewGuid(), "server-memory", "Server Memory", "builtin.memory.server"),
                new DataSourceEngineeringDto(Guid.NewGuid(), "plc-a", "PLC A", "modbus.tcp"),
                new DataSourceEngineeringDto(Guid.NewGuid(), "client-memory", "Client Memory", "builtin.memory.client")
            ],
            [new ScriptEngineeringVisualDefinitionIdentity(screenId, "screen", "overview")]);

        var processReference = ScriptEngineeringReferenceKeys.Tag(processTagId);
        var clientReference = ScriptEngineeringReferenceKeys.Tag(clientTagId);
        var serverReference = ScriptEngineeringReferenceKeys.Tag(serverTagId);
        var visualReference = ScriptEngineeringReferenceKeys.VisualDefinition(screenId);

        Assert.True(resolver.Resolve(ScriptEngineeringDependencyKind.Tag, processReference).IsResolved);
        Assert.True(resolver.Resolve(ScriptEngineeringDependencyKind.ClientMemoryTag, clientReference).IsResolved);
        Assert.True(resolver.Resolve(ScriptEngineeringDependencyKind.Tag, serverReference).IsResolved);
        Assert.True(resolver.Resolve(ScriptEngineeringDependencyKind.ServerMemoryTag, serverReference).IsResolved);
        Assert.True(resolver.Resolve(ScriptEngineeringDependencyKind.VisualDefinition, visualReference).IsResolved);

        Assert.DoesNotContain(
            resolver.References,
            reference => reference.StableReference == clientReference && reference.Kind == ScriptEngineeringDependencyKind.Tag);
        Assert.Contains(
            resolver.References,
            reference => reference.StableReference == serverReference && reference.Kind == ScriptEngineeringDependencyKind.Tag);

        var ordered = resolver.References
            .OrderBy(reference => (int)reference.Kind)
            .ThenBy(reference => reference.StableReference, StringComparer.Ordinal)
            .ThenBy(reference => reference.EntityPath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(reference => reference.EntityId)
            .ToArray();
        Assert.Equal(ordered, resolver.References.ToArray());
        Assert.Empty(resolver.CatalogIssues);
    }

    [Fact]
    public void Resolve_NormalizesStableIdsAndExplainsKindMismatchMissingAndInvalidReferences()
    {
        var clientTagId = Guid.Parse("10000000-0000-0000-0000-000000000010");
        var resolver = ScriptEngineeringReferenceResolver.Create(
            [new TagEngineeringDto(clientTagId, "Selection", "Client.Selection", TagDataType.String, "client-memory")],
            [new DataSourceEngineeringDto(Guid.NewGuid(), "client-memory", "Client Memory", "builtin.memory.client")]);

        var upperCaseReference = clientTagId.ToString("D").ToUpperInvariant();
        var resolved = resolver.Resolve(ScriptEngineeringDependencyKind.ClientMemoryTag, upperCaseReference);

        Assert.True(resolved.IsResolved);
        Assert.Equal(clientTagId.ToString("D"), resolved.StableReference);

        var wrongKind = resolver.Resolve(ScriptEngineeringDependencyKind.Tag, upperCaseReference);
        Assert.False(wrongKind.IsResolved);
        Assert.Equal("SCRIPT_REFERENCE_KIND_MISMATCH", wrongKind.DiagnosticCode);
        Assert.Contains(nameof(ScriptEngineeringDependencyKind.ClientMemoryTag), wrongKind.DiagnosticMessage!);

        var missing = resolver.Resolve(ScriptEngineeringDependencyKind.Tag, Guid.NewGuid().ToString("D"));
        Assert.Equal("SCRIPT_REFERENCE_MISSING", missing.DiagnosticCode);

        var malformed = resolver.Resolve(ScriptEngineeringDependencyKind.VisualObject, "not-a-visual-object-reference");
        Assert.Equal("SCRIPT_REFERENCE_FORMAT_INVALID", malformed.DiagnosticCode);
    }

    [Fact]
    public void ResolveForScope_EnforcesClientLocalMemoryAndVisualIsolationWithoutHidingSharedServerTags()
    {
        var processTagId = Guid.Parse("10000000-0000-0000-0000-000000000020");
        var clientTagId = Guid.Parse("10000000-0000-0000-0000-000000000021");
        var serverTagId = Guid.Parse("10000000-0000-0000-0000-000000000022");
        var visualId = Guid.Parse("20000000-0000-0000-0000-000000000020");

        var resolver = ScriptEngineeringReferenceResolver.Create(
            [
                new TagEngineeringDto(processTagId, "Pressure", "Plant.Pressure", TagDataType.Double, "plc"),
                new TagEngineeringDto(clientTagId, "Selection", "Client.Selection", TagDataType.String, "client-memory"),
                new TagEngineeringDto(serverTagId, "Counter", "Server.Counter", TagDataType.Int32, "server-memory")
            ],
            [
                new DataSourceEngineeringDto(Guid.NewGuid(), "plc", "PLC", "modbus.tcp"),
                new DataSourceEngineeringDto(Guid.NewGuid(), "client-memory", "Client Memory", "builtin.memory.client"),
                new DataSourceEngineeringDto(Guid.NewGuid(), "server-memory", "Server Memory", "builtin.memory.server")
            ],
            [new ScriptEngineeringVisualDefinitionIdentity(visualId, "screen", "overview")]);

        Assert.True(resolver.ResolveForScope(
            ScriptEngineeringScope.ClientVisual,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Tag, processTagId.ToString("D"))).IsResolved);
        Assert.True(resolver.ResolveForScope(
            ScriptEngineeringScope.ClientVisual,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.ClientMemoryTag, clientTagId.ToString("D"))).IsResolved);
        Assert.True(resolver.ResolveForScope(
            ScriptEngineeringScope.ClientVisual,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Tag, serverTagId.ToString("D"))).IsResolved);
        Assert.True(resolver.ResolveForScope(
            ScriptEngineeringScope.ClientVisual,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.VisualDefinition, visualId.ToString("D"))).IsResolved);

        var clientToServerSpecific = resolver.ResolveForScope(
            ScriptEngineeringScope.ClientVisual,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.ServerMemoryTag, serverTagId.ToString("D")));
        Assert.Equal("SCRIPT_REFERENCE_SCOPE_INVALID", clientToServerSpecific.DiagnosticCode);

        Assert.True(resolver.ResolveForScope(
            ScriptEngineeringScope.Server,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.ServerMemoryTag, serverTagId.ToString("D"))).IsResolved);

        var serverToClientSpecific = resolver.ResolveForScope(
            ScriptEngineeringScope.Server,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.ClientMemoryTag, clientTagId.ToString("D")));
        Assert.Equal("SCRIPT_REFERENCE_SCOPE_INVALID", serverToClientSpecific.DiagnosticCode);

        var serverToClientAsShared = resolver.ResolveForScope(
            ScriptEngineeringScope.Server,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Tag, clientTagId.ToString("D")));
        Assert.Equal("SCRIPT_REFERENCE_KIND_MISMATCH", serverToClientAsShared.DiagnosticCode);

        var serverToVisual = resolver.ResolveForScope(
            ScriptEngineeringScope.Server,
            new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.VisualDefinition, visualId.ToString("D")));
        Assert.Equal("SCRIPT_REFERENCE_SCOPE_INVALID", serverToVisual.DiagnosticCode);
    }

    [Fact]
    public void ValidationCatalog_DoesNotAllowClientMemoryToMasqueradeAsGenericSharedTag()
    {
        var clientTagId = Guid.Parse("10000000-0000-0000-0000-000000000030");
        var resolver = ScriptEngineeringReferenceResolver.Create(
            [new TagEngineeringDto(clientTagId, "Selection", "Client.Selection", TagDataType.String, "client-memory")],
            [new DataSourceEngineeringDto(Guid.NewGuid(), "client-memory", "Client Memory", "builtin.memory.client")]);
        var clientReference = clientTagId.ToString("D");
        var catalog = resolver.ToValidationCatalog();

        Assert.False(catalog.Contains(ScriptEngineeringDependencyKind.Tag, clientReference));
        Assert.True(catalog.Contains(ScriptEngineeringDependencyKind.ClientMemoryTag, clientReference));

        var disguised = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "scripts/server/disguised-client-memory",
            "Disguised client memory",
            ScriptEngineeringScope.Server,
            "value = 1",
            dependencies:
            [new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.Tag, clientReference)]);

        var disguisedValidation = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([disguised]),
            catalog);

        Assert.Contains(disguisedValidation.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");

        var explicitClientMemory = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "scripts/server/explicit-client-memory",
            "Explicit client memory",
            ScriptEngineeringScope.Server,
            "value = 1",
            dependencies:
            [new ScriptEngineeringDependency(ScriptEngineeringDependencyKind.ClientMemoryTag, clientReference)]);

        var explicitValidation = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([explicitClientMemory]),
            catalog);

        Assert.Contains(explicitValidation.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_SCOPE_INVALID");
        Assert.DoesNotContain(explicitValidation.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
    }

    [Fact]
    public void Create_FailsClosedWhenOneDataSourceKeyHasConflictingMemoryClassifications()
    {
        var tagId = Guid.Parse("10000000-0000-0000-0000-000000000040");
        var resolver = ScriptEngineeringReferenceResolver.Create(
            [new TagEngineeringDto(tagId, "Ambiguous", "Memory.Ambiguous", TagDataType.Int32, "memory")],
            [
                new DataSourceEngineeringDto(Guid.NewGuid(), "memory", "Memory A", "builtin.memory.client"),
                new DataSourceEngineeringDto(Guid.NewGuid(), "MEMORY", "Memory B", "builtin.memory.server")
            ]);

        Assert.Empty(resolver.References);
        Assert.Contains(
            resolver.CatalogIssues,
            issue => issue.Code == "SCRIPT_REFERENCE_DATASOURCE_AMBIGUOUS" && issue.EntityKey == "Memory.Ambiguous");
        Assert.Equal(
            "SCRIPT_REFERENCE_MISSING",
            resolver.Resolve(ScriptEngineeringDependencyKind.ClientMemoryTag, tagId.ToString("D")).DiagnosticCode);
    }

    [Fact]
    public void FromEngineeringPackage_CatalogsScreenPopupAndDynamoStableDefinitions()
    {
        var screenId = Guid.Parse("20000000-0000-0000-0000-000000000101");
        var popupId = Guid.Parse("20000000-0000-0000-0000-000000000102");
        var dynamoId = Guid.Parse("20000000-0000-0000-0000-000000000103");
        var package = new EngineeringPackage(
            "scada.engineering",
            10,
            DateTimeOffset.UnixEpoch,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens: [new ScreenEngineeringDto(screenId, "overview", "Overview")],
            Popups: [new PopupEngineeringDto(popupId, "pump-detail", "Pump detail")],
            Dynamos: [new DynamoEngineeringDto(dynamoId, "pump", "Pump")]);

        var resolver = ScriptEngineeringReferenceResolver.FromEngineeringPackage(package);

        Assert.True(resolver.Resolve(
            ScriptEngineeringDependencyKind.VisualDefinition,
            screenId.ToString("D")).IsResolved);
        Assert.True(resolver.Resolve(
            ScriptEngineeringDependencyKind.VisualDefinition,
            popupId.ToString("D")).IsResolved);
        Assert.True(resolver.Resolve(
            ScriptEngineeringDependencyKind.VisualDefinition,
            dynamoId.ToString("D")).IsResolved);
    }
}
