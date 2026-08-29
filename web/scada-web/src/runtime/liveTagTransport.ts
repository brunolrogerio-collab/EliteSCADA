export type RuntimeTagCurrentValue = Readonly<{
  tagId: string;
  value: unknown;
  timestamp: string;
  quality: string | number;
  source?: string | null;
  sourceTimestamp?: string | null;
  serverTimestamp?: string | null;
}>;

export type RuntimeTagSnapshot = Readonly<{
  id: string;
  name: string;
  path: string;
  dataType: string;
  engineeringUnit?: string | null;
  description?: string | null;
  readOnly: boolean;
  current?: RuntimeTagCurrentValue | null;
}>;

export type RuntimeTagRealtimeMessage = Readonly<{
  type: 'tagValueChanged';
  tag: Readonly<{
    id: string;
    name: string;
    path: string;
    engineeringUnit?: string | null;
  }>;
  value: unknown;
  quality: string | number;
  timestamp: string;
  source?: string | null;
}>;

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export async function loadReadableRuntimeTags(signal?: AbortSignal): Promise<readonly RuntimeTagSnapshot[]> {
  const response = await fetch(`${API}/api/tags`, {
    credentials: 'same-origin',
    headers: { accept: 'application/json' },
    signal
  });
  if (!response.ok) throw new Error(`Runtime TAG request failed with HTTP ${response.status}.`);
  const payload = await response.json() as RuntimeTagSnapshot[];
  return Object.freeze(payload.map(tag => Object.freeze({
    ...tag,
    current: tag.current ? Object.freeze({ ...tag.current }) : null
  })));
}

export function openRuntimeTagSocket(): WebSocket {
  const url = API
    ? `${API.replace(/^http/i, 'ws')}/ws/tags`
    : `${window.location.protocol === 'https:' ? 'wss:' : 'ws:'}//${window.location.host}/ws/tags`;
  return new WebSocket(url);
}

export function parseRuntimeTagRealtimeMessage(data: unknown): RuntimeTagRealtimeMessage | null {
  let candidate: unknown = data;
  if (typeof data === 'string') {
    try { candidate = JSON.parse(data) as unknown; }
    catch { return null; }
  }
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) return null;
  const record = candidate as Record<string, unknown>;
  if (record.type !== 'tagValueChanged') return null;
  if (!record.tag || typeof record.tag !== 'object' || Array.isArray(record.tag)) return null;
  const tag = record.tag as Record<string, unknown>;
  if (typeof tag.id !== 'string' || typeof tag.name !== 'string' || typeof tag.path !== 'string') return null;
  if (typeof record.timestamp !== 'string') return null;
  const quality = record.quality;
  if (typeof quality !== 'string' && typeof quality !== 'number') return null;

  return Object.freeze({
    type: 'tagValueChanged',
    tag: Object.freeze({
      id: tag.id,
      name: tag.name,
      path: tag.path,
      engineeringUnit: typeof tag.engineeringUnit === 'string' ? tag.engineeringUnit : null
    }),
    value: record.value,
    quality,
    timestamp: record.timestamp,
    source: typeof record.source === 'string' ? record.source : null
  });
}
