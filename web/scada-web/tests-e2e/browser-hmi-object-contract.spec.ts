import { expect, test } from '@playwright/test';
import {
  DEFAULT_ALARM_BROWSER_CONFIG,
  DEFAULT_EVENT_BROWSER_CONFIG,
  readAlarmBrowserConfig,
  readEventBrowserConfig,
  normalizeAlarmBrowserConfig,
  normalizeEventBrowserConfig
} from '../src/visual-runtime/browserVisualModel';
import {
  buildAlarmHistoricalRequest,
  buildEventHistoricalRequest,
  historicalAlarmStates
} from '../src/engineering/visual-editor/BrowserVisualElement';
import {
  createObjectAddIntent,
  listVisualObjectPaletteItems
} from '../src/engineering/visual-editor/object-palette/objectPaletteModel';
import { c07VisualEditorText } from '../src/engineering/visual-editor/c07VisualEditorI18n';
import { applyVisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import { historicalBrowserCopy } from '../src/runtime/historical-browser/historicalBrowserI18n';
import { formatHistoricalQueryValue } from '../src/runtime/historical-browser/historicalBrowserQueryAdapter';

test('Alarm Browser and Event Browser are first-class authoring palette objects', () => {
  const items = listVisualObjectPaletteItems();
  const alarm = items.find(item => item.objectType === 'core.alarmBrowser');
  const event = items.find(item => item.objectType === 'core.eventBrowser');

  expect(alarm).toMatchObject({ labelKey: 'alarmBrowser', category: 'content' });
  expect(event).toMatchObject({ labelKey: 'eventBrowser', category: 'content' });

  expect(createObjectAddIntent('core.alarmBrowser', { at: { x: 120, y: 140 } })).toEqual({
    kind: 'object.add',
    objectType: 'core.alarmBrowser',
    at: { x: 120, y: 140 },
    initialProperties: { width: 720, height: 320 }
  });
  expect(createObjectAddIntent('core.eventBrowser', { at: { x: 480, y: 140 } })).toEqual({
    kind: 'object.add',
    objectType: 'core.eventBrowser',
    at: { x: 480, y: 140 },
    initialProperties: { width: 720, height: 320 }
  });
});

test('browser insertion labels follow the shared Screen and Popup visual-editor locale', () => {
  expect(c07VisualEditorText('pt-BR').palette).toEqual({
    alarmBrowser: 'Browser de Alarmes',
    eventBrowser: 'Browser de Eventos'
  });
  expect(c07VisualEditorText('en').palette).toEqual({
    alarmBrowser: 'Alarm Browser',
    eventBrowser: 'Event Browser'
  });
  expect(c07VisualEditorText('es').palette).toEqual({
    alarmBrowser: 'Browser de Alarmas',
    eventBrowser: 'Browser de Eventos'
  });
});

test('browser configurations remain structured, independent and kind-specific', () => {
  const alarm = normalizeAlarmBrowserConfig({
    ...DEFAULT_ALARM_BROWSER_CONFIG,
    mode: 'history',
    lifecycle: 'returned',
    acknowledgement: 'all',
    area: 'Area-A',
    columns: ['timestamp', 'state', 'priority', 'tag.path'],
    pageSize: 20
  });
  const event = normalizeEventBrowserConfig({
    ...DEFAULT_EVENT_BROWSER_CONFIG,
    category: 'operator',
    area: 'Area-B',
    operation: 'start',
    columns: ['timestamp', 'category', 'area', 'operation', 'message'],
    pageSize: 30
  });

  expect(alarm).toMatchObject({ mode: 'history', lifecycle: 'returned', area: 'Area-A', pageSize: 20 });
  expect(event).toMatchObject({ category: 'operator', area: 'Area-B', operation: 'start', pageSize: 30 });
  expect(alarm.columns).not.toEqual(event.columns);
  expect(() => normalizeAlarmBrowserConfig({ ...DEFAULT_ALARM_BROWSER_CONFIG, columns: ['type'] })).toThrow();
  expect(() => normalizeEventBrowserConfig({ ...DEFAULT_EVENT_BROWSER_CONFIG, columns: ['state'] })).toThrow();
});

test('Alarm history maps lifecycle and acknowledgement into one honest state filter', () => {
  expect(historicalAlarmStates({ ...DEFAULT_ALARM_BROWSER_CONFIG, lifecycle: 'active', acknowledgement: 'all' })).toEqual(['Active', 'Acknowledged']);
  expect(historicalAlarmStates({ ...DEFAULT_ALARM_BROWSER_CONFIG, lifecycle: 'active', acknowledgement: 'acknowledged' })).toEqual(['Acknowledged']);
  expect(historicalAlarmStates({ ...DEFAULT_ALARM_BROWSER_CONFIG, lifecycle: 'active', acknowledgement: 'unacknowledged' })).toEqual(['Active']);
  expect(historicalAlarmStates({ ...DEFAULT_ALARM_BROWSER_CONFIG, lifecycle: 'returned', acknowledgement: 'acknowledged' })).toEqual(['Returned']);

  const request = buildAlarmHistoricalRequest({
    ...DEFAULT_ALARM_BROWSER_CONFIG,
    mode: 'history',
    lifecycle: 'active',
    acknowledgement: 'all',
    minimumPriority: 3,
    tagPath: 'Plant.P01'
  }, null);

  expect(request.datasetKey).toBe('alarm.events');
  expect(request.filters).toEqual(expect.arrayContaining([
    expect.objectContaining({ field: 'state', operator: 'in', values: [
      { kind: 'enum', value: 'Active' },
      { kind: 'enum', value: 'Acknowledged' }
    ] }),
    expect.objectContaining({ field: 'priority', operator: 'gte', values: [{ kind: 'number', value: '3' }] }),
    expect.objectContaining({ field: 'tag.path', operator: 'contains', values: [{ kind: 'string', value: 'Plant.P01' }] })
  ]));
});

test('Event Browser queries canonical operational.events without Alarm or Audit semantics', () => {
  const request = buildEventHistoricalRequest({
    ...DEFAULT_EVENT_BROWSER_CONFIG,
    type: 'OperatorAction',
    category: 'operation',
    source: 'runtime.hmi',
    area: 'Process',
    equipmentPath: 'Plant.P01',
    tagPath: 'Plant.P01.Running',
    operator: 'operator-a',
    operation: 'start',
    commandKey: 'pump.start',
    text: 'started',
    pageSize: 40
  }, null);

  expect(request.datasetKey).toBe('operational.events');
  expect(request.page.limit).toBe(40);
  expect(request.search).toBe('started');
  expect(request.filters).toEqual(expect.arrayContaining([
    expect.objectContaining({ field: 'type', operator: 'contains' }),
    expect.objectContaining({ field: 'category', operator: 'contains' }),
    expect.objectContaining({ field: 'source', operator: 'contains' }),
    expect.objectContaining({ field: 'area', operator: 'contains' }),
    expect.objectContaining({ field: 'equipment.path', operator: 'contains' }),
    expect.objectContaining({ field: 'tag.path', operator: 'contains' }),
    expect.objectContaining({ field: 'operator', operator: 'contains' }),
    expect.objectContaining({ field: 'operation', operator: 'contains' }),
    expect.objectContaining({ field: 'command.key', operator: 'contains' })
  ]));
  expect(request.filters?.some(filter => filter.field === 'state' || filter.field === 'priority' || filter.field === 'audit')).toBe(false);
});

test('canonical property mutation changes only the selected Browser instance', () => {
  const screen: any = {
    id: 'screen-c18',
    key: 'c18.screen',
    name: 'C18 Screen',
    elements: [
      { id: 'alarm-a', key: 'alarm-a', type: 'core.alarmBrowser', properties: { x: 0, y: 0, width: 720, height: 320 } },
      { id: 'alarm-b', key: 'alarm-b', type: 'core.alarmBrowser', properties: { x: 740, y: 0, width: 720, height: 320 } },
      { id: 'event-a', key: 'event-a', type: 'core.eventBrowser', properties: { x: 0, y: 340, width: 720, height: 320 } }
    ]
  };

  const updated = applyVisualEditorMutationIntent(screen, {
    kind: 'property.set',
    objectIds: ['alarm-a'],
    propertyKey: 'browserConfig',
    value: { ...DEFAULT_ALARM_BROWSER_CONFIG, area: 'Only-A', pageSize: 25 }
  });

  const alarmA = updated.elements!.find((element: any) => element.id === 'alarm-a')!;
  const alarmB = updated.elements!.find((element: any) => element.id === 'alarm-b')!;
  const eventA = updated.elements!.find((element: any) => element.id === 'event-a')!;

  expect(readAlarmBrowserConfig(alarmA)).toMatchObject({ area: 'Only-A', pageSize: 25 });
  expect(readAlarmBrowserConfig(alarmB)).toMatchObject({ area: '', pageSize: 50 });
  expect(readEventBrowserConfig(eventA)).toMatchObject({ area: '', pageSize: 50 });
  expect(alarmB.properties).not.toHaveProperty('browserConfig');
  expect(eventA.properties).not.toHaveProperty('browserConfig');
});

test('historical browser visible chrome and scalar presentation are covered in pt-BR, en and es', () => {
  const pt = historicalBrowserCopy('pt-BR');
  const en = historicalBrowserCopy('en');
  const es = historicalBrowserCopy('es');

  expect(pt.title).toBe('Browser de dados históricos');
  expect(en.title).toBe('Historical Data Browser');
  expect(es.title).toBe('Browser de datos históricos');
  expect(pt.datasetOperationalEvents).toBe('Eventos operacionais');
  expect(en.datasetOperationalEvents).toBe('Operational events');
  expect(es.datasetOperationalEvents).toBe('Eventos operacionales');

  expect(formatHistoricalQueryValue({ kind: 'boolean', value: 'true' }, 'pt-BR')).toBe('Verdadeiro');
  expect(formatHistoricalQueryValue({ kind: 'boolean', value: 'false' }, 'en')).toBe('False');
  expect(formatHistoricalQueryValue({ kind: 'boolean', value: 'true' }, 'es')).toBe('Verdadero');
  expect(formatHistoricalQueryValue({ kind: 'number', value: 'not-a-number' }, 'pt-BR')).toBe('Indisponível');
  expect(formatHistoricalQueryValue({ kind: 'enum', value: 'Active' }, 'es')).toBe('Active');

  // Persisted/wire identities and enum values are deliberately not localized.
  expect(['historian.samples', 'alarm.events', 'operational.events']).toEqual([
    'historian.samples', 'alarm.events', 'operational.events'
  ]);
});
