import type {
  CommunicationDriverDiagnostic,
  DriverHostHealth,
  EngineeringPackageView,
  EngineeringSnapshot,
  EngineeringWorkspaceDescriptor,
  GatewayRuntimeDiagnostic,
  ImportPreviewView,
  ImportResultView,
  MediaSourceEngineering,
  NetworkReachabilityProbeResponse,
  RuntimeDiagnosticsView,
  VisualAssetEngineering,
  VisualEngineeringAssetReference
} from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class EngineeringSnapshotLoadError extends Error {
  constructor(
    public readonly kind: 'transport' | 'http' | 'response' | 'timeout',
    public readonly path: string,
    public readonly status?: number,
    cause?: unknown
  ) {
    const detail = kind === 'timeout'
      ? `Timed out while loading ${path}.`
      : kind === 'transport'
      ? `Transport unavailable while loading ${path}.`
      : kind === 'http'
        ? `HTTP ${status ?? 'unknown'} response while loading ${path}.`
        : `Invalid HTTP response while loading ${path}.`;
    super(detail, cause === undefined ? undefined : { cause });
    this.name = 'EngineeringSnapshotLoadError';
  }
}

async function getJson<T>(path: string): Promise<T> {
  for (let attempt = 0; ; attempt += 1) {
    try {
      return await getJsonOnce<T>(path);
    } catch (reason) {
      const retryable = reason instanceof EngineeringSnapshotLoadError
        && (reason.kind === 'transport' || reason.kind === 'response');
      if (!retryable || attempt >= 1) throw reason;
      await new Promise(resolve => window.setTimeout(resolve, 250));
    }
  }
}

async function getJsonOnce<T>(path: string): Promise<T> {
  const controller = new AbortController();
  const timeoutId = window.setTimeout(() => controller.abort(), 30_000);
  try {
    const response = await fetch(`${API}${path}`, {
      headers: { accept: 'application/json' },
      signal: controller.signal
    });

    if (!response.ok) {
      throw new EngineeringSnapshotLoadError('http', path, response.status);
    }

    try {
      return await response.json() as T;
    } catch (reason) {
      throw new EngineeringSnapshotLoadError('response', path, response.status, reason);
    }
  } catch (reason) {
    if (reason instanceof EngineeringSnapshotLoadError) throw reason;
    if (controller.signal.aborted) {
      throw new EngineeringSnapshotLoadError('timeout', path, undefined, reason);
    }
    throw new EngineeringSnapshotLoadError('transport', path, undefined, reason);
  } finally {
    window.clearTimeout(timeoutId);
  }
}

async function readError(response: Response): Promise<Error> {
  const body = await response.text();
  return new Error(body || `${response.status} ${response.statusText}`);
}

export async function loadEngineeringWorkspace(): Promise<EngineeringWorkspaceDescriptor> {
  return await getJson<EngineeringWorkspaceDescriptor>('/api/engineering/workspace');
}

export async function loadEngineeringSnapshot(): Promise<EngineeringSnapshot> {
  const [workspace, engineeringPackage] = await Promise.all([
    loadEngineeringWorkspace(),
    getJson<EngineeringPackageView>('/api/engineering/export/json')
  ]);

  return {
    workspace,
    package: {
      ...engineeringPackage,
      tags: engineeringPackage.tags ?? [],
      alarms: engineeringPackage.alarms ?? [],
      dataSources: engineeringPackage.dataSources ?? [],
      templates: engineeringPackage.templates ?? [],
      equipment: engineeringPackage.equipment ?? [],
      dynamos: engineeringPackage.dynamos ?? [],
      screens: engineeringPackage.screens ?? [],
      popups: engineeringPackage.popups ?? [],
      securityRoles: engineeringPackage.securityRoles ?? [],
      gateways: engineeringPackage.gateways ?? [],
      visualAssets: engineeringPackage.visualAssets ?? [],
      mediaSources: engineeringPackage.mediaSources ?? []
    }
  };
}

export async function loadVisualAssets(): Promise<VisualAssetEngineering[]> {
  return await getJson<VisualAssetEngineering[]>('/api/engineering/visual-assets');
}

export type VisualAssetImportResult = {
  asset: VisualAssetEngineering;
  assetRef: VisualEngineeringAssetReference;
  workspaceVersion: number;
};

export async function importVisualAsset(
  file: Blob,
  expectedChangeVersion: number,
  options?: {
    fileName?: string;
    key?: string;
    name?: string;
  }
): Promise<VisualAssetImportResult> {
  const query = new URLSearchParams();
  const inferredFileName = options?.fileName ?? (file instanceof File ? file.name : undefined);
  if (inferredFileName) query.set('fileName', inferredFileName);
  if (options?.key) query.set('key', options.key);
  if (options?.name) query.set('name', options.name);

  const suffix = query.size > 0 ? `?${query.toString()}` : '';
  const response = await fetch(`${API}/api/engineering/visual-assets/import${suffix}`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    },
    body: file
  });

  if (!response.ok) throw await readError(response);
  return await response.json() as VisualAssetImportResult;
}

export async function renameVisualAsset(assetId: string, name: string, expectedChangeVersion: number): Promise<void> {
  const response = await fetch(`${API}/api/engineering/visual-assets/${encodeURIComponent(assetId)}`, {
    method: 'PUT',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    },
    body: JSON.stringify({ name })
  });
  if (!response.ok) throw await readError(response);
}

export async function deleteVisualAsset(assetId: string, expectedChangeVersion: number): Promise<void> {
  const response = await fetch(`${API}/api/engineering/visual-assets/${encodeURIComponent(assetId)}`, {
    method: 'DELETE',
    headers: {
      accept: 'application/json',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    }
  });
  if (!response.ok) throw await readError(response);
}

export type MediaSourceCredentialState = { configured: boolean; code?: string };
export type MediaSourceCredentialInput = { username?: string | null; password?: string | null; bearerToken?: string | null };

export async function createMediaSource(source: MediaSourceEngineering, expectedChangeVersion: number): Promise<MediaSourceEngineering> {
  const response = await fetch(`${API}/api/engineering/media-sources`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    },
    body: JSON.stringify(source)
  });
  if (!response.ok) throw await readError(response);
  return await response.json() as MediaSourceEngineering;
}

export async function updateMediaSource(source: MediaSourceEngineering, expectedChangeVersion: number): Promise<void> {
  if (!source.id) throw new Error('A saved media source ID is required.');
  const response = await fetch(`${API}/api/engineering/media-sources/${encodeURIComponent(source.id)}`, {
    method: 'PUT',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    },
    body: JSON.stringify(source)
  });
  if (!response.ok) throw await readError(response);
}

export async function deleteMediaSource(id: string, expectedChangeVersion: number): Promise<void> {
  const response = await fetch(`${API}/api/engineering/media-sources/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    headers: {
      accept: 'application/json',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    }
  });
  if (!response.ok) throw await readError(response);
}

export async function loadMediaSourceCredentialState(id: string): Promise<MediaSourceCredentialState> {
  return await getJson<MediaSourceCredentialState>(`/api/engineering/media-sources/${encodeURIComponent(id)}/credential-state`);
}

export async function configureMediaSourceCredential(id: string, credential: MediaSourceCredentialInput): Promise<MediaSourceCredentialState> {
  const response = await fetch(`${API}/api/engineering/media-sources/${encodeURIComponent(id)}/credential`, {
    method: 'PUT',
    headers: { accept: 'application/json', 'content-type': 'application/json; charset=utf-8' },
    body: JSON.stringify(credential)
  });
  if (!response.ok) throw await readError(response);
  return await response.json() as MediaSourceCredentialState;
}

export async function deleteMediaSourceCredential(id: string): Promise<void> {
  const response = await fetch(`${API}/api/engineering/media-sources/${encodeURIComponent(id)}/credential`, {
    method: 'DELETE',
    headers: { accept: 'application/json' }
  });
  if (!response.ok) throw await readError(response);
}

export function visualAssetContentUrl(assetId: string): string {
  return `${API}/api/engineering/visual-assets/${encodeURIComponent(assetId)}/content`;
}

export async function loadGatewayDiagnostics(): Promise<GatewayRuntimeDiagnostic[]> {
  return await getJson<GatewayRuntimeDiagnostic[]>('/api/gateway/diagnostics');
}

export async function loadCommunicationDiagnostics(): Promise<CommunicationDriverDiagnostic[]> {
  const diagnostics = await getJson<RuntimeDiagnosticsView>('/api/diagnostics/runtime');
  return diagnostics.runtime?.communicationDrivers ?? [];
}

export async function loadDriverHostHealth(): Promise<DriverHostHealth> {
  return await getJson<DriverHostHealth>('/api/engineering/diagnostics/driver-host');
}

export async function probeNetworkReachability(host: string, port: number): Promise<NetworkReachabilityProbeResponse> {
  const response = await fetch(`${API}/api/engineering/diagnostics/network-probe`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify({ host, port, timeoutMilliseconds: 3000 })
  });
  if (!response.ok) throw await readError(response);
  return await response.json() as NetworkReachabilityProbeResponse;
}

export async function previewEngineeringPackage(
  engineeringPackage: EngineeringPackageView
): Promise<ImportPreviewView> {
  const response = await fetch(`${API}/api/engineering/import/json/preview`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify(engineeringPackage)
  });

  if (!response.ok) throw await readError(response);
  return await response.json() as ImportPreviewView;
}

export async function applyEngineeringPackage(
  engineeringPackage: EngineeringPackageView,
  expectedChangeVersion: number
): Promise<ImportResultView> {
  const current = await loadEngineeringWorkspace();
  if (current.changeVersion !== expectedChangeVersion) {
    throw new EngineeringWorkspaceConflictError(expectedChangeVersion, current.changeVersion);
  }

  const response = await fetch(`${API}/api/engineering/import/json/apply`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    },
    body: JSON.stringify(engineeringPackage)
  });

  if (!response.ok) throw await readError(response);
  return await response.json() as ImportResultView;
}

export type EngineeringDeleteKind = 'tags' | 'alarms' | 'data-sources';

export type EngineeringDependencyView = {
  entityKind: string;
  entityId: string;
  entityKey: string;
  relation: string;
};

export type EngineeringDeleteResult = {
  deleted: boolean;
  entityKind: string;
  entityId: string;
  entityKey: string;
  changeVersion: number;
};

export async function deleteEngineeringEntity(
  kind: EngineeringDeleteKind,
  id: string,
  expectedChangeVersion: number
): Promise<EngineeringDeleteResult> {
  const response = await fetch(`${API}/api/engineering/${kind}/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    headers: {
      accept: 'application/json',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    }
  });

  if (!response.ok) throw await readError(response);
  return await response.json() as EngineeringDeleteResult;
}

export type EngineeringBulkEntityKind = 'tag' | 'alarm' | 'data-source';

export type EngineeringBulkRequest = {
  entityKind: EngineeringBulkEntityKind;
  entityIds: string[];
  tags?: {
    readOnly?: boolean;
    historianEnabled?: boolean;
    historianStrategy?: string;
  };
  alarms?: {
    enabled?: boolean;
    priority?: string;
    requiresAcknowledgement?: boolean;
    shelvingAllowed?: boolean;
  };
  dataSources?: {
    enabled?: boolean;
  };
};

export type EngineeringBulkPreviewResult = {
  changeVersion: number;
  entityKind: EngineeringBulkEntityKind;
  affectedCount: number;
  preview: ImportPreviewView;
};

export type EngineeringBulkApplyResult = {
  changeVersion: number;
  entityKind: EngineeringBulkEntityKind;
  affectedCount: number;
  result: ImportResultView;
};

export async function previewEngineeringBulk(
  request: EngineeringBulkRequest
): Promise<EngineeringBulkPreviewResult> {
  const response = await fetch(`${API}/api/engineering/bulk/preview`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify(request)
  });

  if (!response.ok) throw await readError(response);
  return await response.json() as EngineeringBulkPreviewResult;
}

export async function applyEngineeringBulk(
  request: EngineeringBulkRequest,
  expectedChangeVersion: number
): Promise<EngineeringBulkApplyResult> {
  const response = await fetch(`${API}/api/engineering/bulk/apply`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8',
      'x-elitescada-workspace-version': String(expectedChangeVersion)
    },
    body: JSON.stringify(request)
  });

  if (!response.ok) throw await readError(response);
  return await response.json() as EngineeringBulkApplyResult;
}

export class EngineeringWorkspaceConflictError extends Error {
  constructor(
    public readonly expectedChangeVersion: number,
    public readonly currentChangeVersion: number
  ) {
    super(`Engineering Workspace changed from version ${expectedChangeVersion} to ${currentChangeVersion}. Reload and validate the draft again.`);
    this.name = 'EngineeringWorkspaceConflictError';
  }
}
