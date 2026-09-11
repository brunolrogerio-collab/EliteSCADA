import { expect, test } from '@playwright/test';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  TREND_PENS_PROPERTY,
  createTrendPen,
  readTrendPens,
  trendPensEngineeringValue,
  VISUAL_PROPERTY_KEYS
} from '../src/visual-runtime';
import { applyVisualEditorMutationIntent } from '../src/engineering/visual-editor/visualEditorCanonicalModel';
import {
  popupFrame,
  popupToVisualScreen,
  visualScreenToPopup
} from '../src/engineering/visual-editor/popupVisualAuthoringModel';
import {
  appendTrendLiveSample,
  buildTrendLiveSeries,
  isUsableTrendQuality,
  trendSampleFromRuntimeMessage,
  trendSampleFromRuntimeSnapshot
} from '../src/runtime/trendLiveSeriesModel';
import { buildTrendHistoricalQuery, buildTrendSeries } from '../src/runtime/trendVisualQueryModel';
import type { HistoricalQueryResponse } from '../src/runtime/historical-browser/historicalQueryApi';
import type { PopupEngineering, ScreenEngineering } from '../src/engineering/types';

const tagOne = '11111111-1111-1111-1111-111111111111';
const tagTwo = '22222222-2222-2222-2222-222222222222';

function fixturePens() {
  return [
    createTrendPen({ id: tagOne, path: 'Area/Pump/Pressure', label: 'Pressure', unit: 'bar' }, 0),
    createTrendPen({ id: tagTwo, path: 'Area/Pump/Flow', label: 'Flow', unit: 'm3/h' }, 1)
  ] as const;
}

function idGenerator(...ids: string[]): () => string {
  let index = 0;
  return () => {
    const value = ids[index];
    if (!value) throw new Error('test identity generator exhausted');
    index += 1;
    return value;
  };
}

test('C15 keeps Pens as native JSON structural data outside the scalar registry', () => {
  const pens = fixturePens();
  const value = trendPensEngineeringValue(pens);
  expect(Array.isArray(value)).toBe(true);
  expect(typeof value).not.toBe('string');

  const element = { type: BUILTIN_VISUAL_OBJECT_TYPES.trend, key: 'trend-01', properties: { [TREND_PENS_PROPERTY]: value } };
  const restored = readTrendPens(element);
  expect(restored).toHaveLength(2);
  expect(restored[0].tagId).toBe(tagOne);
  expect(restored[1].axis).toBe('right');
});

test('C15 canonical mutation seam persists Pens without passing them through scalar schema validation', () => {
  const screen: ScreenEngineering = {
    id: 'screen-id', key: 'screen', name: 'Screen', route: '/screen',
    elements: [{ id: 'trend-id', key: 'trend', type: BUILTIN_VISUAL_OBJECT_TYPES.trend, properties: {} }]
  };
  const pens = fixturePens();
  const next = applyVisualEditorMutationIntent(screen, {
    kind: 'property.set',
    objectIds: ['trend-id'],
    propertyKey: TREND_PENS_PROPERTY,
    value: trendPensEngineeringValue(pens)
  });
  expect(readTrendPens(next.elements![0])).toEqual(pens);
  expect(next.elements![0].properties?.[VISUAL_PROPERTY_KEYS.trendMode]).toBeUndefined();
});

test('C15 Trend uses ordinary canonical add move resize and keeps two instances independent', () => {
  let screen: ScreenEngineering = { key: 'screen', name: 'Screen', route: '/screen', elements: [] };
  screen = applyVisualEditorMutationIntent(screen, {
    kind: 'object.add',
    objectType: BUILTIN_VISUAL_OBJECT_TYPES.trend,
    at: { x: 20, y: 30 }
  }, { createObjectId: idGenerator('trend-a') });
  screen = applyVisualEditorMutationIntent(screen, {
    kind: 'property.set',
    objectIds: ['trend-a'],
    propertyKey: TREND_PENS_PROPERTY,
    value: trendPensEngineeringValue(fixturePens())
  });
  screen = applyVisualEditorMutationIntent(screen, {
    kind: 'object.add',
    objectType: BUILTIN_VISUAL_OBJECT_TYPES.trend,
    at: { x: 400, y: 50 }
  }, { createObjectId: idGenerator('trend-b') });
  screen = applyVisualEditorMutationIntent(screen, {
    kind: 'property.set',
    objectIds: ['trend-b'],
    propertyKey: TREND_PENS_PROPERTY,
    value: trendPensEngineeringValue([fixturePens()[1]])
  });
  screen = applyVisualEditorMutationIntent(screen, {
    kind: 'object.move', objectIds: ['trend-a'], delta: { x: 5, y: 7 }
  });
  screen = applyVisualEditorMutationIntent(screen, {
    kind: 'object.resize', objectId: 'trend-a', bounds: { x: 25, y: 37, width: 640, height: 280 }
  });

  expect(screen.elements).toHaveLength(2);
  expect(screen.elements![0].properties).toMatchObject({ x: 25, y: 37, width: 640, height: 280 });
  expect(readTrendPens(screen.elements![0])).toHaveLength(2);
  expect(screen.elements![1].properties).toMatchObject({ x: 400, y: 50 });
  expect(readTrendPens(screen.elements![1])).toHaveLength(1);
  expect(readTrendPens(screen.elements![1])[0].tagId).toBe(tagTwo);
});

test('C15 Popup authoring round-trip uses the same canonical Trend object and native Pens', () => {
  const popup: PopupEngineering = {
    id: 'popup-id', key: 'popup.pump', name: 'Pump popup', templateKey: 'pump.standard', elements: []
  };
  let visualScreen = popupToVisualScreen(popup);
  visualScreen = applyVisualEditorMutationIntent(visualScreen, {
    kind: 'object.add', objectType: BUILTIN_VISUAL_OBJECT_TYPES.trend, at: { x: 12, y: 16 }
  }, { createObjectId: idGenerator('popup-trend') });
  visualScreen = applyVisualEditorMutationIntent(visualScreen, {
    kind: 'property.set',
    objectIds: ['popup-trend'],
    propertyKey: TREND_PENS_PROPERTY,
    value: trendPensEngineeringValue(fixturePens())
  });
  visualScreen = applyVisualEditorMutationIntent(visualScreen, {
    kind: 'object.resize', objectId: 'popup-trend', bounds: { x: 12, y: 16, width: 520, height: 220 }
  });
  const persisted = visualScreenToPopup(visualScreen, popupFrame(popup));

  expect(persisted.templateKey).toBe('pump.standard');
  expect(persisted.elements).toHaveLength(1);
  expect(persisted.elements![0].type).toBe(BUILTIN_VISUAL_OBJECT_TYPES.trend);
  expect(persisted.elements![0].properties).toMatchObject({ x: 12, y: 16, width: 520, height: 220 });
  expect(readTrendPens(persisted.elements![0])).toEqual(fixturePens());
});

test('C15 builds one protected historian.samples query for all visible Pens', () => {
  const request = buildTrendHistoricalQuery(fixturePens(), 3600);
  expect(request.datasetKey).toBe('historian.samples');
  expect(request.version).toBe(1);
  expect(request.timeRange).toEqual({ kind: 'relative', durationSeconds: 3600, anchor: 'now' });
  expect(request.filters).toEqual([{
    field: 'tag.id', operator: 'in', values: [
      { kind: 'guid', value: tagOne },
      { kind: 'guid', value: tagTwo }
    ]
  }]);
  expect(request.orderBy).toEqual([{ field: 'timestamp', direction: 'ascending' }]);
});

test('C15 groups historian rows by canonical tag id and preserves quality', () => {
  const response: HistoricalQueryResponse = {
    version: 1,
    datasetKey: 'historian.samples',
    columns: [],
    rows: [
      row(tagTwo, 'Area/Pump/Flow', '2026-09-03T12:00:02Z', '20.5', 'Good'),
      row(tagOne, 'Area/Pump/Pressure', '2026-09-03T12:00:01Z', '7.2', 'Good'),
      row(tagOne, 'Area/Pump/Pressure', '2026-09-03T12:00:03Z', '7.3', 'Bad')
    ],
    fromUtc: '2026-09-03T11:00:00Z',
    toUtc: '2026-09-03T12:00:10Z',
    nextCursor: null,
    pageSize: 3
  };
  const series = buildTrendSeries(response, fixturePens());
  expect(series).toHaveLength(2);
  expect(series[0].samples.map(sample => sample.value)).toEqual([7.2, 7.3]);
  expect(series[0].samples[1].quality).toBe('Bad');
  expect(series[1].samples[0].value).toBe(20.5);
});

test('C15 live series consumes canonical runtime TAG snapshots and realtime messages', () => {
  const pen = fixturePens()[0];
  const initial = trendSampleFromRuntimeSnapshot(pen, {
    id: tagOne,
    name: 'Pressure',
    path: 'Area/Pump/Pressure',
    dataType: 'double',
    readOnly: true,
    current: {
      tagId: tagOne,
      value: 7.2,
      timestamp: '2026-09-03T12:00:00Z',
      quality: 'Good'
    }
  });
  expect(initial).not.toBeNull();

  const realtime = trendSampleFromRuntimeMessage(pen, {
    type: 'tagValueChanged',
    tag: { id: tagOne, name: 'Pressure', path: 'Area/Pump/Pressure' },
    value: 7.4,
    quality: 'Good',
    timestamp: '2026-09-03T12:00:01Z'
  });
  expect(realtime).not.toBeNull();

  let buffers = appendTrendLiveSample(new Map(), pen, initial!, 3600, Date.parse('2026-09-03T12:00:01Z'));
  buffers = appendTrendLiveSample(buffers, pen, realtime!, 3600, Date.parse('2026-09-03T12:00:01Z'));
  const series = buildTrendLiveSeries([pen], buffers);
  expect(series[0].samples.map(sample => sample.value)).toEqual([7.2, 7.4]);
  expect(isUsableTrendQuality('Good')).toBe(true);
  expect(isUsableTrendQuality(0)).toBe(true);
  expect(isUsableTrendQuality('Uncertain')).toBe(false);
  expect(isUsableTrendQuality('Bad')).toBe(false);
});

function row(tagId: string, tagPath: string, timestamp: string, value: string, quality: string) {
  return Object.freeze({ cells: Object.freeze({
    'tag.id': Object.freeze({ kind: 'guid' as const, value: tagId }),
    'tag.path': Object.freeze({ kind: 'string' as const, value: tagPath }),
    timestamp: Object.freeze({ kind: 'dateTime' as const, value: timestamp }),
    value: Object.freeze({ kind: 'number' as const, value }),
    quality: Object.freeze({ kind: 'enum' as const, value: quality })
  }) });
}
