import { classifyRuntimeTagEndpointIssue, recentHistoryWindow } from './tagInspectorModel';
import type {
  RuntimeTagDetailResponse,
  RuntimeTagEndpointIssue,
  RuntimeTagHistorySample,
  RuntimeTagListItem,
  RuntimeTagRealtimeEvent
} from './tagInspectorTypes';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class RuntimeTagInspectorApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
    public readonly issue: RuntimeTagEndpointIssue = classifyRuntimeTagEndpointIssue(status)
  ) {
    super(message);
    this.name = 'RuntimeTagInspectorApiError';
  }
}

async function requestJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, {
      headers: { accept: 'application/json' },
      signal
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new RuntimeTagInspectorApiError(error instanceof Error ? error.message : String(error));
  }

  if (!response.ok) {
    throw new RuntimeTagInspectorApiError(
      `${response.status} ${response.statusText}`.trim(),
      response.status
    );
  }

  return await response.json() as T;
}

export function loadRuntimeTags(signal?: AbortSignal): Promise<RuntimeTagListItem[]> {
  return requestJson<RuntimeTagListItem[]>('/api/tags', signal);
}

export function loadRuntimeTagDetail(path: string, signal?: AbortSignal): Promise<RuntimeTagDetailResponse> {
  const encodedPath = path.split('/').map(segment => encodeURIComponent(segment)).join('/');
  return requestJson<RuntimeTagDetailResponse>(`/api/tags/by-path/${encodedPath}`, signal);
}

export function loadRecentTagHistory(
  tagId: string,
  minutes = 15,
  limit = 120,
  signal?: AbortSignal
): Promise<RuntimeTagHistorySample[]> {
  const { from, to } = recentHistoryWindow(minutes);
  const safeLimit = Math.min(1000, Math.max(1, Math.floor(limit)));
  const query = new URLSearchParams({ from, to, limit: String(safeLimit) });
  return requestJson<RuntimeTagHistorySample[]>(`/api/history/${encodeURIComponent(tagId)}?${query}`, signal);
}

export type RuntimeTagRealtimeState = 'connecting' | 'live' | 'closed' | 'error';
export type RuntimeTagRealtimeDisposer = () => void;

export function buildRuntimeTagRealtimeUrl(
  apiBase = API,
  locationLike: Pick<Location, 'protocol' | 'host' | 'href'> = window.location
): string {
  if (!apiBase) {
    const protocol = locationLike.protocol === 'https:' ? 'wss:' : 'ws:';
    return `${protocol}//${locationLike.host}/ws/tags`;
  }

  const url = new URL(apiBase, locationLike.href);
  url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
  url.pathname = `${url.pathname.replace(/\/$/, '')}/ws/tags`;
  url.search = '';
  url.hash = '';
  return url.toString();
}

export function connectRuntimeTagRealtime(
  onEvent: (event: RuntimeTagRealtimeEvent) => void,
  onState?: (state: RuntimeTagRealtimeState) => void
): RuntimeTagRealtimeDisposer {
  onState?.('connecting');
  const socket = new WebSocket(buildRuntimeTagRealtimeUrl());
  let disposed = false;

  socket.addEventListener('open', () => onState?.('live'));
  socket.addEventListener('message', event => {
    try {
      const payload = JSON.parse(String(event.data)) as RuntimeTagRealtimeEvent;
      if (payload?.type === 'tagValueChanged') onEvent(payload);
    } catch {
      // Ignore malformed/non-TAG messages. The periodic protected refresh remains authoritative fallback.
    }
  });
  socket.addEventListener('error', () => onState?.('error'));
  socket.addEventListener('close', () => {
    if (!disposed) onState?.('closed');
  });

  return () => {
    disposed = true;
    if (socket.readyState === WebSocket.CONNECTING || socket.readyState === WebSocket.OPEN) socket.close();
  };
}
