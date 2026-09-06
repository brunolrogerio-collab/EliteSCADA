const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const LIBRARY_MEDIA_TYPE = 'application/vnd.elitescada.resource-library';

export type ReusableLibraryDescriptor = {
  libraryId: string;
  name: string;
  version: string;
  contentSha256: string;
  resourceCount: number;
  byteLength: number;
};

export type ReusableLibraryDependency = {
  kind: string;
  resourceId: string;
};

export type ReusableLibraryResource = {
  resourceId: string;
  kind: string;
  sourceKey: string;
  displayName: string;
  payloadPath: string;
  dependencies: ReusableLibraryDependency[];
};

export type ReusableLibraryResourceCatalog = {
  library: ReusableLibraryDescriptor;
  resources: ReusableLibraryResource[];
};

export type ReusableLibraryAssociationResult = {
  association: ReusableLibraryDescriptor;
  added: boolean;
  workingChanged: boolean;
};

export type ReusableLibraryIncorporationResult = {
  libraryId: string;
  resourceId: string;
  kind: string;
  incorporated: boolean;
  deduplicated: boolean | number;
  closureCount: number;
  changeVersion: number;
  created?: number;
};

export type ReusableLibraryExportSelection = {
  kind: string;
  resourceId: string;
};

export type ReusableLibraryDownload = {
  blob: Blob;
  filename: string;
};

export class ReusableLibraryApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly responseBody: string,
    public readonly responseData?: unknown
  ) {
    super(extractErrorMessage(responseData) ?? responseBody || `HTTP ${status}`);
    this.name = 'ReusableLibraryApiError';
  }
}

export async function loadReusableLibraries(): Promise<ReusableLibraryDescriptor[]> {
  return await requestJson<ReusableLibraryDescriptor[]>('/api/engineering/libraries');
}

export async function loadReusableLibraryResources(libraryId: string): Promise<ReusableLibraryResourceCatalog> {
  return await requestJson<ReusableLibraryResourceCatalog>(
    `/api/engineering/libraries/${encodeURIComponent(libraryId)}/resources`
  );
}

export async function associateReusableLibrary(file: Blob): Promise<ReusableLibraryAssociationResult> {
  return await requestJson<ReusableLibraryAssociationResult>('/api/engineering/libraries/associate', {
    method: 'POST',
    headers: { 'content-type': LIBRARY_MEDIA_TYPE },
    body: file
  });
}

export async function disassociateReusableLibrary(libraryId: string): Promise<void> {
  await requestJson(`/api/engineering/libraries/${encodeURIComponent(libraryId)}`, { method: 'DELETE' });
}

export async function incorporateReusableLibraryResource(
  libraryId: string,
  resource: Pick<ReusableLibraryResource, 'kind' | 'resourceId'>,
  expectedChangeVersion: number
): Promise<ReusableLibraryIncorporationResult> {
  return await requestJson<ReusableLibraryIncorporationResult>(
    `/api/engineering/libraries/${encodeURIComponent(libraryId)}/resources/${encodeURIComponent(resource.resourceId)}/incorporate`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json; charset=utf-8',
        'x-elitescada-workspace-version': String(expectedChangeVersion)
      },
      body: JSON.stringify({ kind: resource.kind })
    }
  );
}

export async function exportReusableLibrary(request: {
  libraryId: string;
  name: string;
  version: string;
  resources: ReusableLibraryExportSelection[];
}): Promise<ReusableLibraryDownload> {
  const response = await fetch(`${API}/api/engineering/libraries/export`, {
    method: 'POST',
    headers: {
      accept: LIBRARY_MEDIA_TYPE,
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify(request)
  });

  if (!response.ok) throw await responseError(response);
  return {
    blob: await response.blob(),
    filename: filenameFromDisposition(response.headers.get('content-disposition')) ?? `${safeFileName(request.name)}.escadalib`
  };
}

export function triggerReusableLibraryDownload(download: ReusableLibraryDownload): void {
  const url = URL.createObjectURL(download.blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = download.filename;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}

async function requestJson<T = Record<string, unknown>>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    ...init,
    headers: {
      accept: 'application/json',
      ...init?.headers
    }
  });

  const text = await response.text();
  let data: unknown;
  if (text) {
    try { data = JSON.parse(text); } catch { data = undefined; }
  }
  if (!response.ok) throw new ReusableLibraryApiError(response.status, text, data);
  return (data ?? {}) as T;
}

async function responseError(response: Response): Promise<ReusableLibraryApiError> {
  const text = await response.text();
  let data: unknown;
  try { data = text ? JSON.parse(text) : undefined; } catch { data = undefined; }
  return new ReusableLibraryApiError(response.status, text, data);
}

function extractErrorMessage(value: unknown): string | null {
  if (!value || typeof value !== 'object') return null;
  if ('error' in value && typeof value.error === 'string') return value.error;
  return null;
}

function filenameFromDisposition(value: string | null): string | null {
  if (!value) return null;
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(value)?.[1];
  if (encoded) {
    try { return decodeURIComponent(encoded.replace(/^"|"$/g, '')); } catch { return encoded; }
  }
  return /filename="?([^";]+)"?/i.exec(value)?.[1]?.trim() ?? null;
}

function safeFileName(value: string): string {
  const normalized = value.trim().replace(/[^a-zA-Z0-9._-]+/g, '-').replace(/^-+|-+$/g, '');
  return normalized || 'elitescada-library';
}
