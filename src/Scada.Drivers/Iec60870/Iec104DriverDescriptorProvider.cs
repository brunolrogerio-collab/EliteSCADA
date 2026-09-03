using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Iec60870;

/// <summary>
/// Canonical IEC-104 descriptor projection. Data Source configuration keeps its
/// established elite.iec60870.5.104 schema, while TAG point bindings retain the
/// distinct elite.iec60870.5.104.point contract consumed by Runtime. Keeping
/// both identities on the driver-owned descriptor prevents UI and host code from
/// hardcoding protocol-specific schema knowledge.
/// </summary>
public sealed class Iec104DriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public const string BindingSchemaId = "elite.iec60870.5.104.point";
    public const int BindingSchemaVersion = 1;

    private static readonly IReadOnlyCollection<DriverConfigurationFieldDescriptor> BindingFields =
        new DriverConfigurationFieldDescriptor[]
        {
            new(
                "iec104.typeId",
                DriverConfigurationValueKind.Enum,
                Required: true,
                DisplayName: "Monitored Type ID",
                Description: "IEC 60870-5-104 monitored information Type ID used to decode this point.",
                AllowedValues: new[]
                {
                    "M_SP_NA_1",
                    "M_DP_NA_1",
                    "M_ME_NA_1",
                    "M_ME_NB_1",
                    "M_ME_NC_1",
                    "M_IT_NA_1"
                }),
            new(
                "iec104.commandTypeId",
                DriverConfigurationValueKind.Enum,
                DisplayName: "Command Type ID",
                Description: "Optional IEC 60870-5-104 command Type ID used when the TAG is writable.",
                AllowedValues: new[]
                {
                    "C_SC_NA_1",
                    "C_DC_NA_1",
                    "C_SE_NA_1",
                    "C_SE_NB_1",
                    "C_SE_NC_1"
                },
                Advanced: true),
            new(
                "iec104.commandMode",
                DriverConfigurationValueKind.Enum,
                DisplayName: "Command Mode",
                Description: "Direct operate or select-before-operate command behavior.",
                DefaultValue: "direct",
                AllowedValues: new[] { "direct", "sbo" },
                Advanced: true),
            new(
                "iec104.qualifier",
                DriverConfigurationValueKind.Integer,
                DisplayName: "Qualifier",
                Description: "IEC 60870-5-104 qualifier of command, from 0 to 31.",
                DefaultValue: "0",
                Minimum: 0,
                Maximum: 31,
                Advanced: true)
        };

    public static CommunicationDriverTypeDescriptor SharedDescriptor { get; } =
        Enrich(new Iec104EngineeringProvider().Descriptor);

    public CommunicationDriverTypeDescriptor Descriptor => SharedDescriptor;

    public static CommunicationDriverTypeDescriptor Enrich(CommunicationDriverTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with
        {
            ConfigurationSchema = descriptor.ConfigurationSchema with
            {
                TagBindingFields = BindingFields
            },
            TagBindingSchemaId = BindingSchemaId,
            TagBindingSchemaVersion = BindingSchemaVersion
        };
    }
}
