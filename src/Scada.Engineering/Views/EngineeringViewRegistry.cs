using Scada.Engineering.Contracts;

namespace Scada.Engineering.Views;

public interface IEngineeringViewRegistry
{
    IReadOnlyCollection<ScreenEngineeringDto> SnapshotScreens();
    IReadOnlyCollection<PopupEngineeringDto> SnapshotPopups();

    ScreenEngineeringDto? FindScreen(Guid id);
    ScreenEngineeringDto? FindScreenByKey(string key);
    PopupEngineeringDto? FindPopup(Guid id);
    PopupEngineeringDto? FindPopupByKey(string key);

    void UpsertScreen(ScreenEngineeringDto screen);
    void UpsertPopup(PopupEngineeringDto popup);
}

public sealed class InMemoryEngineeringViewRegistry : IEngineeringViewRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ScreenEngineeringDto> _screensById = new();
    private readonly Dictionary<string, Guid> _screensByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, PopupEngineeringDto> _popupsById = new();
    private readonly Dictionary<string, Guid> _popupsByKey = new(StringComparer.OrdinalIgnoreCase);

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

    public void UpsertScreen(ScreenEngineeringDto screen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screen.Key);
        var normalized = screen with { Id = screen.Id ?? Guid.NewGuid() };
        lock (_sync) UpsertByKey(normalized, normalized.Id!.Value, normalized.Key, _screensById, _screensByKey, x => x.Key);
    }

    public void UpsertPopup(PopupEngineeringDto popup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(popup.Key);
        var normalized = popup with { Id = popup.Id ?? Guid.NewGuid() };
        lock (_sync) UpsertByKey(normalized, normalized.Id!.Value, normalized.Key, _popupsById, _popupsByKey, x => x.Key);
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
