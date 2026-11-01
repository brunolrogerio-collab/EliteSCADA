import React, { useEffect, useMemo, useState } from 'react';
import type { AlarmEngineering, VisualElementEngineering, VisualEngineeringPropertyValue } from '../types';
import type { EngineeringLocale } from '../i18n';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import {
  CHECK_BOX_CONFIG_PROPERTY,
  COMBO_BOX_OPTIONS_PROPERTY,
  comboBoxOptionsEngineeringValue,
  normalizeComboBoxOptions,
  readCheckBoxAlarmSuppressionConfig,
  type AlarmSuppressionDuration,
  type AlarmSuppressionReason,
  type ComboBoxOption
} from './controlVisualModel';
import './ControlConfigurationEditors.css';

type Props = Readonly<{
  element: VisualElementEngineering;
  alarms: readonly AlarmEngineering[];
  locale: EngineeringLocale;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}>;

const COPY = {
  'pt-BR': {
    checkboxTitle: 'Configuração da caixa de seleção',
    checkboxHint: 'Vincule uma fonte Boolean para indicar qualquer estado verdadeiro/falso. Para alterar o processo, use um TAG Booleano gravável; supressão de alarme é um modo opcional.',
    checkboxMode: 'Uso', booleanMode: 'Booleano do processo', suppressionMode: 'Supressão de um alarme',
    alarm: 'Alarme', chooseAlarm: 'Selecione um alarme', noAlarms: 'Nenhum alarme com identidade estável está disponível.',
    reason: 'Motivo', duration: 'Duração', untilReleased: 'Até liberar', oneHour: '1 hora', fourHours: '4 horas', eightHours: '8 horas',
    maintenance: 'Manutenção', startup: 'Partida', testing: 'Teste', other: 'Outro', save: 'Aplicar configuração',
    comboTitle: 'Opções da lista', comboHint: 'Cada opção tem o valor gravado no TAG e o texto que o operador vê. O TAG vinculado precisa ser Texto ou Enum.',
    value: 'Valor do TAG', label: 'Texto exibido', add: 'Adicionar opção', remove: 'Remover',
    empty: 'Adicione ao menos uma opção antes de aplicar.', duplicate: 'Os valores das opções precisam ser únicos.', invalid: 'Revise os valores e textos; ambos são obrigatórios e limitados a 120 caracteres.',
    saved: 'Configuração aplicada no rascunho. Use Preview e Apply para publicá-la.'
  },
  en: {
    checkboxTitle: 'CheckBox configuration',
    checkboxHint: 'Bind a Boolean source to display any true/false state. To change the process, use a writable Boolean TAG; alarm suppression is an optional mode.',
    checkboxMode: 'Use', booleanMode: 'Process Boolean', suppressionMode: 'Suppress one alarm',
    alarm: 'Alarm', chooseAlarm: 'Choose an alarm', noAlarms: 'No alarm with a stable identity is available.',
    reason: 'Reason', duration: 'Duration', untilReleased: 'Until released', oneHour: '1 hour', fourHours: '4 hours', eightHours: '8 hours',
    maintenance: 'Maintenance', startup: 'Startup', testing: 'Test', other: 'Other', save: 'Apply configuration',
    comboTitle: 'List options', comboHint: 'Each option has the value written to the TAG and the text shown to the operator. The bound TAG must be String or Enum.',
    value: 'TAG value', label: 'Display text', add: 'Add option', remove: 'Remove',
    empty: 'Add at least one option before applying.', duplicate: 'Option values must be unique.', invalid: 'Review the values and labels; both are required and limited to 120 characters.',
    saved: 'Configuration applied to the draft. Use Preview and Apply to publish it.'
  },
  es: {
    checkboxTitle: 'Configuración de la casilla',
    checkboxHint: 'Vincule una fuente Booleana para mostrar cualquier estado verdadero/falso. Para cambiar el proceso, use un TAG Booleano escribible; suprimir alarmas es un modo opcional.',
    checkboxMode: 'Uso', booleanMode: 'Booleano del proceso', suppressionMode: 'Suprimir una alarma',
    alarm: 'Alarma', chooseAlarm: 'Seleccione una alarma', noAlarms: 'No hay alarmas con identidad estable disponibles.',
    reason: 'Motivo', duration: 'Duración', untilReleased: 'Hasta liberar', oneHour: '1 hora', fourHours: '4 horas', eightHours: '8 horas',
    maintenance: 'Mantenimiento', startup: 'Arranque', testing: 'Prueba', other: 'Otro', save: 'Aplicar configuración',
    comboTitle: 'Opciones de la lista', comboHint: 'Cada opción tiene el valor guardado en el TAG y el texto que ve el operador. El TAG vinculado debe ser Texto o Enum.',
    value: 'Valor del TAG', label: 'Texto mostrado', add: 'Agregar opción', remove: 'Quitar',
    empty: 'Agregue al menos una opción antes de aplicar.', duplicate: 'Los valores deben ser únicos.', invalid: 'Revise los valores y textos; ambos son obligatorios y tienen un máximo de 120 caracteres.',
    saved: 'Configuración aplicada al borrador. Use Vista previa y Aplicar para publicarla.'
  }
} as const;

const DURATION_LABELS: Readonly<Record<EngineeringLocale, Readonly<Record<AlarmSuppressionDuration, string>>>> = {
  'pt-BR': { untilReleased: 'Até liberar', '1h': '1 hora', '4h': '4 horas', '8h': '8 horas' },
  en: { untilReleased: 'Until released', '1h': '1 hour', '4h': '4 hours', '8h': '8 hours' },
  es: { untilReleased: 'Hasta liberar', '1h': '1 hora', '4h': '4 horas', '8h': '8 horas' }
};

export function CheckBoxConfigurationEditor({ element, alarms, locale, onMutationIntent }: Props) {
  const copy = COPY[locale];
  const current = useMemo(() => {
    try { return readCheckBoxAlarmSuppressionConfig(element.properties?.[CHECK_BOX_CONFIG_PROPERTY]); }
    catch { return null; }
  }, [element.properties]);
  const [mode, setMode] = useState<'boolean' | 'alarmSuppression'>(current ? 'alarmSuppression' : 'boolean');
  const stableAlarms = useMemo(() => alarms
    .filter(alarm => Boolean(alarm.id?.trim() && alarm.name.trim()))
    .slice()
    .sort((left, right) => left.name.localeCompare(right.name, locale)), [alarms, locale]);
  const [alarmId, setAlarmId] = useState(current?.alarmDefinitionId ?? stableAlarms[0]?.id ?? '');
  const [reasonCode, setReasonCode] = useState<AlarmSuppressionReason>(current?.reasonCode ?? 'maintenance');
  const [duration, setDuration] = useState<AlarmSuppressionDuration>(current?.duration ?? 'untilReleased');
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setMode(current ? 'alarmSuppression' : 'boolean');
    setAlarmId(current?.alarmDefinitionId ?? stableAlarms[0]?.id ?? '');
    setReasonCode(current?.reasonCode ?? 'maintenance');
    setDuration(current?.duration ?? 'untilReleased');
    setSaved(false);
    setError(null);
  }, [current, stableAlarms]);

  const apply = () => {
    setError(null);
    setSaved(false);
    if (!element.id) return;
    if (mode === 'boolean') {
      if (element.properties?.[CHECK_BOX_CONFIG_PROPERTY] !== undefined) {
        onMutationIntent({ kind: 'property.remove', objectIds: [element.id], propertyKey: CHECK_BOX_CONFIG_PROPERTY });
      }
      setSaved(true);
      return;
    }
    const target = stableAlarms.find(alarm => alarm.id === alarmId);
    if (!target?.id) {
      setError(copy.noAlarms);
      return;
    }
    const config = Object.freeze({ mode: 'alarmSuppression', alarmDefinitionId: target.id, reasonCode, duration });
    try {
      onMutationIntent({ kind: 'property.set', objectIds: [element.id], propertyKey: CHECK_BOX_CONFIG_PROPERTY, value: config as VisualEngineeringPropertyValue });
      if (element.bindings?.some(binding => binding.key === 'checked')) {
        onMutationIntent({ kind: 'binding.remove', objectId: element.id, propertyKey: 'checked' });
      }
      setSaved(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    }
  };

  return <section className="visual-control-config" aria-label={copy.checkboxTitle} data-testid="visual-checkbox-configuration">
    <h3>{copy.checkboxTitle}</h3>
    <p>{copy.checkboxHint}</p>
    <label><span>{copy.checkboxMode}</span><select value={mode} onChange={event => { setMode(event.currentTarget.value as typeof mode); setSaved(false); }}>
      <option value="boolean">{copy.booleanMode}</option>
      <option value="alarmSuppression">{copy.suppressionMode}</option>
    </select></label>
    {mode === 'alarmSuppression' ? <div className="visual-control-config__grid">
      <label><span>{copy.alarm}</span><select value={alarmId} disabled={stableAlarms.length === 0} onChange={event => setAlarmId(event.currentTarget.value)}>
        <option value="">{stableAlarms.length ? copy.chooseAlarm : copy.noAlarms}</option>
        {stableAlarms.map(alarm => <option key={alarm.id} value={alarm.id}>{alarm.name}</option>)}
      </select></label>
      <label><span>{copy.reason}</span><select value={reasonCode} onChange={event => setReasonCode(event.currentTarget.value as AlarmSuppressionReason)}>
        <option value="maintenance">{copy.maintenance}</option><option value="startup">{copy.startup}</option>
        <option value="testing">{copy.testing}</option><option value="other">{copy.other}</option>
      </select></label>
      <label><span>{copy.duration}</span><select value={duration} onChange={event => setDuration(event.currentTarget.value as AlarmSuppressionDuration)}>
        {(Object.keys(DURATION_LABELS[locale]) as AlarmSuppressionDuration[]).map(value => <option key={value} value={value}>{DURATION_LABELS[locale][value]}</option>)}
      </select></label>
    </div> : null}
    {error ? <p className="visual-control-config__error" role="alert">{error}</p> : null}
    {saved ? <p className="visual-control-config__saved" role="status">{copy.saved}</p> : null}
    <button type="button" onClick={apply}>{copy.save}</button>
  </section>;
}

export function ComboBoxConfigurationEditor({ element, alarms: _alarms, locale, onMutationIntent }: Props) {
  const copy = COPY[locale];
  const [options, setOptions] = useState<ComboBoxOption[]>(() => readOptions(element.properties?.[COMBO_BOX_OPTIONS_PROPERTY]));
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setOptions(readOptions(element.properties?.[COMBO_BOX_OPTIONS_PROPERTY]));
    setError(null);
    setSaved(false);
  }, [element.id, element.properties]);

  const update = (index: number, field: keyof ComboBoxOption, value: string) => {
    setOptions(current => current.map((option, optionIndex) => optionIndex === index ? { ...option, [field]: value } : option));
    setSaved(false);
  };

  const apply = () => {
    setError(null);
    setSaved(false);
    if (!element.id) return;
    try {
      const normalized = normalizeComboBoxOptions(options);
      onMutationIntent({ kind: 'property.set', objectIds: [element.id], propertyKey: COMBO_BOX_OPTIONS_PROPERTY, value: comboBoxOptionsEngineeringValue(normalized) });
      setSaved(true);
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : String(cause);
      setError(/duplicat/i.test(message) ? copy.duplicate : /between 1 and 64|at least one/i.test(message) ? copy.empty : copy.invalid);
    }
  };

  return <section className="visual-control-config" aria-label={copy.comboTitle} data-testid="visual-combobox-configuration">
    <h3>{copy.comboTitle}</h3>
    <p>{copy.comboHint}</p>
    <div className="visual-control-config__options">
      {options.map((option, index) => <div className="visual-control-config__option" key={index}>
        <label><span>{copy.value} {index + 1}</span><input value={option.value} maxLength={120} onChange={event => update(index, 'value', event.currentTarget.value)} /></label>
        <label><span>{copy.label} {index + 1}</span><input value={option.label} maxLength={120} onChange={event => update(index, 'label', event.currentTarget.value)} /></label>
        <button type="button" aria-label={`${copy.remove} ${index + 1}`} onClick={() => { setOptions(current => current.filter((_, optionIndex) => optionIndex !== index)); setSaved(false); }}>{copy.remove}</button>
      </div>)}
    </div>
    <div className="visual-control-config__actions">
      <button type="button" onClick={() => { setOptions(current => [...current, { value: '', label: '' }]); setSaved(false); }}>{copy.add}</button>
      <button type="button" onClick={apply}>{copy.save}</button>
    </div>
    {error ? <p className="visual-control-config__error" role="alert">{error}</p> : null}
    {saved ? <p className="visual-control-config__saved" role="status">{copy.saved}</p> : null}
  </section>;
}

function readOptions(value: unknown): ComboBoxOption[] {
  try { return [...normalizeComboBoxOptions(value)]; }
  catch { return [{ value: '', label: '' }]; }
}
