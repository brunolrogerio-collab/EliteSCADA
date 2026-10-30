import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useAppShellLocale } from '../appShellI18n';
import { translator, type EngineeringLocale, type TranslationKey } from '../engineering/i18n';
import {
  DatabaseTopologyApiError,
  applyHistorianRawRetention,
  createHistorianArchive,
  downloadHistorianArchive,
  commitDatabaseCutover,
  connectExistingDatabase,
  loadDatabaseTopologyStatus,
  loadHistorianStorageAdministrationStatus,
  prepareDatabaseMigration,
  rollbackDatabaseTopology,
  resumeHistorianArchive,
  restoreHistorianArchive,
  saveHistorianArchiveSchedule,
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
  type HistorianStorageAdministrationStatus,
  type HistorianArchiveJob,
  type HistorianArchiveSchedule,
  type RemoteEndpointDraft,
  type RemoteProfileDraft
} from './types';
import './database-topology.css';
import '../engineering/engineering.css';

type Confirmation = 'cutover' | 'connect-existing' | 'rollback' | 'return-local' | null;
type Notice = Readonly<{ tone: 'info' | 'success' | 'warning' | 'danger'; text: string }>;
type RawSampleLimitAction = 'WarnOnly' | 'DeleteOldestCompleteChunks';
const GIBIBYTE = 1024 ** 3;
const MAX_ARCHIVE_UPLOAD_BYTES = 100 * 1024 ** 3;

function toLocalDateTimeInput(value: Date) {
  return new Date(value.getTime() - value.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}
type EngineeringNavItem = {
  id: string;
  icon: string;
  label?: TranslationKey;
  literalLabel?: Record<EngineeringLocale, string>;
};
type EngineeringNavGroup = { label: TranslationKey; items: EngineeringNavItem[] };

const engineeringNavigation: EngineeringNavGroup[] = [
  { label: 'nav.project', items: [
    { id: 'overview', icon: '⌂', label: 'nav.overview' },
    { id: 'installation', icon: '⇆', literalLabel: { 'pt-BR': 'Instalação', en: 'Installation', es: 'Instalación' } },
    { id: 'branding', icon: '◐', literalLabel: { 'pt-BR': 'Cabeçalho', en: 'Header', es: 'Encabezado' } },
    { id: 'mobile', icon: '▱', literalLabel: { 'pt-BR': 'Mobile', en: 'Mobile', es: 'Móvil' } },
    { id: 'scripts', icon: '</>' },
    { id: 'libraries', icon: '▱', literalLabel: { 'pt-BR': 'Bibliotecas', en: 'Libraries', es: 'Bibliotecas' } }
  ] },
  { label: 'nav.communication', items: [
    { id: 'dataSources', icon: '⇄', label: 'nav.dataSources' },
    { id: 'mediaSources', icon: '▣', literalLabel: { 'pt-BR': 'Fontes de mídia', en: 'Media sources', es: 'Fuentes multimedia' } },
    { id: 'tags', icon: '#', label: 'nav.tags' },
    { id: 'gateway', icon: '⇢', literalLabel: { 'pt-BR': 'TAG Gateway', en: 'TAG Gateway', es: 'TAG Gateway' } },
    { id: 'alarms', icon: '!', label: 'nav.alarms' },
    { id: 'operationalEvents', icon: '✦', literalLabel: { 'pt-BR': 'Eventos Operacionais', en: 'Operational Events', es: 'Eventos Operacionales' } }
  ] },
  { label: 'nav.assets', items: [
    { id: 'templates', icon: '◇', label: 'nav.templates' },
    { id: 'equipment', icon: '□', label: 'nav.equipment' },
    { id: 'dynamos', icon: '◈', label: 'nav.dynamos' },
    { id: 'visualAssets', icon: '▧', literalLabel: { 'pt-BR': 'Assets visuais', en: 'Visual assets', es: 'Assets visuales' } }
  ] },
  { label: 'nav.visualization', items: [
    { id: 'screens', icon: '▣', label: 'nav.screens' },
    { id: 'popups', icon: '▤', label: 'nav.popups' }
  ] },
  { label: 'nav.historian', items: [
    { id: 'historian', icon: '⌁', label: 'nav.historian' },
    { id: 'reports', icon: '▧', literalLabel: { 'pt-BR': 'Relatórios', en: 'Reports', es: 'Informes' } }
  ] },
  { label: 'nav.security', items: [
    { id: 'security', icon: '◆', label: 'nav.security' },
    { id: 'highAvailability', icon: '⇄', literalLabel: { 'pt-BR': 'Alta disponibilidade', en: 'High Availability', es: 'Alta disponibilidad' } },
    { id: 'databaseTopology', icon: '▤', literalLabel: { 'pt-BR': 'Banco de dados', en: 'Database', es: 'Base de datos' } }
  ] },
  { label: 'nav.diagnostics', items: [
    { id: 'monitor', icon: '◉', literalLabel: { 'pt-BR': 'Monitoramento', en: 'Development Monitor', es: 'Monitor de Desarrollo' } },
    { id: 'tagMonitor', icon: '◫', literalLabel: { 'pt-BR': 'TAG Monitor', en: 'TAG Monitor', es: 'TAG Monitor' } },
    { id: 'diagnostics', icon: '⋯', label: 'nav.diagnostics' },
    { id: 'information', icon: 'ⓘ', literalLabel: { 'pt-BR': 'Informações', en: 'Information', es: 'Información' } }
  ] }
];

function engineeringNavigationPath(section: string) {
  if (section === 'overview') return '/engineering';
  if (section === 'libraries') return '/engineering/libraries';
  if (section === 'tagMonitor') return '/engineering/diagnostics/tag-monitor';
  if (section === 'databaseTopology') return '/engineering/database-topology';
  return `/engineering/${section}`;
}

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
  if (error.kind === 'conflict' && error.diagnostic?.startsWith('Existing database cannot be connected safely')) {
    return t.existingDatabaseMismatch;
  }
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

function formatStorageBytes(value: number | null | undefined, locale: EngineeringLocale, unknown: string) {
  if (value == null || !Number.isFinite(value) || value < 0) return unknown;
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let scaled = value;
  let unit = 0;
  while (scaled >= 1024 && unit < units.length - 1) {
    scaled /= 1024;
    unit++;
  }
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(scaled)} ${units[unit]}`;
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
  const engineeringText = translator(locale);
  const [status, setStatus] = useState<DatabaseTopologyStatus | null>(null);
  const [draft, setDraft] = useState<RemoteProfileDraft>(() => emptyRemoteProfileDraft());
  const [testHealth, setTestHealth] = useState<DatabaseConnectionHealth | null>(null);
  const [compatibility, setCompatibility] = useState<DatabaseCompatibilityResult | null>(null);
  const [pending, setPending] = useState<DatabasePendingMigration | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation>(null);
  const [remoteEditorRequested, setRemoteEditorRequested] = useState(false);
  const [storageStatus, setStorageStatus] = useState<HistorianStorageAdministrationStatus | null>(null);
  const [storageError, setStorageError] = useState<string | null>(null);
  const [storageBusy, setStorageBusy] = useState(false);
  const [retainRawIndefinitely, setRetainRawIndefinitely] = useState(true);
  const [rawRetentionDaysDraft, setRawRetentionDaysDraft] = useState('');
  const [rawSampleLimitGiBDraft, setRawSampleLimitGiBDraft] = useState('');
  const [rawSampleLimitActionDraft, setRawSampleLimitActionDraft] = useState<RawSampleLimitAction>('WarnOnly');
  const [acknowledgeUnmanagedPolicy, setAcknowledgeUnmanagedPolicy] = useState(false);
  const [acknowledgePotentialExpiration, setAcknowledgePotentialExpiration] = useState(false);
  const [archiveFromDraft, setArchiveFromDraft] = useState(() => toLocalDateTimeInput(new Date(Date.now() - 24 * 60 * 60 * 1000)));
  const [archiveToDraft, setArchiveToDraft] = useState(() => toLocalDateTimeInput(new Date()));
  const [archiveDeleteSource, setArchiveDeleteSource] = useState(false);
  const [archiveDeleteConfirmed, setArchiveDeleteConfirmed] = useState(false);
  const [archiveRestoreFile, setArchiveRestoreFile] = useState<File | null>(null);
  const [archiveScheduleEnabled, setArchiveScheduleEnabled] = useState(false);
  const [archiveScheduleStart, setArchiveScheduleStart] = useState(() => toLocalDateTimeInput(new Date(Date.now() - 180 * 24 * 60 * 60 * 1000)));
  const [archiveScheduleAfterDays, setArchiveScheduleAfterDays] = useState('7');
  const [archiveScheduleIntervalDays, setArchiveScheduleIntervalDays] = useState('1');
  const [archiveScheduleWindowDays, setArchiveScheduleWindowDays] = useState('1');
  const [archiveKeepForever, setArchiveKeepForever] = useState(true);
  const [archiveKeepDays, setArchiveKeepDays] = useState('365');
  const [archiveScheduleDeleteSource, setArchiveScheduleDeleteSource] = useState(false);
  const [archiveScheduleDeleteConfirmed, setArchiveScheduleDeleteConfirmed] = useState(false);

  const applyArchiveScheduleDraft = useCallback((schedule: HistorianArchiveSchedule | null | undefined) => {
    if (!schedule) return;
    setArchiveScheduleEnabled(schedule.enabled);
    setArchiveScheduleStart(toLocalDateTimeInput(new Date(schedule.startFromUtc)));
    setArchiveScheduleAfterDays(String(schedule.archiveAfterDays));
    setArchiveScheduleIntervalDays(String(schedule.intervalDays));
    setArchiveScheduleWindowDays(String(schedule.windowDays));
    setArchiveKeepForever(schedule.keepArchivesDays == null);
    setArchiveKeepDays(schedule.keepArchivesDays == null ? '' : String(schedule.keepArchivesDays));
    setArchiveScheduleDeleteSource(schedule.deleteSourceAfterVerification);
  }, []);

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

  useEffect(() => {
    let active = true;
    void loadHistorianStorageAdministrationStatus()
      .then(result => {
        if (!active) return;
        setStorageStatus(result);
        applyArchiveScheduleDraft(result.archive?.schedule);
        setStorageError(null);
        setRetainRawIndefinitely(!result.rawRetentionEnabled);
        setRawRetentionDaysDraft(result.rawRetentionDays == null ? '' : String(result.rawRetentionDays));
        setRawSampleLimitGiBDraft(result.rawSampleStorageLimitBytes == null
          ? ''
          : String(result.rawSampleStorageLimitBytes / GIBIBYTE));
        setRawSampleLimitActionDraft(result.rawSampleStorageLimitAction ?? 'WarnOnly');
      })
      .catch(error => {
        if (active) setStorageError(apiErrorText(error, t));
      });
    return () => { active = false; };
  }, [applyArchiveScheduleDraft, t]);

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

  const onSaveHistorianRetention = async () => {
    if (storageBusy || busy) return;
    const retentionDays = retainRawIndefinitely ? null : Number(rawRetentionDaysDraft);
    if (retentionDays !== null && (!Number.isFinite(retentionDays) || retentionDays <= 0 || retentionDays > 36500)) {
      setStorageError(t.fieldRequired);
      return;
    }
    const rawSizeLimitGiB = rawSampleLimitGiBDraft.trim() === '' ? null : Number(rawSampleLimitGiBDraft);
    if (rawSizeLimitGiB !== null && (!Number.isFinite(rawSizeLimitGiB) || rawSizeLimitGiB <= 0 || rawSizeLimitGiB > 1_000_000)) {
      setStorageError(t.fieldRequired);
      return;
    }
    const rawSampleStorageLimitBytes = rawSizeLimitGiB === null
      ? null
      : Math.round(rawSizeLimitGiB * GIBIBYTE);

    setStorageBusy(true);
    setStorageError(null);
    setNotice(null);
    try {
      await applyHistorianRawRetention({
        rawRetentionDays: retentionDays,
        acknowledgeUnmanagedPolicyReplacement: acknowledgeUnmanagedPolicy,
        allowPotentialDataExpiration: acknowledgePotentialExpiration,
        configureRawSampleStorageLimit: true,
        rawSampleStorageLimitBytes,
        rawSampleStorageLimitAction: rawSampleStorageLimitBytes === null ? 'WarnOnly' : rawSampleLimitActionDraft
      });
      setStorageStatus(await loadHistorianStorageAdministrationStatus());
      setAcknowledgeUnmanagedPolicy(false);
      setAcknowledgePotentialExpiration(false);
      setNotice({ tone: 'success', text: t.retentionSaved });
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const onRefreshHistorianStorage = async () => {
    if (storageBusy) return;
    setStorageBusy(true);
    setStorageError(null);
    try {
      setStorageStatus(await loadHistorianStorageAdministrationStatus());
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const onCreateHistorianArchive = async () => {
    if (storageBusy || busy) return;
    const fromLocal = new Date(archiveFromDraft);
    const toLocal = new Date(archiveToDraft);
    if (!Number.isFinite(fromLocal.getTime()) || !Number.isFinite(toLocal.getTime()) || fromLocal >= toLocal || toLocal > new Date()) {
      setStorageError(t.archiveInvalidRange);
      return;
    }
    if (archiveDeleteSource && !archiveDeleteConfirmed) {
      setStorageError(t.archiveCleanupConfirm);
      return;
    }
    setStorageBusy(true);
    setStorageError(null);
    setNotice(null);
    try {
      await createHistorianArchive({
        fromUtc: fromLocal.toISOString(),
        toUtc: toLocal.toISOString(),
        deleteSourceAfterVerification: archiveDeleteSource,
        confirmDeleteSourceAfterVerification: archiveDeleteConfirmed
      });
      setStorageStatus(await loadHistorianStorageAdministrationStatus());
      setArchiveDeleteSource(false);
      setArchiveDeleteConfirmed(false);
      setNotice({ tone: 'success', text: t.archiveCreated });
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const onSaveHistorianArchiveSchedule = async () => {
    if (storageBusy || busy) return;
    const startLocal = new Date(archiveScheduleStart);
    const archiveAfterDays = Number(archiveScheduleAfterDays);
    const intervalDays = Number(archiveScheduleIntervalDays);
    const windowDays = Number(archiveScheduleWindowDays);
    const keepArchivesDays = archiveKeepForever ? null : Number(archiveKeepDays);
    if (!Number.isFinite(startLocal.getTime()) || startLocal >= new Date() ||
      !Number.isInteger(archiveAfterDays) || archiveAfterDays < 1 || archiveAfterDays > 36500 ||
      !Number.isInteger(intervalDays) || intervalDays < 1 || intervalDays > 3650 ||
      !Number.isInteger(windowDays) || windowDays < 1 || windowDays > 31 ||
      keepArchivesDays !== null && (!Number.isInteger(keepArchivesDays) || keepArchivesDays < 1 || keepArchivesDays > 36500)) {
      setStorageError(t.archiveScheduleInvalid);
      return;
    }
    if (archiveScheduleDeleteSource && !archiveScheduleDeleteConfirmed) {
      setStorageError(t.archiveCleanupConfirm);
      return;
    }
    setStorageBusy(true);
    setStorageError(null);
    setNotice(null);
    try {
      const schedule = await saveHistorianArchiveSchedule({
        enabled: archiveScheduleEnabled,
        startFromUtc: startLocal.toISOString(),
        archiveAfterDays,
        intervalDays,
        windowDays,
        keepArchivesDays,
        deleteSourceAfterVerification: archiveScheduleDeleteSource,
        confirmDeleteSourceAfterVerification: archiveScheduleDeleteConfirmed
      });
      const updated = await loadHistorianStorageAdministrationStatus();
      setStorageStatus(updated);
      applyArchiveScheduleDraft(schedule);
      setArchiveScheduleDeleteConfirmed(false);
      setNotice({ tone: 'success', text: t.archiveScheduleSaved });
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const onResumeHistorianArchive = async (archiveId: string) => {
    if (storageBusy) return;
    setStorageBusy(true);
    setStorageError(null);
    try {
      await resumeHistorianArchive(archiveId);
      setStorageStatus(await loadHistorianStorageAdministrationStatus());
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const onDownloadHistorianArchive = async (archiveId: string) => {
    if (storageBusy) return;
    setStorageBusy(true);
    setStorageError(null);
    try {
      const blob = await downloadHistorianArchive(archiveId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${archiveId.replaceAll('-', '')}.scadhist`;
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const onRestoreHistorianArchive = async () => {
    if (storageBusy || !archiveRestoreFile) return;
    if (archiveRestoreFile.size > MAX_ARCHIVE_UPLOAD_BYTES) {
      setStorageError(t.archiveUploadLimit);
      return;
    }
    setStorageBusy(true);
    setStorageError(null);
    setNotice(null);
    try {
      const result = await restoreHistorianArchive(archiveRestoreFile);
      setStorageStatus(await loadHistorianStorageAdministrationStatus());
      setNotice({
        tone: 'success',
        text: `${t.archiveRestoreDone}: ${result.samplesRestored.toLocaleString(locale)}. ${result.segmentsAlreadyRestored.toLocaleString(locale)} ${t.archiveRestoreAlready}`
      });
      setArchiveRestoreFile(null);
    } catch (error) {
      setStorageError(apiErrorText(error, t));
    } finally {
      setStorageBusy(false);
    }
  };

  const archiveStateLabel = (job: HistorianArchiveJob) => {
    switch (job.state) {
      case 'Queued': return t.archiveQueued;
      case 'Running': return t.archiveRunning;
      case 'Blocked': return t.archiveBlocked;
      case 'Completed': return t.archiveCompleted;
    }
  };

  const hasActiveArchiveJobs = Boolean(storageStatus?.archive?.jobs.some(job => job.state === 'Queued' || job.state === 'Running'));
  const shouldPollArchiveStatus = hasActiveArchiveJobs || Boolean(storageStatus?.archive?.schedule?.enabled);
  useEffect(() => {
    if (!shouldPollArchiveStatus) return;
    let active = true;
    const timer = window.setInterval(() => {
      void loadHistorianStorageAdministrationStatus()
        .then(result => { if (active) setStorageStatus(result); })
        .catch(() => { /* keep the last confirmed job status visible while the API is unavailable */ });
    }, hasActiveArchiveJobs ? 15_000 : 60_000);
    return () => { active = false; window.clearInterval(timer); };
  }, [hasActiveArchiveJobs, shouldPollArchiveStatus]);

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

  const onConnectExisting = () => run('connect-existing', async () => {
    if (!profileRequest || !remoteCoreReady) {
      setNotice({ tone: 'warning', text: t.fieldRequired });
      return;
    }
    const request = profileRequest;
    setDraft(current => ({
      ...current,
      primary: { ...current.primary, password: '' },
      historian: { ...current.historian, password: '' }
    }));
    const result = await connectExistingDatabase(request);
    setStatus(result.status);
    setPending(null);
    if (result.succeeded) resetTransientEditor();
    setNotice({
      tone: result.succeeded ? 'success' : 'danger',
      text: result.succeeded ? t.restartToFinish : (result.diagnostic ?? t.existingDatabaseMismatch)
    });
    setConfirmation(null);
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

  return <div className="eng-body db-topology-engineering-body" data-testid="database-topology-engineering-shell">
    <aside className="eng-sidebar" aria-label={engineeringText('app.engineering')}>
      <nav className="eng-nav" aria-label={engineeringText('app.engineering')} data-testid="database-topology-navigation">
        {engineeringNavigation.map(group => <div className="eng-nav-group" key={group.label}>
          <span className="eng-nav-label">{engineeringText(group.label)}</span>
          {group.items.map(item => {
            const href = engineeringNavigationPath(item.id);
            const active = item.id === 'databaseTopology';
            return <a key={item.id} href={href} className={active ? 'active' : undefined} aria-current={active ? 'page' : undefined}>
              <i aria-hidden="true">{item.icon}</i>
              <span>{item.literalLabel ? item.literalLabel[locale] : item.label ? engineeringText(item.label) : 'Scripts'}</span>
            </a>;
          })}
        </div>)}
      </nav>
    </aside>
    <main className="db-topology-shell" data-testid="database-topology-app">
    <header className="db-topology-header">
      <div><h1>{t.title}</h1><details className="app-page-help"><summary>{locale === 'pt-BR' ? 'Ajuda' : locale === 'es' ? 'Ayuda' : 'Help'}</summary><p>{t.subtitle}</p><a href="/help?topic=database.topology">{locale === 'pt-BR' ? 'Configuração, campos e migração passo a passo' : locale === 'es' ? 'Configuración, campos y migración paso a paso' : 'Configuration, fields and migration step by step'}</a></details></div>
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

    <section className="db-topology-panel" aria-labelledby="historian-storage-title" data-testid="historian-storage-panel">
      <div className="db-topology-section-heading">
        <div><h2 id="historian-storage-title">{t.storageTitle}</h2><p>{t.storageHelp}</p></div>
        <button type="button" className="db-topology-secondary" disabled={storageBusy} onClick={() => void onRefreshHistorianStorage()}>{storageBusy ? t.working : t.refresh}</button>
      </div>
      {storageError ? <div role="alert" className="db-topology-alert db-topology-alert--danger">{storageError}</div> : null}
      {!storageStatus ? <div className="db-topology-locked-state">{t.loadingStatus}</div> : !storageStatus.supported ? <div className="db-topology-locked-state">{t.storageNotSupported}</div> : <>
        <div className="db-topology-status-strip db-topology-storage-usage">
          <div><span>{t.databaseUsage}</span><strong>{formatStorageBytes(storageStatus.usage?.databaseBytes, locale, t.storageUnknown)}</strong></div>
          <div><span>{t.historianUsage}</span><strong>{formatStorageBytes(storageStatus.usage?.historianBytes, locale, t.storageUnknown)}</strong></div>
          <div><span>{t.rawUsage}</span><strong>{formatStorageBytes(storageStatus.usage?.rawSamplesBytes, locale, t.storageUnknown)}</strong></div>
          <div><span>{t.rawSizeLimit}</span><strong>{formatStorageBytes(storageStatus.rawSampleStorageLimitBytes, locale, t.noSizeLimit)}</strong></div>
          <div><span>{t.aggregateUsage}</span><strong>{formatStorageBytes(storageStatus.usage?.aggregateBytes, locale, t.storageUnknown)}</strong></div>
          <div><span>{t.filesystemUsage}</span><strong>{formatStorageBytes(storageStatus.usage?.filesystemFreeBytes, locale, t.storageUnknown)}</strong></div>
          <div><span>{t.growthEstimate}</span><strong>{formatStorageBytes(storageStatus.usage?.estimatedDailyGrowthBytes, locale, t.storageUnknown)}</strong></div>
        </div>
        {storageStatus.usage?.measuredAtUtc ? <p className="db-topology-muted-note">{t.storageMeasuredAt}: {new Date(storageStatus.usage.measuredAtUtc).toLocaleString(locale)}</p> : null}
        <p className="db-topology-storage-policy-state" data-testid="historian-policy-state">
          <strong>{storageStatus.policyManaged ? t.policyManaged : t.policyUnmanaged}</strong>
        </p>
        {storageStatus.rawSampleLimitExceeded ? <div role="status" className="db-topology-alert db-topology-alert--warning">{t.limitExceeded}</div> : null}
        <div className="db-topology-storage-form">
          <h3>{t.rawRetention}</h3>
          <label className="db-topology-historian-toggle">
            <input type="checkbox" checked={retainRawIndefinitely} disabled={storageBusy} onChange={event => setRetainRawIndefinitely(event.target.checked)} />
            <span>{t.noExpiry}</span>
          </label>
          {!retainRawIndefinitely ? <label className="db-topology-storage-days">
            <span>{t.retentionDays}</span>
            <input type="number" min="1" max="36500" step="1" placeholder={t.retentionPlaceholder} value={rawRetentionDaysDraft} disabled={storageBusy} onChange={event => setRawRetentionDaysDraft(event.target.value)} />
          </label> : null}
          <h3>{t.rawSizeLimit}</h3>
          <label className="db-topology-storage-days">
            <span>{t.rawSizeLimit}</span>
            <div className="db-topology-storage-limit-input">
              <input type="number" min="0.01" max="1000000" step="0.01" placeholder={t.rawSizeLimitPlaceholder} value={rawSampleLimitGiBDraft} disabled={storageBusy} onChange={event => setRawSampleLimitGiBDraft(event.target.value)} />
              <span>{t.gibibytes}</span>
            </div>
          </label>
          {rawSampleLimitGiBDraft.trim() !== '' ? <label className="db-topology-storage-days">
            <span>{t.limitAction}</span>
            <select value={rawSampleLimitActionDraft} disabled={storageBusy} onChange={event => setRawSampleLimitActionDraft(event.target.value as RawSampleLimitAction)}>
              <option value="WarnOnly">{t.warnOnly}</option>
              <option value="DeleteOldestCompleteChunks">{t.deleteOldestChunks}</option>
            </select>
          </label> : null}
          {rawSampleLimitActionDraft === 'DeleteOldestCompleteChunks' && rawSampleLimitGiBDraft.trim() !== '' ? <p className="db-topology-muted-note">{t.chunkLimitDetail}</p> : null}
          {!storageStatus.policyManaged ? <label className="db-topology-historian-toggle db-topology-storage-confirmation">
            <input type="checkbox" checked={acknowledgeUnmanagedPolicy} disabled={storageBusy} onChange={event => setAcknowledgeUnmanagedPolicy(event.target.checked)} />
            <span>{t.adoptUnmanaged}</span>
          </label> : null}
          {!retainRawIndefinitely || rawSampleLimitActionDraft === 'DeleteOldestCompleteChunks' && rawSampleLimitGiBDraft.trim() !== '' ? <label className="db-topology-historian-toggle db-topology-storage-confirmation">
            <input type="checkbox" checked={acknowledgePotentialExpiration} disabled={storageBusy} onChange={event => setAcknowledgePotentialExpiration(event.target.checked)} />
            <span>{t.confirmExpiration}</span>
          </label> : null}
          <p className="db-topology-muted-note">{t.storagePolicyDetail}</p>
          <div className="db-topology-actions">
            <button type="button" className="db-topology-primary-action" disabled={storageBusy || Boolean(busy) || (!storageStatus.policyManaged && !acknowledgeUnmanagedPolicy) || ((!retainRawIndefinitely || rawSampleLimitActionDraft === 'DeleteOldestCompleteChunks' && rawSampleLimitGiBDraft.trim() !== '') && !acknowledgePotentialExpiration)} onClick={() => void onSaveHistorianRetention()}>
              {storageBusy ? t.working : t.saveRetention}
            </button>
          </div>
        </div>
        {storageStatus.archive?.supported ? <div className="db-topology-storage-form" data-testid="historian-archive-panel">
          <h3>{t.archiveTitle}</h3>
          <p className="db-topology-muted-note">{t.archiveHelp}</p>
          <div className="db-topology-status-strip">
            <div><span>{t.archiveDestination}</span><strong>{storageStatus.archive.destination || t.storageUnknown}</strong></div>
          </div>
          <p className="db-topology-muted-note">{t.archiveDestinationHelp}</p>
          <section className="db-topology-archive-schedule" data-testid="historian-archive-schedule">
            <h3>{t.archiveScheduleTitle}</h3>
            <p className="db-topology-muted-note">{t.archiveScheduleHelp}</p>
            {storageStatus.archive.schedule ? <p className="db-topology-muted-note">
              {storageStatus.archive.schedule.enabled ? t.archiveScheduleEnabled : t.archiveScheduleDisabled}
              {' · '}{t.archiveScheduleCursor}: {new Date(storageStatus.archive.schedule.nextFromUtc).toLocaleString(locale)}
              {' · '}{t.archiveScheduleNextRun}: {new Date(storageStatus.archive.schedule.nextRunAtUtc).toLocaleString(locale)}
            </p> : null}
            <label className="db-topology-historian-toggle">
              <input type="checkbox" checked={archiveScheduleEnabled} disabled={storageBusy} onChange={event => setArchiveScheduleEnabled(event.target.checked)} />
              <span>{t.archiveScheduleEnabled}</span>
            </label>
            <label className="db-topology-storage-days">
              <span>{t.archiveScheduleStart}</span>
              <input type="datetime-local" value={archiveScheduleStart} disabled={storageBusy} onChange={event => setArchiveScheduleStart(event.target.value)} />
            </label>
            <label className="db-topology-storage-days">
              <span>{t.archiveScheduleAfterDays}</span>
              <input type="number" min="1" max="36500" step="1" value={archiveScheduleAfterDays} disabled={storageBusy} onChange={event => setArchiveScheduleAfterDays(event.target.value)} />
            </label>
            <label className="db-topology-storage-days">
              <span>{t.archiveScheduleInterval}</span>
              <input type="number" min="1" max="3650" step="1" value={archiveScheduleIntervalDays} disabled={storageBusy} onChange={event => setArchiveScheduleIntervalDays(event.target.value)} />
            </label>
            <label className="db-topology-storage-days">
              <span>{t.archiveScheduleWindow}</span>
              <input type="number" min="1" max="31" step="1" value={archiveScheduleWindowDays} disabled={storageBusy} onChange={event => setArchiveScheduleWindowDays(event.target.value)} />
            </label>
            <label className="db-topology-historian-toggle">
              <input type="checkbox" checked={archiveKeepForever} disabled={storageBusy} onChange={event => setArchiveKeepForever(event.target.checked)} />
              <span>{t.archiveKeepForever}</span>
            </label>
            {!archiveKeepForever ? <label className="db-topology-storage-days">
              <span>{t.archiveKeepDays}</span>
              <input type="number" min="1" max="36500" step="1" value={archiveKeepDays} disabled={storageBusy} onChange={event => setArchiveKeepDays(event.target.value)} />
            </label> : null}
            <label className="db-topology-historian-toggle">
              <input type="checkbox" checked={archiveScheduleDeleteSource} disabled={storageBusy} onChange={event => {
                setArchiveScheduleDeleteSource(event.target.checked);
                if (!event.target.checked) setArchiveScheduleDeleteConfirmed(false);
              }} />
              <span>{t.archiveScheduleCleanup}</span>
            </label>
            {archiveScheduleDeleteSource ? <label className="db-topology-historian-toggle db-topology-storage-confirmation">
              <input type="checkbox" checked={archiveScheduleDeleteConfirmed} disabled={storageBusy} onChange={event => setArchiveScheduleDeleteConfirmed(event.target.checked)} />
              <span>{t.archiveScheduleCleanupConfirm}</span>
            </label> : null}
            <p className="db-topology-muted-note">{t.archiveScheduleRotationHelp}</p>
            <div className="db-topology-actions">
              <button type="button" className="db-topology-primary-action" disabled={storageBusy || Boolean(busy) || (archiveScheduleDeleteSource && !archiveScheduleDeleteConfirmed)} onClick={() => void onSaveHistorianArchiveSchedule()}>
                {storageBusy ? t.working : t.archiveScheduleSave}
              </button>
            </div>
          </section>
          <label className="db-topology-storage-days">
            <span>{t.archiveFrom}</span>
            <input type="datetime-local" value={archiveFromDraft} disabled={storageBusy} onChange={event => setArchiveFromDraft(event.target.value)} />
          </label>
          <label className="db-topology-storage-days">
            <span>{t.archiveTo}</span>
            <input type="datetime-local" value={archiveToDraft} disabled={storageBusy} onChange={event => setArchiveToDraft(event.target.value)} />
          </label>
          <label className="db-topology-historian-toggle">
            <input type="checkbox" checked={archiveDeleteSource} disabled={storageBusy} onChange={event => {
              setArchiveDeleteSource(event.target.checked);
              if (!event.target.checked) setArchiveDeleteConfirmed(false);
            }} />
            <span>{t.archiveCleanupSource}</span>
          </label>
          {archiveDeleteSource ? <label className="db-topology-historian-toggle db-topology-storage-confirmation">
            <input type="checkbox" checked={archiveDeleteConfirmed} disabled={storageBusy} onChange={event => setArchiveDeleteConfirmed(event.target.checked)} />
            <span>{t.archiveCleanupConfirm}</span>
          </label> : <p className="db-topology-muted-note">{t.archiveCleanupWarning}</p>}
          <p className="db-topology-muted-note">{t.archivePackageFormat}</p>
          <div className="db-topology-actions">
            <button type="button" className="db-topology-primary-action" disabled={storageBusy || Boolean(busy) || (archiveDeleteSource && !archiveDeleteConfirmed)} onClick={() => void onCreateHistorianArchive()}>
              {storageBusy ? t.working : t.archiveCreate}
            </button>
          </div>

          <h3>{t.archiveRestore}</h3>
          <label className="db-topology-storage-days">
            <span>{t.archiveSelectFile}</span>
            <input type="file" accept=".scadhist,application/vnd.scadivo.historian-archive" disabled={storageBusy} onChange={event => setArchiveRestoreFile(event.target.files?.[0] ?? null)} />
          </label>
          <p className="db-topology-muted-note">{t.archiveUploadLimit}</p>
          <div className="db-topology-actions">
            <button type="button" className="db-topology-secondary" disabled={storageBusy || !archiveRestoreFile} onClick={() => void onRestoreHistorianArchive()}>
              {storageBusy ? t.working : t.archiveRestore}
            </button>
          </div>

          <h3>{t.archiveState}</h3>
          {storageStatus.archive.jobs.length === 0 ? <p className="db-topology-muted-note">{t.archiveNoJobs}</p> : <ul className="db-topology-archive-list">
            {storageStatus.archive.jobs.map(job => <li key={job.archiveId} data-testid="historian-archive-job">
              <strong>{archiveStateLabel(job)}</strong>
              <span>{new Date(job.fromUtc).toLocaleString(locale)} – {new Date(job.toUtc).toLocaleString(locale)}</span>
              <span>{t.archiveSamples}: {job.samplesArchived.toLocaleString(locale)}</span>
              {job.archiveLengthBytes != null ? <span>{t.archiveSize}: {formatStorageBytes(job.archiveLengthBytes, locale, t.storageUnknown)}</span> : null}
              {job.failureCode ? <span>{t.archiveFailure}: {job.failureCode}</span> : null}
              <div className="db-topology-actions">
                {job.state === 'Blocked' ? <button type="button" className="db-topology-secondary" disabled={storageBusy} onClick={() => void onResumeHistorianArchive(job.archiveId)}>{t.archiveResume}</button> : null}
                {job.state === 'Completed' && job.archiveFileAvailable ? <button type="button" className="db-topology-secondary" disabled={storageBusy} onClick={() => void onDownloadHistorianArchive(job.archiveId)}>{t.archiveDownload}</button> : null}
                {job.state === 'Completed' && !job.archiveFileAvailable ? <span className="db-topology-muted-note">{t.archiveRotated}</span> : null}
              </div>
            </li>)}
          </ul>}
        </div> : null}
      </>}
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
          <button type="button" className="db-topology-secondary" disabled={Boolean(busy) || configurationLocked || compatibility?.compatible !== true || !remoteCoreReady} onClick={() => setConfirmation('connect-existing')}>{busy === 'connect-existing' ? t.working : t.connectExisting}</button>
        </div>
        {compatibility?.compatible ? <p className="db-topology-muted-note">{t.connectExistingConfirmBody}</p> : null}

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
          {confirmation === 'cutover' ? t.cutoverConfirmTitle : confirmation === 'connect-existing' ? t.connectExistingConfirmTitle : confirmation === 'return-local' ? t.returnToLocalConfirmTitle : t.rollbackConfirmTitle}
        </h2>
        <p>
          {confirmation === 'cutover' ? t.cutoverConfirmBody : confirmation === 'connect-existing' ? t.connectExistingConfirmBody : confirmation === 'return-local' ? t.returnToLocalConfirmBody : t.rollbackConfirmBody}
        </p>
        <dl>
          <div><dt>{t.source}</dt><dd>{endpointLabel(status?.activeTopology)}</dd></div>
          <div><dt>{t.target}</dt><dd>{confirmation === 'cutover' || confirmation === 'connect-existing' ? `${draft.primary.host}:${draft.primary.port}/${draft.primary.database}` : confirmation === 'return-local' ? t.localManaged : endpointLabel(status?.previousTopology)}</dd></div>
          <div><dt>{t.preserved}</dt><dd>{confirmation === 'return-local' ? t.returnToLocalPreserveRemote : confirmation === 'connect-existing' ? t.connectExistingConfirmBody : t.preserveLocal}</dd></div>
        </dl>
        <div className="db-topology-actions">
          <button type="button" className="db-topology-secondary" onClick={() => setConfirmation(null)}>{t.cancel}</button>
          <button
            type="button"
            className="db-topology-danger-action"
            onClick={confirmation === 'cutover' ? onCommit : confirmation === 'connect-existing' ? onConnectExisting : confirmation === 'return-local' ? onReturnLocal : onRollback}
          >{busy ? t.working : t.confirm}</button>
        </div>
      </section>
    </div> : null}
    </main>
  </div>;
}
