import { useEffect, useRef, useState, type FormEvent } from 'react';
import type { EngineeringLocale } from './i18n';
import type { AssetProcessIntelligenceOperatingContextProfileEngineering, AssetProcessIntelligenceProfileEngineering, TagEngineering } from './types';
import { coverageCopy, coverageErrorText, localDateTimeInput, parseWindow } from './AssetProcessIntelligenceCoveragePanel';
import { isNumericTagDataType } from './intelligenceDashboardGenerator';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type Score = { sourceTimestampUtc: string; score: number | null; levelComponent: number | null; relationshipComponent: number | null;
  crossSignalInteraction: number | null; targetContributions: Array<{ signalId: string; level: number; marginalVariance: number }> };
type JointResult = { referenceFitStatus: string; referenceFitUnavailableReason: string | null;
  referenceCompleteCohortCount: number; evaluationScoredCohortCount: number; evaluationWarmupCohortCount: number;
  omittedEvaluationScores: number; scores: Score[] };

export function IntelligenceExperimentalJointPanel({ locale, scopeId, assignmentId, profile, tags, contexts, projectKey, activeRevision, canQuery }: {
  locale: EngineeringLocale; scopeId: string; assignmentId: string; profile: AssetProcessIntelligenceProfileEngineering;
  tags: readonly TagEngineering[]; contexts: readonly AssetProcessIntelligenceOperatingContextProfileEngineering[];
  projectKey: string | null; activeRevision: number | null; canQuery: boolean;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const [contextId, setContextId] = useState('');
  const context = contexts.find(item => item.id === contextId && item.assignmentId === assignmentId);
  const [targets, setTargets] = useState<string[]>([]);
  const [cadence, setCadence] = useState('');
  const [from, setFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 172800000)));
  const [to, setTo] = useState(() => localDateTimeInput(new Date(Date.now() - 86400000)));
  const [evaluationFrom, setEvaluationFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 86400000)));
  const [evaluationTo, setEvaluationTo] = useState(() => localDateTimeInput(new Date()));
  const [result, setResult] = useState<JointResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const sequence = useRef(0);
  const options = profile.signals.flatMap(signal => {
    const tag = tags.find(item => item.id?.toLowerCase() === signal.tagId.toLowerCase());
    return tag && isNumericTagDataType(tag.dataType) && !context?.predicates.some(predicate => predicate.tagId.toLowerCase() === signal.tagId.toLowerCase())
      ? [{ id: signal.id, tagId: signal.tagId, label: tag.name || tag.path }] : [];
  });
  const selectionKey = JSON.stringify({ profile, contexts, tags });
  useEffect(() => {
    controller.current?.abort(); sequence.current++;
    setResult(null); setError(null); setBusy(false);
    return () => controller.current?.abort();
  }, [scopeId, assignmentId, selectionKey, contextId, projectKey, activeRevision, canQuery, from, to, evaluationFrom, evaluationTo, cadence, targets]);
  const reference = parseWindow(from, to).window;
  const evaluation = parseWindow(evaluationFrom, evaluationTo).window;
  const seconds = Number(cadence);
  const targetOptions = targets.map(id => options.find(option => option.id === id));
  const valid = !!context && !!reference && !!evaluation && Date.parse(reference.toUtc) <= Date.parse(evaluation.fromUtc) &&
    Number.isInteger(seconds) && seconds >= 1 && seconds <= 86400 && targets.length >= 2 && targets.length <= 8 &&
    targetOptions.every(Boolean) && new Set(targetOptions.map(option => option?.tagId.toLowerCase())).size === targets.length;
  const unavailable = text('Dados insuficientes para ajustar a referência. Confira o período, a variação dos sinais e o contexto.',
    'Not enough data to fit the reference. Check the period, signal variation and context.',
    'Datos insuficientes para ajustar la referencia. Revise el período, la variación de señales y el contexto.');
  async function query(event: FormEvent) {
    event.preventDefault();
    if (!canQuery || !valid || !context || !reference || !evaluation || !projectKey || activeRevision === null) return;
    controller.current?.abort();
    const current = new AbortController(); controller.current = current;
    const request = ++sequence.current;
    setBusy(true); setResult(null); setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/experimental-joint-deviation/v1/query`, {
        method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopeKind: 0, scopeId, assignmentId, operatingContextProfileId: context.id,
          expectedProfileVersion: context.version, targetSignalIds: targets, expectedCadenceSeconds: seconds,
          referenceWindow: reference, evaluationWindow: evaluation }), signal: current.signal
      });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(typeof payload?.code === 'string' ? payload.code : `HTTP_${response.status}`);
      const sameWindow = (value: { fromUtc?: string; toUtc?: string } | null, expected: { fromUtc: string; toUtc: string }) =>
        Date.parse(value?.fromUtc ?? '') === Date.parse(expected.fromUtc) && Date.parse(value?.toUtc ?? '') === Date.parse(expected.toUtc);
      if (payload?.contractVersion !== 1 || payload.projectKey !== projectKey || payload.activeRevision !== activeRevision ||
          payload.scopeId !== scopeId || payload.assignmentId !== assignmentId || payload.measurementProfileId !== profile.id ||
          payload.operatingContextProfileId !== context.id || payload.operatingContextProfileVersion !== context.version ||
          payload.expectedCadenceSeconds !== seconds || payload.validationStatus !== 'experimental_not_field_validated' ||
          payload.classificationStatus !== 'none' || payload.decisionThreshold !== null ||
          !sameWindow(payload.referenceWindow, reference) || !sameWindow(payload.evaluationWindow, evaluation) ||
          !Array.isArray(payload.targetSignals) || payload.targetSignals.length !== targets.length ||
          new Set(payload.targetSignals.map((signal: { signalId: string }) => signal?.signalId)).size !== targets.length ||
          !payload.targetSignals.every((signal: { signalId: string }) => targets.includes(signal?.signalId)) ||
          !['available', 'insufficient_data'].includes(payload.referenceFitStatus) ||
          ![payload.referenceCompleteCohortCount, payload.evaluationScoredCohortCount, payload.evaluationWarmupCohortCount, payload.omittedEvaluationScores]
            .every(count => Number.isInteger(count) && count >= 0) ||
          !Array.isArray(payload.scores) || payload.scores.length > 500 || !payload.scores.every((point: Score) =>
            Number.isFinite(Date.parse(point?.sourceTimestampUtc)) && [point.score, point.levelComponent, point.relationshipComponent, point.crossSignalInteraction]
              .every(value => value === null || typeof value === 'number' && Number.isFinite(value)) &&
            Array.isArray(point.targetContributions) && point.targetContributions.length === targets.length &&
            new Set(point.targetContributions.map(item => item?.signalId)).size === targets.length &&
            point.targetContributions.every(item => targets.includes(item?.signalId) && Number.isFinite(item.level) && Number.isFinite(item.marginalVariance)))) throw new Error('INVALID_RESPONSE');
      if (!current.signal.aborted && sequence.current === request) setResult(payload);
    } catch (cause) {
      if (!current.signal.aborted && sequence.current === request) setError(coverageErrorText(cause instanceof Error ? cause.message : 'NETWORK_ERROR', coverageCopy(locale)));
    } finally { if (sequence.current === request) setBusy(false); }
  }
  const number = (value: number | null) => value === null ? '—' : new Intl.NumberFormat(locale, { maximumFractionDigits: 4 }).format(value);
  return <details className="asset-process-intelligence__coverage" data-testid={`intelligence-joint-deviation-${assignmentId}`}>
    <summary>{text('Desvio conjunto experimental', 'Experimental joint deviation', 'Desviación conjunta experimental')}</summary>
    <p>{text('Compare de dois a oito sinais com uma referência escolhida, sob as condições do contexto salvo. O cálculo observa mudanças de nível e da relação entre sinais.',
      'Compare two to eight signals against a selected reference, under saved context conditions. The calculation observes changes in level and relationships between signals.',
      'Compare de dos a ocho señales con una referencia elegida, bajo las condiciones del contexto guardado. El cálculo observa cambios de nivel y de la relación entre señales.')}</p>
    <p role="note">{text('Experimental, validado somente por simulação. O resultado não diagnostica falhas, não prevê vida útil e não tem limiar de alarme validado em campo.',
      'Experimental, validated by simulation only. Results do not diagnose faults, predict remaining life or provide a field-validated alarm threshold.',
      'Experimental, validado solo por simulación. El resultado no diagnostica fallos, no predice vida útil ni tiene umbral de alarma validado en campo.')}</p>
    {!canQuery && <p>{text('Publique as alterações e confira a ativação e as permissões deste equipamento antes de consultar.', 'Publish changes and check equipment activation and permissions before querying.', 'Publique los cambios y revise la activación y los permisos del equipo antes de consultar.')}</p>}
    <form onSubmit={event => void query(event)}>
      <label>{text('Contexto salvo', 'Saved context', 'Contexto guardado')}<select value={contextId} onChange={event => { setContextId(event.currentTarget.value); setTargets([]); }}>
        <option value="">{text('Escolha o contexto', 'Choose a context', 'Elija un contexto')}</option>
        {contexts.filter(item => item.assignmentId === assignmentId).map(item => <option key={item.id} value={item.id}>{item.displayLabel}</option>)}
      </select></label>
      <fieldset><legend>{text('Medições a comparar (2 a 8)', 'Measurements to compare (2 to 8)', 'Mediciones a comparar (2 a 8)')}</legend>
        {options.map(option => <label key={option.id}><input type="checkbox" checked={targets.includes(option.id)} disabled={!context || targets.length >= 8 && !targets.includes(option.id)}
          onChange={event => { const checked = event.currentTarget.checked; setTargets(current => checked ? [...current, option.id] : current.filter(id => id !== option.id)); }}/>{option.label}</label>)}
      </fieldset>
      <label>{text('Intervalo esperado entre leituras (segundos)', 'Expected interval between readings (seconds)', 'Intervalo esperado entre lecturas (segundos)')}
        <input type="number" min="1" max="86400" step="1" value={cadence} onChange={event => setCadence(event.currentTarget.value)}/></label>
      <p>{text('Informe a cadência real de aquisição dos sinais. Uma lacuna interrompe o estado temporal; o sistema não preenche leituras ausentes.', 'Enter the actual signal acquisition cadence. A gap breaks temporal state; missing readings are not filled.', 'Indique la cadencia real de adquisición. Una laguna interrumpe el estado temporal; no se rellenan lecturas ausentes.')}</p>
      <div className="asset-process-intelligence__coverage-form">
        <label>{text('Início da referência', 'Reference start', 'Inicio de referencia')}<input type="datetime-local" value={from} onChange={event => setFrom(event.currentTarget.value)}/></label>
        <label>{text('Fim da referência', 'Reference end', 'Fin de referencia')}<input type="datetime-local" value={to} onChange={event => setTo(event.currentTarget.value)}/></label>
        <label>{text('Início da avaliação', 'Evaluation start', 'Inicio de evaluación')}<input type="datetime-local" value={evaluationFrom} onChange={event => setEvaluationFrom(event.currentTarget.value)}/></label>
        <label>{text('Fim da avaliação', 'Evaluation end', 'Fin de evaluación')}<input type="datetime-local" value={evaluationTo} onChange={event => setEvaluationTo(event.currentTarget.value)}/></label>
      </div>
      <p>{text('Horários locais, convertidos para UTC. Até 31 dias por período; a avaliação deve começar após o fim da referência.', 'Local times, converted to UTC. Up to 31 days per period; evaluation must start after the reference ends.', 'Horas locales, convertidas a UTC. Hasta 31 días por período; la evaluación debe comenzar después de la referencia.')}</p>
      <button type="submit" disabled={!canQuery || !valid || busy}>{text('Calcular desvio conjunto', 'Calculate joint deviation', 'Calcular desviación conjunta')}</button>
      {busy && <button type="button" onClick={() => { controller.current?.abort(); sequence.current++; setBusy(false); }}>{text('Cancelar consulta', 'Cancel query', 'Cancelar consulta')}</button>}
    </form>
    {error && <p role="alert">{error}</p>}
    {result && <div data-testid="intelligence-joint-deviation-result">
      <p role="status">{result.referenceFitStatus === 'available' ? text('Referência ajustada para esta consulta.', 'Reference fitted for this query.', 'Referencia ajustada para esta consulta.') : unavailable}</p>
      <p>{text('Instantes completos na referência / avaliados com score / em aquecimento', 'Complete reference timestamps / scored / warming up', 'Instantes completos de referencia / evaluados con score / en calentamiento')}: {result.referenceCompleteCohortCount} / {result.evaluationScoredCohortCount} / {result.evaluationWarmupCohortCount}</p>
      {result.scores.length > 0 && <table><thead><tr><th>{text('Horário local', 'Local time', 'Hora local')}</th><th>{text('Desvio conjunto', 'Joint deviation', 'Desviación conjunta')}</th><th>{text('Componente de nível', 'Level component', 'Componente de nivel')}</th><th>{text('Componente de relação', 'Relationship component', 'Componente de relación')}</th></tr></thead>
        <tbody>{result.scores.map((point, index) => <tr key={`${point.sourceTimestampUtc}:${index}`}><td>{new Date(point.sourceTimestampUtc).toLocaleString(locale)}</td><td>{number(point.score)}
          <details><summary>{text('Contribuições das medições', 'Measurement contributions', 'Contribuciones de las mediciones')}</summary>
            {point.targetContributions.map(item => <p key={item.signalId}>{options.find(option => option.id === item.signalId)?.label}: {text('nível', 'level', 'nivel')} {number(item.level)}; {text('variância marginal', 'marginal variance', 'varianza marginal')} {number(item.marginalVariance)}</p>)}
            <p>{text('Interação entre sinais', 'Cross-signal interaction', 'Interacción entre señales')}: {number(point.crossSignalInteraction)}</p>
          </details></td><td>{number(point.levelComponent)}</td><td>{number(point.relationshipComponent)}</td></tr>)}</tbody></table>}
      <p>{text('Os números descrevem afastamento desta referência, sem unidade física. Um valor maior não confirma defeito. As contribuições ajudam a investigar quais medições mudaram; não identificam a causa.',
        'Numbers describe deviation from this reference, without a physical unit. A higher value does not confirm a fault. Contributions help investigate which measurements changed; they do not identify the cause.',
        'Los números describen alejamiento de esta referencia, sin unidad física. Un valor mayor no confirma un fallo. Las contribuciones ayudan a investigar qué mediciones cambiaron; no identifican la causa.')}</p>
      <p>{text('Pontos adicionais não exibidos', 'Additional points not displayed', 'Puntos adicionales no mostrados')}: {result.omittedEvaluationScores}</p>
    </div>}
  </details>;
}
