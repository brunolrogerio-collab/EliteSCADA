using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// Library-independent compiled runtime plan for one external communication Data
/// Source. Protocol-specific plan records may expose richer public properties,
/// but they must keep SDK/library/session objects out of this boundary.
/// </summary>
public interface ICommunicationDriverRuntimePlan
{
    string DataSourceKey { get; }
    string Name { get; }
    string DriverType { get; }
    IReadOnlyCollection<TagDefinition> Tags { get; }
}

public sealed record CommunicationDriverRuntimePlanningResult(
    ICommunicationDriverRuntimePlan? Plan,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate =>
        Plan is not null && Issues.All(x => x.Severity != DriverEngineeringIssueSeverity.Error);
}

public interface ICommunicationDriverRuntimePlanner
{
    string DriverType { get; }
    CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource);
}

public sealed record CommunicationDriverRuntimeServices(
    string ProjectKey,
    ICurrentTagCache Cache,
    ITagRegistry Registry,
    ICommunicationDriverProtectedMaterialResolver? ProtectedMaterialResolver = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectKey))
            throw new ArgumentException("Project key is required for communication runtime services.", nameof(ProjectKey));
        if (!string.Equals(ProjectKey, ProjectKey.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Project key must not contain leading or trailing whitespace.", nameof(ProjectKey));
        ArgumentNullException.ThrowIfNull(Cache);
        ArgumentNullException.ThrowIfNull(Registry);
    }
}

public interface ICommunicationDriverRuntimeFactory
{
    string DriverType { get; }
    ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services);
}

public sealed record CommunicationDriverRuntimeComponentRegistration(
    ICommunicationDriverRuntimePlanner Planner,
    ICommunicationDriverRuntimeFactory Factory)
{
    public string DriverType => Planner.DriverType;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Planner);
        ArgumentNullException.ThrowIfNull(Factory);
        ValidateDriverType(Planner.DriverType, nameof(Planner));
        ValidateDriverType(Factory.DriverType, nameof(Factory));

        if (!string.Equals(Planner.DriverType, Factory.DriverType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Runtime planner type '{Planner.DriverType}' does not match factory type '{Factory.DriverType}'.");
    }

    private static void ValidateDriverType(string driverType, string componentName)
    {
        if (string.IsNullOrWhiteSpace(driverType))
            throw new InvalidOperationException($"{componentName} must declare a DriverType.");
        if (!string.Equals(driverType, driverType.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException($"{componentName} DriverType must not contain leading or trailing whitespace.");
    }
}

public sealed class CommunicationDriverRuntimeComponentRegistry
{
    private readonly Dictionary<string, CommunicationDriverRuntimeComponentRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CommunicationDriverRuntimeComponentRegistration> Registrations =>
        _registrations.Values.OrderBy(x => x.DriverType, StringComparer.OrdinalIgnoreCase).ToArray();

    public void Register(CommunicationDriverRuntimeComponentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        registration.Validate();
        if (!_registrations.TryAdd(registration.DriverType, registration))
            throw new InvalidOperationException($"Runtime components for DriverType '{registration.DriverType}' are already registered.");
    }

    public bool TryGet(string driverType, out CommunicationDriverRuntimeComponentRegistration? registration)
    {
        if (string.IsNullOrWhiteSpace(driverType))
        {
            registration = null;
            return false;
        }

        return _registrations.TryGetValue(driverType.Trim(), out registration);
    }

    public CommunicationDriverRuntimeComponentRegistration GetRequired(string driverType)
    {
        if (TryGet(driverType, out var registration) && registration is not null)
            return registration;
        throw new KeyNotFoundException($"Runtime components for DriverType '{driverType}' are not registered.");
    }
}
