import { expect, test } from '@playwright/test';
import type {
  AssetProcessIntelligenceProfileEngineering,
  ScreenEngineering,
  TagEngineering
} from '../src/engineering/types';
import {
  generateIntelligenceDashboardDraft,
  generateIntelligencePerformanceScreenDraft,
  INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS,
  isNumericTagDataType
} from '../src/engineering/intelligenceDashboardGenerator';
import { BUILTIN_VISUAL_OBJECT_TYPES, MAX_TREND_PENS } from '../src/visual-runtime';

const equipmentAssignment = {
  equipmentName: 'Pump P-101',
  equipmentPath: 'Plant.P-101',
  profile: {
    id: 'profile-1', key: 'pump-standard', name: 'Pump profile',
    signals: [
      { id: 'signal-current', key: 'current', signalRole: 'current', tagId: 'tag-current', required: true },
      { id: 'signal-flow', key: 'flow', signalRole: 'flow', tagId: 'tag-flow', required: false },
      { id: 'signal-running', key: 'running', signalRole: 'state', tagId: 'tag-running', required: true },
      { id: 'signal-stale', key: 'missing', signalRole: 'pressure', tagId: 'no-longer-present', required: false }
    ]
  } satisfies AssetProcessIntelligenceProfileEngineering
};

function numericTag(id: string, path: string, scaleMinimum?: number | null, scaleMaximum?: number | null): TagEngineering {
  return { id, name: path.split('.').at(-1) ?? path, path, dataType: 'Double', engineeringUnit: 'A', scaleMinimum, scaleMaximum, readOnly: true };
}

function ids(): () => string {
  let index = 0;
  return () => `dashboard-element-${++index}`;
}

function generated(existing: readonly ScreenEngineering[] = [], tags: readonly TagEngineering[] = [
  numericTag('tag-current', 'Plant.P-101.Current', 0, 120),
  numericTag('tag-flow', 'Plant.P-101.Flow'),
  { id: 'tag-running', name: 'Running', path: 'Plant.P-101.Running', dataType: 'Boolean', readOnly: true }
]) {
  return generateIntelligenceDashboardDraft(existing, {
    kind: 'equipment-profile', stableKey: 'Plant.P-101/pump-standard', displayName: 'Pump P-101',
    assignments: [equipmentAssignment]
  }, tags, 'pt-BR', ids());
}

test('dashboard generator builds a canonical editable screen from stable TAG references', () => {
  const screen = generated();
  const value = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay);
  const measurement = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.measurementBar);
  const trend = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.trend);

  expect(screen).toMatchObject({ key: 'intelligence-plant.p-101-pump-standard', route: '/intelligence-plant.p-101-pump-standard' });
  expect(screen.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.1',
    intelligenceDashboardSourceKind: 'equipment-profile'
  });
  expect(measurement?.bindings?.[0]).toMatchObject({
    key: 'value', kind: 'Tag', target: 'Plant.P-101.Current', tagReference: { tagId: 'tag-current' }
  });
  expect(value?.bindings?.[0]).toMatchObject({
    key: 'text', kind: 'Tag', target: 'Plant.P-101.Flow', tagReference: { tagId: 'tag-flow' }
  });
  expect(measurement?.properties).toMatchObject({ minimum: 0, maximum: 120 });
  expect(value?.properties).not.toHaveProperty('minimum');
  expect(trend?.properties?.pens).toHaveLength(2);
  expect(trend?.properties?.pens).toEqual(expect.arrayContaining([
    expect.objectContaining({ visible: false })
  ]));
  expect(screen.elements?.some(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.numericInput)).toBe(false);
  expect(screen.metadata?.intelligenceDashboardSummary).toContain('não calcula KPIs');
});

test('dashboard generator fails closed when no numeric TAG can be resolved', () => {
  expect(() => generated([], [
    { id: 'tag-running', name: 'Running', path: 'Plant.P-101.Running', dataType: 'Boolean', readOnly: true }
  ])).toThrow(/TAGs numéricos/);
});

test('dashboard generator caps cards and trend pens and reports omitted sources', () => {
  const signals = Array.from({ length: MAX_TREND_PENS + 3 }, (_, index) => ({
    id: `signal-${index}`, key: `signal-${index}`, signalRole: 'process', tagId: `tag-${index}`, required: true
  }));
  const profile: AssetProcessIntelligenceProfileEngineering = {
    id: 'profile-many', key: 'many-signals', name: 'Many signals', signals
  };
  const tags = signals.map((signal, index) => numericTag(signal.tagId, `Plant.P-101.Signal${index}`));
  const screen = generateIntelligenceDashboardDraft([], {
    kind: 'process-stage', stableKey: 'stage-many-signals', displayName: 'Pumping stage',
    assignments: [{ ...equipmentAssignment, profile }]
  }, tags, 'en', ids());
  const valueDisplays = screen.elements?.filter(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay) ?? [];
  const trend = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.trend);
  const summary = String(screen.metadata?.intelligenceDashboardSummary ?? '');

  expect(valueDisplays).toHaveLength(INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS);
  expect(trend?.properties?.pens).toHaveLength(MAX_TREND_PENS);
  expect(summary).toContain('4 additional numeric signals omitted from value cards');
  expect(summary).toContain('initially hidden until enabled in Runtime');
  expect(summary).toContain('does not calculate KPIs');
});

test('dashboard generator keeps existing screen key and route identities unique', () => {
  const first = generated();
  const second = generated([first]);
  expect(second.key).toBe(`${first.key}-2`);
  expect(second.route).toBe(`${first.route}-2`);
  expect(first.key).not.toBe(second.key);
});

test('Performance V1B screen draft uses the canonical visual type and persists stable refs only', () => {
  const selection = {
    scopeId: '10000000-0000-0000-0000-000000000001',
    assignmentId: '20000000-0000-0000-0000-000000000002',
    profileId: '30000000-0000-0000-0000-000000000003',
    ruleId: 'specific-active-energy-at-recorded-production-rate.v1',
    ruleVersion: '1.0',
    signalIds: [
      '40000000-0000-0000-0000-000000000004',
      '50000000-0000-0000-0000-000000000005'
    ],
    displayName: 'Pump P-101'
  } as const;
  const first = generateIntelligencePerformanceScreenDraft([], selection, 'pt-BR', ids());
  const second = generateIntelligencePerformanceScreenDraft([first], selection, 'pt-BR', ids());
  const performance = first.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligencePerformanceV1B);

  expect(first.key).toBe('intelligence-performance-pump-p-101');
  expect(second.key).toBe(`${first.key}-2`);
  expect(performance?.properties).toMatchObject({
    intelligenceScopeId: selection.scopeId,
    intelligenceAssignmentId: selection.assignmentId,
    intelligenceProfileId: selection.profileId,
    intelligenceRuleId: selection.ruleId,
    intelligenceRuleVersion: selection.ruleVersion,
    intelligenceSignalIds: JSON.stringify(selection.signalIds)
  });
  expect(performance?.properties).not.toHaveProperty('result');
  expect(performance?.properties).not.toHaveProperty('window');
  expect(first.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.4',
    intelligencePerformanceRuleId: selection.ruleId,
    intelligencePerformanceRuleVersion: selection.ruleVersion
  });
});

test('Performance V1B screen draft rejects incomplete and duplicated identities', () => {
  const selection = {
    scopeId: '10000000-0000-0000-0000-000000000001',
    assignmentId: '20000000-0000-0000-0000-000000000002',
    profileId: '30000000-0000-0000-0000-000000000003',
    ruleId: 'specific-active-energy-at-recorded-production-rate.v1',
    ruleVersion: '1.0',
    signalIds: [
      '40000000-0000-0000-0000-000000000004',
      '40000000-0000-0000-0000-000000000004'
    ],
    displayName: 'Pump P-101'
  } as const;
  expect(() => generateIntelligencePerformanceScreenDraft([], selection, 'en', ids())).toThrow(/stable equipment assignment/);
});

test('numeric source filtering excludes state and text TAGs', () => {
  expect(isNumericTagDataType('Double')).toBe(true);
  expect(isNumericTagDataType('Float32')).toBe(true);
  expect(isNumericTagDataType('Boolean')).toBe(false);
  expect(isNumericTagDataType('String')).toBe(false);
});
