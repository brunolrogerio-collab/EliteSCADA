const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type RuntimeTagWriteValue = string | number | boolean;

export class RuntimeTagWriteError extends Error {
  constructor(
    message: string,
    public readonly status?: number
  ) {
    super(message);
    this.name = 'RuntimeTagWriteError';
  }
}

export async function writeRuntimeTagValue(tagId: string, value: RuntimeTagWriteValue): Promise<void> {
  const normalizedTagId = tagId.trim();
  if (!normalizedTagId) throw new RuntimeTagWriteError('A stable TAG identity is required.');
  if (!isRuntimeTagWriteValue(value)) {
    throw new RuntimeTagWriteError('TAG write value must be a boolean, finite number, or string.');
  }

  let response: Response;
  try {
    response = await fetch(`${API}/api/tags/${encodeURIComponent(normalizedTagId)}/write`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        accept: 'application/json',
        'content-type': 'application/json; charset=utf-8'
      },
      body: JSON.stringify({ value })
    });
  } catch (reason) {
    throw new RuntimeTagWriteError(reason instanceof Error ? reason.message : String(reason));
  }

  if (response.ok) return;
  const body = await response.text();
  throw new RuntimeTagWriteError(body || `${response.status} ${response.statusText}`.trim(), response.status);
}

function isRuntimeTagWriteValue(value: unknown): value is RuntimeTagWriteValue {
  return typeof value === 'boolean' ||
    typeof value === 'string' ||
    (typeof value === 'number' && Number.isFinite(value));
}
