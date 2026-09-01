const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class RuntimeTagWriteError extends Error {
  constructor(
    message: string,
    public readonly status?: number
  ) {
    super(message);
    this.name = 'RuntimeTagWriteError';
  }
}

export async function writeRuntimeTagValue(tagId: string, value: number): Promise<void> {
  const normalizedTagId = tagId.trim();
  if (!normalizedTagId) throw new RuntimeTagWriteError('A stable TAG identity is required.');
  if (!Number.isFinite(value)) throw new RuntimeTagWriteError('TAG write value must be finite.');

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
