using System.Text;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyLogixDriverTests
{
    [Fact]
    public void PortableAddress_RoundTripsControllerAndProgramIdentity()
    {
        var controller = new LogixSymbolReference(LogixTagScope.Controller, "Tank.Level", LogixNativeType.Real);
        var program = new LogixSymbolReference(LogixTagScope.Program, "Machine.Status", LogixNativeType.Dint, "Packaging");

        var controllerText = LogixPortableAddress.Format(controller, LogixExternalAccess.ReadOnly);
        var programText = LogixPortableAddress.Format(program, LogixExternalAccess.ReadWrite, constant: true);

        Assert.True(LogixPortableAddress.TryParse(controllerText, out var parsedController, out var controllerAccess, out var controllerConstant, out var controllerError), controllerError);
        Assert.True(LogixPortableAddress.TryParse(programText, out var parsedProgram, out var programAccess, out var programConstant, out var programError), programError);

        Assert.Equal(controller, parsedController);
        Assert.Equal(LogixExternalAccess.ReadOnly, controllerAccess);
        Assert.False(controllerConstant);
        Assert.Equal(program, parsedProgram);
        Assert.Equal(LogixExternalAccess.ReadWrite, programAccess);
        Assert.True(programConstant);
    }

    [Fact]
    public void PhysicalBitBinding_ValidatesWidthAndPreservesNeighborBits()
    {
        var tag = TagDefinition.Create(
            "CommandBit",
            "PLC.CommandBit",
            TagDataType.Boolean,
            source: "CLX",
            readOnly: false,
            addressSelector: new TagValueSelector(TagValueSelectorKind.Bit, 7));
        var binding = new LogixTagBinding(
            tag,
            new LogixSymbolReference(LogixTagScope.Controller, "CommandWord", LogixNativeType.Dint),
            Writable: true,
            ExternalAccess: LogixExternalAccess.ReadWrite);

        binding.Validate();
        var original = unchecked((int)0x5A5A_005A);
        var set = Assert.IsType<int>(LogixValueCodec.ApplyPhysicalBit(LogixNativeType.Dint, original, 7, true));
        var cleared = Assert.IsType<int>(LogixValueCodec.ApplyPhysicalBit(LogixNativeType.Dint, set, 7, false));

        Assert.Equal(original | (1 << 7), set);
        Assert.Equal(original & ~(1 << 7), cleared);
        Assert.Equal((uint)original & ~(1u << 7), (uint)cleared);
    }

    [Fact]
    public void DirectBoolWrite_IsFailClosedUntilPackedBoolMetadataIsProven()
    {
        var tag = TagDefinition.Create("Run", "PLC.Run", TagDataType.Boolean, source: "CLX", readOnly: false);
        var binding = new LogixTagBinding(
            tag,
            new LogixSymbolReference(LogixTagScope.Controller, "Run", LogixNativeType.Bool),
            Writable: true,
            ExternalAccess: LogixExternalAccess.ReadWrite);

        var error = Assert.Throws<ArgumentException>(binding.Validate);
        Assert.Contains("Direct writes", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EngineeringDescriptor_SeparatesRuntimeAndEngineeringCapabilities()
    {
        var descriptor = new AllenBradleyLogixEngineeringAdapter().Descriptor;

        Assert.Equal(AllenBradleyLogixEngineeringAdapter.DriverType, descriptor.DriverType);
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Read));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Write));
        Assert.True(descriptor.RuntimeCapabilities.HasFlag(DriverCapabilities.Diagnostics));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.FileImport));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Reconcile));
        Assert.Equal(DriverAcquisitionMode.Polling, Assert.Single(descriptor.AcquisitionModes));
    }

    [Fact]
    public async Task L5xImport_PreservesControllerProgramAccessAndUnsupportedWriteEvidence()
    {
        const string l5x = """
            <?xml version="1.0" encoding="UTF-8"?>
            <RSLogix5000Content>
              <Controller Name="DemoController">
                <Tags>
                  <Tag Name="TankLevel" TagType="Base" DataType="DINT" ExternalAccess="Read/Write" Constant="false" />
                </Tags>
                <Programs>
                  <Program Name="Packaging">
                    <Tags>
                      <Tag Name="Running" TagType="Base" DataType="BOOL" ExternalAccess="Read/Write" Constant="false" />
                      <Tag Name="RecipeNames" TagType="Base" DataType="STRING" ExternalAccess="Read Only" Constant="false" />
                    </Tags>
                  </Program>
                </Programs>
              </Controller>
            </RSLogix5000Content>
            """;

        var adapter = new AllenBradleyLogixEngineeringAdapter();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(l5x));
        var candidates = new List<DriverImportCandidate>();
        await foreach (var candidate in adapter.ImportAsync(new DriverImportRequest(null, "demo.L5X", "application/xml"), stream))
            candidates.Add(candidate);

        Assert.Equal(3, candidates.Count);
        var level = Assert.Single(candidates, static x => x.DisplayName == "TankLevel");
        Assert.Equal("controller:TankLevel", level.StableIdentity);
        Assert.True(level.IsReadable);
        Assert.True(level.IsWritable);
        Assert.Equal(TagDataType.Int32, level.SuggestedDataType);

        var running = Assert.Single(candidates, static x => x.DisplayName == "Running");
        Assert.Equal("program:Packaging:Running", running.StableIdentity);
        Assert.True(running.IsReadable);
        Assert.False(running.IsWritable);
        Assert.Contains(running.Issues ?? Array.Empty<DriverEngineeringIssue>(), static x => x.Code == "LOGIX_BOOL_DIRECT_WRITE_DEFERRED");

        var strings = Assert.Single(candidates, static x => x.DisplayName == "RecipeNames");
        Assert.False(strings.IsReadable);
        Assert.Equal(TagDataType.String, strings.SuggestedDataType);
        Assert.Contains(strings.Issues ?? Array.Empty<DriverEngineeringIssue>(), static x => x.Code == "LOGIX_TYPE_RUNTIME_UNSUPPORTED");
    }

    [Fact]
    public void EngineeringOptions_RejectCipSecurityInsteadOfDowngrading()
    {
        var context = new DriverEngineeringDataSourceContext(
            "CLX",
            "ControlLogix",
            AllenBradleyLogixEngineeringAdapter.DriverType,
            new Dictionary<string, string>
            {
                ["host"] = "10.0.0.10",
                ["profile"] = "ControlLogix",
                ["route"] = "1,0",
                ["securityMode"] = "CipSecurityRequired"
            },
            new Dictionary<string, string>());

        Assert.False(AllenBradleyLogixEngineeringAdapter.TryCreateOptions(context, out var options, out var issues));
        Assert.Null(options);
        Assert.Contains(issues, static x => x.Code == "LOGIX_CIP_SECURITY_NOT_IMPLEMENTED" && x.Severity == DriverEngineeringIssueSeverity.Error);
    }

    [Fact]
    public async Task Runtime_CoalescesSharedPhysicalSymbolAndPerformsCoordinatedBitRmw()
    {
        var bit0 = TagDefinition.Create(
            "Bit0",
            "PLC.Bit0",
            TagDataType.Boolean,
            source: "CLX",
            readOnly: false,
            addressSelector: new TagValueSelector(TagValueSelectorKind.Bit, 0));
        var bit1 = TagDefinition.Create(
            "Bit1",
            "PLC.Bit1",
            TagDataType.Boolean,
            source: "CLX",
            readOnly: false,
            addressSelector: new TagValueSelector(TagValueSelectorKind.Bit, 1));
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "CommandWord", LogixNativeType.Dint);
        var bindings = new[]
        {
            new LogixTagBinding(bit0, reference, Writable: true, ExternalAccess: LogixExternalAccess.ReadWrite),
            new LogixTagBinding(bit1, reference, Writable: true, ExternalAccess: LogixExternalAccess.ReadWrite)
        };

        var fake = new FakeLogixProtocolClient();
        fake.Values[reference.StableIdentity] = 2;
        var cache = new FakeCurrentTagCache();
        var registry = new FakeTagRegistry();
        await using var driver = new AllenBradleyLogixDriver(
            "CLX",
            "ControlLogix",
            new AllenBradleyLogixOptions("127.0.0.1", ScanInterval: TimeSpan.FromSeconds(30)),
            cache,
            registry,
            bindings,
            new FakeLogixProtocolClientFactory(fake));

        await driver.StartAsync();

        Assert.True(cache.TryGet(bit0.Id, out var bit0Value));
        Assert.True(cache.TryGet(bit1.Id, out var bit1Value));
        Assert.False(Assert.IsType<bool>(bit0Value!.Value));
        Assert.True(Assert.IsType<bool>(bit1Value!.Value));
        Assert.Contains(fake.ReadReferenceCounts, static count => count == 1);

        await driver.WriteAsync(bit0.Id, true);
        Assert.Equal(3, Assert.IsType<int>(fake.Values[reference.StableIdentity]));
        Assert.Equal(reference.StableIdentity, Assert.Single(fake.Writes).Reference.StableIdentity);

        await driver.StopAsync();
        var diagnostics = driver.GetCommunicationDiagnostics();
        Assert.Equal(2, diagnostics.AssociatedTagCount);
        Assert.Equal("rockwell.logix.eip", diagnostics.DriverType);
    }

    private sealed class FakeCurrentTagCache : ICurrentTagCache
    {
        private readonly Dictionary<Guid, TagValue> _values = new();

        public bool TryGet(Guid tagId, out TagValue? value)
        {
            var found = _values.TryGetValue(tagId, out var current);
            value = current;
            return found;
        }

        public IReadOnlyCollection<TagValue> Snapshot() => _values.Values.ToArray();

        public ValueTask<TagValue?> UpdateAsync(TagDefinition tag, TagValue value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.TryGetValue(tag.Id, out var previous);
            _values[tag.Id] = value;
            return ValueTask.FromResult<TagValue?>(previous);
        }
    }

    private sealed class FakeTagRegistry : ITagRegistry
    {
        private readonly Dictionary<Guid, TagDefinition> _tags = new();

        public TagDefinition Register(TagDefinition tag)
        {
            if (!_tags.TryAdd(tag.Id, tag)) throw new InvalidOperationException("TAG already registered.");
            return tag;
        }

        public TagDefinition Upsert(TagDefinition tag)
        {
            _tags[tag.Id] = tag;
            return tag;
        }

        public bool TryGet(Guid tagId, out TagDefinition? tag)
        {
            var found = _tags.TryGetValue(tagId, out var current);
            tag = current;
            return found;
        }

        public bool TryGetByPath(string path, out TagDefinition? tag)
        {
            tag = _tags.Values.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            return tag is not null;
        }

        public IReadOnlyCollection<TagDefinition> Snapshot() => _tags.Values.ToArray();
    }

    private sealed class FakeLogixProtocolClientFactory(FakeLogixProtocolClient client) : ILogixProtocolClientFactory
    {
        public ILogixProtocolClient Create() => client;
    }

    private sealed class FakeLogixProtocolClient : ILogixProtocolClient
    {
        private long _requests;
        private long _connections;
        private long _disconnections;

        public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<int> ReadReferenceCounts { get; } = new();
        public List<(LogixSymbolReference Reference, object Value)> Writes { get; } = new();
        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(AllenBradleyLogixOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            options.Validate();
            if (!IsConnected) _connections++;
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsConnected) _disconnections++;
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask<LogixControllerIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LogixControllerIdentity(1, 14, 1, 35, 11, 0x12345678, "Fake Logix"));
        }

        public ValueTask<IReadOnlyList<LogixReadResult>> ReadManyAsync(
            IReadOnlyList<LogixSymbolReference> references,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _requests += references.Count;
            ReadReferenceCounts.Add(references.Count);
            IReadOnlyList<LogixReadResult> results = references
                .Select(reference => Values.TryGetValue(reference.StableIdentity, out var value)
                    ? new LogixReadResult(reference, true, value)
                    : new LogixReadResult(reference, false, Error: LogixProtocolError.SymbolNotFound, Message: "missing"))
                .ToArray();
            return ValueTask.FromResult(results);
        }

        public ValueTask<LogixSymbolBrowsePage> BrowseControllerSymbolsAsync(uint startInstance = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new LogixSymbolBrowsePage(Array.Empty<LogixBrowseSymbol>(), null, false));
        }

        public ValueTask WriteAsync(LogixSymbolReference reference, object? nativeValue, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nativeValue is null) throw new ArgumentNullException(nameof(nativeValue));
            _requests++;
            Values[reference.StableIdentity] = nativeValue;
            Writes.Add((reference, nativeValue));
            return ValueTask.CompletedTask;
        }

        public LogixTransportDiagnosticSnapshot GetDiagnostics() => new(
            IsConnected,
            _requests,
            _requests,
            0,
            0,
            _connections,
            _disconnections,
            0,
            _connections > 0 ? DateTimeOffset.UtcNow : null,
            _disconnections > 0 ? DateTimeOffset.UtcNow : null,
            null);

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
