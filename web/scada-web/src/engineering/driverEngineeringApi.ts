import type {
  CommunicationTagBindingEngineering,
  TagPhysicalValueTransformEngineering
} from './TagSourceSelector.logic';
import type { TagValueSelectorEngineering } from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type DriverEngineeringIssueView = Readonly<{
  code: string;
  severity: string | number;
  message: string;
  fieldKey?: string | null;
  messageResourceKey?: string | null;
}>;

export type DriverConnectionTestResultView = Readonly<{
  succeeded: boolean;
  sanitizedEndpoint?: string | null;
  observedIdentity?: string | null;
  observedProperties?: Record<string, string> | null;
  issues?: readonly DriverEngineeringIssueView[] | null;
}>;

export type DriverPointReadTestStatusView =
  | 'Good'
  | 'Bad'
  | 'NoData'
  | 'IntermittentOrUncertain'
  | string
  | number;

export type DriverPointReadRawRepresentationView = Readonly<{
  kind: string;
  hex?: string | null;
  elements?: readonly string[] | null;
  metadata?: Record<string, string> | null;
}>;

export type DriverPointReadValueView = Readonly<{
  valueType: string;
  value: unknown;
  engineeringUnit?: string | null;
}>;

export type DriverPointReadSampleView = Readonly<{
  status: DriverPointReadTestStatusView;
  observedAtUtc: string;
  sourceTimestampUtc?: string | null;
  latencyMilliseconds?: number | null;
  quality: string | number;
  raw?: DriverPointReadRawRepresentationView | null;
  decoded?: DriverPointReadValueView | null;
  engineering?: DriverPointReadValueView | null;
  effectiveValueTransform?: TagPhysicalValueTransformEngineering | null;
  issues?: readonly DriverEngineeringIssueView[] | null;
}>;

export type DriverPointReadSampleSummaryView = Readonly<{
  requestedSamples: number;
  completedSamples: number;
  goodSamples: number;
  uncertainSamples: number;
  badSamples: number;
  noDataSamples: number;
  minimumLatencyMilliseconds?: number | null;
  averageLatencyMilliseconds?: number | null;
  maximumLatencyMilliseconds?: number | null;
}>;

export type DriverPointReadTestResultView = Readonly<{
  status: DriverPointReadTestStatusView;
  sanitizedEndpoint?: string | null;
  portableAddress: string;
  summary: DriverPointReadSampleSummaryView;
  samples: readonly DriverPointReadSampleView[];
  issues?: readonly DriverEngineeringIssueView[] | null;
}>;

export type DriverPointReadTestRequestView = Readonly<{
  binding: CommunicationTagBindingEngineering;
  dataType: string;
  addressSelector?: TagValueSelectorEngineering | null;
  engineeringUnit?: string | null;
  sampleCount?: number;
  sampleIntervalMilliseconds?: number;
  timeoutMilliseconds?: number;
}>;

export class DriverEngineeringHttpError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
    readonly fieldKey?: string
  ) {
    super(message);
    this.name = 'DriverEngineeringHttpError';
  }
}

export type DriverDiscoveryCandidateView = Readonly<{
  candidateId: string;
  stableIdentity: string;
  displayName: string;
  sanitizedEndpoint?: string | null;
  suggestedSettings?: Record<string, string> | null;
  metadata?: Record<string, string> | null;
  issues?: readonly DriverEngineeringIssueView[] | null;
}>;

export type DriverBrowseNodeView = Readonly<{
  nodeId: string;
  stableIdentity: string;
  displayName: string;
  isContainer: boolean;
  isReadable: boolean;
  isWritable: boolean;
  portableAddress?: string | null;
  suggestedDataType?: string | number | null;
  engineeringUnit?: string | null;
  metadata?: Record<string, string> | null;
  issues?: readonly DriverEngineeringIssueView[] | null;
}>;

export type DriverBrowsePageView = Readonly<{
  nodes: readonly DriverBrowseNodeView[];
  continuationToken?: string | null;
  isPartial: boolean;
  issues?: readonly DriverEngineeringIssueView[] | null;
}>;

export type DriverDraftDataSourceView = Readonly<{
  sourceKey: string;
  sourceName: string;
  driverType: string;
  settings?: Record<string, string> | null;
  secretReferences?: Record<string, string> | null;
}>;

export async function testEngineeringDataSourceConnection(
  dataSourceId: string
): Promise<DriverConnectionTestResultView> {
  return await postJson<DriverConnectionTestResultView>(
    `/api/engineering/data-sources/${encodeURIComponent(dataSourceId)}/driver-tools/connection-test`,
    {});
}

export async function testEngineeringDataSourceDraftConnection(
  dataSource: DriverDraftDataSourceView
): Promise<DriverConnectionTestResultView> {
  return await postJson<DriverConnectionTestResultView>(
    '/api/engineering/driver-tools/connection-test',
    dataSource);
}

export async function testEngineeringPointRead(
  dataSourceId: string,
  request: DriverPointReadTestRequestView
): Promise<DriverPointReadTestResultView> {
  return await postJson<DriverPointReadTestResultView>(
    `/api/engineering/data-sources/${encodeURIComponent(dataSourceId)}/driver-tools/point-read-test`,
    request);
}

export async function testEngineeringDraftPointRead(
  dataSource: DriverDraftDataSourceView,
  request: DriverPointReadTestRequestView
): Promise<DriverPointReadTestResultView> {
  return await postJson<DriverPointReadTestResultView>(
    '/api/engineering/driver-tools/point-read-test',
    {
      dataSource,
      ...request
    });
}

export async function discoverEngineeringDataSource(
  dataSourceId: string,
  request: Readonly<{
    parameters?: Record<string, string> | null;
    maximumResults?: number | null;
  }> = {}
): Promise<DriverDiscoveryCandidateView[]> {
  return await postJson<DriverDiscoveryCandidateView[]>(
    `/api/engineering/data-sources/${encodeURIComponent(dataSourceId)}/driver-tools/discover`,
    request);
}

export async function discoverEngineeringDataSourceDraft(
  dataSource: DriverDraftDataSourceView,
  request: Readonly<{
    parameters?: Record<string, string> | null;
    maximumResults?: number | null;
  }> = {}
): Promise<DriverDiscoveryCandidateView[]> {
  return await postJson<DriverDiscoveryCandidateView[]>(
    '/api/engineering/driver-tools/discover',
    {
      dataSource,
      parameters: request.parameters ?? null,
      maximumResults: request.maximumResults ?? null
    });
}

export async function browseEngineeringDataSource(
  dataSourceId: string,
  request: Readonly<{
    parentNodeId?: string | null;
    continuationToken?: string | null;
    pageSize?: number | null;
    parameters?: Record<string, string> | null;
  }> = {}
): Promise<DriverBrowsePageView> {
  return await postJson<DriverBrowsePageView>(
    `/api/engineering/data-sources/${encodeURIComponent(dataSourceId)}/driver-tools/browse`,
    request);
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify(body)
  });

  if (!response.ok) {
    const text = await response.text();
    const problem = readProblem(text);
    throw new DriverEngineeringHttpError(
      problem.detail || `${response.status} ${response.statusText}`,
      response.status,
      problem.code,
      problem.fieldKey
    );
  }

  return await response.json() as T;
}

function readProblem(body: string): { detail?: string; code?: string; fieldKey?: string } {
  if (!body.trim()) return {};
  try {
    const parsed = JSON.parse(body) as { detail?: unknown; error?: unknown; title?: unknown; code?: unknown; fieldKey?: unknown };
    const detail = [parsed.detail, parsed.error, parsed.title].find((value): value is string => typeof value === 'string' && value.trim().length > 0);
    return {
      ...(detail ? { detail } : {}),
      ...(typeof parsed.code === 'string' && parsed.code.trim() ? { code: parsed.code.trim() } : {}),
      ...(typeof parsed.fieldKey === 'string' && parsed.fieldKey.trim() ? { fieldKey: parsed.fieldKey.trim() } : {})
    };
  } catch {
    // Preserve a plain server diagnostic when the response is not JSON.
  }
  return { detail: body };
}
