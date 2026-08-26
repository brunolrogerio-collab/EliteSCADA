import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import type { EngineeringLocale } from './i18n';
import './userAdministration.css';

type LocalUser = {
  id: string;
  username: string;
  displayName: string;
  isEnabled: boolean;
  roles: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

type LocalRole = {
  key: string;
  name: string;
  description?: string | null;
};

type Strings = typeof strings['pt-BR'];

const strings = {
  'pt-BR': {
    eyebrow: 'Identidades locais',
    title: 'Usuários',
    description: 'Contas locais ficam fora do Engineering Package e recebem apenas chaves de papéis definidos pelo projeto.',
    notAuthorized: 'Sua conta não possui UserRoleAdmin para administrar usuários locais.',
    loadError: 'Não foi possível carregar os usuários locais.',
    retry: 'Tentar novamente',
    create: 'Novo usuário',
    username: 'Usuário',
    displayName: 'Nome de exibição',
    password: 'Senha inicial',
    passwordHint: 'Mínimo de 12 caracteres.',
    enabled: 'Habilitado',
    roles: 'Papéis',
    noRoles: 'Nenhum papel definido no Engineering Workspace.',
    createAction: 'Criar usuário',
    creating: 'Criando...',
    edit: 'Editar usuário',
    save: 'Salvar alterações',
    saving: 'Salvando...',
    resetPassword: 'Redefinir senha',
    newPassword: 'Nova senha',
    resetAction: 'Redefinir',
    resetting: 'Redefinindo...',
    select: 'Selecionar',
    active: 'Ativo',
    disabled: 'Desabilitado',
    created: 'Criado',
    updated: 'Atualizado',
    none: 'Nenhum usuário local cadastrado.',
    refresh: 'Atualizar',
    saved: 'Usuário atualizado.',
    createdSuccess: 'Usuário criado.',
    passwordSuccess: 'Senha redefinida. Sessões anteriores deste usuário foram invalidadas.',
    sessionNote: 'Alterar papel, estado, perfil ou senha invalida os JWTs locais anteriores desse usuário.',
    unknownError: 'Falha na operação.'
  },
  en: {
    eyebrow: 'Local identities',
    title: 'Users',
    description: 'Local accounts stay outside the Engineering Package and only receive role keys defined by the project.',
    notAuthorized: 'Your account does not have UserRoleAdmin to manage local users.',
    loadError: 'Local users could not be loaded.',
    retry: 'Try again',
    create: 'New user',
    username: 'Username',
    displayName: 'Display name',
    password: 'Initial password',
    passwordHint: 'At least 12 characters.',
    enabled: 'Enabled',
    roles: 'Roles',
    noRoles: 'No roles are defined in the Engineering Workspace.',
    createAction: 'Create user',
    creating: 'Creating...',
    edit: 'Edit user',
    save: 'Save changes',
    saving: 'Saving...',
    resetPassword: 'Reset password',
    newPassword: 'New password',
    resetAction: 'Reset',
    resetting: 'Resetting...',
    select: 'Select',
    active: 'Active',
    disabled: 'Disabled',
    created: 'Created',
    updated: 'Updated',
    none: 'No local users are configured.',
    refresh: 'Refresh',
    saved: 'User updated.',
    createdSuccess: 'User created.',
    passwordSuccess: 'Password reset. Previous local sessions for this user were invalidated.',
    sessionNote: 'Changing roles, status, profile or password invalidates this user’s previous local JWTs.',
    unknownError: 'Operation failed.'
  },
  es: {
    eyebrow: 'Identidades locales',
    title: 'Usuarios',
    description: 'Las cuentas locales permanecen fuera del Engineering Package y solo reciben claves de roles definidos por el proyecto.',
    notAuthorized: 'Su cuenta no posee UserRoleAdmin para administrar usuarios locales.',
    loadError: 'No fue posible cargar los usuarios locales.',
    retry: 'Intentar nuevamente',
    create: 'Nuevo usuario',
    username: 'Usuario',
    displayName: 'Nombre para mostrar',
    password: 'Contraseña inicial',
    passwordHint: 'Mínimo de 12 caracteres.',
    enabled: 'Habilitado',
    roles: 'Roles',
    noRoles: 'No hay roles definidos en el Engineering Workspace.',
    createAction: 'Crear usuario',
    creating: 'Creando...',
    edit: 'Editar usuario',
    save: 'Guardar cambios',
    saving: 'Guardando...',
    resetPassword: 'Restablecer contraseña',
    newPassword: 'Nueva contraseña',
    resetAction: 'Restablecer',
    resetting: 'Restableciendo...',
    select: 'Seleccionar',
    active: 'Activo',
    disabled: 'Deshabilitado',
    created: 'Creado',
    updated: 'Actualizado',
    none: 'No hay usuarios locales registrados.',
    refresh: 'Actualizar',
    saved: 'Usuario actualizado.',
    createdSuccess: 'Usuario creado.',
    passwordSuccess: 'Contraseña restablecida. Las sesiones locales anteriores de este usuario fueron invalidadas.',
    sessionNote: 'Cambiar roles, estado, perfil o contraseña invalida los JWT locales anteriores de este usuario.',
    unknownError: 'La operación falló.'
  }
} as const;

export function UserAdministration({ locale }: { locale: EngineeringLocale }) {
  const s: Strings = strings[locale];
  const [users, setUsers] = useState<LocalUser[]>([]);
  const [roles, setRoles] = useState<LocalRole[]>([]);
  const [loading, setLoading] = useState(true);
  const [forbidden, setForbidden] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    setForbidden(false);
    try {
      const [nextUsers, nextRoles] = await Promise.all([
        requestJson<LocalUser[]>('/api/auth/users'),
        requestJson<LocalRole[]>('/api/auth/roles')
      ]);
      setUsers(nextUsers);
      setRoles(nextRoles);
      setSelectedId(current => current && nextUsers.some(user => user.id === current) ? current : nextUsers[0]?.id ?? null);
    } catch (reason) {
      if (reason instanceof HttpError && (reason.status === 401 || reason.status === 403)) {
        setForbidden(true);
      } else {
        setError(messageOf(reason, s.unknownError));
      }
    } finally {
      setLoading(false);
    }
  }, [s.unknownError]);

  useEffect(() => {
    void load();
  }, [load]);

  const selected = useMemo(
    () => users.find(user => user.id === selectedId) ?? null,
    [selectedId, users]
  );

  if (loading) {
    return <section className="eng-panel user-admin"><div className="user-admin-state">{s.title}…</div></section>;
  }

  if (forbidden) {
    return (
      <section className="eng-panel user-admin">
        <header><span>{s.eyebrow}</span><h2>{s.title}</h2></header>
        <div className="user-admin-state warning">{s.notAuthorized}</div>
      </section>
    );
  }

  if (error) {
    return (
      <section className="eng-panel user-admin">
        <header><span>{s.eyebrow}</span><h2>{s.title}</h2></header>
        <div className="user-admin-state error">{s.loadError}<small>{error}</small></div>
        <button type="button" className="user-admin-button" onClick={() => void load()}>{s.retry}</button>
      </section>
    );
  }

  return (
    <section className="eng-panel user-admin" data-testid="user-administration">
      <header className="user-admin-header">
        <div>
          <span>{s.eyebrow}</span>
          <h2>{s.title}</h2>
          <p>{s.description}</p>
        </div>
        <button type="button" className="user-admin-button secondary" onClick={() => void load()}>{s.refresh}</button>
      </header>

      {notice && <div className="user-admin-notice" role="status">{notice}</div>}
      <p className="user-admin-note">{s.sessionNote}</p>

      <div className="user-admin-grid">
        <div className="user-admin-column">
          <CreateUserForm
            roles={roles}
            s={s}
            onCreated={async () => {
              setNotice(s.createdSuccess);
              await load();
            }}
            onError={setError}
          />

          <div className="user-list" data-testid="user-list">
            {users.length === 0 && <div className="user-admin-state">{s.none}</div>}
            {users.map(user => (
              <button
                type="button"
                className={`user-row ${selectedId === user.id ? 'selected' : ''}`}
                key={user.id}
                onClick={() => setSelectedId(user.id)}
              >
                <span className={`user-status ${user.isEnabled ? 'enabled' : 'disabled'}`} />
                <span className="user-row-main"><strong>{user.displayName}</strong><small>{user.username}</small></span>
                <span className="user-row-roles">{user.roles.join(', ') || '—'}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="user-admin-column">
          {selected ? (
            <EditUserForm
              key={`${selected.id}:${selected.updatedAtUtc}`}
              user={selected}
              roles={roles}
              locale={locale}
              s={s}
              onSaved={async () => {
                setNotice(s.saved);
                await load();
              }}
              onPasswordReset={async () => {
                setNotice(s.passwordSuccess);
                await load();
              }}
              onError={setError}
            />
          ) : (
            <div className="user-admin-state">{s.none}</div>
          )}
        </div>
      </div>
    </section>
  );
}

function CreateUserForm({
  roles,
  s,
  onCreated,
  onError
}: {
  roles: LocalRole[];
  s: Strings;
  onCreated: () => Promise<void>;
  onError: (message: string) => void;
}) {
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await requestJson<LocalUser>('/api/auth/users', {
        method: 'POST',
        body: JSON.stringify({ username, displayName, password, roles: selectedRoles, isEnabled: enabled })
      });
      setUsername('');
      setDisplayName('');
      setPassword('');
      setSelectedRoles([]);
      setEnabled(true);
      await onCreated();
    } catch (reason) {
      onError(messageOf(reason, s.unknownError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="user-admin-form" onSubmit={event => void submit(event)} data-testid="create-user-form">
      <h3>{s.create}</h3>
      <label>{s.username}<input name="new-username" value={username} minLength={3} required onChange={e => setUsername(e.target.value)} /></label>
      <label>{s.displayName}<input name="new-display-name" value={displayName} maxLength={300} required onChange={e => setDisplayName(e.target.value)} /></label>
      <label>{s.password}<input name="new-password" type="password" value={password} minLength={12} required onChange={e => setPassword(e.target.value)} /><small>{s.passwordHint}</small></label>
      <label className="user-admin-toggle"><input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)} />{s.enabled}</label>
      <RolePicker roles={roles} selected={selectedRoles} onChange={setSelectedRoles} label={s.roles} empty={s.noRoles} />
      <button className="user-admin-button primary" type="submit" disabled={busy}>{busy ? s.creating : s.createAction}</button>
    </form>
  );
}

function EditUserForm({
  user,
  roles,
  locale,
  s,
  onSaved,
  onPasswordReset,
  onError
}: {
  user: LocalUser;
  roles: LocalRole[];
  locale: EngineeringLocale;
  s: Strings;
  onSaved: () => Promise<void>;
  onPasswordReset: () => Promise<void>;
  onError: (message: string) => void;
}) {
  const [displayName, setDisplayName] = useState(user.displayName);
  const [enabled, setEnabled] = useState(user.isEnabled);
  const [selectedRoles, setSelectedRoles] = useState<string[]>(user.roles);
  const [newPassword, setNewPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      await requestJson<LocalUser>(`/api/auth/users/${user.id}`, {
        method: 'PUT',
        body: JSON.stringify({ displayName, isEnabled: enabled, roles: selectedRoles })
      });
      await onSaved();
    } catch (reason) {
      onError(messageOf(reason, s.unknownError));
    } finally {
      setSaving(false);
    }
  };

  const resetPassword = async (event: FormEvent) => {
    event.preventDefault();
    setResetting(true);
    try {
      await requestJson<void>(`/api/auth/users/${user.id}/password-reset`, {
        method: 'POST',
        body: JSON.stringify({ password: newPassword })
      });
      setNewPassword('');
      await onPasswordReset();
    } catch (reason) {
      onError(messageOf(reason, s.unknownError));
    } finally {
      setResetting(false);
    }
  };

  return (
    <div className="user-edit" data-testid="edit-user-form">
      <form className="user-admin-form" onSubmit={event => void save(event)}>
        <h3>{s.edit}</h3>
        <div className="user-admin-id"><strong>{user.username}</strong><small>{user.id}</small></div>
        <label>{s.displayName}<input name="edit-display-name" value={displayName} maxLength={300} required onChange={e => setDisplayName(e.target.value)} /></label>
        <label className="user-admin-toggle"><input name="edit-enabled" type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)} />{s.enabled}</label>
        <RolePicker roles={roles} selected={selectedRoles} onChange={setSelectedRoles} label={s.roles} empty={s.noRoles} />
        <div className="user-admin-meta"><span>{s.created}: {formatDate(user.createdAtUtc, locale)}</span><span>{s.updated}: {formatDate(user.updatedAtUtc, locale)}</span></div>
        <button className="user-admin-button primary" type="submit" disabled={saving}>{saving ? s.saving : s.save}</button>
      </form>

      <form className="user-admin-form password-reset" onSubmit={event => void resetPassword(event)}>
        <h3>{s.resetPassword}</h3>
        <label>{s.newPassword}<input name="reset-password" type="password" value={newPassword} minLength={12} required onChange={e => setNewPassword(e.target.value)} /><small>{s.passwordHint}</small></label>
        <button className="user-admin-button" type="submit" disabled={resetting}>{resetting ? s.resetting : s.resetAction}</button>
      </form>
    </div>
  );
}

function RolePicker({
  roles,
  selected,
  onChange,
  label,
  empty
}: {
  roles: LocalRole[];
  selected: string[];
  onChange: (roles: string[]) => void;
  label: string;
  empty: string;
}) {
  const toggle = (key: string, checked: boolean) => {
    const next = checked
      ? [...selected, key]
      : selected.filter(role => role.toLowerCase() !== key.toLowerCase());
    onChange([...new Set(next)].sort((a, b) => a.localeCompare(b)));
  };

  return (
    <fieldset className="user-role-picker">
      <legend>{label}</legend>
      {roles.length === 0 && <small>{empty}</small>}
      {roles.map(role => (
        <label key={role.key} title={role.description ?? undefined}>
          <input
            type="checkbox"
            checked={selected.some(item => item.toLowerCase() === role.key.toLowerCase())}
            onChange={event => toggle(role.key, event.target.checked)}
          />
          <span><strong>{role.name}</strong><code>{role.key}</code></span>
        </label>
      ))}
    </fieldset>
  );
}

class HttpError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: init?.body ? { 'Content-Type': 'application/json', ...(init.headers ?? {}) } : init?.headers
  });
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json() as { error?: string };
      if (body.error) message = body.error;
    } catch {
      // Keep the HTTP status when the server returned no JSON body.
    }
    throw new HttpError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

function messageOf(reason: unknown, fallback: string) {
  return reason instanceof Error && reason.message ? reason.message : fallback;
}

function formatDate(value: string, locale: EngineeringLocale) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(locale, {
    dateStyle: 'short',
    timeStyle: 'short'
  }).format(date);
}
