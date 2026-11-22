import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthGate';
import type { EngineeringLocale } from './i18n';
import type { IntelligenceCurrentFrequencySignal } from './intelligenceCurrentFrequencySignals';
import { coverageCopy, coverageErrorText, localDateTimeInput, parseWindow, type WindowValue } from './AssetProcessIntelligenceCoveragePanel';
import { matchesIntelligenceWindow } from './IntelligenceComparisonPeriod';
import { intelligenceEvidenceText } from './intelligenceEvidenceText';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type SignalEvidence = { rawObservationCount: number; acceptedSynchronizedSampleCount: number; rejectedObservationCount: number; rejectionCounts: Record<string, number> };
type Evidence = { window: WindowValue; current: SignalEvidence; frequency: SignalEvidence; exactSourceTimestampPairCount: number;
  frequencyInRangePairCount: number; frequencyOutOfRangePairCount: number; selectedPairCount: number; reusedSourceTimestampPairCount: number;
  unmatchedCurrentAcceptedSampleCount: number; unmatchedFrequencyAcceptedSampleCount: number };
type ResponseSignal = { signalId: string; tagId: string; roleCode: string; currentBasis: string | null; signalKind: string | null;
  frequencyReference: string | null; controlledUnitCode: string; canonicalUnit: string };
type ComparisonResult = { contractVersion: number; catalogVersion: number; ruleId: string; ruleVersion: string; projectKey: string; activeRevision: number;
  scopeKind: string | number; scopeId: string; assignmentId: string; equipmentId: string; profileId: string;
  currentSignal: ResponseSignal; frequencySignal: ResponseSignal; minimumFrequencyHz: number; maximumFrequencyHz: number;
  primary: Evidence; comparison: Evidence; totalRawObservationCount: number;
  metric: { status: string; reasonCodes: string[]; primaryMeanCurrentA: number | null; comparisonMeanCurrentA: number | null;
    differenceA: number | null; percentChange: number | null; percentChangeStatus: string; percentChangeReasonCode: string | null };
  provenance: { dataset: string; datasetContractVersion: number; physicalSimultaneity: string; operatingRegimeEquivalence: string; expectedCadence: string; completeness: string } };
const count = (value: unknown) => Number.isInteger(value) && Number(value) >= 0 && Number(value) <= 50000;
function validEvidence(value: Evidence | undefined, window: WindowValue): boolean {
  return !!value && matchesIntelligenceWindow(value.window, window) &&
    [value.exactSourceTimestampPairCount, value.frequencyInRangePairCount, value.frequencyOutOfRangePairCount, value.selectedPairCount,
      value.reusedSourceTimestampPairCount, value.unmatchedCurrentAcceptedSampleCount, value.unmatchedFrequencyAcceptedSampleCount].every(count) &&
    [value.current, value.frequency].every(signal => !!signal &&
      [signal.rawObservationCount, signal.acceptedSynchronizedSampleCount, signal.rejectedObservationCount].every(count) &&
      !!signal.rejectionCounts && typeof signal.rejectionCounts === 'object' && !Array.isArray(signal.rejectionCounts) &&
      Object.entries(signal.rejectionCounts).length <= 32 && Object.values(signal.rejectionCounts).every(count));
}
const sameId = (value: unknown, expected: string) => typeof value === 'string' && value.toLowerCase() === expected.toLowerCase();

export function IntelligenceCurrentFrequencyPanel({ locale, scopeId, assignmentId, profileId, signals, projectKey, activeRevision, canQuery, runtimeView = false }: {
  locale: EngineeringLocale; scopeId: string; assignmentId: string; profileId: string; signals: readonly IntelligenceCurrentFrequencySignal[];
  projectKey: string | null; activeRevision: number | null; canQuery: boolean; runtimeView?: boolean;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const { profile: session } = useAuth();
  const sessionKey = session ? JSON.stringify([session.subjectId, session.identityProvider, session.expiresAtUtc, [...session.roles].sort()]) : 'anonymous';
  const [currentId, setCurrentId] = useState(''), [frequencyId, setFrequencyId] = useState('');
  const [from, setFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 86400000)));
  const [to, setTo] = useState(() => localDateTimeInput(new Date()));
  const [comparisonFrom, setComparisonFrom] = useState(''), [comparisonTo, setComparisonTo] = useState('');
  const [minimum, setMinimum] = useState(''), [maximum, setMaximum] = useState('');
  const [result, setResult] = useState<ComparisonResult | null>(null), [error, setError] = useState<string | null>(null), [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null), sequence = useRef(0);
  const current = signals.find(signal => signal.id === currentId && signal.kind === 'current');
  const frequency = signals.find(signal => signal.id === frequencyId && signal.kind === 'frequency');
  const primary = parseWindow(from, to).window, comparison = parseWindow(comparisonFrom, comparisonTo).window;
  const periodsValid = !!primary && !!comparison && Date.parse(primary.toUtc) - Date.parse(primary.fromUtc) === Date.parse(comparison.toUtc) - Date.parse(comparison.fromUtc) &&
    (Date.parse(primary.toUtc) < Date.parse(comparison.fromUtc) || Date.parse(comparison.toUtc) < Date.parse(primary.fromUtc));
  const rangeValid = minimum.trim() !== '' && maximum.trim() !== '' && Number.isFinite(Number(minimum)) && Number.isFinite(Number(maximum)) && Number(minimum) <= Number(maximum);
  const ready = !!current && !!frequency && current.tagId.toLowerCase() !== frequency.tagId.toLowerCase() && periodsValid && rangeValid;
  const signalsKey = JSON.stringify(signals);
  useEffect(() => {
    controller.current?.abort(); sequence.current++; setResult(null); setError(null); setBusy(false);
    return () => { controller.current?.abort(); sequence.current++; };
  }, [scopeId, assignmentId, profileId, projectKey, activeRevision, canQuery, sessionKey, signalsKey, currentId, frequencyId, from, to, comparisonFrom, comparisonTo, minimum, maximum]);
  async function query(event: FormEvent) {
    event.preventDefault();
    if (!canQuery || !projectKey || activeRevision === null || !ready || !primary || !comparison || !current || !frequency) return;
    controller.current?.abort(); const abort = new AbortController(); controller.current = abort; const request = ++sequence.current;
    setBusy(true); setResult(null); setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/query`, {
        method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json', accept: 'application/json' }, signal: abort.signal,
        body: JSON.stringify({ scopeKind: 0, scopeId, assignmentId, currentSignalId: current.id, frequencySignalId: frequency.id,
          primaryWindow: primary, comparisonWindow: comparison, minimumFrequencyHz: Number(minimum), maximumFrequencyHz: Number(maximum) })
      });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error([401,403].includes(response.status) ? `HTTP_${response.status}` : typeof payload?.code === 'string' ? payload.code : `HTTP_${response.status}`);
      const data = payload as ComparisonResult | null;
      const signalMatches = (signal: ResponseSignal | undefined, expected: IntelligenceCurrentFrequencySignal) => !!signal && sameId(signal.signalId, expected.id) && sameId(signal.tagId, expected.tagId) &&
        (expected.kind === 'current' ? signal.roleCode === 'pump.electrical_current' && signal.currentBasis === expected.basis && signal.signalKind == null && signal.frequencyReference == null && signal.controlledUnitCode === 'unit.electric_current.ampere' && signal.canonicalUnit === 'A'
          : signal.roleCode === 'pump.drive_frequency' && signal.frequencyReference === expected.basis && signal.signalKind === 'measured_output' && signal.currentBasis == null && signal.controlledUnitCode === 'unit.frequency.hertz' && signal.canonicalUnit === 'Hz');
      const metric = data?.metric, provenance = data?.provenance;
      if (!data || data.contractVersion !== 1 || data.catalogVersion !== 1 || data.ruleId !== 'current-at-recorded-frequency.compare.v1' || data.ruleVersion !== '1.0.0' ||
          data.projectKey !== projectKey || data.activeRevision !== activeRevision || ![0, 'Equipment'].includes(data.scopeKind) ||
          !sameId(data.scopeId, scopeId) || !sameId(data.equipmentId, scopeId) || !sameId(data.assignmentId, assignmentId) || !sameId(data.profileId, profileId) ||
          !signalMatches(data.currentSignal, current) || !signalMatches(data.frequencySignal, frequency) ||
          data.minimumFrequencyHz !== Number(minimum) || data.maximumFrequencyHz !== Number(maximum) ||
          !validEvidence(data.primary, primary) || !validEvidence(data.comparison, comparison) || !count(data.totalRawObservationCount) ||
          !metric || !['available','insufficient_data','unsupported'].includes(metric.status) ||
          !Array.isArray(metric.reasonCodes) || metric.reasonCodes.length > 32 || !metric.reasonCodes.every(reason => typeof reason === 'string') ||
          ![metric.primaryMeanCurrentA, metric.comparisonMeanCurrentA, metric.differenceA, metric.percentChange].every(value => value === null || typeof value === 'number' && Number.isFinite(value)) ||
          !['available','unavailable','unsupported'].includes(metric.percentChangeStatus) || !(metric.percentChangeReasonCode === null || typeof metric.percentChangeReasonCode === 'string') ||
          (metric.status === 'available' && (metric.primaryMeanCurrentA === null || metric.comparisonMeanCurrentA === null || metric.differenceA === null)) ||
          (metric.status !== 'available' && (metric.differenceA !== null || metric.percentChange !== null)) ||
          (metric.percentChangeStatus === 'available' ? metric.percentChange === null : metric.percentChange !== null) ||
          !provenance || provenance.dataset !== 'historian.samples.v2' || provenance.datasetContractVersion !== 2 ||
          typeof provenance.physicalSimultaneity !== 'string' || !provenance.physicalSimultaneity.startsWith('unverified;') ||
          typeof provenance.operatingRegimeEquivalence !== 'string' || !provenance.operatingRegimeEquivalence.startsWith('unverified;') ||
          provenance.expectedCadence !== 'unknown' || provenance.completeness !== 'unknown') throw new Error('INVALID_RESPONSE');
      if (!abort.signal.aborted && sequence.current === request) setResult(data);
    } catch (cause) {
      if (!abort.signal.aborted && sequence.current === request) {
        const code = cause instanceof Error ? cause.message : 'NETWORK_ERROR';
        setError(code === 'INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE'
          ? text('A consulta ultrapassou 50.000 leituras brutas no conjunto dos dois sinais e períodos. Reduza os períodos e tente novamente.', 'The query exceeded 50,000 raw readings across both signals and periods. Shorten the periods and try again.', 'La consulta superó 50.000 lecturas brutas entre ambas señales y períodos. Reduzca los períodos e inténtelo de nuevo.')
          : code === 'INTELLIGENCE_ANALYTICS_SOURCE_NOT_AVAILABLE'
            ? text('Uma das medições não está disponível na fonte ativa do Runtime. Confira a fonte de dados e a revisão em operação.', 'One measurement is unavailable in the active Runtime source. Check the data source and running revision.', 'Una medición no está disponible en la fuente activa del Runtime. Revise la fuente de datos y la revisión en operación.')
            : ['INTELLIGENCE_CURRENT_FREQUENCY_SEMANTICS_UNSUPPORTED','INTELLIGENCE_CURRENT_FREQUENCY_UNITS_UNSUPPORTED'].includes(code)
              ? text('Confira no perfil o significado das medições e as unidades controladas em ampere e hertz. Esta combinação não é compatível com a comparação.', 'Check the profile measurement meanings and controlled ampere and hertz units. This combination is not supported by the comparison.', 'Revise en el perfil los significados y las unidades controladas en amperios y hercios. Esta combinación no es compatible con la comparación.')
              : ['HTTP_401','HTTP_403'].includes(code)
                ? text('Seu acesso não permite consultar estas medições. Confira o login e as permissões de tendência e leitura dos TAGs.', 'Your access does not allow querying these measurements. Check your login, trend permission and TAG read permissions.', 'Su acceso no permite consultar estas mediciones. Revise el inicio de sesión y los permisos de tendencia y lectura de los TAGs.')
                : code === 'INTELLIGENCE_EXECUTION_PERMIT_CANCELLED'
                  ? text('A execução foi revogada durante a consulta. Confira a ativação antes de tentar novamente.', 'Execution was revoked during the query. Check activation before trying again.', 'Se revocó la ejecución durante la consulta. Revise la activación antes de intentarlo de nuevo.')
                  : coverageErrorText(code, coverageCopy(locale)));
      }
    } finally { if (sequence.current === request) setBusy(false); }
  }
  const basisLabel = (basis: string) => ({
    line_rms: text('Corrente de linha RMS', 'RMS line current', 'Corriente de línea RMS'),
    phase_rms: text('Corrente de fase RMS', 'RMS phase current', 'Corriente de fase RMS'),
    dc_bus: text('Corrente do barramento CC', 'DC bus current', 'Corriente del bus CC'),
    drive_electrical: text('Frequência elétrica medida do inversor', 'Measured drive electrical frequency', 'Frecuencia eléctrica medida del variador'),
    shaft_rotational: text('Frequência de rotação medida do eixo', 'Measured shaft rotational frequency', 'Frecuencia de giro medida del eje')
  }[basis] ?? '');
  const number = (value: number | null, unit: string) => value === null ? text('Não disponível', 'Unavailable', 'No disponible') : `${value.toLocaleString(locale, { maximumFractionDigits: 2 })} ${unit}`;
  const evidence = (value: Evidence, label: string) => <article>
    <h4>{label}</h4>
    <p>{text('Pares utilizados na média', 'Pairs used in the mean', 'Pares utilizados en la media')}: {value.selectedPairCount}</p>
    <p>{text('Pares no mesmo instante de origem / fora da faixa', 'Pairs at the same source time / outside range', 'Pares en el mismo instante de origen / fuera del rango')}: {value.exactSourceTimestampPairCount} / {value.frequencyOutOfRangePairCount}</p>
    <p>{text('Leituras sem par: corrente / frequência', 'Unpaired readings: current / frequency', 'Lecturas sin par: corriente / frecuencia')}: {value.unmatchedCurrentAcceptedSampleCount} / {value.unmatchedFrequencyAcceptedSampleCount}</p>
    <p>{text('Pares descartados por horário reutilizado entre períodos', 'Pairs discarded for source time reused across periods', 'Pares descartados por hora de origen reutilizada entre períodos')}: {value.reusedSourceTimestampPairCount}</p>
    {(['current','frequency'] as const).map(kind => <section key={kind}><strong>{kind === 'current' ? text('Corrente', 'Current', 'Corriente') : text('Frequência', 'Frequency', 'Frecuencia')}</strong>
      <p>{text('Recebidas / aceitas com origem sincronizada / rejeitadas', 'Received / accepted with synchronized source / rejected', 'Recibidas / aceptadas con origen sincronizado / rechazadas')}: {value[kind].rawObservationCount} / {value[kind].acceptedSynchronizedSampleCount} / {value[kind].rejectedObservationCount}</p>
      {Object.entries(value[kind].rejectionCounts).filter(([, total]) => total > 0).map(([reason,total]) => <p key={reason}>{intelligenceEvidenceText(reason, locale)}: {total}</p>)}
    </section>)}
  </article>;
  return <details open={runtimeView || undefined} className="asset-process-intelligence__coverage asset-process-intelligence__analysis" data-testid={`intelligence-current-frequency-${assignmentId}`}>
    <summary>{text('Comparar corrente em frequência semelhante', 'Compare current at similar frequency', 'Comparar corriente a frecuencia similar')}</summary>
    <p>{text('A corrente aumentou quando o equipamento trabalhava na mesma faixa de frequência? Escolha as medições, dois períodos de mesma duração e a faixa em Hz. O servidor compara somente os pares de leituras que atendem a essa faixa.',
      'Did current increase while the equipment was operating in the same frequency range? Choose the measurements, two equal-duration periods and a range in Hz. The server compares only reading pairs within that range.',
      '¿Aumentó la corriente mientras el equipo trabajaba en el mismo rango de frecuencia? Elija las mediciones, dos períodos de igual duración y el rango en Hz. El servidor compara solo los pares de lecturas dentro de ese rango.')}</p>
    {(!signals.some(signal => signal.kind === 'current') || !signals.some(signal => signal.kind === 'frequency')) && <p role="note">{text('Este perfil precisa de corrente em ampere e frequência medida em hertz, com unidades e significados controlados. Na edição do perfil, informe também o tipo de corrente e se a frequência é elétrica do inversor ou de rotação do eixo. Um comando de frequência ou uma unidade escrita apenas como texto não serve para esta comparação.',
      'This profile needs current in amperes and measured frequency in hertz, with controlled units and meanings. In the profile editor, also specify the current basis and whether frequency is electrical drive output or shaft rotation. A frequency command or a free-text unit cannot be used for this comparison.',
      'Este perfil necesita corriente en amperios y frecuencia medida en hercios, con unidades y significados controlados. En el editor del perfil indique también el tipo de corriente y si la frecuencia es eléctrica del variador o de giro del eje. Un comando de frecuencia o una unidad escrita solo como texto no sirve para esta comparación.')}</p>}
    {!canQuery && <p role="note">{runtimeView ? text('Consulte no Runtime ao vivo. No editor e na reprodução histórica, esta consulta fica desabilitada.', 'Query in live Runtime. This query is disabled in the editor and historical playback.', 'Consulte en Runtime en vivo. Esta consulta está deshabilitada en el editor y reproducción histórica.') : text('Publique as alterações, ative este equipamento e confira as permissões de consulta antes de continuar.', 'Publish changes, activate this equipment and check query permissions before continuing.', 'Publique los cambios, active este equipo y revise los permisos de consulta antes de continuar.')}</p>}
    <form onSubmit={event => void query(event)} className="asset-process-intelligence__coverage-form">
      <label>{text('Medição de corrente', 'Current measurement', 'Medición de corriente')}<select disabled={!canQuery} value={currentId} onChange={event => setCurrentId(event.currentTarget.value)}>
        <option value="">{text('Escolha a corrente', 'Choose current', 'Elija la corriente')}</option>{signals.filter(signal => signal.kind === 'current').map(signal => <option key={signal.id} value={signal.id}>{signal.label} · {basisLabel(signal.basis)} (A)</option>)}
      </select></label>
      {current && <p>{basisLabel(current.basis)} (A).</p>}
      <label>{text('Medição de frequência', 'Frequency measurement', 'Medición de frecuencia')}<select disabled={!canQuery} value={frequencyId} onChange={event => setFrequencyId(event.currentTarget.value)}>
        <option value="">{text('Escolha a frequência', 'Choose frequency', 'Elija la frecuencia')}</option>{signals.filter(signal => signal.kind === 'frequency').map(signal => <option key={signal.id} value={signal.id}>{signal.label} · {basisLabel(signal.basis)} (Hz)</option>)}
      </select></label>
      {frequency && <p>{basisLabel(frequency.basis)}. {frequency.basis === 'drive_electrical' ? text('Essa frequência não informa diretamente a rotação do eixo.', 'This frequency does not directly provide shaft speed.', 'Esta frecuencia no indica directamente la velocidad del eje.') : text('A unidade é Hz, não rpm. O sistema não presume a relação com a frequência elétrica.', 'The unit is Hz, not rpm. No relationship with electrical frequency is assumed.', 'La unidad es Hz, no rpm. No se supone relación con la frecuencia eléctrica.')}</p>}
      <label>{text('Frequência mínima (Hz)', 'Minimum frequency (Hz)', 'Frecuencia mínima (Hz)')}<input type="number" step="any" value={minimum} disabled={!canQuery} onChange={event => setMinimum(event.currentTarget.value)}/></label>
      <label>{text('Frequência máxima (Hz)', 'Maximum frequency (Hz)', 'Frecuencia máxima (Hz)')}<input type="number" step="any" value={maximum} disabled={!canQuery} onChange={event => setMaximum(event.currentTarget.value)}/></label>
      <p>{text('Os dois limites são incluídos. Por exemplo, 49 a 51 Hz. A mesma faixa será usada nos dois períodos.', 'Both limits are included. For example, 49 to 51 Hz. The same range is used in both periods.', 'Se incluyen ambos límites. Por ejemplo, 49 a 51 Hz. Se usa el mismo rango en los dos períodos.')}</p>
      <label>{text('Início do período principal (horário local)', 'Primary period start (local time)', 'Inicio del período principal (hora local)')}<input type="datetime-local" value={from} disabled={!canQuery} onChange={event => setFrom(event.currentTarget.value)}/></label>
      <label>{text('Fim do período principal (horário local)', 'Primary period end (local time)', 'Fin del período principal (hora local)')}<input type="datetime-local" value={to} disabled={!canQuery} onChange={event => setTo(event.currentTarget.value)}/></label>
      <label>{text('Início do período de comparação (horário local)', 'Comparison period start (local time)', 'Inicio del período de comparación (hora local)')}<input type="datetime-local" value={comparisonFrom} disabled={!canQuery} onChange={event => setComparisonFrom(event.currentTarget.value)}/></label>
      <label>{text('Fim do período de comparação (horário local)', 'Comparison period end (local time)', 'Fin del período de comparación (hora local)')}<input type="datetime-local" value={comparisonTo} disabled={!canQuery} onChange={event => setComparisonTo(event.currentTarget.value)}/></label>
      <p>{text('Use períodos de mesma duração, com até 31 dias cada, que não se toquem nem se sobreponham. A comparação pode vir antes ou depois do período principal. Os horários locais serão convertidos para UTC.', 'Use equal-duration periods of up to 31 days each, without touching or overlap. Comparison may precede or follow the primary period. Local times are converted to UTC.', 'Use períodos de igual duración, de hasta 31 días cada uno, sin extremos compartidos ni superposición. La comparación puede ser anterior o posterior al principal. Las horas locales se convierten a UTC.')}</p>
      {!periodsValid && <p role="note">{text('Confira a duração e a separação dos dois períodos.', 'Check both period durations and their separation.', 'Revise la duración y separación de los dos períodos.')}</p>}
      <button type="submit" disabled={!canQuery || !ready || busy}>{text('Comparar corrente', 'Compare current', 'Comparar corriente')}</button>
      {busy && <button type="button" onClick={() => { controller.current?.abort(); sequence.current++; setBusy(false); }}>{text('Cancelar consulta', 'Cancel query', 'Cancelar consulta')}</button>}
    </form>
    {error && <p role="alert">{error}</p>}
    {result && <div className="asset-process-intelligence__coverage-result" data-testid="intelligence-current-frequency-result">
      <h4>{result.metric.status === 'available' ? text('Comparação disponível', 'Comparison available', 'Comparación disponible') : text('Não há comparação disponível para estes dados', 'No comparison is available for these data', 'No hay comparación disponible para estos datos')}</h4>
      <p>{current?.label} · {basisLabel(current?.basis ?? '')}; {frequency?.label} · {basisLabel(frequency?.basis ?? '')}</p>
      <dl><dt>{text('Corrente média — principal', 'Mean current — primary', 'Corriente media — principal')}</dt><dd>{number(result.metric.primaryMeanCurrentA, 'A')}</dd>
        <dt>{text('Corrente média — comparação', 'Mean current — comparison', 'Corriente media — comparación')}</dt><dd>{number(result.metric.comparisonMeanCurrentA, 'A')}</dd>
        <dt>{text('Diferença (principal menos comparação)', 'Difference (primary minus comparison)', 'Diferencia (principal menos comparación)')}</dt><dd>{number(result.metric.differenceA, 'A')}</dd>
        <dt>{text('Variação em relação à comparação', 'Change relative to comparison', 'Variación respecto a la comparación')}</dt><dd>{number(result.metric.percentChange, '%')}</dd></dl>
      {[...new Set([...result.metric.reasonCodes, ...(result.metric.percentChangeReasonCode?.split(';') ?? [])])].map(reason => <p key={reason}>{intelligenceEvidenceText(reason, locale)}</p>)}
      <div className="asset-process-intelligence__coverage-signals">{evidence(result.primary, text('Período principal', 'Primary period', 'Período principal'))}{evidence(result.comparison, text('Período de comparação', 'Comparison period', 'Período de comparación'))}</div>
      <p>{text('São médias das amostras aceitas, não médias ponderadas pelo tempo. Cada período precisa de pelo menos dois pares com horário de origem sincronizado e exatamente igual entre as duas medições. Leituras ausentes não são preenchidas.', 'These are means of accepted samples, not time-weighted means. Each period needs at least two pairs with synchronized, exactly matching source times between the two measurements. Missing readings are not filled.', 'Son medias de muestras aceptadas, no ponderadas por tiempo. Cada período necesita al menos dos pares con horas de origen sincronizadas y exactamente iguales entre las dos mediciones. No se rellenan lecturas ausentes.')}</p>
    </div>}
    <p>{text('Frequência semelhante não garante a mesma carga mecânica nem o mesmo regime de processo. Uma variação de corrente ajuda a investigar o comportamento, mas não comprova perda de eficiência ou defeito. A simultaneidade física e a completude dos dados não estão comprovadas.', 'Similar frequency does not guarantee equal mechanical load or process conditions. A current change helps investigate behavior, but does not prove efficiency loss or a fault. Physical simultaneity and data completeness are not established.', 'Una frecuencia similar no garantiza la misma carga mecánica ni las mismas condiciones del proceso. Un cambio de corriente ayuda a investigar el comportamiento, pero no demuestra pérdida de eficiencia ni un fallo. No se han comprobado la simultaneidad física ni la integridad de los datos.')}</p>
  </details>;
}
