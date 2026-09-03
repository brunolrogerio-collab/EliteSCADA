export const CLIENT_VISUAL_PYTHON_BRIDGE_VERSION = 1 as const;

export const CLIENT_VISUAL_PYTHON_POLICY = {
  handlerTimeoutMs: 250,
  hardStopGraceMs: 50,
  maxQueuedEvents: 128,
  minimumTimerIntervalMs: 50,
  maxConsecutiveFailuresBeforeThrottle: 5,
  maxBridgeDepth: 32,
  maxBridgeNodes: 4096,
  maxBridgeStringLength: 65536,
  queueOverflowStrategy: 'coalesce-by-event-key',
  faultIsolationScope: 'script-runtime-instance'
} as const;

/**
 * Capabilities exposed by the official EliteSCADA Client Visual Python product
 * provider. Product surfaces such as the Script Assistant and API Help iterate
 * this list so they never advertise host-specific protocol reservations as if
 * those operations were available to ordinary scripts.
 */
export const CLIENT_VISUAL_PYTHON_CAPABILITIES = [
  'tag.read',
  'tag.write',
  'clientMemory.read',
  'clientMemory.write',
  'visualProperty.read',
  'visualProperty.write',
  'visualTween.request'
] as const;

/**
 * Complete bridge protocol vocabulary. `backendOperation.request` remains a
 * reserved host-composition hook and therefore stays type-safe and fail-closed
 * in the dispatcher, but the official product provider does not compose or
 * advertise it.
 */
export const CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES = [
  ...CLIENT_VISUAL_PYTHON_CAPABILITIES,
  'backendOperation.request'
] as const;

export type ClientVisualPythonCapability = typeof CLIENT_VISUAL_PYTHON_PROTOCOL_CAPABILITIES[number];

export const CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES = [
  'filesystem',
  'operating-system',
  'shell-process',
  'arbitrary-network',
  'database',
  'industrial-driver',
  'secret-credential',
  'browser-dom',
  'browser-storage',
  'server-memory-write',
  'shared-tag-write-direct'
] as const;

export type ClientVisualPythonDeniedBoundary = typeof CLIENT_VISUAL_PYTHON_DENIED_BOUNDARIES[number];

export type PythonRuntimeIdentity = {
  scriptId: string;
  runtimeInstanceId: string;
  visualRuntimeInstanceId?: string;
};

export type PythonSourceDiagnostic = {
  severity: 'error' | 'warning' | 'info';
  code: string;
  message: string;
  line: number;
  column: number;
  endLine?: number;
  endColumn?: number;
};

export type PythonWorkerRequest =
  | { kind: 'initialize-script'; requestId: string; identity: PythonRuntimeIdentity; source: string; handlerNames: string[] }
  | { kind: 'compile-source'; requestId: string; identity: PythonRuntimeIdentity; source: string }
  | { kind: 'dispatch-event'; requestId: string; executionId: string; identity: PythonRuntimeIdentity; handlerName: string; eventKey: string; payload: unknown; deadlineEpochMs: number }
  | { kind: 'api-response'; requestId: string; identity: PythonRuntimeIdentity; ok: boolean; value?: unknown; error?: string }
  | { kind: 'cancel-execution'; requestId: string; executionId: string; identity: PythonRuntimeIdentity }
  | { kind: 'dispose-script'; requestId: string; identity: PythonRuntimeIdentity };

export type PythonWorkerResponse =
  | { kind: 'ready'; requestId: string; identity: PythonRuntimeIdentity }
  | { kind: 'compile-result'; requestId: string; identity: PythonRuntimeIdentity; diagnostics: PythonSourceDiagnostic[] }
  | { kind: 'execution-result'; requestId: string; executionId: string; identity: PythonRuntimeIdentity; handlerName: string; eventKey: string; payload: unknown; deadlineEpochMs: number }
  | { kind: 'execution-result'; requestId: string; executionId: string; identity: PythonRuntimeIdentity; status: 'completed' | 'cancelled' | 'timed-out' | 'faulted' | 'throttled'; durationMs: number; sanitizedError?: string }
  | { kind: 'api-request'; requestId: string; executionId: string; identity: PythonRuntimeIdentity; capability: ClientVisualPythonCapability; operation: string; arguments: unknown }
  | { kind: 'diagnostic'; requestId: string; identity: PythonRuntimeIdentity; diagnostic: PythonSourceDiagnostic }
  | { kind: 'disposed'; requestId: string; identity: PythonRuntimeIdentity };

export type PythonWorkerEnvelope<T extends PythonWorkerRequest | PythonWorkerResponse> = {
  bridgeVersion: typeof CLIENT_VISUAL_PYTHON_BRIDGE_VERSION;
  message: T;
};

export function hasMatchingPythonRuntimeIdentity(
  expected: PythonRuntimeIdentity,
  actual: PythonRuntimeIdentity
): boolean {
  return expected.scriptId === actual.scriptId &&
    expected.runtimeInstanceId === actual.runtimeInstanceId &&
    expected.visualRuntimeInstanceId === actual.visualRuntimeInstanceId;
}
