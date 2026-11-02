import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { EngineeringLocale } from './i18n';
import { intelligenceEvidenceText } from './intelligenceEvidenceText';
import { IntelligenceComparisonPeriod, matchesIntelligenceWindow, useIntelligenceComparisonPeriod } from './IntelligenceComparisonPeriod';
import { coverageCopy, coverageErrorText, localDateTimeInput, parseWindow, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type SignalEvidenceWindow = { rawObservationCount: number; acceptedSynchronizedCount: number; rejectedObservationCount: number; rejectionCounts?: Record<string, number>; status?: string };
type CohortEvidence = { completeRequiredNumericCohortCount: number | null; completeSelectedNumericCohortCount?: number | null; requiredReasons?: string[]; selectedReasons?: string[] };
type Evidence = {
  contractVersion: number; projectKey: string; activeRevision: number; scopeId: string; profileId: string;
  referenceWindow: WindowValue;
  comparisonWindow?: WindowValue | null;
  referenceCohort: CohortEvidence;
  comparisonCohort?: CohortEvidence | null;
  signals: Array<{ signalId: string; tagPath: string; unit: string | null; required: boolean;
    reference: SignalEvidenceWindow; comparison?: SignalEvidenceWindow | null }>;
};
function isCohortEvidence(value: unknown): value is CohortEvidence {
  if (!value || typeof value !== 'object') return false;
  const cohort = value as Partial<CohortEvidence>;
  const count = (item: unknown) => item === null || Number.isInteger(item) && Number(item) >= 0;
  return count(cohort.completeRequiredNumericCohortCount) &&
    (cohort.completeSelectedNumericCohortCount === undefined || count(cohort.completeSelectedNumericCohortCount)) &&
    [cohort.requiredReasons, cohort.selectedReasons].every(items => items === undefined ||
      Array.isArray(items) && items.length <= 32 && items.every(item => typeof item === 'string'));
}

export function IntelligenceReferenceEvidencePanel({ locale, scopeId, profileId, projectKey, activeRevision, canQuery }: {
  locale: EngineeringLocale; scopeId: string; profileId: string;
  projectKey: string | null; activeRevision: number | null; canQuery: boolean;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const [from, setFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 86400000)));
  const [to, setTo] = useState(() => localDateTimeInput(new Date()));
  const window = parseWindow(from, to).window;
  const comparison = useIntelligenceComparisonPeriod(window);
  const [result, setResult] = useState<Evidence | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const sequence = useRef(0);
  useEffect(() => {
    controller.current?.abort();
    sequence.current++;
    setResult(null); setError(null); setBusy(false);
    return () => controller.current?.abort();
  }, [scopeId, profileId, projectKey, activeRevision, canQuery, from, to, comparison.key]);
  async function query(event: FormEvent) {
    event.preventDefault();
    if (!canQuery || !projectKey || activeRevision === null || !window || !comparison.valid) return;
    controller.current?.abort();
    const current = new AbortController(); controller.current = current;
    const request = ++sequence.current;
    setBusy(true); setResult(null); setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/reference-evidence/v1/query`, {
        method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopeKind: 0, scopeId, profileId, referenceWindow: window, comparisonWindow: comparison.window }), signal: current.signal
      });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(typeof payload?.code === 'string' ? payload.code : `HTTP_${response.status}`);
      if (payload?.contractVersion !== 1 || payload.projectKey !== projectKey || payload.activeRevision !== activeRevision ||
          payload.scopeId?.toLowerCase() !== scopeId.toLowerCase() || payload.profileId?.toLowerCase() !== profileId.toLowerCase() ||
          Date.parse(payload.referenceWindow?.fromUtc) !== Date.parse(window.fromUtc) ||
          Date.parse(payload.referenceWindow?.toUtc) !== Date.parse(window.toUtc) ||
          !matchesIntelligenceWindow(payload.comparisonWindow, comparison.window) ||
          !isCohortEvidence(payload.referenceCohort) ||
          (comparison.window ? !isCohortEvidence(payload.comparisonCohort) : payload.comparisonCohort != null) ||
          !Array.isArray(payload.signals) || payload.signals.length > 16 ||
          !payload.signals.every((signal: Evidence['signals'][number]) => typeof signal?.tagPath === 'string' &&
            typeof signal.signalId === 'string' && (signal.unit == null || typeof signal.unit === 'string') && signal.reference &&
            (comparison.window ? !!signal.comparison : signal.comparison == null) &&
            [signal.reference, ...(comparison.window ? [signal.comparison] : [])].every(evidence => !!evidence &&
              (evidence.status === undefined || typeof evidence.status === 'string') &&
              [evidence.rawObservationCount, evidence.acceptedSynchronizedCount, evidence.rejectedObservationCount]
                .every(count => Number.isInteger(count) && count >= 0)))) throw new Error('INVALID_RESPONSE');
      if (!current.signal.aborted && sequence.current === request) setResult(payload);
    } catch (cause) {
      if (!current.signal.aborted && sequence.current === request)
        setError(coverageErrorText(cause instanceof Error ? cause.message : 'NETWORK_ERROR', coverageCopy(locale)));
    } finally {
      if (sequence.current === request) setBusy(false);
    }
  }
  const cohortEvidence = (cohort: CohortEvidence, label: string) => <section>
    <h4>{label}</h4>
    <p>{text('Leituras simultâneas dos sinais numéricos obrigatórios', 'Simultaneous readings of required numeric signals', 'Lecturas simultáneas de señales numéricas obligatorias')}: {cohort.completeRequiredNumericCohortCount ?? text('Não disponível', 'Unavailable', 'No disponible')}</p>
    {cohort.requiredReasons?.map(reason => <p key={reason}>{intelligenceEvidenceText(reason, locale)}</p>)}
    {cohort.completeSelectedNumericCohortCount !== undefined && <p>{text('Leituras simultâneas dos sinais numéricos do perfil', 'Simultaneous readings of numeric profile signals', 'Lecturas simultáneas de señales numéricas del perfil')}: {cohort.completeSelectedNumericCohortCount ?? text('Não disponível', 'Unavailable', 'No disponible')}</p>}
    {cohort.selectedReasons?.map(reason => <p key={reason}>{intelligenceEvidenceText(reason, locale)}</p>)}
  </section>;
  return <details className="asset-process-intelligence__coverage asset-process-intelligence__analysis" data-testid={`intelligence-reference-evidence-${profileId}`}>
    <summary>{text('Conferir dados da referência', 'Check reference data', 'Revisar datos de referencia')}</summary>
    <p>{text('Escolha um período conhecido do equipamento. A consulta mostra quais leituras podem ser utilizadas; não confirma que o equipamento estava saudável.',
      'Choose a known equipment period. The query shows which readings can be used; it does not confirm that the equipment was healthy.',
      'Elija un período conocido del equipo. La consulta muestra qué lecturas se pueden usar; no confirma que el equipo estuviera sano.')}</p>
    {!canQuery && <p role="note">{text('Publique as alterações e ative este equipamento com as permissões de consulta antes de continuar.',
      'Publish changes and activate this equipment with query permissions before continuing.',
      'Publique los cambios y active este equipo con permisos de consulta antes de continuar.')}</p>}
    <form className="asset-process-intelligence__coverage-form" onSubmit={event => void query(event)}>
      <label>{text('Início da referência (horário local)', 'Reference start (local time)', 'Inicio de referencia (hora local)')}
        <input type="datetime-local" value={from} disabled={!canQuery} onChange={event => setFrom(event.currentTarget.value)}/></label>
      <label>{text('Fim da referência (horário local)', 'Reference end (local time)', 'Fin de referencia (hora local)')}
        <input type="datetime-local" value={to} disabled={!canQuery} onChange={event => setTo(event.currentTarget.value)}/></label>
      <p>{text('Use um intervalo de até 31 dias. Os horários serão convertidos para UTC.', 'Use a period of up to 31 days. Times are converted to UTC.', 'Use un período de hasta 31 días. Las horas se convierten a UTC.')}</p>
      {!window && <p role="alert">{text('Confira o início e o fim do período.', 'Check the start and end of the period.', 'Revise el inicio y el fin del período.')}</p>}
      <IntelligenceComparisonPeriod locale={locale} period={comparison}/>
      <button type="submit" disabled={!canQuery || !window || !comparison.valid || busy}>{text('Consultar referência', 'Query reference', 'Consultar referencia')}</button>
      {busy && <button type="button" onClick={() => { controller.current?.abort(); sequence.current++; setBusy(false); }}>{text('Cancelar consulta', 'Cancel query', 'Cancelar consulta')}</button>}
    </form>
    {error && <p role="alert">{error}</p>}
    {result && <div className="asset-process-intelligence__coverage-result" data-testid="intelligence-reference-evidence-result">
      {cohortEvidence(result.referenceCohort, text('Referência', 'Reference', 'Referencia'))}
      {result.comparisonCohort && cohortEvidence(result.comparisonCohort, text('Comparação', 'Comparison', 'Comparación'))}
      <div className="asset-process-intelligence__coverage-signals">{result.signals.map(signal => <article key={signal.signalId}>
        <strong>{signal.tagPath}{signal.unit ? ` (${signal.unit})` : ''}</strong>
        {signal.reference.status && <p>{intelligenceEvidenceText(signal.reference.status, locale)}</p>}
        <p>{text('Leituras recebidas / aceitas com origem sincronizada / rejeitadas', 'Readings received / accepted with synchronized source / rejected', 'Lecturas recibidas / aceptadas con origen sincronizado / rechazadas')}: {signal.reference.rawObservationCount} / {signal.reference.acceptedSynchronizedCount} / {signal.reference.rejectedObservationCount}</p>
        {Object.entries(signal.reference.rejectionCounts ?? {}).filter(([, count]) => Number.isInteger(count) && count > 0)
          .map(([reason, count]) => <p key={reason}>{intelligenceEvidenceText(reason, locale)}: {count}</p>)}
        {signal.comparison && <p>{text('Comparação: recebidas / aceitas / rejeitadas', 'Comparison: received / accepted / rejected', 'Comparación: recibidas / aceptadas / rechazadas')}: {signal.comparison.rawObservationCount} / {signal.comparison.acceptedSynchronizedCount} / {signal.comparison.rejectedObservationCount}</p>}
      </article>)}</div>
      <p>{text('A contagem de leituras não comprova saúde, representatividade ou completude do período.', 'Reading counts do not prove health, representativeness or completeness of the period.', 'El número de lecturas no demuestra salud, representatividad ni integridad del período.')}</p>
    </div>}
  </details>;
}
