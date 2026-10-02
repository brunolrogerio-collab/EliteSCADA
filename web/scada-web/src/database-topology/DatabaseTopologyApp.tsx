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

type Confirmation = 'cutover' | 'rollback' | null;
type Notice = Readonly<{ tone: 'info' | 'success' | 'warning' | 'danger'; text: string }>;

const PHASES: readonly DatabaseMigrationPhase[] = [
  'Tested',
  'Compatible',
  'Prepared',
  'Quiescing',
  'Copying',
  'Copied',
  'Verifying',
  'Verified',
  'Switching',
  'Readiness',
  'Completed'
];

function endpointLabel(profile?: DatabaseProfileStatus | null) {
  const endpoint = profile?.primary;
  if (!endpoint) return 'Local Managed';
  return `${endpoint.host}:${endpoint.port}/${endpoint.database}`;
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

function healthTone(health?: DatabaseConnectionHealth | null) {
  if (!health) return 'unknown';
  if (health.reachable && !health.failureCode) return 'healthy';
  return 'danger';
}

function ResultPill({ ok, children }: { ok?: boolean | null; children: React.ReactNode }) {
  const tone = ok == null ? 'unknown' : ok ? 'healthy' : 'danger';
  return <span className={`db-topology-pill db-topology-pill--${tone}`}>{children}</span>;
}

function EndpointFields({
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

  return <div className="db-topology-fields" data-testid={`${prefix}-endpoint-fields`}>
    <label><span>{t.host}</span><input aria-label={`${prefix} ${t.host}`} value={value.host} disabled={disabled} onChange={event => field('host', event.target.value)} /></label>
    <label><span>{t.port}</span><input aria-label={`${prefix} ${t.port}`} inputMode="numeric" value={value.port} disabled={disabled} onChange={event => field('port', event.target.value)} /></label>
    <label><span>{t.database}</span><input aria-label={`${prefix} ${t.database}`} value={value.database} disabled={disabled} onChange={event => field('database', event.target.value)} /></label>
    <label><span>{t.username}</span><input aria-label={`${prefix} ${t.username}`} autoComplete="username" value={value.username} disabled={disabled} onChange={event => field('username', event.target.value)} /></label>
    <label className="db-topology-field-wide">
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
    <label><span>{t.tlsMode}</span>
      <select aria-label={`${prefix} ${t.tlsMode}`} value={value.tlsMode} disabled={disabled} onChange={event => field('tlsMode', event.target.value as RemoteEndpointDraft['tlsMode'])}>
        <option value="Disable">Disable</option>
        <option value="Prefer">Prefer</option>
        <option value="Require">Require</option>
        <option value="VerifyCa">VerifyCa</option>
        <option value="VerifyFull">VerifyFull</option>
      </select>
    </label>
    <label><span>{t.timeout}</span><input aria-label={`${prefix} ${t.timeout}`} inputMode="numeric" value={value.timeoutSeconds} disabled={disabled} onChange={event => field('timeoutSeconds', event.target.value)} /></label>
    <label className="db-topology-field-wide"><span>{t.rootCertificatePath}</span><input aria-label={`${prefix} ${t.rootCertificatePath}`} value={value.rootCertificatePath} disabled={disabled} onChange={event => field('rootCertificatePath', event.target.value)} /></label>

  </div>;
}

function HealthCard({ title, health }: { title: string; health?: DatabaseConnectionHealth | null }) {
  const locale = useAppShellLocale();
  const t = databaseTopologyText(locale);
  const authOk = health ? health.failureCode !== 'authentication-failed' && health.reachable : null;
  const tlsOk = health ? health.failureCode !== 'tls-validation-failed' && health.reachable : null;

  return <article className="db-topology-health" data-tone={healthTone(health)}>
    <header><strong>{title}</strong><ResultPill ok={health ? health.reachable && !health.failureCode : null}>{health ? (health.reachable && !health.failureCode ? t.healthy : t.degraded) : t.unknown}</ResultPill></header>
    <dl>
      <div><dt>{t.reachable}</dt><dd><ResultPill ok={health?.reachable ?? null}>{health ? (health.reachable ? t.available : t.unavailable) : t.unknown}</ResultPill></dd></div>
      <div><dt>{t.authentication}</dt><dd><ResultPill ok={authOk}>{authOk == null ? t.unknown : authOk ? t.available : t.unavailable}</ResultPill></dd></div>
      <div><dt>{t.tls}</dt><dd><ResultPill ok={tlsOk}>{tlsOk == null ? t.unknown : tlsOk ? t.available : t.unavailable}</ResultPill></dd></div>
      <div><dt>{t.postgresql}</dt><dd>{health?.postgreSqlVersion ?? t.unknown}</dd></div>
      <div><dt>{t.timescale}</dt><dd><ResultPill ok={health ? health.timescaleCapable : null}>{health?.timescaleDbVersion ?? (health ? (health.timescaleCapable ? t.available : t.unavailable) : t.unknown)}</ResultPill></dd></div>
      <div><dt>{t.schema}</dt><dd><ResultPill ok={health ? health.schemaCompatible : null}>{health ? (health.schemaCompatible ? t.compatible : t.incompatible) : t.unknown}</ResultPill></dd></div>
    </dl>
    {health?.diagnostic ? <p className="db-topology-diagnostic"><strong>{t.diagnostic}:</strong> {health.diagnostic}</p> : null}
  </article>;
}

function ProfileSummary({ profile, title }: { profile?: DatabaseProfileStatus | null; title: string }) {
  const locale = useAppShellLocale();
  const t = databaseTopologyText(locale);
  if (!profile) return <article className="db-topology-profile"><h3>{title}</h3><p>{t.notConfigured}</p></article>;
  return <article className="db-topology-profile">
    <h3>{title}</h3>
    <dl>
      <div><dt>{t.currentMode}</dt><dd>{profile.mode === 'Remote' ? t.remote : t.localManaged}</dd></div>
      <div><dt>{t.endpoint}</dt><dd>{endpointLabel(profile)}</dd></div>
      {profile.primary ? <div><dt>{t.status}</dt><dd>{profile.primary.credentialConfigured ? t.credentialsConfigured : t.credentialsNotConfigured}</dd></div> : null}
      <div><dt>{t.historian}</dt><dd>{profile.historianUsesPrimary ? t.usePrimary : t.override}</dd></div>
      {profile.historianOverride ? <div><dt>{t.historianOverride}</dt><dd>{profile.historianOverride.host}:{profile.historianOverride.port}/{profile.historianOverride.database}</dd></div> : null}
    </dl>
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

  const refresh = useCallback(async (refreshHealth = false) => {
    setBusy(current => current ?? 'refresh');
    setNotice(null);
    try {
      setStatus(await loadDatabaseTopologyStatus(refreshHealth));
    } catch (error) {
      setNotice({ tone: 'danger', text: apiErrorText(error, t) });
    } finally {
      setBusy(current => current === 'refresh' ? null : current);
    }
  }, [t]);

  useEffect(() => { void refresh(true); }, [refresh]);

  const profileRequest = useMemo(() => buildProfile(draft), [draft]);
  const primaryDraftValid = useMemo(() => parseEndpoint(draft.primary), [draft.primary]);
  const pendingPhase = status?.pendingPhase ?? pending?.phase ?? null;
  const operationId = status?.pendingOperationId ?? pending?.operationId ?? null;
  const isCritical = pendingPhase != null && ['Quiescing', 'Copying', 'Copied', 'Verifying', 'Verified', 'Switching', 'Readiness', 'RollbackRequired'].includes(pendingPhase);
  const hasPrevious = Boolean(status?.previousTopology);
  const canRollback = Boolean(status?.recoveryRequired || pendingPhase === 'RollbackRequired' || hasPrevious);

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

  const onTest = () => run('test', async () => {
    if (!primaryDraftValid) {
      setNotice({ tone: 'warning', text: t.fieldRequired });
      return;
    }
    const health = await testDatabaseConnection(primaryDraftValid, draft.historianUsesPrimary);
    setTestHealth(health);
    setNotice({ tone: health.reachable && !health.failureCode ? 'success' : 'warning', text: health.diagnostic ?? (health.reachable ? t.healthy : t.degraded) });
  });

  const onCompatibility = () => run('compatibility', async () => {
    if (!profileRequest) {
      setNotice({ tone: 'warning', text: t.fieldRequired });
      return;
    }
    const result = await validateDatabaseCompatibility(profileRequest);
    setCompatibility(result);
    setNotice({ tone: result.compatible ? 'success' : 'warning', text: result.diagnostic ?? (result.compatible ? t.compatible : t.incompatible) });
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
    setNotice({ tone: 'success', text: t.passwordCleared });
    await refresh(false);
  });

  const onStartMigration = () => run('copy', async () => {
    if (!operationId) return;
    setPending(await startDatabaseMigration(operationId));
    await refresh(false);
  });

  const onVerify = () => run('verify', async () => {
    if (!operationId) return;
    const result = await verifyDatabaseMigration(operationId);
    setNotice({ tone: result.succeeded ? 'success' : 'danger', text: result.diagnostic ?? (result.succeeded ? t.compatible : t.incompatible) });
    await refresh(false);
  });

  const onCommit = () => run('commit', async () => {
    if (!operationId) return;
    const result = await commitDatabaseCutover(operationId);
    setStatus(result.status);
    setPending(null);
    setNotice({ tone: result.succeeded ? 'success' : 'danger', text: result.succeeded ? t.phaseCompleted : (result.diagnostic ?? t.errorServer) });
    setConfirmation(null);
  });

  const onRollback = () => run('rollback', async () => {
    const result = await rollbackDatabaseTopology(operationId);
    setStatus(result.status);
    setPending(null);
    setNotice({ tone: result.rolledBack ? 'success' : 'warning', text: phaseLabel(result.status.lastOperation?.phase, t) });
    setConfirmation(null);
  });

  const primaryEndpoint = status?.activeTopology.primary;

  return <main className="db-topology-shell" data-testid="database-topology-app">
    <header className="db-topology-header">
      <div><span>EliteSCADA · System / Storage</span><h1>{t.title}</h1><p>{t.subtitle}</p></div>
      <button type="button" className="db-topology-secondary" disabled={Boolean(busy)} onClick={() => void refresh(true)}>{t.refresh}</button>
    </header>

    {notice ? <div role="status" className={`db-topology-notice db-topology-notice--${notice.tone}`}>{notice.text}</div> : null}

    <section className="db-topology-grid db-topology-grid--summary">
      <ProfileSummary profile={status?.activeTopology} title={t.currentTopology} />
      <ProfileSummary profile={status?.previousTopology} title={t.previousTopology} />
      <article className="db-topology-profile db-topology-operation-card">
        <h3>{t.pendingOperation}</h3>
        <dl>
          <div><dt>{t.phase}</dt><dd data-testid="database-pending-phase">{pendingPhase ? phaseLabel(pendingPhase, t) : t.noPending}</dd></div>
          <div><dt>{t.operationId}</dt><dd className="db-topology-mono">{operationId ?? '—'}</dd></div>
          <div><dt>{t.restartRequired}</dt><dd><ResultPill ok={status ? !status.restartRequired : null}>{status?.restartRequired ? t.restartRequired : '—'}</ResultPill></dd></div>
          <div><dt>{t.recoveryRequired}</dt><dd><ResultPill ok={status ? !status.recoveryRequired : null}>{status?.recoveryRequired ? t.recoveryRequired : '—'}</ResultPill></dd></div>
          <div><dt>{t.lastOperation}</dt><dd>{status?.lastOperation ? `${phaseLabel(status.lastOperation.phase, t)} · ${status.lastOperation.operationId}` : t.noLastOperation}</dd></div>
          {status?.lastOperation?.failureCode ? <div><dt>{t.diagnostic}</dt><dd>{status.lastOperation.diagnostic ?? status.lastOperation.failureCode}</dd></div> : null}
        </dl>
      </article>
    </section>

    <section className="db-topology-panel" aria-labelledby="database-health-title">
      <div className="db-topology-section-heading"><div><h2 id="database-health-title">{t.health}</h2><p>{status?.lastHealthCheckUtc ? new Date(status.lastHealthCheckUtc).toLocaleString(locale) : t.unknown}</p></div></div>
      <div className="db-topology-grid db-topology-grid--health">
        <HealthCard title={t.primary} health={status?.primaryHealth} />
        <HealthCard title={status?.activeTopology.historianUsesPrimary ? `${t.historian} · ${t.usePrimary}` : `${t.historian} · ${t.override}`} health={status?.historianHealth ?? (status?.activeTopology.historianUsesPrimary ? status?.primaryHealth : null)} />
      </div>
      {status?.restartRequired ? <div className="db-topology-safety-strip" data-testid="database-restart-warning"><strong>{t.restartRequired}</strong><p>{t.restartPendingBody}</p></div> : null}
      <div className="db-topology-safety-strip"><strong>{t.failClosed}</strong></div>
    </section>

    <section className="db-topology-panel" aria-labelledby="remote-profile-title">
      <div className="db-topology-section-heading"><div><h2 id="remote-profile-title">{t.remoteProfile}</h2><p>{t.remoteProfileHelp}</p></div>{primaryEndpoint?.credentialConfigured ? <span className="db-topology-credential-state">{t.credentialsConfigured}</span> : null}</div>
      <h3>{t.primary}</h3>
      <EndpointFields value={draft.primary} prefix="Primary" disabled={isCritical} onChange={primary => {
        setDraft(current => ({ ...current, primary }));
        setCompatibility(null);
      }} />
      <label className="db-topology-historian-toggle">
        <input
          type="checkbox"
          checked={draft.historianUsesPrimary}
          disabled={isCritical}
          onChange={event => {
            setDraft(current => ({ ...current, historianUsesPrimary: event.target.checked }));
            setCompatibility(null);
          }}
        />
        <span>{t.historianUsePrimary}</span>
      </label>
      {!draft.historianUsesPrimary ? <>
        <h3>{t.historianOverride}</h3>
        <EndpointFields value={draft.historian} prefix="Historian" disabled={isCritical} onChange={historian => {
          setDraft(current => ({ ...current, historian }));
          setCompatibility(null);
        }} />
      </> : null}
      <div className="db-topology-actions">
        <button type="button" disabled={Boolean(busy) || isCritical} onClick={onTest}>{busy === 'test' ? t.working : t.testConnection}</button>
        <button type="button" disabled={Boolean(busy) || isCritical} onClick={onCompatibility}>{busy === 'compatibility' ? t.working : t.validateCompatibility}</button>
        <button type="button" className="db-topology-primary-action" disabled={Boolean(busy) || isCritical || compatibility?.compatible !== true} onClick={onPrepare}>{busy === 'prepare' ? t.working : t.prepare}</button>
      </div>

      {testHealth ? <div className="db-topology-result" data-testid="database-test-result"><h3>{t.testResult}</h3><HealthCard title={t.primary} health={testHealth} /></div> : null}
      {compatibility ? <div className="db-topology-result" data-testid="database-compatibility-result">
        <div className="db-topology-section-heading">
          <h3>{t.compatibilityResult}</h3>
          <ResultPill ok={compatibility.compatible}>{compatibility.compatible ? t.compatible : t.incompatible}</ResultPill>
        </div>
        <div className="db-topology-grid db-topology-grid--health"><HealthCard title={t.primary} health={compatibility.primary} />{compatibility.historian ? <HealthCard title={t.historianOverride} health={compatibility.historian} /> : null}</div>
        {compatibility.diagnostic ? <p className="db-topology-diagnostic"><strong>{t.diagnostic}:</strong> {compatibility.diagnostic}</p> : null}
      </div> : null}
    </section>

    <section className="db-topology-panel" aria-labelledby="migration-flow-title">
      <div className="db-topology-section-heading"><div><h2 id="migration-flow-title">{t.migrationFlow}</h2><p>TEST → COMPATIBILITY → PREPARE → QUIESCE → COPY → VERIFY → CUTOVER → READINESS → RESTART → COMPLETE</p></div></div>
      <ol className="db-topology-timeline">
        {PHASES.map(phase => {
          const currentIndex = pendingPhase ? PHASES.indexOf(pendingPhase) : -1;
          const index = PHASES.indexOf(phase);
          const reached = currentIndex >= 0 && index <= currentIndex;
          const active = pendingPhase === phase;
          return <li key={phase} data-active={active || undefined} data-reached={reached || undefined}><span>{index + 1}</span><strong>{phaseLabel(phase, t)}</strong></li>;
        })}
      </ol>

      <div className="db-topology-maintenance" data-testid="database-maintenance-warning"><strong>{t.maintenanceTitle}</strong><p>{t.maintenanceBody}</p></div>
      <div className="db-topology-preservation"><strong>{t.preserveLocal}</strong></div>

      {pending?.plan ? <article className="db-topology-plan" data-testid="database-migration-plan">
        <h3>{t.plan}</h3>
        <dl>
          <div><dt>{t.currentToTarget}</dt><dd>{pending.plan.sourceMode} → {pending.plan.targetMode}</dd></div>
          <div><dt>{t.operationId}</dt><dd className="db-topology-mono">{pending.plan.operationId}</dd></div>
          <div><dt>{t.durableDomains}</dt><dd>{pending.plan.durableDomains.join(', ')}</dd></div>
        </dl>
      </article> : null}

      <div className="db-topology-actions db-topology-actions--migration">
        <button type="button" disabled={Boolean(busy) || pendingPhase !== 'Prepared' || !operationId} onClick={onStartMigration}>{busy === 'copy' ? t.working : t.startMigration}</button>
        <button type="button" disabled={Boolean(busy) || pendingPhase !== 'Copied' || !operationId} onClick={onVerify}>{busy === 'verify' ? t.working : t.verify}</button>
        <button type="button" className="db-topology-danger-action" disabled={Boolean(busy) || pendingPhase !== 'Verified' || !operationId} onClick={() => setConfirmation('cutover')}>{t.commitCutover}</button>
        <button type="button" className="db-topology-secondary" disabled={Boolean(busy) || !canRollback} onClick={() => setConfirmation('rollback')}>{t.rollback}</button>
      </div>
    </section>

    {confirmation ? <div className="db-topology-dialog-backdrop" role="presentation">
      <section role="dialog" aria-modal="true" aria-labelledby="database-confirm-title" className="db-topology-dialog">
        <h2 id="database-confirm-title">{confirmation === 'cutover' ? t.cutoverConfirmTitle : t.rollbackConfirmTitle}</h2>
        <p>{confirmation === 'cutover' ? t.cutoverConfirmBody : t.rollbackConfirmBody}</p>
        <dl>
          <div><dt>{t.source}</dt><dd>{endpointLabel(status?.activeTopology)}</dd></div>
          <div><dt>{t.target}</dt><dd>{confirmation === 'cutover' ? (pending?.candidate.primary ? `${pending.candidate.primary.host}:${pending.candidate.primary.port}/${pending.candidate.primary.database}` : t.remote) : endpointLabel(status?.previousTopology)}</dd></div>
          <div><dt>{t.preserved}</dt><dd>{t.preserveLocal}</dd></div>
        </dl>
        <div className="db-topology-actions">
          <button type="button" className="db-topology-secondary" onClick={() => setConfirmation(null)}>{t.cancel}</button>
          <button type="button" className="db-topology-danger-action" onClick={confirmation === 'cutover' ? onCommit : onRollback}>{busy ? t.working : t.confirm}</button>
        </div>
      </section>
    </div> : null}
  </main>;
}
