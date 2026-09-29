using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Historian;

namespace Scada.DriverHost.Engineering;

public sealed record HistorianCaptureProfileRuntimeResolution(
    EngineeringPackage Package,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate => Issues.All(x => !x.IsError);
}

public static class HistorianCaptureProfileRuntimeResolver
{
    private const string IssueSource = "historian.capture";

    public static HistorianCaptureProfileRuntimeResolution Resolve(EngineeringPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var issues = new List<EngineeringDriverIssue>();
        var profiles = package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>();
        var profilesById = new Dictionary<Guid, HistorianCaptureProfileEngineeringDto>();

        foreach (var profile in profiles)
        {
            if (profile is null)
            {
                issues.Add(new(
                    "HISTORIAN_CAPTURE_PROFILE_NULL",
                    "Historian capture profile cannot be null.",
                    IssueSource));
                continue;
            }

            foreach (var problem in HistorianCaptureProfileEngineeringValidation.Validate(profile))
                issues.Add(new(problem.Code, problem.Message, IssueSource));

            if (!profile.Id.HasValue || profile.Id.Value == Guid.Empty)
            {
                issues.Add(new(
                    "HISTORIAN_CAPTURE_PROFILE_STABLE_ID_REQUIRED",
                    $"Historian capture profile '{profile.Key}' requires a stable non-empty identity before Runtime activation.",
                    IssueSource));
                continue;
            }

            if (!profilesById.TryAdd(profile.Id.Value, profile))
            {
                issues.Add(new(
                    "HISTORIAN_CAPTURE_PROFILE_ID_DUPLICATE",
                    $"Historian capture profile identity '{profile.Id.Value:D}' is duplicated in the Active package.",
                    IssueSource));
            }
        }

        var resolvedTags = new List<TagEngineeringDto>(package.Tags.Count);
        foreach (var tag in package.Tags)
        {
            if (!tag.HistorianCaptureProfileId.HasValue)
            {
                // Frozen compatibility rule: inline TAG policy remains authoritative
                // until a stable profile reference is deliberately attached.
                resolvedTags.Add(tag);
                continue;
            }

            var profileId = tag.HistorianCaptureProfileId.Value;
            if (profileId == Guid.Empty || !profilesById.TryGetValue(profileId, out var profile))
            {
                issues.Add(new(
                    "HISTORIAN_CAPTURE_PROFILE_NOT_FOUND",
                    $"TAG '{tag.Path}' references Historian capture profile '{profileId:D}', which is not present in the Active package.",
                    IssueSource,
                    tag.Path));
                resolvedTags.Add(tag);
                continue;
            }

            var tagProblems = HistorianCaptureProfileEngineeringValidation.ValidateForTag(profile, tag.DataType);
            foreach (var problem in tagProblems)
            {
                issues.Add(new(
                    problem.Code,
                    $"TAG '{tag.Path}': {problem.Message}",
                    IssueSource,
                    tag.Path));
            }

            if (tagProblems.Count > 0)
            {
                resolvedTags.Add(tag);
                continue;
            }

            resolvedTags.Add(tag with
            {
                Historian = ToEffectiveSettings(profile)
            });
        }

        return new HistorianCaptureProfileRuntimeResolution(
            package with { Tags = resolvedTags.ToArray() },
            issues);
    }

    private static HistorianSettingsDto ToEffectiveSettings(HistorianCaptureProfileEngineeringDto profile) =>
        new(
            Enabled: true,
            Strategy: profile.Strategy switch
            {
                HistorianCaptureStrategy.Periodic => "periodic",
                HistorianCaptureStrategy.OnChange => "onChange",
                HistorianCaptureStrategy.OnChangeDeadband => "onChangeDeadband",
                HistorianCaptureStrategy.OnChangeDeadbandMaxInterval => "onChangeDeadbandMaxInterval",
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.Strategy, "Unsupported Historian capture strategy.")
            },
            Deadband: profile.Deadband,
            PeriodMilliseconds: profile.PeriodMilliseconds,
            MaximumPeriodMilliseconds: profile.MaximumIntervalMilliseconds);
}
