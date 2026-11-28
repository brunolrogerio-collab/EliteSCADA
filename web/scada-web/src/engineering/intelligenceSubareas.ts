import type { EquipmentEngineering, LocationEngineering } from './types';

type Resource = { kind: string; scopeId: string; path?: string | null; name: string };
export const unassignedArea = 'unassigned';

export function intelligenceSubareas<R extends Resource>(
  resources: readonly R[], equipment: readonly EquipmentEngineering[], locations: readonly LocationEngineering[], locale: string
) {
  const equipmentById = new Map(equipment.map(item => [item.id?.toLowerCase(), item]));
  const locationsById = new Map(locations.map(item => [item.id?.toLowerCase(), item]));
  const groups = new Map<string, { key: string; label: string; count: number }>();
  const rows = resources.map(resource => {
    const id = resource.scopeId.toLowerCase();
    const locationId = resource.kind === 'Location' ? id : equipmentById.get(id)?.locationId?.toLowerCase();
    let key = unassignedArea;
    let label = '';
    if (locationId && locationsById.has(locationId)) {
      key = 'location:' + locationId;
      let location = locationsById.get(locationId);
      const names = [];
      const seen = new Set<string>();
      while (location?.id && !seen.has(location.id)) {
        seen.add(location.id);
        names.unshift(location.name);
        location = location.parentLocationId ? locationsById.get(location.parentLocationId.toLowerCase()) : undefined;
      }
      label = names.join(' / ');
    } else {
      const segments = (resource.path ?? '').split(/[./\\]/).map(part => part.trim()).filter(Boolean);
      if (resource.kind === 'Equipment') segments.pop();
      if (segments.length) {
        label = segments.join(' / ');
        key = 'path:' + label.toLowerCase();
      }
    }
    const group = groups.get(key) ?? { key, label, count: 0 };
    group.count++;
    groups.set(key, group);
    return { resource, key, search: normalizeSearch([label, resource.name, resource.path].join(' ')) };
  });
  const collator = new Intl.Collator(locale, { numeric: true, sensitivity: 'base' });
  const areas = [...groups.values()].sort((a, b) =>
    Number(a.key === unassignedArea) - Number(b.key === unassignedArea) || collator.compare(a.label, b.label));
  return { rows, areas };
}

export function normalizeSearch(text: string) {
  return text.normalize('NFD').replace(/\p{M}/gu, '').trim().toLowerCase();
}
