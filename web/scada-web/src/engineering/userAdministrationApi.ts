export type LocalUser = {
  id: string;
  username: string;
  displayName: string;
  isEnabled: boolean;
  roles: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type LocalRole = {
  key: string;
  name: string;
  description?: string | null;
};

export type CreateLocalUserInput = {
  username: string;
  displayName: string;
  password: string;
  roles: string[];
  isEnabled: boolean;
};

export type UpdateLocalUserInput = {
  displayName: string;
  roles: string[];
  isEnabled: boolean;
};

export const AUTHORITY_POLICY_SCHEMA = 'elitescada.authority-policy';
export const AUTHORITY_POLICY_SCHEMA_VERSION = 1;

export const SECURITY_CAPABILITIES = [
  { value: 0, id: 'View', group: 'runtime' },
  { value: 1, id: 'TagRead', group: 'runtime' },
  { value: 2, id: 'CommandExecute', group: 'runtime' },
  { value: 3, id: 'ProcessValueWrite', group: 'runtime' },
  { value: 4, id: 'AlarmAcknowledge', group: 'alarms' },
  { value: 5, id: 'AlarmShelve', group: 'alarms' },
  { value: 6, id: 'TrendUse', group: 'trends' },
  { value: 7, id: 'TrendSave', group: 'trends' },
  { value: 8, id: 'EngineeringModify', group: 'engineering' },
  { value: 9, id: 'UserRoleAdmin', group: 'users' },
  { value: 10, id: 'SystemAdmin', group: 'users' },
  { value: 11, id: 'EngineeringView', group: 'engineering' },
  { value: 12, id: 'HighAvailabilityObserve', group: 'ha' },
  { value: 13, id: 'HighAvailabilityTransfer', group: 'ha' },
  { value: 14, id: 'HighAvailabilityAdmin', group: 'ha' }
] as const;

export type SecurityCapabilityWire = number | string;
export type SecurityCapabilityGroup = typeof SECURITY_CAPABILITIES[number]['group'];

export type AuthorityGrantScope = {
  area?: string | null;
  equipmentPath?: string | null;
  screenKey?: string | null;
  tagPath?: string | null;
  commandKey?: string | null;
  scopeNodeId?: string | null;
  includeDescendants?: boolean;
};

export type AuthorityGrant = {
  capability: SecurityCapabilityWire;
  scope?: AuthorityGrantScope | null;
  metadata?: Record<string, string> | null;
};

export type AuthorityRole = {
  id?: string | null;
  key: string;
  name: string;
  description?: string | null;
  grants?: AuthorityGrant[] | null;
  metadata?: Record<string, string> | null;
};

export type AuthorityScopeNode = {
  id: string;
  key: string;
  name: string;
  kind: number | string;
  parentId?: string | null;
  resourceId?: string | null;
  metadata?: Record<string, string> | null;
};

export type AuthorityPolicyDocument = {
  schema: string;
  schemaVersion: number;
  version: number;
  roles: AuthorityRole[];
  scopes: AuthorityScopeNode[];
};

export type AuthorityPolicyMutationRequest = {
  schema: string;
  schemaVersion: number;
  expectedVersion: number;
  roles: AuthorityRole[];
  scopes: AuthorityScopeNode[];
};

export type AuthorityPolicyPreview = {
  schema: string;
  schemaVersion: number;
  valid: boolean;
  expectedVersion: number;
  policy: AuthorityPolicyDocument;
};

export type EffectiveCapabilities = {
  authorityPolicy: {
    schema: string;
    schemaVersion: number;
  };
  authenticationEnabled: boolean;
  runtime: string[];
  workspace: string[];
};

type ErrorPayload = {
  error?: string;
  unknownRoles?: string[];
  currentVersion?: number;
};

export class AdministrationHttpError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly unknownRoles: string[] = [],
    public readonly currentVersion?: number
  ) {
    super(message);
    this.name = 'AdministrationHttpError';
  }
}

export const localUserAdministrationApi = {
  listUsers: () => requestJson<LocalUser[]>('/api/auth/users'),
  listRoles: () => requestJson<LocalRole[]>('/api/auth/roles'),
  createUser: (input: CreateLocalUserInput) => requestJson<LocalUser>('/api/auth/users', {
    method: 'POST',
    body: JSON.stringify(input)
  }),
  updateUser: (id: string, input: UpdateLocalUserInput) => requestJson<LocalUser>('/api/auth/users/' + encodeURIComponent(id), {
    method: 'PUT',
    body: JSON.stringify(input)
  }),
  resetPassword: (id: string, password: string) => requestJson<void>('/api/auth/users/' + encodeURIComponent(id) + '/password-reset', {
    method: 'POST',
    body: JSON.stringify({ password })
  })
};

export const authorityPolicyAdministrationApi = {
  load: () => requestJson<AuthorityPolicyDocument>('/api/auth/authority-policy'),
  preview: (input: AuthorityPolicyMutationRequest) => requestJson<AuthorityPolicyPreview>('/api/auth/authority-policy/preview', {
    method: 'POST',
    body: JSON.stringify(input)
  }),
  apply: (input: AuthorityPolicyMutationRequest) => requestJson<AuthorityPolicyDocument>('/api/auth/authority-policy', {
    method: 'PUT',
    body: JSON.stringify(input)
  }),
  effectiveCapabilities: () => requestJson<EffectiveCapabilities>('/api/auth/effective-capabilities')
};

export function toAuthorityMutationRequest(policy: AuthorityPolicyDocument): AuthorityPolicyMutationRequest {
  return {
    schema: AUTHORITY_POLICY_SCHEMA,
    schemaVersion: AUTHORITY_POLICY_SCHEMA_VERSION,
    expectedVersion: policy.version,
    roles: policy.roles,
    scopes: policy.scopes
  };
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    let payload: ErrorPayload = {};
    try {
      payload = await response.json() as ErrorPayload;
    } catch {
      // Preserve the HTTP status when the server returned no JSON body.
    }

    throw new AdministrationHttpError(
      response.status,
      payload.error || response.status + ' ' + response.statusText,
      Array.isArray(payload.unknownRoles) ? payload.unknownRoles : [],
      typeof payload.currentVersion === 'number' ? payload.currentVersion : undefined
    );
  }

  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}
