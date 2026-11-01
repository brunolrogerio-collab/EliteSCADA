import React, { useEffect, useMemo, useState, type CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualElementEngineering, VisualEngineeringPropertyValue } from '../types';
import { VISUAL_PROPERTY_KEYS } from '../../visual-runtime';
import {
  releaseRuntimeAlarmSuppression,
  loadRuntimeAlarmSuppressions,
  setRuntimeAlarmSuppression
} from '../../runtime/alarmCenterApi';
import type { RuntimeAlarmSuppressionSnapshot } from '../../runtime/alarmCenterTypes';
import { visualTagSampleKey, type VisualDynamicSample } from './visualDynamicRuntime';
import {
  CHECK_BOX_CONFIG_PROPERTY,
  COMBO_BOX_OPTIONS_PROPERTY,
  readCheckBoxAlarmSuppressionConfig,
  resolveComboBoxSelection,
  suppressionExpiry,
  type ComboBoxOption
} from './controlVisualModel';
import { normalizeComboBoxOptions } from './controlVisualModel';
import type { SliderTagWrite } from './SliderVisualElement';
import './ControlVisualElements.css';

type ControlLocale = 'pt-BR' | 'en' | 'es';
type Feedback = 'idle' | 'pending' | 'accepted' | 'confirmed' | 'failed';

const COPY = {
  'pt-BR': {
    select: 'Selecione uma opção', missing: (value: string) => `Valor atual sem opção: ${value}`,
    noOptions: 'Configure pelo menos uma opção na Engenharia.', unavailable: 'Valor indisponível ou qualidade diferente de Good.',
    incompatible: 'Vincule um TAG do tipo Texto ou Enum.', pending: 'Enviando…', accepted: 'Solicitação enviada; aguardando confirmação do TAG.',
    confirmed: 'Valor confirmado pelo TAG.', invalidConfiguration: 'A configuração deste controle está inválida.',
    suppressionUnavailable: 'Estado da supressão indisponível.', suppressionInherited: (cause: string) => `Supressão herdada de ${cause}.`,
    suppressionActive: 'Supressão aplicada.', suppressionReleased: 'Supressão liberada.',
    reason: { maintenance: 'Manutenção', startup: 'Partida', testing: 'Teste', other: 'Outro' }
  },
  en: {
    select: 'Select an option', missing: (value: string) => `Current value has no option: ${value}`,
    noOptions: 'Configure at least one option in Engineering.', unavailable: 'Value unavailable or quality is not Good.',
    incompatible: 'Bind a Text or Enum TAG.', pending: 'Sending…', accepted: 'Request sent; waiting for TAG confirmation.',
    confirmed: 'TAG value confirmed.', invalidConfiguration: 'This control configuration is invalid.',
    suppressionUnavailable: 'Suppression state is unavailable.', suppressionInherited: (cause: string) => `Suppression inherited from ${cause}.`,
    suppressionActive: 'Suppression applied.', suppressionReleased: 'Suppression released.',
    reason: { maintenance: 'Maintenance', startup: 'Startup', testing: 'Test', other: 'Other' }
  },
  es: {
    select: 'Seleccione una opción', missing: (value: string) => `El valor actual no tiene opción: ${value}`,
    noOptions: 'Configure al menos una opción en Ingeniería.', unavailable: 'Valor no disponible o calidad distinta de Good.',
    incompatible: 'Vincule un TAG de tipo Texto o Enum.', pending: 'Enviando…', accepted: 'Solicitud enviada; esperando confirmación del TAG.',
    confirmed: 'Valor confirmado por el TAG.', invalidConfiguration: 'La configuración de este control no es válida.',
    suppressionUnavailable: 'Estado de supresión no disponible.', suppressionInherited: (cause: string) => `Supresión heredada de ${cause}.`,
    suppressionActive: 'Supresión aplicada.', suppressionReleased: 'Supresión liberada.',
    reason: { maintenance: 'Mantenimiento', startup: 'Arranque', testing: 'Prueba', other: 'Otro' }
  }
} as const;

export type ControlVisualElementProps = Readonly<{
  element: VisualElementEngineering;
  values: Readonly<Record<string, VisualEngineeringPropertyValue>>;
  liveSamples: ReadonlyMap<string, VisualDynamicSample>;
  style: CSSProperties;
  runtimeObjectId?: string;
  locale: EngineeringLocale;
  title?: string;
  processValueWriteEnabled: boolean;
  alarmShelveEnabled: boolean;
  onTagWrite?: SliderTagWrite;
}>;

export function CheckBoxVisualElement({
  element, values, liveSamples, style, runtimeObjectId, locale, title,
  processValueWriteEnabled, alarmShelveEnabled, onTagWrite
}: ControlVisualElementProps) {
  const lang = controlLocale(locale);
  const copy = COPY[lang];
  const binding = element.bindings?.find(candidate => candidate.key === VISUAL_PROPERTY_KEYS.checked);
  const sample = binding ? readBindingSample(binding, liveSamples) : undefined;
  const configResult = useMemo(() => {
    try {
      const config = readCheckBoxAlarmSuppressionConfig(element.properties?.[CHECK_BOX_CONFIG_PROPERTY]);
      return config ? { state: 'suppression' as const, config } : { state: 'boolean' as const };
    } catch (error) {
      return { state: 'invalid' as const, message: error instanceof Error ? error.message : String(error) };
    }
  }, [element.properties]);
  const config = configResult.state === 'suppression' ? configResult.config : null;
  const [suppressionSnapshot, setSuppressionSnapshot] = useState<RuntimeAlarmSuppressionSnapshot | null>(null);
  const [suppressionError, setSuppressionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>('idle');
  const [writeError, setWriteError] = useState<string | null>(null);
  const [requestedValue, setRequestedValue] = useState<boolean | null>(null);
  const [requestStartedAt, setRequestStartedAt] = useState(0);
  const enabled = values[VISUAL_PROPERTY_KEYS.enabled] !== false;
  const interactionEnabled = values[VISUAL_PROPERTY_KEYS.interactionEnabled] === true;
  const booleanValueGood = Boolean(sample && isGoodSample(sample) && typeof sample.value === 'boolean' &&
    sample.dataType.trim().toLowerCase() === 'boolean');
  const tagId = binding?.tagReference?.tagId?.trim() ?? '';

  useEffect(() => {
    if (!config) { setSuppressionSnapshot(null); setSuppressionError(null); return; }
    const controller = new AbortController();
    void refreshSuppression(config.alarmDefinitionId, controller.signal)
      .then(snapshot => { setSuppressionSnapshot(snapshot); setSuppressionError(null); })
      .catch(error => { if (!controller.signal.aborted) setSuppressionError(error instanceof Error ? error.message : String(error)); });
    return () => controller.abort();
  }, [config?.alarmDefinitionId]);

  useEffect(() => {
  if (feedback !== 'accepted' || requestedValue === null || !booleanValueGood || sample?.value !== requestedValue) return;
    if (Date.parse(sample.timestamp ?? '') >= requestStartedAt) setFeedback('confirmed');
  }, [feedback, requestedValue, booleanValueGood, sample?.value, sample?.timestamp, requestStartedAt]);

  const alarm = config && suppressionSnapshot?.alarms.find(item =>
    item.definitionId.toLocaleLowerCase() === config.alarmDefinitionId.toLocaleLowerCase());
  const directCause = alarm?.suppressionCauses?.find(cause =>
    cause.targetKind === 'alarm' && cause.targetId.toLocaleLowerCase() === config?.alarmDefinitionId.toLocaleLowerCase());
  const inheritedCause = alarm?.isSuppressed && !directCause ? alarm.suppressionCauses?.[0] : undefined;
  const inherited = Boolean(inheritedCause);
  const checked = configResult.state === 'suppression' ? Boolean(directCause) : booleanValueGood ? sample!.value as boolean : values[VISUAL_PROPERTY_KEYS.checked] === true;
  const tagCanWrite = configResult.state === 'boolean' && enabled && interactionEnabled && processValueWriteEnabled &&
    Boolean(onTagWrite && tagId && binding?.kind === 'Tag' && isWriteDirection(binding.direction) && booleanValueGood && sample?.readOnly !== true && !busy);
  const suppressionCanWrite = Boolean(configResult.state === 'suppression' && config && enabled && interactionEnabled && alarmShelveEnabled &&
    alarm && !inherited && !suppressionError && !busy);
  const disabled = configResult.state === 'invalid' ? true : configResult.state === 'suppression'
    ? !suppressionCanWrite
    : !tagCanWrite;
  const stateMessage = configResult.state === 'invalid'
    ? copy.invalidConfiguration
    : configResult.state === 'suppression'
      ? suppressionError || writeError || (!suppressionSnapshot ? copy.suppressionUnavailable : inheritedCause
        ? copy.suppressionInherited(inheritedCause.targetPath || inheritedCause.targetName)
        : busy || feedback === 'pending' ? copy.pending
          : feedback === 'confirmed' ? checked ? copy.suppressionActive : copy.suppressionReleased
            : null)
      : writeError || (feedback === 'pending' ? copy.pending : feedback === 'accepted' ? copy.accepted : feedback === 'confirmed' ? copy.confirmed : null) ||
        (binding && !booleanValueGood ? copy.unavailable : null);

  const toggle = async (nextChecked: boolean) => {
    if (disabled) return;
    setBusy(true);
    setWriteError(null);
    setFeedback('pending');
    try {
      if (configResult.state === 'suppression' && config) {
        const result = nextChecked
          ? await setRuntimeAlarmSuppression('alarm', config.alarmDefinitionId, config.reasonCode, suppressionExpiry(config.duration))
          : await releaseRuntimeAlarmSuppression('alarm', config.alarmDefinitionId);
        if (!result.ok) throw new Error(result.error);
        const next = await refreshSuppression(config.alarmDefinitionId);
        setSuppressionSnapshot(next);
        setSuppressionError(null);
        setFeedback('confirmed');
      } else if (onTagWrite && tagId) {
        const started = Date.now();
        setRequestedValue(nextChecked);
        setRequestStartedAt(started);
        await onTagWrite(tagId, nextChecked);
        setFeedback('accepted');
      }
    } catch (error) {
      setWriteError(error instanceof Error ? error.message : String(error));
      setFeedback('failed');
    } finally {
      setBusy(false);
    }
  };

  const descriptionId = `checkbox-status-${safeId(element.id ?? element.key)}`;
  return <div className="visual-control visual-control--checkbox" style={style}
    data-object-id={element.id ?? undefined} data-runtime-object-id={runtimeObjectId}
    data-control-kind="checkBox" data-control-state={busy ? 'pending' : configResult.state === 'invalid' ? 'invalid' : inherited ? 'inherited' : binding && !booleanValueGood ? 'unavailable' : feedback}
    title={[title, stateMessage].filter(Boolean).join('\n') || undefined}>
    <label className="visual-control__checkbox-label">
      <input
        type="checkbox"
        checked={checked}
        ref={input => { if (input) input.indeterminate = inherited || Boolean(binding && !booleanValueGood); }}
        aria-label={typeof values[VISUAL_PROPERTY_KEYS.text] === 'string' ? values[VISUAL_PROPERTY_KEYS.text] as string : element.key}
        aria-checked={inherited || Boolean(binding && !booleanValueGood) ? 'mixed' : checked}
        aria-invalid={Boolean(binding && !booleanValueGood)}
        aria-describedby={stateMessage ? descriptionId : undefined}
        disabled={!enabled || disabled}
        data-quality={configResult.state === 'suppression' ? suppressionError ? 'unavailable' : alarm ? inherited ? 'inherited' : 'good' : 'unavailable' : configResult.state === 'invalid' ? 'unavailable' : sample?.quality ?? 'unavailable'}
        onChange={event => { void toggle(event.currentTarget.checked); }}
      />
      <span>{typeof values[VISUAL_PROPERTY_KEYS.text] === 'string' ? values[VISUAL_PROPERTY_KEYS.text] as string : element.key}</span>
    </label>
    {stateMessage ? <span id={descriptionId} className={feedback === 'failed' || suppressionError ? 'visual-control__status visual-control__status--error' : 'visual-control__status'} role={suppressionError || feedback === 'failed' ? 'alert' : 'status'}>{stateMessage}</span> : null}
    {config && alarm?.isSuppressed && directCause ? <small className="visual-control__status">{copy.reason[directCause.reasonCode as keyof typeof copy.reason] ?? directCause.reasonCode}</small> : null}
  </div>;
}

export function ComboBoxVisualElement({
  element, values, liveSamples, style, runtimeObjectId, locale, title,
  processValueWriteEnabled, onTagWrite
}: ControlVisualElementProps) {
  const lang = controlLocale(locale);
  const copy = COPY[lang];
  const binding = element.bindings?.find(candidate => candidate.key === VISUAL_PROPERTY_KEYS.text);
  const sample = binding ? readBindingSample(binding, liveSamples) : undefined;
  const rawOptions = element.properties?.[COMBO_BOX_OPTIONS_PROPERTY];
  const selection = resolveComboBoxSelection(rawOptions, sample);
  let options: readonly ComboBoxOption[] = Object.freeze([]);
  try { options = normalizeComboBoxOptions(rawOptions); } catch { /* The explicit state below explains an invalid list. */ }
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>('idle');
  const [writeError, setWriteError] = useState<string | null>(null);
  const [requestedValue, setRequestedValue] = useState<string | null>(null);
  const [requestStartedAt, setRequestStartedAt] = useState(0);
  const enabled = values[VISUAL_PROPERTY_KEYS.enabled] !== false;
  const interactionEnabled = values[VISUAL_PROPERTY_KEYS.interactionEnabled] === true;
  const tagId = binding?.tagReference?.tagId?.trim() ?? '';
  const writable = Boolean(onTagWrite && tagId && binding && isWriteDirection(binding.direction) && sample &&
    isGoodSample(sample) && sample.readOnly !== true && (sample.dataType.trim().toLowerCase() === 'string' || sample.dataType.trim().toLowerCase() === 'enum') &&
    typeof sample.value === 'string' && selection.state === 'selected');

  useEffect(() => {
    if (feedback === 'accepted' && requestedValue !== null && sample && isGoodSample(sample) &&
        sample.value === requestedValue && Date.parse(sample.timestamp ?? '') >= requestStartedAt) setFeedback('confirmed');
  }, [feedback, requestedValue, sample?.value, sample?.quality, sample?.timestamp, requestStartedAt]);

  const status = writeError || (selection.state === 'unconfigured' ? copy.noOptions :
    selection.state === 'unavailable' ? copy.unavailable :
    selection.state === 'incompatible' ? copy.incompatible :
    selection.state === 'missing-option' ? copy.missing(selection.value) :
    feedback === 'pending' || busy ? copy.pending : feedback === 'accepted' ? copy.accepted : feedback === 'confirmed' ? copy.confirmed : null);
  const selectValue = selection.state === 'selected' ? selection.value :
    selection.state === 'missing-option' ? `__missing__:${selection.value}` : '';
  const disabled = !enabled || !interactionEnabled || !processValueWriteEnabled || !writable || busy;
  const descriptionId = `combobox-status-${safeId(element.id ?? element.key)}`;

  const choose = async (nextValue: string) => {
    if (disabled || !onTagWrite || !tagId || !options.some(option => option.value === nextValue)) return;
    const started = Date.now();
    setBusy(true); setWriteError(null); setFeedback('pending'); setRequestedValue(nextValue); setRequestStartedAt(started);
    try { await onTagWrite(tagId, nextValue); setFeedback('accepted'); }
    catch (error) { setWriteError(error instanceof Error ? error.message : String(error)); setFeedback('failed'); }
    finally { setBusy(false); }
  };

  return <div className="visual-control visual-control--combobox" style={style}
    data-object-id={element.id ?? undefined} data-runtime-object-id={runtimeObjectId}
    data-control-kind="comboBox" data-control-state={busy ? 'pending' : selection.state}
    title={[title, status].filter(Boolean).join('\n') || undefined}>
    <select
      aria-label={title || element.key}
      aria-describedby={status ? descriptionId : undefined}
      aria-invalid={selection.state === 'missing-option' || selection.state === 'incompatible'}
      disabled={disabled}
      value={selectValue}
      data-quality={sample?.quality ?? 'unavailable'}
      onChange={event => { void choose(event.currentTarget.value); }}
    >
      {selection.state === 'missing-option' ? <option value={selectValue} disabled>{copy.missing(selection.value)}</option> : null}
      {options.length === 0 ? <option value="">{copy.select}</option> : null}
      {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>
    {status ? <span id={descriptionId} className={feedback === 'failed' || writeError ? 'visual-control__status visual-control__status--error' : 'visual-control__status'} role={feedback === 'failed' || writeError ? 'alert' : 'status'}>{status}</span> : null}
  </div>;
}

function readBindingSample(
  binding: NonNullable<VisualElementEngineering['bindings']>[number],
  samples: ReadonlyMap<string, VisualDynamicSample>
): VisualDynamicSample | undefined {
  const id = binding.tagReference?.tagId?.trim();
  return (id ? samples.get(visualTagSampleKey(id)) : undefined) ?? samples.get(binding.target);
}

function isGoodSample(sample: VisualDynamicSample): boolean {
  return sample.state == null && (sample.quality === 0 || String(sample.quality).toLowerCase() === 'good');
}

function isWriteDirection(direction: string | null | undefined): boolean {
  const value = direction?.trim().toLowerCase();
  return value === 'write' || value === 'readwrite' || value === 'inputoutput';
}

function controlLocale(locale: EngineeringLocale): ControlLocale {
  return locale === 'en' || locale === 'es' ? locale : 'pt-BR';
}

function safeId(value: string): string {
  return value.replace(/[^a-zA-Z0-9_-]/g, '-');
}

async function refreshSuppression(alarmDefinitionId: string, signal?: AbortSignal): Promise<RuntimeAlarmSuppressionSnapshot> {
  const result = await loadRuntimeAlarmSuppressions(signal);
  if (!result.available) throw new Error(result.error);
  const target = result.value.alarms.find(item => item.definitionId.toLocaleLowerCase() === alarmDefinitionId.toLocaleLowerCase());
  if (!target) throw new Error('The selected alarm is not available in the active Runtime.');
  return result.value;
}
