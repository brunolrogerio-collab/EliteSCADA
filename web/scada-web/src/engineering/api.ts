import type {
  CommunicationDriverDiagnostic,
  EngineeringPackageView,
  EngineeringSnapshot,
  EngineeringWorkspaceDescriptor,
  GatewayRuntimeDiagnostic,
  ImportPreviewView,
  ImportResultView,
  RuntimeDiagnosticsView
} from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

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
      securityRoles: engineeringPackage.securityRoles ?? [],
      gateways: engineeringPackage.gateways ?? []
    }
  };
}

export async function loadGatewayDiagnostics(): Promise<GatewayRuntimeDiagnostic[]> {
  return await getJson<GatewayRuntimeDiagnostic[]>('/api/gateway/diagnostics');
}

export async function loadCommunicationDiagnostics(): Promise<CommunicationDriverDiagnostic[]> {
  const diagnostics = await getJson<RuntimeDiagnosticsView>('/api/diagnostics/runtime');
  return diagnostics.runtime?.communicationDrivers ?? [];
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
