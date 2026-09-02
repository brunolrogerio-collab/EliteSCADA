import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import './auth.css';

type AuthConfiguration = {
  authenticationEnabled: boolean;
  localLoginEnabled: boolean;
  initialAdministratorRequired: boolean;
  initialAdministratorSetupAvailable: boolean;
  initialAdministratorBlockedReason?: string | null;
  passwordPolicy: {
    minimumLength: number;
    maximumLength: number;
  };
};

type PersistenceStatus = {
  enabled: boolean;
  hasProjects: boolean | null;
};

type LocalSessionStatus = {
  authenticated: boolean;
  username?: string | null;
};

export type AuthProfile = {
  subjectId: string;
  username?: string;
  displayName?: string;
  roles: string[];
  expiresAtUtc?: string;
  identityProvider?: string;
};

type AuthContextValue = {
  profile: AuthProfile | null;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue>({
  profile: null,
  logout: async () => undefined
});

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');
const localeKey = 'elitescada.engineering.locale';
type AuthLocale = 'pt-BR' | 'en' | 'es';

const messages = {
  'pt-BR': {
    title: 'EliteSCADA',
    subtitle: 'Acesso ao Runtime e Engineering',
    welcome: 'Bem-vindo ao EliteSCADA',
    firstRun: 'Crie o Administrador inicial para concluir a configuração segura deste servidor.',
    firstRunBlocked: 'A identidade local está vazia, mas o servidor não pode confirmar uma instalação realmente vazia. Por segurança, o bootstrap anônimo permanece fechado. Restaure um Administrador ou use a configuração explícita de bootstrap do servidor.',
    username: 'Usuário',
    displayName: 'Nome de exibição',
    password: 'Senha',
    confirmPassword: 'Confirmar senha',
    passwordHint: (minimum: number) => `Use pelo menos ${minimum} caracteres.`,
    passwordMismatch: 'As senhas não conferem.',
    createAdministrator: 'Criar Administrador',
    creatingAdministrator: 'Criando Administrador…',
    bootstrapClosed: 'O Administrador inicial já foi criado ou o bootstrap seguro não está mais disponível.',
    firstProjectTitle: 'Criar novo projeto',
    firstProject: 'Nenhum projeto persistido existe neste servidor. Crie o primeiro projeto para iniciar o Working no Engineering.',
    projectKey: 'Chave do projeto',
    projectName: 'Nome do projeto',
    projectKeyHint: 'Identificador estável, por exemplo planta-piloto.',
    createProject: 'Criar projeto e abrir Engineering',
    creatingProject: 'Criando projeto…',
    projectConflict: 'Outro projeto foi criado enquanto esta tela estava aberta. Recarregue o Engineering.',
    login: 'Entrar',
    signingIn: 'Entrando…',
    invalid: 'Usuário ou senha inválidos.',
    unavailable: 'Não foi possível acessar o serviço de autenticação.',
    external: 'Este servidor exige autenticação externa. O login local não está habilitado.',
    retry: 'Tentar novamente'
  },
  en: {
    title: 'EliteSCADA',
    subtitle: 'Runtime and Engineering access',
    welcome: 'Welcome to EliteSCADA',
    firstRun: 'Create the initial Administrator to complete the secure setup of this server.',
    firstRunBlocked: 'The local identity store is empty, but the server cannot confirm a truly empty installation. Anonymous bootstrap remains closed for safety. Restore an Administrator or use the server-side explicit bootstrap configuration.',
    username: 'Username',
    displayName: 'Display name',
    password: 'Password',
    confirmPassword: 'Confirm password',
    passwordHint: (minimum: number) => `Use at least ${minimum} characters.`,
    passwordMismatch: 'Passwords do not match.',
    createAdministrator: 'Create Administrator',
    creatingAdministrator: 'Creating Administrator…',
    bootstrapClosed: 'The initial Administrator already exists or secure bootstrap is no longer available.',
    firstProjectTitle: 'Create New Project',
    firstProject: 'No persisted project exists on this server. Create the first project to start a Working project in Engineering.',
    projectKey: 'Project key',
    projectName: 'Project name',
    projectKeyHint: 'Stable identifier, for example pilot-plant.',
    createProject: 'Create project and open Engineering',
    creatingProject: 'Creating project…',
    projectConflict: 'Another project was created while this screen was open. Reload Engineering.',
    login: 'Sign in',
    signingIn: 'Signing in…',
    invalid: 'Invalid username or password.',
    unavailable: 'The authentication service could not be reached.',
    external: 'This server requires external authentication. Local login is not enabled.',
    retry: 'Retry'
  },
  es: {
    title: 'EliteSCADA',
    subtitle: 'Acceso a Runtime y Engineering',
    welcome: 'Bienvenido a EliteSCADA',
    firstRun: 'Cree el Administrador inicial para completar la configuración segura de este servidor.',
    firstRunBlocked: 'El almacén de identidad local está vacío, pero el servidor no puede confirmar una instalación realmente vacía. Por seguridad, el bootstrap anónimo permanece cerrado. Restaure un Administrador o use la configuración explícita de bootstrap del servidor.',
    username: 'Usuario',
    displayName: 'Nombre para mostrar',
    password: 'Contraseña',
    confirmPassword: 'Confirmar contraseña',
    passwordHint: (minimum: number) => `Use al menos ${minimum} caracteres.`,
    passwordMismatch: 'Las contraseñas no coinciden.',
    createAdministrator: 'Crear Administrador',
    creatingAdministrator: 'Creando Administrador…',
    bootstrapClosed: 'El Administrador inicial ya existe o el bootstrap seguro ya no está disponible.',
    firstProjectTitle: 'Crear nuevo proyecto',
    firstProject: 'No existe ningún proyecto persistido en este servidor. Cree el primer proyecto para iniciar el Working en Engineering.',
    projectKey: 'Clave del proyecto',
    projectName: 'Nombre del proyecto',
    projectKeyHint: 'Identificador estable, por ejemplo planta-piloto.',
    createProject: 'Crear proyecto y abrir Engineering',
    creatingProject: 'Creando proyecto…',
    projectConflict: 'Otro proyecto fue creado mientras esta pantalla estaba abierta. Recargue Engineering.',
    login: 'Ingresar',
    signingIn: 'Ingresando…',
    invalid: 'Usuario o contraseña inválidos.',
    unavailable: 'No fue posible acceder al servicio de autenticación.',
    external: 'Este servidor requiere autenticación externa. El acceso local no está habilitado.',
    retry: 'Reintentar'
  }
} as const;

function resolveLocale(): AuthLocale {
  const stored = window.localStorage.getItem(localeKey);
  if (stored === 'pt-BR' || stored === 'en' || stored === 'es') return stored;
  const browser = navigator.language.toLowerCase();
  if (browser.startsWith('es')) return 'es';
  if (browser.startsWith('en')) return 'en';
  return 'pt-BR';
}

async function getConfiguration(): Promise<AuthConfiguration> {
  const response = await fetch(`${API}/api/auth/config`, { headers: { accept: 'application/json' } });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return await response.json() as AuthConfiguration;
}

async function getProfile(localLoginEnabled: boolean): Promise<AuthProfile | null> {
  const response = await fetch(`${API}/api/auth/me`, { headers: { accept: 'application/json' } });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  const profile = await response.json() as AuthProfile;
  if (!localLoginEnabled) return profile;

  const localSessionResponse = await fetch(`${API}/api/auth/local-session`, { headers: { accept: 'application/json' } });
  if (!localSessionResponse.ok)
    throw new Error(`${localSessionResponse.status} ${localSessionResponse.statusText}`);
  const localSession = await localSessionResponse.json() as LocalSessionStatus;
  return localSession.authenticated
    ? {
        ...profile,
        username: localSession.username ?? profile.username,
        identityProvider: 'local'
      }
    : profile;
}

async function needsFirstProject(): Promise<boolean> {
  const response = await fetch(`${API}/api/engineering/persistence/status`, { headers: { accept: 'application/json' } });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  const status = await response.json() as PersistenceStatus;
  return status.enabled && status.hasProjects === false;
}

async function responseError(response: Response): Promise<string | null> {
  try {
    const payload = await response.json() as { error?: string };
    return payload.error ?? null;
  } catch {
    return null;
  }
}

export function useAuth() {
  return useContext(AuthContext);
}

export function AuthGate({ children }: { children: React.ReactNode }) {
  const locale = useMemo(resolveLocale, []);
  const t = messages[locale];
  const [configuration, setConfiguration] = useState<AuthConfiguration | null>(null);
  const [profile, setProfile] = useState<AuthProfile | null>(null);
  const [checking, setChecking] = useState(true);
  const [checkingProject, setCheckingProject] = useState(false);
  const [projectSetupRequired, setProjectSetupRequired] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [projectKey, setProjectKey] = useState('');
  const [projectName, setProjectName] = useState('');
  const [signingIn, setSigningIn] = useState(false);
  const [creatingAdministrator, setCreatingAdministrator] = useState(false);
  const [creatingProject, setCreatingProject] = useState(false);
  const [invalid, setInvalid] = useState(false);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);
  const [projectError, setProjectError] = useState<string | null>(null);

  const acceptAuthenticatedProfile = useCallback(async (nextProfile: AuthProfile | null) => {
    setProfile(nextProfile);
    if (nextProfile?.identityProvider !== 'local') {
      setProjectSetupRequired(false);
      return;
    }

    setCheckingProject(true);
    try {
      setProjectSetupRequired(await needsFirstProject());
    } finally {
      setCheckingProject(false);
    }
  }, []);

  const check = useCallback(async () => {
    setChecking(true);
    setUnavailable(false);
    try {
      const config = await getConfiguration();
      setConfiguration(config);
      if (!config.authenticationEnabled) {
        await acceptAuthenticatedProfile(null);
        return;
      }
      await acceptAuthenticatedProfile(await getProfile(config.localLoginEnabled));
    } catch {
      setUnavailable(true);
    } finally {
      setChecking(false);
    }
  }, [acceptAuthenticatedProfile]);

  useEffect(() => { void check(); }, [check]);

  const bootstrap = async (event: React.FormEvent) => {
    event.preventDefault();
    const minimum = configuration?.passwordPolicy.minimumLength ?? 8;
    if (!username.trim() || password.length < minimum) return;
    if (password !== confirmPassword) {
      setBootstrapError(t.passwordMismatch);
      return;
    }

    setCreatingAdministrator(true);
    setBootstrapError(null);
    setUnavailable(false);
    try {
      const response = await fetch(`${API}/api/auth/bootstrap`, {
        method: 'POST',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json'
        },
        body: JSON.stringify({ username, displayName, password })
      });
      if (response.status === 409) {
        setBootstrapError(await responseError(response) ?? t.bootstrapClosed);
        await check();
        return;
      }
      if (response.status === 400) {
        setBootstrapError(await responseError(response) ?? t.unavailable);
        return;
      }
      if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);

      const created = await response.json() as AuthProfile;
      setConfiguration(current => current ? {
        ...current,
        initialAdministratorRequired: false,
        initialAdministratorSetupAvailable: false,
        initialAdministratorBlockedReason: null
      } : current);
      setPassword('');
      setConfirmPassword('');
      await acceptAuthenticatedProfile(created);
    } catch {
      setUnavailable(true);
    } finally {
      setCreatingAdministrator(false);
    }
  };

  const login = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!username.trim() || !password) return;
    setSigningIn(true);
    setInvalid(false);
    try {
      const response = await fetch(`${API}/api/auth/login`, {
        method: 'POST',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json'
        },
        body: JSON.stringify({ username, password })
      });
      if (response.status === 401) {
        setInvalid(true);
        return;
      }
      if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
      setPassword('');
      await acceptAuthenticatedProfile(await response.json() as AuthProfile);
    } catch {
      setUnavailable(true);
    } finally {
      setSigningIn(false);
    }
  };

  const createFirstProject = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!projectKey.trim() || !projectName.trim()) return;
    setCreatingProject(true);
    setProjectError(null);
    try {
      const response = await fetch(`${API}/api/engineering/persistence/projects/first`, {
        method: 'POST',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json'
        },
        body: JSON.stringify({ projectKey, projectName })
      });
      if (response.status === 409) {
        setProjectError(t.projectConflict);
        return;
      }
      if (response.status === 400 || response.status === 403) {
        setProjectError(await responseError(response) ?? t.unavailable);
        return;
      }
      if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
      setProjectSetupRequired(false);
    } catch {
      setUnavailable(true);
    } finally {
      setCreatingProject(false);
    }
  };

  const logout = useCallback(async () => {
    await fetch(`${API}/api/auth/logout`, { method: 'POST' });
    setProfile(null);
    setProjectSetupRequired(false);
  }, []);

  if (checking || checkingProject) {
    return <div className="auth-page"><div className="auth-card auth-loading"><strong>{t.title}</strong></div></div>;
  }

  if (unavailable) {
    return (
      <div className="auth-page">
        <div className="auth-card">
          <div className="auth-mark">E</div>
          <h1>{t.title}</h1>
          <p>{t.unavailable}</p>
          <button type="button" className="auth-primary" onClick={() => void check()}>{t.retry}</button>
        </div>
      </div>
    );
  }

  if (!configuration?.authenticationEnabled) {
    return <AuthContext.Provider value={{ profile, logout }}>{children}</AuthContext.Provider>;
  }

  if (profile && projectSetupRequired) {
    return (
      <div className="auth-page">
        <form className="auth-card auth-card--first-run" onSubmit={createFirstProject}>
          <div className="auth-mark">E</div>
          <h1>{t.firstProjectTitle}</h1>
          <p>{t.firstProject}</p>
          <label>
            <span>{t.projectKey}</span>
            <input
              name="project-key"
              autoFocus
              maxLength={200}
              value={projectKey}
              onChange={event => setProjectKey(event.target.value)}
            />
            <small className="auth-hint">{t.projectKeyHint}</small>
          </label>
          <label>
            <span>{t.projectName}</span>
            <input
              name="project-name"
              maxLength={300}
              value={projectName}
              onChange={event => setProjectName(event.target.value)}
            />
          </label>
          {projectError && <div className="auth-error" role="alert">{projectError}</div>}
          <button
            className="auth-primary"
            type="submit"
            disabled={creatingProject || !projectKey.trim() || !projectName.trim()}
          >
            {creatingProject ? t.creatingProject : t.createProject}
          </button>
        </form>
      </div>
    );
  }

  if (profile) {
    return <AuthContext.Provider value={{ profile, logout }}>{children}</AuthContext.Provider>;
  }

  if (!configuration.localLoginEnabled) {
    return (
      <div className="auth-page">
        <div className="auth-card">
          <div className="auth-mark">E</div>
          <h1>{t.title}</h1>
          <p>{t.external}</p>
        </div>
      </div>
    );
  }

  if (configuration.initialAdministratorRequired && !configuration.initialAdministratorSetupAvailable) {
    return (
      <div className="auth-page">
        <div className="auth-card auth-card--first-run">
          <div className="auth-mark">E</div>
          <h1>{t.title}</h1>
          <p role="alert">{t.firstRunBlocked}</p>
        </div>
      </div>
    );
  }

  if (configuration.initialAdministratorRequired) {
    const minimum = configuration.passwordPolicy.minimumLength;
    return (
      <div className="auth-page">
        <form className="auth-card auth-card--first-run" onSubmit={bootstrap}>
          <div className="auth-mark">E</div>
          <h1>{t.welcome}</h1>
          <p>{t.firstRun}</p>
          <label>
            <span>{t.username}</span>
            <input
              name="bootstrap-username"
              autoComplete="username"
              autoFocus
              value={username}
              onChange={event => setUsername(event.target.value)}
            />
          </label>
          <label>
            <span>{t.displayName}</span>
            <input
              name="bootstrap-display-name"
              autoComplete="name"
              value={displayName}
              onChange={event => setDisplayName(event.target.value)}
            />
          </label>
          <label>
            <span>{t.password}</span>
            <input
              name="bootstrap-password"
              type="password"
              minLength={minimum}
              maxLength={configuration.passwordPolicy.maximumLength}
              autoComplete="new-password"
              value={password}
              onChange={event => setPassword(event.target.value)}
            />
            <small className="auth-hint">{t.passwordHint(minimum)}</small>
          </label>
          <label>
            <span>{t.confirmPassword}</span>
            <input
              name="bootstrap-password-confirmation"
              type="password"
              minLength={minimum}
              maxLength={configuration.passwordPolicy.maximumLength}
              autoComplete="new-password"
              value={confirmPassword}
              onChange={event => setConfirmPassword(event.target.value)}
            />
          </label>
          {bootstrapError && <div className="auth-error" role="alert">{bootstrapError}</div>}
          <button
            className="auth-primary"
            type="submit"
            disabled={creatingAdministrator || !username.trim() || password.length < minimum || password !== confirmPassword}
          >
            {creatingAdministrator ? t.creatingAdministrator : t.createAdministrator}
          </button>
        </form>
      </div>
    );
  }

  return (
    <div className="auth-page">
      <form className="auth-card" onSubmit={login}>
        <div className="auth-mark">E</div>
        <h1>{t.title}</h1>
        <p>{t.subtitle}</p>
        <label>
          <span>{t.username}</span>
          <input
            name="username"
            autoComplete="username"
            autoFocus
            value={username}
            onChange={event => setUsername(event.target.value)}
          />
        </label>
        <label>
          <span>{t.password}</span>
          <input
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
          />
        </label>
        {invalid && <div className="auth-error" role="alert">{t.invalid}</div>}
        <button className="auth-primary" type="submit" disabled={signingIn || !username.trim() || !password}>
          {signingIn ? t.signingIn : t.login}
        </button>
      </form>
    </div>
  );
}
