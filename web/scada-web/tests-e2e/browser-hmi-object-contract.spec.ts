import { expect, test } from '@playwright/test';
import {
  DEFAULT_ALARM_BROWSER_CONFIG,
  DEFAULT_EVENT_BROWSER_CONFIG,
  normalizeAlarmBrowserConfig,
  normalizeEventBrowserConfig
} from '../src/visual-runtime/browserVisualModel';
import {
  createObjectAddIntent,
  listVisualObjectPaletteItems
} from '../src/engineering/visual-editor/object-palette/objectPaletteModel';
import { historicalBrowserCopy } from '../src/runtime/historical-browser/historicalBrowserI18n';

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

test('historical browser visible chrome is covered in pt-BR, en and es without translating dataset identities', () => {
  const pt = historicalBrowserCopy('pt-BR');
  const en = historicalBrowserCopy('en');
  const es = historicalBrowserCopy('es');

  expect(pt.title).toBe('Browser de dados históricos');
  expect(en.title).toBe('Historical Data Browser');
  expect(es.title).toBe('Browser de datos históricos');
  expect(pt.datasetOperationalEvents).toBe('Eventos operacionais');
  expect(en.datasetOperationalEvents).toBe('Operational events');
  expect(es.datasetOperationalEvents).toBe('Eventos operacionales');

  // Persisted/wire identities are not localized. These are deliberately asserted
  // as technical contract values next to the localized presentation copy.
  expect(['historian.samples', 'alarm.events', 'operational.events']).toEqual([
    'historian.samples', 'alarm.events', 'operational.events'
  ]);
});
