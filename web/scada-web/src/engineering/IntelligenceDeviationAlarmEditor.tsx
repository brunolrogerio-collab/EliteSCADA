import { useEffect, useState } from 'react';
import { coverageCopy, coverageErrorText, localDateTimeInput, parseWindow } from './AssetProcessIntelligenceCoveragePanel';
import type { EngineeringLocale } from './i18n';
import type { AssetProcessIntelligenceDeviationAlarmEngineering, AssetProcessIntelligenceOperatingContextProfileEngineering, TagEngineering } from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export function isCompleteDeviationAlarm(context: AssetProcessIntelligenceOperatingContextProfileEngineering): boolean {
  const alarm = context.deviationAlarm;
  return !alarm || Boolean(alarm.tagId && context.predicates.length &&
    !context.predicates.some(predicate => predicate.tagId.toLowerCase() === alarm.tagId.toLowerCase()) &&
    alarm.minimum !== null && Number.isFinite(alarm.minimum) &&
    alarm.maximum !== null && Number.isFinite(alarm.maximum) && alarm.minimum < alarm.maximum &&
    Number.isInteger(alarm.persistenceSeconds) && alarm.persistenceSeconds >= 0 && alarm.persistenceSeconds <= 300);
}

export function IntelligenceDeviationAlarmEditor({
  locale, context, targets, disabled, scopeId, projectKey, revision, onChange
}: {
  locale: EngineeringLocale;
  context: AssetProcessIntelligenceOperatingContextProfileEngineering;
  targets: TagEngineering[];
  disabled: boolean;
  scopeId?: string;
  projectKey?: string | null;
  revision?: number | null;
  onChange: (configuration: AssetProcessIntelligenceDeviationAlarmEngineering | null) => void;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const alarm = context.deviationAlarm;
  const target = targets.find(tag => tag.id === alarm?.tagId);
  const [from, setFrom] = useState(() => localDateTimeInput(new Date(Date.now() - 3600000)));
  const [to, setTo] = useState(() => localDateTimeInput(new Date()));
  const [request, setRequest] = useState<AbortController | null>(null);
  const [feedback, setFeedback] = useState<{ text: string; error?: true } | null>(null);
  const window = parseWindow(from, to).window;
  const busy = request !== null;
  const tagId = target?.id;

  useEffect(() => {
    request?.abort();
    setRequest(null);
    setFeedback(null);
    // Only invalidate on a change to the scope of the requested evidence.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tagId, scopeId, projectKey, revision, from, to, disabled]);
  useEffect(() => () => request?.abort(), [request]);

  async function suggestRange() {
    if (!alarm || !tagId || !scopeId || !projectKey || revision == null || !window || disabled || busy) return;
    const current = new AbortController();
    setRequest(current);
    setFeedback(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/query`, {
        method: 'POST', credentials: 'include', signal: current.signal,
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopeKind: 0, scopeId, tagId, window })
      });
      const result = await response.json().catch(() => null);
      if (!response.ok) throw new Error(result?.code ?? `HTTP_${response.status}`);
      if (current.signal.aborted) return;
      const summary = result?.period?.summary;
      if (result?.projectKey !== projectKey || result?.activeRevision !== revision ||
          result?.scopeId?.toLowerCase() !== scopeId.toLowerCase() ||
          result?.tagId?.toLowerCase() !== tagId.toLowerCase() ||
          Date.parse(result?.period?.window?.fromUtc) !== Date.parse(window.fromUtc) ||
          Date.parse(result?.period?.window?.toUtc) !== Date.parse(window.toUtc))
        throw new Error('INVALID_RESPONSE');
      if (!Number.isInteger(summary?.acceptedSampleCount) || summary.acceptedSampleCount < 2 ||
          !Number.isFinite(summary.minimum) || !Number.isFinite(summary.maximum) || summary.minimum >= summary.maximum) {
        setFeedback({ text: text('O período precisa de pelo menos dois valores válidos e distintos.', 'Choose a period with at least two distinct valid values.', 'Elija un período con al menos dos valores válidos distintos.'), error: true });
        return;
      }
      onChange({ ...alarm, minimum: summary.minimum, maximum: summary.maximum });
      setFeedback({ text: text(
        `Faixa preenchida com ${summary.acceptedSampleCount} leituras válidas. Revise os limites antes de salvar.`,
        `Range filled from ${summary.acceptedSampleCount} valid readings. Review it before saving.`,
        `Intervalo rellenado con ${summary.acceptedSampleCount} lecturas válidas. Revise los límites antes de guardar.`) });
    } catch (error) {
      if (!current.signal.aborted)
        setFeedback({ text: coverageErrorText(error instanceof Error ? error.message : 'NETWORK_ERROR', coverageCopy(locale)), error: true });
    } finally {
      if (!current.signal.aborted) setRequest(null);
    }
  }

  return <fieldset className="asset-process-intelligence__analysis" disabled={disabled}>
    <legend>{text('Alarme de desvio', 'Deviation alarm', 'Alarma de desviación')}</legend>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={Boolean(alarm)}
      disabled={!alarm && (!context.predicates.length || !targets.length)}
      onChange={event => onChange(event.currentTarget.checked
        ? { tagId: targets[0]?.id ?? '', minimum: null, maximum: null, persistenceSeconds: 10 }
        : null)}/>{text('Gerar alarme na área Intelligence', 'Raise an alarm in the Intelligence area', 'Crear una alarma en el área Intelligence')}</label>
    {!alarm && (!context.predicates.length || !targets.length) && <p>{text(
      'Defina uma condição e inclua outra medição numérica no perfil para monitorar.',
      'Add a condition and another numeric measurement to monitor.',
      'Añada una condición y otra medición numérica para supervisar.')}</p>}
    {alarm && <>
      <p>{text('O alarme dispara se a medição ficar fora da faixa durante a condição configurada. Publique e ative o equipamento para monitorar.',
        'The alarm triggers when the measurement stays outside the range in the configured context. Publish and activate the equipment to start monitoring.',
        'La alarma se activa si la medición queda fuera del intervalo en la condición configurada. Publique y active el equipo para supervisar.')}</p>
      <div className="asset-process-intelligence__form-grid">
        <label>{text('Medição monitorada', 'Monitored measurement', 'Medición supervisada')}
          <select value={target?.id ?? ''} onChange={event => onChange({ ...alarm, tagId: event.currentTarget.value, minimum: null, maximum: null })}>
            <option value="">{text('Escolha uma medição', 'Choose a measurement', 'Elija una medición')}</option>
            {targets.map(tag => <option key={tag.id} value={tag.id}>{tag.name}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</option>)}
          </select>
        </label>
        <label>{text('Mínimo esperado', 'Expected minimum', 'Mínimo esperado')}
          <input type="number" step="any" value={alarm.minimum ?? ''} disabled={busy}
            onChange={event => onChange({ ...alarm, minimum: Number.isNaN(event.currentTarget.valueAsNumber) ? null : event.currentTarget.valueAsNumber })}/>
        </label>
        <label>{text('Máximo esperado', 'Expected maximum', 'Máximo esperado')}
          <input type="number" step="any" value={alarm.maximum ?? ''} disabled={busy}
            onChange={event => onChange({ ...alarm, maximum: Number.isNaN(event.currentTarget.valueAsNumber) ? null : event.currentTarget.valueAsNumber })}/>
        </label>
        <label>{text('Tempo fora da faixa (s)', 'Time outside range (s)', 'Tiempo fuera del intervalo (s)')}
          <input type="number" min={0} max={300} value={Number.isFinite(alarm.persistenceSeconds) ? alarm.persistenceSeconds : ''} disabled={busy}
            onChange={event => onChange({ ...alarm, persistenceSeconds: event.currentTarget.valueAsNumber })}/>
        </label>
      </div>
      <details>
        <summary>{text('Sugerir pelo histórico', 'Suggest from history', 'Sugerir desde el histórico')}</summary>
        <p>{text('Escolha um período típico. A sugestão usa o menor e o maior valor válido da medição no período inteiro.',
          'Choose a typical period. The suggestion uses the lowest and highest valid readings over the full period.',
          'Elija un período típico. La sugerencia usa los valores válidos mínimo y máximo de todo el período.')}</p>
        <div className="asset-process-intelligence__form-grid">
          <label>{text('Início (horário local)', 'Start (local time)', 'Inicio (hora local)')}<input type="datetime-local" value={from} onChange={event => setFrom(event.currentTarget.value)}/></label>
          <label>{text('Fim (horário local)', 'End (local time)', 'Fin (hora local)')}<input type="datetime-local" value={to} onChange={event => setTo(event.currentTarget.value)}/></label>
        </div>
        <button type="button" className="secondary" onClick={() => void suggestRange()} disabled={!window || !target || !scopeId || !projectKey || revision == null || busy}>
          {busy ? text('Consultando…', 'Reading…', 'Consultando…') : text('Preencher limites', 'Fill range', 'Rellenar límites')}
        </button>
        {feedback && <p role={feedback.error ? 'alert' : 'status'}>{feedback.text}</p>}
      </details>
    </>}
  </fieldset>;
}
