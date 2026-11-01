import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { EngineeringLocale } from './i18n';
import { coverageCopy, coverageErrorText, localDateTimeInput, parseWindow, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type Evidence = {
  contractVersion: number; projectKey: string; activeRevision: number; scopeId: string; profileId: string;
  referenceWindow: WindowValue;
  referenceCohort: { completeRequiredNumericCohortCount: number | null };
  signals: Array<{ signalId: string; tagPath: string; unit: string | null; required: boolean;
    reference: { rawObservationCount: number; acceptedSynchronizedCount: number; rejectedObservationCount: number } }>;
};

export function IntelligenceReferenceEvidencePanel({ locale, scopeId, profileId, projectKey, activeRevision, canQuery }: {
  locale: EngineeringLocale; scopeId: string; profileId: string;
  projectKey: string | null; activeRevision: number | null; canQuery: boolean;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const [from, setFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 86400000)));
  const [to, setTo] = useState(() => localDateTimeInput(new Date()));
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
  }, [scopeId, profileId, projectKey, activeRevision, canQuery, from, to]);
  const window = parseWindow(from, to).window;
  async function query(event: FormEvent) {
    event.preventDefault();
    if (!canQuery || !projectKey || activeRevision === null || !window) return;
    controller.current?.abort();
    const current = new AbortController(); controller.current = current;
    const request = ++sequence.current;
    setBusy(true); setResult(null); setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/reference-evidence/v1/query`, {
        method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopeKind: 0, scopeId, profileId, referenceWindow: window }), signal: current.signal
      });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(typeof payload?.code === 'string' ? payload.code : `HTTP_${response.status}`);
      if (payload?.contractVersion !== 1 || payload.projectKey !== projectKey || payload.activeRevision !== activeRevision ||
          payload.scopeId?.toLowerCase() !== scopeId.toLowerCase() || payload.profileId?.toLowerCase() !== profileId.toLowerCase() ||
          Date.parse(payload.referenceWindow?.fromUtc) !== Date.parse(window.fromUtc) ||
          Date.parse(payload.referenceWindow?.toUtc) !== Date.parse(window.toUtc) ||
          !payload.referenceCohort || !(payload.referenceCohort.completeRequiredNumericCohortCount === null ||
            Number.isInteger(payload.referenceCohort.completeRequiredNumericCohortCount) && payload.referenceCohort.completeRequiredNumericCohortCount >= 0) ||
          !Array.isArray(payload.signals) || payload.signals.length > 16 ||
          !payload.signals.every((signal: Evidence['signals'][number]) => typeof signal?.tagPath === 'string' &&
            typeof signal.signalId === 'string' && signal.reference &&
            [signal.reference.rawObservationCount, signal.reference.acceptedSynchronizedCount, signal.reference.rejectedObservationCount]
              .every(count => Number.isInteger(count) && count >= 0))) throw new Error('INVALID_RESPONSE');
      if (!current.signal.aborted && sequence.current === request) setResult(payload);
    } catch (cause) {
      if (!current.signal.aborted && sequence.current === request)
        setError(coverageErrorText(cause instanceof Error ? cause.message : 'NETWORK_ERROR', coverageCopy(locale)));
    } finally {
      if (sequence.current === request) setBusy(false);
    }
  }
  return <details className="asset-process-intelligence__coverage" data-testid={`intelligence-reference-evidence-${profileId}`}>
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
      <button type="submit" disabled={!canQuery || !window || busy}>{text('Consultar referência', 'Query reference', 'Consultar referencia')}</button>
      {busy && <button type="button" onClick={() => { controller.current?.abort(); sequence.current++; setBusy(false); }}>{text('Cancelar consulta', 'Cancel query', 'Cancelar consulta')}</button>}
    </form>
    {error && <p role="alert">{error}</p>}
    {result && <div data-testid="intelligence-reference-evidence-result">
      <p role="status">{text('Leituras simultâneas dos sinais numéricos obrigatórios', 'Simultaneous readings of required numeric signals', 'Lecturas simultáneas de señales numéricas obligatorias')}: {result.referenceCohort.completeRequiredNumericCohortCount ?? text('Não disponível', 'Unavailable', 'No disponible')}</p>
      {result.signals.map(signal => <article key={signal.signalId}>
        <strong>{signal.tagPath}{signal.unit ? ` (${signal.unit})` : ''}</strong>
        <p>{text('Leituras recebidas / aceitas com origem sincronizada / rejeitadas', 'Readings received / accepted with synchronized source / rejected', 'Lecturas recibidas / aceptadas con origen sincronizado / rechazadas')}: {signal.reference.rawObservationCount} / {signal.reference.acceptedSynchronizedCount} / {signal.reference.rejectedObservationCount}</p>
      </article>)}
      <p>{text('A contagem de leituras não comprova saúde, representatividade ou completude do período.', 'Reading counts do not prove health, representativeness or completeness of the period.', 'El número de lecturas no demuestra salud, representatividad ni integridad del período.')}</p>
    </div>}
  </details>;
}
