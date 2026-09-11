import { useCallback, useEffect, useState } from 'react';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type SecurityCapability =
  | 'View'
  | 'TagRead'
  | 'CommandExecute'
  | 'ProcessValueWrite'
  | 'AlarmAcknowledge'
  | 'AlarmShelve'
  | 'TrendUse'
  | 'TrendSave'
  | 'EngineeringModify'
  | 'UserRoleAdmin'
  | 'SystemAdmin';

export type EffectiveCapabilities = Readonly<{
  authenticationEnabled: boolean;
  runtime: ReadonlySet<SecurityCapability>;
  workspace: ReadonlySet<SecurityCapability>;
}>;

type EffectiveCapabilitiesWire = Readonly<{
  authenticationEnabled: boolean;
  runtime: readonly SecurityCapability[];
  workspace: readonly SecurityCapability[];
}>;

export type EffectiveCapabilitiesState = Readonly<{
  capabilities: EffectiveCapabilities | null;
  loading: boolean;
  error: Error | null;
  reload: () => Promise<void>;
}>;

export type AppSurfaceAccess = Readonly<{
  runtime: boolean;
  history: boolean;
  engineering: boolean;
  audit: boolean;
  licensing: boolean;
}>;

export function hasRuntimeCapability(
  capabilities: EffectiveCapabilities | null,
  capability: SecurityCapability
): boolean {
  return capabilities?.runtime.has(capability) === true;
}

export function hasWorkspaceCapability(
  capabilities: EffectiveCapabilities | null,
  capability: SecurityCapability
): boolean {
  return capabilities?.workspace.has(capability) === true;
}

/**
 * Frontend projection of the backend gates for first-class application surfaces.
 * Keep every grant independent: one capability never implies another here.
 *
 * Backend authority mirrored here:
 * - Runtime application: Runtime View.
 * - Historian samples: Runtime TrendUse (the route additionally requires Runtime View).
 * - Engineering workspace: Workspace EngineeringModify.
 * - Audit: Runtime SystemAdmin.
 * - Licensing: Workspace EngineeringModify via RequireWorkspaceEngineeringRead.
 */
export function resolveAppSurfaceAccess(
  capabilities: EffectiveCapabilities | null
): AppSurfaceAccess {
  return Object.freeze({
    runtime: hasRuntimeCapability(capabilities, 'View'),
    history: hasRuntimeCapability(capabilities, 'TrendUse'),
    engineering: hasWorkspaceCapability(capabilities, 'EngineeringModify'),
    audit: hasRuntimeCapability(capabilities, 'SystemAdmin'),
    licensing: hasWorkspaceCapability(capabilities, 'EngineeringModify')
  });
}

export function useEffectiveCapabilities(): EffectiveCapabilitiesState {
  const [capabilities, setCapabilities] = useState<EffectiveCapabilities | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${API}/api/auth/effective-capabilities`, {
        headers: { accept: 'application/json' }
      });
      if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
      const payload = await response.json() as EffectiveCapabilitiesWire;
      setCapabilities(Object.freeze({
        authenticationEnabled: payload.authenticationEnabled,
        runtime: new Set(payload.runtime),
        workspace: new Set(payload.workspace)
      }));
    } catch (reason) {
      setCapabilities(null);
      setError(reason instanceof Error ? reason : new Error(String(reason)));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void reload(); }, [reload]);

  return { capabilities, loading, error, reload };
}
