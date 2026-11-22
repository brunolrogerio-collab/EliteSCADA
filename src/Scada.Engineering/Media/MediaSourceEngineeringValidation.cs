using Scada.Engineering.Contracts;

namespace Scada.Engineering.Media;

public sealed record MediaSourceValidationIssue(string Code, string Message);

public static class MediaSourceEngineeringValidation
{
    public const int MaximumEndpointLength = 2048;

    public static IReadOnlyList<MediaSourceValidationIssue> Validate(
        MediaSourceEngineeringDto? source)
    {
        if (source is null)
            return [new("MEDIA_SOURCE_REQUIRED", "A media source definition is required.")];

        var issues = new List<MediaSourceValidationIssue>();
        ValidateIdentity(source, issues);
        ValidateEndpoint(source, issues);
        if (!Enum.IsDefined(source.Protocol))
            issues.Add(new("MEDIA_SOURCE_PROTOCOL_INVALID", "The media transport protocol is not supported."));

        return issues;
    }

    private static void ValidateIdentity(
        MediaSourceEngineeringDto source,
        ICollection<MediaSourceValidationIssue> issues)
    {
        if (source.Id == Guid.Empty)
            issues.Add(new("MEDIA_SOURCE_ID_INVALID", "A media source ID cannot be empty."));
        if (string.IsNullOrWhiteSpace(source.Key) || source.Key.Length > 128 ||
            !string.Equals(source.Key, source.Key.Trim(), StringComparison.Ordinal) ||
            source.Key.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
            issues.Add(new("MEDIA_SOURCE_KEY_INVALID", "The media source key must be 1–128 letters, digits, dots, hyphens or underscores."));
        if (string.IsNullOrWhiteSpace(source.Name) || source.Name.Length > 128 ||
            !string.Equals(source.Name, source.Name.Trim(), StringComparison.Ordinal) ||
            source.Name.Any(char.IsControl))
            issues.Add(new("MEDIA_SOURCE_NAME_INVALID", "The media source name must be 1–128 printable characters."));
    }

    private static void ValidateEndpoint(
        MediaSourceEngineeringDto source,
        ICollection<MediaSourceValidationIssue> issues)
    {
        var endpoint = source.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > MaximumEndpointLength ||
            !string.Equals(endpoint, endpoint.Trim(), StringComparison.Ordinal) ||
            endpoint.Any(char.IsControl) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            issues.Add(new("MEDIA_SOURCE_ENDPOINT_INVALID", "The endpoint must be a valid absolute media URI."));
            return;
        }

        var expectedScheme = source.Protocol switch
        {
            MediaSourceProtocol.Http or MediaSourceProtocol.Hls or MediaSourceProtocol.Mjpeg =>
                uri.Scheme is "http" or "https",
            MediaSourceProtocol.Rtsp => uri.Scheme is "rtsp" or "rtsps",
            _ => false
        };

        if (!expectedScheme || string.IsNullOrWhiteSpace(uri.Host))
            issues.Add(new("MEDIA_SOURCE_SCHEME_MISMATCH", "The endpoint scheme does not match the selected media protocol."));
        if (!string.IsNullOrEmpty(uri.UserInfo))
            issues.Add(new("MEDIA_SOURCE_INLINE_CREDENTIALS", "Credentials must be provisioned separately; endpoint user-info is not allowed."));
        if (!string.IsNullOrEmpty(uri.Query))
            issues.Add(new("MEDIA_SOURCE_QUERY_NOT_ALLOWED", "Query parameters are not accepted in media endpoints; provision credentials through protected material instead."));
        if (!string.IsNullOrEmpty(uri.Fragment))
            issues.Add(new("MEDIA_SOURCE_FRAGMENT_NOT_ALLOWED", "Media endpoints must not contain a URI fragment."));
        if (!uri.IsDefaultPort && (uri.Port is < 1 or > 65535))
            issues.Add(new("MEDIA_SOURCE_PORT_INVALID", "The endpoint port is outside the supported range."));
    }
}
