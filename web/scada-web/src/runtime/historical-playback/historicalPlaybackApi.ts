import type { VisualLiveScalarSample } from '../../engineering/visual-editor/visualEditorLiveValues';
import { visualTagSampleKey } from '../../engineering/visual-editor/visualDynamicRuntime';

const RESOLVE_ROUTE = '/api/runtime/historical-playback/resolve';
const TRANSIENT_DATA_QUERY_ROUTE = '/api/historical/data-query';
const PLAYBACK_QUERY_VERSION = 1;
const QUERY_BATCH_SIZE = 100;
const ANALOG_QUERY_ID = 'e31b4cef-2b1b-4f91-b889-79782c202711';
const DISCRETE_QUERY_ID = '5c7c41d0-cf56-4e3f-a463-4f87db8a421c';

export type HistoricalPlaybackVisualScope = Readonly<{
  screenKey: string;
  popupKeys: readonly string[];
}>;

export type HistoricalPlaybackResolvedRange = Readonly<{ fromUtc: string; toUtc: string }>;
export type HistoricalPlaybackSampleLoad = Readonly<{
  samples: ReadonlyMap<string, VisualLiveScalarSample>;
  gaps: number;
  resolvedTags: number;
}>;

type ResolvedTag = Readonly<{
  id: string;
  path: string;
  dataType: string;
  retrievalMode: 'interpolated' | 'atOrBefore';
}>;
type ScopeResponse = Readonly<{ tags?: readonly ResolvedTag[] }>;
type DataQueryValue = Readonly<{ kind: string; value: string | null }>;
type DataQueryRow = Readonly<{
  data?: Readonly<{ cells?: Readonly<Record<string, DataQueryValue>> }>;
  provenance?: Readonly<{ kind?: string; reason?: string | null }> | null;
}>;
type DataQueryResponse = Readonly<{ rows?: readonly DataQueryRow[] }>;

export async function loadHistoricalPlaybackSamples(
  scope: HistoricalPlaybackVisualScope,
  range: HistoricalPlaybackResolvedRange,
  atUtc: string,
  signal?: AbortSignal
): Promise<HistoricalPlaybackSampleLoad> {
  const resolved = await resolveVisualScope(scope, signal);
  const sampleMap = new Map<string, VisualLiveScalarSample>();
  await queryBatches(resolved.filter(x => x.retrievalMode === 'interpolated'), 'interpolated', range, atUtc, sampleMap, signal);
  await queryBatches(resolved.filter(x => x.retrievalMode === 'atOrBefore'), 'atOrBefore', range, atUtc, sampleMap, signal);

  const returned = new Set([...sampleMap.values()]
    .map(sample => sample.tagId?.trim().toLocaleLowerCase())
    .filter((value): value is string => Boolean(value)));
  let gaps = 0;
  for (const tag of resolved) {
    if (returned.has(tag.id.toLocaleLowerCase())) continue;
    const gap = Object.freeze({
      reference: tag.path,
      tagId: tag.id,
      value: null,
      dataType: tag.dataType,
      quality: null,
      readOnly: true,
      state: 'Gap',
      timestamp: atUtc
    });
    sampleMap.set(tag.path, gap);
    sampleMap.set(visualTagSampleKey(tag.id), gap);
    gaps += 1;
  }

  return Object.freeze({ samples: sampleMap, gaps, resolvedTags: resolved.length });
}

async function resolveVisualScope(scope: HistoricalPlaybackVisualScope, signal?: AbortSignal): Promise<readonly ResolvedTag[]> {
  const response = await fetch(RESOLVE_ROUTE, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { accept: 'application/json', 'content-type': 'application/json' },
    body: JSON.stringify({ screenKey: scope.screenKey, popupKeys: scope.popupKeys }),
    signal
  });
  if (!response.ok) throw new Error(await response.text() || `Historical Playback scope resolution failed with HTTP ${response.status}.`);
  const payload = await response.json() as ScopeResponse;
  return Object.freeze([...(payload.tags ?? [])]);
}

async function queryBatches(
  tags: readonly ResolvedTag[],
  retrievalMode: 'interpolated' | 'atOrBefore',
  range: HistoricalPlaybackResolvedRange,
  atUtc: string,
  samples: Map<string, VisualLiveScalarSample>,
  signal?: AbortSignal
): Promise<void> {
  for (let offset = 0; offset < tags.length; offset += QUERY_BATCH_SIZE) {
    const batch = tags.slice(offset, offset + QUERY_BATCH_SIZE);
    const response = await executeTransientDataQuery(createPlaybackDefinition(batch, retrievalMode, range, atUtc), signal);
    projectRows(response, batch, samples, atUtc);
  }
}

function createPlaybackDefinition(
  tags: readonly ResolvedTag[],
  retrievalMode: 'interpolated' | 'atOrBefore',
  range: HistoricalPlaybackResolvedRange,
  atUtc: string
): Readonly<Record<string, unknown>> {
  const durationMs = Math.max(1, Math.min(2_147_483_647,
    Math.floor(new Date(range.toUtc).getTime() - new Date(range.fromUtc).getTime())));
  return Object.freeze({
    id: retrievalMode === 'interpolated' ? ANALOG_QUERY_ID : DISCRETE_QUERY_ID,
    key: retrievalMode === 'interpolated' ? 'runtime-historical-playback-analog' : 'runtime-historical-playback-discrete',
    name: 'Runtime Historical Playback',
    providerKey: 'historical',
    version: PLAYBACK_QUERY_VERSION,
    query: Object.freeze({
      version: PLAYBACK_QUERY_VERSION,
      datasetKey: 'historian.samples',
      timeRange: Object.freeze({ kind: 'absolute', fromUtc: range.fromUtc, toUtc: range.toUtc }),
      filters: Object.freeze([Object.freeze({
        field: 'tag.id',
        operator: 'in',
        values: Object.freeze(tags.map(tag => Object.freeze({ kind: 'guid', value: tag.id })))
      })]),
      orderBy: Object.freeze([Object.freeze({ field: 'timestamp', direction: 'descending' })]),
      page: Object.freeze({ limit: Math.max(1, tags.length) })
    }),
    selectedFields: Object.freeze(['tag.id', 'tag.path', 'quality', 'value', 'timestamp']),
    historianRetrieval: Object.freeze({
      mode: retrievalMode,
      targetUtc: atUtc,
      ...(retrievalMode === 'interpolated' ? { maximumGapMilliseconds: durationMs } : {})
    })
  });
}

async function executeTransientDataQuery(definition: Readonly<Record<string, unknown>>, signal?: AbortSignal): Promise<DataQueryResponse> {
  const response = await fetch(TRANSIENT_DATA_QUERY_ROUTE, {
    method: 'POST', credentials: 'same-origin',
    headers: { accept: 'application/json', 'content-type': 'application/json' },
    body: JSON.stringify({ definition, execution: {} }), signal
  });
  if (!response.ok) throw new Error(await response.text() || `Historical Playback query failed with HTTP ${response.status}.`);
  return await response.json() as DataQueryResponse;
}

function projectRows(
  response: DataQueryResponse,
  requested: readonly ResolvedTag[],
  samples: Map<string, VisualLiveScalarSample>,
  atUtc: string
): void {
  const byId = new Map(requested.map(tag => [tag.id.toLocaleLowerCase(), tag]));
  for (const row of response.rows ?? []) {
    const cells = row.data?.cells;
    const id = cells?.['tag.id']?.value?.trim();
    if (!id) continue;
    const tag = byId.get(id.toLocaleLowerCase());
    if (!tag) continue;
    const valueCell = cells?.value;
    const provenance = row.provenance?.kind?.toLocaleLowerCase() ?? '';
    const isGap = provenance === 'gap' || !valueCell || valueCell.kind === 'null' || valueCell.value === null;
    const sample = Object.freeze({
      reference: tag.path,
      tagId: tag.id,
      value: isGap ? null : decodeHistoricalValue(valueCell),
      dataType: tag.dataType,
      quality: isGap ? null : (cells?.quality?.value ?? null),
      readOnly: true,
      state: isGap ? 'Gap' : undefined,
      timestamp: cells?.timestamp?.value ?? atUtc
    });
    samples.set(tag.path, sample);
    samples.set(visualTagSampleKey(tag.id), sample);
  }
}

function decodeHistoricalValue(value: DataQueryValue): unknown {
  if (value.value === null) return null;
  switch (value.kind) {
    case 'boolean': return value.value === 'true';
    case 'int16': case 'int32': case 'int64': case 'float': case 'double': case 'number': {
      const numeric = Number(value.value); return Number.isFinite(numeric) ? numeric : null;
    }
    case 'null': return null;
    default: return value.value;
  }
}
