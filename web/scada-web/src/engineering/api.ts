import type {
  EngineeringPackageView,
  EngineeringSnapshot,
  EngineeringWorkspaceDescriptor,
  ImportPreviewView,
  ImportResultView
} from './types';

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    headers: { accept: 'application/json' }
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  return await response.json() as T;
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
      securityRoles: engineeringPackage.securityRoles ?? []
    }
  };
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

export class EngineeringWorkspaceConflictError extends Error {
  constructor(
    public readonly expectedChangeVersion: number,
    public readonly currentChangeVersion: number
  ) {
    super(`Engineering Workspace changed from version ${expectedChangeVersion} to ${currentChangeVersion}. Reload and validate the draft again.`);
    this.name = 'EngineeringWorkspaceConflictError';
  }
}
