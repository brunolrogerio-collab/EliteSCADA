import { useCallback, useEffect, useMemo, useState } from 'react';
import { hasWorkspaceCapability, useEffectiveCapabilities } from '../auth/effectiveCapabilities';
import type { EngineeringLocale } from './i18n';
import './asset-process-intelligence.css';

type ScopeKind = 'Equipment' | 'Location';
type Scope = { kind: ScopeKind; scopeId: string; active: boolean; executionArmed?: boolean };
type Resource = { kind: ScopeKind; scopeId: string; path?: string | null; name: string };
type Snapshot = {
  configuration: { version: number; scopes: Scope[] };
  resources: Resource[];
  licensed: boolean;
  execution: {
    stage: string;
    workerRegistered: boolean;
    queueDepth: number;
    activePermitCount: number;
    projectKey: string | null;
    activeRevision: number | null;
    workspaceDirty: boolean;
  };
};

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export function AssetProcessIntelligenceWorkspace({ locale }: { locale: EngineeringLocale }) {
  const { capabilities } = useEffectiveCapabilities();
  const canEngineer = hasWorkspaceCapability(capabilities, 'EngineeringModify');
  const canManage = hasWorkspaceCapability(capabilities, 'AssetProcessIntelligenceManage');
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [transitioning, setTransitioning] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const copy = useMemo(() => text(locale), [locale]);
  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/scopes`, {
        headers: { accept: 'application/json' }, credentials: 'include'
      });
      if (!response.ok) throw await responseError(response);
      const next = await response.json() as Snapshot;
      setSnapshot(next);
      setSelected(new Set(next.configuration.scopes.map(scopeKey)));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const saveScopes = async () => {
    if (!snapshot) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const scopes = snapshot.resources
        .filter(resource => selected.has(scopeKey(resource)))
        .map(resource => ({ kind: resource.kind === 'Equipment' ? 0 : 1, scopeId: resource.scopeId }));
      // The server remains authoritative for scope access, resource identity and validation.
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/scopes`, {
        method: 'PUT', credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopes })
      });
      if (!response.ok) throw await responseError(response);
      setMessage(copy.saved);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setSaving(false);
    }
  };

  const setActivation = async (scope: Scope, active: boolean) => {
    const key = scopeKey(scope);
    setTransitioning(key);
    setError(null);
    setMessage(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/scopes/${scope.kind.toLowerCase()}/${encodeURIComponent(scope.scopeId)}/${active ? 'activate' : 'pause'}`, {
        method: 'POST', credentials: 'include', headers: { accept: 'application/json' }
      });
      if (!response.ok) throw await responseError(response);
      setMessage(active ? copy.activated : copy.paused);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setTransitioning(null);
    }
  };

  const configured = snapshot?.configuration.scopes ?? [];
  const dirtySelection = snapshot !== null && (
    snapshot.resources.filter(resource => selected.has(scopeKey(resource))).length !== configured.length ||
    configured.some(scope => !selected.has(scopeKey(scope)))
  );

  return <section className="eng-section asset-process-intelligence" data-testid="asset-process-intelligence-workspace">
    <header className="eng-section-header">
      <div><span className="eng-eyebrow">{copy.eyebrow}</span><h1>{copy.title}</h1><p>{copy.description}</p></div>
      {snapshot ? <div className="eng-section-meta"><strong>{snapshot.execution.stage}</strong><span>{snapshot.execution.activePermitCount} {copy.permits}</span></div> : null}
    </header>

    <section className="eng-panel asset-process-intelligence__stage" aria-live="polite">
      <strong>{snapshot?.execution.workerRegistered ? copy.workerAvailable : copy.noWorker}</strong>
      <p>{copy.stageHint}</p>
      {snapshot?.execution.workspaceDirty ? <p role="status">{copy.workingChanges} <a href="/engineering">{copy.openLifecycle}</a></p> : null}
      <div className="asset-process-intelligence__status">
        <span>{copy.entitlement}: <strong>{snapshot?.licensed ? copy.entitled : copy.notEntitled}</strong></span>
        <span>{copy.project}: <strong>{snapshot?.execution.projectKey ?? copy.noProject}</strong></span>
        <span>{copy.revision}: <strong>{snapshot?.execution.activeRevision ?? '—'}</strong></span>
        <span>{copy.queue}: <strong>{snapshot?.execution.queueDepth ?? 0}</strong></span>
      </div>
    </section>

    {error ? <div className="eng-state-card error" role="alert">{error}</div> : null}
    {message ? <div className="eng-state-card" role="status">{message}</div> : null}
    {loading ? <div className="eng-state-card"><strong>{copy.loading}</strong></div> : null}
    {!loading && snapshot && <>
      <section className="eng-panel asset-process-intelligence__scope-panel">
        <header><div><h2>{copy.eligibleScopes}</h2><p>{copy.scopeHint}</p></div>
          <button type="button" className="secondary" onClick={() => void load()}>{copy.refresh}</button>
        </header>
        {snapshot.resources.length === 0 ? <div className="eng-empty"><strong>{copy.noResources}</strong></div> :
          <div className="asset-process-intelligence__list">
            {snapshot.resources.map(resource => {
              const key = scopeKey(resource);
              const scope = configured.find(item => scopeKey(item) === key);
              const active = scope?.active === true;
              const armed = scope?.executionArmed === true;
              return <article key={key} className="asset-process-intelligence__row">
                <label>
                  <input type="checkbox" checked={selected.has(key)} disabled={!canEngineer || active || saving || transitioning !== null}
                    onChange={event => setSelected(current => {
                      const next = new Set(current);
                      if (event.currentTarget.checked) next.add(key); else next.delete(key);
                      return next;
                    })}/>
                  <span><strong>{resource.name}</strong><small>{resource.kind} · {resource.path ?? resource.scopeId}</small></span>
                </label>
                <div className="asset-process-intelligence__controls">
                  {!scope ? <span>{copy.notConfigured}</span> : <>
                    <span>{active ? copy.configuredActive : copy.statusPaused}</span>
                    <span>{armed ? copy.permitArmed : active ? copy.permitClosed : copy.noPermit}</span>
      <button type="button" className="secondary" disabled={!canEngineer || !canManage || (!snapshot.licensed && !active) || (snapshot.execution.workspaceDirty && !active) || transitioning !== null}
                      onClick={() => void setActivation(scope, !active)}>
                      {transitioning === key ? copy.working : active ? copy.pause : copy.activate}
                    </button>
                  </>}
                </div>
              </article>;
            })}
          </div>}
        <footer className="asset-process-intelligence__footer">
          {!canEngineer || !canManage ? <span>{copy.authorizationHint}</span> : null}
          <button type="button" disabled={!canEngineer || !dirtySelection || saving || transitioning !== null} onClick={() => void saveScopes()}>
            {saving ? copy.saving : copy.saveScopes}
          </button>
        </footer>
      </section>
    </>}
  </section>;
}

function scopeKey(scope: { kind: ScopeKind; scopeId: string }) { return `${scope.kind}:${scope.scopeId.toLowerCase()}`; }

async function responseError(response: Response): Promise<Error> {
  let detail = `${response.status} ${response.statusText}`;
  try {
    const payload = await response.json() as { error?: unknown };
    if (typeof payload.error === 'string') detail = payload.error;
  } catch { /* keep status text */ }
  return new Error(detail);
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Engineering · Stage 1', title: 'Asset & Process Intelligence',
    description: 'Choose the equipment and locations eligible for future consultative analysis.',
    noWorker: 'No analytics worker is registered in this Stage 1.', workerAvailable: 'Analytics worker registered',
    stageHint: 'Activation publishes the scope against the current active project revision. It does not run analysis yet.', workingChanges: 'Working changes must be reviewed and published before activation.', openLifecycle: 'Open project lifecycle',
    entitlement: 'Signed entitlement', entitled: 'Available', notEntitled: 'Required to activate', project: 'Project', revision: 'Active revision', queue: 'Queue', permits: 'execution permits',
    noProject: 'No active project', eligibleScopes: 'Eligible scopes', scopeHint: 'Select stable Equipment and Location identities. Saving changes Working only; publish it before activation.',
    noResources: 'No stable Equipment or Location resources are available.', refresh: 'Refresh', notConfigured: 'Not selected', configuredActive: 'Configured active', statusPaused: 'Paused', permitArmed: 'Process permit armed', permitClosed: 'Persisted active · process permit closed; explicitly reactivate after restart', noPermit: 'No process permit', activate: 'Activate', pause: 'Pause', working: 'Applying…', saveScopes: 'Save eligible scopes', saving: 'Saving…', saved: 'Eligible scopes saved.', activated: 'Scope activated for the current revision.', paused: 'Scope paused; pending work was cancelled.', authorizationHint: 'EngineeringModify and a matching resource-scoped AssetProcessIntelligenceManage grant are required.', loading: 'Loading scopes…'
  } as const;
  if (locale === 'es') return {
    eyebrow: 'Engineering · Etapa 1', title: 'Inteligencia de Activos y Procesos',
    description: 'Seleccione los equipos y ubicaciones aptos para futuros análisis consultivos.',
    noWorker: 'Esta etapa aún no registra ningún worker analítico.', workerAvailable: 'Worker analítico registrado',
    stageHint: 'La activación publica el alcance contra la revisión activa del proyecto. Todavía no ejecuta análisis.', workingChanges: 'Revise y publique los cambios de trabajo antes de activar.', openLifecycle: 'Abrir ciclo del proyecto',
    entitlement: 'Licencia firmada', entitled: 'Disponible', notEntitled: 'Necesaria para activar', project: 'Proyecto', revision: 'Revisión activa', queue: 'Cola', permits: 'permisos de ejecución',
    noProject: 'Sin proyecto activo', eligibleScopes: 'Alcances elegibles', scopeHint: 'Seleccione identidades estables. Guardar modifica solo Working; publique antes de activar.',
    noResources: 'No hay recursos estables de equipo o ubicación.', refresh: 'Actualizar', notConfigured: 'No seleccionado', configuredActive: 'Activo configurado', statusPaused: 'Pausado', permitArmed: 'Permiso de proceso habilitado', permitClosed: 'Activo persistido · permiso cerrado; reactive explícitamente tras reinicio', noPermit: 'Sin permiso de proceso', activate: 'Activar', pause: 'Pausar', working: 'Aplicando…', saveScopes: 'Guardar alcances', saving: 'Guardando…', saved: 'Alcances guardados.', activated: 'Alcance activado para la revisión actual.', paused: 'Alcance pausado; se canceló el trabajo pendiente.', authorizationHint: 'Se requiere EngineeringModify y un permiso AssetProcessIntelligenceManage para ese recurso.', loading: 'Cargando alcances…'
  } as const;
  return {
    eyebrow: 'Engineering · Stage 1', title: 'Inteligência de Ativos e Processos',
    description: 'Selecione equipamentos e locais elegíveis para futuras análises consultivas.',
    noWorker: 'Esta Stage 1 ainda não registra nenhum worker analítico.', workerAvailable: 'Worker analítico registrado',
    stageHint: 'A ativação publica o escopo na revisão ativa do projeto. Ainda não executa análises.', workingChanges: 'Revise e publique as alterações de Working antes de ativar.', openLifecycle: 'Abrir ciclo do projeto',
    entitlement: 'Licença assinada', entitled: 'Disponível', notEntitled: 'Necessária para ativar', project: 'Projeto', revision: 'Revisão ativa', queue: 'Fila', permits: 'permissões de execução',
    noProject: 'Nenhum projeto ativo', eligibleScopes: 'Escopos elegíveis', scopeHint: 'Selecione identidades estáveis. Salvar altera apenas o Working; publique antes de ativar.',
    noResources: 'Não há equipamentos ou locais com identidade estável.', refresh: 'Atualizar', notConfigured: 'Não selecionado', configuredActive: 'Ativo na configuração', statusPaused: 'Pausado', permitArmed: 'Permissão de processo armada', permitClosed: 'Ativo persistido · permissão fechada; reative explicitamente após reiniciar', noPermit: 'Sem permissão de processo', activate: 'Ativar', pause: 'Pausar', working: 'Aplicando…', saveScopes: 'Salvar escopos elegíveis', saving: 'Salvando…', saved: 'Escopos elegíveis salvos.', activated: 'Escopo ativado para a revisão atual.', paused: 'Escopo pausado; o trabalho pendente foi cancelado.', authorizationHint: 'São necessários EngineeringModify e AssetProcessIntelligenceManage com escopo para este recurso.', loading: 'Carregando escopos…'
  } as const;
}
