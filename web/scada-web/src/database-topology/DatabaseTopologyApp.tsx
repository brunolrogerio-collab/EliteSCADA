import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useAppShellLocale } from '../appShellI18n';
import {
  DatabaseTopologyApiError,
  commitDatabaseCutover,
  loadDatabaseTopologyStatus,
  prepareDatabaseMigration,
  rollbackDatabaseTopology,
  startDatabaseMigration,
  testDatabaseConnection,
  validateDatabaseCompatibility,
  verifyDatabaseMigration
} from './api';
import { databaseTopologyText } from './i18n';
import {
  emptyRemoteProfileDraft,
  type DatabaseCompatibilityResult,
  type DatabaseConnectionHealth,
  type DatabaseMigrationPhase,
  type DatabasePendingMigration,
  type DatabaseProfileStatus,
  type DatabaseRemoteEndpointRequest,
  type DatabaseRemoteProfileRequest,
  type DatabaseTopologyStatus,
  type RemoteEndpointDraft,
  type RemoteProfileDraft
} from './types';
import './database-topology.css';

type Confirmation = 'cutover' | 'rollback' | 'return-local' | null;
type Notice = Readonly<{ tone: 'info' | 'success' | 'warning' | 'danger'; text: string }>;

function endpointLabel(profile?: DatabaseProfileStatus | null) {
  const endpoint = profile?.primary;
  if (!endpoint) return 'Local Managed';
  return `${endpoint.host}:${endpoint.port}/${endpoint.database}`;
}

function modeLabel(mode: string | null | undefined, t: ReturnType<typeof databaseTopologyText>) {
  if (mode === 'LocalManaged') return t.localManaged;
  if (mode === 'Remote') return t.remote;
  return mode || t.unknown;
}

function parseEndpoint(draft: RemoteEndpointDraft): DatabaseRemoteEndpointRequest | null {
  const port = Number(draft.port);
  const timeoutSeconds = Number(draft.timeoutSeconds);
  if (!draft.host.trim() || !draft.database.trim() || !draft.username.trim()) return null;
  if (!Number.isInteger(port) || port < 1 || port > 65535) return null;
  if (!Number.isInteger(timeoutSeconds) || timeoutSeconds < 1 || timeoutSeconds > 120) return null;
  if ((draft.tlsMode === 'VerifyCa' || draft.tlsMode === 'VerifyFull') && !draft.rootCertificatePath.trim()) {
    return null;
  }

  return {
    host: draft.host.trim(),
    port,
    database: draft.database.trim(),
    username: draft.username.trim(),
    password: draft.password || null,
    credentialReference: null,
    tlsMode: draft.tlsMode,
    rootCertificatePath: draft.rootCertificatePath.trim() || null,
    trustServerCertificate: false,
    timeoutSeconds
  };
}

function buildProfile(draft: RemoteProfileDraft): DatabaseRemoteProfileRequest | null {
  const primary = parseEndpoint(draft.primary);
  if (!primary) return null;
  if (draft.historianUsesPrimary) return { primary, historianOverride: null };
  const historianOverride = parseEndpoint(draft.historian);
  return historianOverride ? { primary, historianOverride } : null;
}

function apiErrorText(error: unknown, t: ReturnType<typeof databaseTopologyText>) {
  if (!(error instanceof DatabaseTopologyApiError)) return t.errorUnavailable;
  if (error.kind === 'unauthenticated') return t.errorUnauthenticated;
  if (error.kind === 'forbidden') return t.errorForbidden;
  if (error.kind === 'invalid-request') return error.diagnostic || t.errorInvalid;
  if (error.kind === 'conflict') return error.diagnostic || t.errorConflict;
  if (error.kind === 'server') return error.diagnostic || t.errorServer;
  return error.diagnostic || t.errorUnavailable;
}

function phaseLabel(phase: DatabaseMigrationPhase | null | undefined, t: ReturnType<typeof databaseTopologyText>) {
  if (!phase) return '—';
  const key = `phase${phase}` as keyof typeof t;
  return t[key] || phase;
}

function ResultPill({ ok, children }: { ok?: boolean | null; children: React.ReactNode }) {
  const tone = ok == null ? 'unknown' : ok ? 'healthy' : 'danger';
  return <span className={`db-topology-pill db-topology-pill--${tone}`}>{children}</span>;
}

function healthOk(health?: DatabaseConnectionHealth | null) {
  return health ? health.reachable && !health.failureCode : null;
}

function tlsRequiresCa(mode: RemoteEndpointDraft['tlsMode']) {
  return mode === 'VerifyCa' || mode === 'VerifyFull';
}

function migrationStageIndex(phase?: DatabaseMigrationPhase | null) {
  if (!phase) return -1;
  if (phase === 'Tested' || phase === 'Compatible') return 0;
  if (phase === 'Prepared') return 1;
  if (phase === 'Quiescing' || phase === 'Copying' || phase === 'Copied') return 2;
  if (phase === 'Verifying' || phase === 'Verified') return 3;
  if (phase === 'Switching' || phase === 'Readiness' || phase === 'Completed' || phase === 'RolledBack') return 4;
  return -1;
}

function EndpointCoreFields({
  value,
  onChange,
  prefix,
  disabled
}: {
  value: RemoteEndpointDraft;
  onChange: (next: RemoteEndpointDraft) => void;
  prefix: string;
  disabled?: boolean;
}) {
  const locale = useAppShellLocale();
  const t = databaseTopologyText(locale);
  const field = <K extends keyof RemoteEndpointDraft>(key: K, next: RemoteEndpointDraft[K]) =>
    onChange({ ...value, [key]: next });

  return <div className="db-topology-fields db-topology-fields--core" data-testid={`${prefix}-endpoint-fields`}>
    <label><span>{t.host}</span><input aria-label={`${prefix} ${t.host}`} value={value.host} disabled={disabled} onChange={event => field('host', event.target.value)} /></label>
    <label><span>{t.username}</span><input aria-label={`${prefix} ${t.username}`} autoComplete="username" value={value.username} disabled={disabled} onChange={event => field('username', event.target.value)} /></label>
    <label>
      <span>{t.password}</span>
      <input
        aria-label={`${prefix} ${t.password}`}
        type="password"
        autoComplete="new-password"
        value={value.password}
        disabled={disabled}
        onChange={event => field('password', event.target.value)}
      />
      <small>{t.passwordHelp}</small>
    </label>
  </div>;
}

function EndpointAdvancedFields({
  value,
  onChange,
  prefix,
  disabled
}: {
  value: RemoteEndpointDraft;
  onChange: (next: RemoteEndpointDraft) => void;
  prefix: string;
  disabled?: boolean;
}) {
  const locale = useAppShellLocale();
  const t = databaseTopologyText(locale);
  const field = <K extends keyof RemoteEndpointDraft>(key: K, next: RemoteEndpointDraft[K]) =>
    onChange({ ...value, [key]: next });

  return <div className="db-topology-fields db-topology-fields--advanced">
    <label><span>{t.database}</span><input aria-label={`${prefix} ${t.database}`} value={value.database} disabled={disabled} onChange={event => field('database', event.target.value)} /></label>
    <label><span>{t.port}</span><input aria-label={`${prefix} ${t.port}`} inputMode="numeric" value={value.port} disabled={disabled} onChange={event => field('port', event.target.value)} /></label>
    <label><span>{t.tlsMode}</span>
      <select
        aria-label={`${prefix} ${t.tlsMode}`}
        value={value.tlsMode}
        disabled={disabled}
        onChange={event => {
          const tlsMode = event.target.value as RemoteEndpointDraft['tlsMode'];
          onChange({
            ...value,
            tlsMode,
            rootCertificatePath: tlsRequiresCa(tlsMode) ? value.rootCertificatePath : ''
          });
        }}
      >
        <option value="Disable">Disable</option>
        <option value="Prefer">Prefer</option>
        <option value="Require">Require</option>
        <option value="VerifyCa">VerifyCa</option>
        <option value="VerifyFull">VerifyFull</option>
      </select>
    </label>
    <label><span>{t.timeout}</span><input aria-label={`${prefix} ${t.timeout}`} inputMode="numeric" value={value.timeoutSeconds} disabled={disabled} onChange={event => field('timeoutSeconds', event.target.value)} /></label>
    {tlsRequiresCa(value.tlsMode) ? <label className="db-topology-field-wide"><span>{t.rootCertificatePath}</span><input aria-label={`${prefix} ${t.rootCertificatePath}`} value={value.rootCertificatePath} disabled={disabled} onChange={event => field('rootCertificatePath', event.target.value)} /></label> : null}
  </div>;
}

function HealthTechnicalDetails({ title, health }: { title: string; health?: DatabaseConnectionHealth | null }) {
  const locale = useAppShellLocale();
  const t = databaseTopologyText(locale);
  const authOk = health ? health.failureCode !== 'authentication-failed' && health.reachable : null;
  const tlsOk = health ? health.failureCode !== 'tls-validation-failed' && health.reachable : null;

  return <article className="db-topology-health-detail">
    <header><strong>{title}</strong><ResultPill ok={healthOk(health)}>{health ? (healthOk(health) ? t.healthy : t.degraded) : t.unknown}</ResultPill></header>
    <dl>
      <div><dt>{t.reachable}</dt><dd>{health ? (health.reachable ? t.available : t.unavailable) : t.unknown}</dd></div>
      <div><dt>{t.authentication}</dt><dd>{authOk == null ? t.unknown : authOk ? t.available : t.unavailable}</dd></div>
      <div><dt>{t.tls}</dt><dd>{tlsOk == null ? t.unknown : tlsOk ? t.available : t.unavailable}</dd></div>
      <div><dt>{t.postgresql}</dt><dd>{health?.postgreSqlVersion ?? t.unknown}</dd></div>
      <div><dt>{t.timescale}</dt><dd>{health?.timescaleDbVersion ?? (health?.timescaleCapable ? t.available : t.unknown)}</dd></div>
      <div><dt>{t.schema}</dt><dd>{health ? (health.schemaCompatible ? t.compatible : t.incompatible) : t.unknown}</dd></div>
    </dl>
    {health?.diagnostic ? <p className="db-topology-diagnostic"><strong>{t.diagnostic}:</strong> {health.diagnostic}</p> : null}
  </article>;
}

export function DatabaseTopologyApp() {
  const locale = useAppShellLocale();
  const t = databaseTopologyText(locale);
  const [status, setStatus] = useState<DatabaseTopologyStatus | null>(null);
  const [draft, setDraft] = useState<RemoteProfileDraft>(() => emptyRemoteProfileDraft());
  const [testHealth, setTestHealth] = useState<DatabaseConnectionHealth | null>(null);
  const [compatibility, setCompatibility] = useState<DatabaseCompatibilityResult | null>(null);
  const [pending, setPending] = useState<DatabasePendingMigration | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation>(null);
  const [remoteEditorRequested, setRemoteEditorRequested] = useState(false);

  const refresh = useCallback(async (refreshHealth = false, silent = false) => {
    if (!silent) {
      setBusy(current => current ?? 'refresh');
      setNotice(null);
    }
    try {
      setStatus(await loadDatabaseTopologyStatus(refreshHealth));
    } catch (error) {
      if (!silent) setNotice({ tone: 'danger', text: apiErrorText(error, t) });
    } finally {
      if (!silent) setBusy(current => current === 'refresh' ? null : current);
    }
  }, [t]);

  useEffect(() => { void refresh(true); }, [refresh]);

  const profileRequest = useMemo(() => buildProfile(draft), [draft]);
  const primaryDraftValid = useMemo(() => parseEndpoint(draft.primary), [draft.primary]);
  const pendingPhase = status?.pendingPhase ?? pending?.phase ?? null;
  const operationId = status?.pendingOperationId ?? pending?.operationId ?? null;
  const displayPhase = pendingPhase ?? status?.lastOperation?.phase ?? null;
  const configurationLocked = pendingPhase != null;
  const canRollback = Boolean(status?.recoveryRequired || pendingPhase === 'RollbackRequired');
  const canReturnLocal = Boolean(
    status?.activeTopology.mode === 'Remote' &&
    status?.previousTopology?.mode === 'LocalManaged' &&
    !pendingPhase &&
    !status?.restartRequired
  );
  const primaryEndpoint = status?.activeTopology.primary;
  const primaryHealth = status?.primaryHealth;
  const historianHealth = status?.historianHealth ?? (status?.activeTopology.historianUsesPrimary ? primaryHealth : null);
  const activeHealthFailed = primaryHealth ? healthOk(primaryHealth) === false : false;
  const stageIndex = migrationStageIndex(displayPhase);
  const stages = [t.stepValidate, t.stepPrepare, t.stepCopy, t.stepVerify, t.stepCutover];
  const validationHealth = compatibility?.primary ?? testHealth;
  const validationDiagnostic = compatibility?.diagnostic ?? testHealth?.diagnostic ?? null;
  const remoteDraftStarted = Boolean(draft.primary.host.trim() || draft.primary.username.trim() || draft.primary.password);
  const remoteCoreReady = Boolean(
    draft.primary.host.trim() &&
    draft.primary.username.trim() &&
    draft.primary.password &&
    (draft.historianUsesPrimary || (
      draft.historian.host.trim() &&
      draft.historian.username.trim() &&
      draft.historian.password
    ))
  );
  const showRemoteEditor = Boolean(
    status && (
      configurationLocked ||
      status.activeTopology.mode !== 'Remote' ||
      remoteEditorRequested ||
      remoteDraftStarted
    )
  );
  const autoRefreshActive = Boolean(
    pendingPhase && ['Quiescing', 'Copying', 'Verifying', 'Switching', 'Readiness', 'RollbackRequired'].includes(pendingPhase)
  ) || ['migrate', 'verify', 'commit', 'rollback', 'return-local'].includes(busy ?? '');
  const showMigrationPanel = Boolean(
    pendingPhase ||
    pending?.plan ||
    canRollback ||
    (status?.restartRequired && status?.lastOperation?.phase === 'Completed')
  );
  const nextActionText = !status
    ? t.loadingStatus
    : status.restartRequired
    ? t.restartToFinish
    : canRollback
      ? t.recoveryNext
      : pendingPhase === 'Prepared'
        ? t.preparedReady
        : pendingPhase === 'Copied'
          ? t.verificationAutomatic
          : pendingPhase && ['Quiescing', 'Copying', 'Verifying', 'Switching', 'Readiness'].includes(pendingPhase)
            ? t.migrationRunning
            : pendingPhase === 'Verified'
              ? t.cutoverReady
              : activeHealthFailed
                ? t.healthAttention
                : compatibility?.compatible
                ? t.targetReady
                : remoteDraftStarted || remoteEditorRequested
                  ? t.configureRemoteHint
                  : status?.activeTopology.mode === 'Remote'
                    ? t.remoteStable
                    : t.localStable;

  useEffect(() => {
    if (!autoRefreshActive) return;
    const timer = window.setInterval(() => { void refresh(false, true); }, 5000);
    return () => window.clearInterval(timer);
  }, [autoRefreshActive, refresh]);

  useEffect(() => {
    if (autoRefreshActive) return;
    const timer = window.setInterval(() => { void refresh(true, true); }, 60000);
    return () => window.clearInterval(timer);
  }, [autoRefreshActive, refresh]);

  const invalidateValidation = () => {
    setTestHealth(null);
    setCompatibility(null);
    setNotice(null);
  };

  const resetTransientEditor = () => {
    setDraft(emptyRemoteProfileDraft());
    setTestHealth(null);
    setCompatibility(null);
    setRemoteEditorRequested(false);
  };

  useEffect(() => {
    if (pendingPhase) return;
    if (status?.lastOperation?.phase !== 'Completed' && status?.lastOperation?.phase !== 'RolledBack') return;
    setPending(null);
    setDraft(emptyRemoteProfileDraft());
    setTestHealth(null);
    setCompatibility(null);
    setRemoteEditorRequested(false);
  }, [pendingPhase, status?.lastOperation?.operationId, status?.lastOperation?.phase]);

  const run = async (name: string, action: () => Promise<void>) => {
    if (busy) return;
    setBusy(name);
    setNotice(null);
    try {
      await action();
    } catch (error) {
      setNotice({ tone: 'danger', text: apiErrorText(error, t) });
    } finally {
      setBusy(null);
    }
  };

  const onValidateTarget = () => run('validate', async () => {
    if (!primaryDraftValid || !profileRequest) {
      setNotice({ tone: 'warning', text: t.fieldRequired });
      return;
    }

    setTestHealth(null);
    setCompatibility(null);

    const health = await testDatabaseConnection(primaryDraftValid, draft.historianUsesPrimary);
    setTestHealth(health);
    if (!health.reachable || health.failureCode) {
      setNotice({ tone: 'warning', text: health.diagnostic ?? t.degraded });
      return;
    }

    const result = await validateDatabaseCompatibility(profileRequest);
    setCompatibility(result);
    setNotice({
      tone: result.compatible ? 'success' : 'warning',
      text: result.diagnostic ?? (result.compatible ? t.compatible : t.incompatible)
    });
  });

  const onPrepare = () => run('prepare', async () => {
    if (!profileRequest) {
      setNotice({ tone: 'warning', text: t.fieldRequired });
      return;
    }
    if (!draft.primary.password || (!draft.historianUsesPrimary && !draft.historian.password)) {
      setNotice({ tone: 'warning', text: t.passwordRequired });
      return;
    }
    const result = await prepareDatabaseMigration(profileRequest);
    setPending(result);
    setDraft(current => ({
      ...current,
      primary: { ...current.primary, password: '' },
      historian: { ...current.historian, password: '' }
    }));
    await refresh(false);
    setNotice({ tone: 'success', text: t.credentialsConfigured });
  });

  const onStartMigration = () => run('migrate', async () => {
    if (!operationId) return;
    const copied = await startDatabaseMigration(operationId);
    setPending(copied);
    if (copied.phase !== 'Copied') {
      await refresh(false, true);
      return;
    }

    setBusy('verify');
    const verification = await verifyDatabaseMigration(operationId);
    await refresh(false, true);
    setNotice({
      tone: verification.succeeded ? 'success' : 'danger',
      text: verification.diagnostic ?? (verification.succeeded ? t.compatible : t.incompatible)
    });
  });

  const onVerify = () => run('verify', async () => {
    if (!operationId) return;
    const result = await verifyDatabaseMigration(operationId);
    await refresh(false, true);
    setNotice({ tone: result.succeeded ? 'success' : 'danger', text: result.diagnostic ?? (result.succeeded ? t.compatible : t.incompatible) });
  });

  const onCommit = () => run('commit', async () => {
    if (!operationId) return;
    const result = await commitDatabaseCutover(operationId);
    setStatus(result.status);
    setPending(null);
    if (result.succeeded) resetTransientEditor();
    setNotice({ tone: result.succeeded ? 'success' : 'danger', text: result.succeeded ? t.phaseCompleted : (result.diagnostic ?? t.errorServer) });
    setConfirmation(null);
  });

  const onRollback = () => run('rollback', async () => {
    const result = await rollbackDatabaseTopology(null);
    setStatus(result.status);
    setPending(null);
    if (result.rolledBack) resetTransientEditor();
    setNotice({ tone: result.rolledBack ? 'success' : 'warning', text: phaseLabel(result.status.lastOperation?.phase, t) });
    setConfirmation(null);
  });

  const onReturnLocal = () => run('return-local', async () => {
    const result = await rollbackDatabaseTopology(null);
    setStatus(result.status);
    setPending(null);
    if (result.rolledBack) resetTransientEditor();
    setNotice({ tone: result.rolledBack ? 'success' : 'warning', text: result.rolledBack ? t.localManagedDefault : (result.diagnostic ?? t.errorServer) });
    setConfirmation(null);
  });

  return <main className="db-topology-shell" data-testid="database-topology-app">
    <header className="db-topology-header">
      <div><span>EliteSCADA · System / Storage</span><h1>{t.title}</h1><p>{t.subtitle}</p></div>
      <div className="db-topology-auto-state">
        <strong>{t.automaticRefresh}</strong>
        {status?.lastHealthCheckUtc ? <small>{new Date(status.lastHealthCheckUtc).toLocaleString(locale)}</small> : null}
      </div>
    </header>

    {notice ? <div role="status" className={`db-topology-notice db-topology-notice--${notice.tone}`}>{notice.text}</div> : null}

    <section className="db-topology-panel db-topology-overview" aria-labelledby="database-overview-title">
      <div className="db-topology-section-heading">
        <h2 id="database-overview-title">{t.overview}</h2>
      </div>

      <div className="db-topology-status-strip">
        <div><span>{t.currentMode}</span><strong>{status ? (status.activeTopology.mode === 'Remote' ? t.remote : t.localManagedDefault) : t.unknown}</strong></div>
        <div><span>{t.primary}</span><strong>{status ? endpointLabel(status.activeTopology) : '—'}</strong></div>
        <div><span>{t.health}</span><ResultPill ok={healthOk(primaryHealth)}>{primaryHealth ? (healthOk(primaryHealth) ? t.healthy : t.degraded) : t.unknown}</ResultPill></div>
      </div>

      <div className="db-topology-next-action" data-testid="database-next-action">
        <span>{t.nextAction}</span>
        <strong>{nextActionText}</strong>
        {pendingPhase ? <small data-testid="database-pending-phase">{phaseLabel(pendingPhase, t)}</small> : null}
      </div>

      {status?.restartRequired ? <div className="db-topology-alert db-topology-alert--warning" data-testid="database-restart-warning"><strong>{t.restartRequired}</strong><span>{t.restartPendingBody}</span></div> : null}
      {status?.recoveryRequired ? <div className="db-topology-alert db-topology-alert--danger"><strong>{t.recoveryRequired}</strong></div> : null}
      {canReturnLocal ? <div className="db-topology-actions db-topology-return-local">
        <button type="button" className="db-topology-secondary" disabled={Boolean(busy)} onClick={() => setConfirmation('return-local')}>{t.returnToLocal}</button>
      </div> : null}

      <details className="db-topology-details">
        <summary>{t.technicalDetails}</summary>
        <div className="db-topology-technical-grid">
          <HealthTechnicalDetails title={t.primary} health={primaryHealth} />
          {!status?.activeTopology.historianUsesPrimary ? <HealthTechnicalDetails title={t.historianOverride} health={historianHealth} /> : null}
        </div>
        <dl className="db-topology-meta-list">
          <div><dt>{t.previousTopology}</dt><dd>{status?.previousTopology ? endpointLabel(status.previousTopology) : t.notConfigured}</dd></div>
          <div><dt>{t.lastOperation}</dt><dd>{status?.lastOperation ? phaseLabel(status.lastOperation.phase, t) : t.noLastOperation}</dd></div>
          {status?.lastOperation?.failureCode ? <div><dt>{t.diagnostic}</dt><dd>{status.lastOperation.diagnostic ?? status.lastOperation.failureCode}</dd></div> : null}
        </dl>
        <p className="db-topology-muted-note">{t.failClosed}</p>
      </details>
    </section>

    <section className="db-topology-panel" aria-labelledby="remote-profile-title">
      <div className="db-topology-section-heading">
        <div><h2 id="remote-profile-title">{t.remoteProfile}</h2><p>{status?.activeTopology.mode === 'Remote' ? t.remoteProfileChangeHelp : t.remoteProfileHelp}</p></div>
        {primaryEndpoint?.credentialConfigured && !showRemoteEditor ? <span className="db-topology-credential-state">{t.credentialsConfigured}</span> : null}
      </div>

      {!status ? <div className="db-topology-locked-state">{t.loadingStatus}</div> : status.restartRequired ? <div className="db-topology-locked-state">{t.restartToFinish}</div> : !showRemoteEditor ? <div className="db-topology-collapsed-editor">
        <button type="button" className="db-topology-secondary" onClick={() => setRemoteEditorRequested(true)}>{t.configureAnotherRemote}</button>
      </div> : configurationLocked ? <div className="db-topology-locked-state">{t.configurationLockedHelp}</div> : <>
      <EndpointCoreFields value={draft.primary} prefix="Primary" disabled={false} onChange={primary => {
        setDraft(current => ({ ...current, primary }));
        invalidateValidation();
      }} />

      <details className="db-topology-details db-topology-advanced">
        <summary>
          <span>{t.advancedSettings}</span>
          <small>{t.automaticDefaults}: {t.database} {draft.primary.database} · {t.port} {draft.primary.port} · TLS {draft.primary.tlsMode} · {t.timeout} {draft.primary.timeoutSeconds}s · {draft.historianUsesPrimary ? t.usePrimary : t.override}</small>
        </summary>
        <EndpointAdvancedFields value={draft.primary} prefix="Primary" disabled={configurationLocked} onChange={primary => {
          setDraft(current => ({ ...current, primary }));
          invalidateValidation();
        }} />
        <label className="db-topology-historian-toggle">
          <input
            type="checkbox"
            checked={draft.historianUsesPrimary}
            disabled={configurationLocked}
            onChange={event => {
              const usePrimary = event.target.checked;
              setDraft(current => ({
                ...current,
                historianUsesPrimary: usePrimary,
                historian: usePrimary ? current.historian : { ...current.primary, password: '' }
              }));
              invalidateValidation();
            }}
          />
          <span>{t.historianUsePrimary}</span>
        </label>
        {!draft.historianUsesPrimary ? <div className="db-topology-override-editor">
          <h3>{t.historianOverride}</h3>
          <EndpointCoreFields value={draft.historian} prefix="Historian" disabled={configurationLocked} onChange={historian => {
            setDraft(current => ({ ...current, historian }));
            invalidateValidation();
          }} />
          <EndpointAdvancedFields value={draft.historian} prefix="Historian" disabled={configurationLocked} onChange={historian => {
            setDraft(current => ({ ...current, historian }));
            invalidateValidation();
          }} />
        </div> : null}
      </details>

      <div className="db-topology-validation">
        <div className="db-topology-section-heading">
          <div><h3>{t.validation}</h3><p>{t.validateTargetHelp}</p></div>
        </div>
        <div className="db-topology-actions db-topology-actions--guided">
          <button type="button" data-step="1" disabled={Boolean(busy) || configurationLocked || !remoteCoreReady || !profileRequest} onClick={onValidateTarget}>{busy === 'validate' ? t.working : t.validateTarget}</button>
          <button type="button" data-step="2" className="db-topology-primary-action" disabled={Boolean(busy) || configurationLocked || compatibility?.compatible !== true} onClick={onPrepare}>{busy === 'prepare' ? t.working : t.prepare}</button>
        </div>

        {validationHealth ? <div className="db-topology-result-summary" data-testid="database-validation-result">
          <ResultPill ok={compatibility ? compatibility.compatible : healthOk(validationHealth)}>
            {compatibility ? (compatibility.compatible ? t.compatible : t.incompatible) : (healthOk(validationHealth) ? t.healthy : t.degraded)}
          </ResultPill>
          <span>{t.postgresql} {validationHealth.postgreSqlVersion ?? '—'}</span>
          <span>{t.timescale} {validationHealth.timescaleDbVersion ?? (validationHealth.timescaleCapable ? t.available : t.unavailable)}</span>
          {validationDiagnostic ? <strong>{validationDiagnostic}</strong> : null}
          <details className="db-topology-inline-details">
            <summary>{t.technicalDetails}</summary>
            <div className="db-topology-technical-grid">
              <HealthTechnicalDetails title={t.primary} health={validationHealth} />
              {compatibility?.historian ? <HealthTechnicalDetails title={t.historianOverride} health={compatibility.historian} /> : null}
            </div>
          </details>
        </div> : null}
      </div>
      </>}
    </section>

    {showMigrationPanel ? <section className="db-topology-panel" aria-labelledby="migration-flow-title">
      <div className="db-topology-section-heading">
        <div><h2 id="migration-flow-title">{t.migrationFlow}</h2><p>{displayPhase ? phaseLabel(displayPhase, t) : t.noPending}</p></div>
      </div>

      <ol className="db-topology-stepper" aria-label={t.migrationFlow}>
        {stages.map((label, index) => <li key={label} data-active={stageIndex === index || undefined} data-reached={stageIndex >= index || undefined}><span>{index + 1}</span><strong>{label}</strong></li>)}
      </ol>

      <div className="db-topology-safety-summary" data-testid="database-maintenance-warning">
        <span>{t.maintenanceShort}</span>
        <span>{t.preserveLocal}</span>
      </div>

      {pending?.plan ? <article className="db-topology-plan" data-testid="database-migration-plan">
        <div className="db-topology-plan-route"><span>{t.source}</span><strong>{modeLabel(pending.plan.sourceMode, t)}</strong><span>→</span><span>{t.target}</span><strong>{modeLabel(pending.plan.targetMode, t)}</strong></div>
      </article> : null}

      <details className="db-topology-details db-topology-impact">
        <summary>{t.maintenanceTitle}</summary>
        <p>{t.maintenanceBody}</p>
        <p>{t.failClosed}</p>
      </details>

      <div className="db-topology-actions db-topology-actions--migration">
        {pendingPhase === 'Prepared' && operationId ? <button type="button" className="db-topology-primary-action" disabled={Boolean(busy)} onClick={onStartMigration}>{busy === 'migrate' || busy === 'verify' ? t.working : t.startMigration}</button> : null}
        {pendingPhase === 'Copied' && operationId && !busy ? <button type="button" className="db-topology-secondary" onClick={onVerify}>{t.verify}</button> : null}
        {pendingPhase === 'Verified' && operationId ? <button type="button" className="db-topology-danger-action" disabled={Boolean(busy)} onClick={() => setConfirmation('cutover')}>{t.commitCutover}</button> : null}
        {canRollback ? <button type="button" className="db-topology-secondary" disabled={Boolean(busy)} onClick={() => setConfirmation('rollback')}>{t.rollback}</button> : null}
      </div>
    </section> : null}

    {confirmation ? <div className="db-topology-dialog-backdrop" role="presentation">
      <section role="dialog" aria-modal="true" aria-labelledby="database-confirm-title" className="db-topology-dialog">
        <h2 id="database-confirm-title">
          {confirmation === 'cutover' ? t.cutoverConfirmTitle : confirmation === 'return-local' ? t.returnToLocalConfirmTitle : t.rollbackConfirmTitle}
        </h2>
        <p>
          {confirmation === 'cutover' ? t.cutoverConfirmBody : confirmation === 'return-local' ? t.returnToLocalConfirmBody : t.rollbackConfirmBody}
        </p>
        <dl>
          <div><dt>{t.source}</dt><dd>{endpointLabel(status?.activeTopology)}</dd></div>
          <div><dt>{t.target}</dt><dd>{confirmation === 'cutover' ? (pending?.candidate.primary ? `${pending.candidate.primary.host}:${pending.candidate.primary.port}/${pending.candidate.primary.database}` : t.remote) : confirmation === 'return-local' ? t.localManaged : endpointLabel(status?.previousTopology)}</dd></div>
          <div><dt>{t.preserved}</dt><dd>{confirmation === 'return-local' ? t.returnToLocalPreserveRemote : t.preserveLocal}</dd></div>
        </dl>
        <div className="db-topology-actions">
          <button type="button" className="db-topology-secondary" onClick={() => setConfirmation(null)}>{t.cancel}</button>
          <button
            type="button"
            className="db-topology-danger-action"
            onClick={confirmation === 'cutover' ? onCommit : confirmation === 'return-local' ? onReturnLocal : onRollback}
          >{busy ? t.working : t.confirm}</button>
        </div>
      </section>
    </div> : null}
  </main>;
}
