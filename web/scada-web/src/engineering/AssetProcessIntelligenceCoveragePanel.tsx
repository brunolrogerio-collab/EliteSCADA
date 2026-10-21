import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import type { EngineeringLocale } from './i18n';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const MAX_WINDOW_MS = 31 * 24 * 60 * 60 * 1000;
const DEFAULT_BUCKET_SECONDS = 900;
type CoverageBlockedReason = 'capabilities-loading' | 'authority-unavailable' | 'license-required' | 'trend-use-required' |
  'assignment-ambiguous' | 'scope-unavailable' | 'scope-inactive' | 'permit-required' | 'signals-required' | 'duplicate-tags' |
  'configuration-unsaved' | 'scope-selection-unsaved' | 'workspace-dirty' | 'project-unavailable';

type WindowValue = { fromUtc: string; toUtc: string };
type SignalSummary = {
  rawObservationCount: number;
  acceptedSampleCount: number;
  rejectedSampleCount: number;
  qualityCounts: Record<string, number>;
  validSampleCount: number | null;
  minimum: number | null;
  maximum: number | null;
  sampleMean: number | null;
  completeness: string;
  expectedSampleCount: number | null;
};
type ProfileCoverageResponse = {
  contractVersion: number;
  ruleCatalogVersion: number;
  ruleVersion: string;
  projectKey: string;
  activeRevision: number;
  scopeKind: string;
  scopeId: string;
  profileId: string;
  profileKey: string;
  profileName: string;
  window: WindowValue;
  signals: Array<{
    signalId: string;
    signalKey: string;
    signalRoleMetadata: string;
    required: boolean;
    tagId: string;
    tagPath: string;
    tagDataType: string;
    unit: string | null;
    status: string;
    summary: SignalSummary | null;
  }>;
  temporalAlignment: {
    requiredSignalCount: number;
    requiredSignalsWithGoodSamples: number;
    exactHistorianTimestampIntersectionCount: number;
    status: string;
    timestampBasis: string;
    sourceAcquisitionAlignment: string;
    expectedCadence: string;
    completeness: string;
  };
  provenance: {
    dataset: string;
    datasetContractVersion: number;
    ruleVersion: string;
    signalRoleInterpretation: string;
    unitConversion: string;
    processKpiDisposition: string;
  };
};

export function AssetProcessIntelligenceCoveragePanel({
  locale,
  scopeId,
  profileId,
  projectKey,
  activeRevision,
  canQuery,
  blockedReason
}: {
  locale: EngineeringLocale;
  scopeId: string;
  profileId: string;
  projectKey: string | null;
  activeRevision: number | null;
  canQuery: boolean;
  blockedReason: CoverageBlockedReason | null;
}) {
  const copy = useMemo(() => coverageCopy(locale), [locale]);
  const [expanded, setExpanded] = useState(false);
  const [fromLocal, setFromLocal] = useState(() => localDateTimeInput(new Date(Date.now() - 24 * 60 * 60 * 1000)));
  const [toLocal, setToLocal] = useState(() => localDateTimeInput(new Date()));
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ProfileCoverageResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  const requestVersion = useRef(0);

  useEffect(() => {
    controller.current?.abort();
    controller.current = null;
    requestVersion.current += 1;
    setLoading(false);
    setResult(null);
    setError(null);
    return () => controller.current?.abort();
  }, [scopeId, profileId, projectKey, activeRevision, canQuery, blockedReason, fromLocal, toLocal, expanded]);

  const windowResult = useMemo(() => parseWindow(fromLocal, toLocal), [fromLocal, toLocal]);

  const query = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canQuery || !projectKey || activeRevision === null || !windowResult.window) return;

    controller.current?.abort();
    const currentController = new AbortController();
    controller.current = currentController;
    const version = ++requestVersion.current;
    setLoading(true);
    setResult(null);
    setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/profile-coverage`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({
          scopeKind: 0,
          scopeId,
          profileId,
          window: windowResult.window,
          bucketSeconds: DEFAULT_BUCKET_SECONDS
        }),
        signal: currentController.signal
      });
      const payload = await response.json().catch(() => null) as ProfileCoverageResponse | { code?: unknown } | null;
      if (!response.ok) {
        const code = payload && 'code' in payload && typeof payload.code === 'string'
          ? payload.code
          : `HTTP_${response.status}`;
        throw new CoverageQueryError(code);
      }
      if (!isMatchingResponse(payload, scopeId, profileId, projectKey, activeRevision, windowResult.window)) {
        throw new CoverageQueryError('INVALID_RESPONSE');
      }
      if (requestVersion.current === version) setResult(payload);
    } catch (reason) {
      if (currentController.signal.aborted) return;
      if (requestVersion.current === version) {
        setError(reason instanceof CoverageQueryError
          ? coverageErrorText(reason.code, copy)
          : copy.networkError);
      }
    } finally {
      if (requestVersion.current === version) setLoading(false);
    }
  };

  return <section className="asset-process-intelligence__coverage" data-testid={`intelligence-profile-coverage-${profileId}`}>
    <button type="button" className="secondary" aria-expanded={expanded} onClick={() => setExpanded(value => !value)}>
      {expanded ? copy.hide : copy.review}
    </button>
    {expanded ? <div className="asset-process-intelligence__coverage-body">
      <p>{copy.description}</p>
      {!canQuery ? <p className="asset-process-intelligence__coverage-note" role="note">{blockedReason ? blockedReasonText(blockedReason, copy) : copy.unavailable}</p> : null}
      <form className="asset-process-intelligence__coverage-form" onSubmit={event => void query(event)}>
        <label>{copy.from}<input type="datetime-local" value={fromLocal} disabled={!canQuery || loading} onChange={event => setFromLocal(event.currentTarget.value)}/></label>
        <label>{copy.to}<input type="datetime-local" value={toLocal} disabled={!canQuery || loading} onChange={event => setToLocal(event.currentTarget.value)}/></label>
        {!windowResult.window ? <p className="asset-process-intelligence__coverage-error" role="alert">{windowResult.error === 'missing' ? copy.windowRequired : copy.windowInvalid}</p> : null}
        <p className="asset-process-intelligence__coverage-note">{copy.utcHint}</p>
        <button type="submit" disabled={!canQuery || !windowResult.window || loading}>
          {loading ? copy.loading : copy.analyze}
        </button>
      </form>
      {error ? <p className="asset-process-intelligence__coverage-error" role="alert">{error}</p> : null}
      {result ? <CoverageResult result={result} locale={locale} copy={copy}/> : null}
    </div> : null}
  </section>;
}

function CoverageResult({ result, locale, copy }: {
  result: ProfileCoverageResponse;
  locale: EngineeringLocale;
  copy: CoverageCopy;
}) {
  const localeCode = locale === 'en' ? 'en-US' : locale === 'es' ? 'es-ES' : 'pt-BR';
  const number = (value: number | null | undefined) => value === null || value === undefined || !Number.isFinite(value)
    ? copy.unknown
    : new Intl.NumberFormat(localeCode, { maximumFractionDigits: 6 }).format(value);
  const alignmentText = describeAlignment(result.temporalAlignment.status, copy);

  return <div className="asset-process-intelligence__coverage-result" data-testid="intelligence-profile-coverage-result">
    <p role="status"><strong>{copy.queryWindow}</strong> {formatUtc(result.window.fromUtc, localeCode)} – {formatUtc(result.window.toUtc, localeCode)}</p>
    <section>
      <h4>{copy.temporalAlignment}</h4>
      <p><strong>{alignmentText}</strong></p>
      <dl className="asset-process-intelligence__coverage-facts">
        <Fact label={copy.requiredWithData} value={`${number(result.temporalAlignment.requiredSignalsWithGoodSamples)} / ${number(result.temporalAlignment.requiredSignalCount)}`}/>
        <Fact label={copy.exactTimestampIntersection} value={number(result.temporalAlignment.exactHistorianTimestampIntersectionCount)}/>
        <Fact label={copy.timestampBasis} value={result.temporalAlignment.timestampBasis}/>
        <Fact label={copy.sourceAcquisitionAlignment} value={result.temporalAlignment.sourceAcquisitionAlignment}/>
        <Fact label={copy.expectedCadence} value={result.temporalAlignment.expectedCadence}/>
        <Fact label={copy.completeness} value={result.temporalAlignment.completeness}/>
      </dl>
    </section>
    <section>
      <h4>{copy.signals}</h4>
      <div className="asset-process-intelligence__coverage-signals">
        {result.signals.map(signal => <article key={signal.signalId}>
          <h5>{signal.signalKey} {signal.required ? `· ${copy.required}` : `· ${copy.optional}`}</h5>
          <p>{signal.tagPath} · {signal.tagDataType}{signal.unit ? ` · ${signal.unit}` : ''}</p>
          <p><strong>{copy.signalStatus}:</strong> {describeSignalStatus(signal.status, copy)}</p>
          <p><strong>{copy.roleMetadata}:</strong> {signal.signalRoleMetadata || copy.unknown}</p>
          {signal.summary ? <>
            <dl className="asset-process-intelligence__coverage-facts">
              <Fact label={copy.rawObservations} value={number(signal.summary.rawObservationCount)}/>
              <Fact label={copy.acceptedSamples} value={number(signal.summary.acceptedSampleCount)}/>
              <Fact label={copy.rejectedSamples} value={number(signal.summary.rejectedSampleCount)}/>
              <Fact label={copy.minimum} value={number(signal.summary.minimum)}/>
              <Fact label={copy.maximum} value={number(signal.summary.maximum)}/>
              <Fact label={copy.sampleMean} value={number(signal.summary.sampleMean)}/>
              <Fact label={copy.completeness} value={signal.summary.completeness}/>
              <Fact label={copy.expectedSamples} value={number(signal.summary.expectedSampleCount)}/>
            </dl>
            <p><strong>{copy.qualityCounts}:</strong> {Object.entries(signal.summary.qualityCounts ?? {}).sort(([left], [right]) => left.localeCompare(right))
              .map(([quality, count]) => `${quality}: ${number(count)}`).join(' · ') || copy.none}</p>
          </> : null}
        </article>)}
      </div>
    </section>
    <section>
      <h4>{copy.provenance}</h4>
      <dl className="asset-process-intelligence__coverage-facts">
        <Fact label={copy.dataset} value={result.provenance.dataset}/>
        <Fact label={copy.contractVersion} value={number(result.provenance.datasetContractVersion)}/>
        <Fact label={copy.ruleVersion} value={result.provenance.ruleVersion}/>
        <Fact label={copy.roleInterpretation} value={result.provenance.signalRoleInterpretation}/>
        <Fact label={copy.unitConversion} value={result.provenance.unitConversion}/>
        <Fact label={copy.processKpiDisposition} value={result.provenance.processKpiDisposition}/>
      </dl>
      <p className="asset-process-intelligence__coverage-note">{copy.nonClaim}</p>
    </section>
  </div>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>;
}

type CoverageCopy = ReturnType<typeof coverageCopy>;

function coverageCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    blockedCapabilitiesLoading: 'Checking authorization…', blockedAuthorityUnavailable: 'Authorization could not be verified; analysis is disabled.',
    blockedLicenseRequired: 'The Asset & Process Intelligence license is required.', blockedTrendUseRequired: 'Runtime TrendUse is required.',
    blockedAssignmentAmbiguous: 'The Equipment/profile assignment is missing or ambiguous.', blockedScopeUnavailable: 'A unique Equipment scope is not configured.',
    blockedScopeInactive: 'Activate this scope before requesting historical analysis.', blockedPermitRequired: 'The current execution permit is not armed.',
    blockedSignalsRequired: 'The profile has no signals to analyze.', blockedDuplicateTags: 'The profile contains duplicate TAG references; the query is disabled.',
    blockedConfigurationUnsaved: 'Save or discard the local profile changes before querying.', blockedScopeSelectionUnsaved: 'Save or discard scope-selection changes before querying.',
    blockedWorkspaceDirty: 'Publish or discard Engineering Working changes before querying the active project revision.', blockedProjectUnavailable: 'An active project revision is required.',
    review: 'Review historical coverage', hide: 'Hide coverage', description: 'Run the existing read-only profile coverage analysis for an explicit time window. Results are transient and are not added to the screen draft.',
    from: 'From (local time)', to: 'To (local time)', utcHint: 'The selected local times are sent to the server as UTC. Maximum window: 31 days. No query runs until Analyze is selected.',
    analyze: 'Analyze coverage', loading: 'Analyzing…', unavailable: 'Analysis is currently unavailable.',
    windowRequired: 'Choose both ends of the time window.', windowInvalid: 'The end must be after the start and the window cannot exceed 31 days.',
    queryFailed: 'Coverage query failed', networkError: 'Coverage could not be retrieved. Check connectivity and authorization, then retry explicitly.',
    unknown: 'unknown', none: 'none reported', queryWindow: 'Requested window:', temporalAlignment: 'Temporal alignment',
    requiredWithData: 'Required signals with Good numeric samples', exactTimestampIntersection: 'Exact Historian timestamp intersection',
    timestampBasis: 'Timestamp basis', sourceAcquisitionAlignment: 'Source acquisition alignment', expectedCadence: 'Expected cadence',
    completeness: 'Completeness', signals: 'Signals', required: 'required', optional: 'optional', signalStatus: 'Status', roleMetadata: 'Role metadata',
    rawObservations: 'Raw observations', acceptedSamples: 'Accepted Good numeric samples', rejectedSamples: 'Rejected observations',
    minimum: 'Minimum', maximum: 'Maximum', sampleMean: 'Arithmetic sample mean (not time-weighted)', expectedSamples: 'Expected sample count',
    qualityCounts: 'Quality counts', provenance: 'Provenance and limits', dataset: 'Dataset', contractVersion: 'Dataset contract version',
    ruleVersion: 'Rule version', roleInterpretation: 'Signal role interpretation', unitConversion: 'Unit conversion', processKpiDisposition: 'Process KPI disposition',
    nonClaim: 'Exact Historian event-time equality does not prove source-acquisition synchronization. Unknown completeness/cadence stays unknown; signal role metadata is not interpreted and no process KPI is inferred.',
    unavailableResponse: 'The response did not match this profile and time window.', networkUnavailable: 'The analysis is unavailable.',
    observed: 'Exact Historian event-time overlap observed', noOverlap: 'No exact Historian event-time overlap observed',
    noRequired: 'No required signals configured', missingRequired: 'Required signal data is insufficient', singleRequired: 'Only one required signal; no alignment comparison',
    descriptive: 'Descriptive trend available', noGood: 'No Good numeric samples', nonNumeric: 'Unsupported non-numeric TAG type'
  };
  if (locale === 'es') return {
    blockedCapabilitiesLoading: 'Verificando autorización…', blockedAuthorityUnavailable: 'No se pudo verificar la autorización; el análisis está deshabilitado.',
    blockedLicenseRequired: 'Se requiere la licencia de Inteligencia de Activos y Procesos.', blockedTrendUseRequired: 'Se requiere TrendUse de Runtime.',
    blockedAssignmentAmbiguous: 'La asignación equipo/perfil falta o es ambigua.', blockedScopeUnavailable: 'No hay un alcance único de equipo configurado.',
    blockedScopeInactive: 'Active este alcance antes de solicitar análisis histórico.', blockedPermitRequired: 'El permiso de ejecución actual no está habilitado.',
    blockedSignalsRequired: 'El perfil no tiene señales para analizar.', blockedDuplicateTags: 'El perfil contiene referencias TAG duplicadas; la consulta está deshabilitada.',
    blockedConfigurationUnsaved: 'Guarde o descarte los cambios locales del perfil antes de consultar.', blockedScopeSelectionUnsaved: 'Guarde o descarte los cambios de alcance antes de consultar.',
    blockedWorkspaceDirty: 'Publique o descarte los cambios de Engineering Working antes de consultar la revisión activa.', blockedProjectUnavailable: 'Se requiere una revisión de proyecto activa.',
    review: 'Revisar cobertura histórica', hide: 'Ocultar cobertura', description: 'Ejecute el análisis read-only existente para un intervalo explícito. El resultado es transitorio y no se agrega al borrador de pantalla.',
    from: 'Desde (hora local)', to: 'Hasta (hora local)', utcHint: 'Las horas locales se envían al servidor en UTC. Intervalo máximo: 31 días. No se consulta hasta pulsar Analizar.',
    analyze: 'Analizar cobertura', loading: 'Analizando…', unavailable: 'El análisis no está disponible ahora.',
    windowRequired: 'Seleccione ambos extremos del intervalo.', windowInvalid: 'El final debe ser posterior al inicio y el intervalo no puede superar 31 días.',
    queryFailed: 'Falló la consulta de cobertura', networkError: 'No se pudo obtener la cobertura. Compruebe la conexión y autorización y vuelva a intentarlo explícitamente.',
    unknown: 'desconocido', none: 'sin datos', queryWindow: 'Intervalo consultado:', temporalAlignment: 'Alineación temporal',
    requiredWithData: 'Señales requeridas con muestras numéricas Good', exactTimestampIntersection: 'Intersección exacta de marcas Historian',
    timestampBasis: 'Base temporal', sourceAcquisitionAlignment: 'Alineación de adquisición de origen', expectedCadence: 'Cadencia esperada',
    completeness: 'Completitud', signals: 'Señales', required: 'requerida', optional: 'opcional', signalStatus: 'Estado', roleMetadata: 'Metadatos de rol',
    rawObservations: 'Observaciones brutas', acceptedSamples: 'Muestras numéricas Good aceptadas', rejectedSamples: 'Observaciones rechazadas',
    minimum: 'Mínimo', maximum: 'Máximo', sampleMean: 'Media aritmética de muestras (no ponderada por tiempo)', expectedSamples: 'Muestras esperadas',
    qualityCounts: 'Conteo por calidad', provenance: 'Procedencia y límites', dataset: 'Dataset', contractVersion: 'Versión del contrato del dataset',
    ruleVersion: 'Versión de regla', roleInterpretation: 'Interpretación del rol', unitConversion: 'Conversión de unidades', processKpiDisposition: 'Estado KPI de proceso',
    nonClaim: 'La igualdad exacta de tiempo del Historian no demuestra sincronización de adquisición. La completitud/cadencia desconocida sigue desconocida; no se interpreta el rol ni se infiere un KPI de proceso.',
    unavailableResponse: 'La respuesta no coincide con este perfil e intervalo.', networkUnavailable: 'El análisis no está disponible.',
    observed: 'Se observó solapamiento temporal exacto de Historian', noOverlap: 'No se observó solapamiento temporal exacto de Historian',
    noRequired: 'No hay señales requeridas configuradas', missingRequired: 'Datos insuficientes para señales requeridas', singleRequired: 'Una sola señal requerida; sin comparación de alineación',
    descriptive: 'Tendencia descriptiva disponible', noGood: 'Sin muestras numéricas Good', nonNumeric: 'Tipo de TAG no numérico no admitido'
  };
  return {
    blockedCapabilitiesLoading: 'Verificando autorização…', blockedAuthorityUnavailable: 'Não foi possível confirmar a autorização; a análise está desabilitada.',
    blockedLicenseRequired: 'É necessária a licença de Inteligência de Ativos e Processos.', blockedTrendUseRequired: 'É necessário TrendUse no Runtime.',
    blockedAssignmentAmbiguous: 'A associação equipamento/perfil está ausente ou ambígua.', blockedScopeUnavailable: 'Não há um escopo único de equipamento configurado.',
    blockedScopeInactive: 'Ative este escopo antes de solicitar análise histórica.', blockedPermitRequired: 'A permissão de execução atual não está armada.',
    blockedSignalsRequired: 'O perfil não possui sinais para analisar.', blockedDuplicateTags: 'O perfil tem referências TAG duplicadas; a consulta está desabilitada.',
    blockedConfigurationUnsaved: 'Salve ou descarte as alterações locais do perfil antes de consultar.', blockedScopeSelectionUnsaved: 'Salve ou descarte alterações na seleção de escopos antes de consultar.',
    blockedWorkspaceDirty: 'Publique ou descarte alterações de Engineering Working antes de consultar a revisão ativa.', blockedProjectUnavailable: 'É necessária uma revisão ativa do projeto.',
    review: 'Revisar cobertura histórica', hide: 'Ocultar cobertura', description: 'Execute a análise de cobertura somente para leitura do perfil em uma janela explícita. O resultado é temporário e não é adicionado ao rascunho da tela.',
    from: 'De (horário local)', to: 'Até (horário local)', utcHint: 'Os horários locais selecionados são enviados ao servidor em UTC. Janela máxima: 31 dias. Nenhuma consulta ocorre até clicar em Analisar.',
    analyze: 'Analisar cobertura', loading: 'Analisando…', unavailable: 'A análise não está disponível neste momento.',
    windowRequired: 'Escolha os dois extremos da janela.', windowInvalid: 'O fim deve ser posterior ao início e a janela não pode exceder 31 dias.',
    queryFailed: 'Falha na consulta de cobertura', networkError: 'Não foi possível obter a cobertura. Confira a conexão e autorização e tente novamente explicitamente.',
    unknown: 'desconhecido', none: 'nenhum informado', queryWindow: 'Janela consultada:', temporalAlignment: 'Alinhamento temporal',
    requiredWithData: 'Sinais obrigatórios com amostras numéricas Good', exactTimestampIntersection: 'Interseção exata de timestamps do Historian',
    timestampBasis: 'Base do timestamp', sourceAcquisitionAlignment: 'Alinhamento da aquisição de origem', expectedCadence: 'Cadência esperada',
    completeness: 'Completude', signals: 'Sinais', required: 'obrigatório', optional: 'opcional', signalStatus: 'Estado', roleMetadata: 'Metadados do papel',
    rawObservations: 'Observações brutas', acceptedSamples: 'Amostras numéricas Good aceitas', rejectedSamples: 'Observações rejeitadas',
    minimum: 'Mínimo', maximum: 'Máximo', sampleMean: 'Média aritmética das amostras (não ponderada pelo tempo)', expectedSamples: 'Quantidade esperada de amostras',
    qualityCounts: 'Contagens por qualidade', provenance: 'Proveniência e limites', dataset: 'Dataset', contractVersion: 'Versão do contrato do dataset',
    ruleVersion: 'Versão da regra', roleInterpretation: 'Interpretação do papel do sinal', unitConversion: 'Conversão de unidade', processKpiDisposition: 'Situação do KPI de processo',
    nonClaim: 'Igualdade exata de tempo do Historian não comprova sincronização da aquisição de origem. Completude/cadência desconhecida permanece desconhecida; o papel do sinal não é interpretado e nenhum KPI de processo é inferido.',
    unavailableResponse: 'A resposta não corresponde a este perfil e janela.', networkUnavailable: 'A análise não está disponível.',
    observed: 'Sobreposição de timestamps exatos do Historian observada', noOverlap: 'Nenhuma sobreposição de timestamps exatos do Historian observada',
    noRequired: 'Nenhum sinal obrigatório configurado', missingRequired: 'Dados de sinais obrigatórios insuficientes', singleRequired: 'Um único sinal obrigatório; sem comparação de alinhamento',
    descriptive: 'Tendência descritiva disponível', noGood: 'Sem amostras numéricas Good', nonNumeric: 'Tipo de TAG não numérico não suportado'
  };
}

function blockedReasonText(reason: CoverageBlockedReason, copy: CoverageCopy) {
  switch (reason) {
    case 'capabilities-loading': return copy.blockedCapabilitiesLoading;
    case 'authority-unavailable': return copy.blockedAuthorityUnavailable;
    case 'license-required': return copy.blockedLicenseRequired;
    case 'trend-use-required': return copy.blockedTrendUseRequired;
    case 'assignment-ambiguous': return copy.blockedAssignmentAmbiguous;
    case 'scope-unavailable': return copy.blockedScopeUnavailable;
    case 'scope-inactive': return copy.blockedScopeInactive;
    case 'permit-required': return copy.blockedPermitRequired;
    case 'signals-required': return copy.blockedSignalsRequired;
    case 'duplicate-tags': return copy.blockedDuplicateTags;
    case 'configuration-unsaved': return copy.blockedConfigurationUnsaved;
    case 'scope-selection-unsaved': return copy.blockedScopeSelectionUnsaved;
    case 'workspace-dirty': return copy.blockedWorkspaceDirty;
    case 'project-unavailable': return copy.blockedProjectUnavailable;
  }
}

function parseWindow(fromLocal: string, toLocal: string): { window: WindowValue | null; error: 'missing' | 'invalid' | null } {
  if (!fromLocal || !toLocal) return { window: null, error: 'missing' };
  const from = new Date(fromLocal);
  const to = new Date(toLocal);
  const fromMs = from.getTime();
  const toMs = to.getTime();
  if (!Number.isFinite(fromMs) || !Number.isFinite(toMs) || toMs <= fromMs || toMs - fromMs > MAX_WINDOW_MS) {
    return { window: null, error: 'invalid' };
  }
  return { window: { fromUtc: from.toISOString(), toUtc: to.toISOString() }, error: null };
}

function localDateTimeInput(value: Date) {
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

function isMatchingResponse(
  value: unknown,
  scopeId: string,
  profileId: string,
  projectKey: string,
  activeRevision: number,
  window: WindowValue
): value is ProfileCoverageResponse {
  if (!value || typeof value !== 'object') return false;
  const response = value as Partial<ProfileCoverageResponse>;
  return response.projectKey === projectKey && response.activeRevision === activeRevision &&
    response.scopeKind === 'Equipment' &&
    typeof response.scopeId === 'string' && response.scopeId.toLowerCase() === scopeId.toLowerCase() &&
    response.profileId?.toLowerCase() === profileId.toLowerCase() &&
    Date.parse(response.window?.fromUtc ?? '') === Date.parse(window.fromUtc) &&
    Date.parse(response.window?.toUtc ?? '') === Date.parse(window.toUtc) &&
    Array.isArray(response.signals) && Boolean(response.temporalAlignment && response.provenance);
}

function formatUtc(value: string, locale: string) {
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(date) : value;
}

function describeAlignment(status: string, copy: CoverageCopy) {
  switch (status) {
    case 'exact_historian_timestamp_overlap_observed': return copy.observed;
    case 'insufficient_exact_historian_timestamp_overlap': return copy.noOverlap;
    case 'insufficient_no_required_signals': return copy.noRequired;
    case 'insufficient_required_signal_data': return copy.missingRequired;
    case 'single_required_signal_no_alignment_comparison': return copy.singleRequired;
    default: return status;
  }
}

function describeSignalStatus(status: string, copy: CoverageCopy) {
  switch (status) {
    case 'descriptive_trend_available': return copy.descriptive;
    case 'insufficient_no_good_numeric_samples': return copy.noGood;
    case 'unsupported_non_numeric_tag_type': return copy.nonNumeric;
    default: return status;
  }
}

function coverageErrorText(code: string, copy: CoverageCopy) {
  switch (code) {
    case 'INTELLIGENCE_ENTITLEMENT_REQUIRED': return `${copy.queryFailed}: ${code}`;
    case 'INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE': return `${copy.queryFailed}: ${code}`;
    case 'INTELLIGENCE_SCOPE_NOT_ACTIVE_OR_AMBIGUOUS': return `${copy.queryFailed}: ${code}`;
    case 'INTELLIGENCE_EXECUTION_PERMIT_REQUIRED': return `${copy.queryFailed}: ${code}`;
    case 'INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE': return `${copy.queryFailed}: ${code}`;
    case 'INTELLIGENCE_WORKSPACE_CHANGED': return `${copy.queryFailed}: ${code}`;
    case 'HISTORICAL_QUERY_FORBIDDEN': return `${copy.queryFailed}: ${code}`;
    case 'INVALID_RESPONSE': return copy.unavailableResponse;
    default: return `${copy.queryFailed}: ${code}`;
  }
}

class CoverageQueryError extends Error {
  constructor(readonly code: string) { super(code); }
}
