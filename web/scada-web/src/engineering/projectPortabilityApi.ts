import type {
  ProjectPackageInspection,
  ProjectPortabilityApplyResult,
  ProjectPortabilityContext,
  ProjectPortabilityDownload,
  ProjectPortabilityMergeMode,
  ProjectPortabilityPreview,
  ProjectPortabilityWorkspace
} from './projectPortabilityTypes';

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');
const PACKAGE_MEDIA_TYPE = 'application/vnd.elitescada.project-package';

export class ProjectPortabilityApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly responseBody: string,
    public readonly responseData?: unknown
  ) {
    super(extractErrorMessage(responseData) ?? (responseBody || `HTTP ${status}`));
    this.name = 'ProjectPortabilityApiError';
  }
}

export async function loadProjectPortabilityContext(): Promise<ProjectPortabilityContext> {
  const [workspace, canonical] = await Promise.all([
    requestJson<ProjectPortabilityWorkspace>('/api/engineering/workspace'),
    requestJson<{ schema: string; schemaVersion: number; exportedAt?: string | null }>('/api/engineering/export/json')
  ]);

  return {
    workspace,
    canonical: {
      schema: canonical.schema,
      schemaVersion: canonical.schemaVersion,
      exportedAt: canonical.exportedAt ?? null
    }
  };
}

export async function loadProjectPortabilityWorkspace(): Promise<ProjectPortabilityWorkspace> {
  return await requestJson<ProjectPortabilityWorkspace>('/api/engineering/workspace');
}

export async function exportCanonicalEngineeringJson(): Promise<ProjectPortabilityDownload> {
  return await requestDownload('/api/engineering/export/json', 'scada-engineering.json');
}

export async function previewCanonicalEngineeringJson(
  jsonText: string,
  mode: ProjectPortabilityMergeMode
): Promise<{ preview: ProjectPortabilityPreview; expectedChangeVersion: number }> {
  const workspace = await loadProjectPortabilityWorkspace();
  const preview = await requestJson<ProjectPortabilityPreview>(
    `/api/engineering/import/json/preview?mode=${encodeURIComponent(mode)}`,
    {
      method: 'POST',
      headers: { 'content-type': 'application/json; charset=utf-8' },
      body: jsonText
    }
  );
  return { preview, expectedChangeVersion: workspace.changeVersion };
}

export async function applyCanonicalEngineeringJson(
  jsonText: string,
  mode: ProjectPortabilityMergeMode,
  expectedChangeVersion: number
): Promise<ProjectPortabilityApplyResult> {
  return await requestJson<ProjectPortabilityApplyResult>(
    `/api/engineering/import/json/apply?mode=${encodeURIComponent(mode)}`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json; charset=utf-8',
        'x-elitescada-workspace-version': String(expectedChangeVersion)
      },
      body: jsonText
    }
  );
}

export async function exportProjectPackage(
  projectKey: string,
  projectName: string
): Promise<ProjectPortabilityDownload> {
  const query = new URLSearchParams({ projectKey, projectName });
  return await requestDownload(
    `/api/project-package/export?${query.toString()}`,
    `${safeFileName(projectKey)}.escadapkg`
  );
}

export async function inspectProjectPackage(file: Blob): Promise<ProjectPackageInspection> {
  return await requestJson<ProjectPackageInspection>('/api/project-package/inspect', {
    method: 'POST',
    headers: { 'content-type': PACKAGE_MEDIA_TYPE },
    body: file
  });
}

export async function previewProjectPackage(
  file: Blob,
  mode: ProjectPortabilityMergeMode
): Promise<{ preview: ProjectPortabilityPreview; expectedChangeVersion: number }> {
  const workspace = await loadProjectPortabilityWorkspace();
  const preview = await requestJson<ProjectPortabilityPreview>(
    `/api/project-package/import/preview?mode=${encodeURIComponent(mode)}`,
    {
      method: 'POST',
      headers: { 'content-type': PACKAGE_MEDIA_TYPE },
      body: file
    }
  );
  return { preview, expectedChangeVersion: workspace.changeVersion };
}

export async function restoreProjectPackage(
  file: Blob,
  mode: ProjectPortabilityMergeMode,
  expectedChangeVersion: number
): Promise<ProjectPortabilityApplyResult> {
  return await requestJson<ProjectPortabilityApplyResult>(
    `/api/project-package/import/apply?mode=${encodeURIComponent(mode)}`,
    {
      method: 'POST',
      headers: {
        'content-type': PACKAGE_MEDIA_TYPE,
        'x-elitescada-workspace-version': String(expectedChangeVersion)
      },
      body: file
    }
  );
}

export function triggerBrowserDownload(download: ProjectPortabilityDownload): void {
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

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
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
    try {
      data = JSON.parse(text);
    } catch {
      data = undefined;
    }
  }

  if (!response.ok) throw new ProjectPortabilityApiError(response.status, text, data);
  return (data ?? {}) as T;
}

async function requestDownload(path: string, fallbackFilename: string): Promise<ProjectPortabilityDownload> {
  const response = await fetch(`${API}${path}`, { headers: { accept: '*/*' } });
  if (!response.ok) {
    const text = await response.text();
    let data: unknown;
    try {
      data = text ? JSON.parse(text) : undefined;
    } catch {
      data = undefined;
    }
    throw new ProjectPortabilityApiError(response.status, text, data);
  }

  return {
    blob: await response.blob(),
    filename: filenameFromDisposition(response.headers.get('content-disposition')) ?? fallbackFilename
  };
}

function filenameFromDisposition(value: string | null): string | null {
  if (!value) return null;
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(value)?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded.replace(/^"|"$/g, ''));
    } catch {
      return encoded;
    }
  }
  return /filename="?([^";]+)"?/i.exec(value)?.[1]?.trim() ?? null;
}

function extractErrorMessage(value: unknown): string | null {
  if (!value || typeof value !== 'object') return null;
  if ('error' in value && typeof value.error === 'string') return value.error;
  return null;
}

function safeFileName(value: string): string {
  const normalized = value.trim().replace(/[^a-zA-Z0-9._-]+/g, '-').replace(/^-+|-+$/g, '');
  return normalized || 'elitescada-project';
}
