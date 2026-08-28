import { classifyRuntimeTagEndpointIssue } from './tagInspectorModel';
import { clampTrendSampleLimit, validateTrendRange } from './trendModel';
import type { RuntimeTagEndpointIssue, RuntimeTagHistorySample, RuntimeTagListItem } from './trendTypes';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class BasicTrendApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
    public readonly issue: RuntimeTagEndpointIssue = classifyRuntimeTagEndpointIssue(status)
  ) {
    super(message);
    this.name = 'BasicTrendApiError';
  }
}

async function requestJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, { headers: { accept: 'application/json' }, signal });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new BasicTrendApiError(error instanceof Error ? error.message : String(error));
  }

  if (!response.ok) {
    throw new BasicTrendApiError(`${response.status} ${response.statusText}`.trim(), response.status);
  }

  return await response.json() as T;
}

export function buildTrendHistoryPath(
  tagId: string,
  from: string,
  to: string,
  limit = 500
): string {
  validateTrendRange(from, to);
  const safeLimit = clampTrendSampleLimit(limit);
  const query = new URLSearchParams({ from, to, limit: String(safeLimit) });
  return `/api/history/${encodeURIComponent(tagId)}?${query}`;
}

export function loadTrendTags(signal?: AbortSignal): Promise<RuntimeTagListItem[]> {
  return requestJson<RuntimeTagListItem[]>('/api/tags', signal);
}

export function loadTrendHistory(
  tagId: string,
  from: string,
  to: string,
  limit = 500,
  signal?: AbortSignal
): Promise<RuntimeTagHistorySample[]> {
  return requestJson<RuntimeTagHistorySample[]>(buildTrendHistoryPath(tagId, from, to, limit), signal);
}
