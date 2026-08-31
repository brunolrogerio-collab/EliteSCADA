import type { ClientVisualPythonDispatchStatus } from '../../python-runtime/clientVisualPythonRuntime';
import type { PythonSourceDiagnostic } from '../../python-runtime/pythonRuntimeContracts';

export const PYTHON_PREVIEW_SAMPLE_LIMITS = {
  maxCharacters: 16_384,
  maxDepth: 8,
  maxNodes: 256
} as const;

export type PythonPreviewState =
  | 'idle'
  | 'running'
  | 'success'
  | 'validation-error'
  | 'runtime-error'
  | 'timed-out'
  | 'cancelled'
  | 'unavailable';

export type PythonPreviewTraceFrame = {
  functionName: string;
  line: number;
};

export type PythonPreviewTrace = {
  exceptionType: string;
  failingLine: number;
  frames: PythonPreviewTraceFrame[];
};

export type PythonPreviewProjection = {
  state: Exclude<PythonPreviewState, 'idle' | 'running' | 'unavailable'>;
  status: ClientVisualPythonDispatchStatus;
  durationMs?: number;
  sanitizedError?: string;
  trace?: PythonPreviewTrace;
};

export type PythonPreviewSampleParseResult =
  | { ok: true; value: unknown }
  | { ok: false; error: 'empty' | 'too-large' | 'invalid-json' | 'too-deep' | 'too-many-values' | 'unsupported-key' };

type PythonPreviewSampleError = Extract<PythonPreviewSampleParseResult, { ok: false }>['error'];

const runtimeFaultPattern = /Python handler failed \(([A-Za-z_][A-Za-z0-9_]{0,63})\) at line ([1-9][0-9]*)\.?/;
const sensitiveAssignmentPattern = /\b(password|passwd|pwd|token|secret|api[_-]?key|credential|connection[_-]?string)\b\s*[:=]\s*([^,;}]+)/gi;
const bearerPattern = /\bBearer\s+[A-Za-z0-9._~+/=-]+/gi;

export function parsePythonPreviewSample(text: string): PythonPreviewSampleParseResult {
  const normalized = text.trim();
  if (!normalized) return { ok: false, error: 'empty' };
  if (normalized.length > PYTHON_PREVIEW_SAMPLE_LIMITS.maxCharacters) {
    return { ok: false, error: 'too-large' };
  }

  let value: unknown;
  try {
    value = JSON.parse(normalized) as unknown;
  } catch {
    return { ok: false, error: 'invalid-json' };
  }

  const boundary = inspectBoundedValue(value, 0, { nodes: 0 });
  return boundary ? { ok: false, error: boundary } : { ok: true, value };
}

export function projectPythonPreviewExecution(
  handlerName: string,
  status: ClientVisualPythonDispatchStatus,
  durationMs?: number,
  sanitizedError?: string
): PythonPreviewProjection {
  const trace = sanitizedError ? parseSanitizedTrace(handlerName, sanitizedError) : undefined;
  const state: PythonPreviewProjection['state'] = status === 'completed'
    ? 'success'
    : status === 'timed-out'
      ? 'timed-out'
      : status === 'cancelled'
        ? 'cancelled'
        : 'runtime-error';

  return {
    state,
    status,
    durationMs,
    sanitizedError: sanitizedError ? redactPythonPreviewText(sanitizedError) : undefined,
    trace
  };
}

export function firstBlockingPythonDiagnostic(
  diagnostics: readonly PythonSourceDiagnostic[]
): PythonSourceDiagnostic | undefined {
  return diagnostics.find(item => item.severity === 'error');
}

export function sourceLineForPreview(source: string, line: number): string | undefined {
  if (!Number.isInteger(line) || line < 1) return undefined;
  const value = source.split(/\r?\n/)[line - 1];
  return value === undefined ? undefined : redactPythonPreviewText(value.trimEnd());
}

export function redactPythonPreviewText(value: string): string {
  return value
    .replace(sensitiveAssignmentPattern, (_match, key: string) => `${key}=<redacted>`)
    .replace(bearerPattern, 'Bearer <redacted>');
}

function parseSanitizedTrace(handlerName: string, sanitizedError: string): PythonPreviewTrace | undefined {
  const match = runtimeFaultPattern.exec(sanitizedError);
  if (!match) return undefined;
  const failingLine = Number.parseInt(match[2], 10);
  if (!Number.isSafeInteger(failingLine) || failingLine < 1) return undefined;

  return {
    exceptionType: match[1],
    failingLine,
    frames: [{ functionName: safeHandlerName(handlerName), line: failingLine }]
  };
}

function safeHandlerName(value: string): string {
  return /^[A-Za-z_][A-Za-z0-9_]{0,127}$/.test(value) ? value : '<handler>';
}

function inspectBoundedValue(
  value: unknown,
  depth: number,
  counter: { nodes: number }
): PythonPreviewSampleError | undefined {
  counter.nodes += 1;
  if (counter.nodes > PYTHON_PREVIEW_SAMPLE_LIMITS.maxNodes) return 'too-many-values';
  if (depth > PYTHON_PREVIEW_SAMPLE_LIMITS.maxDepth) return 'too-deep';

  if (value === null || typeof value === 'string' || typeof value === 'boolean') return undefined;
  if (typeof value === 'number') return Number.isFinite(value) ? undefined : 'invalid-json';
  if (Array.isArray(value)) {
    for (const item of value) {
      const issue = inspectBoundedValue(item, depth + 1, counter);
      if (issue) return issue;
    }
    return undefined;
  }
  if (typeof value !== 'object') return 'invalid-json';

  for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
    if (key === '__proto__' || key === 'prototype' || key === 'constructor') return 'unsupported-key';
    const issue = inspectBoundedValue(item, depth + 1, counter);
    if (issue) return issue;
  }
  return undefined;
}
