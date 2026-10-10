import { admitInteractiveRuntimeSession } from '../runtimeSessionAdmissionApi';
import { assertRuntimeProcessMutationAllowed } from '../historical-playback/runtimeHistoricalPlaybackGuard';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class RuntimeCommandExecutionError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = 'RuntimeCommandExecutionError';
  }
}

export type RuntimeCommandFetch = (
  input: RequestInfo | URL,
  init?: RequestInit
) => Promise<Response>;

export async function executeRuntimeCommand(
  commandId: string,
  fetcher: RuntimeCommandFetch = fetch
): Promise<void> {
  assertRuntimeProcessMutationAllowed('Operational Command');
  const normalized = commandId.trim();
  if (!normalized) throw new RuntimeCommandExecutionError(400, 'Operational Command identity is required.');

  const leaseHeaders = await admitInteractiveRuntimeSession(fetcher);
  const response = await fetcher(`${API}/api/commands/${encodeURIComponent(normalized)}/execute`, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { accept: 'application/json', ...leaseHeaders }
  });

  if (!response.ok) {
    const body = await response.text();
    throw new RuntimeCommandExecutionError(
      response.status,
      body || `${response.status} ${response.statusText}`
    );
  }
}


export type RuntimeRichCommandOutcome =
  | 'Rejected'
  | 'Accepted'
  | 'Completed'
  | 'Failed'
  | 'TimedOut'
  | 'Unknown';

export type RuntimeRichCommandDefinition = Readonly<{
  commandId: string;
  semanticKey: string;
  description?: string | null;
  parameters: readonly Readonly<{
    key: string;
    required: boolean;
    description?: string | null;
    schema: Readonly<{
      kind: 'Boolean' | 'Integer' | 'Number' | 'String' | 'Enum' | 'Duration' | 'Percentage';
      minimum?: number | null;
      maximum?: number | null;
      maximumLength?: number | null;
      enumValues?: readonly string[] | null;
      unit?: string | null;
    }>;
  }>[];
}>;

export type RuntimeRichCommandResult = Readonly<{
  invocationId?: string;
  commandId: string;
  outcome: RuntimeRichCommandOutcome;
  observedAt: string;
  code?: string | null;
  message?: string | null;
}>;

const richCommandOutcomes = new Set<RuntimeRichCommandOutcome>([
  'Rejected', 'Accepted', 'Completed', 'Failed', 'TimedOut', 'Unknown'
]);

export async function loadRuntimeRichCommandDefinition(
  commandId: string,
  fetcher: RuntimeCommandFetch = fetch
): Promise<RuntimeRichCommandDefinition> {
  const normalized = commandId.trim();
  if (!normalized) throw new RuntimeCommandExecutionError(400, 'Rich Command identity is required.');

  const response = await fetcher(
    API + '/api/runtime/rich-commands/' + encodeURIComponent(normalized) + '/definition',
    {
      method: 'GET',
      credentials: 'same-origin',
      headers: { accept: 'application/json' }
    }
  );
  if (!response.ok) {
    const body = await response.text();
    throw new RuntimeCommandExecutionError(
      response.status,
      body || String(response.status) + ' ' + response.statusText
    );
  }

  const definition = await response.json() as RuntimeRichCommandDefinition;
  if (definition.commandId?.toLowerCase() !== normalized.toLowerCase() ||
      !Array.isArray(definition.parameters)) {
    throw new RuntimeCommandExecutionError(502, 'Active Rich Command definition response is invalid.');
  }
  return definition;
}

/**
 * Sends exactly one invocation request. A transport failure after this POST begins
 * is surfaced as Unknown; callers must never retry an ambiguous command outcome.
 */
export async function executeRuntimeRichCommand(
  commandId: string,
  parameters: Readonly<Record<string, unknown>> = {},
  fetcher: RuntimeCommandFetch = fetch
): Promise<RuntimeRichCommandResult> {
  const normalized = commandId.trim();
  if (!normalized)
    return richRejected('', 'command.identity_required', 'Rich Command identity is required.');

  let leaseHeaders: Record<string, string>;
  try {
    assertRuntimeProcessMutationAllowed('Rich Command');
    leaseHeaders = await admitInteractiveRuntimeSession(fetcher);
  } catch (reason) {
    return richRejected(
      normalized,
      'authorization.rejected',
      reason instanceof Error ? reason.message : String(reason)
    );
  }

  let response: Response;
  try {
    response = await fetcher(
      API + '/api/runtime/rich-commands/' + encodeURIComponent(normalized) + '/execute',
      {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json; charset=utf-8',
          ...leaseHeaders
        },
        body: JSON.stringify({ commandId: normalized, parameters })
      }
    );
  } catch {
    return richUnknown(normalized, 'transport.ambiguous', 'The Rich Command outcome is unknown.');
  }

  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    if (response.status >= 400 && response.status < 500) {
      return richRejected(
        normalized,
        'response.rejected',
        'Rich Command request was rejected (' + response.status + ').'
      );
    }
    return richUnknown(normalized, 'response.ambiguous', 'The Rich Command outcome is unknown.');
  }

  if (isRichCommandResult(payload) &&
      payload.commandId.toLowerCase() === normalized.toLowerCase()) {
    return payload;
  }

  if (response.status >= 400 && response.status < 500) {
    return richRejected(
      normalized,
      'response.rejected',
      'Rich Command request was rejected (' + response.status + ').'
    );
  }

  return richUnknown(
    normalized,
    'response.invalid',
    'The Rich Command outcome is unknown because the response did not prove the result.'
  );
}

function isRichCommandResult(value: unknown): value is RuntimeRichCommandResult {
  if (!value || typeof value !== 'object') return false;
  const result = value as Partial<RuntimeRichCommandResult>;
  return typeof result.commandId === 'string' &&
    typeof result.outcome === 'string' &&
    richCommandOutcomes.has(result.outcome as RuntimeRichCommandOutcome) &&
    typeof result.observedAt === 'string';
}

function richRejected(commandId: string, code: string, message: string): RuntimeRichCommandResult {
  return {
    commandId,
    outcome: 'Rejected',
    observedAt: new Date().toISOString(),
    code,
    message
  };
}

function richUnknown(commandId: string, code: string, message: string): RuntimeRichCommandResult {
  return {
    commandId,
    outcome: 'Unknown',
    observedAt: new Date().toISOString(),
    code,
    message
  };
}
