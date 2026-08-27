export type LocalUser = {
  id: string;
  username: string;
  displayName: string;
  isEnabled: boolean;
  roles: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type LocalRole = {
  key: string;
  name: string;
  description?: string | null;
};

export type CreateLocalUserInput = {
  username: string;
  displayName: string;
  password: string;
  roles: string[];
  isEnabled: boolean;
};

export type UpdateLocalUserInput = {
  displayName: string;
  roles: string[];
  isEnabled: boolean;
};

type ErrorPayload = {
  error?: string;
  unknownRoles?: string[];
};

export class AdministrationHttpError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly unknownRoles: string[] = []
  ) {
    super(message);
  }
}

export const localUserAdministrationApi = {
  listUsers: () => requestJson<LocalUser[]>('/api/auth/users'),
  listRoles: () => requestJson<LocalRole[]>('/api/auth/roles'),
  createUser: (input: CreateLocalUserInput) => requestJson<LocalUser>('/api/auth/users', {
    method: 'POST',
    body: JSON.stringify(input)
  }),
  updateUser: (id: string, input: UpdateLocalUserInput) => requestJson<LocalUser>(`/api/auth/users/${id}`, {
    method: 'PUT',
    body: JSON.stringify(input)
  }),
  resetPassword: (id: string, password: string) => requestJson<void>(`/api/auth/users/${id}/password-reset`, {
    method: 'POST',
    body: JSON.stringify({ password })
  })
};

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    let payload: ErrorPayload = {};
    try {
      payload = await response.json() as ErrorPayload;
    } catch {
      // Preserve the HTTP status when the server returned no JSON body.
    }

    throw new AdministrationHttpError(
      response.status,
      payload.error || `${response.status} ${response.statusText}`,
      Array.isArray(payload.unknownRoles) ? payload.unknownRoles : []
    );
  }

  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}
