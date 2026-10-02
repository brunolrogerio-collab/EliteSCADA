import type { RuntimeVisualSourceRequest, VisualLiveScalarSample } from '../../engineering/visual-editor/visualEditorLiveValues';
import { visualTagSampleKey } from '../../engineering/visual-editor/visualDynamicRuntime';
import { loadReadableRuntimeTags, type RuntimeTagSnapshot } from '../liveTagTransport';

const TRANSIENT_DATA_QUERY_ROUTE = '/api/historical/data-query';
const PLAYBACK_QUERY_VERSION = 1;
const QUERY_BATCH_SIZE = 100;
const ANALOG_QUERY_ID = 'e31b4cef-2b1b-4f91-b889-79782c202711';
const DISCRETE_QUERY_ID = '5c7c41d0-cf56-4e3f-a463-4f87db8a421c';

export type HistoricalPlaybackResolvedRange = Readonly<{
  fromUtc: string;
  toUtc: string;
}>;

export type HistoricalPlaybackSampleLoad = Readonly<{
  samples: ReadonlyMap<string, VisualLiveScalarSample>;
  gaps: number;
  resolvedTags: number;
}>;

type ResolvedTagRequest = Readonly<{
  id: string;
  path: string;
  dataType: string;
  engineeringUnit?: string | null;
}>;

type DataQueryValue = Readonly<{ kind: string; value: string | null }>;
type DataQueryRow = Readonly<{
  data?: Readonly<{ cells?: Readonly<Record<string, DataQueryValue>> }>;
  provenance?: Readonly<{ kind?: string; reason?: string | null }> | null;
}>;
type DataQueryResponse = Readonly<{ rows?: readonly DataQueryRow[] }>;

export async function loadHistoricalPlaybackSamples(
  requests: readonly RuntimeVisualSourceRequest[],
  range: HistoricalPlaybackResolvedRange,
  atUtc: string,
  signal?: AbortSignal
): Promise<HistoricalPlaybackSampleLoad> {
  const catalog = await loadReadableRuntimeTags(signal);
  const resolved = resolveTagRequests(requests, catalog);
  const sampleMap = new Map<string, VisualLiveScalarSample>();

  for (const request of requests.filter(item => item.kind === 'clientmemory')) {
    const unavailable = Object.freeze({
      reference: request.target,
      tagId: request.tagReference?.tagId ?? null,
      value: null,
      dataType: request.dataType ?? 'String',
      readOnly: true,
      state: 'NotHistorical',
      timestamp: atUtc
    });
    if (request.target) sampleMap.set(request.target, unavailable);
    if (request.tagReference?.tagId) sampleMap.set(visualTagSampleKey(request.tagReference.tagId), unavailable);
  }

  const analog = resolved.filter(item => isAnalogDataType(item.dataType));
  const discrete = resolved.filter(item => !isAnalogDataType(item.dataType));

  await queryBatches(analog, 'interpolated', range, atUtc, sampleMap, signal);
  await queryBatches(discrete, 'atOrBefore', range, atUtc, sampleMap, signal);

  const seen = new Set(
    [...sampleMap.values()]
      .map(sample => sample.tagId?.trim().toLocaleLowerCase())
      .filter((value): value is string => Boolean(value))
  );
  let gaps = [...sampleMap.values()].filter(sample => sample.state === 'Gap' || sample.state === 'Unavailable').length;

  for (const request of requests.filter(item => item.kind === 'tag')) {
    const resolvedTag = findCatalogTag(request, catalog);
    const id = resolvedTag?.id ?? request.tagReference?.tagId ?? null;
    const path = resolvedTag?.path ?? request.target;
    if (id && seen.has(id.trim().toLocaleLowerCase())) continue;
    const gap = Object.freeze({
      reference: path,
      tagId: id,
      value: null,
      dataType: resolvedTag?.dataType ?? request.dataType ?? 'String',
      quality: null,
      readOnly: true,
      state: 'Gap',
      timestamp: atUtc
    });
    if (path) sampleMap.set(path, gap);
    if (id) sampleMap.set(visualTagSampleKey(id), gap);
    gaps += 1;
  }

  return Object.freeze({
    samples: sampleMap,
    gaps,
    resolvedTags: resolved.length
  });
}

async function queryBatches(
  tags: readonly ResolvedTagRequest[],
  retrievalMode: 'interpolated' | 'atOrBefore',
  range: HistoricalPlaybackResolvedRange,
  atUtc: string,
  samples: Map<string, VisualLiveScalarSample>,
  signal?: AbortSignal
): Promise<void> {
  for (let offset = 0; offset < tags.length; offset += QUERY_BATCH_SIZE) {
    const batch = tags.slice(offset, offset + QUERY_BATCH_SIZE);
    const response = await executeTransientDataQuery(
      createPlaybackDefinition(batch, retrievalMode, range, atUtc),
      signal
    );
    projectRows(response, batch, samples, atUtc);
  }
}

function createPlaybackDefinition(
  tags: readonly ResolvedTagRequest[],
  retrievalMode: 'interpolated' | 'atOrBefore',
  range: HistoricalPlaybackResolvedRange,
  atUtc: string
): Readonly<Record<string, unknown>> {
  const durationMs = Math.max(
    1,
    Math.min(
      2_147_483_647,
      Math.floor(new Date(range.toUtc).getTime() - new Date(range.fromUtc).getTime())
    )
  );
  return Object.freeze({
    id: retrievalMode === 'interpolated' ? ANALOG_QUERY_ID : DISCRETE_QUERY_ID,
    key: retrievalMode === 'interpolated'
      ? 'runtime-historical-playback-analog'
      : 'runtime-historical-playback-discrete',
    name: 'Runtime Historical Playback',
    providerKey: 'historical',
    version: PLAYBACK_QUERY_VERSION,
    query: Object.freeze({
      version: PLAYBACK_QUERY_VERSION,
      datasetKey: 'historian.samples',
      timeRange: Object.freeze({
        kind: 'absolute',
        fromUtc: range.fromUtc,
        toUtc: range.toUtc
      }),
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

async function executeTransientDataQuery(
  definition: Readonly<Record<string, unknown>>,
  signal?: AbortSignal
): Promise<DataQueryResponse> {
  const response = await fetch(TRANSIENT_DATA_QUERY_ROUTE, {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json'
    },
    body: JSON.stringify({ definition, execution: {} }),
    signal
  });

  if (!response.ok) {
    let detail = `Historical Playback query failed with HTTP ${response.status}.`;
    try {
      const payload = await response.json() as { error?: unknown };
      if (typeof payload.error === 'string' && payload.error.trim()) detail = payload.error;
    } catch {
      // Keep status-only diagnostic.
    }
    throw new Error(detail);
  }
  return await response.json() as DataQueryResponse;
}

function projectRows(
  response: DataQueryResponse,
  requested: readonly ResolvedTagRequest[],
  samples: Map<string, VisualLiveScalarSample>,
  atUtc: string
): void {
  const byId = new Map(requested.map(tag => [tag.id.toLocaleLowerCase(), tag]));
  const returned = new Set<string>();

  for (const row of response.rows ?? []) {
    const cells = row.data?.cells;
    const id = cells?.['tag.id']?.value?.trim();
    if (!id) continue;
    const tag = byId.get(id.toLocaleLowerCase());
    if (!tag) continue;
    returned.add(tag.id.toLocaleLowerCase());

    const provenanceKind = row.provenance?.kind?.toLocaleLowerCase() ?? '';
    const valueCell = cells?.value;
    const quality = cells?.quality?.value ?? null;
    const timestamp = cells?.timestamp?.value ?? atUtc;
    const isGap = provenanceKind === 'gap' || !valueCell || valueCell.kind === 'null' || valueCell.value === null;
    const sample = Object.freeze({
      reference: tag.path,
      tagId: tag.id,
      value: isGap ? null : decodeHistoricalValue(valueCell),
      dataType: tag.dataType,
      quality: isGap ? null : quality,
      readOnly: true,
      state: isGap ? 'Gap' : undefined,
      timestamp
    });
    samples.set(tag.path, sample);
    samples.set(visualTagSampleKey(tag.id), sample);
  }

  for (const tag of requested) {
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
    samples.set(tag.path, gap);
    samples.set(visualTagSampleKey(tag.id), gap);
  }
}

function resolveTagRequests(
  requests: readonly RuntimeVisualSourceRequest[],
  catalog: readonly RuntimeTagSnapshot[]
): readonly ResolvedTagRequest[] {
  const byId = new Map(catalog.map(tag => [tag.id.trim().toLocaleLowerCase(), tag]));
  const byPath = new Map(catalog.map(tag => [tag.path, tag]));
  const resolved = new Map<string, ResolvedTagRequest>();

  for (const request of requests) {
    if (request.kind !== 'tag') continue;
    const idKey = request.tagReference?.tagId?.trim().toLocaleLowerCase();
    const tag = (idKey ? byId.get(idKey) : undefined) ?? byPath.get(request.target);
    if (!tag) continue;
    resolved.set(tag.id.toLocaleLowerCase(), Object.freeze({
      id: tag.id,
      path: tag.path,
      dataType: tag.dataType,
      engineeringUnit: tag.engineeringUnit
    }));
  }
  return Object.freeze([...resolved.values()]);
}

function findCatalogTag(
  request: RuntimeVisualSourceRequest,
  catalog: readonly RuntimeTagSnapshot[]
): RuntimeTagSnapshot | undefined {
  const id = request.tagReference?.tagId?.trim().toLocaleLowerCase();
  return catalog.find(tag =>
    (id && tag.id.trim().toLocaleLowerCase() === id) || tag.path === request.target
  );
}

function isAnalogDataType(dataType: string): boolean {
  const normalized = dataType.trim().toLocaleLowerCase();
  return normalized === 'float' || normalized === 'double';
}

function decodeHistoricalValue(value: DataQueryValue): unknown {
  if (value.value === null) return null;
  switch (value.kind) {
    case 'boolean':
      return value.value === 'true';
    case 'int16':
    case 'int32':
    case 'int64':
    case 'float':
    case 'double':
    case 'number': {
      const numeric = Number(value.value);
      return Number.isFinite(numeric) ? numeric : null;
    }
    case 'null':
      return null;
    default:
      return value.value;
  }
}
