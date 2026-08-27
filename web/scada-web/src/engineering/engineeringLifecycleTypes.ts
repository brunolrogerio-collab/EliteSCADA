export type EngineeringLifecycleWorkspaceDescriptor = {
  projectKey?: string | null;
  projectName?: string | null;
  baseRevision?: number | null;
  checkedOutAtUtc?: string | null;
  lastSavedAtUtc?: string | null;
  isDirty: boolean;
  changeVersion: number;
};

export type EngineeringPersistenceStatus = {
  enabled: boolean;
  provider?: string | null;
  configuredProjectKey?: string | null;
};

export type EngineeringRevisionMetadata = {
  revision: number;
  projectKey: string;
  projectName: string;
  engineeringSchema: string;
  engineeringSchemaVersion: number;
  savedAtUtc: string;
  savedBy?: string | null;
  basedOnRevision?: number | null;
};

export type EngineeringProjectLifecycle = {
  projectKey: string;
  status: string | number;
  workingRevision?: number | null;
  publishedRevision?: number | null;
  publishedAtUtc?: string | null;
  publishedBy?: string | null;
  runtimeStatus: string | number;
  activeRevision?: number | null;
  activatedAtUtc?: string | null;
  activatedBy?: string | null;
};

export type EngineeringRuntimeDescriptor = {
  mode: string;
  projectKey?: string | null;
  revision?: number | null;
  activatedAtUtc?: string | null;
  tagCount?: number;
  activeAlarmCount?: number;
};

export type EngineeringRuntimeConsistency = {
  projectKey: string;
  configuredProjectKey?: string | null;
  consistent: boolean;
  durable: EngineeringProjectLifecycle;
  live: EngineeringRuntimeDescriptor;
};

export type EngineeringLifecycleState = {
  workspace: EngineeringLifecycleWorkspaceDescriptor;
  persistence: EngineeringPersistenceStatus;
  projectKey: string | null;
  lifecycle: EngineeringProjectLifecycle | null;
  revisions: EngineeringRevisionMetadata[];
  runtime: EngineeringRuntimeConsistency | null;
};

export type EngineeringLifecycleAction = 'save' | 'checkout' | 'publish' | 'activate';
