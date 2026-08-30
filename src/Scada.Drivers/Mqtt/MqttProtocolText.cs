using System.Text;

namespace Scada.Drivers.Mqtt;

internal static class MqttProtocolText
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void ValidateUtf8EncodedString(
        string value,
        string parameterName,
        bool allowEmpty = true)
    {
        if (value is null)
            throw new ArgumentNullException(parameterName);
        if (!allowEmpty && value.Length == 0)
            throw new ArgumentException("MQTT UTF-8 encoded string must not be empty.", parameterName);
        if (value.IndexOf('\0') >= 0)
            throw new ArgumentException("MQTT UTF-8 encoded string must not contain U+0000.", parameterName);

        int byteCount;
        try
        {
            byteCount = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException ex)
        {
            throw new ArgumentException(
                "MQTT UTF-8 encoded string must be well-formed Unicode and must not contain unpaired UTF-16 surrogates.",
                parameterName,
                ex);
        }

        if (byteCount > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"MQTT UTF-8 encoded string exceeds the protocol limit of {ushort.MaxValue} bytes.",
                parameterName);
        }
    }
}
