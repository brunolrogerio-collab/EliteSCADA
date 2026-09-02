import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import './auth.css';

type AuthConfiguration = {
  authenticationEnabled: boolean;
  localLoginEnabled: boolean;
  initialAdministratorRequired: boolean;
  passwordPolicy: {
    minimumLength: number;
    maximumLength: number;
  };
};

export type AuthProfile = {
  subjectId: string;
  username?: string;
  displayName?: string;
  roles: string[];
  expiresAtUtc?: string;
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
    username: 'Usuário',
    displayName: 'Nome de exibição',
    password: 'Senha',
    confirmPassword: 'Confirmar senha',
    passwordHint: (minimum: number) => `Use pelo menos ${minimum} caracteres.`,
    passwordMismatch: 'As senhas não conferem.',
    createAdministrator: 'Criar Administrador',
    creatingAdministrator: 'Criando Administrador…',
    bootstrapClosed: 'O Administrador inicial já foi criado. Entre com uma conta existente.',
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
    username: 'Username',
    displayName: 'Display name',
    password: 'Password',
    confirmPassword: 'Confirm password',
    passwordHint: (minimum: number) => `Use at least ${minimum} characters.`,
    passwordMismatch: 'Passwords do not match.',
    createAdministrator: 'Create Administrator',
    creatingAdministrator: 'Creating Administrator…',
    bootstrapClosed: 'The initial Administrator has already been created. Sign in with an existing account.',
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
    username: 'Usuario',
    displayName: 'Nombre para mostrar',
    password: 'Contraseña',
    confirmPassword: 'Confirmar contraseña',
    passwordHint: (minimum: number) => `Use al menos ${minimum} caracteres.`,
    passwordMismatch: 'Las contraseñas no coinciden.',
    createAdministrator: 'Crear Administrador',
    creatingAdministrator: 'Creando Administrador…',
    bootstrapClosed: 'El Administrador inicial ya fue creado. Ingrese con una cuenta existente.',
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

async function getProfile(): Promise<AuthProfile | null> {
  const response = await fetch(`${API}/api/auth/me`, { headers: { accept: 'application/json' } });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return await response.json() as AuthProfile;
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
  const [unavailable, setUnavailable] = useState(false);
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [signingIn, setSigningIn] = useState(false);
  const [creatingAdministrator, setCreatingAdministrator] = useState(false);
  const [invalid, setInvalid] = useState(false);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);

  const check = useCallback(async () => {
    setChecking(true);
    setUnavailable(false);
    try {
      const config = await getConfiguration();
      setConfiguration(config);
      if (!config.authenticationEnabled || config.initialAdministratorRequired) {
        setProfile(null);
        return;
      }
      setProfile(await getProfile());
    } catch {
      setUnavailable(true);
    } finally {
      setChecking(false);
    }
  }, []);

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
        setBootstrapError(t.bootstrapClosed);
        await check();
        return;
      }
      if (response.status === 400) {
        setBootstrapError(await responseError(response) ?? t.unavailable);
        return;
      }
      if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);

      const created = await response.json() as AuthProfile;
      setProfile(created);
      setConfiguration(current => current ? { ...current, initialAdministratorRequired: false } : current);
      setPassword('');
      setConfirmPassword('');
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
      setProfile(await response.json() as AuthProfile);
      setPassword('');
    } catch {
      setUnavailable(true);
    } finally {
      setSigningIn(false);
    }
  };

  const logout = useCallback(async () => {
    await fetch(`${API}/api/auth/logout`, { method: 'POST' });
    setProfile(null);
  }, []);

  if (checking) {
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

  if (!configuration?.authenticationEnabled || profile) {
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
