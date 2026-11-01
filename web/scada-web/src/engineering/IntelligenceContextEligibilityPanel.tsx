import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { EngineeringLocale } from './i18n';
import { intelligenceEvidenceText } from './intelligenceEvidenceText';
import { IntelligenceComparisonPeriod, matchesIntelligenceWindow, useIntelligenceComparisonPeriod } from './IntelligenceComparisonPeriod';
import type { AssetProcessIntelligenceOperatingContextProfileEngineering } from './types';
import { coverageCopy, coverageErrorText, localDateTimeInput, parseWindow, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type SignalWindow = { rawObservationCount: number; acceptedSynchronizedUniqueCount: number; contextEligibleObservationCount: number; status: string; rejectionCounts: Record<string, number> };
type ContextResult = {
  contractVersion: number; projectKey: string; activeRevision: number; assignmentId: string; measurementProfileId: string;
  operatingContextProfileId: string; profileVersion: number; referenceWindow: WindowValue;
  comparisonWindow?: WindowValue | null;
  signals: Array<{ signalId: string; tagPath: string; engineeringUnit: string | null; reference: SignalWindow; comparison?: SignalWindow | null }>;
};
export function IntelligenceContextEligibilityPanel({ locale, scopeId, assignmentId, profileId, contexts, projectKey, activeRevision, canQuery }: {
  locale: EngineeringLocale; scopeId: string; assignmentId: string; profileId: string;
  contexts: readonly AssetProcessIntelligenceOperatingContextProfileEngineering[];
  projectKey: string | null; activeRevision: number | null; canQuery: boolean;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const [contextId, setContextId] = useState('');
  const context = contexts.find(item => item.id === contextId && item.assignmentId === assignmentId);
  const [from, setFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 86400000)));
  const [to, setTo] = useState(() => localDateTimeInput(new Date()));
  const window = parseWindow(from, to).window;
  const comparison = useIntelligenceComparisonPeriod(window);
  const [result, setResult] = useState<ContextResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const sequence = useRef(0);
  useEffect(() => {
    controller.current?.abort(); sequence.current++;
    setResult(null); setError(null); setBusy(false);
    return () => controller.current?.abort();
  }, [scopeId, assignmentId, profileId, context?.id, context?.version, projectKey, activeRevision, canQuery, from, to, comparison.key]);
  async function query(event: FormEvent) {
    event.preventDefault();
    if (!canQuery || !context || !projectKey || activeRevision === null || !window || !comparison.valid) return;
    controller.current?.abort();
    const current = new AbortController(); controller.current = current;
    const request = ++sequence.current;
    setBusy(true); setResult(null); setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/context-eligibility/v1/query`, {
        method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopeKind: 0, scopeId, assignmentId, operatingContextProfileId: context.id,
          expectedProfileVersion: context.version, referenceWindow: window, comparisonWindow: comparison.window }), signal: current.signal
      });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(typeof payload?.code === 'string' ? payload.code : `HTTP_${response.status}`);
      if (payload?.contractVersion !== 1 || payload.projectKey !== projectKey || payload.activeRevision !== activeRevision ||
          payload.assignmentId !== assignmentId || payload.measurementProfileId !== profileId ||
          payload.operatingContextProfileId !== context.id || payload.profileVersion !== context.version ||
          Date.parse(payload.referenceWindow?.fromUtc) !== Date.parse(window.fromUtc) ||
          Date.parse(payload.referenceWindow?.toUtc) !== Date.parse(window.toUtc) ||
          !matchesIntelligenceWindow(payload.comparisonWindow, comparison.window) ||
          !Array.isArray(payload.signals) || payload.signals.length > 16 ||
          !payload.signals.every((signal: ContextResult['signals'][number]) => typeof signal?.tagPath === 'string' &&
            typeof signal.signalId === 'string' && (signal.engineeringUnit == null || typeof signal.engineeringUnit === 'string') && signal.reference &&
            (comparison.window ? !!signal.comparison : signal.comparison == null) &&
            [signal.reference, ...(comparison.window ? [signal.comparison] : [])].every(evidence => !!evidence &&
              [evidence.rawObservationCount, evidence.acceptedSynchronizedUniqueCount, evidence.contextEligibleObservationCount]
                .every(count => Number.isInteger(count) && count >= 0)))) throw new Error('INVALID_RESPONSE');
      if (!current.signal.aborted && sequence.current === request) setResult(payload);
    } catch (cause) {
      if (!current.signal.aborted && sequence.current === request) {
        const code = cause instanceof Error ? cause.message : 'NETWORK_ERROR';
        setError(code.includes('OPERATING_CONTEXT') || code.includes('CONTEXT_ELIGIBILITY')
          ? text('O contexto ou suas medições mudaram. Atualize a página e confira as condições antes de consultar novamente.',
            'The context or its measurements changed. Refresh and review the conditions before querying again.',
            'El contexto o sus mediciones cambiaron. Actualice la página y revise las condiciones antes de consultar de nuevo.')
          : coverageErrorText(code, coverageCopy(locale)));
      }
    } finally { if (sequence.current === request) setBusy(false); }
  }
  const options = contexts.filter(item => item.assignmentId === assignmentId);
  return <details className="asset-process-intelligence__coverage" data-testid={`intelligence-context-eligibility-${assignmentId}`}>
    <summary>{text('Conferir condições do contexto', 'Check context conditions', 'Revisar condiciones del contexto')}</summary>
    <p>{text('Confira quantas leituras atendem a todas as condições do contexto salvo. Isso ajuda a escolher dados comparáveis pelas condições declaradas; não comprova equivalência física entre regimes.',
      'Check how many readings meet all conditions in the saved context. This helps select data matching the declared conditions; it does not prove physical equivalence between operating regimes.',
      'Revise cuántas lecturas cumplen todas las condiciones del contexto guardado. Esto ayuda a seleccionar datos compatibles con las condiciones declaradas; no demuestra equivalencia física entre regímenes.')}</p>
    {options.length === 0 && <p>{text('Crie e publique um contexto operacional para este equipamento antes de consultar.', 'Create and publish an operating context for this equipment before querying.', 'Cree y publique un contexto operativo para este equipo antes de consultar.')}</p>}
    {!canQuery && <p role="note">{text('A consulta depende de alterações publicadas, equipamento ativo e permissões de leitura.', 'Querying requires published changes, active equipment and read permissions.', 'La consulta requiere cambios publicados, equipo activo y permisos de lectura.')}</p>}
    <form className="asset-process-intelligence__coverage-form" onSubmit={event => void query(event)}>
      <label>{text('Contexto salvo', 'Saved context', 'Contexto guardado')}<select value={contextId} onChange={event => setContextId(event.currentTarget.value)}>
        <option value="">{text('Escolha o contexto', 'Choose a context', 'Elija un contexto')}</option>
        {options.map(item => <option key={item.id} value={item.id}>{item.displayLabel}</option>)}
      </select></label>
      <label>{text('Início do período (horário local)', 'Period start (local time)', 'Inicio del período (hora local)')}
        <input type="datetime-local" value={from} onChange={event => setFrom(event.currentTarget.value)}/></label>
      <label>{text('Fim do período (horário local)', 'Period end (local time)', 'Fin del período (hora local)')}
        <input type="datetime-local" value={to} onChange={event => setTo(event.currentTarget.value)}/></label>
      <p>{text('Até 31 dias. Os horários locais são convertidos para UTC.', 'Up to 31 days. Local times are converted to UTC.', 'Hasta 31 días. Las horas locales se convierten a UTC.')}</p>
      {!window && <p role="alert">{text('Confira o início e o fim do período.', 'Check the start and end of the period.', 'Revise el inicio y el fin del período.')}</p>}
      <IntelligenceComparisonPeriod locale={locale} period={comparison}/>
      <button type="submit" disabled={!canQuery || !context || !window || !comparison.valid || busy}>{text('Consultar condições', 'Query conditions', 'Consultar condiciones')}</button>
      {busy && <button type="button" onClick={() => { controller.current?.abort(); sequence.current++; setBusy(false); }}>{text('Cancelar consulta', 'Cancel query', 'Cancelar consulta')}</button>}
    </form>
    {error && <p role="alert">{error}</p>}
    {result && <div data-testid="intelligence-context-eligibility-result">
      {result.signals.map(signal => <article key={signal.signalId}><strong>{signal.tagPath}{signal.engineeringUnit ? ` (${signal.engineeringUnit})` : ''}</strong>
        <p>{intelligenceEvidenceText(signal.reference.status, locale)}</p>
        <p>{text('Leituras recebidas / sincronizadas / compatíveis com todas as condições', 'Readings received / synchronized / matching all conditions', 'Lecturas recibidas / sincronizadas / compatibles con todas las condiciones')}: {signal.reference.rawObservationCount} / {signal.reference.acceptedSynchronizedUniqueCount} / {signal.reference.contextEligibleObservationCount}</p>
        {signal.comparison && <><p>{text('Comparação', 'Comparison', 'Comparación')}: {intelligenceEvidenceText(signal.comparison.status, locale)}</p>
          <p>{signal.comparison.rawObservationCount} / {signal.comparison.acceptedSynchronizedUniqueCount} / {signal.comparison.contextEligibleObservationCount}</p></>}
      </article>)}
    </div>}
  </details>;
}
