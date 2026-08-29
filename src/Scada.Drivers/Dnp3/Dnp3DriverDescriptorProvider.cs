using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Dnp3;

/// <summary>
/// Public library-neutral Driver SDK descriptor for the DNP3 Master family.
/// It describes authorable configuration only; active Engineering capabilities
/// such as ConnectionTest/Browse/Reconcile remain disabled until their real
/// protocol-backed adapters exist.
/// </summary>
public sealed class Dnp3DriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string DriverType = "dnp3.master";
    public const string ConfigurationSchemaId = "elitescada.driver.dnp3.master";

    private static readonly CommunicationDriverTypeDescriptor Value = CreateDescriptor();

    public static CommunicationDriverTypeDescriptor SharedDescriptor => Value;
    public CommunicationDriverTypeDescriptor Descriptor => Value;

    private static CommunicationDriverTypeDescriptor CreateDescriptor() => new(
        DriverType,
        "DNP3 Master",
        DriverContractVersion: 1,
        RuntimeCapabilities:
            DriverCapabilities.Read |
            DriverCapabilities.Write |
            DriverCapabilities.Subscribe |
            DriverCapabilities.Diagnostics |
            DriverCapabilities.SourceTimestamp,
        EngineeringCapabilities: DriverEngineeringCapabilities.None,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            ConfigurationSchemaId,
            SchemaVersion: 1,
            DataSourceFields: CreateDataSourceFields(),
            TagBindingFields: CreateTagBindingFields()),
        SupportsSharedTransportInfrastructure: false,
        Description: "DNP3 Master/Client Data Source. Initial transport profile is TCP with integrity/class polling plus event/unsolicited acquisition.");

    private static IReadOnlyCollection<DriverConfigurationFieldDescriptor> CreateDataSourceFields() =>
    [
        Field("transport", DriverConfigurationValueKind.Enum, required: true, defaultValue: "tcp", allowedValues: ["tcp"], description: "Transport profile. TCP is the only implemented first-cut profile; serial is deliberately not advertised yet."),
        Field("host", DriverConfigurationValueKind.Host, required: true, description: "DNP3 TCP outstation host or IP address."),
        Field("port", DriverConfigurationValueKind.Port, defaultValue: "20000", minimum: 1, maximum: 65535, description: "DNP3 TCP port."),
        Field("masterAddress", DriverConfigurationValueKind.Integer, required: true, minimum: 0, maximum: Dnp3TcpConnectionOptions.MaxIndividualLinkAddress, description: "Local DNP3 link-layer master address."),
        Field("outstationAddress", DriverConfigurationValueKind.Integer, required: true, minimum: 0, maximum: Dnp3TcpConnectionOptions.MaxIndividualLinkAddress, description: "Remote DNP3 link-layer outstation address."),
        Field("connectTimeout", DriverConfigurationValueKind.Duration, defaultValue: "00:00:05", description: "Bounded TCP connection timeout."),
        Field("responseTimeout", DriverConfigurationValueKind.Duration, defaultValue: "00:00:05", description: "Bounded DNP3 response timeout."),
        Field("reconnectMinDelay", DriverConfigurationValueKind.Duration, defaultValue: "00:00:01", description: "Minimum reconnect backoff."),
        Field("reconnectMaxDelay", DriverConfigurationValueKind.Duration, defaultValue: "00:00:30", description: "Maximum reconnect backoff."),
        Field("keepAliveTimeout", DriverConfigurationValueKind.Duration, defaultValue: "00:01:00", advanced: true, description: "Optional link-status keepalive timeout."),
        Field("integrityPollInterval", DriverConfigurationValueKind.Duration, defaultValue: "00:15:00", advanced: true, description: "Periodic integrity resynchronization interval."),
        Field("class1PollInterval", DriverConfigurationValueKind.Duration, advanced: true, description: "Optional Class 1 fallback scan interval when polling is required."),
        Field("class2PollInterval", DriverConfigurationValueKind.Duration, advanced: true, description: "Optional Class 2 fallback scan interval when polling is required."),
        Field("class3PollInterval", DriverConfigurationValueKind.Duration, advanced: true, description: "Optional Class 3 fallback scan interval when polling is required."),
        Field("startupIntegrityClass0", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("startupIntegrityClass1", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("startupIntegrityClass2", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("startupIntegrityClass3", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("disableUnsolicitedClass1OnStartup", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("disableUnsolicitedClass2OnStartup", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("disableUnsolicitedClass3OnStartup", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("enableUnsolicitedClass1AfterIntegrity", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("enableUnsolicitedClass2AfterIntegrity", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("enableUnsolicitedClass3AfterIntegrity", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("eventScanClass1OnEventsAvailable", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("eventScanClass2OnEventsAvailable", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("eventScanClass3OnEventsAvailable", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("integrityOnEventBufferOverflow", DriverConfigurationValueKind.Boolean, defaultValue: "true", advanced: true),
        Field("timeSyncMode", DriverConfigurationValueKind.Enum, defaultValue: "disabled", allowedValues: ["disabled", "lan", "nonLan"], advanced: true),
        Field("maxQueuedUserRequests", DriverConfigurationValueKind.Integer, defaultValue: "16", minimum: 1, maximum: 1024, advanced: true, description: "Bounded user command/request queue; commands are never retained for replay across reconnect.")
    ];

    private static IReadOnlyCollection<DriverConfigurationFieldDescriptor> CreateTagBindingFields() =>
    [
        Field("pointKind", DriverConfigurationValueKind.Enum, required: true, allowedValues:
        [
            "binaryInput",
            "doubleBitBinaryInput",
            "analogInput",
            "counter",
            "frozenCounter",
            "binaryOutputStatus",
            "analogOutputStatus"
        ], description: "Stable DNP3 logical point family. Static/event group is representation, not point identity."),
        Field("index", DriverConfigurationValueKind.Integer, required: true, minimum: 0, maximum: ushort.MaxValue, description: "DNP3 point index; sparse indices are valid."),
        Field("staticVariation", DriverConfigurationValueKind.String, advanced: true, description: "Optional preferred static variation in canonical GxVy form; variation 0 means device default where supported."),
        Field("eventVariation", DriverConfigurationValueKind.String, advanced: true, description: "Optional preferred event variation in canonical GxVy form; variation 0 means device default where supported."),
        Field("expectedEventClass", DriverConfigurationValueKind.Enum, allowedValues: ["class1", "class2", "class3"], advanced: true, description: "Optional expected point event class used for validation/diagnostics, not identity."),
        Field("writable", DriverConfigurationValueKind.Boolean, defaultValue: "false", description: "Only supported output-status point families may enable commands."),
        Field("commandMode", DriverConfigurationValueKind.Enum, defaultValue: "selectBeforeOperate", allowedValues: ["selectBeforeOperate", "directOperate"], advanced: true, description: "Explicit command execution mode. Direct Operate No Response is intentionally not exposed."),
        Field("binaryTrueOperation", DriverConfigurationValueKind.Enum, defaultValue: "latchOn", allowedValues: ["latchOn", "latchOff", "pulseOn", "pulseOff"], advanced: true),
        Field("binaryFalseOperation", DriverConfigurationValueKind.Enum, defaultValue: "latchOff", allowedValues: ["latchOn", "latchOff", "pulseOn", "pulseOff"], advanced: true),
        Field("tripCloseCode", DriverConfigurationValueKind.Enum, defaultValue: "none", allowedValues: ["none", "trip", "close"], advanced: true),
        Field("commandCount", DriverConfigurationValueKind.Integer, defaultValue: "1", minimum: 1, maximum: byte.MaxValue, advanced: true),
        Field("commandOnTime", DriverConfigurationValueKind.Duration, defaultValue: "00:00:00", advanced: true),
        Field("commandOffTime", DriverConfigurationValueKind.Duration, defaultValue: "00:00:00", advanced: true),
        Field("analogCommandVariation", DriverConfigurationValueKind.Enum, allowedValues: ["int32", "int16", "float32", "float64"], advanced: true, description: "Group 41 command variation; must match the canonical TAG data type.")
    ];

    private static DriverConfigurationFieldDescriptor Field(
        string key,
        DriverConfigurationValueKind kind,
        bool required = false,
        string? defaultValue = null,
        IReadOnlyCollection<string>? allowedValues = null,
        double? minimum = null,
        double? maximum = null,
        bool advanced = false,
        string? description = null) => new(
            key,
            kind,
            Required: required,
            Description: description,
            DefaultValue: defaultValue,
            AllowedValues: allowedValues,
            Minimum: minimum,
            Maximum: maximum,
            Advanced: advanced);
}
