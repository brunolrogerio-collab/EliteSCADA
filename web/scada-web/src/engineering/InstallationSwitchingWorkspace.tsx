import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { saveEngineeringRevision } from './engineeringLifecycleApi';
import {
  exportProjectPackage,
  saveProjectDownload,
  triggerBrowserDownload
} from './projectPortabilityApi';
import type { EngineeringLocale } from './i18n';
import './installation-switching.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

type LicenseAction = 'Keep' | 'Remove' | 'Replace';
type Phase = 'idle' | 'validating' | 'detaching' | 'neutral';

type InstallationDetachRequest = {
  acknowledgeUnsavedWorking: boolean;
  acknowledgeApplicationRemoval: boolean;
  acknowledgeAuthorityRemoval: boolean;
  acknowledgeHistorianPreserved: boolean;
  licenseAction: 0 | 1 | 2;
  replacementLicenseCode: string | null;
};

type InstallationDetachPreflight = {
  canDetach: boolean;
  projectKey?: string | null;
  projectName?: string | null;
  workingRevision?: number | null;
  publishedRevision?: number | null;
  activeRevision?: number | null;
  unsavedWorking: boolean;
  runtimeActive: boolean;
  authorityState: string;
  authorityEpoch: number;
  licenseState: string;
  engineeringLocked: boolean;
  historianPreservedByDefault: boolean;
  applicationExportRecommended: boolean;
  authorityExportRecommended: boolean;
  requiredAcknowledgements: string[];
  blockers: string[];
};

type InstallationDetachResult = {
  detached: boolean;
  stage: string;
  detachedProjectKey?: string | null;
  detachedRevision?: number | null;
  authorityEpoch: number;
  licenseState: string;
  licenseOutcome: string;
  historianPreserved: boolean;
  signInRequired: boolean;
  issues: string[];
};

type Diagnostic = {
  status?: number;
  stage?: string;
  details: string[];
};

class InstallationApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly data: unknown
  ) {
    super(message);
    this.name = 'InstallationApiError';
  }
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json'
    },
    body: JSON.stringify(body)
  });
  const text = await response.text();
  let data: unknown;
  try {
    data = text ? JSON.parse(text) : {};
  } catch {
    data = undefined;
  }
  if (!response.ok) {
    const message = data && typeof data === 'object' && 'error' in data && typeof data.error === 'string'
      ? data.error
      : `HTTP ${response.status}`;
    throw new InstallationApiError(message, response.status, data);
  }
  return (data ?? {}) as T;
}

function diagnosticFrom(reason: unknown): Diagnostic {
  if (reason instanceof InstallationApiError) {
    const data = reason.data && typeof reason.data === 'object' ? reason.data as Record<string, unknown> : null;
    const issues = Array.isArray(data?.issues)
      ? data!.issues.filter((item): item is string => typeof item === 'string')
      : [];
    const blockers = Array.isArray(data?.blockers)
      ? data!.blockers.filter((item): item is string => typeof item === 'string')
      : [];
    return {
      status: reason.status,
      stage: typeof data?.stage === 'string' ? data.stage : undefined,
      details: [...issues, ...blockers, reason.message].filter((item, index, all) => all.indexOf(item) === index)
    };
  }
  return { details: [reason instanceof Error ? reason.message : String(reason)] };
}

function emptyRequest(): InstallationDetachRequest {
  return {
    acknowledgeUnsavedWorking: false,
    acknowledgeApplicationRemoval: false,
    acknowledgeAuthorityRemoval: false,
    acknowledgeHistorianPreserved: false,
    licenseAction: 0,
    replacementLicenseCode: null
  };
}

export function InstallationSwitchingWorkspace({
  locale,
  onWorkspaceChanged
}: {
  locale: EngineeringLocale;
  onWorkspaceChanged?: () => Promise<void> | void;
}) {
  const t = useMemo(() => installationCopy(locale), [locale]);
  const [review, setReview] = useState<InstallationDetachPreflight | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [phase, setPhase] = useState<Phase>('idle');
  const [message, setMessage] = useState<string | null>(null);
  const [diagnostic, setDiagnostic] = useState<Diagnostic | null>(null);
  const [confirming, setConfirming] = useState(false);

  const [applicationExported, setApplicationExported] = useState(false);
  const [authorityExported, setAuthorityExported] = useState(false);
  const [authorityPassword, setAuthorityPassword] = useState('');
  const [skipApplicationExport, setSkipApplicationExport] = useState(false);
  const [skipAuthorityExport, setSkipAuthorityExport] = useState(false);

  const [ackUnsavedWorking, setAckUnsavedWorking] = useState(false);
  const [ackApplication, setAckApplication] = useState(false);
  const [ackAuthority, setAckAuthority] = useState(false);
  const [ackHistorian, setAckHistorian] = useState(false);
  const [licenseAction, setLicenseAction] = useState<LicenseAction>('Keep');
  const [replacementLicenseCode, setReplacementLicenseCode] = useState('');

  const loadReview = useCallback(async () => {
    setLoading(true);
    setDiagnostic(null);
    try {
      setReview(await postJson<InstallationDetachPreflight>('/api/installation/detach/preflight', emptyRequest()));
    } catch (reason) {
      setDiagnostic(diagnosticFrom(reason));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadReview(); }, [loadReview]);

  const clearFeedback = () => {
    setMessage(null);
    setDiagnostic(null);
  };

  async function saveWorking() {
    if (!review?.projectKey || !review.projectName) return;
    setBusy('save');
    clearFeedback();
    try {
      const saved = await saveEngineeringRevision(review.projectKey, review.projectName);
      setApplicationExported(false);
      setSkipApplicationExport(false);
      setAckUnsavedWorking(false);
      setMessage(t.saved.replace('{revision}', String(saved.revision)));
      await loadReview();
      await onWorkspaceChanged?.();
    } catch (reason) {
      setDiagnostic(diagnosticFrom(reason));
    } finally {
      setBusy(null);
    }
  }

  async function exportApplication() {
    if (!review?.projectKey || !review.projectName) return;
    setBusy('application-export');
    clearFeedback();
    try {
      const result = await saveProjectDownload(await exportProjectPackage(review.projectKey, review.projectName));
      if (result === 'cancelled') return;
      setApplicationExported(true);
      setSkipApplicationExport(false);
      setMessage(t.applicationExported);
    } catch (reason) {
      setDiagnostic(diagnosticFrom(reason));
    } finally {
      setBusy(null);
    }
  }

  async function exportAuthority() {
    if (!authorityPassword) return;
    setBusy('authority-export');
    clearFeedback();
    try {
      const response = await postJson<{ backup: string }>('/api/auth/authority-backup/export', {
        password: authorityPassword
      });
      if (!response.backup) throw new Error(t.authorityExportFailed);
      triggerBrowserDownload({
        blob: new Blob([response.backup], { type: 'application/json' }),
        filename: 'elitescada-authority-backup.json'
      });
      setAuthorityPassword('');
      setAuthorityExported(true);
      setSkipAuthorityExport(false);
      setMessage(t.authorityExported);
    } catch (reason) {
      setDiagnostic(diagnosticFrom(reason));
    } finally {
      setBusy(null);
    }
  }

  function currentRequest(): InstallationDetachRequest {
    return {
      acknowledgeUnsavedWorking: !review?.unsavedWorking || ackUnsavedWorking,
      acknowledgeApplicationRemoval: ackApplication,
      acknowledgeAuthorityRemoval: ackAuthority,
      acknowledgeHistorianPreserved: ackHistorian,
      licenseAction: licenseAction === 'Keep' ? 0 : licenseAction === 'Remove' ? 1 : 2,
      replacementLicenseCode: licenseAction === 'Replace' ? replacementLicenseCode.trim() : null
    };
  }

  async function detach() {
    if (!review) return;
    clearFeedback();
    setPhase('validating');
    setBusy('detach');
    const request = currentRequest();
    try {
      const validated = await postJson<InstallationDetachPreflight>('/api/installation/detach/preflight', request);
      setReview(validated);
      if (!validated.canDetach) {
        setPhase('idle');
        setDiagnostic({
          stage: 'preflight',
          details: [...validated.blockers, ...validated.requiredAcknowledgements]
        });
        return;
      }

      setPhase('detaching');
      const result = await postJson<InstallationDetachResult>('/api/installation/detach', request);
      if (!result.detached) {
        setPhase('idle');
        setDiagnostic({ stage: result.stage, details: result.issues.length ? result.issues : [t.detachFailed] });
        return;
      }

      setPhase('neutral');
      setMessage(t.neutralReached);
      window.location.assign('/');
    } catch (reason) {
      setPhase('idle');
      setDiagnostic(diagnosticFrom(reason));
    } finally {
      setBusy(null);
    }
  }

  if (loading && !review) {
    return <section className="installation-switching installation-switching--loading" aria-label={t.title}>{t.loading}</section>;
  }

  if (!review) {
    return (
      <section className="installation-switching" aria-label={t.title}>
        <header><div><span className="installation-switching__eyebrow">{t.eyebrow}</span><h2>{t.title}</h2></div></header>
        <p className="installation-switching__error" role="alert">{t.loadFailed}</p>
        <button type="button" onClick={() => void loadReview()}>{t.retry}</button>
        <DiagnosticPanel diagnostic={diagnostic} t={t} />
      </section>
    );
  }

  const unpublishedWorking =
    review.workingRevision != null &&
    review.workingRevision !== review.publishedRevision;
  const exportsReady =
    (applicationExported || skipApplicationExport) &&
    (authorityExported || skipAuthorityExport);
  const confirmationsReady =
    (!review.unsavedWorking || ackUnsavedWorking) &&
    ackApplication &&
    ackAuthority &&
    ackHistorian;
  const licenseReady = licenseAction !== 'Replace' || replacementLicenseCode.trim().length > 0;
  const canDetach =
    review.blockers.length === 0 &&
    exportsReady &&
    confirmationsReady &&
    licenseReady &&
    phase === 'idle' &&
    !busy;

  return (
    <section className="installation-switching" aria-label={t.title} data-testid="installation-switching">
      <header className="installation-switching__header">
        <div>
          <span className="installation-switching__eyebrow">{t.eyebrow}</span>
          <h2>{t.title}</h2>
          <p>{t.description}</p>
        </div>
        <button type="button" onClick={() => void loadReview()} disabled={Boolean(busy) || loading}>
          {loading ? t.refreshing : t.refresh}
        </button>
      </header>

      {message && <p className="installation-switching__notice" role="status">{message}</p>}
      {diagnostic && <p className="installation-switching__error" role="alert">{t.operationFailed}</p>}
      <DiagnosticPanel diagnostic={diagnostic} t={t} />

      <div className="installation-switching__facts" data-testid="installation-current-state">
        <Fact label={t.application} value={review.projectName || t.unnamed} detail={review.projectKey || t.noProject} />
        <Fact
          label={t.working}
          value={review.unsavedWorking ? t.dirty : t.clean}
          detail={revisionText(review.workingRevision, t.none)}
          attention={review.unsavedWorking || unpublishedWorking}
        />
        <Fact label={t.published} value={revisionText(review.publishedRevision, t.none)} detail={unpublishedWorking ? t.unpublishedWarning : t.publishedHint} attention={unpublishedWorking} />
        <Fact label={t.active} value={revisionText(review.activeRevision, t.none)} detail={review.runtimeActive ? t.runtimeActive : t.runtimeInactive} />
        <Fact label={t.authority} value={authorityLabel(review.authorityState, t)} detail={t.authorityProtected} />
        <Fact label={t.license} value={licenseLabel(review.licenseState, t)} detail={t.machineLevel} />
        <Fact label={t.historian} value={review.historianPreservedByDefault ? t.preserved : t.reviewRequired} detail={t.historianHint} attention={!review.historianPreservedByDefault} />
        <Fact label={t.engineeringProtection} value={review.engineeringLocked ? t.locked : t.unlocked} detail={t.engineeringProtectionHint} />
      </div>

      {(review.unsavedWorking || unpublishedWorking) && (
        <div className="installation-switching__warning" role="status">
          <strong>{t.workingAttention}</strong>
          <span>{review.unsavedWorking ? t.unsavedWarning : t.unpublishedWarningLong}</span>
          {review.unsavedWorking && review.projectKey && review.projectName && (
            <button type="button" onClick={() => void saveWorking()} disabled={Boolean(busy)}>
              {busy === 'save' ? t.saving : t.saveRevision}
            </button>
          )}
        </div>
      )}

      <div className="installation-switching__preservation">
        <article>
          <div>
            <span>{t.recommended}</span>
            <h3>{t.applicationBackup}</h3>
            <p>{t.applicationBackupHint}</p>
          </div>
          <button type="button" onClick={() => void exportApplication()} disabled={Boolean(busy) || !review.projectKey || !review.projectName}>
            {busy === 'application-export' ? t.exporting : applicationExported ? t.exported : t.exportApplication}
          </button>
        </article>

        <article>
          <div>
            <span>{t.recommended}</span>
            <h3>{t.authorityBackup}</h3>
            <p>{t.authorityBackupHint}</p>
          </div>
          <label>
            <span>{t.authorityPassword}</span>
            <input
              type="password"
              autoComplete="new-password"
              value={authorityPassword}
              onChange={event => {
                setAuthorityPassword(event.target.value);
                setAuthorityExported(false);
              }}
              disabled={Boolean(busy)}
            />
          </label>
          <button type="button" onClick={() => void exportAuthority()} disabled={Boolean(busy) || !authorityPassword}>
            {busy === 'authority-export' ? t.exporting : authorityExported ? t.exported : t.exportAuthority}
          </button>
        </article>
      </div>

      <div className="installation-switching__historian-note">
        <strong>{t.historianNotDeleted}</strong>
        <span>{t.historianDetail}</span>
      </div>

      {!confirming ? (
        <div className="installation-switching__entry">
          <div>
            <strong>{t.switchTitle}</strong>
            <p>{t.switchHint}</p>
          </div>
          <button type="button" className="installation-switching__danger" onClick={() => {
            clearFeedback();
            setConfirming(true);
          }}>
            {t.openDetach}
          </button>
        </div>
      ) : (
        <div className="installation-switching__confirmation" role="dialog" aria-modal="false" aria-labelledby="installation-detach-title">
          <div className="installation-switching__confirmation-heading">
            <div>
              <span className="installation-switching__eyebrow">{t.confirmEyebrow}</span>
              <h3 id="installation-detach-title">{t.confirmTitle}</h3>
              <p>{t.confirmDescription}</p>
            </div>
            <button type="button" onClick={() => setConfirming(false)} disabled={Boolean(busy)}>{t.cancel}</button>
          </div>

          <fieldset>
            <legend>{t.backupDecision}</legend>
            <Check
              checked={applicationExported || skipApplicationExport}
              disabled={applicationExported || Boolean(busy)}
              onChange={setSkipApplicationExport}
              label={applicationExported ? t.applicationBackedUp : t.continueWithoutApplicationBackup}
            />
            <Check
              checked={authorityExported || skipAuthorityExport}
              disabled={authorityExported || Boolean(busy)}
              onChange={setSkipAuthorityExport}
              label={authorityExported ? t.authorityBackedUp : t.continueWithoutAuthorityBackup}
            />
          </fieldset>

          <fieldset>
            <legend>{t.consequences}</legend>
            {review.unsavedWorking && (
              <Check checked={ackUnsavedWorking} disabled={Boolean(busy)} onChange={setAckUnsavedWorking} label={t.ackUnsaved} />
            )}
            <Check checked={ackApplication} disabled={Boolean(busy)} onChange={setAckApplication} label={t.ackApplication} />
            <Check checked={ackAuthority} disabled={Boolean(busy)} onChange={setAckAuthority} label={t.ackAuthority} />
            <Check checked={ackHistorian} disabled={Boolean(busy)} onChange={setAckHistorian} label={t.ackHistorian} />
          </fieldset>

          <fieldset>
            <legend>{t.licenseDecision}</legend>
            <Radio checked={licenseAction === 'Keep'} disabled={Boolean(busy)} onChange={() => setLicenseAction('Keep')} title={t.keepLicense} description={t.keepLicenseHint} />
            <Radio checked={licenseAction === 'Remove'} disabled={Boolean(busy)} onChange={() => setLicenseAction('Remove')} title={t.removeLicense} description={t.removeLicenseHint} />
            <Radio checked={licenseAction === 'Replace'} disabled={Boolean(busy)} onChange={() => setLicenseAction('Replace')} title={t.replaceLicense} description={t.replaceLicenseHint} />
            {licenseAction === 'Replace' && (
              <label className="installation-switching__license-code">
                <span>{t.replacementCode}</span>
                <textarea
                  rows={4}
                  value={replacementLicenseCode}
                  onChange={event => setReplacementLicenseCode(event.target.value)}
                  disabled={Boolean(busy)}
                />
              </label>
            )}
          </fieldset>

          {review.blockers.length > 0 && (
            <div className="installation-switching__blocking">
              <strong>{t.blocked}</strong>
              <ul>{review.blockers.map(item => <li key={item}>{item}</li>)}</ul>
            </div>
          )}

          {phase !== 'idle' && (
            <div className="installation-switching__progress" role="status" data-testid="installation-detach-progress">
              <span className="installation-switching__spinner" aria-hidden="true" />
              <div>
                <strong>{phase === 'validating' ? t.validating : phase === 'detaching' ? t.detaching : t.neutralReached}</strong>
                <span>{phase === 'detaching' ? t.transactionHint : t.validationHint}</span>
              </div>
            </div>
          )}

          <div className="installation-switching__final-actions">
            <button type="button" onClick={() => setConfirming(false)} disabled={Boolean(busy)}>{t.back}</button>
            <button
              type="button"
              className="installation-switching__danger"
              data-testid="installation-detach-confirm"
              disabled={!canDetach}
              onClick={() => void detach()}
            >
              {phase === 'validating' ? t.validating : phase === 'detaching' ? t.detaching : t.detachNow}
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

function Fact({ label, value, detail, attention = false }: {
  label: string;
  value: string;
  detail: string;
  attention?: boolean;
}) {
  return <div className={`installation-switching__fact${attention ? ' installation-switching__fact--attention' : ''}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></div>;
}

function Check({ checked, disabled, onChange, label }: {
  checked: boolean;
  disabled: boolean;
  onChange: (value: boolean) => void;
  label: string;
}) {
  return <label className="installation-switching__check"><input type="checkbox" checked={checked} disabled={disabled} onChange={event => onChange(event.target.checked)} /><span>{label}</span></label>;
}

function Radio({ checked, disabled, onChange, title, description }: {
  checked: boolean;
  disabled: boolean;
  onChange: () => void;
  title: string;
  description: string;
}) {
  return <label className="installation-switching__radio"><input type="radio" name="installation-license-action" checked={checked} disabled={disabled} onChange={onChange} /><span><strong>{title}</strong><small>{description}</small></span></label>;
}

function DiagnosticPanel({ diagnostic, t }: {
  diagnostic: Diagnostic | null;
  t: ReturnType<typeof installationCopy>;
}) {
  if (!diagnostic) return null;
  return (
    <details className="installation-switching__diagnostic">
      <summary>{t.technicalDetails}</summary>
      {diagnostic.status != null && <div><strong>HTTP</strong><span>{diagnostic.status}</span></div>}
      {diagnostic.stage && <div><strong>{t.stage}</strong><span>{diagnostic.stage}</span></div>}
      {diagnostic.details.length > 0 && <ul>{diagnostic.details.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}</ul>}
    </details>
  );
}

function revisionText(value: number | null | undefined, none: string) {
  return value == null ? none : `r${value}`;
}

function authorityLabel(value: string, t: ReturnType<typeof installationCopy>) {
  if (value === 'AuthorityPresent') return t.authorityPresent;
  if (value === 'DeliberatelyDetached') return t.authorityDetached;
  return t.reviewRequired;
}

function licenseLabel(value: string, t: ReturnType<typeof installationCopy>) {
  if (value === 'Valid') return t.licenseValid;
  if (value === 'Demo') return t.licenseDemo;
  if (value === 'Missing') return t.licenseMissing;
  if (value === 'Invalid') return t.licenseInvalid;
  return value || t.reviewRequired;
}

function installationCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Installation', title: 'Switch application on this installation',
    description: 'Preserve what you need, detach the current Application securely, then return this same EliteSCADA installation to a real neutral state.',
    loading: 'Loading installation state…', loadFailed: 'Installation state could not be loaded.', retry: 'Try again',
    refresh: 'Refresh', refreshing: 'Refreshing…', operationFailed: 'The operation did not complete. The current installation remains recoverable.',
    application: 'Current Application', unnamed: 'Unnamed application', noProject: 'No project identity', working: 'Working', dirty: 'Unsaved changes', clean: 'Saved',
    published: 'Published', publishedHint: 'Published lifecycle state', active: 'Active', runtimeActive: 'Runtime active', runtimeInactive: 'Runtime inactive',
    authority: 'Security Authority', authorityProtected: 'Users, credentials, roles and scopes stay separate from the Application file.', authorityPresent: 'Present', authorityDetached: 'Detached',
    license: 'Machine license', machineLevel: 'Machine-level; switching Application does not remove it automatically.', licenseValid: 'Valid', licenseDemo: 'Demo / no installed license', licenseMissing: 'No installed license', licenseInvalid: 'Invalid installed license',
    historian: 'Historian', preserved: 'Preserved', reviewRequired: 'Review required', historianHint: 'Detach does not delete historical data.',
    engineeringProtection: 'Engineering protection', locked: 'Locked', unlocked: 'Unlocked', engineeringProtectionHint: 'Application protection state is shown for review only.',
    none: 'None', unpublishedWarning: 'Working differs from Published', workingAttention: 'Review Working before detach',
    unsavedWarning: 'Working has unsaved changes. Save a revision, export the Application, go back, or explicitly continue knowing the current Working may be lost.',
    unpublishedWarningLong: 'The current Working revision is not the Published revision. Review or export it before switching if you need to preserve that state.',
    saveRevision: 'Save revision', saving: 'Saving…', saved: 'Saved as revision {revision}.',
    recommended: 'Recommended before detach', applicationBackup: 'Application backup', applicationBackupHint: 'Export the current Application as .escadapkg. Authority credentials are not included.',
    exportApplication: 'Export Application', authorityBackup: 'Authority backup', authorityBackupHint: 'Export users, credentials, roles and scopes as a separately protected Authority backup.',
    authorityPassword: 'New backup password', exportAuthority: 'Export Authority', exporting: 'Exporting…', exported: 'Exported',
    applicationExported: 'Application backup exported.', authorityExported: 'Authority backup exported.', authorityExportFailed: 'Authority backup export did not return a backup payload.',
    historianNotDeleted: 'Historian is not deleted by detach.', historianDetail: 'Historical/database state remains independent. This workflow performs no DROP, TRUNCATE or bulk cleanup.',
    switchTitle: 'Ready to switch Applications?', switchHint: 'This is different from Delete Project, Uninstall, or Remove License. It invokes the server-owned secure detach transaction.',
    openDetach: 'Detach application from this installation', confirmEyebrow: 'Deliberate destructive transition', confirmTitle: 'Confirm secure detach',
    confirmDescription: 'Runtime effects are fenced first. The current Application and Authority are detached, old sessions stop being valid, and the installation becomes neutral. Historian data is preserved. The machine license follows the choice below.',
    cancel: 'Cancel', back: 'Back', backupDecision: 'Backup decision',
    applicationBackedUp: 'Application backup was exported in this review.', authorityBackedUp: 'Authority backup was exported in this review.',
    continueWithoutApplicationBackup: 'Continue without exporting the Application now; I understand I may need another backup to restore it later.',
    continueWithoutAuthorityBackup: 'Continue without exporting the Authority now; I understand its users/roles will not come from .escadapkg.',
    consequences: 'Consequences', ackUnsaved: 'I understand unsaved Working changes may be discarded unless preserved separately.',
    ackApplication: 'I understand this Application will stop being the one attached to this installation and its Runtime effects will be fenced.',
    ackAuthority: 'I understand the current Authority will be detached and existing sessions/tokens will no longer authorize the next Application.',
    ackHistorian: 'I understand Historian/database data is preserved and is not silently deleted by this operation.',
    licenseDecision: 'Machine license', keepLicense: 'Keep license', keepLicenseHint: 'Keep the current machine license while switching Applications.',
    removeLicense: 'Remove license', removeLicenseHint: 'Deliberately remove the installed license through the canonical lifecycle; the installation enters its defined Demo/no-license state.',
    replaceLicense: 'Replace license', replaceLicenseHint: 'Validate the candidate before detach. An invalid candidate does not destroy a currently valid license.',
    replacementCode: 'Replacement license code', blocked: 'Detach is blocked by the current server state.',
    validating: 'Validating current state…', detaching: 'Secure detach in progress…', validationHint: 'The server revalidates the exact current state before any detach mutation.',
    transactionHint: 'One server transaction owns Runtime fencing, Application detach, Authority detach and session invalidation. This UI does not simulate internal phases.',
    detachNow: 'Detach and return to neutral', detachFailed: 'Secure detach did not complete.', neutralReached: 'Installation is neutral. Reopening secure bootstrap…',
    technicalDetails: 'Technical diagnostics', stage: 'Stage'
  };

  if (locale === 'es') return {
    eyebrow: 'Instalación', title: 'Cambiar la aplicación de esta instalación',
    description: 'Conserve lo necesario, desvincule de forma segura la Application actual y devuelva esta misma instalación de EliteSCADA a un estado neutral real.',
    loading: 'Cargando estado de la instalación…', loadFailed: 'No fue posible cargar el estado de la instalación.', retry: 'Intentar nuevamente',
    refresh: 'Actualizar', refreshing: 'Actualizando…', operationFailed: 'La operación no terminó. La instalación actual permanece recuperable.',
    application: 'Application actual', unnamed: 'Aplicación sin nombre', noProject: 'Sin identidad de proyecto', working: 'Working', dirty: 'Cambios sin guardar', clean: 'Guardado',
    published: 'Published', publishedHint: 'Estado Published del ciclo', active: 'Active', runtimeActive: 'Runtime activo', runtimeInactive: 'Runtime inactivo',
    authority: 'Security Authority', authorityProtected: 'Usuarios, credenciales, roles y scopes permanecen separados del archivo de Application.', authorityPresent: 'Presente', authorityDetached: 'Desvinculada',
    license: 'Licencia de la máquina', machineLevel: 'Es de la máquina; cambiar Application no la elimina automáticamente.', licenseValid: 'Válida', licenseDemo: 'Demo / sin licencia instalada', licenseMissing: 'Sin licencia instalada', licenseInvalid: 'Licencia instalada inválida',
    historian: 'Historian', preserved: 'Preservado', reviewRequired: 'Revisión necesaria', historianHint: 'Detach no elimina datos históricos.',
    engineeringProtection: 'Protección de Engineering', locked: 'Bloqueado', unlocked: 'Desbloqueado', engineeringProtectionHint: 'El estado de protección se muestra solamente para revisión.',
    none: 'Ninguna', unpublishedWarning: 'Working difiere de Published', workingAttention: 'Revise Working antes de desvincular',
    unsavedWarning: 'Working contiene cambios sin guardar. Guarde una revisión, exporte la Application, vuelva atrás o continúe explícitamente sabiendo que el Working actual puede perderse.',
    unpublishedWarningLong: 'La revisión Working actual no es la revisión Published. Revísela o expórtela antes del cambio si necesita conservar ese estado.',
    saveRevision: 'Guardar revisión', saving: 'Guardando…', saved: 'Guardado como revisión {revision}.',
    recommended: 'Recomendado antes de desvincular', applicationBackup: 'Backup de Application', applicationBackupHint: 'Exporte la Application actual como .escadapkg. Las credenciales de Authority no están incluidas.',
    exportApplication: 'Exportar Application', authorityBackup: 'Backup de Authority', authorityBackupHint: 'Exporte usuarios, credenciales, roles y scopes como un backup protegido y separado de Authority.',
    authorityPassword: 'Nueva contraseña del backup', exportAuthority: 'Exportar Authority', exporting: 'Exportando…', exported: 'Exportado',
    applicationExported: 'Backup de Application exportado.', authorityExported: 'Backup de Authority exportado.', authorityExportFailed: 'La exportación de Authority no devolvió un backup.',
    historianNotDeleted: 'Detach no elimina el Historian.', historianDetail: 'El estado histórico/de base de datos permanece independiente. Este flujo no ejecuta DROP, TRUNCATE ni limpieza masiva.',
    switchTitle: '¿Listo para cambiar de Application?', switchHint: 'Esto es distinto de Delete Project, Uninstall o Remove License. Invoca la transacción segura de detach del servidor.',
    openDetach: 'Desvincular aplicación de esta instalación', confirmEyebrow: 'Transición destructiva deliberada', confirmTitle: 'Confirmar detach seguro',
    confirmDescription: 'Primero se aíslan los efectos del Runtime. La Application y Authority actuales se desvinculan, las sesiones anteriores dejan de ser válidas y la instalación queda neutral. Historian se preserva. La licencia de la máquina sigue la opción indicada.',
    cancel: 'Cancelar', back: 'Volver', backupDecision: 'Decisión sobre backups',
    applicationBackedUp: 'El backup de Application fue exportado en esta revisión.', authorityBackedUp: 'El backup de Authority fue exportado en esta revisión.',
    continueWithoutApplicationBackup: 'Continuar sin exportar ahora la Application; entiendo que puedo necesitar otro backup para restaurarla después.',
    continueWithoutAuthorityBackup: 'Continuar sin exportar ahora la Authority; entiendo que sus usuarios/roles no vienen dentro de .escadapkg.',
    consequences: 'Consecuencias', ackUnsaved: 'Entiendo que los cambios Working sin guardar pueden descartarse si no se preservan por separado.',
    ackApplication: 'Entiendo que esta Application dejará de estar vinculada a la instalación y sus efectos de Runtime serán aislados.',
    ackAuthority: 'Entiendo que la Authority actual será desvinculada y las sesiones/tokens existentes no autorizarán la próxima Application.',
    ackHistorian: 'Entiendo que los datos de Historian/base de datos se preservan y no se eliminan silenciosamente.',
    licenseDecision: 'Licencia de la máquina', keepLicense: 'Mantener licencia', keepLicenseHint: 'Mantiene la licencia actual de la máquina durante el cambio.',
    removeLicense: 'Eliminar licencia', removeLicenseHint: 'Elimina deliberadamente la licencia mediante el ciclo canónico; la instalación entra en el estado Demo/sin licencia definido.',
    replaceLicense: 'Reemplazar licencia', replaceLicenseHint: 'Valida la candidata antes del detach. Una candidata inválida no destruye una licencia actualmente válida.',
    replacementCode: 'Código de licencia de reemplazo', blocked: 'El estado actual del servidor bloquea el detach.',
    validating: 'Validando estado actual…', detaching: 'Detach seguro en curso…', validationHint: 'El servidor revalida el estado actual exacto antes de cualquier mutación de detach.',
    transactionHint: 'Una sola transacción del servidor controla el aislamiento del Runtime, detach de Application, detach de Authority e invalidación de sesiones. La UI no simula fases internas.',
    detachNow: 'Desvincular y volver a neutral', detachFailed: 'El detach seguro no terminó.', neutralReached: 'La instalación está neutral. Reabriendo el bootstrap seguro…',
    technicalDetails: 'Diagnóstico técnico', stage: 'Etapa'
  };

  return {
    eyebrow: 'Instalação', title: 'Trocar a aplicação desta instalação',
    description: 'Preserve o que precisar, desvincule com segurança a Application atual e devolva esta mesma instalação do EliteSCADA a um estado neutro real.',
    loading: 'Carregando estado da instalação…', loadFailed: 'Não foi possível carregar o estado da instalação.', retry: 'Tentar novamente',
    refresh: 'Atualizar', refreshing: 'Atualizando…', operationFailed: 'A operação não foi concluída. A instalação atual permanece recuperável.',
    application: 'Application atual', unnamed: 'Aplicação sem nome', noProject: 'Sem identidade de projeto', working: 'Working', dirty: 'Alterações não salvas', clean: 'Salvo',
    published: 'Published', publishedHint: 'Estado Published do ciclo', active: 'Active', runtimeActive: 'Runtime ativo', runtimeInactive: 'Runtime inativo',
    authority: 'Security Authority', authorityProtected: 'Usuários, credenciais, roles e scopes permanecem separados do arquivo da Application.', authorityPresent: 'Presente', authorityDetached: 'Desvinculada',
    license: 'Licença da máquina', machineLevel: 'É machine-level; trocar Application não remove a licença automaticamente.', licenseValid: 'Válida', licenseDemo: 'Demo / sem licença instalada', licenseMissing: 'Sem licença instalada', licenseInvalid: 'Licença instalada inválida',
    historian: 'Historian', preserved: 'Preservado', reviewRequired: 'Revisão necessária', historianHint: 'Detach não apaga dados históricos.',
    engineeringProtection: 'Proteção do Engineering', locked: 'Bloqueado', unlocked: 'Desbloqueado', engineeringProtectionHint: 'O estado de proteção é exibido apenas para revisão.',
    none: 'Nenhuma', unpublishedWarning: 'Working difere de Published', workingAttention: 'Revise o Working antes do detach',
    unsavedWarning: 'O Working possui alterações não salvas. Salve uma revisão, exporte a Application, volte ou continue deliberadamente sabendo que o Working atual pode ser perdido.',
    unpublishedWarningLong: 'A revisão Working atual não é a revisão Published. Revise ou exporte antes da troca caso precise preservar esse estado.',
    saveRevision: 'Salvar revisão', saving: 'Salvando…', saved: 'Salvo como revisão {revision}.',
    recommended: 'Recomendado antes do detach', applicationBackup: 'Backup da Application', applicationBackupHint: 'Exporte a Application atual como .escadapkg. Credenciais da Authority não estão incluídas.',
    exportApplication: 'Exportar Application', authorityBackup: 'Backup da Authority', authorityBackupHint: 'Exporte usuários, credenciais, roles e scopes como um backup protegido e separado da Authority.',
    authorityPassword: 'Nova senha do backup', exportAuthority: 'Exportar Authority', exporting: 'Exportando…', exported: 'Exportado',
    applicationExported: 'Backup da Application exportado.', authorityExported: 'Backup da Authority exportado.', authorityExportFailed: 'A exportação da Authority não retornou um backup.',
    historianNotDeleted: 'Detach não apaga o Historian.', historianDetail: 'O estado histórico/banco permanece independente. Este fluxo não executa DROP, TRUNCATE nem limpeza em massa.',
    switchTitle: 'Pronto para trocar de Application?', switchHint: 'Isto é diferente de Delete Project, Uninstall ou Remove License. A ação chama a transação segura de detach do servidor.',
    openDetach: 'Desvincular aplicação desta instalação', confirmEyebrow: 'Transição destrutiva deliberada', confirmTitle: 'Confirmar detach seguro',
    confirmDescription: 'Os efeitos de Runtime são fenced primeiro. A Application e a Authority atuais são desvinculadas, sessões antigas deixam de ser válidas e a instalação volta ao neutro. O Historian é preservado. A licença da máquina segue a escolha abaixo.',
    cancel: 'Cancelar', back: 'Voltar', backupDecision: 'Decisão sobre backups',
    applicationBackedUp: 'O backup da Application foi exportado nesta revisão.', authorityBackedUp: 'O backup da Authority foi exportado nesta revisão.',
    continueWithoutApplicationBackup: 'Continuar sem exportar a Application agora; entendo que posso precisar de outro backup para restaurá-la depois.',
    continueWithoutAuthorityBackup: 'Continuar sem exportar a Authority agora; entendo que usuários/roles dela não vêm dentro do .escadapkg.',
    consequences: 'Consequências', ackUnsaved: 'Entendo que alterações Working não salvas podem ser descartadas se não forem preservadas separadamente.',
    ackApplication: 'Entendo que esta Application deixará de ser a vinculada à instalação e seus efeitos de Runtime serão fenced.',
    ackAuthority: 'Entendo que a Authority atual será desvinculada e sessões/tokens existentes não autorizarão a próxima Application.',
    ackHistorian: 'Entendo que dados do Historian/banco são preservados e não serão apagados silenciosamente.',
    licenseDecision: 'Licença da máquina', keepLicense: 'Manter licença', keepLicenseHint: 'Mantém a licença atual da máquina durante a troca de Applications.',
    removeLicense: 'Remover licença', removeLicenseHint: 'Remove deliberadamente a licença pelo lifecycle canônico; a instalação entra no estado Demo/sem licença definido.',
    replaceLicense: 'Substituir licença', replaceLicenseHint: 'Valida a candidata antes do detach. Uma candidata inválida não destrói uma licença atualmente válida.',
    replacementCode: 'Código da licença substituta', blocked: 'O estado atual do servidor bloqueia o detach.',
    validating: 'Validando estado atual…', detaching: 'Detach seguro em andamento…', validationHint: 'O servidor revalida o estado atual exato antes de qualquer mutação de detach.',
    transactionHint: 'Uma única transação do servidor controla fence de Runtime, detach da Application, detach da Authority e invalidação de sessões. A UI não simula fases internas.',
    detachNow: 'Desvincular e voltar ao neutro', detachFailed: 'O detach seguro não foi concluído.', neutralReached: 'A instalação está neutra. Reabrindo o bootstrap seguro…',
    technicalDetails: 'Diagnóstico técnico', stage: 'Etapa'
  };
}
