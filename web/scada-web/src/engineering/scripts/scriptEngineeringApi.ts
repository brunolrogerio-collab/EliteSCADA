import {
  buildCanonicalScriptPackage,
  canonicalScriptPackageFingerprint,
  normalizeScriptDefinition,
  normalizeVisualEventReference
} from './ScriptEngineeringWorkspace.logic';
import type {
  CanonicalScriptPackage,
  ScriptDeleteResult,
  ScriptEngineeringContext,
  ScriptEngineeringDefinition,
  ScriptEngineeringWorkspaceDescriptor,
  ScriptImportMode,
  ScriptImportPreview,
  ScriptImportResult,
  ScriptMutationPreviewToken,
  ScriptVisualEventReference
} from './scriptEngineeringTypes';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class ScriptEngineeringApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly responseBody: string,
    public readonly responseData?: unknown
  ) {
    super(extractErrorMessage(responseData) ?? (responseBody || `HTTP ${status}`));
    this.name = 'ScriptEngineeringApiError';
  }
}

export async function loadScriptEngineeringContext(): Promise<ScriptEngineeringContext> {
  const [workspace, rawScripts, rawReferences] = await Promise.all([
    loadScriptEngineeringWorkspace(),
    requestJson<unknown[]>('/api/engineering/scripts'),
    requestJson<unknown[]>('/api/engineering/script-visual-event-references')
  ]);

  return {
    workspace,
    scripts: rawScripts.map(item => normalizeScriptDefinition((item ?? {}) as Record<string, unknown>)),
    visualEventReferences: rawReferences.map(item => normalizeVisualEventReference((item ?? {}) as Record<string, unknown>))
  };
}

export async function loadScriptEngineeringWorkspace(): Promise<ScriptEngineeringWorkspaceDescriptor> {
  return await requestJson<ScriptEngineeringWorkspaceDescriptor>('/api/engineering/workspace');
}

export async function previewScriptMutation(
  script: ScriptEngineeringDefinition,
  visualEventReferences: readonly ScriptVisualEventReference[],
  mode: ScriptImportMode
): Promise<ScriptMutationPreviewToken> {
  const workspace = await loadScriptEngineeringWorkspace();
  const packageData = buildCanonicalScriptPackage(script, visualEventReferences);
  const preview = await requestJson<ScriptImportPreview>(
    `/api/engineering/import/json/preview?mode=${encodeURIComponent(mode)}`,
    {
      method: 'POST',
      headers: { 'content-type': 'application/json; charset=utf-8' },
      body: JSON.stringify(packageData)
    }
  );

  return {
    package: packageData,
    packageFingerprint: canonicalScriptPackageFingerprint(packageData),
    mode,
    expectedChangeVersion: workspace.changeVersion,
    preview
  };
}

export async function applyScriptMutation(token: ScriptMutationPreviewToken): Promise<ScriptImportResult> {
  return await requestJson<ScriptImportResult>(
    `/api/engineering/import/json/apply?mode=${encodeURIComponent(token.mode)}`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json; charset=utf-8',
        'x-elitescada-workspace-version': String(token.expectedChangeVersion)
      },
      body: JSON.stringify(token.package)
    }
  );
}

export async function deleteScriptDefinition(
  scriptId: string,
  expectedChangeVersion: number
): Promise<ScriptDeleteResult> {
  return await requestJson<ScriptDeleteResult>(
    `/api/engineering/scripts/${encodeURIComponent(scriptId)}`,
    {
      method: 'DELETE',
      headers: {
        'x-elitescada-workspace-version': String(expectedChangeVersion)
      }
    }
  );
}

export function extractDeleteDependencies(error: unknown): Array<{
  entityKind: string;
  entityId: string;
  entityKey: string;
  relation: string;
}> {
  if (!(error instanceof ScriptEngineeringApiError)) return [];
  const data = error.responseData;
  if (!data || typeof data !== 'object' || !('dependencies' in data) || !Array.isArray(data.dependencies)) return [];
  return data.dependencies.flatMap(item => {
    if (!item || typeof item !== 'object') return [];
    const value = item as Record<string, unknown>;
    return [{
      entityKind: String(value.entityKind ?? ''),
      entityId: String(value.entityId ?? ''),
      entityKey: String(value.entityKey ?? ''),
      relation: String(value.relation ?? '')
    }];
  });
}

export function packageContainsOnlyScriptMutation(packageData: CanonicalScriptPackage): boolean {
  return packageData.tags.length === 0 &&
    packageData.alarms.length === 0 &&
    packageData.scripts.length === 1 &&
    packageData.scriptVisualEventReferences.every(reference => reference.scriptId === packageData.scripts[0]?.id);
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

  if (!response.ok) throw new ScriptEngineeringApiError(response.status, text, data);
  return (data ?? {}) as T;
}

function extractErrorMessage(value: unknown): string | null {
  if (!value || typeof value !== 'object') return null;
  if ('error' in value && typeof value.error === 'string') return value.error;
  return null;
}