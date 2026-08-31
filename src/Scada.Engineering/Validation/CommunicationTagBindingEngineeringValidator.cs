using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Validation;

internal static class CommunicationTagBindingEngineeringValidator
{
    public const int IntroducedSchemaVersion = 15;

    private static readonly string[] SensitiveKeyFragments =
    {
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
        "privatekey", "private_key", "credential", "clientsecret", "client_secret"
    };

    private static readonly string[] SensitiveValueFragments =
    {
        "password=", "passwd=", "pwd=", "token=", "apikey=", "api_key=",
        "clientsecret=", "client_secret=", "privatekey=", "private_key="
    };

    public static IReadOnlyCollection<ImportIssue> Validate(
        TagEngineeringDto tag,
        int packageSchemaVersion)
    {
        if (tag.CommunicationBinding is null)
            return [];

        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(tag.Path) ? tag.Name : tag.Path;
        var binding = tag.CommunicationBinding;

        if (packageSchemaVersion < IntroducedSchemaVersion)
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_SCHEMA_VERSION",
                $"TAG '{key}' declares CommunicationBinding but package schema v{packageSchemaVersion} predates schema v{IntroducedSchemaVersion}.",
                key));
        }

        try
        {
            binding.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_INVALID",
                $"TAG '{key}' has invalid CommunicationBinding: {ex.Message}",
                key));
        }

        if (string.IsNullOrWhiteSpace(tag.Source))
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_SOURCE_REQUIRED",
                $"TAG '{key}' requires Source when CommunicationBinding is configured.",
                key));
        }

        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_ADDRESS_REQUIRED",
                $"TAG '{key}' requires legacy Address compatibility text when CommunicationBinding is configured.",
                key));
        }
        else if (!string.Equals(tag.Address, binding.PortableAddress, StringComparison.Ordinal))
        {
            issues.Add(Error(
                "TAG_COMMUNICATION_BINDING_ADDRESS_MISMATCH",
                $"TAG '{key}' Address must exactly match CommunicationBinding.PortableAddress during v15 compatibility migration.",
                key));
        }

        foreach (var setting in binding.EffectiveSettings)
        {
            var normalizedKey = setting.Key
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(".", string.Empty, StringComparison.Ordinal);
            var value = setting.Value ?? string.Empty;
            if (SensitiveKeyFragments.Any(fragment => normalizedKey.Contains(fragment, StringComparison.OrdinalIgnoreCase)) ||
                SensitiveValueFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error(
                    "TAG_COMMUNICATION_BINDING_PLAINTEXT_SECRET",
                    $"TAG '{key}' CommunicationBinding setting '{setting.Key}' appears to contain protected material. Use Data Source secretReferences instead.",
                    key));
            }
        }

        return issues;
    }

    private static ImportIssue Error(string code, string message, string key) =>
        new(code, message, ImportEntityKind.Tag, key, true);
}
