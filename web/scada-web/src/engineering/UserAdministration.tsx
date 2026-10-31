import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthGate';
import {
  countAdministrationUsers,
  filterAdministrationUsers,
  summarizeUserChanges,
  type UserChange,
  type UserStatusFilter
} from './UserAdministration.logic';
import type { EngineeringLocale } from './i18n';
import { AuthorityPolicyAdministration } from './AuthorityPolicyAdministration';
import {
  AdministrationHttpError,
  localUserAdministrationApi,
  type LocalAuthenticationSessionPolicy,
  type LocalRole,
  type LocalUser
} from './userAdministrationApi';
import './userAdministration.css';

type AccessFailure = 'unauthorized' | 'forbidden' | null;

type UiStrings = {
  eyebrow: string;
  title: string;
  description: string;
  unauthorized: string;
  notAuthorized: string;
  loadError: string;
  retry: string;
  refresh: string;
  refreshing: string;
  create: string;
  closeCreate: string;
  username: string;
  displayName: string;
  password: string;
  passwordHint: string;
  enabled: string;
  disabled: string;
  roles: string;
  noRoles: string;
  createAction: string;
  creating: string;
  cancel: string;
  edit: string;
  save: string;
  reviewChanges: string;
  saving: string;
  confirmSave: string;
  resetPassword: string;
  newPassword: string;
  resetAction: string;
  resetting: string;
  confirmReset: string;
  none: string;
  noMatches: string;
  saved: string;
  createdSuccess: string;
  passwordSuccess: string;
  sessionPolicyTitle: string;
  sessionNote: string;
  sessionDurationTitle: string;
  sessionDurationDescription: string;
  sessionDurationLabel: string;
  sessionDurationMinutes: string;
  sessionDurationRange: string;
  sessionDurationCurrent: string;
  sessionDurationBehavior: string;
  sessionDurationSave: string;
  sessionDurationSaving: string;
  sessionDurationSaved: string;
  sessionDurationLoadError: string;
  sessionDurationPersistenceWarning: string;
  currentAccount: string;
  currentAccountWarning: string;
  sessionExpires: string;
  unknownError: string;
  validationError: string;
  conflictError: string;
  notFoundError: string;
  actionForbidden: string;
  sessionExpired: string;
  created: string;
  updated: string;
  search: string;
  searchPlaceholder: string;
  statusFilter: string;
  allStatuses: string;
  enabledOnly: string;
  disabledOnly: string;
  usersCount: string;
  enabledCount: string;
  disabledCount: string;
  rolesCount: string;
  details: string;
  rolesAssigned: string;
  noAssignedRoles: string;
  changesTitle: string;
  changesSessionImpact: string;
  changeDisplayName: string;
  changeStatus: string;
  changeRoles: string;
  disableWarning: string;
  passwordImpact: string;
  individualSessionsUnavailable: string;
};

const strings: Record<EngineeringLocale, UiStrings> = {
  'pt-BR': {
    eyebrow: 'Identidades locais',
    title: 'Administração',
    description: 'Gerencie contas locais e suas chaves de papéis. A autorização continua sendo aplicada pelo backend e pela política ativa.',
    unauthorized: 'Sua sessão não é mais válida. Entre novamente para continuar.',
    notAuthorized: 'Sua conta não possui UserRoleAdmin ou SystemAdmin para administrar usuários locais.',
    loadError: 'Não foi possível carregar a administração local.',
    retry: 'Tentar novamente',
    refresh: 'Atualizar',
    refreshing: 'Atualizando…',
    create: 'Novo usuário',
    closeCreate: 'Fechar criação',
    username: 'Usuário',
    displayName: 'Nome de exibição',
    password: 'Senha inicial',
    passwordHint: 'Mínimo de 8 caracteres. A senha nunca é retornada pela API.',
    enabled: 'Habilitado',
    disabled: 'Desabilitado',
    roles: 'Papéis',
    noRoles: 'Nenhum papel definido no Engineering Workspace.',
    createAction: 'Criar usuário',
    creating: 'Criando…',
    cancel: 'Cancelar',
    edit: 'Conta selecionada',
    save: 'Salvar alterações',
    reviewChanges: 'Revisar alterações',
    saving: 'Salvando…',
    confirmSave: 'Confirmar e invalidar sessões anteriores',
    resetPassword: 'Redefinir senha',
    newPassword: 'Nova senha',
    resetAction: 'Revisar redefinição',
    resetting: 'Redefinindo…',
    confirmReset: 'Confirmar nova senha',
    none: 'Nenhum usuário local cadastrado.',
    noMatches: 'Nenhum usuário corresponde aos filtros atuais.',
    saved: 'Usuário atualizado. Sessões locais anteriores dessa conta foram invalidadas.',
    createdSuccess: 'Usuário criado.',
    passwordSuccess: 'Senha redefinida. Sessões locais anteriores dessa conta foram invalidadas.',
    sessionPolicyTitle: 'Consequência de sessão',
    sessionNote: 'Salvar nome, estado ou papéis e redefinir senha invalida os JWTs locais anteriores da conta alterada e encerra conexões realtime dessa identidade.',
    sessionDurationTitle: 'Tempo para pedir login novamente',
    sessionDurationDescription: 'Define por quanto tempo um login local pode permanecer válido.',
    sessionDurationLabel: 'Duração da sessão',
    sessionDurationMinutes: 'minutos',
    sessionDurationRange: 'Escolha de 5 a 1.440 minutos (até 24 horas).',
    sessionDurationCurrent: 'Equivale a',
    sessionDurationBehavior: 'O prazo começa quando a pessoa entra. Fechar o navegador não encerra a sessão. Ao vencer, será preciso entrar novamente. O novo prazo vale para próximos logins; sessões abertas mantêm o vencimento que já receberam.',
    sessionDurationSave: 'Salvar prazo',
    sessionDurationSaving: 'Salvando…',
    sessionDurationSaved: 'Prazo de login atualizado.',
    sessionDurationLoadError: 'Não foi possível carregar o prazo de login.',
    sessionDurationPersistenceWarning: 'Este ambiente não usa banco persistente. O ajuste se perde se a API reiniciar.',
    currentAccount: 'Sua conta atual',
    currentAccountWarning: 'Você está alterando a conta usada neste navegador. A confirmação pode invalidar esta sessão imediatamente.',
    sessionExpires: 'Sessão atual expira',
    unknownError: 'Falha na operação.',
    validationError: 'Os dados enviados não foram aceitos.',
    conflictError: 'A operação conflita com o estado atual.',
    notFoundError: 'A conta não existe mais. Atualize a lista.',
    actionForbidden: 'Sua identidade está autenticada, mas não tem autoridade para esta operação.',
    sessionExpired: 'Sua sessão expirou ou foi invalidada. Entre novamente.',
    created: 'Criado',
    updated: 'Atualizado',
    search: 'Buscar usuários',
    searchPlaceholder: 'Nome, usuário ou papel…',
    statusFilter: 'Estado da conta',
    allStatuses: 'Todos',
    enabledOnly: 'Somente habilitados',
    disabledOnly: 'Somente desabilitados',
    usersCount: 'Usuários',
    enabledCount: 'Habilitados',
    disabledCount: 'Desabilitados',
    rolesCount: 'Papéis disponíveis',
    details: 'Detalhes',
    rolesAssigned: 'Papéis atribuídos',
    noAssignedRoles: 'Nenhum papel atribuído',
    changesTitle: 'Confirme o impacto desta alteração',
    changesSessionImpact: 'O backend incrementará a versão de segurança desta conta. Tokens locais emitidos anteriormente deixam de ser válidos.',
    changeDisplayName: 'Nome de exibição será alterado',
    changeStatus: 'Estado habilitado/desabilitado será alterado',
    changeRoles: 'Papéis atribuídos serão alterados',
    disableWarning: 'A conta ficará desabilitada e não poderá efetuar novo login local.',
    passwordImpact: 'A senha será substituída e todas as sessões locais anteriores desta conta serão invalidadas.',
    individualSessionsUnavailable: 'O contrato atual não enumera sessões individuais; a unidade de revogação disponível é a identidade da conta.'
  },
  en: {
    eyebrow: 'Local identities',
    title: 'Administration',
    description: 'Manage local accounts and their role keys. Authorization remains enforced by the backend and active policy.',
    unauthorized: 'Your session is no longer valid. Sign in again to continue.',
    notAuthorized: 'Your account does not have UserRoleAdmin or SystemAdmin to manage local users.',
    loadError: 'Local administration could not be loaded.',
    retry: 'Try again',
    refresh: 'Refresh',
    refreshing: 'Refreshing…',
    create: 'New user',
    closeCreate: 'Close creation',
    username: 'Username',
    displayName: 'Display name',
    password: 'Initial password',
    passwordHint: 'At least 8 characters. The password is never returned by the API.',
    enabled: 'Enabled',
    disabled: 'Disabled',
    roles: 'Roles',
    noRoles: 'No roles are defined in the Engineering Workspace.',
    createAction: 'Create user',
    creating: 'Creating…',
    cancel: 'Cancel',
    edit: 'Selected account',
    save: 'Save changes',
    reviewChanges: 'Review changes',
    saving: 'Saving…',
    confirmSave: 'Confirm and invalidate previous sessions',
    resetPassword: 'Reset password',
    newPassword: 'New password',
    resetAction: 'Review password reset',
    resetting: 'Resetting…',
    confirmReset: 'Confirm new password',
    none: 'No local users are configured.',
    noMatches: 'No users match the current filters.',
    saved: 'User updated. Previous local sessions for this account were invalidated.',
    createdSuccess: 'User created.',
    passwordSuccess: 'Password reset. Previous local sessions for this account were invalidated.',
    sessionPolicyTitle: 'Session consequence',
    sessionNote: 'Saving name, status or roles and resetting a password invalidates prior local JWTs for the changed account and closes realtime connections for that identity.',
    sessionDurationTitle: 'Ask for login again after',
    sessionDurationDescription: 'Sets how long a local login can remain valid.',
    sessionDurationLabel: 'Session duration',
    sessionDurationMinutes: 'minutes',
    sessionDurationRange: 'Choose between 5 and 1,440 minutes (up to 24 hours).',
    sessionDurationCurrent: 'Equivalent to',
    sessionDurationBehavior: 'The timer starts when the user signs in. Closing the browser does not end the session. After it expires, the user must sign in again. The new duration applies to future logins; open sessions keep their existing expiry.',
    sessionDurationSave: 'Save duration',
    sessionDurationSaving: 'Saving…',
    sessionDurationSaved: 'Login duration updated.',
    sessionDurationLoadError: 'Could not load the login duration.',
    sessionDurationPersistenceWarning: 'This environment has no persistent database. The setting is lost if the API restarts.',
    currentAccount: 'Your current account',
    currentAccountWarning: 'You are changing the account used by this browser. Confirmation may invalidate this session immediately.',
    sessionExpires: 'Current session expires',
    unknownError: 'Operation failed.',
    validationError: 'The submitted data was not accepted.',
    conflictError: 'The operation conflicts with the current state.',
    notFoundError: 'The account no longer exists. Refresh the list.',
    actionForbidden: 'Your identity is authenticated but does not have authority for this operation.',
    sessionExpired: 'Your session expired or was invalidated. Sign in again.',
    created: 'Created',
    updated: 'Updated',
    search: 'Search users',
    searchPlaceholder: 'Name, username or role…',
    statusFilter: 'Account status',
    allStatuses: 'All',
    enabledOnly: 'Enabled only',
    disabledOnly: 'Disabled only',
    usersCount: 'Users',
    enabledCount: 'Enabled',
    disabledCount: 'Disabled',
    rolesCount: 'Available roles',
    details: 'Details',
    rolesAssigned: 'Assigned roles',
    noAssignedRoles: 'No assigned roles',
    changesTitle: 'Confirm the impact of this change',
    changesSessionImpact: 'The backend will advance this account security version. Previously issued local tokens stop being valid.',
    changeDisplayName: 'Display name will change',
    changeStatus: 'Enabled/disabled status will change',
    changeRoles: 'Assigned roles will change',
    disableWarning: 'The account will be disabled and cannot perform a new local login.',
    passwordImpact: 'The password will be replaced and all previous local sessions for this account will be invalidated.',
    individualSessionsUnavailable: 'The current contract does not enumerate individual sessions; the available revocation unit is the account identity.'
  },
  es: {
    eyebrow: 'Identidades locales',
    title: 'Administración',
    description: 'Administre cuentas locales y sus claves de roles. La autorización continúa aplicada por el backend y la política activa.',
    unauthorized: 'Su sesión ya no es válida. Ingrese nuevamente para continuar.',
    notAuthorized: 'Su cuenta no posee UserRoleAdmin o SystemAdmin para administrar usuarios locales.',
    loadError: 'No fue posible cargar la administración local.',
    retry: 'Intentar nuevamente',
    refresh: 'Actualizar',
    refreshing: 'Actualizando…',
    create: 'Nuevo usuario',
    closeCreate: 'Cerrar creación',
    username: 'Usuario',
    displayName: 'Nombre para mostrar',
    password: 'Contraseña inicial',
    passwordHint: 'Mínimo de 8 caracteres. La contraseña nunca es devuelta por la API.',
    enabled: 'Habilitado',
    disabled: 'Deshabilitado',
    roles: 'Roles',
    noRoles: 'No hay roles definidos en el Engineering Workspace.',
    createAction: 'Crear usuario',
    creating: 'Creando…',
    cancel: 'Cancelar',
    edit: 'Cuenta seleccionada',
    save: 'Guardar cambios',
    reviewChanges: 'Revisar cambios',
    saving: 'Guardando…',
    confirmSave: 'Confirmar e invalidar sesiones anteriores',
    resetPassword: 'Restablecer contraseña',
    newPassword: 'Nueva contraseña',
    resetAction: 'Revisar restablecimiento',
    resetting: 'Restableciendo…',
    confirmReset: 'Confirmar nueva contraseña',
    none: 'No hay usuarios locales registrados.',
    noMatches: 'Ningún usuario coincide con los filtros actuales.',
    saved: 'Usuario actualizado. Las sesiones locales anteriores de esta cuenta fueron invalidadas.',
    createdSuccess: 'Usuario creado.',
    passwordSuccess: 'Contraseña restablecida. Las sesiones locales anteriores de esta cuenta fueron invalidadas.',
    sessionPolicyTitle: 'Consecuencia de sesión',
    sessionNote: 'Guardar nombre, estado o roles y restablecer la contraseña invalida los JWT locales anteriores de la cuenta modificada y cierra conexiones realtime de esa identidad.',
    sessionDurationTitle: 'Pedir ingreso nuevamente después de',
    sessionDurationDescription: 'Define cuánto tiempo puede seguir válido un ingreso local.',
    sessionDurationLabel: 'Duración de la sesión',
    sessionDurationMinutes: 'minutos',
    sessionDurationRange: 'Elija entre 5 y 1.440 minutos (hasta 24 horas).',
    sessionDurationCurrent: 'Equivale a',
    sessionDurationBehavior: 'El plazo comienza al ingresar. Cerrar el navegador no termina la sesión. Al vencer, será necesario ingresar nuevamente. El nuevo plazo se aplica a futuros ingresos; las sesiones abiertas conservan su vencimiento actual.',
    sessionDurationSave: 'Guardar plazo',
    sessionDurationSaving: 'Guardando…',
    sessionDurationSaved: 'Duración de ingreso actualizada.',
    sessionDurationLoadError: 'No fue posible cargar la duración de ingreso.',
    sessionDurationPersistenceWarning: 'Este entorno no usa una base de datos persistente. El ajuste se pierde si se reinicia la API.',
    currentAccount: 'Su cuenta actual',
    currentAccountWarning: 'Está modificando la cuenta usada por este navegador. La confirmación puede invalidar esta sesión inmediatamente.',
    sessionExpires: 'La sesión actual vence',
    unknownError: 'La operación falló.',
    validationError: 'Los datos enviados no fueron aceptados.',
    conflictError: 'La operación entra en conflicto con el estado actual.',
    notFoundError: 'La cuenta ya no existe. Actualice la lista.',
    actionForbidden: 'Su identidad está autenticada pero no posee autoridad para esta operación.',
    sessionExpired: 'Su sesión expiró o fue invalidada. Ingrese nuevamente.',
    created: 'Creado',
    updated: 'Actualizado',
    search: 'Buscar usuarios',
    searchPlaceholder: 'Nombre, usuario o rol…',
    statusFilter: 'Estado de la cuenta',
    allStatuses: 'Todos',
    enabledOnly: 'Solo habilitados',
    disabledOnly: 'Solo deshabilitados',
    usersCount: 'Usuarios',
    enabledCount: 'Habilitados',
    disabledCount: 'Deshabilitados',
    rolesCount: 'Roles disponibles',
    details: 'Detalles',
    rolesAssigned: 'Roles asignados',
    noAssignedRoles: 'Sin roles asignados',
    changesTitle: 'Confirme el impacto de este cambio',
    changesSessionImpact: 'El backend avanzará la versión de seguridad de esta cuenta. Los tokens locales emitidos anteriormente dejarán de ser válidos.',
    changeDisplayName: 'El nombre para mostrar cambiará',
    changeStatus: 'El estado habilitado/deshabilitado cambiará',
    changeRoles: 'Los roles asignados cambiarán',
    disableWarning: 'La cuenta quedará deshabilitada y no podrá iniciar una nueva sesión local.',
    passwordImpact: 'La contraseña será reemplazada y todas las sesiones locales anteriores de esta cuenta serán invalidadas.',
    individualSessionsUnavailable: 'El contrato actual no enumera sesiones individuales; la unidad de revocación disponible es la identidad de la cuenta.'
  }
};

function formatSessionDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  if (hours > 0 && remainingMinutes > 0) return `${hours} h ${remainingMinutes} min`;
  if (hours > 0) return `${hours} h`;
  return `${minutes} min`;
}

export function UserAdministration({ locale }: { locale: EngineeringLocale }) {
  const s = strings[locale];
  const { profile } = useAuth();
  const [users, setUsers] = useState<LocalUser[]>([]);
  const [roles, setRoles] = useState<LocalRole[]>([]);
  const [sessionPolicy, setSessionPolicy] = useState<LocalAuthenticationSessionPolicy | null>(null);
  const [sessionMinutesDraft, setSessionMinutesDraft] = useState('');
  const [sessionPolicyLoading, setSessionPolicyLoading] = useState(true);
  const [sessionPolicySaving, setSessionPolicySaving] = useState(false);
  const [sessionPolicyError, setSessionPolicyError] = useState<string | null>(null);
  const observedSessionPolicyVersion = useRef<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [accessFailure, setAccessFailure] = useState<AccessFailure>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [operationError, setOperationError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<UserStatusFilter>('all');
  const [createOpen, setCreateOpen] = useState(false);

  const load = useCallback(async (preferredId?: string, background = false) => {
    if (background) setRefreshing(true);
    else setLoading(true);
    setLoadError(null);
    setAccessFailure(null);

    try {
      const [nextUsers, nextRoles] = await Promise.all([
        localUserAdministrationApi.listUsers(),
        localUserAdministrationApi.listRoles()
      ]);
      setUsers(nextUsers);
      setRoles(nextRoles);
      setSelectedId(current => {
        if (preferredId && nextUsers.some(user => user.id === preferredId)) return preferredId;
        if (current && nextUsers.some(user => user.id === current)) return current;
        return nextUsers[0]?.id ?? null;
      });
    } catch (reason) {
      if (reason instanceof AdministrationHttpError && reason.status === 401) {
        setAccessFailure('unauthorized');
      } else if (reason instanceof AdministrationHttpError && reason.status === 403) {
        setAccessFailure('forbidden');
      } else {
        setLoadError(administrationErrorMessage(reason, s));
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [s]);

  const loadSessionPolicy = useCallback(async () => {
    setSessionPolicyLoading(true);
    setSessionPolicyError(null);
    try {
      const next = await localUserAdministrationApi.getSessionPolicy();
      if (observedSessionPolicyVersion.current !== next.version) {
        setSessionMinutesDraft(String(next.accessTokenMinutes));
        observedSessionPolicyVersion.current = next.version;
      }
      setSessionPolicy(next);
    } catch (reason) {
      if (reason instanceof AdministrationHttpError && reason.status === 401) {
        setAccessFailure('unauthorized');
      } else if (reason instanceof AdministrationHttpError && reason.status === 403) {
        setAccessFailure('forbidden');
      } else {
        setSessionPolicyError(administrationErrorMessage(reason, s));
      }
    } finally {
      setSessionPolicyLoading(false);
    }
  }, [s]);

  useEffect(() => {
    void load();
    void loadSessionPolicy();
  }, [load, loadSessionPolicy]);

  const selected = useMemo(
    () => users.find(user => user.id === selectedId) ?? null,
    [selectedId, users]
  );
  const filteredUsers = useMemo(
    () => filterAdministrationUsers(users, query, statusFilter),
    [users, query, statusFilter]
  );
  const counts = useMemo(() => countAdministrationUsers(users), [users]);
  const parsedSessionMinutes = Number(sessionMinutesDraft);
  const validSessionMinutes = Number.isInteger(parsedSessionMinutes) && parsedSessionMinutes >= 5 && parsedSessionMinutes <= 1440;
  const sessionPolicyChanged = sessionPolicy !== null && parsedSessionMinutes !== sessionPolicy.accessTokenMinutes;

  if (loading) {
    return <section className="eng-panel user-admin"><div className="user-admin-state">{s.title}…</div></section>;
  }

  if (accessFailure) {
    return (
      <section className="eng-panel user-admin">
        <header><span>{s.eyebrow}</span><h2>{s.title}</h2></header>
        <div className="user-admin-state warning" role="alert">
          {accessFailure === 'unauthorized' ? s.unauthorized : s.notAuthorized}
        </div>
      </section>
    );
  }

  if (loadError) {
    return (
      <section className="eng-panel user-admin">
        <header><span>{s.eyebrow}</span><h2>{s.title}</h2></header>
        <div className="user-admin-state error" role="alert">{s.loadError}<small>{loadError}</small></div>
        <button type="button" className="user-admin-button" onClick={() => void load()}>{s.retry}</button>
      </section>
    );
  }

  const refreshAfter = async (message: string, preferredId?: string) => {
    setOperationError(null);
    setNotice(message);
    await load(preferredId, true);
  };

  const reportOperationError = (reason: unknown) => {
    setNotice(null);
    setOperationError(administrationErrorMessage(reason, s));
  };

  const saveSessionPolicy = async () => {
    if (!sessionPolicy || !validSessionMinutes || !sessionPolicyChanged) return;
    setSessionPolicySaving(true);
    setSessionPolicyError(null);
    setOperationError(null);
    setNotice(null);
    try {
      const updated = await localUserAdministrationApi.updateSessionPolicy({
        expectedVersion: sessionPolicy.version,
        accessTokenMinutes: parsedSessionMinutes
      });
      setSessionPolicy(updated);
      setSessionMinutesDraft(String(updated.accessTokenMinutes));
      observedSessionPolicyVersion.current = updated.version;
      setNotice(s.sessionDurationSaved);
    } catch (reason) {
      if (reason instanceof AdministrationHttpError && reason.status === 409) {
        await loadSessionPolicy();
      }
      reportOperationError(reason);
    } finally {
      setSessionPolicySaving(false);
    }
  };

  return (
    <section className="eng-panel user-admin" data-testid="user-administration">
      <header className="user-admin-header">
        <div>
          <span>{s.eyebrow}</span>
          <h2>{s.title}</h2>
          <p>{s.description}</p>
          <a href="/help?topic=security.local-accounts">{locale === 'pt-BR' ? 'Como cadastrar e administrar usuários' : locale === 'es' ? 'Cómo crear y administrar usuarios' : 'How to create and administer users'}</a>
        </div>
        <div className="user-admin-header-actions">
          <button
            type="button"
            className="user-admin-button secondary"
            disabled={refreshing}
            onClick={() => {
              void load(undefined, true);
              void loadSessionPolicy();
            }}
          >
            {refreshing ? s.refreshing : s.refresh}
          </button>
          <button
            type="button"
            className="user-admin-button primary"
            aria-expanded={createOpen}
            onClick={() => setCreateOpen(open => !open)}
            data-testid="admin-create-toggle"
          >
            {createOpen ? s.closeCreate : s.create}
          </button>
        </div>
      </header>

      <div className="user-admin-summary" data-testid="admin-summary">
        <SummaryCard label={s.usersCount} value={counts.total} />
        <SummaryCard label={s.enabledCount} value={counts.enabled} tone="good" />
        <SummaryCard label={s.disabledCount} value={counts.disabled} tone={counts.disabled > 0 ? 'muted' : undefined} />
        <SummaryCard label={s.rolesCount} value={roles.length} />
      </div>

      <div className="user-admin-session-policy">
        <div>
          <strong>{s.sessionPolicyTitle}</strong>
          <span>{s.sessionNote}</span>
        </div>
        <small>{s.individualSessionsUnavailable}</small>
      </div>

      <section className="user-admin-session-config" data-testid="local-session-duration">
        <div className="user-admin-session-config-heading">
          <strong>{s.sessionDurationTitle}</strong>
          <span>{s.sessionDurationDescription}</span>
        </div>
        {sessionPolicyLoading && <small>{s.refreshing}</small>}
        {sessionPolicyError && <div className="user-admin-state error" role="alert">{s.sessionDurationLoadError}<small>{sessionPolicyError}</small></div>}
        {sessionPolicy && (
          <>
            <div className="user-admin-session-config-controls">
              <label htmlFor="local-session-duration-minutes">
                <span>{s.sessionDurationLabel}</span>
                <div className="user-admin-session-duration-input">
                  <input
                    id="local-session-duration-minutes"
                    type="number"
                    min={5}
                    max={1440}
                    step={1}
                    inputMode="numeric"
                    value={sessionMinutesDraft}
                    onChange={event => setSessionMinutesDraft(event.target.value)}
                    disabled={sessionPolicyLoading || sessionPolicySaving}
                    aria-describedby="local-session-duration-help"
                    data-testid="local-session-duration-input"
                  />
                  <span>{s.sessionDurationMinutes}</span>
                </div>
              </label>
              <button
                type="button"
                className="user-admin-button primary"
                disabled={sessionPolicySaving || sessionPolicyLoading || !validSessionMinutes || !sessionPolicyChanged}
                onClick={() => void saveSessionPolicy()}
                data-testid="local-session-duration-save"
              >
                {sessionPolicySaving ? s.sessionDurationSaving : s.sessionDurationSave}
              </button>
            </div>
            <small id="local-session-duration-help">{s.sessionDurationRange}</small>
            <small>{s.sessionDurationCurrent} {formatSessionDuration(sessionPolicy.accessTokenMinutes)}</small>
            <p>{s.sessionDurationBehavior}</p>
            {!sessionPolicy.persistent && <div className="user-admin-state warning" role="status">{s.sessionDurationPersistenceWarning}</div>}
          </>
        )}
      </section>

      {notice && <div className="user-admin-notice" role="status">{notice}</div>}
      {operationError && <div className="user-admin-state error" role="alert">{operationError}</div>}

      {createOpen && (
        <CreateUserForm
          roles={roles}
          s={s}
          onCreated={user => {
            setCreateOpen(false);
            return refreshAfter(s.createdSuccess, user.id);
          }}
          onCancel={() => setCreateOpen(false)}
          onError={reportOperationError}
        />
      )}

      <div className="user-admin-toolbar">
        <label className="user-admin-search">
          <span>{s.search}</span>
          <input
            type="search"
            value={query}
            placeholder={s.searchPlaceholder}
            onChange={event => setQuery(event.target.value)}
            data-testid="admin-search"
          />
        </label>
        <label className="user-admin-filter">
          <span>{s.statusFilter}</span>
          <select
            value={statusFilter}
            onChange={event => setStatusFilter(event.target.value as UserStatusFilter)}
            data-testid="admin-status-filter"
          >
            <option value="all">{s.allStatuses}</option>
            <option value="enabled">{s.enabledOnly}</option>
            <option value="disabled">{s.disabledOnly}</option>
          </select>
        </label>
      </div>

      <div className="user-admin-grid">
        <div className="user-admin-column user-admin-list-column">
          <div className="user-list" data-testid="user-list" aria-label={s.usersCount}>
            {users.length === 0 && <div className="user-admin-state">{s.none}</div>}
            {users.length > 0 && filteredUsers.length === 0 && <div className="user-admin-state">{s.noMatches}</div>}
            {filteredUsers.map(user => {
              const isCurrent = profile?.subjectId === user.id;
              return (
                <button
                  type="button"
                  className={`user-row ${selectedId === user.id ? 'selected' : ''}`}
                  key={user.id}
                  aria-pressed={selectedId === user.id}
                  onClick={() => setSelectedId(user.id)}
                >
                  <span className={`user-status ${user.isEnabled ? 'enabled' : 'disabled'}`} aria-hidden="true" />
                  <span className="user-row-main">
                    <strong>{user.displayName}</strong>
                    <small>{user.username}</small>
                  </span>
                  <span className="user-row-context">
                    {isCurrent && <em>{s.currentAccount}</em>}
                    <span className={`user-account-state ${user.isEnabled ? 'enabled' : 'disabled'}`}>
                      {user.isEnabled ? s.enabled : s.disabled}
                    </span>
                    <small>{user.roles.join(', ') || s.noAssignedRoles}</small>
                  </span>
                </button>
              );
            })}
          </div>
        </div>

        <div className="user-admin-column user-admin-detail-column">
          {selected ? (
            <EditUserForm
              key={`${selected.id}:${selected.updatedAtUtc}`}
              user={selected}
              roles={roles}
              locale={locale}
              s={s}
              isCurrentUser={profile?.subjectId === selected.id}
              currentSessionExpiresAt={profile?.subjectId === selected.id ? profile.expiresAtUtc : undefined}
              onSaved={() => refreshAfter(s.saved, selected.id)}
              onPasswordReset={() => refreshAfter(s.passwordSuccess, selected.id)}
              onError={reportOperationError}
            />
          ) : (
            <div className="user-admin-state">{s.none}</div>
          )}
        </div>
      </div>

      <AuthorityPolicyAdministration
        locale={locale}
        users={users}
        selectedUser={selected}
        onPolicyApplied={() => load(selected?.id, true)}
      />
    </section>
  );
}

function SummaryCard({
  label,
  value,
  tone
}: {
  label: string;
  value: number;
  tone?: 'good' | 'muted';
}) {
  return (
    <div className={`user-admin-summary-card ${tone ? `is-${tone}` : ''}`}>
      <strong>{value}</strong>
      <span>{label}</span>
    </div>
  );
}

function CreateUserForm({
  roles,
  s,
  onCreated,
  onCancel,
  onError
}: {
  roles: LocalRole[];
  s: UiStrings;
  onCreated: (user: LocalUser) => Promise<void>;
  onCancel: () => void;
  onError: (reason: unknown) => void;
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
      const created = await localUserAdministrationApi.createUser({
        username,
        displayName,
        password,
        roles: selectedRoles,
        isEnabled: enabled
      });
      setPassword('');
      await onCreated(created);
    } catch (reason) {
      onError(reason);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="user-admin-form user-admin-create" onSubmit={event => void submit(event)} data-testid="create-user-form">
      <div className="user-admin-form-heading">
        <div><span>{s.create}</span><h3>{s.createAction}</h3></div>
        <button type="button" className="user-admin-button secondary" onClick={onCancel}>{s.cancel}</button>
      </div>
      <div className="user-admin-form-grid">
        <label>{s.username}<input name="new-username" value={username} minLength={3} required autoComplete="off" onChange={e => setUsername(e.target.value)} /></label>
        <label>{s.displayName}<input name="new-display-name" value={displayName} maxLength={300} required onChange={e => setDisplayName(e.target.value)} /></label>
        <label className="user-admin-form-wide">{s.password}<input name="new-password" type="password" value={password} minLength={8} required autoComplete="new-password" onChange={e => setPassword(e.target.value)} /><small>{s.passwordHint}</small></label>
      </div>
      <label className="user-admin-toggle"><input type="checkbox" checked={enabled} onChange={e => setEnabled(e.target.checked)} />{s.enabled}</label>
      <RolePicker roles={roles} selected={selectedRoles} onChange={setSelectedRoles} label={s.roles} empty={s.noRoles} />
      <div className="user-admin-form-actions">
        <button className="user-admin-button primary" type="submit" disabled={busy}>{busy ? s.creating : s.createAction}</button>
      </div>
    </form>
  );
}

function EditUserForm({
  user,
  roles,
  locale,
  s,
  isCurrentUser,
  currentSessionExpiresAt,
  onSaved,
  onPasswordReset,
  onError
}: {
  user: LocalUser;
  roles: LocalRole[];
  locale: EngineeringLocale;
  s: UiStrings;
  isCurrentUser: boolean;
  currentSessionExpiresAt?: string;
  onSaved: () => Promise<void>;
  onPasswordReset: () => Promise<void>;
  onError: (reason: unknown) => void;
}) {
  const [displayName, setDisplayName] = useState(user.displayName);
  const [enabled, setEnabled] = useState(user.isEnabled);
  const [selectedRoles, setSelectedRoles] = useState<string[]>(user.roles);
  const [newPassword, setNewPassword] = useState('');
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [reviewSave, setReviewSave] = useState(false);
  const [reviewPassword, setReviewPassword] = useState(false);

  const changes = useMemo(() => summarizeUserChanges(user, {
    displayName,
    isEnabled: enabled,
    roles: selectedRoles
  }), [displayName, enabled, selectedRoles, user]);

  const markChanged = () => setReviewSave(false);

  const review = (event: FormEvent) => {
    event.preventDefault();
    if (changes.length === 0) return;
    setReviewSave(true);
  };

  const save = async () => {
    if (changes.length === 0 || saving) return;
    setSaving(true);
    try {
      await localUserAdministrationApi.updateUser(user.id, {
        displayName,
        isEnabled: enabled,
        roles: selectedRoles
      });
      setReviewSave(false);
      await onSaved();
    } catch (reason) {
      onError(reason);
    } finally {
      setSaving(false);
    }
  };

  const reviewReset = (event: FormEvent) => {
    event.preventDefault();
    if (newPassword.length < 8) return;
    setReviewPassword(true);
  };

  const resetPassword = async () => {
    if (!reviewPassword || resetting) return;
    setResetting(true);
    try {
      await localUserAdministrationApi.resetPassword(user.id, newPassword);
      setNewPassword('');
      setReviewPassword(false);
      await onPasswordReset();
    } catch (reason) {
      onError(reason);
    } finally {
      setResetting(false);
    }
  };

  return (
    <div className="user-edit" data-testid="edit-user-form">
      <div className="user-admin-detail-header">
        <div>
          <span>{s.edit}</span>
          <h3>{user.displayName}</h3>
          <small>{user.username}</small>
        </div>
        <span className={`user-account-state ${user.isEnabled ? 'enabled' : 'disabled'}`}>
          {user.isEnabled ? s.enabled : s.disabled}
        </span>
      </div>

      {isCurrentUser && (
        <div className="user-admin-current-session" data-testid="current-admin-account">
          <strong>{s.currentAccount}</strong>
          <span>{s.currentAccountWarning}</span>
          {currentSessionExpiresAt && <small>{s.sessionExpires}: {formatDate(currentSessionExpiresAt, locale)}</small>}
        </div>
      )}

      <div className="user-admin-role-summary">
        <span>{s.rolesAssigned}</span>
        <div>
          {user.roles.length > 0
            ? user.roles.map(role => <code key={role}>{role}</code>)
            : <small>{s.noAssignedRoles}</small>}
        </div>
      </div>

      <form className="user-admin-form" onSubmit={review}>
        <h3>{s.details}</h3>
        <div className="user-admin-id"><strong>{user.username}</strong><small>{user.id}</small></div>
        <label>{s.displayName}<input name="edit-display-name" value={displayName} maxLength={300} required onChange={e => { setDisplayName(e.target.value); markChanged(); }} /></label>
        <label className="user-admin-toggle user-admin-toggle-card">
          <input name="edit-enabled" type="checkbox" checked={enabled} onChange={e => { setEnabled(e.target.checked); markChanged(); }} />
          <span><strong>{enabled ? s.enabled : s.disabled}</strong><small>{s.enabled}</small></span>
        </label>
        <RolePicker roles={roles} selected={selectedRoles} onChange={next => { setSelectedRoles(next); markChanged(); }} label={s.roles} empty={s.noRoles} />
        <div className="user-admin-meta"><span>{s.created}: {formatDate(user.createdAtUtc, locale)}</span><span>{s.updated}: {formatDate(user.updatedAtUtc, locale)}</span></div>
        <button className="user-admin-button primary" type="submit" disabled={changes.length === 0 || saving} data-testid="review-user-changes">
          {saving ? s.saving : changes.length > 0 ? s.reviewChanges : s.save}
        </button>

        {reviewSave && (
          <ConfirmationPanel
            title={s.changesTitle}
            message={s.changesSessionImpact}
            items={changes.map(change => changeLabel(change, s))}
            warning={!enabled ? s.disableWarning : isCurrentUser ? s.currentAccountWarning : undefined}
            confirmLabel={saving ? s.saving : s.confirmSave}
            cancelLabel={s.cancel}
            busy={saving}
            onConfirm={() => void save()}
            onCancel={() => setReviewSave(false)}
            testId="confirm-user-changes"
          />
        )}
      </form>

      <form className="user-admin-form password-reset" onSubmit={reviewReset}>
        <div className="user-admin-form-heading">
          <div><span>{s.sessionPolicyTitle}</span><h3>{s.resetPassword}</h3></div>
        </div>
        <label>{s.newPassword}<input name="reset-password" type="password" value={newPassword} minLength={8} required autoComplete="new-password" onChange={e => { setNewPassword(e.target.value); setReviewPassword(false); }} /><small>{s.passwordHint}</small></label>
        <button className="user-admin-button" type="submit" disabled={resetting}>{resetting ? s.resetting : s.resetAction}</button>

        {reviewPassword && (
          <ConfirmationPanel
            title={s.resetPassword}
            message={s.passwordImpact}
            warning={isCurrentUser ? s.currentAccountWarning : undefined}
            confirmLabel={resetting ? s.resetting : s.confirmReset}
            cancelLabel={s.cancel}
            busy={resetting}
            onConfirm={() => void resetPassword()}
            onCancel={() => setReviewPassword(false)}
            testId="confirm-password-reset"
          />
        )}
      </form>
    </div>
  );
}

function ConfirmationPanel({
  title,
  message,
  items = [],
  warning,
  confirmLabel,
  cancelLabel,
  busy,
  onConfirm,
  onCancel,
  testId
}: {
  title: string;
  message: string;
  items?: string[];
  warning?: string;
  confirmLabel: string;
  cancelLabel: string;
  busy: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  testId: string;
}) {
  return (
    <div className="user-admin-confirmation" role="alert" data-testid={testId}>
      <strong>{title}</strong>
      <p>{message}</p>
      {items.length > 0 && <ul>{items.map(item => <li key={item}>{item}</li>)}</ul>}
      {warning && <p className="user-admin-confirmation-warning">{warning}</p>}
      <div className="user-admin-form-actions">
        <button type="button" className="user-admin-button secondary" disabled={busy} onClick={onCancel}>{cancelLabel}</button>
        <button type="button" className="user-admin-button danger" disabled={busy} onClick={onConfirm}>{confirmLabel}</button>
      </div>
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
        <label key={role.key}>
          <input
            type="checkbox"
            checked={selected.some(item => item.toLowerCase() === role.key.toLowerCase())}
            onChange={event => toggle(role.key, event.target.checked)}
          />
          <span>
            <strong>{role.name}</strong>
            <code>{role.key}</code>
            {role.description && <small>{role.description}</small>}
          </span>
        </label>
      ))}
    </fieldset>
  );
}

function changeLabel(change: UserChange, s: UiStrings) {
  if (change === 'displayName') return s.changeDisplayName;
  if (change === 'status') return s.changeStatus;
  return s.changeRoles;
}

function administrationErrorMessage(reason: unknown, s: UiStrings) {
  if (!(reason instanceof AdministrationHttpError)) {
    return reason instanceof Error && reason.message ? reason.message : s.unknownError;
  }

  let message: string;
  switch (reason.status) {
    case 400:
    case 422:
      message = s.validationError;
      break;
    case 401:
      message = s.sessionExpired;
      break;
    case 403:
      message = s.actionForbidden;
      break;
    case 404:
      message = s.notFoundError;
      break;
    case 409:
      message = s.conflictError;
      break;
    default:
      message = s.unknownError;
      break;
  }

  const details = [reason.message, reason.unknownRoles.length > 0 ? reason.unknownRoles.join(', ') : '']
    .filter(Boolean)
    .join(' ');
  return details ? `${message} ${details}` : message;
}

function formatDate(value: string, locale: EngineeringLocale) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(locale, {
    dateStyle: 'short',
    timeStyle: 'short'
  }).format(date);
}
