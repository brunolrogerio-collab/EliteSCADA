using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;
using Scada.Drivers.Mitsubishi;
using Scada.Drivers.Panasonic;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpPlcExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PlcGuidesCoverEveryDeclaredConnectionAndBindingField(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        (string Topic, CommunicationDriverTypeDescriptor Descriptor)[] drivers = [
            ("drivers.siemens-setup", new S7IsoEngineeringAdapter().Descriptor),
            ("drivers.logix-setup", new AllenBradleyLogixEngineeringAdapter().Descriptor),
            ("drivers.melsec-setup", MitsubishiMelsecDriverDescriptorProvider.SharedDescriptor),
            ("drivers.panasonic-setup", PanasonicMewtocolDriverDescriptorProvider.TcpDescriptor),
            ("drivers.panasonic-setup", PanasonicMewtocolDriverDescriptorProvider.SerialDescriptor)
        ];
        foreach (var (topic, descriptor) in drivers)
        {
            var keys = catalog.Topics.Single(t => t.Id == topic).Sections
                .SelectMany(s => s.Fields ?? []).Select(f => f.Key).ToHashSet();
            foreach (var field in descriptor.ConfigurationSchema.DataSourceFields.Concat(descriptor.ConfigurationSchema.TagBindingFields))
                Assert.Contains(field.Key, keys);
        }
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void ExactPublishedExamplesPassActualRuntimePlannersWithoutOpeningAConnection(string locale)
    {
        foreach (var (topic, descriptor, planner) in Drivers())
        {
            var (source, tag) = Example(locale, topic, descriptor);
            var result = planner.Plan(Package(source, tag), source);
            Assert.True(result.CanActivate, string.Join("; ", result.Issues.Select(i => i.Message)));
            Assert.Equal(tag.Id, Assert.Single(result.Plan!.Tags).Id);
            var wrongType = tag with { DataType = TagDataType.Boolean };
            Assert.False(planner.Plan(Package(source, wrongType), source).CanActivate);
        }

        // The guide's serial fields must form the same valid point under the distinct serial source.
        var (tcp, serialTag) = Example(locale, "drivers.panasonic-setup", PanasonicMewtocolDriverDescriptorProvider.TcpDescriptor);
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.panasonic-setup");
        var fields = guide.Sections.SelectMany(s => s.Fields ?? []).ToDictionary(f => f.Key, f => f.Example);
        var serialSettings = tcp.Settings!.Where(p => p.Key is not ("host" or "port" or "connectTimeoutMilliseconds"))
            .ToDictionary(p => p.Key, p => p.Value);
        foreach (var key in new[] { "serialPort", "baudRate", "dataBits", "parity", "stopBits", "turnaroundMilliseconds" })
            serialSettings[key] = fields[key];
        var serial = tcp with { Driver = PanasonicMewtocolDriverDescriptorProvider.SerialDriverTypeId, Settings = serialSettings };
        var serialPlanner = new PanasonicMewtocolCommunicationRuntimePlanner(serial.Driver);
        var serialResult = serialPlanner.Plan(Package(serial, serialTag), serial);
        Assert.True(serialResult.CanActivate, string.Join("; ", serialResult.Issues.Select(i => i.Message)));
        var invalidFrame = new Dictionary<string, string>(serialSettings) { ["frameMode"] = "Expanded" };
        var invalid = serial with { Settings = invalidFrame };
        Assert.False(serialPlanner.Plan(Package(invalid, serialTag), invalid).CanActivate);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedMemoryExamplesPreserveActualByteOrderAndProfileBoundaries(string locale)
    {
        var (_, tag) = Example(locale, "drivers.siemens-setup", new S7IsoEngineeringAdapter().Descriptor);
        Assert.True(S7IsoCommunicationBindingProjection.TryMaterializeCanonical(tag.Address, tag.CommunicationBinding!.Settings!, false, false, out var binding, out var projectionError), projectionError);
        Assert.Equal(tag.Address, S7IsoCommunicationBindingProjection.ToCanonicalPortableAddress(binding!));
        var s7 = binding!.ToPoint(TagDefinition.Create(tag.Name, tag.Path, tag.DataType));
        Assert.Equal(123f, Assert.IsType<float>(S7IsoValueCodec.Decode(s7, new byte[] { 0x42, 0xF6, 0, 0 })));
        Assert.Equal(123f, Assert.IsType<float>(LogixValueCodec.DecodeAtomic(LogixNativeType.Real, new byte[] { 0, 0, 0xF6, 0x42 })));

        var (logixSource, logixTag) = Example(locale, "drivers.logix-setup", new AllenBradleyLogixEngineeringAdapter().Descriptor);
        Assert.True(LogixPortableAddress.TryParse(logixTag.Address, out var reference, out var access, out var constant, out var error), error);
        Assert.Equal("TankLevel", reference!.SymbolPath);
        Assert.Equal(LogixExternalAccess.ReadOnly, access);
        Assert.False(constant);
        var secureSettings = new Dictionary<string, string>(logixSource.Settings!) { ["securityMode"] = "CipSecurityRequired" };
        var secureSource = logixSource with { Settings = secureSettings };
        Assert.False(new AllenBradleyLogixCommunicationRuntimePlanner().Plan(Package(secureSource, logixTag), secureSource).CanActivate);
        Assert.False(LogixValueCodec.IsFirstCutRuntimeReadable(LogixNativeType.Lreal));
        Assert.False(LogixValueCodec.IsFirstCutRuntimeWritable(LogixNativeType.Bool));

        var (_, melsecTag) = Example(locale, "drivers.melsec-setup", MitsubishiMelsecDriverDescriptorProvider.SharedDescriptor);
        Assert.True(MitsubishiMelsecAddress.TryParse(melsecTag.Address, MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out var address, out error), error);
        Assert.Equal(100, address!.Number);
        Assert.True(MitsubishiMelsecAddress.TryParse("X10", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out address, out error), error);
        Assert.Equal(16, address!.Number);
        Assert.False(MitsubishiMelsecAddress.TryParse("R0", MitsubishiMelsecFamilyProfile.Fx5U32MtDs, null, out _, out _));

        var (_, panasonicTag) = Example(locale, "drivers.panasonic-setup", PanasonicMewtocolDriverDescriptorProvider.TcpDescriptor);
        Assert.True(PanasonicMewtocolAddress.TryParse(panasonicTag.Address, PanasonicMewtocolFamilyProfile.Fp0rF32, out var panasonic, out error), error);
        Assert.Equal(100, panasonic!.Number);
        Assert.True(PanasonicMewtocolAddress.TryParse("X10", PanasonicMewtocolFamilyProfile.Fp0rF32, out panasonic, out error), error);
        Assert.Equal(16, panasonic!.Number);
        Assert.Equal(123, Assert.IsType<int>(MitsubishiMelsecValueCodec.Decode(MitsubishiMelsecPhysicalType.Int16, new byte[] { 123, 0 }, false)));
        Assert.Equal(123, Assert.IsType<int>(PanasonicMewtocolValueCodec.DecodeWord(new byte[] { 123, 0 }, PanasonicMewtocolPhysicalType.Int16)));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void CurrentCatalogBindingIdentityUsedByTheGenericAssistantIsRejectedForS7(string locale)
    {
        // Characterization of #786; this assertion and the Help marker must change with the product correction.
        var catalog = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Describe();
        var type = catalog.DataSourceTypes.Single(t => t.TypeKey == "siemens.s7.iso");
        var (source, tag) = Example(locale, "drivers.siemens-setup", new S7IsoEngineeringAdapter().Descriptor);
        var emittedSettings = new Dictionary<string, string>(tag.CommunicationBinding!.Settings!) { ["valueOrder"] = "Normal" };
        var generated = tag.CommunicationBinding with { SchemaId = type.TagBindingSchemaId!, SchemaVersion = type.TagBindingSchemaVersion!.Value, Settings = emittedSettings };
        Assert.NotEqual(S7IsoTagBinding.SchemaId, generated.SchemaId);
        var assistantTag = tag with { CommunicationBinding = generated };
        var result = new S7IsoCommunicationRuntimePlanner().Plan(Package(source, assistantTag), source);
        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, i => i.Code == "S7_TAG_BINDING_SCHEMA_MISMATCH");
        // Correcting the schema alone still leaves the legacy order setting incompatible with the current projection.
        var schemaOnly = assistantTag with { CommunicationBinding = generated with { SchemaId = S7IsoTagBinding.SchemaId } };
        Assert.False(new S7IsoCommunicationRuntimePlanner().Plan(Package(source, schemaOnly), source).CanActivate);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void GenericAssistantLogixSettingsRemainRejectedInsteadOfWeakeningRuntimeGuards(string locale)
    {
        // Characterization of #786; not an assertion that the assistant path is delivered.
        var descriptor = new AllenBradleyLogixEngineeringAdapter().Descriptor;
        var (source, tag) = Example(locale, "drivers.logix-setup", descriptor);
        var settings = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.logix-setup").Sections
            .SelectMany(s => s.Fields ?? []).Where(f => descriptor.ConfigurationSchema.TagBindingFields.Any(k => k.Key == f.Key) && f.Example.Length > 0)
            .ToDictionary(f => f.Key, f => f.Example);
        var emitted = tag with { CommunicationBinding = tag.CommunicationBinding! with { Settings = settings } };
        var result = new AllenBradleyLogixCommunicationRuntimePlanner().Plan(Package(source, emitted), source);
        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, i => i.Code == "LOGIX_TAG_BINDING_SETTING_UNSUPPORTED");
    }

    private static (string, CommunicationDriverTypeDescriptor, ICommunicationDriverRuntimePlanner)[] Drivers() => [
        ("drivers.siemens-setup", new S7IsoEngineeringAdapter().Descriptor, new S7IsoCommunicationRuntimePlanner()),
        ("drivers.logix-setup", new AllenBradleyLogixEngineeringAdapter().Descriptor, new AllenBradleyLogixCommunicationRuntimePlanner()),
        ("drivers.melsec-setup", MitsubishiMelsecDriverDescriptorProvider.SharedDescriptor, new MitsubishiMelsecCommunicationRuntimePlanner()),
        ("drivers.panasonic-setup", PanasonicMewtocolDriverDescriptorProvider.TcpDescriptor, new PanasonicMewtocolCommunicationRuntimePlanner(PanasonicMewtocolDriverDescriptorProvider.TcpDriverTypeId))
    ];

    private static (DataSourceEngineeringDto, TagEngineeringDto) Example(string locale, string topic, CommunicationDriverTypeDescriptor descriptor)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == topic);
        using var json = JsonDocument.Parse(Assert.Single(guide.Sections, s => s.Code is not null).Code!);
        var root = json.RootElement;
        static Dictionary<string, string> Settings(JsonElement element) => element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var source = new DataSourceEngineeringDto(Guid.NewGuid(), "plc.example", "PLC example", descriptor.DriverType, Settings: Settings(root.GetProperty("source")));
        var binding = new CommunicationTagBinding(1,
            topic == "drivers.siemens-setup" ? S7IsoTagBinding.SchemaId : descriptor.TagBindingSchemaId ?? descriptor.ConfigurationSchema.SchemaId,
            descriptor.TagBindingSchemaVersion ?? descriptor.ConfigurationSchema.SchemaVersion,
            root.GetProperty("address").GetString()!, Settings: Settings(root.GetProperty("binding")));
        var tag = new TagEngineeringDto(Guid.NewGuid(), "Reading", "Plant.PLC.Reading", Enum.Parse<TagDataType>(root.GetProperty("tagDataType").GetString()!),
            Source: source.Key, Address: binding.PortableAddress, ReadOnly: true, CommunicationBinding: binding, DataSourceId: source.Id);
        return (source, tag);
    }

    private static EngineeringPackage Package(DataSourceEngineeringDto source, TagEngineeringDto tag) => new(
        EngineeringExchangeService.CurrentSchema, EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [tag], [], DataSources: [source]);
}
