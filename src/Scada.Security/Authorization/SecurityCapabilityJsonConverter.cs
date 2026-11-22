using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scada.Security.Authorization;

/// <summary>
/// Preserves the established lower-camel Engineering wire aliases while resolving them
/// through the explicit Authority contract. Numeric enum values and unknown IDs are rejected.
/// </summary>
public sealed class SecurityCapabilityJsonConverter : JsonConverter<SecurityCapability>
{
    public override SecurityCapability Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            !AuthorityPolicyContract.TryParseCapabilityId(reader.GetString(), out var capability))
        {
            throw new JsonException("Unknown Authority capability ID.");
        }

        return capability;
    }

    public override void Write(
        Utf8JsonWriter writer,
        SecurityCapability value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(AuthorityPolicyContract.GetEngineeringWireId(value));
}
