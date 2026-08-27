import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  applyCanonicalEngineeringJson,
  exportCanonicalEngineeringJson,
  exportProjectPackage,
  inspectProjectPackage,
  loadProjectPortabilityContext,
  previewCanonicalEngineeringJson,
  previewProjectPackage,
  restoreProjectPackage,
  triggerBrowserDownload
} from './projectPortabilityApi';
import {
  canonicalJsonCandidateIdentity,
  canonicalTextFingerprint,
  mergeModeLabel,
  previewTokenMatches,
  PROJECT_PORTABILITY_MERGE_MODES,
  projectPortabilityErrorText,
  projectPortabilityFileFingerprint
} from './EngineeringProjectManagementWorkspace.logic';
import { projectManagementCopy } from './EngineeringProjectManagementWorkspace.copy';
import type {
  CanonicalEngineeringIdentity,
  PortabilityPreviewToken,
  ProjectPackageInspection,
  ProjectPortabilityContext,
  ProjectPortabilityMergeMode,
  ProjectPortabilityPreview
} from './projectPortabilityTypes';
import type { EngineeringLocale } from './i18n';
import './engineering-project-management.css';

type ConfirmationKind = 'json-apply' | 'package-restore';

export function EngineeringProjectManagementWorkspace({ locale }: { locale: EngineeringLocale }) {
  const copy = useMemo(() => projectManagementCopy(locale), [locale]);
  const [context, setContext] = useState<ProjectPortabilityContext | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [jsonFile, setJsonFile] = useState<File | null>(null);
  const [jsonText, setJsonText] = useState('');
  const [jsonIdentity, setJsonIdentity] = useState<CanonicalEngineeringIdentity | null>(null);
  const [jsonFingerprint, setJsonFingerprint] = useState<string | null>(null);
  const [jsonMode, setJsonMode] = useState<ProjectPortabilityMergeMode>('CreateAndUpdate');
  const [jsonPreview, setJsonPreview] = useState<PortabilityPreviewToken | null>(null);

  const [packageFile, setPackageFile] = useState<File | null>(null);
  const [packageFingerprint, setPackageFingerprint] = useState<string | null>(null);
  const [packageInspection, setPackageInspection] = useState<ProjectPackageInspection | null>(null);
  const [packageMode, setPackageMode] = useState<ProjectPortabilityMergeMode>('CreateAndUpdate');
  const [packagePreview, setPackagePreview] = useState<PortabilityPreviewToken | null>(null);
  const [confirmation, setConfirmation] = useState<ConfirmationKind | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setContext(await loadProjectPortabilityContext());
    } catch (cause) {
      setError(projectPortabilityErrorText(cause, locale));
    } finally {
      setLoading(false);
    }
  }, [locale]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const activeJsonPreview = previewTokenMatches(jsonPreview, jsonFingerprint, jsonMode)
    ? jsonPreview
    : null;
  const activePackagePreview = previewTokenMatches(packagePreview, packageFingerprint, packageMode)
    ? packagePreview
    : null;

  async function downloadJson() {
    await perform('download-json', async () => {
      triggerBrowserDownload(await exportCanonicalEngineeringJson());
      setNotice(copy.savedJson);
    });
  }

  async function downloadPackage() {
    if (!context?.workspace.projectKey || !context.workspace.projectName) return;
    await perform('download-package', async () => {
      triggerBrowserDownload(await exportProjectPackage(
        context.workspace.projectKey!,
        context.workspace.projectName!
      ));
      setNotice(copy.savedPackage);
    });
  }

  async function selectJsonFile(file: File | null) {
    setJsonFile(file);
    setJsonText('');
    setJsonIdentity(null);
    setJsonFingerprint(null);
    setJsonPreview(null);
    setNotice(null);
    setError(null);
    if (!file) return;

    try {
      const text = await file.text();
      const identity = canonicalJsonCandidateIdentity(text);
      setJsonText(text);
      setJsonFingerprint(canonicalTextFingerprint(file, text));
      if (!identity.validJson) {
        setError(copy.invalidJson);
        return;
      }
      setJsonIdentity({ schema: identity.schema ?? '—', schemaVersion: identity.schemaVersion ?? 0 });
    } catch (cause) {
      setError(projectPortabilityErrorText(cause, locale));
    }
  }

  async function previewJsonImport() {
    if (!jsonFile || !jsonText || !jsonFingerprint) return;
    await perform('json-preview', async () => {
      const validated = await previewCanonicalEngineeringJson(jsonText, jsonMode);
      setJsonPreview({
        sourceFingerprint: jsonFingerprint,
        mode: jsonMode,
        expectedChangeVersion: validated.expectedChangeVersion,
        preview: validated.preview
      });
      setNotice(copy.previewReady);
    });
  }

  async function applyJsonImport() {
    if (!activeJsonPreview || !jsonText) return;
    await perform('json-apply', async () => {
      await applyCanonicalEngineeringJson(
        jsonText,
        activeJsonPreview.mode,
        activeJsonPreview.expectedChangeVersion
      );
      setJsonPreview(null);
      setConfirmation(null);
      await refresh();
      setNotice(copy.jsonApplied);
    });
  }

  async function selectPackageFile(file: File | null) {
    setPackageFile(file);
    setPackageFingerprint(file ? projectPortabilityFileFingerprint(file) : null);
    setPackageInspection(null);
    setPackagePreview(null);
    setNotice(null);
    setError(null);
    if (!file) return;
    await inspectSelectedPackage(file);
  }

  async function inspectSelectedPackage(file = packageFile) {
    if (!file) return;
    await perform('package-inspect', async () => {
      setPackageInspection(await inspectProjectPackage(file));
      setPackagePreview(null);
      setNotice(copy.packageInspected);
    });
  }

  async function previewPackageRestore() {
    if (!packageFile || !packageFingerprint || !packageInspection) return;
    await perform('package-preview', async () => {
      const validated = await previewProjectPackage(packageFile, packageMode);
      setPackagePreview({
        sourceFingerprint: packageFingerprint,
        mode: packageMode,
        expectedChangeVersion: validated.expectedChangeVersion,
        preview: validated.preview
      });
      setNotice(copy.previewReady);
    });
  }

  async function restorePackage() {
    if (!packageFile || !activePackagePreview) return;
    await perform('package-restore', async () => {
      await restoreProjectPackage(
        packageFile,
        activePackagePreview.mode,
        activePackagePreview.expectedChangeVersion
      );
      setPackagePreview(null);
      setConfirmation(null);
      await refresh();
      setNotice(copy.packageRestored);
    });
  }

  async function perform(name: string, action: () => Promise<void>) {
    setBusy(name);
    setError(null);
    setNotice(null);
    try {
      await action();
    } catch (cause) {
      setError(projectPortabilityErrorText(cause, locale));
    } finally {
      setBusy(null);
    }
  }

  if (loading && !context) {
    return <section className="eng-project-management eng-project-management--loading" aria-label={copy.title}>{copy.loading}</section>;
  }

  if (!context) {
    return (
      <section className="eng-project-management" aria-label={copy.title}>
        <h2>{copy.title}</h2>
        <p className="eng-project-management__error" role="alert">{error ?? copy.loadFailed}</p>
        <button type="button" onClick={() => void refresh()}>{copy.retry}</button>
      </section>
    );
  }

  return (
    <section className="eng-project-management" aria-label={copy.title}>
      <header className="eng-project-management__header">
        <div>
          <span className="eng-project-management__eyebrow">{copy.eyebrow}</span>
          <h2>{copy.title}</h2>
          <p>{copy.description}</p>
        </div>
        <button type="button" onClick={() => void refresh()} disabled={Boolean(busy) || loading}>
          {loading ? copy.refreshing : copy.refresh}
        </button>
      </header>

      {error && <p className="eng-project-management__error" role="alert">{error}</p>}
      {notice && <p className="eng-project-management__notice" role="status">{notice}</p>}

      <div className="eng-project-management__facts">
        <Fact label={copy.project} value={context.workspace.projectName || copy.unnamed} detail={context.workspace.projectKey || copy.noKey} />
        <Fact label={copy.working} value={context.workspace.isDirty ? copy.dirty : copy.clean} detail={`${copy.changeVersion}: ${context.workspace.changeVersion}`} attention={context.workspace.isDirty} />
        <Fact label={copy.baseRevision} value={context.workspace.baseRevision ? `r${context.workspace.baseRevision}` : copy.none} detail={`${copy.changeVersion}: ${context.workspace.changeVersion}`} />
        <Fact label={copy.canonicalSchema} value={context.canonical.schema} detail={`${copy.schemaVersion}: ${context.canonical.schemaVersion}`} />
      </div>

      <div className="eng-project-management__columns">
        <article className="eng-project-management__card" data-testid="project-json-portability">
          <CardHeader title={copy.jsonTitle} description={copy.jsonDescription}>
            <button type="button" onClick={() => void downloadJson()} disabled={Boolean(busy)}>{copy.downloadJson}</button>
          </CardHeader>

          <label className="eng-project-management__file-field">
            <span>{copy.jsonSource}</span>
            <input
              type="file"
              accept="application/json,.json"
              onChange={event => void selectJsonFile(event.target.files?.[0] ?? null)}
              disabled={Boolean(busy)}
            />
            <small>{jsonFile?.name ?? copy.noFile}</small>
          </label>

          {jsonFile && jsonIdentity && (
            <div className="eng-project-management__source-facts">
              <Fact label={copy.sourceSchema} value={jsonIdentity.schema} detail={`${copy.schemaVersion}: ${jsonIdentity.schemaVersion || '—'}`} />
            </div>
          )}

          <MergeModeSelector locale={locale} value={jsonMode} label={copy.mergeMode} disabled={Boolean(busy)} onChange={setJsonMode} />

          <div className="eng-project-management__actions">
            <button
              type="button"
              data-testid="project-json-preview"
              onClick={() => void previewJsonImport()}
              disabled={Boolean(busy) || !jsonFile || !jsonText || !jsonIdentity}
            >{copy.previewJson}</button>
            <button
              type="button"
              data-testid="project-json-apply"
              onClick={() => setConfirmation('json-apply')}
              disabled={Boolean(busy) || !activeJsonPreview}
            >{copy.applyJson}</button>
          </div>

          <PreviewPanel preview={activeJsonPreview?.preview ?? null} copy={copy} />
        </article>

        <article className="eng-project-management__card" data-testid="project-package-portability">
          <CardHeader title={copy.packageTitle} description={copy.packageDescription}>
            <button
              type="button"
              onClick={() => void downloadPackage()}
              disabled={Boolean(busy) || !context.workspace.projectKey || !context.workspace.projectName}
            >{copy.downloadPackage}</button>
          </CardHeader>

          <label className="eng-project-management__file-field">
            <span>{copy.packageSource}</span>
            <input
              type="file"
              accept=".escadapkg,application/vnd.elitescada.project-package"
              onChange={event => void selectPackageFile(event.target.files?.[0] ?? null)}
              disabled={Boolean(busy)}
            />
            <small>{packageFile?.name ?? copy.noFile}</small>
          </label>

          <div className="eng-project-management__actions">
            <button type="button" onClick={() => void inspectSelectedPackage()} disabled={Boolean(busy) || !packageFile}>{copy.inspectPackage}</button>
          </div>

          {packageInspection ? <PackageManifestPanel inspection={packageInspection} locale={locale} copy={copy} /> : (
            <p className="eng-project-management__empty">{copy.packageNotInspected}</p>
          )}

          <MergeModeSelector locale={locale} value={packageMode} label={copy.mergeMode} disabled={Boolean(busy)} onChange={setPackageMode} />

          <div className="eng-project-management__actions">
            <button
              type="button"
              data-testid="project-package-preview"
              onClick={() => void previewPackageRestore()}
              disabled={Boolean(busy) || !packageFile || !packageInspection}
            >{copy.previewPackage}</button>
            <button
              type="button"
              data-testid="project-package-restore"
              onClick={() => setConfirmation('package-restore')}
              disabled={Boolean(busy) || !activePackagePreview}
            >{copy.restorePackage}</button>
          </div>

          <PreviewPanel preview={activePackagePreview?.preview ?? null} copy={copy} />
        </article>
      </div>

      <section className="eng-project-management__boundary" aria-label={copy.secretsTitle}>
        <div>
          <strong>{copy.secretsTitle}</strong>
          <p>{copy.secretsHint}</p>
        </div>
        <div>
          <strong>{copy.excludedTitle}</strong>
          <ul>{copy.excluded.map(item => <li key={item}>{item}</li>)}</ul>
        </div>
      </section>

      {confirmation && (
        <div className="eng-project-management__confirmation" role="dialog" aria-modal="false" aria-labelledby="project-portability-confirm-title">
          <div>
            <strong id="project-portability-confirm-title">
              {confirmation === 'json-apply' ? copy.applyConfirmTitle : copy.restoreConfirmTitle}
            </strong>
            <p>{confirmation === 'json-apply' ? copy.applyConfirmDescription : copy.restoreConfirmDescription}</p>
          </div>
          <div className="eng-project-management__actions">
            <button type="button" onClick={() => setConfirmation(null)} disabled={Boolean(busy)}>{copy.cancel}</button>
            <button
              type="button"
              className="eng-project-management__critical"
              onClick={() => void (confirmation === 'json-apply' ? applyJsonImport() : restorePackage())}
              disabled={Boolean(busy)}
            >{confirmation === 'json-apply' ? copy.applyConfirm : copy.restoreConfirm}</button>
          </div>
        </div>
      )}

      <footer className="eng-project-management__authority">
        <strong>{copy.authorityTitle}</strong>
        <span>{copy.authorityHint}</span>
      </footer>
    </section>
  );
}

function CardHeader({ title, description, children }: { title: string; description: string; children: React.ReactNode }) {
  return (
    <header className="eng-project-management__card-header">
      <div><h3>{title}</h3><p>{description}</p></div>
      <div>{children}</div>
    </header>
  );
}

function Fact({ label, value, detail, attention = false }: { label: string; value: string; detail: string; attention?: boolean }) {
  return (
    <div className={`eng-project-management__fact${attention ? ' eng-project-management__fact--attention' : ''}`}>
      <span>{label}</span><strong>{value}</strong><small>{detail}</small>
    </div>
  );
}

function MergeModeSelector({ locale, value, label, disabled, onChange }: {
  locale: EngineeringLocale;
  value: ProjectPortabilityMergeMode;
  label: string;
  disabled: boolean;
  onChange: (mode: ProjectPortabilityMergeMode) => void;
}) {
  return (
    <label className="eng-project-management__mode">
      <span>{label}</span>
      <select value={value} disabled={disabled} onChange={event => onChange(event.target.value as ProjectPortabilityMergeMode)}>
        {PROJECT_PORTABILITY_MERGE_MODES.map(mode => <option key={mode} value={mode}>{mergeModeLabel(mode, locale)}</option>)}
      </select>
    </label>
  );
}

function PreviewPanel({ preview, copy }: { preview: ProjectPortabilityPreview | null; copy: ReturnType<typeof projectManagementCopy> }) {
  if (!preview) return <p className="eng-project-management__empty">{copy.noPreview}</p>;
  const issues = preview.items.flatMap(item => item.issues).slice(0, 12);
  return (
    <section className={`eng-project-management__preview${preview.canApply ? '' : ' eng-project-management__preview--blocked'}`} data-testid="project-portability-preview-result">
      <div className="eng-project-management__section-heading">
        <h4>{copy.previewTitle}</h4>
        <strong>{preview.canApply ? copy.canApply : copy.blocked}</strong>
      </div>
      <dl className="eng-project-management__preview-counts">
        <div><dt>{copy.create}</dt><dd>{preview.createCount}</dd></div>
        <div><dt>{copy.update}</dt><dd>{preview.updateCount}</dd></div>
        <div><dt>{copy.skip}</dt><dd>{preview.skipCount}</dd></div>
        <div><dt>{copy.errors}</dt><dd>{preview.errorCount}</dd></div>
      </dl>
      <div className="eng-project-management__issues">
        <strong>{copy.issues}</strong>
        {issues.length === 0 ? <span>{copy.noIssues}</span> : (
          <ul>{issues.map((issue, index) => <li key={`${issue.code}-${issue.entityKey}-${index}`}><code>{issue.code}</code> {issue.message}</li>)}</ul>
        )}
      </div>
    </section>
  );
}

function PackageManifestPanel({ inspection, locale, copy }: {
  inspection: ProjectPackageInspection;
  locale: EngineeringLocale;
  copy: ReturnType<typeof projectManagementCopy>;
}) {
  const entityTotal = Object.entries(inspection.engineering)
    .filter(([key, value]) => !['schema', 'schemaVersion'].includes(key) && typeof value === 'number')
    .reduce((sum, [, value]) => sum + Number(value), 0);
  const payload = inspection.manifest.files[0];
  return (
    <section className="eng-project-management__manifest" data-testid="project-package-manifest">
      <div className="eng-project-management__section-heading"><h4>{copy.manifest}</h4><strong>{inspection.manifest.product}</strong></div>
      <dl>
        <div><dt>{copy.packageId}</dt><dd><code>{inspection.manifest.packageId}</code></dd></div>
        <div><dt>{copy.createdAt}</dt><dd>{formatTimestamp(inspection.manifest.createdAtUtc, locale)}</dd></div>
        <div><dt>{copy.format}</dt><dd>{inspection.manifest.format} v{inspection.manifest.formatVersion}</dd></div>
        <div><dt>{copy.projectIdentity}</dt><dd>{inspection.manifest.projectName} <code>{inspection.manifest.projectKey}</code></dd></div>
        <div><dt>{copy.packageSchema}</dt><dd>{inspection.manifest.engineeringSchema} v{inspection.manifest.engineeringSchemaVersion}</dd></div>
        <div><dt>{copy.packagePayload}</dt><dd>{entityTotal} {copy.items}</dd></div>
        {payload && <div><dt>{copy.checksum}</dt><dd><code className="eng-project-management__hash">{payload.sha256}</code><small>{payload.length} {copy.bytes}</small></dd></div>}
      </dl>
    </section>
  );
}

function formatTimestamp(value: string, locale: EngineeringLocale): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale === 'en' ? 'en-US' : locale, { dateStyle: 'short', timeStyle: 'short' }).format(date);
}
