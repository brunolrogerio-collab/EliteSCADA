using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Validation;

internal static class CommunicationTagBindingEngineeringValidator
{
    private static readonly string[] SensitiveKeyFragments =
    {
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
        "privatekey", "private_key", "credential", "clientsecret", "client_secret"
    };

    private static readonly string[] SensitiveValueFragments =
    {
        "password=", "passwd=", "pwd=", "token=", "apikey=", "api_key=", "clientsecret=", "client_secret="
    };

    public static IReadOnlyCollection<ImportIssue> Validate(TagEngineeringDto tag)
    {
        var binding = tag.CommunicationBinding;
        if (binding is null)
            return Array.Empty<ImportIssue>();

        var key = string.IsNullOrWhiteSpace(tag.Path) ? tag.Name : tag.Path;
        var issues = new List<ImportIssue>();

        try
        {
            binding.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_INVALID",
                $"TAG '{key}' communication binding is invalid: {ex.Message}",
                key));
            return issues;
        }

        if (string.IsNullOrWhiteSpace(tag.Source))
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_SOURCE_REQUIRED",
                $"TAG '{key}' requires a Data Source when a communication binding is configured.",
                key));
        }

        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_ADDRESS_REQUIRED",
                $"TAG '{key}' must retain Address as the compatibility alias for its canonical portable address in schema v14.",
                key));
        }
        else if (!string.Equals(tag.Address, binding.PortableAddress, StringComparison.Ordinal))
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_ADDRESS_MISMATCH",
                $"TAG '{key}' Address and canonical communication binding PortableAddress must match exactly.",
                key));
        }

        ValidateSettings(tag, binding, key, issues);
        ValidateTransform(tag, binding.ValueTransform, key, issues);
        return issues;
    }

    private static void ValidateSettings(
        TagEngineeringDto tag,
        CommunicationTagBinding binding,
        string key,
        List<ImportIssue> issues)
    {
        foreach (var setting in binding.EffectiveSettings)
        {
            var normalizedKey = setting.Key
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal);

            if (SensitiveKeyFragments.Any(fragment => normalizedKey.Contains(fragment, StringComparison.OrdinalIgnoreCase)) ||
                SensitiveValueFragments.Any(fragment => setting.Value.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error(
                    "TAG_BINDING_PLAINTEXT_SECRET",
                    $"TAG '{key}' communication binding setting '{setting.Key}' appears to contain protected material. Store secrets only as Data Source secretReferences.",
                    key));
            }
        }
    }

    private static void ValidateTransform(
        TagEngineeringDto tag,
        TagPhysicalValueTransform? transform,
        string key,
        List<ImportIssue> issues)
    {
        if (transform is null || transform.IsIdentity)
            return;

        if (tag.DataType is TagDataType.String or TagDataType.DateTime or TagDataType.Enum)
        {
            issues.Add(Error(
                "TAG_BINDING_TRANSFORM_TYPE_INVALID",
                $"TAG '{key}' data type '{tag.DataType}' cannot use byte/word transformation.",
                key));
            return;
        }

        if (tag.DataType == TagDataType.Boolean && tag.AddressSelector is null)
        {
            issues.Add(Error(
                "TAG_BINDING_TRANSFORM_BOOLEAN_SELECTOR_REQUIRED",
                $"Boolean TAG '{key}' can use byte/word transformation only when it selects a bit from a driver-owned integer source value.",
                key));
            return;
        }

        if (tag.DataType == TagDataType.Int16 && transform.WordSwap)
        {
            issues.Add(Error(
                "TAG_BINDING_WORD_SWAP_WIDTH_INVALID",
                $"TAG '{key}' cannot use Word Swap on a single 16-bit canonical value.",
                key));
        }
    }

    private static ImportIssue Error(string code, string message, string key) =>
        new(code, message, ImportEntityKind.Tag, key, true);
}
