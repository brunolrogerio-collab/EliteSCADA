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
  confirmationText,
  isRevisionActive,
  isRevisionPublished,
  isWorkspaceBaseRevision,
  lifecycleErrorText,
  projectLifecycleName,
  runtimeLifecycleName
} from './EngineeringLifecycleWorkspace.logic';
import type { EngineeringLifecycleAction, EngineeringLifecycleState, EngineeringRevisionMetadata } from './engineeringLifecycleTypes';
import type { EngineeringLocale } from './i18n';
import './engineering-lifecycle-workspace.css';

type PendingConfirmation = { action: EngineeringLifecycleAction; revision?: number };
type Copy = ReturnType<typeof lifecycleCopy>;

export function EngineeringLifecycleWorkspace({ locale }: { locale: EngineeringLocale }) {
  const copy = useMemo(() => lifecycleCopy(locale), [locale]);
  const [state, setState] = useState<EngineeringLifecycleState | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<PendingConfirmation | null>(null);

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
    setBusy(true);
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
      setConfirmation(null);
      await refresh();
    } catch (cause) {
      setError(lifecycleErrorText(cause, locale));
    } finally {
      setBusy(false);
    }
  }

  if (loading && !state) return <section className="eng-lifecycle-workspace eng-lifecycle-workspace--loading">{copy.loading}</section>;
  if (!state) return <section className="eng-lifecycle-workspace"><h2>{copy.title}</h2><p role="alert">{error ?? copy.loadFailed}</p><button onClick={() => void refresh()}>{copy.retry}</button></section>;

  const lifecycle = state.lifecycle;
  const runtime = state.runtime;
  const steps = buildLifecycleSteps(state);
  const configuredProjectMatches = Boolean(state.projectKey && state.persistence.configuredProjectKey && state.persistence.configuredProjectKey.toLowerCase() === state.projectKey.toLowerCase());

  return (
    <section className="eng-lifecycle-workspace" aria-label={copy.title}>
      <header className="eng-lifecycle-workspace__header">
        <div><span className="eng-lifecycle-workspace__eyebrow">{copy.eyebrow}</span><h2>{copy.title}</h2><p>{copy.description}</p></div>
        <button className="eng-lifecycle-workspace__refresh" onClick={() => void refresh()} disabled={loading || busy}>{loading ? copy.refreshing : copy.refresh}</button>
      </header>

      {!state.persistence.enabled && <Banner title={copy.persistenceUnavailable} text={copy.persistenceUnavailableHint} />}
      {state.persistence.enabled && state.projectKey && !configuredProjectMatches && <Banner title={copy.runtimeBindingMismatch} text={copy.runtimeBindingMismatchHint.replace('{configured}', state.persistence.configuredProjectKey ?? copy.notConfigured)} />}
      {error && <p className="eng-lifecycle-workspace__error" role="alert">{error}</p>}
      {notice && <p className="eng-lifecycle-workspace__notice" role="status">{notice}</p>}

      <div className="eng-lifecycle-workspace__facts">
        <Fact label={copy.project} value={state.workspace.projectName || copy.unnamedProject} detail={state.projectKey ?? copy.noProjectKey} />
        <Fact label={copy.working} value={state.workspace.isDirty ? copy.dirty : copy.clean} detail={`${copy.changeVersion}: ${state.workspace.changeVersion}`} attention={state.workspace.isDirty} />
        <Fact label={copy.baseRevision} value={revisionText(state.workspace.baseRevision, copy)} detail={formatTimestamp(state.workspace.lastSavedAtUtc, locale, copy.never)} />
        <Fact label={copy.lifecycleStatus} value={translate(projectLifecycleName(lifecycle?.status), copy.projectStatuses)} detail={translate(runtimeLifecycleName(lifecycle?.runtimeStatus), copy.runtimeStatuses)} />
        <Fact label={copy.publishedRevision} value={revisionText(lifecycle?.publishedRevision, copy)} detail={formatTimestamp(lifecycle?.publishedAtUtc, locale, copy.never)} />
        <Fact label={copy.activeRevision} value={revisionText(lifecycle?.activeRevision, copy)} detail={formatTimestamp(lifecycle?.activatedAtUtc, locale, copy.never)} />
        <Fact label={copy.liveRuntime} value={revisionText(runtime?.live.revision, copy)} detail={runtime?.consistent ? copy.runtimeConsistent : copy.runtimeDiverged} attention={runtime?.consistent === false} />
      </div>

      <ol className="eng-lifecycle-workspace__steps" aria-label={copy.lifecycleFlow}>
        {steps.map((step, index) => <li key={step.key} className={`eng-lifecycle-workspace__step eng-lifecycle-workspace__step--${step.state}`}><span>{index + 1}</span><div><strong>{stepLabel(step.key, copy)}</strong><small>{revisionText(step.revision, copy)}</small></div></li>)}
      </ol>

      <div className="eng-lifecycle-workspace__actions">
        <div><h3>{copy.workingActions}</h3><p>{copy.workingActionsHint}</p></div>
        <div className="eng-lifecycle-workspace__action-buttons">
          <button onClick={() => void perform('save')} disabled={busy || !canSaveWorkspace(state)}>{copy.saveRevision}</button>
          <button onClick={() => setConfirmation({ action: 'activate' })} disabled={busy || !canActivatePublished(state)}>{copy.activatePublished}</button>
        </div>
      </div>
      {!canSaveWorkspace(state) && state.persistence.enabled && state.workspace.baseRevision && !state.workspace.isDirty && <p className="eng-lifecycle-workspace__hint">{copy.cleanSaveHint}</p>}

      <section className="eng-lifecycle-workspace__revisions" aria-label={copy.revisions}>
        <div className="eng-lifecycle-workspace__section-heading"><div><h3>{copy.revisions}</h3><p>{copy.revisionsHint}</p></div><strong>{state.revisions.length}</strong></div>
        {state.revisions.length === 0 ? <p className="eng-lifecycle-workspace__empty">{copy.noRevisions}</p> : <div className="eng-lifecycle-workspace__revision-list">{state.revisions.map(revision => <RevisionRow key={revision.revision} revision={revision} state={state} locale={locale} copy={copy} busy={busy} onCheckout={() => setConfirmation({ action: 'checkout', revision: revision.revision })} onPublish={() => setConfirmation({ action: 'publish', revision: revision.revision })} />)}</div>}
      </section>

      {confirmation && <ConfirmationPanel confirmation={confirmation} locale={locale} cancelLabel={copy.cancel} busy={busy} onCancel={() => setConfirmation(null)} onConfirm={() => void perform(confirmation.action, confirmation.revision)} />}
      <footer className="eng-lifecycle-workspace__authority"><strong>{copy.authorityTitle}</strong><span>{copy.authorityHint}</span></footer>
    </section>
  );
}

function RevisionRow({ revision, state, locale, copy, busy, onCheckout, onPublish }: { revision: EngineeringRevisionMetadata; state: EngineeringLifecycleState; locale: EngineeringLocale; copy: Copy; busy: boolean; onCheckout: () => void; onPublish: () => void }) {
  return <article className="eng-lifecycle-workspace__revision-row">
    <div className="eng-lifecycle-workspace__revision-id"><strong>r{revision.revision}</strong><span>{revision.projectName}</span></div>
    <div className="eng-lifecycle-workspace__revision-meta"><span>{formatTimestamp(revision.savedAtUtc, locale, copy.never)}</span><span>{revision.basedOnRevision ? `${copy.basedOn} r${revision.basedOnRevision}` : copy.rootRevision}</span></div>
    <div className="eng-lifecycle-workspace__badges">{isWorkspaceBaseRevision(revision, state) && <span>{copy.workingBase}</span>}{isRevisionPublished(revision, state.lifecycle) && <span>{copy.published}</span>}{isRevisionActive(revision, state.lifecycle) && <span>{copy.active}</span>}</div>
    <div className="eng-lifecycle-workspace__row-actions"><button onClick={onCheckout} disabled={busy || isWorkspaceBaseRevision(revision, state)}>{copy.checkout}</button><button onClick={onPublish} disabled={busy || isRevisionPublished(revision, state.lifecycle)}>{copy.publish}</button></div>
  </article>;
}

function ConfirmationPanel({ confirmation, locale, cancelLabel, busy, onCancel, onConfirm }: { confirmation: PendingConfirmation; locale: EngineeringLocale; cancelLabel: string; busy: boolean; onCancel: () => void; onConfirm: () => void }) {
  const copy = confirmationText(confirmation.action, locale, confirmation.revision);
  return <div className="eng-lifecycle-workspace__confirmation" role="dialog" aria-modal="false" aria-labelledby="eng-lifecycle-confirm-title"><div><strong id="eng-lifecycle-confirm-title">{copy.title}</strong><p>{copy.description}</p></div><div><button onClick={onCancel} disabled={busy}>{cancelLabel}</button><button className="eng-lifecycle-workspace__critical" onClick={onConfirm} disabled={busy}>{copy.confirm}</button></div></div>;
}

function Banner({ title, text }: { title: string; text: string }) { return <div className="eng-lifecycle-workspace__banner eng-lifecycle-workspace__banner--warning" role="status"><strong>{title}</strong><span>{text}</span></div>; }
function Fact({ label, value, detail, attention = false }: { label: string; value: string; detail: string; attention?: boolean }) { return <div className={`eng-lifecycle-workspace__fact${attention ? ' eng-lifecycle-workspace__fact--attention' : ''}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></div>; }
function revisionText(revision: number | null | undefined, copy: Copy) { return revision ? `r${revision}` : copy.none; }
function translate(value: string, map: Record<string, string>) { return map[value] ?? value; }
function formatTimestamp(value: string | null | undefined, locale: EngineeringLocale, fallback: string) { if (!value) return fallback; const date = new Date(value); if (Number.isNaN(date.getTime())) return value; return new Intl.DateTimeFormat(locale === 'en' ? 'en-US' : locale, { dateStyle: 'short', timeStyle: 'short' }).format(date); }
function stepLabel(key: 'working' | 'revision' | 'published' | 'active', copy: Copy) { return { working: copy.working, revision: copy.savedRevision, published: copy.published, active: copy.active }[key]; }

function lifecycleCopy(locale: EngineeringLocale) {
  const common = { published: 'Published', active: 'Active', none: '—' };
  if (locale === 'en') return { ...common, eyebrow: 'Authoritative project lifecycle', title: 'Engineering Lifecycle', description: 'Operate Working, immutable Revisions, Published and Active without bypassing backend authority.', loading: 'Loading Engineering lifecycle…', loadFailed: 'Engineering lifecycle could not be loaded.', retry: 'Try again', refresh: 'Refresh', refreshing: 'Refreshing…', project: 'Project', unnamedProject: 'Unnamed project', noProjectKey: 'No project key', working: 'Working', dirty: 'Unsaved changes', clean: 'Clean', changeVersion: 'Change version', baseRevision: 'Base revision', lifecycleStatus: 'Lifecycle', publishedRevision: 'Published revision', activeRevision: 'Active revision', liveRuntime: 'Live Runtime', runtimeConsistent: 'Matches durable Active revision', runtimeDiverged: 'Runtime differs from durable Active revision', lifecycleFlow: 'Engineering lifecycle flow', workingActions: 'Working actions', workingActionsHint: 'Save creates an immutable revision. Publication and activation remain separate.', saveRevision: 'Save revision', activatePublished: 'Activate Published', cleanSaveHint: 'Working is clean and already based on a saved revision.', revisions: 'Revisions', revisionsHint: 'Checkout changes Working. Publish selects Published; it does not activate Runtime.', noRevisions: 'No saved revisions exist.', basedOn: 'Based on', rootRevision: 'Initial/root revision', workingBase: 'Working base', savedRevision: 'Saved revision', checkout: 'Checkout', publish: 'Publish', cancel: 'Cancel', never: 'Not recorded', notConfigured: 'not configured', persistenceUnavailable: 'Engineering persistence is unavailable', persistenceUnavailableHint: 'Lifecycle actions require configured PostgreSQL Engineering persistence.', runtimeBindingMismatch: 'Runtime project binding does not match Working', runtimeBindingMismatchHint: 'Configured Runtime project: {configured}. Activation is blocked because the backend will reject a different project key.', projectNameRequired: 'Project name is required before saving.', saved: 'Saved as revision {revision}.', checkedOut: 'Revision {revision} checked out into Working.', publishedNotice: 'Revision {revision} published.', activated: 'Published revision activated successfully.', authorityTitle: 'Backend authority preserved', authorityHint: 'Authentication, EngineeringModify authorization, Audit, validation and activation are enforced by the server. This UI never supplies trusted operator identity.', projectStatuses: { Empty: 'Empty', Draft: 'Draft', Published: 'Published', ChangesPending: 'Changes pending', Unknown: 'Unknown' } as Record<string, string>, runtimeStatuses: { Inactive: 'Inactive', ActivationPending: 'Activation pending', Active: 'Active', Unknown: 'Unknown' } as Record<string, string> };
  if (locale === 'es') return { ...common, eyebrow: 'Ciclo autoritativo del proyecto', title: 'Ciclo de Engineering', description: 'Opere Working, Revisiones inmutables, Published y Active sin evitar la autoridad del backend.', loading: 'Cargando ciclo de Engineering…', loadFailed: 'No fue posible cargar el ciclo de Engineering.', retry: 'Intentar nuevamente', refresh: 'Actualizar', refreshing: 'Actualizando…', project: 'Proyecto', unnamedProject: 'Proyecto sin nombre', noProjectKey: 'Sin clave de proyecto', working: 'Working', dirty: 'Cambios sin guardar', clean: 'Sin cambios', changeVersion: 'Versión de cambio', baseRevision: 'Revisión base', lifecycleStatus: 'Ciclo', publishedRevision: 'Revisión Published', activeRevision: 'Revisión Active', liveRuntime: 'Runtime en vivo', runtimeConsistent: 'Coincide con Active durable', runtimeDiverged: 'Runtime difiere de Active durable', lifecycleFlow: 'Flujo del ciclo', workingActions: 'Acciones de Working', workingActionsHint: 'Guardar crea una revisión inmutable. Publicar y activar siguen separados.', saveRevision: 'Guardar revisión', activatePublished: 'Activar Published', cleanSaveHint: 'Working está limpio y ya se basa en una revisión guardada.', revisions: 'Revisiones', revisionsHint: 'Checkout cambia Working. Publish selecciona Published; no activa Runtime.', noRevisions: 'No existen revisiones guardadas.', basedOn: 'Basada en', rootRevision: 'Revisión inicial/raíz', workingBase: 'Base de Working', savedRevision: 'Revisión guardada', checkout: 'Checkout', publish: 'Publicar', cancel: 'Cancelar', never: 'No registrado', notConfigured: 'no configurado', persistenceUnavailable: 'La persistencia de Engineering no está disponible', persistenceUnavailableHint: 'Las acciones requieren persistencia PostgreSQL de Engineering.', runtimeBindingMismatch: 'El proyecto Runtime no coincide con Working', runtimeBindingMismatchHint: 'Proyecto Runtime configurado: {configured}. La activación está bloqueada porque el backend rechazará otra clave.', projectNameRequired: 'Se requiere el nombre del proyecto antes de guardar.', saved: 'Guardado como revisión {revision}.', checkedOut: 'Revisión {revision} cargada en Working.', publishedNotice: 'Revisión {revision} publicada.', activated: 'La revisión Published se activó correctamente.', authorityTitle: 'Autoridad del backend preservada', authorityHint: 'Autenticación, EngineeringModify, Audit, validación y activación se aplican en el servidor. Esta UI no envía identidad confiable del operador.', projectStatuses: { Empty: 'Vacío', Draft: 'Borrador', Published: 'Published', ChangesPending: 'Cambios pendientes', Unknown: 'Desconocido' } as Record<string, string>, runtimeStatuses: { Inactive: 'Inactivo', ActivationPending: 'Activación pendiente', Active: 'Active', Unknown: 'Desconocido' } as Record<string, string> };
  return { ...common, eyebrow: 'Ciclo autoritativo do projeto', title: 'Ciclo do Engineering', description: 'Opere Working, Revisões imutáveis, Published e Active sem contornar a autoridade do backend.', loading: 'Carregando ciclo do Engineering…', loadFailed: 'Não foi possível carregar o ciclo do Engineering.', retry: 'Tentar novamente', refresh: 'Atualizar', refreshing: 'Atualizando…', project: 'Projeto', unnamedProject: 'Projeto sem nome', noProjectKey: 'Sem chave do projeto', working: 'Working', dirty: 'Alterações não salvas', clean: 'Sem alterações', changeVersion: 'Versão de mudança', baseRevision: 'Revisão base', lifecycleStatus: 'Ciclo', publishedRevision: 'Revisão Published', activeRevision: 'Revisão Active', liveRuntime: 'Runtime ao vivo', runtimeConsistent: 'Coincide com Active durável', runtimeDiverged: 'Runtime diverge de Active durável', lifecycleFlow: 'Fluxo do ciclo', workingActions: 'Ações do Working', workingActionsHint: 'Salvar cria uma revisão imutável. Publicar e ativar continuam separados.', saveRevision: 'Salvar revisão', activatePublished: 'Ativar Published', cleanSaveHint: 'O Working está limpo e já se baseia em uma revisão salva.', revisions: 'Revisões', revisionsHint: 'Checkout altera o Working. Publish escolhe Published; não ativa o Runtime.', noRevisions: 'Não existem revisões salvas.', basedOn: 'Baseada em', rootRevision: 'Revisão inicial/raiz', workingBase: 'Base do Working', savedRevision: 'Revisão salva', checkout: 'Checkout', publish: 'Publicar', cancel: 'Cancelar', never: 'Não registrado', notConfigured: 'não configurado', persistenceUnavailable: 'Persistência do Engineering indisponível', persistenceUnavailableHint: 'As ações exigem persistência PostgreSQL de Engineering configurada.', runtimeBindingMismatch: 'O projeto Runtime não corresponde ao Working', runtimeBindingMismatchHint: 'Projeto Runtime configurado: {configured}. A ativação fica bloqueada porque o backend rejeitará outra chave.', projectNameRequired: 'O nome do projeto é obrigatório antes de salvar.', saved: 'Salvo como revisão {revision}.', checkedOut: 'Revisão {revision} carregada no Working.', publishedNotice: 'Revisão {revision} publicada.', activated: 'A revisão Published foi ativada com sucesso.', authorityTitle: 'Autoridade do backend preservada', authorityHint: 'Autenticação, EngineeringModify, Audit, validação e ativação são aplicadas pelo servidor. Esta UI não envia identidade confiável do operador.', projectStatuses: { Empty: 'Vazio', Draft: 'Rascunho', Published: 'Published', ChangesPending: 'Alterações pendentes', Unknown: 'Desconhecido' } as Record<string, string>, runtimeStatuses: { Inactive: 'Inativo', ActivationPending: 'Ativação pendente', Active: 'Active', Unknown: 'Desconhecido' } as Record<string, string> };
}
