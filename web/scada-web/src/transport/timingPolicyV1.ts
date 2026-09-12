export const TIMING_POLICY_V1_SCHEMA = 'elitescada.timing-policy/v1' as const;

export const TIMING_HEADERS = Object.freeze({
  correlationId: 'X-EliteSCADA-Correlation-Id',
  requestId: 'X-EliteSCADA-Request-Id',
  operation: 'X-EliteSCADA-Operation',
  category: 'X-EliteSCADA-Request-Category',
  attempt: 'X-EliteSCADA-Attempt'
});

export type AdaptiveTimingCategory =
  | 'transportConnect'
  | 'requestRead'
  | 'bootstrap'
  | 'longOperation'
  | 'realtimeConnect'
  | 'reconnect'
  | 'realtimeHeartbeat'
  | 'commandWriteResponse'
  | 'staleData';

export type RemoteFailureCategory =
  | 'callerCancelled'
  | 'policyTimeout'
  | 'offline'
  | 'dns'
  | 'connect'
  | 'tls'
  | 'transport'
  | 'responseIntegrity'
  | 'httpStatus'
  | 'parseSchema'
  | 'staleData'
  | 'unknownMutationOutcome';

export type RemoteRequestMetadata = Readonly<{
  requestId: string;
  operation: string;
  category: AdaptiveTimingCategory;
  attempt: number;
  startedAtUtc: string;
  authorityGeneration?: string | null;
}>;

export type RemoteFailure = Readonly<{
  category: RemoteFailureCategory;
  message: string;
  metadata: RemoteRequestMetadata;
  correlationId?: string | null;
  httpStatus?: number | null;
  retryable: boolean;
  outcomeUnknown: boolean;
}>;

export type TimingValueBounds = Readonly<{
  default: number;
  minimum: number;
  maximum: number;
}>;

export type TimingPolicyV1Values = Readonly<{
  connectBudgetMilliseconds: number;
  readBudgetMilliseconds: number;
  bootstrapBudgetMilliseconds: number;
  longOperationBudgetMilliseconds: number;
  realtimeConnectBudgetMilliseconds: number;
  reconnectDelayMilliseconds: readonly number[];
  reconnectJitterRatio: number;
  realtimeObservationMilliseconds: number;
  realtimeStaleAfterMilliseconds: number;
  ordinaryReadMaxRetries: number;
  commandWriteResponseBudgetMilliseconds: number;
  slowThresholdMilliseconds: number;
  staleMinimumMilliseconds: number;
}>;

export type TimingPolicyV1Contract = Readonly<{
  schema: typeof TIMING_POLICY_V1_SCHEMA;
  schemaVersion: 1;
  adaptiveCategories: readonly AdaptiveTimingCategory[];
  failureCategories: readonly RemoteFailureCategory[];
  effective: TimingPolicyV1Values;
  limits: Readonly<Record<keyof Omit<TimingPolicyV1Values, 'reconnectDelayMilliseconds' | 'reconnectJitterRatio'>, TimingValueBounds>> & Readonly<{
    reconnectDelayMilliseconds: TimingValueBounds;
    reconnectJitterRatioDefault: number;
    reconnectJitterRatioMinimum: number;
    reconnectJitterRatioMaximum: number;
  }>;
  excludedOwnershipDomains: readonly (
    | 'securitySession'
    | 'haAuthority'
    | 'driverProtocol'
    | 'internalExecution'
  )[];
  correlation: Readonly<{
    responseHeader: string;
    requestIdHeader: string;
    operationHeader: string;
    categoryHeader: string;
    attemptHeader: string;
  }>;
}>;

export function createRemoteRequestMetadata(
  operation: string,
  category: AdaptiveTimingCategory,
  attempt = 1,
  authorityGeneration?: string | null
): RemoteRequestMetadata {
  const normalizedOperation = operation.trim();
  if (!normalizedOperation || normalizedOperation.length > 96 || !/^[A-Za-z0-9][A-Za-z0-9._:/-]*$/.test(normalizedOperation)) {
    throw new Error('Remote operation must be a safe identifier of at most 96 characters.');
  }
  if (!Number.isInteger(attempt) || attempt < 1 || attempt > 4) {
    throw new Error('Remote request attempt must be between 1 and 4.');
  }

  return Object.freeze({
    requestId: crypto.randomUUID(),
    operation: normalizedOperation,
    category,
    attempt,
    startedAtUtc: new Date().toISOString(),
    authorityGeneration
  });
}

export function requestMetadataHeaders(metadata: RemoteRequestMetadata): Readonly<Record<string, string>> {
  return Object.freeze({
    [TIMING_HEADERS.requestId]: metadata.requestId,
    [TIMING_HEADERS.operation]: metadata.operation,
    [TIMING_HEADERS.category]: metadata.category,
    [TIMING_HEADERS.attempt]: String(metadata.attempt)
  });
}
