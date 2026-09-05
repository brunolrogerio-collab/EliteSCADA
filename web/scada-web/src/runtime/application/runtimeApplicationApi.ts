import type {
  DynamoEngineering,
  PopupEngineering,
  ScreenEngineering,
  VisualAssetEngineering
} from '../../engineering/types';
import type {
  ScriptEngineeringDefinition,
  ScriptVisualEventReference
} from '../../engineering/scripts/scriptEngineeringTypes';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type RuntimeHmiEngineeringPackage = Readonly<{
  schema: string;
  schemaVersion: number;
  exportedAt?: string | null;
  startupScreenId?: string | null;
  screens: ScreenEngineering[];
  popups: PopupEngineering[];
  dynamos: DynamoEngineering[];
  scripts: ScriptEngineeringDefinition[];
  scriptVisualEventReferences: ScriptVisualEventReference[];
  visualAssets: VisualAssetEngineering[];
}>;

export type RuntimeApplicationProjection = Readonly<{
  mode: 'simulation' | 'engineering';
  projectKey?: string | null;
  projectName?: string | null;
  revision?: number | null;
  activatedAtUtc?: string | null;
  package?: RuntimeHmiEngineeringPackage | null;
}>;

export class RuntimeApplicationProjectionError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = 'RuntimeApplicationProjectionError';
  }
}

export async function loadRuntimeApplicationProjection(
  signal?: AbortSignal
): Promise<RuntimeApplicationProjection> {
  const response = await fetch(`${API}/api/runtime/application`, {
    headers: { accept: 'application/json' },
    signal
  });

  if (!response.ok) {
    const body = await response.text();
    throw new RuntimeApplicationProjectionError(
      response.status,
      body || `${response.status} ${response.statusText}`
    );
  }

  const projection = await response.json() as RuntimeApplicationProjection;
  if (projection.mode === 'engineering') {
    if (!projection.projectKey?.trim() || !Number.isInteger(projection.revision) || !projection.package) {
      throw new RuntimeApplicationProjectionError(
        500,
        'Active Engineering Runtime projection is incomplete.'
      );
    }
  }
  return projection;
}

export function runtimeVisualAssetContentUrl(assetId: string): string {
  const normalized = assetId.trim().toLowerCase().startsWith('asset:')
    ? assetId.trim().slice('asset:'.length)
    : assetId.trim();
  return `${API}/api/runtime/visual-assets/${encodeURIComponent(normalized)}/content`;
}
