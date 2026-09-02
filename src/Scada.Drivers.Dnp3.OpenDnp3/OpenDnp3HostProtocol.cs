using System.Globalization;
using System.Text;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Dnp3.OpenDnp3;

internal abstract record OpenDnp3HostMessage;
internal sealed record OpenDnp3HostReadyMessage : OpenDnp3HostMessage;
internal sealed record OpenDnp3HostStateMessage(Dnp3SessionState State) : OpenDnp3HostMessage;
internal sealed record OpenDnp3HostMeasurementMessage(Dnp3Measurement Measurement) : OpenDnp3HostMessage;
internal sealed record OpenDnp3HostCommandMessage(long RequestId, Dnp3CommandResult Result) : OpenDnp3HostMessage;

internal static class OpenDnp3HostProtocol
{
    public const string VersionToken = "V1";

    public static OpenDnp3HostMessage Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new FormatException("OpenDNP3 host emitted an empty protocol line.");

        var parts = line.Split('\t');
        if (parts.Length < 2 || !parts[0].Equals(VersionToken, StringComparison.Ordinal))
            throw new FormatException("OpenDNP3 host protocol version is missing or unsupported.");

        return parts[1] switch
        {
            "READY" => ParseReady(parts),
            "STATE" => ParseState(parts),
            "MEASUREMENT" => ParseMeasurement(parts),
            "COMMAND" => ParseCommand(parts),
            _ => throw new FormatException($"Unsupported OpenDNP3 host message '{parts[1]}'.")
        };
    }

    public static string BuildBinaryCommand(
        long requestId,
        ushort index,
        Dnp3BinaryOperation operation,
        Dnp3BinaryCommandProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        return string.Join(
            '\t',
            VersionToken,
            "BINARY",
            requestId.ToString(CultureInfo.InvariantCulture),
            index.ToString(CultureInfo.InvariantCulture),
            operation.ToString(),
            profile.Mode.ToString(),
            profile.TripCloseCode.ToString(),
            profile.Count.ToString(CultureInfo.InvariantCulture),
            ToMilliseconds(profile.OnTime).ToString(CultureInfo.InvariantCulture),
            ToMilliseconds(profile.OffTime).ToString(CultureInfo.InvariantCulture));
    }

    public static string BuildAnalogCommand(
        long requestId,
        ushort index,
        object value,
        Dnp3AnalogCommandProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(profile.Variation) || !Enum.IsDefined(profile.Mode))
            throw new ArgumentOutOfRangeException(nameof(profile), "DNP3 analog command profile contains an unsupported enum value.");

        var wireValue = value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("DNP3 analog command value must be an invariant-formattable numeric value.", nameof(value))
        };

        return string.Join(
            '\t',
            VersionToken,
            "ANALOG",
            requestId.ToString(CultureInfo.InvariantCulture),
            index.ToString(CultureInfo.InvariantCulture),
            profile.Variation.ToString(),
            profile.Mode.ToString(),
            wireValue);
    }

    private static OpenDnp3HostMessage ParseReady(string[] parts)
    {
        if (parts.Length != 2)
            throw new FormatException("READY message has unexpected fields.");
        return new OpenDnp3HostReadyMessage();
    }

    private static OpenDnp3HostMessage ParseState(string[] parts)
    {
        if (parts.Length != 3 || !Enum.TryParse<Dnp3SessionState>(parts[2], true, out var state))
            throw new FormatException("STATE message contains an invalid session state.");
        return new OpenDnp3HostStateMessage(state);
    }

    private static OpenDnp3HostMessage ParseCommand(string[] parts)
    {
        if (parts.Length != 6)
            throw new FormatException("COMMAND message has unexpected fields.");

        var requestId = ParseLong(parts[2], "command request id");
        var succeeded = ParseBoolean(parts[3], "command success flag");
        if (string.IsNullOrWhiteSpace(parts[4]))
            throw new FormatException("COMMAND message status is required.");

        var message = string.IsNullOrEmpty(parts[5])
            ? null
            : Encoding.UTF8.GetString(Convert.FromBase64String(parts[5]));
        var result = succeeded
            ? Dnp3CommandResult.Success(parts[4]) with { Message = message }
            : Dnp3CommandResult.Failure(parts[4], message);
        return new OpenDnp3HostCommandMessage(requestId, result);
    }

    private static OpenDnp3HostMessage ParseMeasurement(string[] parts)
    {
        if (parts.Length != 22)
            throw new FormatException($"MEASUREMENT message has {parts.Length} fields; expected 22.");
        if (!Enum.TryParse<Dnp3PointKind>(parts[2], true, out var kind))
            throw new FormatException("MEASUREMENT message contains an invalid point kind.");

        var index = ParseUShort(parts[3], "measurement index");
        var group = ParseByte(parts[4], "measurement group");
        var variation = ParseByte(parts[5], "measurement variation");
        var isEvent = ParseBoolean(parts[6], "event flag");
        var flags = new Dnp3PointFlagSet(
            ParseBoolean(parts[7], "flags-present flag"),
            ParseBoolean(parts[8], "online flag"),
            ParseBoolean(parts[9], "restart flag"),
            ParseBoolean(parts[10], "communication-lost flag"),
            ParseBoolean(parts[11], "remote-forced flag"),
            ParseBoolean(parts[12], "local-forced flag"),
            ParseBoolean(parts[13], "chatter-filter flag"),
            ParseBoolean(parts[14], "over-range flag"),
            ParseBoolean(parts[15], "rollover flag"),
            ParseBoolean(parts[16], "discontinuity flag"),
            ParseBoolean(parts[17], "reference-error flag"));
        DateTimeOffset? timestamp = string.IsNullOrEmpty(parts[18])
            ? null
            : DateTimeOffset.FromUnixTimeMilliseconds(ParseLong(parts[18], "source timestamp"));
        var synchronized = ParseBoolean(parts[19], "timestamp synchronization flag");
        var value = ParseValue(parts[20], parts[21]);

        return new OpenDnp3HostMeasurementMessage(
            new Dnp3Measurement(
                kind,
                index,
                value,
                new Dnp3ObjectVariation(group, variation),
                isEvent,
                flags,
                timestamp,
                synchronized));
    }

    private static object ParseValue(string type, string value) => type switch
    {
        "bool" => ParseBoolean(value, "boolean measurement value"),
        "i16" => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        "i32" => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        "i64" => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        "f32" => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        "f64" => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        "enum" => Enum.IsDefined(typeof(Dnp3DoubleBitState), int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture))
            ? (Dnp3DoubleBitState)int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)
            : throw new FormatException("Double-bit measurement contains an invalid state."),
        _ => throw new FormatException($"Unsupported OpenDNP3 measurement value type '{type}'.")
    };

    private static bool ParseBoolean(string value, string field) => value switch
    {
        "1" or "true" or "TRUE" => true,
        "0" or "false" or "FALSE" => false,
        _ => throw new FormatException($"Invalid {field} '{value}'.")
    };

    private static byte ParseByte(string value, string field) =>
        byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {field} '{value}'.");

    private static ushort ParseUShort(string value, string field) =>
        ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {field} '{value}'.");

    private static long ParseLong(string value, string field) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Invalid {field} '{value}'.");

    private static long ToMilliseconds(TimeSpan value) => checked((long)value.TotalMilliseconds);
}
