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
  const normalized = commandId.trim();
  if (!normalized) throw new RuntimeCommandExecutionError(400, 'Operational Command identity is required.');

  const response = await fetcher(`${API}/api/commands/${encodeURIComponent(normalized)}/execute`, {
    method: 'POST',
    headers: { accept: 'application/json' }
  });

  if (!response.ok) {
    const body = await response.text();
    throw new RuntimeCommandExecutionError(
      response.status,
      body || `${response.status} ${response.statusText}`
    );
  }
}