import { expect, test } from '@playwright/test';
import type {
  AssetProcessIntelligenceProfileEngineering,
  ScreenEngineering,
  TagEngineering
} from '../src/engineering/types';
import {
  generateIntelligenceDashboardDraft,
  generateIntelligenceDashboardCompositionDrafts,
  generateIntelligenceContextualDeviationScreenDraft,
  generateIntelligenceGuidedDashboardDrafts,
  generateIntelligenceProfileCoverageScreenDraft,
  generateIntelligenceReferenceEvidenceScreenDraft,
  generateIntelligenceContextEligibilityScreenDraft,
  generateIntelligenceProcessStageCoverageScreenDraft,
  generateIntelligenceProcessStageGuidedDashboardDrafts,
  generateIntelligenceProcessStageInsightsDashboardDraft,
  generateIntelligenceProcessStageCompositionDrafts,
  generateIntelligencePerformanceScreenDraft,
  INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS,
  INTELLIGENCE_PROFILE_COMPOSITE_MAX_VALUE_CARDS,
  INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS,
  isNumericTagDataType,
  type IntelligenceProfileCoverageDashboardSelection
} from '../src/engineering/intelligenceDashboardGenerator';
import { BUILTIN_VISUAL_OBJECT_TYPES, MAX_TREND_PENS, VISUAL_PROPERTY_KEYS } from '../src/visual-runtime';

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
  expect(screen.properties).toMatchObject({ backgroundMode: 'color', backgroundColor: '#0F172A' });
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
  expect(value?.properties).not.toHaveProperty(VISUAL_PROPERTY_KEYS.showEngineeringUnit);
  expect(measurement?.properties).toMatchObject({ minimum: 0, maximum: 120 });
  expect(value?.properties).not.toHaveProperty('minimum');
  expect(trend?.properties?.pens).toHaveLength(2);
  expect(trend?.properties?.pens).toEqual(expect.arrayContaining([
    expect.objectContaining({ visible: false })
  ]));
  expect(screen.elements?.some(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.numericInput)).toBe(false);
  expect(screen.metadata?.intelligenceDashboardSummary).toContain('não calcula KPIs');
  expect(screen.metadata?.intelligenceDashboardSummary).toContain('A cobertura do perfil foi omitida');
});

test('measurement layouts require real scales and retain authored bands, targets and theme', () => {
  const source = { kind: 'equipment-profile' as const, stableKey: 'pump', displayName: 'Pump', assignments: [equipmentAssignment] };
  const tags = [numericTag('tag-current', 'Plant.Current')];
  const layouts = ['measurementDial', 'measurementRing', 'measurementBar', 'measurementThermometer', 'measurementBands', 'measurementTarget'] as const;
  for (const layout of layouts) {
    const appearance = { theme: 'high-contrast' as const, measurements: {
      'tag-current': { layout, minimum: 10, maximum: 110, target: 60, bandOneEnd: 30, bandTwoEnd: 80 }
    } };
    const screen = generateIntelligenceDashboardDraft([], source, tags, 'pt-BR', ids(), appearance);
    const widget = screen.elements?.find(element => element.type === `core.${layout}`);
    expect(widget?.properties).toMatchObject({ minimum: 10, maximum: 110, measurementTheme: 'high-contrast', measurementUnit: 'A' });
    if (layout === 'measurementTarget') expect(widget?.properties?.measurementTarget).toBe(60);
    if (layout === 'measurementBands') expect(widget?.properties).toMatchObject({ measurementBandOneEnd: 30, measurementBandTwoEnd: 80 });
    expect(() => generateIntelligenceDashboardDraft([], source, tags, 'pt-BR', ids(), {
      ...appearance, measurements: { 'tag-current': { layout } }
    })).toThrow(/limites/);
  }
});

test('single-equipment dashboard composes bounded profile coverage beside its signal/trend view', () => {
  const profileId = '30000000-0000-0000-0000-000000000003';
  const scopeId = '10000000-0000-0000-0000-000000000001';
  const screen = generateIntelligenceDashboardDraft([], {
    kind: 'equipment-profile', stableKey: 'Plant.P-101/pump-standard', displayName: 'Pump P-101',
    assignments: [{
      ...equipmentAssignment,
      scopeId,
      profile: { ...equipmentAssignment.profile, id: profileId }
    }]
  }, [
    numericTag('tag-current', 'Plant.P-101.Current', 0, 120),
    numericTag('tag-flow', 'Plant.P-101.Flow'),
    { id: 'tag-running', name: 'Running', path: 'Plant.P-101.Running', dataType: 'Boolean', readOnly: true }
  ], 'pt-BR', ids());
  const coverage = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1);
  const trend = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.trend);
  const summary = String(screen.metadata?.intelligenceDashboardSummary ?? '');

  expect(screen.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.8',
    intelligenceDashboardSourceKind: 'equipment-profile'
  });
  expect(coverage?.properties).toMatchObject({
    x: 900, y: 166, width: 956, height: 806,
    intelligenceScopeId: scopeId,
    intelligenceProfileId: profileId
  });
  expect(coverage?.properties).not.toHaveProperty('window');
  expect(coverage?.properties).not.toHaveProperty('result');
  expect(trend?.properties).toMatchObject({ x: 64, width: 800 });
  expect(summary).toContain('relatório separado de cobertura do perfil');

  for (const element of screen.elements ?? []) {
    const x = Number(element.properties?.x ?? 0);
    const y = Number(element.properties?.y ?? 0);
    const width = Number(element.properties?.width ?? 0);
    const height = Number(element.properties?.height ?? 0);
    expect(x + width).toBeLessThanOrEqual(1920);
    expect(y + height).toBeLessThanOrEqual(1080);
  }
});

test('guided single-equipment dashboard creates four linked canonical screens without combining analyses', () => {
  const scopeId = '10000000-0000-0000-0000-000000000001';
  const assignmentId = '20000000-0000-0000-0000-000000000002';
  const profileId = '30000000-0000-0000-0000-000000000003';
  const signalIds = [
    '40000000-0000-0000-0000-000000000004',
    '50000000-0000-0000-0000-000000000005',
    '60000000-0000-0000-0000-000000000006',
    '70000000-0000-0000-0000-000000000007',
    '80000000-0000-0000-0000-000000000008'
  ];
  const tagIds = signalIds.map((_, index) => `90000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`);
  const profile: AssetProcessIntelligenceProfileEngineering = {
    id: profileId,
    key: 'pump-profile',
    name: 'Pump profile',
    signals: signalIds.map((id, index) => ({
      id,
      key: `signal-${index + 1}`,
      signalRole: 'process',
      tagId: tagIds[index],
      required: true
    }))
  };
  const tags = signalIds.map((_, index) => numericTag(tagIds[index], `Plant.P-101.Signal${index + 1}`));
  const screens = generateIntelligenceGuidedDashboardDrafts([], {
    kind: 'equipment-profile',
    stableKey: 'Plant.P-101/pump-profile',
    displayName: 'Pump P-101 · Pump profile',
    assignments: [{ equipmentName: 'Pump P-101', equipmentPath: 'Plant.P-101', scopeId, assignmentId, profile }]
  }, tags, {
    performance: {
      scopeId, assignmentId, profileId,
      ruleId: 'specific-active-energy-at-recorded-production-rate.v1',
      ruleVersion: '1.0',
      signalIds: [signalIds[0], signalIds[1]],
      displayName: 'Pump P-101'
    },
    contextualDeviation: {
      scopeId, assignmentId, profileId,
      targetSignalIds: [signalIds[0], signalIds[1]],
      firstContextSignalId: signalIds[2],
      firstContextLowerInclusiveCanonical: 10,
      firstContextUpperExclusiveCanonical: 20,
      secondContextSignalId: signalIds[3],
      secondContextLowerInclusiveCanonical: 100,
      secondContextUpperExclusiveCanonical: 200,
      displayName: 'Pump P-101'
    }
  }, 'pt-BR', ids());

  expect(screens).toHaveLength(4);
  const partial = generateIntelligenceDashboardCompositionDrafts([], {
    kind: 'equipment-profile', stableKey: 'pump', displayName: 'Pump',
    assignments: [{ equipmentName: 'Pump', equipmentPath: 'Plant.Pump', scopeId, assignmentId, profile }]
  }, tags, { performance: {
    scopeId, assignmentId, profileId, ruleId: 'specific-active-energy-at-recorded-production-rate.v1',
    ruleVersion: '1.0', signalIds: [signalIds[0], signalIds[1]], displayName: 'Pump'
  } }, { theme: 'dark', measurements: {} }, 'pt-BR', ids());
  expect(partial).toHaveLength(2);
  expect(partial.map(screen => screen.metadata?.intelligenceDashboardViewRole)).toEqual(['values-trend', 'performance']);
  for (const screen of partial) {
    expect(screen.elements?.filter(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.button)).toHaveLength(1);
    expect(screen.elements?.some(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceContextualDeviationV2)).toBe(false);
  }
  expect(screens.map(screen => screen.metadata?.intelligenceDashboardViewRole)).toEqual([
    'values-trend', 'profile-coverage', 'performance', 'contextual-deviation'
  ]);
  expect(screens.every(screen => screen.metadata?.intelligenceDashboardSetVersion === 'stage5b.9')).toBe(true);
  expect(new Set(screens.map(screen => screen.key)).size).toBe(4);
  expect(new Set(screens.map(screen => screen.route)).size).toBe(4);
  expect(screens[0].elements?.some(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1)).toBe(false);

  for (const screen of screens) {
    const buttons = screen.elements?.filter(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.button) ?? [];
    expect(buttons).toHaveLength(3);
    expect(buttons.every(button => screen.key !== button.actions?.[0]?.targetKey)).toBe(true);
    expect(buttons.every(button => screens.some(target => target.key === button.actions?.[0]?.targetKey))).toBe(true);
    expect(buttons.every(button => button.actions?.[0]?.kind === 'NavigateScreen')).toBe(true);
    for (const element of screen.elements ?? []) {
      const x = Number(element.properties?.x ?? 0);
      const y = Number(element.properties?.y ?? 0);
      const width = Number(element.properties?.width ?? 0);
      const height = Number(element.properties?.height ?? 0);
      expect(x + width).toBeLessThanOrEqual(1920);
      expect(y + height).toBeLessThanOrEqual(1080);
    }
  }

  const performance = screens[2].elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligencePerformanceV1B);
  const deviation = screens[3].elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceContextualDeviationV2);
  expect(performance?.properties).toMatchObject({ intelligenceScopeId: scopeId, intelligenceAssignmentId: assignmentId, intelligenceProfileId: profileId });
  expect(deviation?.properties).toMatchObject({ intelligenceScopeId: scopeId, intelligenceAssignmentId: assignmentId, intelligenceProfileId: profileId });
  expect(JSON.stringify(screens)).not.toMatch(/"(?:window|result)"\s*:/i);
});

test('guided dashboard fails closed for missing selections and inconsistent assignment references', () => {
  const scopeId = '10000000-0000-0000-0000-000000000001';
  const assignmentId = '20000000-0000-0000-0000-000000000002';
  const profileId = '30000000-0000-0000-0000-000000000003';
  const signalIds = [
    '40000000-0000-0000-0000-000000000004',
    '50000000-0000-0000-0000-000000000005',
    '60000000-0000-0000-0000-000000000006',
    '70000000-0000-0000-0000-000000000007'
  ];
  const profile: AssetProcessIntelligenceProfileEngineering = {
    id: profileId, key: 'complete', name: 'Complete',
    signals: signalIds.map((id, index) => ({ id, key: `signal-${index}`, signalRole: 'process', tagId: `tag-${index}`, required: true }))
  };
  const source = {
    kind: 'equipment-profile' as const,
    stableKey: 'Pump/profile', displayName: 'Pump',
    assignments: [{ equipmentName: 'Pump', equipmentPath: 'Plant.Pump', scopeId, assignmentId, profile }]
  };
  const selections = {
    performance: {
      scopeId, assignmentId, profileId, ruleId: 'rule.v1', ruleVersion: '1.0', signalIds: signalIds.slice(0, 2), displayName: 'Pump'
    },
    contextualDeviation: {
      scopeId, assignmentId, profileId, targetSignalIds: signalIds.slice(0, 2),
      firstContextSignalId: signalIds[2], firstContextLowerInclusiveCanonical: 0, firstContextUpperExclusiveCanonical: 1,
      secondContextSignalId: signalIds[3], secondContextLowerInclusiveCanonical: 0, secondContextUpperExclusiveCanonical: 1,
      displayName: 'Pump'
    }
  } as const;
  const tags = signalIds.map((_, index) => numericTag(`tag-${index}`, `Plant.Pump.Signal${index}`));

  expect(() => generateIntelligenceGuidedDashboardDrafts([], source, tags, { ...selections, performance: null as never }, 'en', ids()))
    .toThrow(/valid Performance rule/);
  expect(() => generateIntelligenceGuidedDashboardDrafts([], {
    ...source,
    assignments: [{ ...source.assignments[0], assignmentId: '21000000-0000-0000-0000-000000000002' }]
  }, tags, selections, 'en', ids())).toThrow(/current Equipment assignment/);
  expect(() => generateIntelligenceGuidedDashboardDrafts([], source, tags.slice(1), selections, 'en', ids()))
    .toThrow(/required profile TAG binding/);
});

test('composite dashboard limits value cards to the bounded two-column area and reports omissions', () => {
  const signals = Array.from({ length: INTELLIGENCE_PROFILE_COMPOSITE_MAX_VALUE_CARDS + 2 }, (_, index) => ({
    id: `signal-${index}`, key: `signal-${index}`, signalRole: 'process', tagId: `tag-${index}`, required: true
  }));
  const profile: AssetProcessIntelligenceProfileEngineering = {
    id: '30000000-0000-0000-0000-000000000003', key: 'many-signals', name: 'Many signals', signals
  };
  const screen = generateIntelligenceDashboardDraft([], {
    kind: 'equipment-profile', stableKey: 'Plant.P-101/many-signals', displayName: 'Pump P-101',
    assignments: [{ ...equipmentAssignment, scopeId: '10000000-0000-0000-0000-000000000001', profile }]
  }, signals.map(signal => numericTag(signal.tagId, `Plant.P-101.${signal.key}`)), 'pt-BR', ids());
  const values = screen.elements?.filter(element =>
    element.type === BUILTIN_VISUAL_OBJECT_TYPES.measurementBar || element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay) ?? [];

  expect(values).toHaveLength(INTELLIGENCE_PROFILE_COMPOSITE_MAX_VALUE_CARDS);
  expect(screen.metadata?.intelligenceDashboardSummary).toContain('2 sinais numéricos adicionais omitidos');
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

test('profile coverage screen draft stores only stable references and creates collision-safe canonical Screens', () => {
  const selection: IntelligenceProfileCoverageDashboardSelection = {
    scopeId: '10000000-0000-0000-0000-000000000001',
    profileId: '30000000-0000-0000-0000-000000000003',
    displayName: 'Pump P-101'
  };
  const first = generateIntelligenceProfileCoverageScreenDraft([], selection, 'en', ids());
  const second = generateIntelligenceProfileCoverageScreenDraft([first], selection, 'en', ids());
  const coverage = first.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1);

  expect(first.key).toBe('intelligence-coverage-pump-p-101');
  expect(second.key).toBe(`${first.key}-2`);
  expect(second.route).toBe(`${first.route}-2`);
  expect(first.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.5',
    intelligenceProfileCoverageProfileId: selection.profileId
  });
  expect(coverage?.properties).toMatchObject({
    intelligenceScopeId: selection.scopeId,
    intelligenceProfileId: selection.profileId
  });
  expect(coverage?.bindings).toBeUndefined();
  expect(coverage?.properties).not.toHaveProperty('result');
  expect(coverage?.properties).not.toHaveProperty('window');
  expect(first.metadata).not.toHaveProperty('intelligenceProfileCoverageResult');
  expect(first.metadata).not.toHaveProperty('intelligenceProfileCoverageWindow');
});

test('profile coverage screen draft rejects non-canonical stable references', () => {
  expect(() => generateIntelligenceProfileCoverageScreenDraft([], {
    scopeId: 'not-a-scope-uuid',
    profileId: '30000000-0000-0000-0000-000000000003',
    displayName: 'Pump P-101'
  }, 'en', ids())).toThrow(/canonical Equipment scope and profile UUIDs/);
});

test('process-stage coverage screen stores ordered independent stable member references only', () => {
  const selection = {
    stageId: '10000000-0000-0000-0000-000000000001',
    stageKey: 'pump-train',
    stageName: 'Pumping train',
    members: [
      {
        assignmentId: '30000000-0000-0000-0000-000000000003',
        scopeId: '40000000-0000-0000-0000-000000000004',
        profileId: '50000000-0000-0000-0000-000000000005',
        displayLabel: 'Pump P-102 · Pump profile', order: 1
      },
      {
        assignmentId: '20000000-0000-0000-0000-000000000002',
        scopeId: '60000000-0000-0000-0000-000000000006',
        profileId: '70000000-0000-0000-0000-000000000007',
        displayLabel: 'Pump P-101 · Pump profile', order: 0
      }
    ]
  } as const;
  const first = generateIntelligenceProcessStageCoverageScreenDraft([], selection, 'pt-BR', ids());
  const second = generateIntelligenceProcessStageCoverageScreenDraft([first], selection, 'pt-BR', ids());
  const visual = first.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageCoverageV1);
  const members = JSON.parse(String(visual?.properties?.intelligenceStageMembers)) as Array<{ assignmentId: string; order: number }>;

  expect(first.key).toBe('intelligence-stage-coverage-pump-train');
  expect(second.key).toBe(`${first.key}-2`);
  expect(second.route).toBe(`${first.route}-2`);
  expect(first.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.7',
    intelligenceProcessStageCoverageStageId: selection.stageId
  });
  expect(visual?.properties).toMatchObject({
    intelligenceStageId: selection.stageId,
    intelligenceStageKey: selection.stageKey,
    intelligenceStageName: selection.stageName
  });
  expect(members.map(member => member.assignmentId)).toEqual([
    '20000000-0000-0000-0000-000000000002',
    '30000000-0000-0000-0000-000000000003'
  ]);
  expect(visual?.properties).not.toHaveProperty('window');
  expect(visual?.properties).not.toHaveProperty('result');
});

test('process-stage coverage screen rejects missing, duplicate, unbounded and unordered member references', () => {
  const base = {
    stageId: '10000000-0000-0000-0000-000000000001',
    stageKey: 'pump-train',
    stageName: 'Pumping train',
    members: [{
      assignmentId: '20000000-0000-0000-0000-000000000002',
      scopeId: '30000000-0000-0000-0000-000000000003',
      profileId: '40000000-0000-0000-0000-000000000004',
      displayLabel: 'Pump P-101 · Pump profile', order: 0
    }]
  } as const;
  expect(() => generateIntelligenceProcessStageCoverageScreenDraft([], { ...base, members: [] }, 'en', ids())).toThrow(/one to 32/);
  expect(() => generateIntelligenceProcessStageCoverageScreenDraft([], {
    ...base, members: [{ ...base.members[0], assignmentId: 'invalid' }]
  }, 'en', ids())).toThrow(/one to 32/);
  expect(() => generateIntelligenceProcessStageCoverageScreenDraft([], {
    ...base, members: Array.from({ length: INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS + 1 }, (_, index) => ({
      ...base.members[0], assignmentId: `20000000-0000-0000-0000-${String(index + 2).padStart(12, '0')}`,
      scopeId: `30000000-0000-0000-0000-${String(index + 2).padStart(12, '0')}`,
      profileId: `40000000-0000-0000-0000-${String(index + 2).padStart(12, '0')}`, order: index
    }))
  }, 'en', ids())).toThrow(/one to 32/);
  expect(() => generateIntelligenceProcessStageCoverageScreenDraft([], {
    ...base, members: [{ ...base.members[0], order: -1 }]
  }, 'en', ids())).toThrow(/one to 32/);
  expect(() => generateIntelligenceProcessStageCoverageScreenDraft([], {
    ...base, members: [base.members[0], { ...base.members[0], order: 1 }]
  }, 'en', ids())).toThrow(/one to 32/);
});

test('guided process-stage dashboard links TAG overview and independent member coverage without aggregation', () => {
  const selection = {
    stageId: '10000000-0000-0000-0000-000000000001',
    stageKey: 'pump-train',
    stageName: 'Pumping train',
    members: [
      {
        assignmentId: '20000000-0000-0000-0000-000000000002',
        scopeId: '30000000-0000-0000-0000-000000000003',
        profileId: '40000000-0000-0000-0000-000000000004',
        displayLabel: 'Pump P-101 · Pump profile', order: 1,
        performance: {
          ruleId: 'specific-active-energy-at-recorded-production-rate.v1', ruleVersion: '1.0.0',
          signalIds: ['80000000-0000-0000-0000-000000000002', '81000000-0000-0000-0000-000000000002']
        },
        contextualDeviation: {
          targetSignalIds: ['80000000-0000-0000-0000-000000000002', '81000000-0000-0000-0000-000000000002'],
          firstContextSignalId: '82000000-0000-0000-0000-000000000002',
          firstContextLowerInclusiveCanonical: 1, firstContextUpperExclusiveCanonical: 5,
          secondContextSignalId: '83000000-0000-0000-0000-000000000002',
          secondContextLowerInclusiveCanonical: 10, secondContextUpperExclusiveCanonical: 20
        }
      },
      {
        assignmentId: '50000000-0000-0000-0000-000000000005',
        scopeId: '60000000-0000-0000-0000-000000000006',
        profileId: '70000000-0000-0000-0000-000000000007',
        displayLabel: 'Pump P-102 · Pump profile', order: 0,
        performance: {
          ruleId: 'specific-active-energy-at-recorded-production-rate.v1', ruleVersion: '1.0.0',
          signalIds: ['80000000-0000-0000-0000-000000000001', '81000000-0000-0000-0000-000000000001']
        },
        contextualDeviation: {
          targetSignalIds: ['80000000-0000-0000-0000-000000000001', '81000000-0000-0000-0000-000000000001'],
          firstContextSignalId: '82000000-0000-0000-0000-000000000001',
          firstContextLowerInclusiveCanonical: 1, firstContextUpperExclusiveCanonical: 5,
          secondContextSignalId: '83000000-0000-0000-0000-000000000001',
          secondContextLowerInclusiveCanonical: 10, secondContextUpperExclusiveCanonical: 20
        }
      }
    ]
  } as const;
  const orderedSelectionMembers = [...selection.members].sort((left, right) => left.order - right.order);
  const source = {
    kind: 'process-stage' as const,
    stableKey: selection.stageKey,
    displayName: selection.stageName,
    assignments: orderedSelectionMembers.map((member, index) => ({
      equipmentName: `Pump P-10${index + 1}`,
      equipmentPath: `Plant.P-10${index + 1}`,
      scopeId: member.scopeId,
      assignmentId: member.assignmentId,
      profile: {
        id: member.profileId,
        key: 'pump-profile',
        name: 'Pump profile',
        signals: [
          { id: `80000000-0000-0000-0000-00000000000${index + 1}`, key: 'flow', signalRole: 'flow', tagId: `tag-flow-${index + 1}`, required: true },
          { id: `81000000-0000-0000-0000-00000000000${index + 1}`, key: 'power', signalRole: 'active-power', tagId: `tag-power-${index + 1}`, required: true },
          { id: `82000000-0000-0000-0000-00000000000${index + 1}`, key: 'temperature', signalRole: 'temperature', tagId: `tag-temperature-${index + 1}`, required: true },
          { id: `83000000-0000-0000-0000-00000000000${index + 1}`, key: 'speed', signalRole: 'speed', tagId: `tag-speed-${index + 1}`, required: true }
        ]
      }
    }))
  };
  const drafts = generateIntelligenceProcessStageGuidedDashboardDrafts([], source, selection, [
    numericTag('tag-flow-1', 'Plant.P-101.Flow'),
    numericTag('tag-flow-2', 'Plant.P-102.Flow')
  ], 'en', ids());
  const [overview, coverage] = drafts;
  const overviewNavigation = overview.elements?.find(element => element.key === 'intelligence-stage-guided-navigation-2');
  const coverageNavigation = coverage.elements?.find(element => element.key === 'intelligence-stage-guided-navigation-1');
  const coverageVisual = coverage.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageCoverageV1);
  const members = JSON.parse(String(coverageVisual?.properties?.intelligenceStageMembers)) as Array<{ assignmentId: string }>;
  const elementIds = drafts.flatMap(screen => (screen.elements ?? []).map(element => element.id));
  const elements = drafts.flatMap(screen => screen.elements ?? []);

  expect(drafts).toHaveLength(2);
  expect(new Set(drafts.map(screen => screen.key)).size).toBe(2);
  expect(new Set(drafts.map(screen => screen.route)).size).toBe(2);
  expect(new Set(elementIds).size).toBe(elementIds.length);
  expect(overview.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.10',
    intelligenceDashboardSetRootKey: overview.key,
    intelligenceDashboardViewRole: 'values-trend'
  });
  expect(coverage.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.10',
    intelligenceDashboardSetRootKey: overview.key,
    intelligenceDashboardViewRole: 'process-stage-coverage'
  });
  expect(overview.elements?.filter(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay)).toHaveLength(2);
  expect(overview.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.trend)?.properties?.pens).toHaveLength(2);
  expect(overview.metadata?.intelligenceDashboardSummary).toContain('does not calculate KPIs');
  expect(members.map(member => member.assignmentId)).toEqual(orderedSelectionMembers.map(member => member.assignmentId));
  expect(overviewNavigation?.actions).toEqual([expect.objectContaining({ kind: 'NavigateScreen', targetKey: coverage.key })]);
  expect(coverageNavigation?.actions).toEqual([expect.objectContaining({ kind: 'NavigateScreen', targetKey: overview.key })]);
  expect(coverageVisual?.properties).not.toHaveProperty('result');
  expect(coverageVisual?.properties).not.toHaveProperty('window');
  for (const element of elements) {
    const { x, y, width, height } = element.properties;
    if ([x, y, width, height].every(value => typeof value === 'number')) {
      expect(x as number).toBeGreaterThanOrEqual(0);
      expect(y as number).toBeGreaterThanOrEqual(0);
      expect((x as number) + (width as number)).toBeLessThanOrEqual(1920);
      expect((y as number) + (height as number)).toBeLessThanOrEqual(1080);
    }
  }
});

test('guided process-stage dashboard rejects incomplete or mismatched canonical members before returning drafts', () => {
  const selection = {
    stageId: '10000000-0000-0000-0000-000000000001',
    stageKey: 'pump-train',
    stageName: 'Pumping train',
    members: [{
      assignmentId: '20000000-0000-0000-0000-000000000002',
      scopeId: '30000000-0000-0000-0000-000000000003',
      profileId: '40000000-0000-0000-0000-000000000004',
      displayLabel: 'Pump P-101 · Pump profile', order: 0,
      performance: { ruleId: 'rule', ruleVersion: '1.0.0', signalIds: ['80000000-0000-0000-0000-000000000001', '81000000-0000-0000-0000-000000000001'] },
      contextualDeviation: {
        targetSignalIds: ['80000000-0000-0000-0000-000000000001', '81000000-0000-0000-0000-000000000001'],
        firstContextSignalId: '82000000-0000-0000-0000-000000000001', firstContextLowerInclusiveCanonical: 1,
        firstContextUpperExclusiveCanonical: 5, secondContextSignalId: '83000000-0000-0000-0000-000000000001',
        secondContextLowerInclusiveCanonical: 10, secondContextUpperExclusiveCanonical: 20
      }
    }]
  } as const;
  const source = {
    kind: 'process-stage' as const, stableKey: selection.stageKey, displayName: selection.stageName,
    assignments: [{
      equipmentName: 'Pump P-101', equipmentPath: 'Plant.P-101', scopeId: selection.members[0].scopeId,
      assignmentId: selection.members[0].assignmentId,
      profile: { id: selection.members[0].profileId, key: 'pump-profile', name: 'Pump profile', signals: [] }
    }]
  };
  expect(() => generateIntelligenceProcessStageGuidedDashboardDrafts([], source, {
    ...selection, members: [{ ...selection.members[0], scopeId: '50000000-0000-0000-0000-000000000005' }]
  }, [numericTag('tag-flow', 'Plant.P-101.Flow')], 'en', ids())).toThrow(/stable assignment/);
  expect(() => generateIntelligenceProcessStageGuidedDashboardDrafts([], {
    ...source, stableKey: 'different-stage'
  }, selection, [numericTag('tag-flow', 'Plant.P-101.Flow')], 'en', ids())).toThrow(/matching canonical member references/);
  expect(() => generateIntelligenceProcessStageGuidedDashboardDrafts([], {
    ...source, assignments: [source.assignments[0], source.assignments[0]]
  }, { ...selection, members: [selection.members[0], {
    ...selection.members[0],
    assignmentId: '50000000-0000-0000-0000-000000000005',
    scopeId: '60000000-0000-0000-0000-000000000006',
    profileId: '70000000-0000-0000-0000-000000000007',
    displayLabel: 'Pump P-102 · Pump profile', order: 1
  }] }, [numericTag('tag-flow', 'Plant.P-101.Flow')], 'en', ids())).toThrow(/every member to match/);
  expect(() => generateIntelligenceProcessStageGuidedDashboardDrafts([], source, {
    ...selection, members: []
  }, [numericTag('tag-flow', 'Plant.P-101.Flow')], 'en', ids())).toThrow(/one to 32/);
});

test('process-stage member-insights screen stores stable per-member report selections without result state', () => {
  const selection = {
    stageId: '10000000-0000-0000-0000-000000000001',
    stageKey: 'pump-train',
    stageName: 'Pumping train',
    members: [{
      assignmentId: '20000000-0000-0000-0000-000000000002',
      scopeId: '30000000-0000-0000-0000-000000000003',
      profileId: '40000000-0000-0000-0000-000000000004',
      displayLabel: 'Pump P-101 · Pump profile', order: 0,
      performance: {
        ruleId: 'specific-active-energy-at-recorded-production-rate.v1', ruleVersion: '1.0.0',
        signalIds: [
          '80000000-0000-0000-0000-000000000001',
          '81000000-0000-0000-0000-000000000001'
        ]
      },
      contextualDeviation: {
        targetSignalIds: [
          '80000000-0000-0000-0000-000000000001',
          '81000000-0000-0000-0000-000000000001'
        ],
        firstContextSignalId: '82000000-0000-0000-0000-000000000001',
        firstContextLowerInclusiveCanonical: 1, firstContextUpperExclusiveCanonical: 5,
        secondContextSignalId: '83000000-0000-0000-0000-000000000001',
        secondContextLowerInclusiveCanonical: 10, secondContextUpperExclusiveCanonical: 20
      }
    }]
  } as const;
  const source = {
    kind: 'process-stage' as const, stableKey: selection.stageKey, displayName: selection.stageName,
    assignments: [{
      equipmentName: 'Pump P-101', equipmentPath: 'Plant.P-101', scopeId: selection.members[0].scopeId,
      assignmentId: selection.members[0].assignmentId,
      profile: {
        id: selection.members[0].profileId, key: 'pump-profile', name: 'Pump profile',
        signals: [
          { id: '80000000-0000-0000-0000-000000000001', key: 'power', signalRole: 'active-power', tagId: 'tag-power', required: true },
          { id: '81000000-0000-0000-0000-000000000001', key: 'rate', signalRole: 'production-rate', tagId: 'tag-rate', required: true },
          { id: '82000000-0000-0000-0000-000000000001', key: 'pressure', signalRole: 'pressure', tagId: 'tag-pressure', required: true },
          { id: '83000000-0000-0000-0000-000000000001', key: 'flow', signalRole: 'flow', tagId: 'tag-flow', required: true }
        ]
      }
    }]
  };

  const screen = generateIntelligenceProcessStageInsightsDashboardDraft([], source, selection, 'en', ids());
  const visual = screen.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageInsightsV1);
  const persistedMembers = JSON.parse(String(visual?.properties?.intelligenceStageMembers)) as typeof selection.members;
  expect(screen.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.11',
    intelligenceProcessStageInsightsStageId: selection.stageId
  });
  expect(visual?.properties).toMatchObject({
    intelligenceStageId: selection.stageId,
    intelligenceStageKey: selection.stageKey,
    intelligenceStageName: selection.stageName
  });
  expect(persistedMembers).toEqual(selection.members);
  expect(visual?.properties).not.toHaveProperty('result');
  expect(visual?.properties).not.toHaveProperty('window');
  expect(() => generateIntelligenceProcessStageInsightsDashboardDraft([], source, {
    ...selection,
    members: [{ ...selection.members[0], performance: {
      ...selection.members[0].performance,
      signalIds: ['99000000-0000-0000-0000-000000000009', '98000000-0000-0000-0000-000000000009']
    } }]
  }, 'en', ids())).toThrow(/belonging to every stable member profile/);
});

test('stage composition preserves independent member reports, authored widgets and a complete bounded navigation graph', () => {
  const id = (n: number) => `85500000-0000-0000-0000-${String(n).padStart(12, '0')}`;
  const tags: TagEngineering[] = Array.from({ length: 8 }, (_, i) => ({ id: id(100 + i), name: `Reading ${i}`, path: `Stage.Tag${i}`, dataType: 'Double', readOnly: true }));
  const assignments = [4, 2, 2].map((count, index) => ({
    assignmentId: id(400 + index), scopeId: id(500 + index), equipmentName: `Pump ${index}`, equipmentPath: `Stage.Pump${index}`,
    profile: { id: id(300 + index), key: `profile-${index}`, name: `Profile ${index}`, signals: Array.from({ length: count }, (_, i) => ({
      id: id(200 + index * 10 + i), key: `signal-${i}`, signalRole: `Reading ${i}`, tagId: tags[index === 0 ? i : index === 1 ? 4 + i : 6 + i].id!, required: true
    })) }
  }));
  const source = { kind: 'process-stage' as const, stableKey: 'stage', displayName: 'Pumping', assignments };
  const coverage = { stageId: id(1), stageKey: source.stableKey, stageName: source.displayName,
    members: assignments.map((a, order) => ({ assignmentId: a.assignmentId, scopeId: a.scopeId, profileId: a.profile.id, order, displayLabel: a.equipmentName })) };
  const reports = { ...coverage, members: coverage.members.map((member, index) => ({ ...member, availabilityVersion: 2 as const,
    ...(index < 2 ? { performance: { ruleId: 'specific-active-energy-at-recorded-production-rate.v1', ruleVersion: '1.0.0', signalIds: assignments[index].profile.signals.slice(0, 2).map(s => s.id) } } : {}),
    ...(index === 0 ? { contextualDeviation: { targetSignalIds: assignments[0].profile.signals.slice(0, 2).map(s => s.id),
      firstContextSignalId: assignments[0].profile.signals[2].id, firstContextLowerInclusiveCanonical: 0, firstContextUpperExclusiveCanonical: 10,
      secondContextSignalId: assignments[0].profile.signals[3].id, secondContextLowerInclusiveCanonical: 0, secondContextUpperExclusiveCanonical: 100 } } : {})
  })) };
  const appearance = { theme: 'light' as const, measurements: { [tags[0].id!]: { layout: 'measurementDial' as const, minimum: 0, maximum: 100 } } };
  const drafts = generateIntelligenceProcessStageCompositionDrafts([], source, coverage, reports, tags, appearance, { coverage: true, reports: true }, 'en', ids());
  expect(drafts.map(s => s.metadata?.intelligenceDashboardViewRole)).toEqual(['values-trend', 'process-stage-coverage', 'process-stage-insights']);
  expect(drafts[0].elements?.some(e => e.type === BUILTIN_VISUAL_OBJECT_TYPES.measurementDial)).toBe(true);
  const widget = drafts[2].elements!.find(e => e.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageInsightsV1)!;
  const persisted = JSON.parse(String(widget.properties?.intelligenceStageMembers));
  expect(persisted).toEqual(reports.members);
  expect(persisted[1]).not.toHaveProperty('contextualDeviation');
  expect(persisted[2]).not.toHaveProperty('performance');
  expect(persisted[2]).not.toHaveProperty('contextualDeviation');
  const keys = new Set(drafts.map(s => s.key));
  for (const screen of drafts) {
    const links = screen.elements!.flatMap(e => e.actions ?? []).filter(a => a.kind === 'NavigateScreen');
    expect(links).toHaveLength(2);
    for (const link of links) expect(keys.has(link.targetKey!)).toBe(true);
  }
  expect(() => generateIntelligenceProcessStageInsightsDashboardDraft([], source, reports as any, 'en', ids())).toThrow();
  const bad = { ...reports, members: reports.members.map((m, i) => i === 1 ? { ...m, performance: { ...m.performance!, signalIds: assignments[0].profile.signals.slice(0, 2).map(s => s.id) } } : m) };
  expect(() => generateIntelligenceProcessStageCompositionDrafts([], source, coverage, bad, tags, appearance, { coverage: true, reports: true }, 'en', ids())).toThrow(/belonging/);
  expect(() => generateIntelligenceProcessStageCompositionDrafts([], source, coverage, { ...reports, stageId: id(9) }, tags, appearance, { coverage: true, reports: true }, 'en', ids())).toThrow(/matching/);
  expect(() => generateIntelligenceProcessStageCompositionDrafts([], source, coverage, { ...reports, members: reports.members.map(m => ({ ...m, availabilityVersion: 3 })) } as any, tags, appearance, { coverage: true, reports: true }, 'en', ids())).toThrow();
  const references = { ...coverage, members: coverage.members.map(member => ({ ...member, availabilityVersion: 2 as const, referenceEvidence: true as const })) };
  const referenceDrafts = generateIntelligenceProcessStageCompositionDrafts([], source, coverage, references, tags, appearance, { coverage: false, reports: true }, 'en', ids());
  expect(referenceDrafts).toHaveLength(2);
  const referenceWidget = referenceDrafts[1].elements!.find(e => e.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageInsightsV1)!;
  const referenceMembers = JSON.parse(String(referenceWidget.properties?.intelligenceStageMembers));
  expect(referenceMembers).toEqual(references.members);
  expect(referenceMembers.every((m: any) => m.referenceEvidence && !m.performance && !m.contextualDeviation)).toBe(true);
  for (const marker of [false, null, 'true']) {
    const invalid = { ...references, members: references.members.map(m => ({ ...m, referenceEvidence: marker })) };
    expect(() => generateIntelligenceProcessStageCompositionDrafts([], source, coverage, invalid as any, tags, appearance, { coverage: false, reports: true }, 'en', ids())).toThrow();
  }
  const conditions = { ...coverage, members: coverage.members.map((member, i) => ({ ...member, availabilityVersion: 2 as const,
    ...(i === 0 ? { operatingContexts: [{ id: id(8), displayLabel: 'Running', version: 3, predicateTagIds: [assignments[0].profile.signals[0].tagId] }] } : {}) })) };
  const conditionDrafts = generateIntelligenceProcessStageCompositionDrafts([], source, coverage, conditions, tags, appearance, { coverage: true, reports: true }, 'en', ids());
  expect(conditionDrafts).toHaveLength(3);
  const conditionWidget = conditionDrafts[2].elements!.find(e => e.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageInsightsV1)!;
  expect(JSON.parse(String(conditionWidget.properties?.intelligenceStageMembers))).toEqual(conditions.members);
  for (const value of [null, [], [{ ...conditions.members[0].operatingContexts![0], version: 0 }],
    [{ ...conditions.members[0].operatingContexts![0], predicateTagIds: [id(9)] }],
    [{ ...conditions.members[0].operatingContexts![0], predicates: [{ minimum: 0 }] }]]) {
    const invalid = { ...conditions, members: conditions.members.map((m, i) => i === 0 ? { ...m, operatingContexts: value } : m) };
    expect(() => generateIntelligenceProcessStageCompositionDrafts([], source, coverage, invalid as any, tags, appearance, { coverage: true, reports: true }, 'en', ids())).toThrow();
  }
  const basic = generateIntelligenceProcessStageCompositionDrafts([], source, coverage, null, tags, appearance, { coverage: false, reports: false }, 'en', ids());
  expect(basic).toHaveLength(1);
  expect(basic[0].elements!.flatMap(e => e.actions ?? []).filter(a => a.kind === 'NavigateScreen')).toEqual([]);
});

test('contextual deviation V2 Screen draft stores stable identities and declared canonical ranges only', () => {
  const selection = {
    scopeId: '10000000-0000-0000-0000-000000000001',
    assignmentId: '20000000-0000-0000-0000-000000000002',
    profileId: '30000000-0000-0000-0000-000000000003',
    targetSignalIds: [
      '40000000-0000-0000-0000-000000000004',
      '50000000-0000-0000-0000-000000000005'
    ],
    firstContextSignalId: '60000000-0000-0000-0000-000000000006',
    firstContextLowerInclusiveCanonical: 5,
    firstContextUpperExclusiveCanonical: 25,
    secondContextSignalId: '70000000-0000-0000-0000-000000000007',
    secondContextLowerInclusiveCanonical: 100,
    secondContextUpperExclusiveCanonical: 220,
    displayName: 'Pump P-101'
  } as const;
  const first = generateIntelligenceContextualDeviationScreenDraft([], selection, 'pt-BR', ids());
  const second = generateIntelligenceContextualDeviationScreenDraft([first], selection, 'pt-BR', ids());
  const visual = first.elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceContextualDeviationV2);

  expect(first.key).toBe('intelligence-contextual-deviation-pump-p-101');
  expect(second.key).toBe(`${first.key}-2`);
  expect(second.route).toBe(`${first.route}-2`);
  expect(first.metadata).toMatchObject({
    intelligenceDashboardGenerator: 'stage5b.6',
    intelligenceContextualDeviationRuleId: 'contextual-deviation-vector.v2',
    intelligenceContextualDeviationRuleVersion: '2.0.0'
  });
  expect(visual?.properties).toMatchObject({
    intelligenceScopeId: selection.scopeId,
    intelligenceAssignmentId: selection.assignmentId,
    intelligenceProfileId: selection.profileId,
    intelligenceSignalIds: JSON.stringify(selection.targetSignalIds),
    intelligenceFirstContextSignalId: selection.firstContextSignalId,
    intelligenceFirstContextLowerInclusiveCanonical: selection.firstContextLowerInclusiveCanonical,
    intelligenceFirstContextUpperExclusiveCanonical: selection.firstContextUpperExclusiveCanonical,
    intelligenceSecondContextSignalId: selection.secondContextSignalId,
    intelligenceSecondContextLowerInclusiveCanonical: selection.secondContextLowerInclusiveCanonical,
    intelligenceSecondContextUpperExclusiveCanonical: selection.secondContextUpperExclusiveCanonical
  });
  expect(visual?.bindings).toBeUndefined();
  expect(visual?.properties).not.toHaveProperty('referenceWindow');
  expect(visual?.properties).not.toHaveProperty('evaluationWindow');
  expect(visual?.properties).not.toHaveProperty('evaluationVectors');
});

test('contextual deviation V2 Screen draft rejects overlapping identities, too many targets or invalid ranges', () => {
  const valid = {
    scopeId: '10000000-0000-0000-0000-000000000001',
    assignmentId: '20000000-0000-0000-0000-000000000002',
    profileId: '30000000-0000-0000-0000-000000000003',
    targetSignalIds: ['40000000-0000-0000-0000-000000000004', '50000000-0000-0000-0000-000000000005'],
    firstContextSignalId: '60000000-0000-0000-0000-000000000006',
    firstContextLowerInclusiveCanonical: 5,
    firstContextUpperExclusiveCanonical: 25,
    secondContextSignalId: '70000000-0000-0000-0000-000000000007',
    secondContextLowerInclusiveCanonical: 100,
    secondContextUpperExclusiveCanonical: 220,
    displayName: 'Pump P-101'
  } as const;
  expect(() => generateIntelligenceContextualDeviationScreenDraft([], {
    ...valid, targetSignalIds: [valid.targetSignalIds[0], valid.firstContextSignalId]
  }, 'en', ids())).toThrow(/unique signal UUIDs/);
  expect(() => generateIntelligenceContextualDeviationScreenDraft([], {
    ...valid, targetSignalIds: Array.from({ length: 9 }, (_, index) => `${String(index + 1).padStart(8, '0')}-0000-0000-0000-000000000000`)
  }, 'en', ids())).toThrow(/two to eight targets/);
  expect(() => generateIntelligenceContextualDeviationScreenDraft([], {
    ...valid, firstContextLowerInclusiveCanonical: Number.NaN
  }, 'en', ids())).toThrow(/finite lower-inclusive/);
});

test('numeric source filtering excludes state and text TAGs', () => {
  expect(isNumericTagDataType('Double')).toBe(true);
  expect(isNumericTagDataType('Float32')).toBe(true);
  expect(isNumericTagDataType('Boolean')).toBe(false);
  expect(isNumericTagDataType('String')).toBe(false);
});

test('reference evidence is an independent canonical view with stable references and complete navigation', () => {
  const scopeId = '10000000-0000-0000-0000-000000000001';
  const profileId = '30000000-0000-0000-0000-000000000003';
  const selection = { scopeId, profileId, displayName: 'Pump reference' };
  const source = { kind: 'equipment-profile' as const, stableKey: 'pump', displayName: 'Pump', assignments: [
    { ...equipmentAssignment, scopeId, profile: { ...equipmentAssignment.profile, id: profileId } }
  ] };
  const drafts = generateIntelligenceDashboardCompositionDrafts([], source, [numericTag('tag-current', 'Plant.Current')],
    { referenceEvidence: selection }, { theme: 'dark', measurements: {} }, 'en', ids());
  expect(drafts).toHaveLength(2);
  const report = drafts[1].elements?.find(element => element.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceReferenceEvidenceV1);
  expect(report?.properties).toMatchObject({ intelligenceScopeId: scopeId, intelligenceProfileId: profileId });
  expect(JSON.stringify(report)).not.toMatch(/referenceWindow|comparisonWindow|referenceCohort|password|result/);
  for (const screen of drafts) {
    const links = screen.elements?.flatMap(element => element.actions ?? []) ?? [];
    expect(links).toHaveLength(1);
    expect(drafts.some(target => target.key === links[0].targetKey && target.key !== screen.key)).toBe(true);
  }
  expect(() => generateIntelligenceDashboardCompositionDrafts([], source, [numericTag('tag-current', 'Plant.Current')], {
    referenceEvidence: { ...selection, scopeId: '90000000-0000-0000-0000-000000000009' }
  }, { theme: 'dark', measurements: {} }, 'en', ids())).toThrow(/Reference evidence selection/);
  expect(() => generateIntelligenceReferenceEvidenceScreenDraft([], { ...selection, profileId: 'invalid' }, 'en')).toThrow();
});

test('context conditions persist references only and five selected views keep navigation inside the canvas', () => {
  const id = (n: number) => `85700000-0000-0000-0000-${String(n).padStart(12, '0')}`;
  const tags = [1, 2, 3, 4].map(n => ({ ...numericTag(id(100 + n), `Pump.Signal${n}`), id: id(100 + n) }));
  const profile = { id: id(10), key: 'pump', name: 'Pump', signals: tags.map((tag, i) => ({ id: id(200 + i), key: `s${i}`, signalRole: `Signal ${i}`, tagId: tag.id, required: true })) };
  const identity = { scopeId: id(20), assignmentId: id(30), profileId: profile.id, displayName: 'Pump' };
  const contexts = [{ id: id(40), displayLabel: 'Normal operation', version: 3, predicateTagIds: [id(300)] }];
  const contextSelection = { ...identity, contexts };
  const source = { kind: 'equipment-profile' as const, stableKey: 'pump', displayName: 'Pump', assignments: [{ scopeId: identity.scopeId, assignmentId: identity.assignmentId, profile, equipmentName: 'Pump', equipmentPath: 'Pump' }] };
  const selections = { referenceEvidence: identity, contextEligibility: contextSelection,
    performance: { ...identity, ruleId: 'specific-active-energy-at-recorded-production-rate.v1', ruleVersion: '1.0.0', signalIds: profile.signals.slice(0, 2).map(s => s.id) },
    contextualDeviation: { ...identity, targetSignalIds: profile.signals.slice(0, 2).map(s => s.id), firstContextSignalId: profile.signals[2].id,
      secondContextSignalId: profile.signals[3].id, firstContextLowerInclusiveCanonical: 0, firstContextUpperExclusiveCanonical: 10,
      secondContextLowerInclusiveCanonical: 0, secondContextUpperExclusiveCanonical: 100 } };
  const drafts = generateIntelligenceDashboardCompositionDrafts([], source, tags, selections, { theme: 'light', measurements: {} }, 'en', ids());
  expect(drafts).toHaveLength(5);
  const conditions = drafts.find(draft => draft.metadata?.intelligenceDashboardViewRole === 'context-eligibility')!;
  const widget = conditions.elements!.find(e => e.type === BUILTIN_VISUAL_OBJECT_TYPES.intelligenceContextEligibilityV1)!;
  expect(JSON.parse(String(widget.properties?.intelligenceOperatingContexts))).toEqual(contexts);
  expect(widget.properties).toMatchObject({ intelligenceScopeId: identity.scopeId, intelligenceAssignmentId: identity.assignmentId, intelligenceProfileId: identity.profileId });
  expect(widget.properties).not.toHaveProperty('referenceWindow');
  expect(JSON.stringify(widget)).not.toMatch(/minimum|maximum|exactValue|booleanValue|referenceWindow|result/);
  for (const draft of drafts) {
    const navigation = draft.elements!.filter(e => e.actions?.some(a => a.kind === 'NavigateScreen'));
    expect(navigation).toHaveLength(4);
    for (const link of navigation) {
      expect(Number(link.properties!.x) + Number(link.properties!.width)).toBeLessThanOrEqual(1920);
      expect(Number(link.properties!.y) + Number(link.properties!.height)).toBeLessThanOrEqual(150);
      expect(drafts.some(target => target.key === link.actions![0].targetKey)).toBe(true);
    }
  }
  for (const invalid of [[], [...contexts, ...contexts], [{ ...contexts[0], version: 0 }], [{ ...contexts[0], predicates: [{ minimum: 20 }] }]]) {
    expect(() => generateIntelligenceContextEligibilityScreenDraft([], { ...identity, contexts: invalid as any }, 'en')).toThrow();
  }
  expect(() => generateIntelligenceDashboardCompositionDrafts([], source, tags, { contextEligibility: { ...contextSelection, assignmentId: id(99) } }, { theme: 'dark', measurements: {} }, 'en')).toThrow(/does not belong/);
});
