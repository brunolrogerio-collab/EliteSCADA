export type DatabaseTopologyMode = 'LocalManaged' | 'Remote';

export type DatabaseTlsMode =
  | 'Disable'
  | 'Prefer'
  | 'Require'
  | 'VerifyCa'
  | 'VerifyFull';

export type DatabaseMigrationPhase =
  | 'Candidate'
  | 'Tested'
  | 'Compatible'
  | 'Prepared'
  | 'Quiescing'
  | 'Copying'
  | 'Copied'
  | 'Verifying'
  | 'Verified'
  | 'Switching'
  | 'Readiness'
  | 'Completed'
  | 'RollbackRequired'
  | 'RolledBack'
  | 'Failed';

export type DatabaseRemoteEndpointRequest = Readonly<{
  host: string;
  port: number;
  database: string;
  username: string;
  password?: string | null;
  credentialReference?: string | null;
  tlsMode: DatabaseTlsMode;
  rootCertificatePath?: string | null;
  trustServerCertificate: false;
  timeoutSeconds: number;
}>;

export type DatabaseRemoteProfileRequest = Readonly<{
  primary: DatabaseRemoteEndpointRequest;
  historianOverride?: DatabaseRemoteEndpointRequest | null;
}>;

export type DatabaseEndpointStatus = Readonly<{
  host: string;
  port: number;
  database: string;
  username: string;
  tlsMode: DatabaseTlsMode;
  credentialConfigured: boolean;
  trustServerCertificate: boolean;
  rootCertificateConfigured: boolean;
  timeoutSeconds: number;
}>;

export type DatabaseProfileStatus = Readonly<{
  mode: DatabaseTopologyMode;
  primary?: DatabaseEndpointStatus | null;
  historianUsesPrimary: boolean;
  historianOverride?: DatabaseEndpointStatus | null;
}>;

export type DatabaseConnectionHealth = Readonly<{
  reachable: boolean;
  postgreSqlVersion?: string | null;
  postgreSqlMajor?: number | null;
  timescaleDbVersion?: string | null;
  timescaleCapable: boolean;
  schemaCompatible: boolean;
  checkedAtUtc: string;
  failureCode?: string | null;
  diagnostic?: string | null;
}>;

export type DatabaseTopologyOperationSummary = Readonly<{
  operationId: string;
  phase: DatabaseMigrationPhase;
  completedAtUtc: string;
  failureCode?: string | null;
  diagnostic?: string | null;
}>;

export type DatabaseTopologyStatus = Readonly<{
  activeTopology: DatabaseProfileStatus;
  previousTopology?: DatabaseProfileStatus | null;
  pendingPhase?: DatabaseMigrationPhase | null;
  pendingOperationId?: string | null;
  recoveryRequired: boolean;
  restartRequired: boolean;
  primaryHealth?: DatabaseConnectionHealth | null;
  historianHealth?: DatabaseConnectionHealth | null;
  lastHealthCheckUtc?: string | null;
  lastOperation?: DatabaseTopologyOperationSummary | null;
}>;

export type DatabaseCompatibilityResult = Readonly<{
  compatible: boolean;
  primary: DatabaseConnectionHealth;
  historian?: DatabaseConnectionHealth | null;
  failureCode?: string | null;
  diagnostic?: string | null;
}>;

export type DatabaseMigrationPlan = Readonly<{
  operationId: string;
  preparedAtUtc: string;
  historianUsesPrimary: boolean;
  timescaleRequired: boolean;
  durableDomains: readonly string[];
  sourceMode: string;
  targetMode: string;
}>;

export type DatabaseRemoteEndpoint = Readonly<{
  host: string;
  port: number;
  database: string;
  username: string;
  credentialReference?: string | null;
  tlsMode: DatabaseTlsMode;
  rootCertificatePath?: string | null;
  trustServerCertificate: boolean;
  timeoutSeconds: number;
}>;

export type DatabaseTopologyProfile = Readonly<{
  mode: DatabaseTopologyMode;
  primary?: DatabaseRemoteEndpoint | null;
  historian: Readonly<{
    usePrimary: boolean;
    override?: DatabaseRemoteEndpoint | null;
  }>;
}>;

export type DatabaseMigrationVerification = Readonly<{
  succeeded: boolean;
  sourceRows: Readonly<Record<string, number>>;
  targetRows: Readonly<Record<string, number>>;
  activeProjectKey?: string | null;
  activeRevision?: number | null;
  failureCode?: string | null;
  diagnostic?: string | null;
}>;

export type DatabasePendingMigration = Readonly<{
  operationId: string;
  candidate: DatabaseTopologyProfile;
  phase: DatabaseMigrationPhase;
  startedAtUtc: string;
  updatedAtUtc: string;
  plan?: DatabaseMigrationPlan | null;
  verification?: DatabaseMigrationVerification | null;
  maintenanceLeaseExpiresAtUtc?: string | null;
  failureCode?: string | null;
  diagnostic?: string | null;
}>;

export type DatabaseCutoverResult = Readonly<{
  succeeded: boolean;
  rolledBack: boolean;
  restartRequired: boolean;
  status: DatabaseTopologyStatus;
  failureCode?: string | null;
  diagnostic?: string | null;
}>;

export type RemoteEndpointDraft = Readonly<{
  host: string;
  port: string;
  database: string;
  username: string;
  password: string;
  tlsMode: DatabaseTlsMode;
  rootCertificatePath: string;
  timeoutSeconds: string;
}>;

export type RemoteProfileDraft = Readonly<{
  primary: RemoteEndpointDraft;
  historianUsesPrimary: boolean;
  historian: RemoteEndpointDraft;
}>;

export const emptyRemoteEndpointDraft = (): RemoteEndpointDraft => ({
  host: '',
  port: '5432',
  database: 'elitescada',
  username: '',
  password: '',
  tlsMode: 'Require',
  rootCertificatePath: '',
  timeoutSeconds: '15'
});

export const emptyRemoteProfileDraft = (): RemoteProfileDraft => ({
  primary: emptyRemoteEndpointDraft(),
  historianUsesPrimary: true,
  historian: emptyRemoteEndpointDraft()
});
