export type HaLocale = 'pt-BR' | 'en' | 'es';

export type HaEndpoint = {
  kind: number | string;
  address: string;
  priority: number;
};

export type HaNodeSnapshot = {
  nodeId: string;
  role: string;
  state: number | string;
  ready: boolean;
  healthy: boolean;
  synchronizationComplete: boolean;
  haLicenseEntitled: boolean;
  runtime: {
    projectKey?: string | null;
    revision?: number | null;
    mode?: string | null;
  };
  lastObservedAtUtc?: string | null;
  fresh: boolean;
  readinessReason?: string | null;
  endpoints: HaEndpoint[];
};

export type HaTransferOperation = {
  transferId: string;
  sourceNodeId: string;
  targetNodeId: string;
  breakEpoch: number;
  startedAtUtc: string;
};

export type HaTopologySnapshot = {
  schema: string;
  schemaVersion: number;
  enabled: boolean;
  clusterId?: string | null;
  topologyVersion: number;
  stateVersion: number;
  authorityEpoch: number;
  authorityInstanceId: string;
  localNodeId: string;
  effectiveActiveNodeId?: string | null;
  ambiguousAuthority: boolean;
  pendingTransfer?: HaTransferOperation | null;
  generatedAtUtc: string;
  nodes: HaNodeSnapshot[];
};

export type HaHostNodeConfiguration = {
  nodeId: string;
  localEndpoint?: string | null;
  remoteEndpoint?: string | null;
};

export type HaHostPeerTransportView = {
  enabled: boolean;
  peerEndpoint?: string | null;
  authenticationConfigured: boolean;
};

export type HaHostProtectionConfiguration = {
  enabled: boolean;
  automaticFailoverEnabled: boolean;
  referenceStoreMode: string;
  referencePath?: string | null;
  leaseSeconds: number;
  pollMilliseconds: number;
  readyWitnessMaximumAgeSeconds: number;
  clockSkewSafetyMarginSeconds: number;
};

export type HaHostConfigurationView = {
  enabled: boolean;
  clusterId?: string | null;
  localNodeId: string;
  initialActiveNodeId?: string | null;
  topologyVersion: number;
  freshnessSeconds: number;
  nodes: HaHostNodeConfiguration[];
  peerTransport: HaHostPeerTransportView;
  protection: HaHostProtectionConfiguration;
};

export type HaReferenceStoreRequirements = {
  mode: string;
  failClosedWhenUnavailable: boolean;
  summary: string;
  requiredSemantics: string[];
};

export type HaHostConfigurationSnapshot = {
  schema: string;
  schemaVersion: number;
  generation: number;
  updatedAtUtc: string;
  pendingRestart: boolean;
  applyMode: string;
  industrialEffectsBlocked: boolean;
  running: HaHostConfigurationView;
  desired: HaHostConfigurationView;
  referenceStoreRequirements: HaReferenceStoreRequirements;
};

export type HaHostConfigurationUpdateRequest = {
  expectedGeneration: number;
  enabled: boolean;
  clusterId?: string | null;
  localNodeId: string;
  initialActiveNodeId?: string | null;
  topologyVersion: number;
  freshnessSeconds: number;
  nodes: HaHostNodeConfiguration[];
  peerTransport: {
    enabled: boolean;
    peerEndpoint?: string | null;
    peerSharedSecret?: string | null;
    clearPeerSharedSecret: false;
  };
  protection: HaHostProtectionConfiguration;
};

export type HaHostConfigurationUpdateResult = {
  accepted: boolean;
  reasonCode: string;
  snapshot: HaHostConfigurationSnapshot;
  errors: string[];
};

export type HaReferenceAuthority = {
  schema: string;
  schemaVersion: number;
  clusterId: string;
  topologyVersion: number;
  epoch: number;
  activeNodeId?: string | null;
  leaseId: string;
  leaseUntilUtc: string;
  updatedAtUtc: string;
  previousAuthorityFenced: boolean;
  reasonCode: string;
};

export type HaStandbyPromotionWitness = {
  nodeId: string;
  readiness: {
    healthy: boolean;
    synchronizationComplete: boolean;
    haLicenseEntitled: boolean;
    observedAtUtc: string;
    diagnostic?: string | null;
  };
  readyAtUtc: string;
};

export type HaProtectionDiagnostics = {
  enabled: boolean;
  automaticFailoverEnabled: boolean;
  industrialEffectsPermitted: boolean;
  status: string;
  reasonCode?: string | null;
  reference?: HaReferenceAuthority | null;
  lastReadyStandbyWitness?: HaStandbyPromotionWitness | null;
  topology: HaTopologySnapshot;
};

export type HaPeerMirrorSnapshot = {
  hasState: boolean;
  liveSynchronized: boolean;
  sourceNodeId?: string | null;
  sourceTransportInstanceId?: string | null;
  replicationSequence?: number | null;
  authoritativeState?: unknown;
  receivedAtUtc?: string | null;
  reasonCode?: string | null;
};

export type HaPeerDiagnostics = {
  connectionState: string;
  authenticationConfigured: boolean;
  localNodeId: string;
  peerNodeId?: string | null;
  peerEndpoint?: string | null;
  transportInstanceId: string;
  lastOutboundSequence: number;
  lastInboundSequence?: number | null;
  lastOutboundSuccessAtUtc?: string | null;
  lastInboundAtUtc?: string | null;
  consecutiveFailures: number;
  reasonCode?: string | null;
  mirror: HaPeerMirrorSnapshot;
};

export type HaProtectionOperation = {
  operationId: string;
  kind: string;
  state: string;
  sourceNodeId?: string | null;
  targetNodeId?: string | null;
  epoch?: number | null;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  reasonCode: string;
};

export type HaAuthoritySnapshot = {
  schema: string;
  schemaVersion: number;
  enabled: boolean;
  clusterId?: string | null;
  topologyVersion: number;
  authorityEpoch: number;
  effectiveActiveNodeId?: string | null;
  ambiguousAuthority: boolean;
  blocked: boolean;
  activeEndpoints: HaEndpoint[];
  protectionStatus: string;
  generatedAtUtc: string;
};

export type HaAdministrationSnapshot = {
  schema: string;
  schemaVersion: number;
  protection: HaProtectionDiagnostics;
  peer: HaPeerDiagnostics;
  configuration: HaHostConfigurationSnapshot;
  operations: HaProtectionOperation[];
};

export type HaLicenseStatus = {
  state: string;
  tier?: string | null;
  schemaVersion?: number | null;
  haRuntime?: boolean | null;
  diagnostic?: string | null;
};

export type HaLicensingSnapshot = {
  license: HaLicenseStatus;
  runtime: {
    state: string;
    activeLicenseState?: string | null;
    activeTier?: string | null;
    lastDiagnostic?: string | null;
  };
};

export type HaWorkspaceSnapshot = {
  topology: HaTopologySnapshot;
  authority: HaAuthoritySnapshot;
  administration: HaAdministrationSnapshot;
  configuration: HaHostConfigurationSnapshot;
  peer: HaPeerDiagnostics;
  licensing: HaLicensingSnapshot;
};

export type HaActionKind = 'switchover' | 'failback' | 'recovery';
