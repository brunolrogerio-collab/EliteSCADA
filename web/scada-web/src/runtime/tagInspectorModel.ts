import type {
  RuntimeTagAccessFilter,
  RuntimeTagEndpointIssue,
  RuntimeTagInspectorFilter,
  RuntimeTagInspectorSummary,
  RuntimeTagListItem,
  RuntimeTagQualityBucket,
  RuntimeTagQualityName,
  RuntimeTagRealtimeEvent
} from './tagInspectorTypes';

const numericQualityNames: RuntimeTagQualityName[] = [
  'good',
  'uncertain',
  'bad',
  'bad-communication',
  'bad-configuration',
  'bad-device',
  'stale',
  'disabled'
];

export function normalizeRuntimeTagQuality(value: string | number | null | undefined): RuntimeTagQualityName {
  if (typeof value === 'number' && Number.isInteger(value) && value >= 0 && value < numericQualityNames.length)
    return numericQualityNames[value];

  const normalized = String(value ?? '')
    .trim()
    .replace(/[_\s]+/g, '-')
    .replace(/([a-z])([A-Z])/g, '$1-$2')
    .toLowerCase();

  switch (normalized) {
    case 'good': return 'good';
    case 'uncertain': return 'uncertain';
    case 'bad': return 'bad';
    case 'badcommunication':
    case 'bad-communication': return 'bad-communication';
    case 'badconfiguration':
    case 'bad-configuration': return 'bad-configuration';
    case 'baddevice':
    case 'bad-device': return 'bad-device';
    case 'stale': return 'stale';
    case 'disabled': return 'disabled';
    default: return 'unknown';
  }
}

export function runtimeTagQualityBucket(tag: RuntimeTagListItem): RuntimeTagQualityBucket {
  if (!tag.current) return 'no-sample';

  const quality = normalizeRuntimeTagQuality(tag.current.quality);
  if (quality === 'good') return 'good';
  if (quality === 'uncertain' || quality === 'stale') return 'attention';
  return 'bad';
}

function accessMatches(tag: RuntimeTagListItem, access: RuntimeTagAccessFilter) {
  if (access === 'all') return true;
  if (access === 'read-only') return tag.readOnly;
  return !tag.readOnly;
}

export function filterRuntimeTags(
  tags: readonly RuntimeTagListItem[],
  filter: RuntimeTagInspectorFilter
): RuntimeTagListItem[] {
  const query = filter.query.trim().toLocaleLowerCase();

  return [...tags]
    .filter(tag => filter.quality === 'all' || runtimeTagQualityBucket(tag) === filter.quality)
    .filter(tag => accessMatches(tag, filter.access))
    .filter(tag => {
      if (!query) return true;
      const searchable = [
        tag.path,
        tag.name,
        tag.dataType,
        tag.engineeringUnit,
        tag.description,
        tag.current?.source,
        tag.current?.value == null ? '' : String(tag.current.value)
      ].filter(Boolean).join(' ').toLocaleLowerCase();
      return searchable.includes(query);
    })
    .sort((left, right) => left.path.localeCompare(right.path, undefined, { sensitivity: 'base', numeric: true }));
}

export function buildRuntimeTagInspectorSummary(tags: readonly RuntimeTagListItem[]): RuntimeTagInspectorSummary {
  const summary: RuntimeTagInspectorSummary = {
    total: tags.length,
    good: 0,
    attention: 0,
    bad: 0,
    noSample: 0,
    readOnly: 0,
    writable: 0
  };

  for (const tag of tags) {
    const bucket = runtimeTagQualityBucket(tag);
    if (bucket === 'no-sample') summary.noSample += 1;
    else summary[bucket] += 1;

    if (tag.readOnly) summary.readOnly += 1;
    else summary.writable += 1;
  }

  return summary;
}

export function applyRuntimeTagRealtimeEvent(
  tags: readonly RuntimeTagListItem[],
  event: RuntimeTagRealtimeEvent
): RuntimeTagListItem[] {
  if (event.type !== 'tagValueChanged' || !event.tag?.id) return [...tags];

  let changed = false;
  const next = tags.map(tag => {
    if (tag.id !== event.tag.id) return tag;
    changed = true;
    return {
      ...tag,
      name: event.tag.name || tag.name,
      path: event.tag.path || tag.path,
      engineeringUnit: event.tag.engineeringUnit ?? tag.engineeringUnit,
      current: {
        tagId: tag.id,
        value: event.value,
        quality: event.quality,
        timestamp: event.timestamp,
        source: event.source ?? null,
        sourceTimestamp: null,
        serverTimestamp: null
      }
    };
  });

  return changed ? next : [...tags];
}

export function classifyRuntimeTagEndpointIssue(status?: number): RuntimeTagEndpointIssue {
  if (status === 401) return 'unauthenticated';
  if (status === 403) return 'forbidden';
  if (status === 404) return 'not-found';
  return 'unavailable';
}

export function recentHistoryWindow(minutes: number, now = new Date()) {
  const safeMinutes = Number.isFinite(minutes) ? Math.min(24 * 60, Math.max(1, Math.floor(minutes))) : 15;
  return {
    from: new Date(now.getTime() - safeMinutes * 60_000).toISOString(),
    to: now.toISOString()
  };
}
