import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  activatePublishedEngineeringRevision,
  checkoutEngineeringRevision,
  loadEngineeringLifecycleState,
  publishEngineeringRevision,
  saveEngineeringRevision
} from './engineeringLifecycleApi';
import {
  buildLifecycleSteps,
  canActivatePublished,
  canSaveWorkspace,
  checkoutConfirmationText,
  checkoutRequiresConfirmation,
  isRevisionActive,
  isRevisionPublished,
  isWorkspaceBaseRevision,
  lifecycleErrorText
} from './EngineeringLifecycleWorkspace.logic';
import type { EngineeringLifecycleAction, EngineeringLifecycleState, EngineeringRevisionMetadata } from './engineeringLifecycleTypes';
import type { EngineeringLocale } from './i18n';
import './engineering-lifecycle-workspace.css';

type PendingCheckout = { revision: number };
type Copy = ReturnType<typeof lifecycleCopy>;

export function EngineeringLifecycleWorkspace({ locale }: { locale: EngineeringLocale }) {
  const copy = useMemo(() => lifecycleCopy(locale), [locale]);
  const [state, setState] = useState<EngineeringLifecycleState | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyAction, setBusyAction] = useState<EngineeringLifecycleAction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [pendingCheckout, setPendingCheckout] = useState<PendingCheckout | null>(null);

  const busy = busyAction !== null;

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setState(await loadEngineeringLifecycleState());
    } catch (cause) {
      setError(lifecycleErrorText(cause, locale));
    } finally {
      setLoading(false);
    }
  }, [locale]);

  useEffect(() => { void refresh(); }, [refresh]);

  async function perform(action: EngineeringLifecycleAction, revision?: number) {
    if (!state?.projectKey) return;
    setBusyAction(action);
    setError(null);
    setNotice(null);
    try {
      if (action === 'save') {
        const projectName = state.workspace.projectName?.trim();
        if (!projectName) throw new Error(copy.projectNameRequired);
        const saved = await saveEngineeringRevision(state.projectKey, projectName);
        setNotice(copy.saved.replace('{revision}', String(saved.revision)));
      } else if (action === 'checkout' && revision) {
        await checkoutEngineeringRevision(state.projectKey, revision);
        setNotice(copy.checkedOut.replace('{revision}', String(revision)));
      } else if (action === 'publish' && revision) {
        await publishEngineeringRevision(state.projectKey, revision);
        setNotice(copy.publishedNotice.replace('{revision}', String(revision)));
      } else if (action === 'activate') {
        await activatePublishedEngineeringRevision(state.projectKey);
        setNotice(copy.activated);
      }
      setPendingCheckout(null);
      await refresh();
    } catch (cause) {
      setError(lifecycleErrorText(cause, locale));
    } finally {
      setBusyAction(null);
    }
  }

  function requestCheckout(revision: number) {
    if (!state) return;
    if (checkoutRequiresConfirmation(state)) {
      setPendingCheckout({ revision });
      return;
    }
    void perform('checkout', revision);
  }

  if (loading && !state) return <section className="eng-lifecycle-workspace eng-lifecycle-workspace--loading">{copy.loading}</section>;
  if (!state) return <section className="eng-lifecycle-workspace"><p role="alert">{error ?? copy.loadFailed}</p><button onClick={() => void refresh()}>{copy.retry}</button></section>;

  const lifecycle = state.lifecycle;
  const runtime = state.runtime;
  const steps = buildLifecycleSteps(state);
  const latestRevision = state.revisions[0] ?? null;
  const configuredProjectMatches = Boolean(
    state.projectKey &&
    state.persistence.configuredProjectKey &&
    state.persistence.configuredProjectKey.toLowerCase() === state.projectKey.toLowerCase()
  );
  const publishedRevision = lifecycle?.publishedRevision ?? null;
  const activeRevision = lifecycle?.activeRevision ?? null;
  const publishedIsActive = Boolean(publishedRevision && activeRevision === publishedRevision);
  const runtimeConsistent = Boolean(activeRevision && runtime?.consistent);
  const runtimeNeedsAttention = Boolean(activeRevision && runtime?.consistent === false);

  return (
    <section className="eng-lifecycle-workspace" aria-label={copy.lifecycleFlow}>
      <div className="eng-lifecycle-workspace__toolbar">
        <button
          className="eng-lifecycle-workspace__refresh"
          onClick={() => void refresh()}
          disabled={loading || busy}
          aria-label={copy.refresh}
          title={copy.refresh}
        >
          {loading ? copy.refreshing : copy.refresh}
        </button>
      </div>

      {!state.persistence.enabled && <Banner title={copy.persistenceUnavailable} text={copy.persistenceUnavailableHint} />}
      {state.persistence.enabled && state.projectKey && !configuredProjectMatches && (
        <Banner
          title={copy.runtimeBindingMismatch}
          text={copy.runtimeBindingMismatchHint.replace('{configured}', state.persistence.configuredProjectKey ?? copy.notConfigured)}
        />
      )}
      {error && <p className="eng-lifecycle-workspace__error" role="alert">{error}</p>}
      {notice && <p className="eng-lifecycle-workspace__notice" role="status">{notice}</p>}

      <ol className="eng-lifecycle-workspace__steps" aria-label={copy.lifecycleFlow}>
        <LifecycleStep
          index={1}
          label={copy.working}
          state={steps[0].state}
          revision={state.workspace.baseRevision}
          copy={copy}
        >
          <dl className="eng-lifecycle-workspace__summary">
            <div><dt>{copy.project}</dt><dd>{state.workspace.projectName || copy.unnamedProject}</dd></div>
            {state.workspace.baseRevision ? <div><dt>{copy.baseRevision}</dt><dd>{revisionText(state.workspace.baseRevision, copy)}</dd></div> : null}
            <div><dt>{copy.status}</dt><dd className={state.workspace.isDirty ? 'is-warning' : ''}>{state.workspace.isDirty ? copy.dirty : copy.clean}</dd></div>
          </dl>
          {canSaveWorkspace(state) && (
            <button
              className="eng-lifecycle-workspace__primary"
              onClick={() => void perform('save')}
              disabled={busy}
            >
              {busyAction === 'save' ? copy.saving : copy.saveRevision}
            </button>
          )}
        </LifecycleStep>

        <LifecycleStep
          index={2}
          label={copy.savedRevision}
          state={steps[1].state}
          revision={latestRevision?.revision ?? state.workspace.baseRevision}
          copy={copy}
          className="eng-lifecycle-workspace__step--revisions"
        >
          <div className="eng-lifecycle-workspace__step-status">
            <span>{latestRevision ? copy.latestRevision : copy.noRevisions}</span>
            {latestRevision ? <strong>{formatTimestamp(latestRevision.savedAtUtc, locale, copy.never)}</strong> : null}
          </div>

          {state.revisions.length > 0 ? (
            <details className="eng-lifecycle-workspace__history">
              <summary>{copy.revisions} ({state.revisions.length})</summary>
              <div className="eng-lifecycle-workspace__revision-list" aria-label={copy.revisions}>
                {state.revisions.map(revision => (
                  <RevisionRow
                    key={revision.revision}
                    revision={revision}
                    state={state}
                    locale={locale}
                    copy={copy}
                    busy={busy}
                    onCheckout={() => requestCheckout(revision.revision)}
                    onPublish={() => void perform('publish', revision.revision)}
                  />
                ))}
              </div>
            </details>
          ) : null}
        </LifecycleStep>

        <LifecycleStep
          index={3}
          label={copy.published}
          state={steps[2].state}
          revision={publishedRevision}
          copy={copy}
        >
          <div className="eng-lifecycle-workspace__step-status">
            <span>{publishedRevision ? (publishedIsActive ? copy.alreadyActive : copy.readyToActivate) : copy.notPublished}</span>
          </div>
          {canActivatePublished(state) && (
            <button
              className="eng-lifecycle-workspace__primary"
              onClick={() => void perform('activate')}
              disabled={busy}
              data-testid="engineering-lifecycle-activate"
            >
              {busyAction === 'activate' ? copy.activating : copy.activateRuntime}
            </button>
          )}
        </LifecycleStep>

        <LifecycleStep
          index={4}
          label={copy.active}
          state={steps[3].state}
          revision={activeRevision}
          copy={copy}
        >
          <div className={`eng-lifecycle-workspace__runtime${runtimeNeedsAttention ? ' eng-lifecycle-workspace__runtime--warning' : ''}`}>
            <strong>{!activeRevision ? copy.noActiveRevision : runtimeConsistent ? copy.runtimeActive : copy.runtimeDiverged}</strong>
            {activeRevision && runtime?.live.revision ? <span>{copy.liveRevision.replace('{revision}', `r${runtime.live.revision}`)}</span> : null}
          </div>
        </LifecycleStep>
      </ol>

      {pendingCheckout && (
        <CheckoutConfirmation
          revision={pendingCheckout.revision}
          locale={locale}
          cancelLabel={copy.cancel}
          busy={busy}
          onCancel={() => setPendingCheckout(null)}
          onConfirm={() => void perform('checkout', pendingCheckout.revision)}
        />
      )}
    </section>
  );
}

function LifecycleStep({
  index,
  label,
  state,
  revision,
  copy,
  children,
  className = ''
}: {
  index: number;
  label: string;
  state: 'complete' | 'current' | 'pending' | 'warning';
  revision: number | null | undefined;
  copy: Copy;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <li className={`eng-lifecycle-workspace__step eng-lifecycle-workspace__step--${state} ${className}`.trim()}>
      <header className="eng-lifecycle-workspace__step-header">
        <span className="eng-lifecycle-workspace__step-number">{index}</span>
        <div>
          <strong>{label}</strong>
          <small>{revisionText(revision, copy)}</small>
        </div>
      </header>
      <div className="eng-lifecycle-workspace__step-body">{children}</div>
    </li>
  );
}

function RevisionRow({
  revision,
  state,
  locale,
  copy,
  busy,
  onCheckout,
  onPublish
}: {
  revision: EngineeringRevisionMetadata;
  state: EngineeringLifecycleState;
  locale: EngineeringLocale;
  copy: Copy;
  busy: boolean;
  onCheckout: () => void;
  onPublish: () => void;
}) {
  const workingBase = isWorkspaceBaseRevision(revision, state);
  const published = isRevisionPublished(revision, state.lifecycle);
  const active = isRevisionActive(revision, state.lifecycle);

  return (
    <article className="eng-lifecycle-workspace__revision-row">
      <div className="eng-lifecycle-workspace__revision-id">
        <strong>r{revision.revision}</strong>
        <span>{formatTimestamp(revision.savedAtUtc, locale, copy.never)}</span>
      </div>
      <div className="eng-lifecycle-workspace__badges">
        {workingBase && <span>{copy.workingBase}</span>}
        {published && <span>{copy.published}</span>}
        {active && <span>{copy.active}</span>}
      </div>
      <div className="eng-lifecycle-workspace__row-actions">
        {!workingBase && <button onClick={onCheckout} disabled={busy}>{copy.useInWorking}</button>}
        {!published && <button onClick={onPublish} disabled={busy}>{copy.publish}</button>}
      </div>
    </article>
  );
}

function CheckoutConfirmation({
  revision,
  locale,
  cancelLabel,
  busy,
  onCancel,
  onConfirm
}: {
  revision: number;
  locale: EngineeringLocale;
  cancelLabel: string;
  busy: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const copy = checkoutConfirmationText(locale, revision);
  return (
    <div className="eng-lifecycle-workspace__confirmation" role="dialog" aria-modal="false" aria-labelledby="eng-lifecycle-checkout-confirm-title">
      <div>
        <strong id="eng-lifecycle-checkout-confirm-title">{copy.title}</strong>
        <p>{copy.description}</p>
      </div>
      <div>
        <button onClick={onCancel} disabled={busy}>{cancelLabel}</button>
        <button className="eng-lifecycle-workspace__critical" onClick={onConfirm} disabled={busy}>{copy.confirm}</button>
      </div>
    </div>
  );
}

function Banner({ title, text }: { title: string; text: string }) {
  return <div className="eng-lifecycle-workspace__banner eng-lifecycle-workspace__banner--warning" role="status"><strong>{title}</strong><span>{text}</span></div>;
}

function revisionText(revision: number | null | undefined, copy: Copy) { return revision ? `r${revision}` : copy.none; }

function formatTimestamp(value: string | null | undefined, locale: EngineeringLocale, fallback: string) {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(locale === 'en' ? 'en-US' : locale, { dateStyle: 'short', timeStyle: 'short' }).format(date);
}

function lifecycleCopy(locale: EngineeringLocale) {
  const common = { published: 'Published', active: 'Active', none: '—' };
  if (locale === 'en') return {
    ...common,
    loading: 'Loading lifecycle…',
    loadFailed: 'The project lifecycle could not be loaded.',
    retry: 'Try again',
    refresh: 'Refresh lifecycle',
    refreshing: 'Refreshing…',
    lifecycleFlow: 'Project lifecycle',
    project: 'Project',
    unnamedProject: 'Unnamed project',
    working: 'Working',
    baseRevision: 'Base revision',
    status: 'Status',
    dirty: 'Unsaved changes',
    clean: 'No changes',
    saveRevision: 'Save revision',
    saving: 'Saving…',
    savedRevision: 'Saved revision',
    latestRevision: 'Latest saved revision',
    revisions: 'Saved revisions',
    noRevisions: 'No saved revisions yet',
    workingBase: 'Working base',
    useInWorking: 'Use in Working',
    publish: 'Publish',
    readyToActivate: 'Ready to activate',
    alreadyActive: 'Already Active',
    notPublished: 'No Published revision',
    activateRuntime: 'Activate in Runtime',
    activating: 'Activating…',
    runtimeActive: 'Runtime active',
    runtimeDiverged: 'Runtime differs from Active',
    noActiveRevision: 'No Active revision',
    liveRevision: 'Live: {revision}',
    cancel: 'Cancel',
    never: 'Not recorded',
    notConfigured: 'not configured',
    persistenceUnavailable: 'Revision storage unavailable',
    persistenceUnavailableHint: 'Saving, publishing and activation require configured Engineering persistence.',
    runtimeBindingMismatch: 'Runtime is linked to another project',
    runtimeBindingMismatchHint: 'Configured Runtime project: {configured}. Activation remains blocked.',
    projectNameRequired: 'Project name is required before saving.',
    saved: 'Revision {revision} saved.',
    checkedOut: 'Revision {revision} is now in Working.',
    publishedNotice: 'Revision {revision} published.',
    activated: 'Published revision activated.'
  };
  if (locale === 'es') return {
    ...common,
    loading: 'Cargando ciclo…',
    loadFailed: 'No fue posible cargar el ciclo del proyecto.',
    retry: 'Intentar nuevamente',
    refresh: 'Actualizar ciclo',
    refreshing: 'Actualizando…',
    lifecycleFlow: 'Ciclo del proyecto',
    project: 'Proyecto',
    unnamedProject: 'Proyecto sin nombre',
    working: 'Working',
    baseRevision: 'Revisión base',
    status: 'Estado',
    dirty: 'Cambios pendientes',
    clean: 'Sin cambios',
    saveRevision: 'Guardar revisión',
    saving: 'Guardando…',
    savedRevision: 'Revisión guardada',
    latestRevision: 'Última revisión guardada',
    revisions: 'Revisiones guardadas',
    noRevisions: 'Aún no hay revisiones guardadas',
    workingBase: 'Base de Working',
    useInWorking: 'Usar en Working',
    publish: 'Publicar',
    readyToActivate: 'Lista para activar',
    alreadyActive: 'Ya está Active',
    notPublished: 'Sin revisión Published',
    activateRuntime: 'Activar en Runtime',
    activating: 'Activando…',
    runtimeActive: 'Runtime activo',
    runtimeDiverged: 'Runtime difiere de Active',
    noActiveRevision: 'Sin revisión Active',
    liveRevision: 'En vivo: {revision}',
    cancel: 'Cancelar',
    never: 'No registrado',
    notConfigured: 'no configurado',
    persistenceUnavailable: 'Almacenamiento de revisiones no disponible',
    persistenceUnavailableHint: 'Guardar, publicar y activar requieren persistencia de Engineering configurada.',
    runtimeBindingMismatch: 'Runtime está vinculado a otro proyecto',
    runtimeBindingMismatchHint: 'Proyecto Runtime configurado: {configured}. La activación permanece bloqueada.',
    projectNameRequired: 'Se requiere el nombre del proyecto antes de guardar.',
    saved: 'Revisión {revision} guardada.',
    checkedOut: 'La revisión {revision} está ahora en Working.',
    publishedNotice: 'Revisión {revision} publicada.',
    activated: 'Revisión Published activada.'
  };
  return {
    ...common,
    loading: 'Carregando ciclo…',
    loadFailed: 'Não foi possível carregar o ciclo do projeto.',
    retry: 'Tentar novamente',
    refresh: 'Atualizar ciclo',
    refreshing: 'Atualizando…',
    lifecycleFlow: 'Ciclo do projeto',
    project: 'Projeto',
    unnamedProject: 'Projeto sem nome',
    working: 'Working',
    baseRevision: 'Revisão base',
    status: 'Status',
    dirty: 'Alterações pendentes',
    clean: 'Sem alterações',
    saveRevision: 'Salvar revisão',
    saving: 'Salvando…',
    savedRevision: 'Revisão salva',
    latestRevision: 'Última revisão salva',
    revisions: 'Revisões salvas',
    noRevisions: 'Ainda não há revisões salvas',
    workingBase: 'Base do Working',
    useInWorking: 'Usar no Working',
    publish: 'Publicar',
    readyToActivate: 'Pronto para ativar',
    alreadyActive: 'Já está Active',
    notPublished: 'Sem revisão Published',
    activateRuntime: 'Ativar no Runtime',
    activating: 'Ativando…',
    runtimeActive: 'Runtime ativo',
    runtimeDiverged: 'Runtime diverge de Active',
    noActiveRevision: 'Sem revisão Active',
    liveRevision: 'Ao vivo: {revision}',
    cancel: 'Cancelar',
    never: 'Não registrado',
    notConfigured: 'não configurado',
    persistenceUnavailable: 'Armazenamento de revisões indisponível',
    persistenceUnavailableHint: 'Salvar, publicar e ativar exigem persistência do Engineering configurada.',
    runtimeBindingMismatch: 'Runtime está vinculado a outro projeto',
    runtimeBindingMismatchHint: 'Projeto Runtime configurado: {configured}. A ativação permanece bloqueada.',
    projectNameRequired: 'O nome do projeto é obrigatório antes de salvar.',
    saved: 'Revisão {revision} salva.',
    checkedOut: 'A revisão {revision} está agora no Working.',
    publishedNotice: 'Revisão {revision} publicada.',
    activated: 'Revisão Published ativada.'
  };
}
