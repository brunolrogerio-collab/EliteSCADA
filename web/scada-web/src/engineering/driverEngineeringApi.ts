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

export async function testEngineeringDataSourceConnection(
  dataSourceId: string
): Promise<DriverConnectionTestResultView> {
  return await postJson<DriverConnectionTestResultView>(
    `/api/engineering/data-sources/${encodeURIComponent(dataSourceId)}/driver-tools/connection-test`,
    {});
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
    throw new Error(readProblemDetail(text) || `${response.status} ${response.statusText}`);
  }

  return await response.json() as T;
}

function readProblemDetail(body: string): string {
  if (!body.trim()) return '';
  try {
    const parsed = JSON.parse(body) as { detail?: unknown; error?: unknown; title?: unknown };
    if (typeof parsed.detail === 'string' && parsed.detail.trim()) return parsed.detail;
    if (typeof parsed.error === 'string' && parsed.error.trim()) return parsed.error;
    if (typeof parsed.title === 'string' && parsed.title.trim()) return parsed.title;
  } catch {
    // Keep server text when it is not JSON.
  }
  return body;
}
