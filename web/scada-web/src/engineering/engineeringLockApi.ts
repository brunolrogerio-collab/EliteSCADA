const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type EngineeringLockStatus = Readonly<{
  configured: boolean;
  locked: boolean;
}>;

type ErrorPayload = Readonly<{ error?: string }>;

export class EngineeringLockApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'EngineeringLockApiError';
  }
}

export async function loadEngineeringLockStatus(): Promise<EngineeringLockStatus> {
  return await request('/api/engineering/lock/status');
}

export async function configureEngineeringLock(
  secret: string,
  lockImmediately = false
): Promise<EngineeringLockStatus> {
  return await request('/api/engineering/lock/configure', {
    method: 'POST',
    body: JSON.stringify({ secret, lockImmediately })
  });
}

export async function lockEngineering(): Promise<EngineeringLockStatus> {
  return await request('/api/engineering/lock/lock', { method: 'POST' });
}

export async function unlockEngineering(secret: string): Promise<EngineeringLockStatus> {
  return await request('/api/engineering/lock/unlock', {
    method: 'POST',
    body: JSON.stringify({ secret })
  });
}

export async function clearEngineeringLock(): Promise<EngineeringLockStatus> {
  return await request('/api/engineering/lock/clear', { method: 'POST' });
}

async function request(path: string, init?: RequestInit): Promise<EngineeringLockStatus> {
  const response = await fetch(`${API}${path}`, {
    ...init,
    headers: {
      accept: 'application/json',
      ...(init?.body ? { 'content-type': 'application/json' } : {}),
      ...(init?.headers ?? {})
    }
  });

  let payload: EngineeringLockStatus | ErrorPayload | null = null;
  try {
    payload = await response.json() as EngineeringLockStatus | ErrorPayload;
  } catch {
    payload = null;
  }

  if (!response.ok) {
    const message = payload && 'error' in payload && typeof payload.error === 'string'
      ? payload.error
      : `${response.status} ${response.statusText}`;
    throw new EngineeringLockApiError(response.status, message);
  }

  if (!payload || !('configured' in payload) || !('locked' in payload)) {
    throw new EngineeringLockApiError(502, 'Invalid Engineering Lock status response.');
  }

  return Object.freeze({ configured: Boolean(payload.configured), locked: Boolean(payload.locked) });
}
