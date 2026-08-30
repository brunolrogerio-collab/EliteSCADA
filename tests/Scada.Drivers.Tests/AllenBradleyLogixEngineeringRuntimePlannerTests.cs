using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.AllenBradley;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyLogixEngineeringRuntimePlannerTests
{
    [Fact]
    public void Planner_BuildsLibraryIndependentPlanFromCanonicalEngineering()
    {
        var tagId = Guid.NewGuid();
        var reference = new LogixSymbolReference(
            LogixTagScope.Program,
            "Batch.CurrentRecipe",
            LogixNativeType.Dint,
            "Packaging");
        var address = LogixPortableAddress.Format(reference, LogixExternalAccess.ReadWrite, constant: false);
        var dataSource = CreateDataSource();
        var tag = new TagEngineeringDto(
            tagId,
            "Current Recipe",
            "Plant/Packaging/CurrentRecipe",
            TagDataType.Int32,
            Source: dataSource.Key,
            Address: address,
            ReadOnly: false,
            Metadata: new Dictionary<string, string> { ["owner"] = "engineering" });
        var package = CreatePackage(dataSource, tag);

        var result = AllenBradleyLogixEngineeringRuntimePlanner.Plan(package, dataSource);

        Assert.True(result.CanActivate);
        var plan = Assert.IsType<AllenBradleyLogixRuntimePlan>(result.Plan);
        Assert.Equal(dataSource.Key, plan.DataSourceKey);
        Assert.Equal(dataSource.Name, plan.Name);
        Assert.Equal(AllenBradleyLogixContractIdentity.DriverType, plan.DriverType);
        Assert.Equal("127.0.0.1", plan.Options.Host);
        Assert.Single(plan.Tags);
        Assert.Single(plan.Bindings);
        var binding = plan.Bindings.Single();
        Assert.Equal(tagId, binding.Tag.Id);
        Assert.Equal(reference.StableIdentity, binding.Reference.StableIdentity);
        Assert.Equal(LogixNativeType.Dint, binding.Reference.NativeType);
        Assert.Equal(LogixExternalAccess.ReadWrite, binding.ExternalAccess);
        Assert.True(binding.Writable);
        Assert.Equal(address, binding.PortableAddress);
        Assert.Equal("engineering", binding.Tag.Metadata!["owner"]);
        Assert.Equal(address, binding.Tag.Metadata["address"]);

        var publicPropertyTypes = plan.GetType().GetProperties().Select(static x => x.PropertyType).ToArray();
        Assert.DoesNotContain(typeof(ILogixProtocolClient), publicPropertyTypes);
        Assert.DoesNotContain(typeof(LogixEtherNetIpClient), publicPropertyTypes);
    }

    [Fact]
    public void Planner_PreservesCanonicalBitSelectorWithoutSecondBitIdentity()
    {
        var selector = new TagValueSelector(TagValueSelectorKind.Bit, 7);
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Machine.StatusWord", LogixNativeType.Dint);
        var dataSource = CreateDataSource();
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Ready Bit",
            "Plant/Machine/Ready",
            TagDataType.Boolean,
            Source: dataSource.Key,
            Address: LogixPortableAddress.Format(reference, LogixExternalAccess.ReadWrite),
            ReadOnly: false,
            AddressSelector: selector);

        var result = AllenBradleyLogixEngineeringRuntimePlanner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.True(result.CanActivate);
        var binding = Assert.Single(Assert.IsType<AllenBradleyLogixRuntimePlan>(result.Plan).Bindings);
        Assert.Same(selector, binding.Tag.AddressSelector);
        Assert.Same(selector, binding.AddressSelector);
        Assert.True(binding.Writable);
        Assert.Equal("controller:Machine.StatusWord", binding.Reference.StableIdentity);
    }

    [Fact]
    public void Planner_InvalidPortableAddressFailsClosed()
    {
        var dataSource = CreateDataSource();
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Bad",
            "Plant/Bad",
            TagDataType.Int32,
            Source: dataSource.Key,
            Address: "not-a-logix-address",
            ReadOnly: true);

        var result = AllenBradleyLogixEngineeringRuntimePlanner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static x => x.Code == "LOGIX_TAG_ADDRESS_INVALID" && x.IsError);
    }

    [Fact]
    public void Planner_ExternalAccessNoneFailsClosedBeforeRuntime()
    {
        var dataSource = CreateDataSource();
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "PrivateValue", LogixNativeType.Dint);
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Private",
            "Plant/Private",
            TagDataType.Int32,
            Source: dataSource.Key,
            Address: LogixPortableAddress.Format(reference, LogixExternalAccess.None),
            ReadOnly: true);

        var result = AllenBradleyLogixEngineeringRuntimePlanner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static x => x.Code == "LOGIX_TAG_NOT_READABLE" && x.IsError);
    }

    [Fact]
    public void Planner_WritableTagRequiresExplicitReadWriteAccess()
    {
        var dataSource = CreateDataSource();
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "ReadOnlyValue", LogixNativeType.Dint);
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Read Only",
            "Plant/ReadOnly",
            TagDataType.Int32,
            Source: dataSource.Key,
            Address: LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly),
            ReadOnly: false);

        var result = AllenBradleyLogixEngineeringRuntimePlanner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static x => x.Code == "LOGIX_TAG_CONFIGURATION_INVALID" && x.IsError);
    }

    [Fact]
    public void Planner_CipSecurityRequiredFailsClosedWithoutDowngrade()
    {
        var settings = DefaultSettings();
        settings["securityMode"] = "CipSecurityRequired";
        var dataSource = CreateDataSource(settings);
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Value", LogixNativeType.Dint);
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Value",
            "Plant/Value",
            TagDataType.Int32,
            Source: dataSource.Key,
            Address: LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly));

        var result = AllenBradleyLogixEngineeringRuntimePlanner.Plan(CreatePackage(dataSource, tag), dataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static x => x.Code == "LOGIX_CIP_SECURITY_NOT_IMPLEMENTED" && x.IsError);
    }

    [Fact]
    public async Task Factory_CreatesProtocolClientOnlyWhenRuntimeIsInstantiated()
    {
        var dataSource = CreateDataSource();
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Counter", LogixNativeType.Dint);
        var tag = new TagEngineeringDto(
            Guid.NewGuid(),
            "Counter",
            "Plant/Counter",
            TagDataType.Int32,
            Source: dataSource.Key,
            Address: LogixPortableAddress.Format(reference, LogixExternalAccess.ReadOnly));
        var planning = AllenBradleyLogixEngineeringRuntimePlanner.Plan(CreatePackage(dataSource, tag), dataSource);
        var plan = Assert.IsType<AllenBradleyLogixRuntimePlan>(planning.Plan);
        var protocolFactory = new CountingProtocolClientFactory();
        var runtimeFactory = new AllenBradleyLogixRuntimeFactory(protocolFactory);

        Assert.Equal(0, protocolFactory.CreateCount);

        var driver = runtimeFactory.Create(plan, new NoopCache(), new InMemoryTagRegistry());

        Assert.Equal(1, protocolFactory.CreateCount);
        Assert.Equal($"{AllenBradleyLogixContractIdentity.DriverType}:{dataSource.Key}", driver.DriverId);
        Assert.Equal(dataSource.Name, driver.Name);
        Assert.Equal(AllenBradleyLogixContractIdentity.DriverType, runtimeFactory.DriverType);
        Assert.Single(driver.Tags);
        await driver.DisposeAsync();
    }

    private static DataSourceEngineeringDto CreateDataSource(Dictionary<string, string>? settings = null) =>
        new(
            Guid.NewGuid(),
            "line1-plc",
            "Line 1 PLC",
            AllenBradleyLogixContractIdentity.DriverType,
            Enabled: true,
            Settings: settings ?? DefaultSettings());

    private static Dictionary<string, string> DefaultSettings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = "127.0.0.1",
            ["profile"] = "CompactLogix",
            ["route"] = "1,0",
            ["scanIntervalMs"] = "1000",
            ["requestTimeoutMs"] = "3000",
            ["maxBatchSize"] = "16"
        };

    private static EngineeringPackage CreatePackage(
        DataSourceEngineeringDto dataSource,
        params TagEngineeringDto[] tags) =>
        new(
            "elitescada.engineering",
            13,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);

    private sealed class NoopCache : ICurrentTagCache
    {
        public bool TryGet(Guid tagId, out TagValue? value)
        {
            value = null;
            return false;
        }

        public IReadOnlyCollection<TagValue> Snapshot() => Array.Empty<TagValue>();

        public ValueTask<TagValue?> UpdateAsync(
            TagDefinition tag,
            TagValue value,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<TagValue?>(null);
    }

    private sealed class CountingProtocolClientFactory : ILogixProtocolClientFactory
    {
        public int CreateCount { get; private set; }

        public ILogixProtocolClient Create()
        {
            CreateCount++;
            return new NoopProtocolClient();
        }
    }

    private sealed class NoopProtocolClient : ILogixProtocolClient
    {
        public bool IsConnected => false;

        public ValueTask ConnectAsync(AllenBradleyLogixOptions options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<LogixControllerIdentity> GetIdentityAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<LogixReadResult>> ReadManyAsync(
            IReadOnlyList<LogixSymbolReference> references,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<LogixSymbolBrowsePage> BrowseControllerSymbolsAsync(
            uint startInstance = 0,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask WriteAsync(
            LogixSymbolReference reference,
            object? nativeValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public LogixTransportDiagnosticSnapshot GetDiagnostics() =>
            new(false, 0, 0, 0, 0, 0, 0, 0, null, null, null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
