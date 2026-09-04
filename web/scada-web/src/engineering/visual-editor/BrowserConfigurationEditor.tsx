import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualElementEngineering } from '../types';
import {
  ALARM_BROWSER_COLUMNS,
  BROWSER_CONFIG_PROPERTY,
  BUILTIN_VISUAL_OBJECT_TYPES,
  EVENT_BROWSER_COLUMNS,
  readAlarmBrowserConfig,
  readEventBrowserConfig,
  type AlarmBrowserColumn,
  type AlarmBrowserConfig,
  type EventBrowserColumn,
  type EventBrowserConfig
} from '../../visual-runtime';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import './BrowserConfigurationEditor.css';

type BrowserConfigurationEditorProps = Readonly<{
  element: VisualElementEngineering;
  locale: EngineeringLocale;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}>;

type Copy = Readonly<{
  title: string;
  mode: string;
  current: string;
  history: string;
  lifecycle: string;
  all: string;
  active: string;
  returned: string;
  acknowledgement: string;
  acknowledged: string;
  unacknowledged: string;
  minimumPriority: string;
  anyPriority: string;
  area: string;
  tagPath: string;
  text: string;
  lookback: string;
  pageSize: string;
  columns: string;
  sorting: string;
  ascending: string;
  descending: string;
  acknowledgeEnabled: string;
  type: string;
  category: string;
  source: string;
  equipment: string;
  operator: string;
  operation: string;
  command: string;
  invalid: string;
}>;

const COPY: Readonly<Record<EngineeringLocale, Copy>> = Object.freeze({
  'pt-BR': Object.freeze({
    title: 'Configuração do Browser', mode: 'Fonte', current: 'Alarmes atuais', history: 'Histórico de alarmes', lifecycle: 'Estado', all: 'Todos', active: 'Ativos', returned: 'Retornados', acknowledgement: 'Reconhecimento', acknowledged: 'Reconhecidos', unacknowledged: 'Não reconhecidos', minimumPriority: 'Severidade mínima', anyPriority: 'Qualquer severidade', area: 'Área', tagPath: 'Caminho do TAG', text: 'Texto', lookback: 'Janela histórica (s)', pageSize: 'Linhas por página', columns: 'Colunas visíveis', sorting: 'Ordenação', ascending: 'Crescente', descending: 'Decrescente', acknowledgeEnabled: 'Permitir ação de ACK', type: 'Tipo', category: 'Categoria', source: 'Origem', equipment: 'Equipamento', operator: 'Operador', operation: 'Operação', command: 'Comando', invalid: 'Configuração inválida'
  }),
  en: Object.freeze({
    title: 'Browser configuration', mode: 'Source', current: 'Current alarms', history: 'Alarm history', lifecycle: 'State', all: 'All', active: 'Active', returned: 'Returned', acknowledgement: 'Acknowledgement', acknowledged: 'Acknowledged', unacknowledged: 'Unacknowledged', minimumPriority: 'Minimum severity', anyPriority: 'Any severity', area: 'Area', tagPath: 'TAG path', text: 'Text', lookback: 'Historical window (s)', pageSize: 'Rows per page', columns: 'Visible columns', sorting: 'Sorting', ascending: 'Ascending', descending: 'Descending', acknowledgeEnabled: 'Allow ACK action', type: 'Type', category: 'Category', source: 'Source', equipment: 'Equipment', operator: 'Operator', operation: 'Operation', command: 'Command', invalid: 'Invalid configuration'
  }),
  es: Object.freeze({
    title: 'Configuración del Browser', mode: 'Fuente', current: 'Alarmas actuales', history: 'Historial de alarmas', lifecycle: 'Estado', all: 'Todos', active: 'Activos', returned: 'Retornados', acknowledgement: 'Reconocimiento', acknowledged: 'Reconocidos', unacknowledged: 'No reconocidos', minimumPriority: 'Severidad mínima', anyPriority: 'Cualquier severidad', area: 'Área', tagPath: 'Ruta del TAG', text: 'Texto', lookback: 'Ventana histórica (s)', pageSize: 'Filas por página', columns: 'Columnas visibles', sorting: 'Ordenación', ascending: 'Ascendente', descending: 'Descendente', acknowledgeEnabled: 'Permitir acción ACK', type: 'Tipo', category: 'Categoría', source: 'Origen', equipment: 'Equipo', operator: 'Operador', operation: 'Operación', command: 'Comando', invalid: 'Configuración inválida'
  })
});

export function BrowserConfigurationEditor({ element, locale, onMutationIntent }: BrowserConfigurationEditorProps) {
  const text = COPY[locale];
  const isAlarm = element.type === BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser;
  const isEvent = element.type === BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser;
  const [error, setError] = useState<string | null>(null);
  const config = useMemo(() => {
    try {
      return isAlarm ? readAlarmBrowserConfig(element) : isEvent ? readEventBrowserConfig(element) : null;
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
      return null;
    }
  }, [element, isAlarm, isEvent]);

  if (!config || !element.id || (!isAlarm && !isEvent)) {
    return error ? <p className="browser-config-editor__error" role="alert">{text.invalid}: {error}</p> : null;
  }

  const commit = (next: AlarmBrowserConfig | EventBrowserConfig) => {
    setError(null);
    onMutationIntent({
      kind: 'property.set',
      objectIds: [element.id!],
      propertyKey: BROWSER_CONFIG_PROPERTY,
      value: next
    });
  };

  return <section className="browser-config-editor" data-testid="browser-configuration-editor">
    <h3>{text.title}</h3>
    {isAlarm
      ? <AlarmFields config={config as AlarmBrowserConfig} text={text} commit={commit} />
      : <EventFields config={config as EventBrowserConfig} text={text} commit={commit} />}
    {error ? <p className="browser-config-editor__error" role="alert">{error}</p> : null}
  </section>;
}

function AlarmFields({ config, text, commit }: { config: AlarmBrowserConfig; text: Copy; commit: (value: AlarmBrowserConfig) => void }) {
  return <>
    <Field label={text.mode}><select value={config.mode} onChange={event => commit({ ...config, mode: event.currentTarget.value as AlarmBrowserConfig['mode'] })}><option value="current">{text.current}</option><option value="history">{text.history}</option></select></Field>
    <Field label={text.lifecycle}><select value={config.lifecycle} onChange={event => commit({ ...config, lifecycle: event.currentTarget.value as AlarmBrowserConfig['lifecycle'] })}><option value="all">{text.all}</option><option value="active">{text.active}</option><option value="returned">{text.returned}</option></select></Field>
    <Field label={text.acknowledgement}><select value={config.acknowledgement} onChange={event => commit({ ...config, acknowledgement: event.currentTarget.value as AlarmBrowserConfig['acknowledgement'] })}><option value="all">{text.all}</option><option value="acknowledged">{text.acknowledged}</option><option value="unacknowledged">{text.unacknowledged}</option></select></Field>
    <Field label={text.minimumPriority}><select value={config.minimumPriority ?? ''} onChange={event => commit({ ...config, minimumPriority: event.currentTarget.value ? Number(event.currentTarget.value) : null })}><option value="">{text.anyPriority}</option><option value="1">1</option><option value="2">2</option><option value="3">3</option><option value="4">4</option></select></Field>
    <TextField label={text.area} value={config.area} onChange={value => commit({ ...config, area: value })} />
    <TextField label={text.tagPath} value={config.tagPath} onChange={value => commit({ ...config, tagPath: value })} />
    <TextField label={text.text} value={config.text} onChange={value => commit({ ...config, text: value })} />
    {config.mode === 'history' ? <NumberField label={text.lookback} value={config.lookbackSeconds} min={60} max={2678400} onChange={value => commit({ ...config, lookbackSeconds: value })} /> : null}
    <NumberField label={text.pageSize} value={config.pageSize} min={10} max={200} onChange={value => commit({ ...config, pageSize: value })} />
    <label className="browser-config-editor__check"><input type="checkbox" checked={config.acknowledgeEnabled} onChange={event => commit({ ...config, acknowledgeEnabled: event.currentTarget.checked })} />{text.acknowledgeEnabled}</label>
    <Columns label={text.columns} allowed={ALARM_BROWSER_COLUMNS} selected={config.columns} onChange={columns => commit({ ...config, columns: columns as readonly AlarmBrowserColumn[] })} />
    <Sort label={text.sorting} fields={['timestamp', 'state', 'priority', 'tag.path']} field={config.sortField} direction={config.sortDirection} text={text} onChange={(sortField, sortDirection) => commit({ ...config, sortField: sortField as AlarmBrowserConfig['sortField'], sortDirection })} />
  </>;
}

function EventFields({ config, text, commit }: { config: EventBrowserConfig; text: Copy; commit: (value: EventBrowserConfig) => void }) {
  return <>
    <TextField label={text.type} value={config.type} onChange={value => commit({ ...config, type: value })} />
    <TextField label={text.category} value={config.category} onChange={value => commit({ ...config, category: value })} />
    <TextField label={text.source} value={config.source} onChange={value => commit({ ...config, source: value })} />
    <TextField label={text.area} value={config.area} onChange={value => commit({ ...config, area: value })} />
    <TextField label={text.equipment} value={config.equipmentPath} onChange={value => commit({ ...config, equipmentPath: value })} />
    <TextField label={text.tagPath} value={config.tagPath} onChange={value => commit({ ...config, tagPath: value })} />
    <TextField label={text.operator} value={config.operator} onChange={value => commit({ ...config, operator: value })} />
    <TextField label={text.operation} value={config.operation} onChange={value => commit({ ...config, operation: value })} />
    <TextField label={text.command} value={config.commandKey} onChange={value => commit({ ...config, commandKey: value })} />
    <TextField label={text.text} value={config.text} onChange={value => commit({ ...config, text: value })} />
    <NumberField label={text.lookback} value={config.lookbackSeconds} min={60} max={2678400} onChange={value => commit({ ...config, lookbackSeconds: value })} />
    <NumberField label={text.pageSize} value={config.pageSize} min={10} max={200} onChange={value => commit({ ...config, pageSize: value })} />
    <Columns label={text.columns} allowed={EVENT_BROWSER_COLUMNS} selected={config.columns} onChange={columns => commit({ ...config, columns: columns as readonly EventBrowserColumn[] })} />
    <Sort label={text.sorting} fields={['timestamp', 'type', 'category', 'source', 'area', 'tag.path']} field={config.sortField} direction={config.sortDirection} text={text} onChange={(sortField, sortDirection) => commit({ ...config, sortField: sortField as EventBrowserConfig['sortField'], sortDirection })} />
  </>;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="browser-config-editor__field"><span>{label}</span>{children}</label>; }
function TextField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) { return <Field label={label}><input value={value} onChange={event => onChange(event.currentTarget.value)} /></Field>; }
function NumberField({ label, value, min, max, onChange }: { label: string; value: number; min: number; max: number; onChange: (value: number) => void }) { return <Field label={label}><input type="number" min={min} max={max} value={value} onChange={event => { const next = Number(event.currentTarget.value); if (Number.isSafeInteger(next) && next >= min && next <= max) onChange(next); }} /></Field>; }

function Columns({ label, allowed, selected, onChange }: { label: string; allowed: readonly string[]; selected: readonly string[]; onChange: (value: readonly string[]) => void }) {
  return <fieldset className="browser-config-editor__columns"><legend>{label}</legend>{allowed.map(column => <label key={column}><input type="checkbox" checked={selected.includes(column)} onChange={event => { const next = event.currentTarget.checked ? [...selected, column] : selected.filter(item => item !== column); if (next.length > 0) onChange(next); }} /><code>{column}</code></label>)}</fieldset>;
}

function Sort({ label, fields, field, direction, text, onChange }: { label: string; fields: readonly string[]; field: string; direction: 'ascending' | 'descending'; text: Copy; onChange: (field: string, direction: 'ascending' | 'descending') => void }) {
  return <div className="browser-config-editor__sort"><span>{label}</span><select value={field} onChange={event => onChange(event.currentTarget.value, direction)}>{fields.map(item => <option key={item} value={item}>{item}</option>)}</select><select value={direction} onChange={event => onChange(field, event.currentTarget.value as 'ascending' | 'descending')}><option value="ascending">{text.ascending}</option><option value="descending">{text.descending}</option></select></div>;
}
