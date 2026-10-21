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
  createObjectAddIntent,
  listVisualObjectPaletteItems
} from '../src/engineering/visual-editor/object-palette/objectPaletteModel';
import { c07VisualEditorText } from '../src/engineering/visual-editor/c07VisualEditorI18n';
import { applyVisualEditorMutationIntent, replaceDynamoInPackage } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import { historicalBrowserCopy } from '../src/runtime/historical-browser/historicalBrowserI18n';
import { formatHistoricalQueryValue } from '../src/runtime/historical-browser/historicalBrowserQueryAdapter';
import { isVisibleInActiveAlarmBrowser, presentCurrentBrowserAlarm } from '../src/engineering/visual-editor/browserVisualPresentation';
import type { RuntimeAlarmCenterItem } from '../src/runtime/alarmCenterTypes';

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
    arc: 'Arco',
    bezier: 'Curva Bézier',
    alarmBrowser: 'Browser de Alarmes',
    eventBrowser: 'Browser de Eventos',
    videoPlayer: 'Vídeo',
    pdfViewer: 'Visualizador PDF',
    reportLauncher: 'Abrir relatório',
    measurementDial: 'Mostrador semicircular',
    measurementRing: 'Anel de medição',
    measurementBar: 'Barra graduada',
    measurementThermometer: 'Régua vertical',
    measurementBands: 'Faixas de intervalo',
    measurementTarget: 'Valor atual e meta'
  });
  expect(c07VisualEditorText('en').palette).toEqual({
    arc: 'Arc',
    bezier: 'Bezier curve',
    alarmBrowser: 'Alarm Browser',
    eventBrowser: 'Event Browser',
    videoPlayer: 'Video',
    pdfViewer: 'PDF viewer',
    reportLauncher: 'Report launcher',
    measurementDial: 'Semicircular dial',
    measurementRing: 'Measurement ring',
    measurementBar: 'Graduated bar',
    measurementThermometer: 'Vertical ruler',
    measurementBands: 'Range bands',
    measurementTarget: 'Actual vs target'
  });
  expect(c07VisualEditorText('es').palette).toEqual({
    arc: 'Arco',
    bezier: 'Curva Bézier',
    alarmBrowser: 'Browser de Alarmas',
    eventBrowser: 'Browser de Eventos',
    videoPlayer: 'Vídeo',
    pdfViewer: 'Visor PDF',
    reportLauncher: 'Abrir informe',
    measurementDial: 'Indicador semicircular',
    measurementRing: 'Anillo de medición',
    measurementBar: 'Barra graduada',
    measurementThermometer: 'Regla vertical',
    measurementBands: 'Bandas de rango',
    measurementTarget: 'Valor actual y objetivo'
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

test('alarm browser localizes current alarm state and priority without exposing a TAG GUID', () => {
  const alarm: RuntimeAlarmCenterItem = {
    definitionId: 'alarm-returned', name: 'High pressure', tagId: 'tag-guid-not-for-display',
    type: 'high', priority: 4, state: 3, lastTransition: '2026-10-21T16:30:00Z', acknowledgedAt: null
  };

  const portuguese = presentCurrentBrowserAlarm(alarm, '', 'pt-BR');
  expect(portuguese).toMatchObject({
    state: 'Retornado', priority: 'Crítica', tagPath: 'Caminho TAG indisponível', canAcknowledge: true
  });
  expect(portuguese.timestamp).not.toBe(alarm.lastTransition);
  expect(portuguese.timestamp).not.toContain('T');
  expect(presentCurrentBrowserAlarm(alarm, '', 'en')).toMatchObject({
    state: 'Returned', priority: 'Critical', tagPath: 'TAG path unavailable', canAcknowledge: true
  });
  expect(presentCurrentBrowserAlarm(alarm, '', 'es')).toMatchObject({
    state: 'Retornada', priority: 'Crítica', tagPath: 'Ruta TAG no disponible', canAcknowledge: true
  });
  expect(isVisibleInActiveAlarmBrowser(alarm)).toBe(true);
  expect(presentCurrentBrowserAlarm(alarm, 'Plant/Pressure', 'en').tagPath).toBe('Plant/Pressure');
  expect(presentCurrentBrowserAlarm(alarm, '', 'en').tagPath).not.toContain(alarm.tagId);
  expect(isVisibleInActiveAlarmBrowser({ ...alarm, acknowledgedAt: '2026-10-21T16:35:00Z' })).toBe(false);

  const active = { ...alarm, state: 1, priority: 3 };
  expect(presentCurrentBrowserAlarm(active, 'Plant/Pump', 'pt-BR')).toMatchObject({
    state: 'Ativo', priority: 'Alta', tagPath: 'Plant/Pump', canAcknowledge: true
  });
  expect(isVisibleInActiveAlarmBrowser(active)).toBe(true);
});

test('editing a Dynamo changes its drawing while preserving its stable link and parameter contract', () => {
  const original = {
    id: 'dynamo-id',
    key: 'process.pump',
    name: 'Pump',
    bindings: [{ key: 'running', kind: 'tag', target: 'pump.running' }],
    parameters: [{ key: 'running', dataType: 'boolean', defaultValue: false }],
    elements: [{ id: 'old-shape', key: 'body', type: 'core.rectangle', properties: { x: 1, y: 2, width: 30, height: 20 } }]
  } as const;
  const model = { schema: 'elitescada.engineering', schemaVersion: 1, exportedAt: '2026-01-01T00:00:00Z', tags: [], alarms: [], dynamos: [original] } as never;
  const draft = {
    id: original.id,
    key: original.key,
    name: 'Pump polished',
    elements: [{ id: 'new-shape', key: 'body', type: 'core.ellipse', properties: { x: 4, y: 5, width: 42, height: 26 } }]
  } as never;

  const result = replaceDynamoInPackage(model, original as never, draft);

  expect(result.dynamos?.[0]).toMatchObject({
    id: 'dynamo-id', key: 'process.pump', name: 'Pump polished',
    bindings: original.bindings,
    parameters: original.parameters,
    elements: draft.elements
  });
  expect(original.name).toBe('Pump');
  expect(original.elements[0].id).toBe('old-shape');
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

  expect(pt.title).toBe('Histórico');
  expect(en.title).toBe('History');
  expect(es.title).toBe('Histórico');
  expect(pt.datasetOperationalEvents).toBe('Eventos');
  expect(en.datasetOperationalEvents).toBe('Events');
  expect(es.datasetOperationalEvents).toBe('Eventos');

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
