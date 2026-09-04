using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.Views;

public interface IEngineeringViewRegistry
{
    IReadOnlyCollection<ScreenEngineeringDto> SnapshotScreens();
    IReadOnlyCollection<PopupEngineeringDto> SnapshotPopups();
    Guid? StartupScreenId { get; }

    ScreenEngineeringDto? FindScreen(Guid id);
    ScreenEngineeringDto? FindScreenByKey(string key);
    PopupEngineeringDto? FindPopup(Guid id);
    PopupEngineeringDto? FindPopupByKey(string key);

    void UpsertScreen(ScreenEngineeringDto screen);
    void UpsertPopup(PopupEngineeringDto popup);
    void SetStartupScreen(Guid? screenId);
}

public sealed class InMemoryEngineeringViewRegistry : IEngineeringViewRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ScreenEngineeringDto> _screensById = new();
    private readonly Dictionary<string, Guid> _screensByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, PopupEngineeringDto> _popupsById = new();
    private readonly Dictionary<string, Guid> _popupsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action? _changed;
    private Guid? _startupScreenId;

    public InMemoryEngineeringViewRegistry(Action? changed = null)
    {
        _changed = changed;
    }

    public Guid? StartupScreenId
    {
        get { lock (_sync) return _startupScreenId; }
    }

    public IReadOnlyCollection<ScreenEngineeringDto> SnapshotScreens()
    {
        lock (_sync)
            return _screensById.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<PopupEngineeringDto> SnapshotPopups()
    {
        lock (_sync)
            return _popupsById.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public ScreenEngineeringDto? FindScreen(Guid id)
    {
        lock (_sync) return _screensById.GetValueOrDefault(id);
    }

    public ScreenEngineeringDto? FindScreenByKey(string key)
    {
        lock (_sync) return _screensByKey.TryGetValue(key, out var id) ? _screensById.GetValueOrDefault(id) : null;
    }

    public PopupEngineeringDto? FindPopup(Guid id)
    {
        lock (_sync) return _popupsById.GetValueOrDefault(id);
    }

    public PopupEngineeringDto? FindPopupByKey(string key)
    {
        lock (_sync) return _popupsByKey.TryGetValue(key, out var id) ? _popupsById.GetValueOrDefault(id) : null;
    }

    public void SetStartupScreen(Guid? screenId)
    {
        if (screenId == Guid.Empty)
            throw new ArgumentException("Startup Screen Id cannot be empty.", nameof(screenId));

        lock (_sync)
        {
            if (screenId.HasValue && !_screensById.ContainsKey(screenId.Value))
                throw new ArgumentException($"Startup Screen '{screenId.Value:D}' was not found.", nameof(screenId));
            _startupScreenId = screenId;
        }
        _changed?.Invoke();
    }

    public void UpsertScreen(ScreenEngineeringDto screen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screen.Key);
        if (screen.Id == Guid.Empty)
            throw new ArgumentException("Screen Id cannot be empty.", nameof(screen));

        lock (_sync)
        {
            var existing = ResolveExisting(screen.Id, screen.Key, _screensById, _screensByKey);
            var normalizedId = screen.Id ?? existing?.Id ?? Guid.NewGuid();
            var identityNormalizedElements = VisualElementIdentity.Normalize(screen.Elements, existing?.Elements);
            var normalized = screen with
            {
                Id = normalizedId,
                Elements = VisualEngineeringPropertyMigration.NormalizeCurrentElements(identityNormalizedElements)
            };

            UpsertByKey(normalized, normalizedId, normalized.Key, _screensById, _screensByKey, x => x.Key);
        }

        _changed?.Invoke();
    }

    public void UpsertPopup(PopupEngineeringDto popup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(popup.Key);
        if (popup.Id == Guid.Empty)
            throw new ArgumentException("Popup Id cannot be empty.", nameof(popup));
        if (!double.IsFinite(popup.X) || !double.IsFinite(popup.Y))
            throw new ArgumentException("Popup X/Y must be finite logical HMI coordinates.", nameof(popup));

        lock (_sync)
        {
            var existing = ResolveExisting(popup.Id, popup.Key, _popupsById, _popupsByKey);
            var normalizedId = popup.Id ?? existing?.Id ?? Guid.NewGuid();
            var identityNormalizedElements = VisualElementIdentity.Normalize(popup.Elements, existing?.Elements);
            var normalized = popup with
            {
                Id = normalizedId,
                Elements = VisualEngineeringPropertyMigration.NormalizeCurrentElements(identityNormalizedElements)
            };

            UpsertByKey(normalized, normalizedId, normalized.Key, _popupsById, _popupsByKey, x => x.Key);
        }

        _changed?.Invoke();
    }

    public void Clear()
    {
        lock (_sync)
        {
            _screensById.Clear();
            _screensByKey.Clear();
            _popupsById.Clear();
            _popupsByKey.Clear();
            _startupScreenId = null;
        }
        _changed?.Invoke();
    }

    private static T? ResolveExisting<T>(
        Guid? id,
        string key,
        Dictionary<Guid, T> byId,
        Dictionary<string, Guid> byKey)
        where T : class
    {
        if (id.HasValue && byId.TryGetValue(id.Value, out var byStableId))
            return byStableId;

        return byKey.TryGetValue(key, out var existingId)
            ? byId.GetValueOrDefault(existingId)
            : null;
    }

    private static void UpsertByKey<T>(
        T value,
        Guid id,
        string key,
        Dictionary<Guid, T> byId,
        Dictionary<string, Guid> byKey,
        Func<T, string> keySelector)
    {
        if (byId.TryGetValue(id, out var previous) && !keySelector(previous).Equals(key, StringComparison.OrdinalIgnoreCase))
            byKey.Remove(keySelector(previous));

        if (byKey.TryGetValue(key, out var otherId) && otherId != id)
            byId.Remove(otherId);

        byId[id] = value;
        byKey[key] = id;
    }
}
