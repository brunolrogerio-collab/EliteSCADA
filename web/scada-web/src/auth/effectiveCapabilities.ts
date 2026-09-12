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
  | 'EngineeringView'
  | 'EngineeringModify'
  | 'UserRoleAdmin'
  | 'SystemAdmin'
  | 'HighAvailabilityObserve'
  | 'HighAvailabilityTransfer'
  | 'HighAvailabilityAdmin';

export const AUTHORITY_POLICY_SCHEMA = 'elitescada.authority-policy';
export const AUTHORITY_POLICY_SCHEMA_VERSION = 1;

const KNOWN_SECURITY_CAPABILITIES: ReadonlySet<string> = new Set<SecurityCapability>([
  'View',
  'TagRead',
  'CommandExecute',
  'ProcessValueWrite',
  'AlarmAcknowledge',
  'AlarmShelve',
  'TrendUse',
  'TrendSave',
  'EngineeringView',
  'EngineeringModify',
  'UserRoleAdmin',
  'SystemAdmin',
  'HighAvailabilityObserve',
  'HighAvailabilityTransfer',
  'HighAvailabilityAdmin'
]);

export type AuthorityPolicyContract = Readonly<{
  schema: typeof AUTHORITY_POLICY_SCHEMA;
  schemaVersion: typeof AUTHORITY_POLICY_SCHEMA_VERSION;
}>;

export type EffectiveCapabilities = Readonly<{
  authorityPolicy: AuthorityPolicyContract;
  authenticationEnabled: boolean;
  runtime: ReadonlySet<SecurityCapability>;
  workspace: ReadonlySet<SecurityCapability>;
}>;

type EffectiveCapabilitiesWire = Readonly<{
  authorityPolicy?: Readonly<{
    schema?: unknown;
    schemaVersion?: unknown;
  }>;
  authenticationEnabled?: unknown;
  runtime?: unknown;
  workspace?: unknown;
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

function parseCapabilitySet(value: unknown): ReadonlySet<SecurityCapability> {
  if (!Array.isArray(value) || value.some(id => typeof id !== 'string' || !KNOWN_SECURITY_CAPABILITIES.has(id))) {
    throw new Error('Unsupported Authority capability ID.');
  }

  return new Set(value as SecurityCapability[]);
}

/**
 * Frontend projection of the backend gates for first-class application surfaces.
 * Keep every grant independent: one capability never implies another here.
 *
 * Backend authority mirrored here:
 * - Runtime application: Runtime View.
 * - Historian samples: Runtime TrendUse (the route additionally requires Runtime View).
 * - Engineering workspace: Workspace EngineeringView.
 * - Audit: Runtime SystemAdmin.
 * - Licensing: Workspace EngineeringView via RequireWorkspaceEngineeringRead.
 */
export function resolveAppSurfaceAccess(
  capabilities: EffectiveCapabilities | null
): AppSurfaceAccess {
  return Object.freeze({
    runtime: hasRuntimeCapability(capabilities, 'View'),
    history: hasRuntimeCapability(capabilities, 'TrendUse'),
    engineering: hasWorkspaceCapability(capabilities, 'EngineeringView'),
    audit: hasRuntimeCapability(capabilities, 'SystemAdmin'),
    licensing: hasWorkspaceCapability(capabilities, 'EngineeringView')
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
      if (payload.authorityPolicy?.schema !== AUTHORITY_POLICY_SCHEMA ||
          payload.authorityPolicy.schemaVersion !== AUTHORITY_POLICY_SCHEMA_VERSION) {
        throw new Error('Unsupported Authority policy contract.');
      }
      if (typeof payload.authenticationEnabled !== 'boolean') {
        throw new Error('Invalid effective capabilities response.');
      }
      setCapabilities(Object.freeze({
        authorityPolicy: Object.freeze({
          schema: AUTHORITY_POLICY_SCHEMA,
          schemaVersion: AUTHORITY_POLICY_SCHEMA_VERSION
        }),
        authenticationEnabled: payload.authenticationEnabled,
        runtime: parseCapabilitySet(payload.runtime),
        workspace: parseCapabilitySet(payload.workspace)
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
