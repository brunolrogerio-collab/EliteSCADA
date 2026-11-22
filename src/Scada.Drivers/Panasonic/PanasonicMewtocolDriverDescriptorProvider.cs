using Scada.Drivers.Abstractions;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Panasonic;

public static class PanasonicMewtocolDriverDescriptorProvider
{
    public const string TcpDriverTypeId = "panasonic.mewtocol.tcp";
    public const string SerialDriverTypeId = "panasonic.mewtocol.serial";
    public const string BindingSchemaId = "panasonic.mewtocol.tag";
    public const int BindingSchemaVersion = 1;

    public static CommunicationDriverTypeDescriptor TcpDescriptor { get; } = CreateDescriptor(isSerial: false);
    public static CommunicationDriverTypeDescriptor SerialDescriptor { get; } = CreateDescriptor(isSerial: true);

    public static CommunicationDriverTypeDescriptor For(string driverType) =>
        string.Equals(driverType, TcpDriverTypeId, StringComparison.OrdinalIgnoreCase) ? TcpDescriptor
        : string.Equals(driverType, SerialDriverTypeId, StringComparison.OrdinalIgnoreCase) ? SerialDescriptor
        : throw new ArgumentException("Driver type must be a Panasonic MEWTOCOL TCP or Host Serial profile.", nameof(driverType));

    private static CommunicationDriverTypeDescriptor CreateDescriptor(bool isSerial)
    {
        static DriverConfigurationFieldDescriptor Field(
            string key,
            DriverConfigurationValueKind kind,
            bool required = false,
            string? defaultValue = null,
            double? minimum = null,
            double? maximum = null,
            IReadOnlyCollection<string>? allowed = null,
            bool advanced = false) =>
            new(key, kind, required, key, DefaultValue: defaultValue, Minimum: minimum, Maximum: maximum,
                AllowedValues: allowed, Advanced: advanced);

        var sourceFields = new List<DriverConfigurationFieldDescriptor>();
        if (isSerial)
        {
            sourceFields.Add(Field("serialPort", DriverConfigurationValueKind.SerialPort, required: true));
            sourceFields.Add(Field("baudRate", DriverConfigurationValueKind.Integer, defaultValue: "9600", minimum: 300, maximum: 230400));
            sourceFields.Add(Field("dataBits", DriverConfigurationValueKind.Integer, defaultValue: "8", minimum: 7, maximum: 8));
            sourceFields.Add(Field("parity", DriverConfigurationValueKind.Enum, defaultValue: nameof(HostSerialParity.None), allowed: new[] { nameof(HostSerialParity.None), nameof(HostSerialParity.Odd), nameof(HostSerialParity.Even) }));
            sourceFields.Add(Field("stopBits", DriverConfigurationValueKind.Enum, defaultValue: nameof(HostSerialStopBits.One), allowed: new[] { nameof(HostSerialStopBits.One), nameof(HostSerialStopBits.Two) }));
            sourceFields.Add(Field("turnaroundMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "0", minimum: 0, maximum: 1000, advanced: true));
        }
        else
        {
            sourceFields.Add(Field("host", DriverConfigurationValueKind.Host, required: true));
            sourceFields.Add(Field("port", DriverConfigurationValueKind.Port, required: true, minimum: 1, maximum: 65535));
            sourceFields.Add(Field("connectTimeoutMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "5000", minimum: 100, maximum: 60000, advanced: true));
        }
        sourceFields.Add(Field("station", DriverConfigurationValueKind.Integer, required: true, minimum: 1, maximum: 99));
        sourceFields.Add(Field("familyProfile", DriverConfigurationValueKind.Enum, required: true,
            allowed: Enum.GetNames<PanasonicMewtocolFamilyProfile>()));
        sourceFields.Add(Field("frameMode", DriverConfigurationValueKind.Enum, defaultValue: nameof(PanasonicMewtocolFrameMode.Standard),
            allowed: Enum.GetNames<PanasonicMewtocolFrameMode>(), advanced: true));
        sourceFields.Add(Field("scanIntervalMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "1000", minimum: 10, maximum: 600000));
        sourceFields.Add(Field("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "3000", minimum: 250, maximum: 60000, advanced: true));

        var tagFields = new[]
        {
            Field("address", DriverConfigurationValueKind.Identifier, required: true),
            Field("physicalDataType", DriverConfigurationValueKind.Enum, required: true,
                allowed: Enum.GetNames<PanasonicMewtocolPhysicalType>()),
            Field("writable", DriverConfigurationValueKind.Boolean, defaultValue: "false")
        };

        var driverType = isSerial ? SerialDriverTypeId : TcpDriverTypeId;
        return new CommunicationDriverTypeDescriptor(
            driverType,
            isSerial ? "Panasonic MEWTOCOL-COM Host Serial" : "Panasonic MEWTOCOL-COM TCP",
            1,
            DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
            DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.PointReadTest,
            new[] { DriverAcquisitionMode.Polling },
            new DriverConfigurationSchemaDescriptor(driverType, 1, sourceFields, tagFields),
            Description: "Classic ASCII MEWTOCOL-COM with bounded FP0R F32, FP-XH common and current FP7 R-series profiles.",
            TagBindingSchemaId: BindingSchemaId,
            TagBindingSchemaVersion: BindingSchemaVersion,
            IntegrationDomains: new[] { IntegrationDomain.Industrial },
            ConnectionModel: isSerial ? DriverConnectionModel.HostSerial : DriverConnectionModel.DirectNetwork,
            ExternalDependencies: isSerial
                ? new[]
                {
                    new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn),
                    new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.HostResource, "Host Serial #469", "Port ownership and transactions are managed by the EliteSCADA Host Serial coordinator.")
                }
                : new[] { new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn) });
    }
}
