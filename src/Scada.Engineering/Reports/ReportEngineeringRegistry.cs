namespace Scada.Engineering.Reports;

public interface IReportEngineeringRegistry
{
    IReadOnlyCollection<ReportEngineeringDto> SnapshotReports();
    ReportEngineeringDto? Find(Guid id);
    ReportEngineeringDto? FindByKey(string key);
    void Upsert(ReportEngineeringDto report);
    void Clear();
}

public sealed class InMemoryReportEngineeringRegistry : IReportEngineeringRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ReportEngineeringDto> _byId = new();
    private readonly Dictionary<string, Guid> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;

    public InMemoryReportEngineeringRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public IReadOnlyCollection<ReportEngineeringDto> SnapshotReports()
    {
        lock (_sync)
            return _byId.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public ReportEngineeringDto? Find(Guid id)
    {
        lock (_sync) return _byId.GetValueOrDefault(id);
    }

    public ReportEngineeringDto? FindByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        lock (_sync)
            return _byKey.TryGetValue(key, out var id) ? _byId.GetValueOrDefault(id) : null;
    }

    public void Upsert(ReportEngineeringDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Key);
        if (report.Id == Guid.Empty)
            throw new ArgumentException("Report Id cannot be empty.", nameof(report));

        lock (_sync)
        {
            var existing = ResolveExisting(report.Id, report.Key);
            var id = report.Id ?? existing?.Id ?? Guid.NewGuid();
            var normalized = NormalizeNestedIdentity(report with { Id = id }, existing);

            if (_byId.TryGetValue(id, out var previous) &&
                !previous.Key.Equals(normalized.Key, StringComparison.OrdinalIgnoreCase))
                _byKey.Remove(previous.Key);

            if (_byKey.TryGetValue(normalized.Key, out var otherId) && otherId != id)
                _byId.Remove(otherId);

            _byId[id] = normalized;
            _byKey[normalized.Key] = id;
        }

        _changed?.Invoke();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byId.Clear();
            _byKey.Clear();
        }
        _changed?.Invoke();
    }

    private ReportEngineeringDto? ResolveExisting(Guid? id, string key)
    {
        if (id.HasValue && _byId.TryGetValue(id.Value, out var byId))
            return byId;
        return _byKey.TryGetValue(key, out var existingId)
            ? _byId.GetValueOrDefault(existingId)
            : null;
    }

    private static ReportEngineeringDto NormalizeNestedIdentity(
        ReportEngineeringDto report,
        ReportEngineeringDto? existing)
    {
        var existingSections = (existing?.Sections ?? Array.Empty<ReportSectionEngineeringDto>())
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var sections = (report.Sections ?? Array.Empty<ReportSectionEngineeringDto>())
            .Select(section =>
            {
                existingSections.TryGetValue(section.Key, out var previousSection);
                var sectionId = section.Id ?? previousSection?.Id ?? Guid.NewGuid();
                var existingControls = (previousSection?.Controls ?? Array.Empty<ReportControlEngineeringDto>())
                    .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Key))
                    .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

                var controls = (section.Controls ?? Array.Empty<ReportControlEngineeringDto>())
                    .Select(control =>
                    {
                        existingControls.TryGetValue(control.Key, out var previousControl);
                        return control with { Id = control.Id ?? previousControl?.Id ?? Guid.NewGuid() };
                    })
                    .ToArray();

                return section with { Id = sectionId, Controls = controls };
            })
            .ToArray();

        return report with { Sections = sections };
    }
}
