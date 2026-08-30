using Scada.Core.Tags;
using Scada.Drivers.Bacnet;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// BACnet-owned runtime construction seam prepared for the Coordinator-owned
/// communication-driver registry. This class deliberately depends only on the
/// library-independent BACnet runtime plan plus host-owned TAG services.
/// </summary>
public sealed class BacnetIpRuntimeFactory
{
    private readonly IBacnetSessionFactory _sessionFactory;

    public BacnetIpRuntimeFactory(IBacnetSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public string DriverType => BacnetDriverDescriptor.DriverType;

    public BacnetIpDriver Create(
        BacnetIpRuntimePlan plan,
        ICurrentTagCache cache,
        ITagRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);

        if (!string.Equals(plan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Runtime plan driver type '{plan.DriverType}' does not match BACnet factory type '{DriverType}'.", nameof(plan));
        if (plan.Points.Count == 0)
            throw new ArgumentException("BACnet runtime plan must contain at least one point.", nameof(plan));

        var session = _sessionFactory.Create(plan.SessionOptions)
            ?? throw new InvalidOperationException("BACnet session factory returned null.");

        return new BacnetIpDriver(
            plan.DataSourceKey,
            plan.Name,
            cache,
            registry,
            plan.Points,
            session,
            plan.ScanRate);
    }
}
