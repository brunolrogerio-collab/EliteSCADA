import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import './auth.css';

type AuthConfiguration = {
  authenticationEnabled: boolean;
  localLoginEnabled: boolean;
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
    username: 'Usuário',
    password: 'Senha',
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
    username: 'Username',
    password: 'Password',
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
    username: 'Usuario',
    password: 'Contraseña',
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
  const [password, setPassword] = useState('');
  const [signingIn, setSigningIn] = useState(false);
  const [invalid, setInvalid] = useState(false);

  const check = useCallback(async () => {
    setChecking(true);
    setUnavailable(false);
    try {
      const config = await getConfiguration();
      setConfiguration(config);
      if (!config.authenticationEnabled) {
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
