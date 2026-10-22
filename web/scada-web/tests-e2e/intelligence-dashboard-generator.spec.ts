import { expect, test } from '@playwright/test';
import type {
  AssetProcessIntelligenceProfileEngineering,
  ScreenEngineering,
  TagEngineering
} from '../src/engineering/types';
import {
  generateIntelligenceDashboardDraft,
  generateIntelligenceContextualDeviationScreenDraft,
  generateIntelligenceGuidedDashboardDrafts,
  generateIntelligenceProfileCoverageScreenDraft,
  generateIntelligenceProcessStageCoverageScreenDraft,
  generateIntelligenceProcessStageGuidedDashboardDrafts,
  generateIntelligencePerformanceScreenDraft,
  INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS,
  INTELLIGENCE_PROFILE_COMPOSITE_MAX_VALUE_CARDS,
  INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS,
  isNumericTagDataType,
  type IntelligenceProfileCoverageDashboardSelection
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
  expect(screen.metadata?.intelligenceDashboardSummary).toContain('A cobertura do perfil foi omitida');
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
        displayLabel: 'Pump P-101 · Pump profile', order: 1
      },
      {
        assignmentId: '50000000-0000-0000-0000-000000000005',
        scopeId: '60000000-0000-0000-0000-000000000006',
        profileId: '70000000-0000-0000-0000-000000000007',
        displayLabel: 'Pump P-102 · Pump profile', order: 0
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
        signals: [{ id: `80000000-0000-0000-0000-00000000000${index + 1}`, key: 'flow', signalRole: 'flow', tagId: `tag-flow-${index + 1}`, required: true }]
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
      displayLabel: 'Pump P-101 · Pump profile', order: 0
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
