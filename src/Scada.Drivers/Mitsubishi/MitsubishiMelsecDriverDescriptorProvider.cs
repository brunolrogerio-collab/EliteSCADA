using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Mitsubishi;

public static class MitsubishiMelsecDriverDescriptorProvider
{
    public const string DriverTypeId = "mitsubishi.melsec.mc";
    public const string BindingSchemaId = "mitsubishi.melsec.mc";
    public const int BindingSchemaVersion = 1;

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } = CreateDescriptor();

    private static CommunicationDriverTypeDescriptor CreateDescriptor()
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

        var sourceFields = new[]
        {
            Field("host", DriverConfigurationValueKind.Host, required: true),
            Field("port", DriverConfigurationValueKind.Port, defaultValue: "5007", minimum: 1, maximum: 65535),
            Field("familyProfile", DriverConfigurationValueKind.Enum, required: true,
                allowed: Enum.GetNames<MitsubishiMelsecFamilyProfile>()),
            Field("scanIntervalMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "1000", minimum: 10, maximum: 600000),
            Field("connectTimeoutMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "5000", minimum: 100, maximum: 60000, advanced: true),
            Field("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, defaultValue: "6000", minimum: 250, maximum: 60000, advanced: true),
            Field("monitoringTimerUnits", DriverConfigurationValueKind.Integer, defaultValue: "20", minimum: 1, maximum: ushort.MaxValue, advanced: true),
            Field("networkNo", DriverConfigurationValueKind.Integer, defaultValue: "0", minimum: 0, maximum: 239, advanced: true),
            Field("stationNo", DriverConfigurationValueKind.Integer, defaultValue: "255", minimum: 1, maximum: 255, advanced: true),
            Field("moduleIoNo", DriverConfigurationValueKind.Identifier, defaultValue: "03FF", advanced: true),
            Field("multidropStationNo", DriverConfigurationValueKind.Integer, defaultValue: "0", minimum: 0, maximum: 31, advanced: true),
            Field("maxWordsPerRequest", DriverConfigurationValueKind.Integer, defaultValue: "64", minimum: 2, maximum: 960, advanced: true),
            Field("maxBitsPerRequest", DriverConfigurationValueKind.Integer, defaultValue: "64", minimum: 1, maximum: 7168, advanced: true),
            Field("maxRandomPointsPerRequest", DriverConfigurationValueKind.Integer, defaultValue: "16", minimum: 1, maximum: 192, advanced: true),
            Field("maxBlocksPerRequest", DriverConfigurationValueKind.Integer, defaultValue: "8", minimum: 1, maximum: 120, advanced: true),
            Field("rMaximumAddress", DriverConfigurationValueKind.Integer, minimum: 0, maximum: MitsubishiMelsecAddress.MaximumWireAddress, advanced: true)
        };
        var tagFields = new[]
        {
            Field("address", DriverConfigurationValueKind.Identifier, required: true),
            Field("physicalDataType", DriverConfigurationValueKind.Enum, required: true,
                allowed: Enum.GetNames<MitsubishiMelsecPhysicalType>()),
            Field("writable", DriverConfigurationValueKind.Boolean, defaultValue: "false")
        };

        return new CommunicationDriverTypeDescriptor(
            DriverTypeId,
            "Mitsubishi MELSEC MC / SLMP",
            1,
            DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
            DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.PointReadTest,
            new[] { DriverAcquisitionMode.Polling },
            new DriverConfigurationSchemaDescriptor(BindingSchemaId, BindingSchemaVersion, sourceFields, tagFields),
            Description: "MELSEC QnA-compatible binary SLMP 3E over TCP with bounded FX5U and iQ-R device profiles.",
            TagBindingSchemaId: BindingSchemaId,
            TagBindingSchemaVersion: BindingSchemaVersion,
            IntegrationDomains: new[] { IntegrationDomain.Industrial },
            ConnectionModel: DriverConnectionModel.DirectNetwork,
            ExternalDependencies: new[] { new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn) });
    }
}
