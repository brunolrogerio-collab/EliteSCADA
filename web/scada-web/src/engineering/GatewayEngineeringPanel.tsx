import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  loadGatewayDiagnostics,
  previewEngineeringPackage
} from './api';
import type { EngineeringLocale } from './i18n';
import type {
  EngineeringPackageView,
  GatewayEngineering,
  GatewayRuntimeDiagnostic,
  ImportPreviewView,
  TagEngineering
} from './types';
import './engineering-mutations.css';

const CLIENT_MEMORY_DRIVER = 'builtin.memory.client';
const SIMULATION_DRIVER = 'builtin.simulation';

type Props = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
};

type Draft = {
  id: string;
  key: string;
  name: string;
  sourceTagId: string;
  destinationTagId: string;
  transferMode: 'onChange' | 'periodic';
  conversionPolicy: 'exact' | 'checkedNumeric';
  initialTransferPolicy: 'waitForNextAcceptableValue' | 'synchronizeFirstAcceptableValue';
  gain: string;
  offset: string;
  deadband: string;
  minimumIntervalMilliseconds: string;
  periodMilliseconds: string;
  enabled: boolean;
};

export function GatewayEngineeringPanel({ model, locale }: Props) {
  const text = labels(locale);
  const eligibleTags = useMemo(() => collectEligibleTags(model), [model]);
  const writableTags = useMemo(() => eligibleTags.filter(tag => !tag.readOnly), [eligibleTags]);
  const routes = model.gateways ?? [];
  const [selected, setSelected] = useState<string>('new');
  const [draft, setDraft] = useState<Draft>(() => emptyDraft(eligibleTags, writableTags));
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<EngineeringPackageView | null>(null);
  const [validatedVersion, setValidatedVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [diagnostics, setDiagnostics] = useState<GatewayRuntimeDiagnostic[]>([]);
  const [diagnosticError, setDiagnosticError] = useState<string | null>(null);
  const [loadingDiagnostics, setLoadingDiagnostics] = useState(false);

  const refreshDiagnostics = async () => {
    setLoadingDiagnostics(true);
    setDiagnosticError(null);
    try {
      setDiagnostics(await loadGatewayDiagnostics());
    } catch (reason) {
      setDiagnosticError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoadingDiagnostics(false);
    }
  };

  useEffect(() => {
    void refreshDiagnostics();
  }, []);

  const invalidate = () => {
    setPreview(null);
    setCandidate(null);
    setValidatedVersion(null);
    setError(null);
  };

  const chooseRoute = (identity: string) => {
    setSelected(identity);
    const route = routes.find(item => routeIdentity(item) === identity);
    setDraft(route ? routeDraft(route, eligibleTags, writableTags) : emptyDraft(eligibleTags, writableTags));
    invalidate();
  };

  const change = <K extends keyof Draft>(key: K, value: Draft[K]) => {
    setDraft(current => ({ ...current, [key]: value }));
    invalidate();
  };

  const runPreview = async () => {
    setPreviewing(true);
    setError(null);
    setPreview(null);
    setCandidate(null);
    setValidatedVersion(null);
    try {
      const route = buildRoute(draft, eligibleTags, text);
      const before = await loadEngineeringWorkspace();
      const next = clone(model);
      const existingIndex = (next.gateways ?? []).findIndex(item => routeIdentity(item) === selected);
      next.gateways = [...(next.gateways ?? [])];
      if (existingIndex >= 0) next.gateways[existingIndex] = route;
      else next.gateways.push(route);

      const nextPreview = await previewEngineeringPackage(next);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion) throw new Error(text.workspaceChanged);

      setPreview(nextPreview);
      setCandidate(next);
      setValidatedVersion(after.changeVersion);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  const runApply = async () => {
    if (!candidate || validatedVersion === null || !preview?.canApply) return;
    setApplying(true);
    setError(null);
    try {
      await applyEngineeringPackage(candidate, validatedVersion);
      window.location.reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      invalidate();
    } finally {
      setApplying(false);
    }
  };

  return (
    <div className="eng-section" data-testid="gateway-engineering-panel">
      <header className="eng-section-header">
        <div>
          <span className="eng-eyebrow">TAG Gateway</span>
          <h1>{text.title}</h1>
          <p>{text.description}</p>
        </div>
        <div className="eng-section-meta">
          <strong>{routes.length} {text.routes}</strong>
          <span>Engineering Schema v{model.schemaVersion}</span>
        </div>
      </header>

      <section className="eng-mutation-panel">
        <header className="eng-mutation-header">
          <div>
            <span>{text.editorEyebrow}</span>
            <h2>{text.editorTitle}</h2>
            <p>{text.editorHint}</p>
          </div>
          <div className="eng-mutation-warning">{text.warning}</div>
        </header>

        <div className="eng-mutation-grid">
          <section className="eng-mutation-card">
            <header><strong>{text.route}</strong><span>{text.routeHint}</span></header>
            <label className="eng-mutation-field">
              <span>{text.route}</span>
              <select value={selected} onChange={event => chooseRoute(event.target.value)} disabled={previewing || applying} data-testid="gateway-route-select">
                <option value="new">{text.newRoute}</option>
                {routes.map(route => <option key={routeIdentity(route)} value={routeIdentity(route)}>{route.key}</option>)}
              </select>
            </label>
            <label className="eng-mutation-field"><span>{text.key}</span><input value={draft.key} onChange={event => change('key', event.target.value)} data-testid="gateway-key" /></label>
            <label className="eng-mutation-field"><span>{text.name}</span><input value={draft.name} onChange={event => change('name', event.target.value)} /></label>
            <label className="eng-mutation-field">
              <span>{text.enabled}</span>
              <select value={draft.enabled ? 'true' : 'false'} onChange={event => change('enabled', event.target.value === 'true')}>
                <option value="true">{text.yes}</option><option value="false">{text.no}</option>
              </select>
            </label>
          </section>

          <section className="eng-mutation-card">
            <header><strong>{text.endpoints}</strong><span>{text.endpointsHint}</span></header>
            <label className="eng-mutation-field">
              <span>{text.source}</span>
              <select value={draft.sourceTagId} onChange={event => change('sourceTagId', event.target.value)} data-testid="gateway-source">
                <option value="">{text.selectTag}</option>
                {eligibleTags.map(tag => <option key={tag.id} value={tag.id}>{tag.path} · {tag.dataType}</option>)}
              </select>
            </label>
            <label className="eng-mutation-field">
              <span>{text.destination}</span>
              <select value={draft.destinationTagId} onChange={event => change('destinationTagId', event.target.value)} data-testid="gateway-destination">
                <option value="">{text.selectTag}</option>
                {writableTags.map(tag => <option key={tag.id} value={tag.id}>{tag.path} · {tag.dataType}</option>)}
              </select>
            </label>
            <code className="eng-mutation-detail">{text.serverOnly}</code>
          </section>

          <section className="eng-mutation-card">
            <header><strong>{text.transfer}</strong><span>{text.transferHint}</span></header>
            <label className="eng-mutation-field"><span>{text.mode}</span>
              <select value={draft.transferMode} onChange={event => change('transferMode', event.target.value as Draft['transferMode'])} data-testid="gateway-mode">
                <option value="onChange">OnChange</option><option value="periodic">Periodic</option>
              </select>
            </label>
            <label className="eng-mutation-field"><span>{text.startup}</span>
              <select value={draft.initialTransferPolicy} onChange={event => change('initialTransferPolicy', event.target.value as Draft['initialTransferPolicy'])}>
                <option value="synchronizeFirstAcceptableValue">{text.synchronize}</option>
                <option value="waitForNextAcceptableValue">{text.waitNext}</option>
              </select>
            </label>
            {draft.transferMode === 'onChange' ? <>
              <label className="eng-mutation-field"><span>{text.deadband}</span><input inputMode="decimal" value={draft.deadband} onChange={event => change('deadband', event.target.value)} /></label>
              <label className="eng-mutation-field"><span>{text.minimumInterval}</span><input inputMode="numeric" value={draft.minimumIntervalMilliseconds} onChange={event => change('minimumIntervalMilliseconds', event.target.value)} /></label>
            </> :
              <label className="eng-mutation-field"><span>{text.period}</span><input inputMode="numeric" value={draft.periodMilliseconds} onChange={event => change('periodMilliseconds', event.target.value)} data-testid="gateway-period" /></label>}
          </section>

          <section className="eng-mutation-card">
            <header><strong>{text.conversion}</strong><span>{text.conversionHint}</span></header>
            <label className="eng-mutation-field"><span>{text.policy}</span>
              <select value={draft.conversionPolicy} onChange={event => change('conversionPolicy', event.target.value as Draft['conversionPolicy'])}>
                <option value="exact">Exact</option><option value="checkedNumeric">CheckedNumeric</option>
              </select>
            </label>
            {draft.conversionPolicy === 'checkedNumeric' && <>
              <label className="eng-mutation-field"><span>Gain</span><input inputMode="decimal" value={draft.gain} onChange={event => change('gain', event.target.value)} /></label>
              <label className="eng-mutation-field"><span>Offset</span><input inputMode="decimal" value={draft.offset} onChange={event => change('offset', event.target.value)} /></label>
            </>}
            <code className="eng-mutation-detail">Quality: GoodOnly</code>
          </section>
        </div>

        <div className="eng-mutation-actions">
          <button type="button" className="secondary" onClick={() => void runPreview()} disabled={previewing || applying || eligibleTags.length === 0 || writableTags.length === 0} data-testid="gateway-preview">
            {previewing ? text.previewing : text.preview}
          </button>
          <button type="button" className="primary" onClick={() => void runApply()} disabled={!preview?.canApply || previewing || applying} data-testid="gateway-apply">
            {applying ? text.applying : text.apply}
          </button>
        </div>

        {preview && <div className={preview.canApply ? 'eng-bulk-preview valid' : 'eng-bulk-preview invalid'} data-testid="gateway-preview-result">
          <strong>{preview.canApply ? text.valid : text.invalid}</strong>
          <span>{text.creates}: <b>{preview.createCount}</b></span>
          <span>{text.updates}: <b>{preview.updateCount}</b></span>
          <span>{text.errors}: <b>{preview.errorCount}</b></span>
        </div>}
        {preview && preview.errorCount > 0 && <pre className="eng-mutation-error">{preview.items.flatMap(item => item.issues).filter(issue => issue.isError).map(issue => `${issue.code}: ${issue.message}`).join('\n')}</pre>}
        {error && <pre className="eng-mutation-error" aria-live="polite">{error}</pre>}
      </section>

      <section className="eng-panel eng-table-panel" data-testid="gateway-diagnostics">
        <div className="eng-mutation-header">
          <div><span>{text.runtimeEyebrow}</span><h2>{text.runtimeTitle}</h2><p>{text.runtimeHint}</p></div>
          <button type="button" className="secondary" disabled={loadingDiagnostics} onClick={() => void refreshDiagnostics()}>{loadingDiagnostics ? text.loading : text.refresh}</button>
        </div>
        {diagnosticError && <pre className="eng-mutation-error">{diagnosticError}</pre>}
        {!diagnosticError && diagnostics.length === 0 ? <div className="eng-empty"><strong>{text.noRuntimeRoutes}</strong><span>{text.noRuntimeHint}</span></div> :
          <div className="eng-table-wrap"><table className="eng-table"><thead><tr>
            <th>{text.route}</th><th>{text.state}</th><th>{text.endpoints}</th><th>{text.transfers}</th><th>{text.skipped}</th><th>{text.failures}</th><th>{text.lastError}</th>
          </tr></thead><tbody>{diagnostics.map(item => <tr key={item.routeId}>
            <td><code className="eng-code">{item.key}</code></td>
            <td>{item.state}</td>
            <td><code className="eng-code">{item.sourceTagPath} → {item.destinationTagPath}</code></td>
            <td>{item.transferCount}</td><td>{item.skippedTransferCount} / {item.coalescedUpdateCount}</td>
            <td>{item.writeFailureCount} ({item.consecutiveFailures})</td><td>{item.lastError ?? '—'}</td>
          </tr>)}</tbody></table></div>}
      </section>
    </div>
  );
}

function collectEligibleTags(model: EngineeringPackageView): TagEngineering[] {
  const sources = new Map((model.dataSources ?? []).map(source => [source.key.toLowerCase(), source]));
  return model.tags.filter(tag => {
    if (!tag.id || !tag.source) return false;
    const source = sources.get(tag.source.toLowerCase());
    if (!source || source.enabled === false) return false;
    const driver = source.driver.toLowerCase();
    return driver !== CLIENT_MEMORY_DRIVER && driver !== SIMULATION_DRIVER;
  }).sort((left, right) => left.path.localeCompare(right.path));
}

function emptyDraft(sources: TagEngineering[], destinations: TagEngineering[]): Draft {
  return {
    id: crypto.randomUUID(), key: '', name: '', sourceTagId: sources[0]?.id ?? '', destinationTagId: destinations[0]?.id ?? '',
    transferMode: 'onChange', conversionPolicy: 'exact', initialTransferPolicy: 'synchronizeFirstAcceptableValue',
    gain: '', offset: '', deadband: '', minimumIntervalMilliseconds: '', periodMilliseconds: '1000', enabled: true
  };
}

function routeDraft(route: GatewayEngineering, sources: TagEngineering[], destinations: TagEngineering[]): Draft {
  return {
    id: route.id ?? crypto.randomUUID(), key: route.key, name: route.name,
    sourceTagId: route.sourceTagId ?? sources.find(tag => tag.path === route.sourceTagPath)?.id ?? '',
    destinationTagId: route.destinationTagId ?? destinations.find(tag => tag.path === route.destinationTagPath)?.id ?? '',
    transferMode: normalizeEnum(route.transferMode) === 'periodic' ? 'periodic' : 'onChange',
    conversionPolicy: normalizeEnum(route.conversionPolicy) === 'checkednumeric' ? 'checkedNumeric' : 'exact',
    initialTransferPolicy: normalizeEnum(route.initialTransferPolicy) === 'waitfornextacceptablevalue' ? 'waitForNextAcceptableValue' : 'synchronizeFirstAcceptableValue',
    gain: optionalText(route.gain), offset: optionalText(route.offset), deadband: optionalText(route.deadband),
    minimumIntervalMilliseconds: optionalText(route.minimumIntervalMilliseconds), periodMilliseconds: optionalText(route.periodMilliseconds ?? 1000),
    enabled: route.enabled !== false
  };
}

function buildRoute(draft: Draft, tags: TagEngineering[], text: ReturnType<typeof labels>): GatewayEngineering {
  const source = tags.find(tag => tag.id === draft.sourceTagId);
  const destination = tags.find(tag => tag.id === draft.destinationTagId);
  if (!draft.key.trim()) throw new Error(text.keyRequired);
  if (!draft.name.trim()) throw new Error(text.nameRequired);
  if (!source) throw new Error(text.sourceRequired);
  if (!destination || destination.readOnly) throw new Error(text.destinationRequired);
  if (source.id === destination.id) throw new Error(text.sameEndpoint);

  return {
    id: draft.id,
    key: draft.key.trim(),
    name: draft.name.trim(),
    sourceTagId: source.id,
    sourceTagPath: source.path,
    destinationTagId: destination.id,
    destinationTagPath: destination.path,
    transferMode: draft.transferMode,
    qualityPolicy: 'goodOnly',
    conversionPolicy: draft.conversionPolicy,
    initialTransferPolicy: draft.initialTransferPolicy,
    gain: draft.conversionPolicy === 'checkedNumeric' ? parseOptionalNumber(draft.gain, text) : null,
    offset: draft.conversionPolicy === 'checkedNumeric' ? parseOptionalNumber(draft.offset, text) : null,
    deadband: draft.transferMode === 'onChange' ? parseOptionalNumber(draft.deadband, text) : null,
    minimumIntervalMilliseconds: draft.transferMode === 'onChange' ? parseOptionalInteger(draft.minimumIntervalMilliseconds, text) : null,
    periodMilliseconds: draft.transferMode === 'periodic' ? parseRequiredInteger(draft.periodMilliseconds, text) : null,
    enabled: draft.enabled
  };
}

function parseOptionalNumber(raw: string, text: ReturnType<typeof labels>): number | null {
  if (!raw.trim()) return null;
  const value = Number(raw);
  if (!Number.isFinite(value)) throw new Error(text.invalidNumber);
  return value;
}

function parseOptionalInteger(raw: string, text: ReturnType<typeof labels>): number | null {
  if (!raw.trim()) return null;
  return parseRequiredInteger(raw, text);
}

function parseRequiredInteger(raw: string, text: ReturnType<typeof labels>): number {
  const value = Number(raw);
  if (!Number.isInteger(value)) throw new Error(text.invalidInteger);
  return value;
}

function routeIdentity(route: GatewayEngineering) { return route.id ? `id:${route.id}` : `key:${route.key.toLowerCase()}`; }
function optionalText(value: number | null | undefined) { return value === null || value === undefined ? '' : String(value); }
function normalizeEnum(value: string | null | undefined) { return (value ?? '').replace(/[^a-z]/gi, '').toLowerCase(); }
function clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T; }

function labels(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'TAG Gateway', description: 'Route server-authoritative TAG values between Data Sources without coupling protocol drivers.', routes: 'routes',
    editorEyebrow: 'Canonical Engineering', editorTitle: 'Route configuration', editorHint: 'Preview and Apply use the public versioned Engineering package.',
    warning: 'Client Memory and built-in simulation are not valid server Gateway endpoints.', route: 'Route', routeHint: 'Edit an existing route or create a new stable route.', newRoute: '+ New route',
    key: 'Key', name: 'Name', enabled: 'Enabled', yes: 'Yes', no: 'No', endpoints: 'Endpoints', endpointsHint: 'Stable TAG IDs are runtime identity; paths remain portable context.', source: 'Source TAG', destination: 'Destination TAG', selectTag: 'Select TAG...', serverOnly: 'Only active server-owned TAGs are eligible.',
    transfer: 'Transfer policy', transferHint: 'OnChange is change-driven; Periodic samples the latest Good value.', mode: 'Mode', startup: 'Startup', synchronize: 'Synchronize first acceptable value', waitNext: 'Wait for next acceptable value', deadband: 'Deadband', minimumInterval: 'Minimum interval (ms)', period: 'Period (ms)',
    conversion: 'Conversion', conversionHint: 'Exact is default. Numeric conversion must be explicit and checked.', policy: 'Policy', preview: 'Preview route', previewing: 'Previewing...', apply: 'Apply to Workspace', applying: 'Applying...', valid: 'Valid Engineering candidate', invalid: 'Invalid Engineering candidate', creates: 'Creates', updates: 'Updates', errors: 'Errors',
    runtimeEyebrow: 'Active Runtime', runtimeTitle: 'Route diagnostics', runtimeHint: 'Gateway failures are isolated from Data Source network diagnostics and source TAG quality.', refresh: 'Refresh diagnostics', loading: 'Loading...', noRuntimeRoutes: 'No active Gateway routes.', noRuntimeHint: 'Publish and activate a revision containing enabled routes to populate runtime diagnostics.', state: 'State', transfers: 'Transfers', skipped: 'Skipped / coalesced', failures: 'Write failures', lastError: 'Last error',
    workspaceChanged: 'The Engineering Workspace changed during Gateway validation. Reload and validate again.', keyRequired: 'Gateway key is required.', nameRequired: 'Gateway name is required.', sourceRequired: 'Select a valid source TAG.', destinationRequired: 'Select a writable destination TAG.', sameEndpoint: 'Source and destination must be different TAGs.', invalidNumber: 'A numeric Gateway setting is invalid.', invalidInteger: 'Interval values must be integers.'
  };
  if (locale === 'es') return {
    title: 'TAG Gateway', description: 'Enruta valores de TAG autoritativos del servidor entre Data Sources sin acoplar drivers de protocolo.', routes: 'rutas',
    editorEyebrow: 'Engineering canónico', editorTitle: 'Configuración de ruta', editorHint: 'Preview y Apply usan el paquete público y versionado de Engineering.',
    warning: 'Client Memory y la simulación integrada no son endpoints válidos del Gateway de servidor.', route: 'Ruta', routeHint: 'Edite una ruta existente o cree una ruta estable.', newRoute: '+ Nueva ruta',
    key: 'Clave', name: 'Nombre', enabled: 'Habilitada', yes: 'Sí', no: 'No', endpoints: 'Endpoints', endpointsHint: 'Los IDs estables de TAG son identidad de runtime; los paths mantienen contexto portable.', source: 'TAG origen', destination: 'TAG destino', selectTag: 'Seleccione TAG...', serverOnly: 'Solo TAGs activos y autoritativos del servidor son elegibles.',
    transfer: 'Política de transferencia', transferHint: 'OnChange responde a cambios; Periodic usa el último valor Good.', mode: 'Modo', startup: 'Inicio', synchronize: 'Sincronizar primer valor aceptable', waitNext: 'Esperar próximo valor aceptable', deadband: 'Deadband', minimumInterval: 'Intervalo mínimo (ms)', period: 'Período (ms)',
    conversion: 'Conversión', conversionHint: 'Exact es el valor por defecto. La conversión numérica debe ser explícita y checked.', policy: 'Política', preview: 'Preview de ruta', previewing: 'Validando...', apply: 'Aplicar al Workspace', applying: 'Aplicando...', valid: 'Candidato Engineering válido', invalid: 'Candidato Engineering inválido', creates: 'Creadas', updates: 'Actualizadas', errors: 'Errores',
    runtimeEyebrow: 'Runtime activo', runtimeTitle: 'Diagnóstico de rutas', runtimeHint: 'Las fallas del Gateway se aíslan de diagnósticos de red y de la calidad del TAG origen.', refresh: 'Actualizar diagnóstico', loading: 'Cargando...', noRuntimeRoutes: 'No hay rutas Gateway activas.', noRuntimeHint: 'Publique y active una revisión con rutas habilitadas para ver diagnóstico runtime.', state: 'Estado', transfers: 'Transferencias', skipped: 'Omitidas / coalescidas', failures: 'Fallas de escritura', lastError: 'Último error',
    workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue y valide nuevamente.', keyRequired: 'La clave del Gateway es obligatoria.', nameRequired: 'El nombre del Gateway es obligatorio.', sourceRequired: 'Seleccione un TAG origen válido.', destinationRequired: 'Seleccione un TAG destino escribible.', sameEndpoint: 'Origen y destino deben ser TAGs diferentes.', invalidNumber: 'Un valor numérico del Gateway no es válido.', invalidInteger: 'Los intervalos deben ser enteros.'
  };
  return {
    title: 'TAG Gateway', description: 'Roteie valores de TAGs autoritativas do servidor entre Data Sources sem acoplar drivers de protocolo.', routes: 'rotas',
    editorEyebrow: 'Engineering canônico', editorTitle: 'Configuração de rota', editorHint: 'Preview e Apply usam o pacote público e versionado de Engineering.',
    warning: 'Client Memory e a simulação interna não são endpoints válidos do Gateway de servidor.', route: 'Rota', routeHint: 'Edite uma rota existente ou crie uma nova rota estável.', newRoute: '+ Nova rota',
    key: 'Chave', name: 'Nome', enabled: 'Habilitada', yes: 'Sim', no: 'Não', endpoints: 'Endpoints', endpointsHint: 'IDs estáveis de TAG são a identidade runtime; paths mantêm contexto portável.', source: 'TAG origem', destination: 'TAG destino', selectTag: 'Selecione a TAG...', serverOnly: 'Somente TAGs ativas e autoritativas do servidor são elegíveis.',
    transfer: 'Política de transferência', transferHint: 'OnChange reage a mudança real; Periodic usa o valor Good mais recente.', mode: 'Modo', startup: 'Inicialização', synchronize: 'Sincronizar primeiro valor aceitável', waitNext: 'Aguardar próximo valor aceitável', deadband: 'Deadband', minimumInterval: 'Intervalo mínimo (ms)', period: 'Período (ms)',
    conversion: 'Conversão', conversionHint: 'Exact é o padrão. Conversão numérica precisa ser explícita e checked.', policy: 'Política', preview: 'Preview da rota', previewing: 'Validando...', apply: 'Aplicar ao Workspace', applying: 'Aplicando...', valid: 'Candidato Engineering válido', invalid: 'Candidato Engineering inválido', creates: 'Criações', updates: 'Atualizações', errors: 'Erros',
    runtimeEyebrow: 'Runtime ativo', runtimeTitle: 'Diagnóstico das rotas', runtimeHint: 'Falhas do Gateway ficam separadas dos diagnósticos de rede e da qualidade da TAG origem.', refresh: 'Atualizar diagnóstico', loading: 'Carregando...', noRuntimeRoutes: 'Nenhuma rota Gateway ativa.', noRuntimeHint: 'Publique e ative uma revisão com rotas habilitadas para preencher o diagnóstico runtime.', state: 'Estado', transfers: 'Transferências', skipped: 'Ignoradas / coalescidas', failures: 'Falhas de escrita', lastError: 'Último erro',
    workspaceChanged: 'O Engineering Workspace mudou durante a validação do Gateway. Recarregue e valide novamente.', keyRequired: 'A chave da rota Gateway é obrigatória.', nameRequired: 'O nome da rota Gateway é obrigatório.', sourceRequired: 'Selecione uma TAG origem válida.', destinationRequired: 'Selecione uma TAG destino gravável.', sameEndpoint: 'Origem e destino devem ser TAGs diferentes.', invalidNumber: 'Um valor numérico do Gateway é inválido.', invalidInteger: 'Intervalos devem ser números inteiros.'
  };
}
