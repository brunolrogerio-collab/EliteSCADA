import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthGate';
import type { EngineeringLocale } from './i18n';
import type { AssetProcessIntelligenceProfileEngineering, TagEngineering } from './types';
import type { VersionedControlledUnitCatalog } from './semanticCatalogApi';
import { isNumericTagDataType } from './intelligenceDashboardGenerator';
import { parseWindow, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';
import { matchesIntelligenceWindow } from './IntelligenceComparisonPeriod';
import './AssetProcessIntelligenceContextualDeviationPanel.css';
import './intelligence-single-target.css';
import { intelligenceEvidenceText } from './intelligenceEvidenceText';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type Signal = { id: string; tagId: string; label: string; unit: string | null; canonicalUnit: string | null; unitCode: string | null };
type Counts = { rawObservationCount: number; goodFiniteNumericCount: number; acceptedSynchronizedSampleCount: number;
  rejectedObservationCount: number; ambiguousSourceTimestampGroupCount: number; ambiguousSourceTimestampRowCount: number;
  qualityCounts: Record<string, number>; rejectionCounts: Record<string, number> };
type Evidence = { window: WindowValue; target: Counts; condition: Counts; exactSourceTimestampPairCount: number;
  conditionInRangePairCount: number; conditionOutOfRangePairCount: number; qualifiedPairCount: number };
type Sample = { sourceTimestampUtc: string; targetObservedTimestampUtc: string; conditionObservedTimestampUtc: string;
  targetValue: number; conditionValueCanonical: number; conditionInRange: boolean; rawDifferenceFromReferenceMedian: number | null;
  robustScore: number | null; scoreUnavailableReason: string | null };
type Result = { referenceMedian: number | null; referenceMedianAbsoluteDeviation: number | null; status: string; reasonCodes: string[];
  referenceEvidence: Evidence; evaluationEvidence: Evidence; totalRawObservationCount: number; returnedEvaluationSampleCount: number;
  omittedEvaluationSampleCount: number; evaluationSamples: Sample[] };
const count = (value: unknown) => Number.isInteger(value) && Number(value) >= 0 && Number(value) <= 50000;
const finite = (value: unknown) => typeof value === 'number' && Number.isFinite(value);
const optionalNumber = (value: unknown) => value === null || finite(value);
const sameId = (value: unknown, expected: string) => typeof value === 'string' && value.toLowerCase() === expected.toLowerCase();
const inside = (value: string, window: WindowValue) => typeof value === 'string' && Date.parse(value) >= Date.parse(window.fromUtc) && Date.parse(value) <= Date.parse(window.toUtc);
function evidenceValid(value: Evidence | undefined, window: WindowValue) {
  const map = (value: unknown) => !!value && typeof value === 'object' && !Array.isArray(value) && Object.keys(value).length <= 64 && Object.values(value).every(count);
  return !!value && matchesIntelligenceWindow(value.window, window) &&
    [value.exactSourceTimestampPairCount, value.conditionInRangePairCount, value.conditionOutOfRangePairCount, value.qualifiedPairCount].every(count) &&
    [value.target, value.condition].every(signal => !!signal && [signal.rawObservationCount, signal.goodFiniteNumericCount,
      signal.acceptedSynchronizedSampleCount, signal.rejectedObservationCount, signal.ambiguousSourceTimestampGroupCount,
      signal.ambiguousSourceTimestampRowCount].every(count) && map(signal.qualityCounts) && map(signal.rejectionCounts));
}

export function IntelligenceSingleTargetDeviationPanel({ locale, scopeId, assignmentId, profile, tags, controlledUnits, projectKey, activeRevision, canQuery }: {
  locale: EngineeringLocale; scopeId: string; assignmentId: string; profile: AssetProcessIntelligenceProfileEngineering;
  tags: readonly TagEngineering[]; controlledUnits: readonly VersionedControlledUnitCatalog[];
  projectKey: string | null; activeRevision: number | null; canQuery: boolean;
}) {
  const t = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const { profile: session } = useAuth();
  const sessionKey = session ? JSON.stringify([session.subjectId, session.identityProvider, session.expiresAtUtc, [...session.roles].sort()]) : 'anonymous';
  const duplicate = new Set(profile.signals.map(signal => signal.id.toLowerCase())).size !== profile.signals.length ||
    new Set(profile.signals.map(signal => signal.tagId.toLowerCase())).size !== profile.signals.length;
  const signals: Signal[] = duplicate ? [] : profile.signals.flatMap(signal => {
    const matches = tags.filter(tag => sameId(tag.id, signal.tagId));
    if (matches.length !== 1 || !isNumericTagDataType(matches[0].dataType)) return [];
    const tag = matches[0], ref = tag.controlledEngineeringUnit;
    const units = ref ? controlledUnits.filter(catalog => catalog.catalogVersion === ref.catalogVersion).flatMap(catalog => catalog.units.filter(unit => unit.code === ref.code)) : [];
    const unit = units.length === 1 && finite(units[0].scaleToCanonical) && units[0].scaleToCanonical > 0 ? units[0] : null;
    return [{ id: signal.id, tagId: signal.tagId, label: tag.name || tag.path, unit: tag.engineeringUnit ?? null,
      canonicalUnit: unit?.canonicalUnit ?? null, unitCode: unit ? ref!.code : null }];
  });
  const [targetId, setTargetId] = useState(''), [conditionId, setConditionId] = useState('');
  const [referenceFrom, setReferenceFrom] = useState(''), [referenceTo, setReferenceTo] = useState('');
  const [evaluationFrom, setEvaluationFrom] = useState(''), [evaluationTo, setEvaluationTo] = useState('');
  const [lower, setLower] = useState(''), [upper, setUpper] = useState('');
  const [result, setResult] = useState<Result | null>(null), [error, setError] = useState(false), [busy, setBusy] = useState(false), [showSamples, setShowSamples] = useState(false);
  const abort = useRef<AbortController | null>(null), sequence = useRef(0);
  const target = signals.find(signal => signal.id === targetId), condition = signals.find(signal => signal.id === conditionId && signal.canonicalUnit);
  const reference = parseWindow(referenceFrom, referenceTo).window, evaluation = parseWindow(evaluationFrom, evaluationTo).window;
  const ready = !!target && !!condition && target.tagId !== condition.tagId && !!reference && !!evaluation &&
    Date.parse(reference.toUtc) < Date.parse(evaluation.fromUtc) && lower.trim() !== '' && upper.trim() !== '' &&
    finite(Number(lower)) && finite(Number(upper)) && Number(lower) < Number(upper);
  const signalsKey = JSON.stringify(signals);
  useEffect(() => {
    abort.current?.abort(); sequence.current++; setResult(null); setError(false); setBusy(false); setShowSamples(false);
    return () => { abort.current?.abort(); sequence.current++; };
  }, [scopeId, assignmentId, profile.id, projectKey, activeRevision, canQuery, sessionKey, signalsKey, targetId, conditionId,
    referenceFrom, referenceTo, evaluationFrom, evaluationTo, lower, upper]);
  async function query(event: FormEvent) {
    event.preventDefault();
    if (!canQuery || !projectKey || activeRevision === null || !ready || !target || !condition || !reference || !evaluation) return;
    abort.current?.abort(); const controller = new AbortController(); abort.current = controller; const request = ++sequence.current;
    setBusy(true); setResult(null); setError(false);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/contextual-reference-deviation/v1/query`, {
        method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json' }, signal: controller.signal,
        body: JSON.stringify({ scopeKind: 0, scopeId, assignmentId, targetSignalId: target.id, conditionSignalId: condition.id,
          referenceWindow: reference, evaluationWindow: evaluation, conditionLowerInclusiveCanonical: Number(lower), conditionUpperExclusiveCanonical: Number(upper) })
      });
      const data = await response.json();
      const matchesSignal = (value: any, signal: Signal) => !!value && sameId(value.signalId, signal.id) && sameId(value.tagId, signal.tagId) &&
        value.engineeringUnit === signal.unit && value.controlledUnitCode === signal.unitCode && value.canonicalUnit === signal.canonicalUnit;
      if (!response.ok || data.contractVersion !== 1 || data.catalogVersion !== 1 || data.ruleId !== 'contextual-reference-deviation.v1' || data.ruleVersion !== '1.0.0' ||
        data.projectKey !== projectKey || data.activeRevision !== activeRevision || ![0, 'Equipment'].includes(data.scopeKind) ||
        !sameId(data.scopeId, scopeId) || !sameId(data.equipmentId, scopeId) || !sameId(data.assignmentId, assignmentId) || !sameId(data.profileId, profile.id) ||
        !matchesSignal(data.targetSignal, target) || !matchesSignal(data.conditionSignal, condition) ||
        !matchesIntelligenceWindow(data.referenceWindow, reference) || !matchesIntelligenceWindow(data.evaluationWindow, evaluation) ||
        data.conditionLowerInclusiveCanonical !== Number(lower) || data.conditionUpperExclusiveCanonical !== Number(upper) || data.conditionCanonicalUnit !== condition.canonicalUnit ||
        !evidenceValid(data.referenceEvidence, reference) || !evidenceValid(data.evaluationEvidence, evaluation) ||
        !optionalNumber(data.referenceMedian) || !optionalNumber(data.referenceMedianAbsoluteDeviation) || data.referenceMedianAbsoluteDeviation < 0 ||
        !['available', 'insufficient_data', 'unsupported'].includes(data.status) || (data.status === 'available' && (data.referenceMedian === null || data.referenceMedianAbsoluteDeviation === null)) ||
        !Array.isArray(data.reasonCodes) || data.reasonCodes.length > 32 || !data.reasonCodes.every((reason: unknown) => typeof reason === 'string') ||
        ![data.totalRawObservationCount, data.returnedEvaluationSampleCount, data.omittedEvaluationSampleCount].every(count) ||
        !Array.isArray(data.evaluationSamples) || data.evaluationSamples.length > 500 || data.evaluationSamples.length !== data.returnedEvaluationSampleCount ||
        !data.evaluationSamples.every((sample: Sample) => !!sample && inside(sample.sourceTimestampUtc, evaluation) && inside(sample.targetObservedTimestampUtc, evaluation) && inside(sample.conditionObservedTimestampUtc, evaluation) &&
          finite(sample.targetValue) && finite(sample.conditionValueCanonical) && typeof sample.conditionInRange === 'boolean' &&
          optionalNumber(sample.rawDifferenceFromReferenceMedian) && optionalNumber(sample.robustScore) && (sample.scoreUnavailableReason === null || typeof sample.scoreUnavailableReason === 'string') &&
          (sample.conditionInRange || sample.rawDifferenceFromReferenceMedian === null && sample.robustScore === null)) ||
        data.provenance?.dataset !== 'historian.samples.v2' || data.provenance?.datasetContractVersion !== 2 ||
        data.provenance?.regimeEquivalence !== 'not_assessed' || data.provenance?.completeness !== 'unknown') throw Error('Invalid response');
      if (!controller.signal.aborted && request === sequence.current) setResult(data);
    } catch { if (!controller.signal.aborted && request === sequence.current) setError(true); }
    finally { if (request === sequence.current) setBusy(false); }
  }
  const n = (value: number | null) => value === null ? t('Não disponível', 'Unavailable', 'No disponible') : value.toLocaleString(locale, { maximumFractionDigits: 6 });
  const time = (value: string) => new Date(value).toLocaleString(locale);
  const reason = (code: string) => ({
    fewer_than_30_qualified_reference_pairs: t('Menos de 30 pares na referência: o score robusto não está disponível.', 'Fewer than 30 reference pairs: robust score is unavailable.', 'Menos de 30 pares de referencia: score robusto no disponible.'),
    reference_mad_zero: t('A referência não tem dispersão: o score robusto não está disponível.', 'The reference has no dispersion: robust score is unavailable.', 'La referencia no tiene dispersión: score robusto no disponible.'),
    no_qualified_reference_pairs: t('Não há pares utilizáveis na referência.', 'No usable reference pairs.', 'No hay pares utilizables en la referencia.'),
    no_qualified_evaluation_pairs: t('Não há pares utilizáveis na avaliação.', 'No usable evaluation pairs.', 'No hay pares utilizables en la evaluación.'),
    condition_outside_requested_range: t('Condição fora da faixa escolhida.', 'Condition outside the selected range.', 'Condición fuera del rango elegido.')
  }[code] ?? t('Há leituras rejeitadas ou limites de cálculo; confira as contagens e a qualidade dos dados.', 'Readings were rejected or calculation limits were reached; check the counts and data quality.', 'Hay lecturas rechazadas o límites de cálculo; revise cantidades y calidad de datos.'));
  const dateField = (label: string, value: string, set: (value: string) => void) => <label>{label}<input type="datetime-local" value={value} onChange={event => set(event.currentTarget.value)} /></label>;
  return <details className="asset-process-intelligence__deviation-panel intelligence-single-target" data-testid={`intelligence-single-target-${assignmentId}`}>
    <summary>{t('Comparar uma medição sob uma condição', 'Compare one measurement under a condition', 'Comparar una medición bajo una condición')}</summary>
    <div className="asset-process-intelligence__deviation-body">
      <p>{t('Escolha o que analisar e a condição que limita a comparação. Exemplo: corrente do motor quando a frequência medida está em uma faixa. A referência é escolhida por você; não é um atestado de equipamento saudável.', 'Choose what to analyze and the condition limiting the comparison. Example: motor current within a measured frequency range. You choose the reference; it is not evidence of healthy equipment.', 'Elija qué analizar y la condición que limita la comparación. Ejemplo: corriente dentro de un rango de frecuencia medida. Usted elige la referencia; no demuestra un equipo sano.')}</p>
      <form onSubmit={event => void query(event)}>
        <label>{t('Medição a analisar', 'Measurement to analyze', 'Medición a analizar')}<select value={targetId} onChange={event => setTargetId(event.currentTarget.value)}><option value="">{t('Escolha a medição', 'Choose measurement', 'Elija medición')}</option>{signals.map(signal => <option key={signal.id} value={signal.id}>{signal.label}</option>)}</select></label>
        <label>{t('Condição medida', 'Measured condition', 'Condición medida')}<select value={conditionId} onChange={event => setConditionId(event.currentTarget.value)}><option value="">{t('Escolha a condição', 'Choose condition', 'Elija condición')}</option>{signals.filter(signal => signal.canonicalUnit && signal.id !== targetId).map(signal => <option key={signal.id} value={signal.id}>{signal.label} ({signal.canonicalUnit})</option>)}</select></label>
        <p>{t('A condição precisa de unidade controlada compatível. Digite os limites na unidade indicada, mesmo que o TAG use outra unidade de exibição. O limite inferior é incluído; o superior é excluído.', 'The condition needs a supported controlled unit. Enter bounds in the indicated unit even if the TAG has another display unit. The lower bound is included; the upper is excluded.', 'La condición necesita unidad controlada compatible. Introduzca límites en la unidad indicada aunque el TAG tenga otra unidad visible. Se incluye el inferior y se excluye el superior.')}</p>
        <label>{t('Limite inferior incluído', 'Included lower bound', 'Límite inferior incluido')} ({condition?.canonicalUnit ?? '—'})<input type="number" step="any" value={lower} onChange={event => setLower(event.currentTarget.value)} /></label>
        <label>{t('Limite superior excluído', 'Excluded upper bound', 'Límite superior excluido')} ({condition?.canonicalUnit ?? '—'})<input type="number" step="any" value={upper} onChange={event => setUpper(event.currentTarget.value)} /></label>
        {dateField(t('Início da referência (horário local)', 'Reference start (local time)', 'Inicio de referencia (hora local)'), referenceFrom, setReferenceFrom)}
        {dateField(t('Fim da referência (horário local)', 'Reference end (local time)', 'Fin de referencia (hora local)'), referenceTo, setReferenceTo)}
        {dateField(t('Início da avaliação (horário local)', 'Evaluation start (local time)', 'Inicio de evaluación (hora local)'), evaluationFrom, setEvaluationFrom)}
        {dateField(t('Fim da avaliação (horário local)', 'Evaluation end (local time)', 'Fin de evaluación (hora local)'), evaluationTo, setEvaluationTo)}
        <p>{t('A avaliação deve começar depois do fim da referência, sem extremo compartilhado. Cada período pode ter até 31 dias e não precisa ter a mesma duração. Horários locais são convertidos para UTC.', 'Evaluation must start after reference ends, with no shared endpoint. Each period may span up to 31 days; durations need not match. Local times are converted to UTC.', 'La evaluación debe comenzar después del fin de referencia, sin extremo compartido. Cada período admite hasta 31 días; no requiere igual duración. Las horas locales se convierten a UTC.')}</p>
        <button type="submit" disabled={busy || !canQuery || !projectKey || activeRevision === null || !ready}>{t('Comparar medição', 'Compare measurement', 'Comparar medición')}</button>
        {busy && <button type="button" onClick={() => { abort.current?.abort(); sequence.current++; setBusy(false); }}>{t('Cancelar comparação da medição', 'Cancel measurement comparison', 'Cancelar comparación de medición')}</button>}
      </form>
      {error && <p role="alert">{t('Não foi possível obter uma comparação válida. Confira acesso, revisão ativa, autorização de execução e dados antes de tentar novamente.', 'A valid comparison could not be obtained. Check access, active revision, execution authorization and data before trying again.', 'No se pudo obtener una comparación válida. Revise acceso, revisión activa, autorización de ejecución y datos antes de reintentar.')}</p>}
      {result && <div data-testid="intelligence-single-target-result">
        <dl><dt>{t('Mediana da referência', 'Reference median', 'Mediana de referencia')}</dt><dd>{n(result.referenceMedian)} {target?.unit}</dd>
          <dt>{t('Dispersão da referência (MAD)', 'Reference dispersion (MAD)', 'Dispersión de referencia (MAD)')}</dt><dd>{n(result.referenceMedianAbsoluteDeviation)} {target?.unit}</dd>
          <dt>{t('Pares utilizáveis: referência / avaliação', 'Usable pairs: reference / evaluation', 'Pares utilizables: referencia / evaluación')}</dt><dd>{result.referenceEvidence.qualifiedPairCount} / {result.evaluationEvidence.qualifiedPairCount}</dd>
          <dt>{t('Registros recebidos / amostras mostradas / omitidas', 'Received records / shown samples / omitted', 'Registros recibidos / muestras mostradas / omitidas')}</dt><dd>{result.totalRawObservationCount} / {result.returnedEvaluationSampleCount} / {result.omittedEvaluationSampleCount}</dd></dl>
        <p>{t('Mediana é o valor central da referência. MAD é a mediana das distâncias até esse valor; não é desvio padrão. O score robusto só existe com pelo menos 30 pares na referência e MAD positivo. Diferença bruta usa a unidade do alvo; score não tem unidade.', 'Median is the central reference value. MAD is the median distance from it, not standard deviation. Robust score requires at least 30 reference pairs and positive MAD. Raw difference uses the target unit; score is unitless.', 'Mediana es el valor central de referencia. MAD es la mediana de sus distancias, no desviación estándar. Score robusto requiere al menos 30 pares y MAD positivo. La diferencia usa la unidad del objetivo; score no tiene unidad.')}</p>
        {result.reasonCodes.map((code, index) => <p key={`${code}-${index}`}>{reason(code)}</p>)}
        <details><summary>{t('Conferir pares, rejeições e qualidade', 'Review pairs, rejections and quality', 'Revisar pares, rechazos y calidad')}</summary>
          {([['reference', result.referenceEvidence], ['evaluation', result.evaluationEvidence]] as const).map(([period, evidence]) => <section key={period}>
            <h4>{period === 'reference' ? t('Referência', 'Reference', 'Referencia') : t('Avaliação', 'Evaluation', 'Evaluación')}</h4>
            <p>{t('Pares por horário de origem / dentro / fora da faixa', 'Source-time pairs / inside / outside range', 'Pares por hora de origen / dentro / fuera del rango')}: {evidence.exactSourceTimestampPairCount} / {evidence.conditionInRangePairCount} / {evidence.conditionOutOfRangePairCount}</p>
            {([['target', evidence.target], ['condition', evidence.condition]] as const).map(([kind, signal]) => <div key={kind}>
              <strong>{kind === 'target' ? target?.label : condition?.label}</strong>
              <p>{t('Recebidas / Boas numéricas / sincronizadas / rejeitadas', 'Received / Good numeric / synchronized / rejected', 'Recibidas / Buenas numéricas / sincronizadas / rechazadas')}: {signal.rawObservationCount} / {signal.goodFiniteNumericCount} / {signal.acceptedSynchronizedSampleCount} / {signal.rejectedObservationCount}</p>
              <p>{t('Grupos ambíguos / leituras nesses grupos', 'Ambiguous groups / readings in those groups', 'Grupos ambiguos / lecturas en esos grupos')}: {signal.ambiguousSourceTimestampGroupCount} / {signal.ambiguousSourceTimestampRowCount}</p>
              {Object.entries(signal.rejectionCounts).filter(([,value]) => value > 0).map(([code,value]) => <p key={code}>{intelligenceEvidenceText(code, locale)}: {value}</p>)}
              {Object.entries(signal.qualityCounts).filter(([,value]) => value > 0).map(([quality,value]) => <p key={quality}>{t('Qualidade', 'Quality', 'Calidad')} — {({Good:t('Boa','Good','Buena'),Uncertain:t('Incerta','Uncertain','Incierta'),Bad:t('Ruim','Bad','Mala'),BadCommunication:t('Falha de comunicação','Communication failure','Fallo de comunicación'),BadConfiguration:t('Configuração inválida','Invalid configuration','Configuración no válida'),BadDevice:t('Falha do equipamento','Device failure','Fallo del dispositivo'),Stale:t('Desatualizada','Stale','Desactualizada'),Disabled:t('Desabilitada','Disabled','Deshabilitada'),Unavailable:t('Indisponível','Unavailable','No disponible'),Missing:t('Ausente','Missing','Ausente')}[quality] ?? t('Não reconhecida','Unrecognized','No reconocida'))}: {value}</p>)}
            </div>)}
          </section>)}
        </details>
        <button type="button" onClick={() => setShowSamples(value => !value)}>{showSamples ? t('Ocultar amostras', 'Hide samples', 'Ocultar muestras') : t('Ver amostras avaliadas', 'Show evaluation samples', 'Ver muestras evaluadas')}</button>
        {showSamples && <div className="intelligence-single-target__table"><table><thead><tr>{[t('Horário de origem', 'Source time', 'Hora de origen'), t('Medição', 'Measurement', 'Medición'), t('Condição', 'Condition', 'Condición'), t('Diferença bruta', 'Raw difference', 'Diferencia bruta'), t('Score robusto', 'Robust score', 'Score robusto'), t('Observação', 'Note', 'Nota')].map(label => <th key={label}>{label}</th>)}</tr></thead><tbody>{result.evaluationSamples.map((sample, index) => <tr key={index}><td>{time(sample.sourceTimestampUtc)}</td><td>{n(sample.targetValue)} {target?.unit}</td><td>{n(sample.conditionValueCanonical)} {condition?.canonicalUnit}</td><td>{n(sample.rawDifferenceFromReferenceMedian)}</td><td>{n(sample.robustScore)}</td><td>{sample.scoreUnavailableReason ? reason(sample.scoreUnavailableReason) : '—'}<br />{t('Registro do alvo', 'Target record', 'Registro del objetivo')}: {time(sample.targetObservedTimestampUtc)}<br />{t('Registro da condição', 'Condition record', 'Registro de condición')}: {time(sample.conditionObservedTimestampUtc)}</td></tr>)}</tbody></table></div>}
        <p>{t('Amostras são pareadas apenas por horários de origem idênticos com evidência explícita de sincronização. Não há aproximação, interpolação nem preenchimento. Isso não prova simultaneidade física, mesmo regime, completude, defeito ou perda de eficiência. O limite conjunto é 50 mil registros; até 500 amostras de avaliação são retornadas.', 'Samples pair only on identical source timestamps with explicit synchronization evidence. There is no nearest-time matching, interpolation or fill. This does not prove physical simultaneity, equivalent conditions, completeness, a fault or efficiency loss. The combined limit is 50,000 records; up to 500 evaluation samples are returned.', 'Las muestras se emparejan solo por horas de origen idénticas con evidencia explícita de sincronización. No hay aproximación, interpolación ni relleno. No demuestra simultaneidad física, condiciones equivalentes, integridad, fallo ni pérdida de eficiencia. Límite conjunto: 50 mil registros; hasta 500 muestras de evaluación.')}</p>
      </div>}
    </div>
  </details>;
}
