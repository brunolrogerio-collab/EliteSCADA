import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { EngineeringApp } from './EngineeringApp';
import { EngineeringProjectManagementWorkspace } from './EngineeringProjectManagementWorkspace';
import { UserAdministration } from './UserAdministration';
import {
  clearEngineeringLock,
  configureEngineeringLock,
  EngineeringLockApiError,
  loadEngineeringLockStatus,
  lockEngineering,
  unlockEngineering,
  type EngineeringLockStatus
} from './engineeringLockApi';
import {
  resolveInitialLocale,
  setStoredLocale,
  type EngineeringLocale
} from './i18n';
import './engineering-lock.css';

type Copy = ReturnType<typeof lockCopy>;

export function EngineeringLockGate() {
  const locale = useLiveEngineeringLocale();
  const copy = useMemo(() => lockCopy(locale), [locale]);
  const [status, setStatus] = useState<EngineeringLockStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setStatus(await loadEngineeringLockStatus());
    } catch (cause) {
      setStatus(null);
      setError(lockErrorText(cause, copy));
    } finally {
      setLoading(false);
    }
  }, [copy]);

  useEffect(() => { void refresh(); }, [refresh]);

  if (loading && !status) {
    return <main className="eng-lock-state" aria-busy="true">{copy.loading}</main>;
  }

  if (!status) {
    return <main className="eng-lock-state" role="alert"><strong>{copy.unavailable}</strong><span>{error}</span><button type="button" onClick={() => void refresh()}>{copy.retry}</button></main>;
  }

  if (status.locked) {
    return <LockedEngineeringSurface locale={locale} copy={copy} status={status} onStatus={setStatus} />;
  }

  return (
    <>
      <EngineeringLockManagement locale={locale} copy={copy} status={status} onStatus={setStatus} />
      <EngineeringApp />
    </>
  );
}

function LockedEngineeringSurface({ locale, copy, status, onStatus }: {
  locale: EngineeringLocale;
  copy: Copy;
  status: EngineeringLockStatus;
  onStatus: (status: EngineeringLockStatus) => void;
}) {
  const [secret, setSecret] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function unlock() {
    setBusy(true);
    setError(null);
    try {
      const next = await unlockEngineering(secret);
      setSecret('');
      onStatus(next);
    } catch (cause) {
      setSecret('');
      setError(lockErrorText(cause, copy));
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="eng-lock-restricted" data-testid="engineering-lock-restricted">
      <header className="eng-lock-restricted__header">
        <div>
          <span className="eng-lock-restricted__eyebrow">{copy.restrictedEyebrow}</span>
          <h1>{copy.lockedTitle}</h1>
          <p>{copy.lockedDescription}</p>
        </div>
        <div className="eng-lock-restricted__header-actions">
          <a href="/">{copy.runtime}</a>
          <a href="/licensing">{copy.licensing}</a>
          <LocalePicker locale={locale} copy={copy} />
        </div>
      </header>

      <section className="eng-lock-card eng-lock-card--unlock" aria-label={copy.unlockTitle}>
        <div>
          <span>{copy.status}</span>
          <strong>{status.configured ? copy.lockedConfigured : copy.locked}</strong>
          <p>{copy.unlockDescription}</p>
        </div>
        <form onSubmit={event => { event.preventDefault(); void unlock(); }}>
          <label>
            <span>{copy.secret}</span>
            <input
              data-testid="engineering-lock-unlock-secret"
              type="password"
              autoComplete="current-password"
              value={secret}
              onChange={event => setSecret(event.target.value)}
              disabled={busy}
            />
          </label>
          <button type="submit" disabled={busy || !secret}>{busy ? copy.unlocking : copy.unlock}</button>
        </form>
        {error && <p className="eng-lock-error" role="alert">{error}</p>}
      </section>

      <section className="eng-lock-boundary" aria-label={copy.availableTitle}>
        <h2>{copy.availableTitle}</h2>
        <p>{copy.availableDescription}</p>
        <div className="eng-lock-boundary__items">
          <span>{copy.portability}</span>
          <span>{copy.users}</span>
          <span>{copy.licensing}</span>
          <span>{copy.unlock}</span>
        </div>
      </section>

      <EngineeringProjectManagementWorkspace locale={locale} />
      <UserAdministration locale={locale} />
    </main>
  );
}

function EngineeringLockManagement({ locale, copy, status, onStatus }: {
  locale: EngineeringLocale;
  copy: Copy;
  status: EngineeringLockStatus;
  onStatus: (status: EngineeringLockStatus) => void;
}) {
  const [secret, setSecret] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function perform(name: string, action: () => Promise<EngineeringLockStatus>, success: string) {
    setBusy(name);
    setError(null);
    setNotice(null);
    try {
      const next = await action();
      setSecret('');
      onStatus(next);
      setNotice(success);
    } catch (cause) {
      setSecret('');
      setError(lockErrorText(cause, copy));
    } finally {
      setBusy(null);
    }
  }

  return (
    <section className="eng-lock-management" data-testid="engineering-lock-management" aria-label={copy.managementTitle}>
      <div className="eng-lock-management__summary">
        <span>{copy.managementEyebrow}</span>
        <strong>{copy.managementTitle}</strong>
        <small>{status.configured ? copy.configured : copy.notConfigured} · {copy.unlocked}</small>
      </div>
      <div className="eng-lock-management__configure">
        <label>
          <span>{status.configured ? copy.replaceSecret : copy.newSecret}</span>
          <input
            data-testid="engineering-lock-configure-secret"
            type="password"
            autoComplete="new-password"
            value={secret}
            onChange={event => setSecret(event.target.value)}
            disabled={Boolean(busy)}
          />
        </label>
        <button type="button" disabled={Boolean(busy) || !secret} onClick={() => void perform('configure', () => configureEngineeringLock(secret, false), copy.configuredNotice)}>{copy.configure}</button>
        <button type="button" disabled={Boolean(busy) || !secret} onClick={() => void perform('configure-lock', () => configureEngineeringLock(secret, true), copy.lockedNotice)}>{copy.configureAndLock}</button>
      </div>
      <div className="eng-lock-management__actions">
        <button type="button" disabled={Boolean(busy) || !status.configured} onClick={() => void perform('lock', lockEngineering, copy.lockedNotice)}>{copy.lockNow}</button>
        <button type="button" disabled={Boolean(busy) || !status.configured} onClick={() => void perform('clear', clearEngineeringLock, copy.clearedNotice)}>{copy.clear}</button>
      </div>
      <p className="eng-lock-management__hint">{copy.lifecycleHint}</p>
      {notice && <p className="eng-lock-notice" role="status">{notice}</p>}
      {error && <p className="eng-lock-error" role="alert">{error}</p>}
    </section>
  );
}

function LocalePicker({ locale, copy }: { locale: EngineeringLocale; copy: Copy }) {
  return (
    <label className="eng-lock-locale">
      <span>{copy.language}</span>
      <select
        aria-label={copy.language}
        value={locale}
        onChange={event => {
          const next = event.target.value as EngineeringLocale;
          setStoredLocale(next);
          document.documentElement.lang = next;
        }}
      >
        <option value="pt-BR">Português</option>
        <option value="en">English</option>
        <option value="es">Español</option>
      </select>
    </label>
  );
}

function useLiveEngineeringLocale(): EngineeringLocale {
  const [locale, setLocale] = useState<EngineeringLocale>(() => resolveInitialLocale());

  useEffect(() => {
    const read = () => {
      const value = document.documentElement.lang;
      if (value === 'pt-BR' || value === 'en' || value === 'es') setLocale(value);
    };
    read();
    const observer = new MutationObserver(read);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
    return () => observer.disconnect();
  }, []);

  return locale;
}

function lockErrorText(cause: unknown, copy: Copy): string {
  if (cause instanceof EngineeringLockApiError) {
    if (cause.status === 403) return copy.unlockDenied;
    return cause.message || copy.operationFailed;
  }
  return cause instanceof Error ? cause.message : copy.operationFailed;
}

function lockCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    loading: 'Checking Engineering protection…', unavailable: 'Engineering protection status is unavailable.', retry: 'Try again',
    restrictedEyebrow: 'Application protection', lockedTitle: 'Engineering is locked', lockedDescription: 'Application Engineering content is protected. System administration, Import/Export, replacement/restore and explicit unlock remain available according to backend Authority.',
    runtime: 'Runtime', licensing: 'Licensing', status: 'Status', lockedConfigured: 'Configured and locked', locked: 'Locked', unlockTitle: 'Unlock current application', unlockDescription: 'Enter the Engineering Lock secret. This secret is independent from your account password and Authority backup password.', secret: 'Engineering Lock secret', unlock: 'Unlock', unlocking: 'Unlocking…', unlockDenied: 'Engineering remains locked. The supplied secret was not accepted.',
    availableTitle: 'Restricted administration remains available', availableDescription: 'Protected Engineering editors are not mounted while locked. These system-owned functions remain reachable under their normal backend authorization.', portability: 'Import / Export / Restore', users: 'Users and roles',
    managementEyebrow: 'Application protection', managementTitle: 'Engineering Lock', configured: 'Secret configured', notConfigured: 'No secret configured', unlocked: 'Unlocked', newSecret: 'New secret', replaceSecret: 'Replace secret', configure: 'Configure', configureAndLock: 'Configure and lock', lockNow: 'Lock now', clear: 'Remove secret', lifecycleHint: 'Lock changes are Working changes. Save → Publish → Activate is required to make a new protection state durable Active authority across restart.', configuredNotice: 'Engineering Lock secret configured.', lockedNotice: 'Engineering is now locked.', clearedNotice: 'Engineering Lock secret removed.', operationFailed: 'Engineering Lock operation failed.', language: 'Language'
  };
  if (locale === 'es') return {
    loading: 'Verificando protección de Engineering…', unavailable: 'El estado de protección de Engineering no está disponible.', retry: 'Intentar nuevamente',
    restrictedEyebrow: 'Protección de la aplicación', lockedTitle: 'Engineering está bloqueado', lockedDescription: 'El contenido de Engineering de la aplicación está protegido. La administración del sistema, Import/Export, reemplazo/restauración y el desbloqueo explícito siguen disponibles según la autoridad del backend.',
    runtime: 'Runtime', licensing: 'Licenciamiento', status: 'Estado', lockedConfigured: 'Configurado y bloqueado', locked: 'Bloqueado', unlockTitle: 'Desbloquear aplicación actual', unlockDescription: 'Ingrese el secreto de Engineering Lock. Es independiente de la contraseña de su cuenta y de la contraseña del backup de Authority.', secret: 'Secreto de Engineering Lock', unlock: 'Desbloquear', unlocking: 'Desbloqueando…', unlockDenied: 'Engineering permanece bloqueado. El secreto informado no fue aceptado.',
    availableTitle: 'La administración restringida sigue disponible', availableDescription: 'Los editores protegidos de Engineering no se montan mientras la aplicación está bloqueada. Estas funciones del sistema conservan su autorización normal del backend.', portability: 'Import / Export / Restaurar', users: 'Usuarios y roles',
    managementEyebrow: 'Protección de la aplicación', managementTitle: 'Engineering Lock', configured: 'Secreto configurado', notConfigured: 'Sin secreto configurado', unlocked: 'Desbloqueado', newSecret: 'Nuevo secreto', replaceSecret: 'Reemplazar secreto', configure: 'Configurar', configureAndLock: 'Configurar y bloquear', lockNow: 'Bloquear ahora', clear: 'Eliminar secreto', lifecycleHint: 'Los cambios de Lock son cambios de Working. Se requiere Guardar → Publicar → Activar para que el nuevo estado de protección sea la autoridad Active durable después de reiniciar.', configuredNotice: 'Secreto de Engineering Lock configurado.', lockedNotice: 'Engineering está bloqueado.', clearedNotice: 'Se eliminó el secreto de Engineering Lock.', operationFailed: 'Falló la operación de Engineering Lock.', language: 'Idioma'
  };
  return {
    loading: 'Verificando proteção do Engineering…', unavailable: 'O estado de proteção do Engineering não está disponível.', retry: 'Tentar novamente',
    restrictedEyebrow: 'Proteção da aplicação', lockedTitle: 'Engineering bloqueado', lockedDescription: 'O conteúdo de Engineering da aplicação está protegido. Administração do sistema, Import/Export, substituição/restauração e desbloqueio explícito permanecem disponíveis conforme a Authority do backend.',
    runtime: 'Runtime', licensing: 'Licenciamento', status: 'Estado', lockedConfigured: 'Configurado e bloqueado', locked: 'Bloqueado', unlockTitle: 'Desbloquear aplicação atual', unlockDescription: 'Informe o segredo do Engineering Lock. Ele é independente da senha da sua conta e da senha de backup da Authority.', secret: 'Segredo do Engineering Lock', unlock: 'Desbloquear', unlocking: 'Desbloqueando…', unlockDenied: 'O Engineering continua bloqueado. O segredo informado não foi aceito.',
    availableTitle: 'Administração restrita permanece disponível', availableDescription: 'Os editores protegidos de Engineering não são montados enquanto a aplicação está bloqueada. Estas funções do sistema permanecem sob a autorização normal do backend.', portability: 'Import / Export / Restaurar', users: 'Usuários e papéis',
    managementEyebrow: 'Proteção da aplicação', managementTitle: 'Engineering Lock', configured: 'Segredo configurado', notConfigured: 'Nenhum segredo configurado', unlocked: 'Desbloqueado', newSecret: 'Novo segredo', replaceSecret: 'Substituir segredo', configure: 'Configurar', configureAndLock: 'Configurar e bloquear', lockNow: 'Bloquear agora', clear: 'Remover segredo', lifecycleHint: 'Mudanças do Lock são mudanças do Working. Salvar → Publicar → Ativar é necessário para que o novo estado de proteção se torne a Authority Active durável após reinício.', configuredNotice: 'Segredo do Engineering Lock configurado.', lockedNotice: 'Engineering bloqueado.', clearedNotice: 'Segredo do Engineering Lock removido.', operationFailed: 'Falha na operação do Engineering Lock.', language: 'Idioma'
  };
}
