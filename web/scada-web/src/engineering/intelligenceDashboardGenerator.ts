import type { EngineeringLocale } from './i18n';
import type {
  AssetProcessIntelligenceProfileEngineering,
  AssetProcessIntelligenceSignalEngineering,
  ScreenEngineering,
  TagEngineering
} from './types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  createTrendPen,
  MAX_TREND_PENS,
  trendPensEngineeringValue,
  VISUAL_PROPERTY_KEYS
} from '../visual-runtime';
import { createScreenDraft } from './visual-editor/visualEditorCanonicalModel';

export const INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS = 15;
export const INTELLIGENCE_PROFILE_COMPOSITE_MAX_VALUE_CARDS = 8;
export const INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS = 32;
const NUMERIC_DATA_TYPES = new Set([
  'byte', 'sbyte', 'int8', 'uint8', 'short', 'int16', 'uint16', 'int', 'integer', 'int32', 'uint32',
  'long', 'int64', 'uint64', 'single', 'float', 'float32', 'double', 'float64', 'decimal', 'number'
]);

export type IntelligenceDashboardAssignmentSource = Readonly<{
  equipmentName: string;
  equipmentPath: string;
  /** Canonical Equipment scope identity; required for the optional coverage panel. */
  scopeId?: string;
  /** Canonical assignment identity; required for a guided single-equipment set. */
  assignmentId?: string;
  profile: AssetProcessIntelligenceProfileEngineering;
}>;

export type IntelligenceDashboardSource = Readonly<{
  kind: 'equipment-profile' | 'process-stage';
  stableKey: string;
  displayName: string;
  assignments: readonly IntelligenceDashboardAssignmentSource[];
}>;

export type IntelligencePerformanceDashboardSelection = Readonly<{
  scopeId: string;
  assignmentId: string;
  profileId: string;
  ruleId: string;
  ruleVersion: string;
  signalIds: readonly string[];
  displayName: string;
}>;

export type IntelligenceProfileCoverageDashboardSelection = Readonly<{
  scopeId: string;
  profileId: string;
  displayName: string;
}>;

export type IntelligenceContextualDeviationDashboardSelection = Readonly<{
  scopeId: string;
  assignmentId: string;
  profileId: string;
  targetSignalIds: readonly string[];
  firstContextSignalId: string;
  firstContextLowerInclusiveCanonical: number;
  firstContextUpperExclusiveCanonical: number;
  secondContextSignalId: string;
  secondContextLowerInclusiveCanonical: number;
  secondContextUpperExclusiveCanonical: number;
  displayName: string;
}>;

export type IntelligenceProcessStageCoverageDashboardSelection = Readonly<{
  stageId: string;
  stageKey: string;
  stageName: string;
  members: readonly Readonly<{
    assignmentId: string;
    scopeId: string;
    profileId: string;
    displayLabel: string;
    order: number;
  }>[];
}>;

export type IntelligenceProcessStageInsightsDashboardMember = IntelligenceProcessStageCoverageDashboardSelection['members'][number] & Readonly<{
  performance: Pick<IntelligencePerformanceDashboardSelection, 'ruleId' | 'ruleVersion' | 'signalIds'>;
  contextualDeviation: Pick<IntelligenceContextualDeviationDashboardSelection,
    'targetSignalIds' | 'firstContextSignalId' | 'firstContextLowerInclusiveCanonical' |
    'firstContextUpperExclusiveCanonical' | 'secondContextSignalId' |
    'secondContextLowerInclusiveCanonical' | 'secondContextUpperExclusiveCanonical'>;
}>;

export type IntelligenceProcessStageGuidedDashboardSelection = IntelligenceProcessStageCoverageDashboardSelection;

export type IntelligenceProcessStageInsightsDashboardSelection = Omit<IntelligenceProcessStageCoverageDashboardSelection, 'members'> & Readonly<{
  members: readonly IntelligenceProcessStageInsightsDashboardMember[];
}>;

type ResolvedSignal = Readonly<{
  assignment: IntelligenceDashboardAssignmentSource;
  signal: AssetProcessIntelligenceSignalEngineering;
  tag: TagEngineering & { id: string };
}>;

type DashboardText = Readonly<{
  generatedTitle: (source: string) => string;
  assignmentSubtitle: (source: IntelligenceDashboardSource) => string;
  signalLabel: (item: ResolvedSignal) => string;
  summary: (counts: Readonly<{
    shown: number;
    supported: number;
    missing: number;
    nonNumeric: number;
    duplicates: number;
    omitted: number;
    trended: number;
    profileCoverage: 'included' | 'unavailable' | 'not-applicable';
  }>) => string;
  noNumericSignals: string;
}>;

export function generateIntelligenceDashboardDraft(
  existingScreens: readonly ScreenEngineering[],
  source: IntelligenceDashboardSource,
  tags: readonly TagEngineering[],
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): ScreenEngineering {
  return generateDashboardDraft(existingScreens, source, tags, locale, createId, true);
}

function generateDashboardDraft(
  existingScreens: readonly ScreenEngineering[],
  source: IntelligenceDashboardSource,
  tags: readonly TagEngineering[],
  locale: EngineeringLocale,
  createId: () => string,
  includeProfileCoverage: boolean
): ScreenEngineering {
  const copy = dashboardText(locale);
  const tagsById = new Map(tags.filter((tag): tag is TagEngineering & { id: string } => Boolean(tag.id?.trim()))
    .map(tag => [tag.id.toLowerCase(), tag]));
  const seenTagIds = new Set<string>();
  const resolved: ResolvedSignal[] = [];
  let missing = 0;
  let nonNumeric = 0;
  let duplicates = 0;

  for (const assignment of source.assignments) {
    for (const signal of assignment.profile.signals) {
      const identity = signal.tagId?.trim().toLowerCase();
      const tag = identity ? tagsById.get(identity) : undefined;
      if (!tag) {
        missing++;
        continue;
      }
      if (seenTagIds.has(tag.id.toLowerCase())) {
        duplicates++;
        continue;
      }
      seenTagIds.add(tag.id.toLowerCase());
      if (!isNumericTagDataType(tag.dataType)) {
        nonNumeric++;
        continue;
      }
      resolved.push({ assignment, signal, tag });
    }
  }
  if (resolved.length === 0) throw new Error(copy.noNumericSignals);

  const draft = createScreenDraft(existingScreens, locale);
  const key = uniqueScreenKey(existingScreens, `intelligence-${slug(source.stableKey)}`);
  const route = uniqueScreenRoute(key, existingScreens.map(screen => screen.route));
  const profileAssignment = source.kind === 'equipment-profile' && source.assignments.length === 1
    ? source.assignments[0]
    : null;
  const profileCoverageAvailable = Boolean(profileAssignment &&
    isCanonicalUuid(profileAssignment.scopeId ?? '') &&
    isCanonicalUuid(profileAssignment.profile.id ?? ''));
  const hasProfileCoverage = includeProfileCoverage && profileCoverageAvailable;
  const profileCoverage: 'included' | 'unavailable' | 'not-applicable' = hasProfileCoverage
    ? 'included'
    : includeProfileCoverage && profileAssignment ? 'unavailable' : 'not-applicable';
  const shownLimit = hasProfileCoverage
    ? INTELLIGENCE_PROFILE_COMPOSITE_MAX_VALUE_CARDS
    : INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS;
  const shown = resolved.slice(0, shownLimit);
  const trendSignals = resolved.slice(0, MAX_TREND_PENS);
  const screenName = copy.generatedTitle(source.displayName);
  const summary = copy.summary({
    shown: shown.length,
    supported: resolved.length,
    missing,
    nonNumeric,
    duplicates,
    omitted: Math.max(0, resolved.length - shown.length),
    trended: trendSignals.length,
    profileCoverage
  });
  const allocateId = createUniqueIdAllocator(createId);
  const elements: NonNullable<ScreenEngineering['elements']> = [
    {
      id: allocateId(), key: 'intelligence-dashboard-title', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
      properties: {
        x: 64, y: 38, width: 1792, height: 60,
        [VISUAL_PROPERTY_KEYS.text]: screenName,
        [VISUAL_PROPERTY_KEYS.fontSize]: 36,
        [VISUAL_PROPERTY_KEYS.fontWeight]: 700,
        [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      }
    },
    {
      id: allocateId(), key: 'intelligence-dashboard-source', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
      properties: {
        x: 64, y: 104, width: 1792, height: 38,
        [VISUAL_PROPERTY_KEYS.text]: copy.assignmentSubtitle(source),
        [VISUAL_PROPERTY_KEYS.fontSize]: 18,
        [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      }
    }
  ];

  const columns = hasProfileCoverage ? 2 : 5;
  const cardWidth = hasProfileCoverage ? 392 : 330;
  const cardHeight = 132;
  const columnGap = hasProfileCoverage ? 18 : 24;
  const rowGap = 18;
  const rowCount = Math.ceil(shown.length / columns);
  shown.forEach((item, index) => {
    const column = index % columns;
    const row = Math.floor(index / columns);
    const x = 64 + column * (cardWidth + columnGap);
    const y = 166 + row * (cardHeight + rowGap);
    const label = copy.signalLabel(item);
    const scale = validScale(item.tag.scaleMinimum, item.tag.scaleMaximum);
    elements.push({
      id: allocateId(), key: `signal-label-${slug(item.signal.key)}-${index + 1}`, type: BUILTIN_VISUAL_OBJECT_TYPES.text,
      properties: {
        x, y, width: cardWidth, height: 32,
        [VISUAL_PROPERTY_KEYS.text]: label,
        [VISUAL_PROPERTY_KEYS.fontSize]: 17,
        [VISUAL_PROPERTY_KEYS.fontWeight]: 600,
        [VISUAL_PROPERTY_KEYS.textColor]: '#CBD5E1',
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      }
    });

    if (scale) {
      elements.push({
        id: allocateId(), key: `measurement-${slug(item.signal.key)}-${index + 1}`,
        type: BUILTIN_VISUAL_OBJECT_TYPES.measurementBar,
        bindings: [tagBinding(item.tag, VISUAL_PROPERTY_KEYS.value)],
        properties: {
          x, y: y + 36, width: cardWidth, height: 92,
          [VISUAL_PROPERTY_KEYS.minimum]: scale.minimum,
          [VISUAL_PROPERTY_KEYS.maximum]: scale.maximum,
          [VISUAL_PROPERTY_KEYS.measurementUnit]: item.tag.engineeringUnit ?? '',
          [VISUAL_PROPERTY_KEYS.measurementFreshnessVisible]: true
        }
      });
    } else {
      elements.push({
        id: allocateId(), key: `value-${slug(item.signal.key)}-${index + 1}`,
        type: BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay,
        bindings: [tagBinding(item.tag, VISUAL_PROPERTY_KEYS.text)],
        properties: {
          x, y: y + 36, width: cardWidth, height: 92,
          [VISUAL_PROPERTY_KEYS.fontSize]: 28,
          [VISUAL_PROPERTY_KEYS.fontWeight]: 600,
          [VISUAL_PROPERTY_KEYS.textColorGood]: '#E2E8F0',
          [VISUAL_PROPERTY_KEYS.textColorBad]: '#FCA5A5'
        }
      });
    }
  });

  const trendY = 166 + rowCount * (cardHeight + rowGap) + 24;
  const pens = trendSignals.map((item, index) => ({
    ...createTrendPen({
      id: item.tag.id,
      path: item.tag.path,
      label: copy.signalLabel(item),
      unit: item.tag.engineeringUnit
    }, index),
    visible: false
  }));
  elements.push({
    id: allocateId(), key: 'intelligence-source-trend', type: BUILTIN_VISUAL_OBJECT_TYPES.trend,
    properties: {
      x: 64, y: trendY, width: 1792, height: 275,
      trendMode: 'history', trendWindowSeconds: 3600, trendRefreshSeconds: 5,
      trendLegendVisible: true, trendGridVisible: true, trendAxesVisible: true,
      trendQualityVisible: true,
      pens: trendPensEngineeringValue(pens)
    }
  });
  if (hasProfileCoverage && profileAssignment) {
    elements[elements.length - 1] = {
      ...elements[elements.length - 1],
      properties: {
        ...elements[elements.length - 1].properties,
        width: 800,
        height: Math.min(275, Math.max(180, 972 - trendY))
      }
    };
    elements.push({
      id: allocateId(), key: 'intelligence-profile-coverage-v1',
      type: BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1,
      properties: {
        x: 900, y: 166, width: 956, height: 806,
        [VISUAL_PROPERTY_KEYS.intelligenceScopeId]: profileAssignment.scopeId!,
        [VISUAL_PROPERTY_KEYS.intelligenceProfileId]: profileAssignment.profile.id!
      }
    });
  }
  elements.push({
    id: allocateId(), key: 'intelligence-dashboard-scope-note', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
    properties: {
      x: 64, y: 990, width: 1792, height: 56,
      [VISUAL_PROPERTY_KEYS.text]: summary,
      [VISUAL_PROPERTY_KEYS.fontSize]: 14,
      [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
      [VISUAL_PROPERTY_KEYS.textWrap]: true
    }
  });

  return {
    ...draft,
    key,
    name: screenName,
    route,
    elements,
    properties: { ...(draft.properties ?? {}) },
    context: { ...(draft.context ?? {}) },
    metadata: {
      ...(draft.metadata ?? {}),
      intelligenceDashboardGenerator: hasProfileCoverage ? 'stage5b.8' : 'stage5b.1',
      intelligenceDashboardSourceKind: source.kind,
      intelligenceDashboardSourceKey: source.stableKey,
      intelligenceDashboardSummary: summary
    }
  };
}

export type IntelligenceGuidedDashboardSelections = Readonly<{
  performance: IntelligencePerformanceDashboardSelection;
  contextualDeviation: IntelligenceContextualDeviationDashboardSelection;
}>;

/**
 * Generates a four-screen, single-Equipment Intelligence set. Every analysis
 * remains on its existing canonical Screen visual and is connected only by
 * ordinary Runtime NavigateScreen actions. No report data or query window is
 * added to Engineering state.
 */
export function generateIntelligenceGuidedDashboardDrafts(
  existingScreens: readonly ScreenEngineering[],
  source: IntelligenceDashboardSource,
  tags: readonly TagEngineering[],
  selections: IntelligenceGuidedDashboardSelections,
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): readonly ScreenEngineering[] {
  const assignment = source.assignments.length === 1 ? source.assignments[0] : null;
  const scopeId = assignment?.scopeId ?? '';
  const assignmentId = assignment?.assignmentId ?? '';
  const profileId = assignment?.profile.id ?? '';
  if (source.kind !== 'equipment-profile' || !assignment ||
      [scopeId, assignmentId, profileId].some(value => !isCanonicalUuid(value))) {
    throw new Error('A guided Intelligence dashboard requires exactly one complete canonical Equipment assignment and profile.');
  }

  const signalIds = assignment.profile.signals.map(signal => signal.id);
  const tagIds = assignment.profile.signals.map(signal => signal.tagId.toLowerCase());
  if (signalIds.some(value => !isCanonicalUuid(value)) ||
      new Set(signalIds.map(value => value.toLowerCase())).size !== signalIds.length ||
      new Set(tagIds).size !== tagIds.length) {
    throw new Error('A guided Intelligence dashboard requires unique stable signal and TAG bindings in the selected profile.');
  }
  const profileSignalIds = new Set(signalIds.map(value => value.toLowerCase()));
  const taggedIds = new Set(tags.filter((tag): tag is TagEngineering & { id: string } => Boolean(tag.id?.trim()))
    .map(tag => tag.id.toLowerCase()));
  const resolvedTagCounts = new Map<string, number>();
  for (const tag of tags) {
    if (tag.id?.trim()) resolvedTagCounts.set(tag.id.toLowerCase(), (resolvedTagCounts.get(tag.id.toLowerCase()) ?? 0) + 1);
  }
  if (assignment.profile.signals.some(signal => signal.required &&
      (!taggedIds.has(signal.tagId.toLowerCase()) || resolvedTagCounts.get(signal.tagId.toLowerCase()) !== 1))) {
    throw new Error('A guided Intelligence dashboard cannot be generated while a required profile TAG binding is missing or ambiguous.');
  }

  if (!selections?.performance || !selections?.contextualDeviation) {
    throw new Error('A guided Intelligence dashboard requires a valid Performance rule and a complete contextual-deviation selection.');
  }
  const performance = selections.performance;
  if (performance.scopeId.toLowerCase() !== scopeId.toLowerCase() ||
      performance.assignmentId.toLowerCase() !== assignmentId.toLowerCase() ||
      performance.profileId.toLowerCase() !== profileId.toLowerCase() ||
      performance.signalIds.some(id => !profileSignalIds.has(id.toLowerCase()))) {
    throw new Error('The selected Performance rule or signals do not belong to the current Equipment assignment and profile.');
  }
  const deviation = selections.contextualDeviation;
  const deviationSignalIds = [
    ...deviation.targetSignalIds,
    deviation.firstContextSignalId,
    deviation.secondContextSignalId
  ];
  if (deviation.scopeId.toLowerCase() !== scopeId.toLowerCase() ||
      deviation.assignmentId.toLowerCase() !== assignmentId.toLowerCase() ||
      deviation.profileId.toLowerCase() !== profileId.toLowerCase() ||
      deviationSignalIds.some(id => !profileSignalIds.has(id.toLowerCase()))) {
    throw new Error('The contextual-deviation signals do not belong to the current Equipment assignment and profile.');
  }

  const usedVisualIds = new Set<string>();
  const allocateVisualId = () => {
    const id = createId().trim();
    if (!id || usedVisualIds.has(id)) throw new Error('Guided dashboard visual identities must be non-empty and unique across all screens.');
    usedVisualIds.add(id);
    return id;
  };
  const drafts: ScreenEngineering[] = [];
  const existingAndDrafts = () => [...existingScreens, ...drafts];
  drafts.push(generateDashboardDraft(existingAndDrafts(), source, tags, locale, allocateVisualId, false));
  drafts.push(generateIntelligenceProfileCoverageScreenDraft(existingAndDrafts(), {
    scopeId,
    profileId,
    displayName: source.displayName
  }, locale, allocateVisualId));
  drafts.push(generateIntelligencePerformanceScreenDraft(existingAndDrafts(), {
    ...performance,
    displayName: source.displayName
  }, locale, allocateVisualId));
  drafts.push(generateIntelligenceContextualDeviationScreenDraft(existingAndDrafts(), {
    ...deviation,
    displayName: source.displayName
  }, locale, allocateVisualId));

  const navigationLabels = guidedDashboardNavigationLabels(locale);
  const viewRoles = ['values-trend', 'profile-coverage', 'performance', 'contextual-deviation'] as const;
  return drafts.map((screen, index) => {
    const elements = (screen.elements ?? []).map(element => {
      if (element.type !== BUILTIN_VISUAL_OBJECT_TYPES.text ||
          !(element.key.endsWith('-title') || element.key.endsWith('-source') || element.key.endsWith('-subtitle'))) return element;
      return { ...element, properties: { ...element.properties, width: 1200 } };
    });
    const navigation = drafts.flatMap((target, targetIndex) => targetIndex === index ? [] : [{
      id: allocateVisualId(),
      key: `intelligence-guided-navigation-${targetIndex + 1}`,
      type: BUILTIN_VISUAL_OBJECT_TYPES.button,
      properties: {
        x: 1304,
        y: 40,
        width: 172,
        height: 44,
        [VISUAL_PROPERTY_KEYS.text]: navigationLabels[viewRoles[targetIndex]],
        [VISUAL_PROPERTY_KEYS.fontSize]: 14,
        [VISUAL_PROPERTY_KEYS.fontWeight]: 600,
        [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
        [VISUAL_PROPERTY_KEYS.backgroundColor]: '#1E293B',
        [VISUAL_PROPERTY_KEYS.strokeColor]: '#475569',
        [VISUAL_PROPERTY_KEYS.strokeWidth]: 1,
        [VISUAL_PROPERTY_KEYS.cornerRadius]: 8,
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      },
      actions: [{ eventKey: 'click', kind: 'NavigateScreen' as const, targetKey: target.key, version: 1 }]
    }]);
    const positionedNavigation = navigation.map((element, buttonIndex) => ({
      ...element,
      properties: { ...element.properties, x: 1304 + buttonIndex * 184 }
    }));
    const titledElements = elements.map(element => {
      if (element.type !== BUILTIN_VISUAL_OBJECT_TYPES.text || element.key !== 'intelligence-dashboard-scope-note') return element;
      return { ...element, properties: { ...element.properties, [VISUAL_PROPERTY_KEYS.text]: guidedDashboardSummary(locale) } };
    });
    return {
      ...screen,
      elements: [...titledElements, ...positionedNavigation],
      metadata: {
        ...(screen.metadata ?? {}),
        intelligenceDashboardGenerator: 'stage5b.9',
        intelligenceDashboardSetVersion: 'stage5b.9',
        intelligenceDashboardSetRootKey: drafts[0].key,
        intelligenceDashboardViewRole: viewRoles[index]
      }
    };
  });
}

/**
 * Creates a canonical Screen draft containing one configured, Runtime-only
 * Performance V1B object. Its Engineering state stores stable references only;
 * the analytics result is always queried transiently in the active Runtime.
 */
export function generateIntelligencePerformanceScreenDraft(
  existingScreens: readonly ScreenEngineering[],
  selection: IntelligencePerformanceDashboardSelection,
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): ScreenEngineering {
  const identifiers = [selection.scopeId, selection.assignmentId, selection.profileId, ...selection.signalIds];
  if (identifiers.some(value => !isCanonicalUuid(value)) ||
      selection.signalIds.length < 2 || selection.signalIds.length > 3 ||
      new Set(selection.signalIds.map(id => id.toLowerCase())).size !== selection.signalIds.length ||
      !selection.ruleId.trim() || selection.ruleId !== selection.ruleId.trim() || selection.ruleId.length > 128 ||
      !selection.ruleVersion.trim() || selection.ruleVersion !== selection.ruleVersion.trim() || selection.ruleVersion.length > 32) {
    throw new Error('A performance screen requires one stable equipment assignment, profile, catalog rule and two or three unique signals.');
  }

  const displayName = selection.displayName.trim() || selection.scopeId;
  const key = uniqueScreenKey(existingScreens, `intelligence-performance-${slug(displayName)}`);
  const route = uniqueScreenRoute(key, existingScreens.map(screen => screen.route));
  const draft = createScreenDraft(existingScreens, locale);
  const allocateId = createUniqueIdAllocator(createId);
  const title = locale === 'en' ? `Recorded performance · ${displayName}`
    : locale === 'es' ? `Rendimiento registrado · ${displayName}`
      : `Desempenho registrado · ${displayName}`;
  const subtitle = locale === 'en'
    ? 'Runtime-only analysis · server-calculated · read-only · transient result'
    : locale === 'es'
      ? 'Análisis solo en Runtime · cálculo en servidor · solo lectura · resultado transitorio'
      : 'Análise somente no Runtime · cálculo no servidor · somente leitura · resultado temporário';
  const elements: NonNullable<ScreenEngineering['elements']> = [
    {
      id: allocateId(), key: 'intelligence-performance-title', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
      properties: {
        x: 64, y: 36, width: 1792, height: 52,
        [VISUAL_PROPERTY_KEYS.text]: title,
        [VISUAL_PROPERTY_KEYS.fontSize]: 32,
        [VISUAL_PROPERTY_KEYS.fontWeight]: 700,
        [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      }
    },
    {
      id: allocateId(), key: 'intelligence-performance-subtitle', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
      properties: {
        x: 64, y: 98, width: 1792, height: 34,
        [VISUAL_PROPERTY_KEYS.text]: subtitle,
        [VISUAL_PROPERTY_KEYS.fontSize]: 16,
        [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      }
    },
    {
      id: allocateId(), key: 'intelligence-performance-v1b',
      type: BUILTIN_VISUAL_OBJECT_TYPES.intelligencePerformanceV1B,
      properties: {
        x: 64, y: 152, width: 1200, height: 720,
        [VISUAL_PROPERTY_KEYS.intelligenceScopeId]: selection.scopeId,
        [VISUAL_PROPERTY_KEYS.intelligenceAssignmentId]: selection.assignmentId,
        [VISUAL_PROPERTY_KEYS.intelligenceProfileId]: selection.profileId,
        [VISUAL_PROPERTY_KEYS.intelligenceRuleId]: selection.ruleId,
        [VISUAL_PROPERTY_KEYS.intelligenceRuleVersion]: selection.ruleVersion,
        [VISUAL_PROPERTY_KEYS.intelligenceSignalIds]: JSON.stringify(selection.signalIds)
      }
    },
    {
      id: allocateId(), key: 'intelligence-performance-limits', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
      properties: {
        x: 64, y: 900, width: 1792, height: 74,
        [VISUAL_PROPERTY_KEYS.text]: locale === 'en'
          ? 'The report describes recorded Historian evidence only. It is not a field-validated diagnosis, physical simultaneity claim or total-system efficiency measure.'
          : locale === 'es'
            ? 'El informe describe solo evidencia registrada en Historian. No es un diagnóstico validado en campo, una afirmación de simultaneidad física ni una medida de eficiencia total del sistema.'
            : 'O relatório descreve somente evidências registradas no Historian. Não é diagnóstico validado em campo, afirmação de simultaneidade física nem medida de eficiência total do sistema.',
        [VISUAL_PROPERTY_KEYS.fontSize]: 14,
        [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      }
    }
  ];

  return {
    ...draft,
    key,
    name: title,
    route,
    elements,
    metadata: {
      ...(draft.metadata ?? {}),
      intelligenceDashboardGenerator: 'stage5b.4',
      intelligencePerformanceRuleId: selection.ruleId,
      intelligencePerformanceRuleVersion: selection.ruleVersion
    }
  };
}

/**
 * Creates a canonical Screen draft containing one Runtime-only, on-demand
 * profile-coverage visual. Engineering state stores stable scope/profile refs;
 * the requested window and returned coverage report are transient Runtime state.
 */
export function generateIntelligenceProfileCoverageScreenDraft(
  existingScreens: readonly ScreenEngineering[],
  selection: IntelligenceProfileCoverageDashboardSelection,
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): ScreenEngineering {
  if (!isCanonicalUuid(selection.scopeId) || !isCanonicalUuid(selection.profileId)) {
    throw new Error('A coverage screen requires canonical Equipment scope and profile UUIDs.');
  }

  const displayName = selection.displayName.trim() || selection.scopeId;
  const key = uniqueScreenKey(existingScreens, `intelligence-coverage-${slug(displayName)}`);
  const route = uniqueScreenRoute(key, existingScreens.map(screen => screen.route));
  const draft = createScreenDraft(existingScreens, locale);
  const allocateId = createUniqueIdAllocator(createId);
  const title = locale === 'en' ? `Historical coverage · ${displayName}`
    : locale === 'es' ? `Cobertura histórica · ${displayName}`
      : `Cobertura histórica · ${displayName}`;
  const subtitle = locale === 'en'
    ? 'Runtime-only report · explicit request · read-only · transient result'
    : locale === 'es'
      ? 'Informe solo en Runtime · solicitud explícita · read-only · resultado transitorio'
      : 'Relatório somente no Runtime · solicitação explícita · somente leitura · resultado temporário';

  return {
    ...draft,
    key,
    name: title,
    route,
    elements: [
      {
        id: allocateId(), key: 'intelligence-coverage-title', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 36, width: 1792, height: 52,
          [VISUAL_PROPERTY_KEYS.text]: title,
          [VISUAL_PROPERTY_KEYS.fontSize]: 32,
          [VISUAL_PROPERTY_KEYS.fontWeight]: 700,
          [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-coverage-subtitle', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 98, width: 1792, height: 34,
          [VISUAL_PROPERTY_KEYS.text]: subtitle,
          [VISUAL_PROPERTY_KEYS.fontSize]: 16,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-profile-coverage-v1',
        type: BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProfileCoverageV1,
        properties: {
          x: 64, y: 152, width: 1792, height: 790,
          [VISUAL_PROPERTY_KEYS.intelligenceScopeId]: selection.scopeId,
          [VISUAL_PROPERTY_KEYS.intelligenceProfileId]: selection.profileId
        }
      },
      {
        id: allocateId(), key: 'intelligence-coverage-limits', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 960, width: 1792, height: 74,
          [VISUAL_PROPERTY_KEYS.text]: locale === 'en'
            ? 'Exact Historian timestamps do not prove source acquisition synchronization. Unknown cadence/completeness remains unknown; no process KPI is inferred.'
            : locale === 'es'
              ? 'Las marcas de tiempo exactas de Historian no prueban sincronización de adquisición. La cadencia/completitud desconocida sigue desconocida; no se infiere un KPI de proceso.'
              : 'Timestamps exatos do Historian não comprovam sincronização da aquisição. Cadência/completude desconhecida permanece desconhecida; nenhum KPI de processo é inferido.',
          [VISUAL_PROPERTY_KEYS.fontSize]: 14,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      }
    ],
    metadata: {
      ...(draft.metadata ?? {}),
      intelligenceDashboardGenerator: 'stage5b.5',
      intelligenceProfileCoverageProfileId: selection.profileId
    }
  };
}

/**
 * Creates a canonical Screen draft that lets Runtime inspect one configured
 * process-stage member's profile coverage at a time. Only stable references and
 * display labels are saved; the existing per-profile report remains authoritative.
 */
export function generateIntelligenceProcessStageCoverageScreenDraft(
  existingScreens: readonly ScreenEngineering[],
  selection: IntelligenceProcessStageCoverageDashboardSelection,
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): ScreenEngineering {
  const members = validateProcessStageCoverageSelection(selection);
  const displayName = selection.stageName;
  const key = uniqueScreenKey(existingScreens, `intelligence-stage-coverage-${slug(selection.stageKey)}`);
  const route = uniqueScreenRoute(key, existingScreens.map(screen => screen.route));
  const draft = createScreenDraft(existingScreens, locale);
  const allocateId = createUniqueIdAllocator(createId);
  const title = locale === 'en' ? `Process-stage coverage · ${displayName}`
    : locale === 'es' ? `Cobertura de etapa · ${displayName}`
      : `Cobertura da etapa · ${displayName}`;
  const subtitle = locale === 'en'
    ? 'Select one configured Equipment assignment and explicitly request its independent, read-only coverage report.'
    : locale === 'es'
      ? 'Seleccione una asignación de equipo configurada y solicite explícitamente su informe de cobertura independiente y de solo lectura.'
      : 'Selecione um Equipamento associado e solicite explicitamente seu relatório de cobertura independente e somente leitura.';

  return {
    ...draft,
    key,
    name: title,
    route,
    elements: [
      {
        id: allocateId(), key: 'intelligence-process-stage-coverage-title', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 36, width: 1792, height: 52,
          [VISUAL_PROPERTY_KEYS.text]: title,
          [VISUAL_PROPERTY_KEYS.fontSize]: 32,
          [VISUAL_PROPERTY_KEYS.fontWeight]: 700,
          [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-process-stage-coverage-subtitle', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 98, width: 1792, height: 36,
          [VISUAL_PROPERTY_KEYS.text]: subtitle,
          [VISUAL_PROPERTY_KEYS.fontSize]: 16,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-process-stage-coverage-v1',
        type: BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageCoverageV1,
        properties: {
          x: 64, y: 150, width: 1792, height: 790,
          [VISUAL_PROPERTY_KEYS.intelligenceStageId]: selection.stageId,
          [VISUAL_PROPERTY_KEYS.intelligenceStageKey]: selection.stageKey,
          [VISUAL_PROPERTY_KEYS.intelligenceStageName]: selection.stageName,
          [VISUAL_PROPERTY_KEYS.intelligenceStageMembers]: JSON.stringify(members)
        }
      },
      {
        id: allocateId(), key: 'intelligence-process-stage-coverage-limits', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 954, width: 1792, height: 86,
          [VISUAL_PROPERTY_KEYS.text]: locale === 'en'
            ? 'Results stay separate per Equipment profile. This screen does not combine samples, compare assets, or compute a process KPI, efficiency score or diagnosis.'
            : locale === 'es'
              ? 'Los resultados se mantienen separados por perfil de equipo. Esta pantalla no combina muestras, compara activos ni calcula un KPI de proceso, una eficiencia o un diagnóstico.'
              : 'Os resultados permanecem separados por perfil de Equipamento. Esta tela não combina amostras, compara ativos nem calcula KPI de processo, eficiência ou diagnóstico.',
          [VISUAL_PROPERTY_KEYS.fontSize]: 14,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      }
    ],
    metadata: {
      ...(draft.metadata ?? {}),
      intelligenceDashboardGenerator: 'stage5b.7',
      intelligenceProcessStageCoverageStageId: selection.stageId
    }
  };
}

function validateProcessStageCoverageSelection(
  selection: IntelligenceProcessStageCoverageDashboardSelection
): IntelligenceProcessStageCoverageDashboardSelection['members'] {
  const members = Array.isArray(selection?.members) ? selection.members : [];
  const identifiers = [selection?.stageId, ...members.flatMap(member => [
    member?.assignmentId, member?.scopeId, member?.profileId
  ])];
  if (identifiers.some(value => typeof value !== 'string' || !isCanonicalUuid(value)) ||
      typeof selection?.stageKey !== 'string' || !selection.stageKey.trim() ||
      selection.stageKey !== selection.stageKey.trim() || selection.stageKey.length > 64 ||
      typeof selection?.stageName !== 'string' || !selection.stageName.trim() ||
      selection.stageName !== selection.stageName.trim() || selection.stageName.length > 128 ||
      members.length < 1 || members.length > INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS ||
      members.some(member => !member || typeof member.displayLabel !== 'string' || !member.displayLabel.trim() ||
        member.displayLabel !== member.displayLabel.trim() || member.displayLabel.length > 320 ||
        !Number.isInteger(member.order) || member.order < 0) ||
      new Set(members.map(member => member.assignmentId.toLowerCase())).size !== members.length ||
      new Set(members.map(member => `${member.scopeId.toLowerCase()}:${member.profileId.toLowerCase()}`)).size !== members.length ||
      new Set(members.map(member => member.order)).size !== members.length) {
    throw new Error('A process-stage coverage screen requires a canonical stage and one to 32 unique, ordered assignment/scope/profile references.');
  }

  return [...members].sort((left, right) => left.order - right.order || left.assignmentId.localeCompare(right.assignmentId));
}

/**
 * Generates a linked pair for one process stage: a TAG values/trend overview
 * and the existing per-member coverage selector. The overview juxtaposes
 * configured TAGs only; no samples are combined and no process KPI is inferred.
 */
export function generateIntelligenceProcessStageGuidedDashboardDrafts(
  existingScreens: readonly ScreenEngineering[],
  source: IntelligenceDashboardSource,
  selection: IntelligenceProcessStageGuidedDashboardSelection,
  tags: readonly TagEngineering[],
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): readonly ScreenEngineering[] {
  const orderedMembers = validateProcessStageCoverageSelection(selection);
  if (source.kind !== 'process-stage' || source.stableKey !== selection.stageKey ||
      source.displayName !== selection.stageName || source.assignments.length !== selection.members.length ||
      source.assignments.length < 1 || source.assignments.length > INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS) {
    throw new Error('A guided process-stage dashboard requires one complete process stage with matching canonical member references.');
  }

  const membersByAssignmentId = new Map(orderedMembers.map(member => [member.assignmentId.toLowerCase(), member]));
  const sourceAssignmentIds = source.assignments.map(assignment => assignment.assignmentId?.toLowerCase() ?? '');
  if (new Set(sourceAssignmentIds).size !== source.assignments.length ||
      source.assignments.some((assignment, index) => {
    const member = membersByAssignmentId.get(sourceAssignmentIds[index]);
    return !member || !isCanonicalUuid(assignment.assignmentId ?? '') ||
      !isCanonicalUuid(assignment.scopeId ?? '') || !isCanonicalUuid(assignment.profile.id) ||
      !assignment.equipmentName.trim() || !assignment.equipmentPath.trim() ||
      member.assignmentId.toLowerCase() !== orderedMembers[index].assignmentId.toLowerCase() ||
      member.scopeId.toLowerCase() !== assignment.scopeId!.toLowerCase() ||
      member.profileId.toLowerCase() !== assignment.profile.id.toLowerCase();
  })) {
    throw new Error('A guided process-stage dashboard requires every member to match its stable assignment, Equipment and profile references.');
  }

  const usedVisualIds = new Set<string>();
  const allocateVisualId = () => {
    const id = createId().trim();
    if (!id || usedVisualIds.has(id)) throw new Error('Guided dashboard visual identities must be non-empty and unique across all screens.');
    usedVisualIds.add(id);
    return id;
  };
  const drafts: ScreenEngineering[] = [];
  const existingAndDrafts = () => [...existingScreens, ...drafts];
  drafts.push(generateDashboardDraft(existingAndDrafts(), source, tags, locale, allocateVisualId, false));
  drafts.push(generateIntelligenceProcessStageCoverageScreenDraft(existingAndDrafts(), {
    stageId: selection.stageId, stageKey: selection.stageKey, stageName: selection.stageName, members: orderedMembers
  }, locale, allocateVisualId));

  const labels = locale === 'en'
    ? ['Values & trend', 'Member coverage'] as const
    : locale === 'es'
      ? ['Valores y tendencia', 'Cobertura por equipo'] as const
      : ['Valores e tendência', 'Cobertura por Equipamento'] as const;
  return drafts.map((screen, index) => {
    const elements = (screen.elements ?? []).map(element => {
      if (element.type !== BUILTIN_VISUAL_OBJECT_TYPES.text ||
          !(element.key.endsWith('-title') || element.key.endsWith('-source') || element.key.endsWith('-subtitle'))) return element;
      return { ...element, properties: { ...element.properties, width: 1200 } };
    });
    const targetIndex = index === 0 ? 1 : 0;
    const navigation = {
      id: allocateVisualId(),
      key: `intelligence-stage-guided-navigation-${targetIndex + 1}`,
      type: BUILTIN_VISUAL_OBJECT_TYPES.button,
      properties: {
        x: 1304,
        y: 40,
        width: 220,
        height: 44,
        [VISUAL_PROPERTY_KEYS.text]: labels[targetIndex],
        [VISUAL_PROPERTY_KEYS.fontSize]: 14,
        [VISUAL_PROPERTY_KEYS.fontWeight]: 600,
        [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
        [VISUAL_PROPERTY_KEYS.backgroundColor]: '#1E293B',
        [VISUAL_PROPERTY_KEYS.strokeColor]: '#475569',
        [VISUAL_PROPERTY_KEYS.strokeWidth]: 1,
        [VISUAL_PROPERTY_KEYS.cornerRadius]: 8,
        [VISUAL_PROPERTY_KEYS.textWrap]: true
      },
      actions: [{ eventKey: 'click', kind: 'NavigateScreen' as const, targetKey: drafts[targetIndex].key, version: 1 }]
    };
    return {
      ...screen,
      elements: [...elements, navigation],
      metadata: {
        ...(screen.metadata ?? {}),
        intelligenceDashboardGenerator: 'stage5b.10',
        intelligenceDashboardSetVersion: 'stage5b.10',
        intelligenceDashboardSetRootKey: drafts[0].key,
        intelligenceDashboardViewRole: index === 0 ? 'values-trend' : 'process-stage-coverage'
      }
    };
  });
}

/**
 * Creates a separate process-stage member-insights Screen. The established
 * Stage 5B.10 overview/coverage pair remains usable without these selections.
 */
export function generateIntelligenceProcessStageInsightsDashboardDraft(
  existingScreens: readonly ScreenEngineering[],
  source: IntelligenceDashboardSource,
  selection: IntelligenceProcessStageInsightsDashboardSelection,
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): ScreenEngineering {
  const orderedMembers = validateProcessStageCoverageSelection(selection);
  if (source.kind !== 'process-stage' || source.stableKey !== selection.stageKey ||
      source.displayName !== selection.stageName || source.assignments.length !== orderedMembers.length ||
      source.assignments.length < 1 || source.assignments.length > INTELLIGENCE_PROCESS_STAGE_COVERAGE_MAX_MEMBERS) {
    throw new Error('A process-stage member-insights screen requires one complete process stage with matching canonical member references.');
  }

  const membersByAssignmentId = new Map(selection.members.map(member => [member.assignmentId.toLowerCase(), member]));
  const sourceAssignmentIds = source.assignments.map(assignment => assignment.assignmentId?.toLowerCase() ?? '');
  if (new Set(sourceAssignmentIds).size !== source.assignments.length || source.assignments.some((assignment, index) => {
    const member = membersByAssignmentId.get(sourceAssignmentIds[index]);
    return !member || !isCanonicalUuid(assignment.assignmentId ?? '') ||
      !isCanonicalUuid(assignment.scopeId ?? '') || !isCanonicalUuid(assignment.profile.id) ||
      !assignment.equipmentName.trim() || !assignment.equipmentPath.trim() ||
      member.assignmentId.toLowerCase() !== orderedMembers[index].assignmentId.toLowerCase() ||
      member.scopeId.toLowerCase() !== assignment.scopeId!.toLowerCase() ||
      member.profileId.toLowerCase() !== assignment.profile.id.toLowerCase();
  })) {
    throw new Error('A process-stage member-insights screen requires every member to match its stable assignment, Equipment and profile references.');
  }

  for (const assignment of source.assignments) {
    const member = membersByAssignmentId.get(assignment.assignmentId!.toLowerCase());
    const profileSignalIds = new Set(assignment.profile.signals.map(signal => signal.id.toLowerCase()));
    const performance = member?.performance;
    const deviation = member?.contextualDeviation;
    const deviationSignalIds = deviation
      ? [...deviation.targetSignalIds, deviation.firstContextSignalId, deviation.secondContextSignalId]
      : [];
    if (!member || !performance || !deviation ||
        performance.signalIds.length < 2 || performance.signalIds.length > 3 ||
        !performance.ruleId || performance.ruleId !== performance.ruleId.trim() || performance.ruleId.length > 128 ||
        !performance.ruleVersion || performance.ruleVersion !== performance.ruleVersion.trim() || performance.ruleVersion.length > 32 ||
        performance.signalIds.some(id => !isCanonicalUuid(id) || !profileSignalIds.has(id.toLowerCase())) ||
        new Set(performance.signalIds.map(id => id.toLowerCase())).size !== performance.signalIds.length ||
        deviation.targetSignalIds.length < 2 || deviation.targetSignalIds.length > 8 ||
        deviationSignalIds.some(id => !isCanonicalUuid(id) || !profileSignalIds.has(id.toLowerCase())) ||
        new Set(deviationSignalIds.map(id => id.toLowerCase())).size !== deviationSignalIds.length ||
        !validCanonicalRange(deviation.firstContextLowerInclusiveCanonical, deviation.firstContextUpperExclusiveCanonical) ||
        !validCanonicalRange(deviation.secondContextLowerInclusiveCanonical, deviation.secondContextUpperExclusiveCanonical)) {
      throw new Error('A process-stage member-insights screen requires complete Performance and contextual-deviation selections belonging to every stable member profile.');
    }
  }

  const orderedSelection: IntelligenceProcessStageInsightsDashboardSelection = {
    ...selection,
    members: orderedMembers.map(member => membersByAssignmentId.get(member.assignmentId.toLowerCase())!)
  };
  if (JSON.stringify(orderedSelection.members).length > 65536) {
    throw new Error('The process-stage member insight configuration exceeds the supported 64 KB limit.');
  }
  return generateIntelligenceProcessStageInsightsScreenDraft(existingScreens, orderedSelection, locale, createId);
}

function generateIntelligenceProcessStageInsightsScreenDraft(
  existingScreens: readonly ScreenEngineering[],
  selection: IntelligenceProcessStageInsightsDashboardSelection,
  locale: EngineeringLocale,
  createId: () => string
): ScreenEngineering {
  const key = uniqueScreenKey(existingScreens, `intelligence-process-stage-insights-${slug(selection.stageKey)}`);
  const route = uniqueScreenRoute(key, existingScreens.map(screen => screen.route));
  const draft = createScreenDraft(existingScreens, locale);
  const allocateId = createUniqueIdAllocator(createId);
  const title = locale === 'en' ? `Process-stage member insights · ${selection.stageName}`
    : locale === 'es' ? `Análisis de miembros de etapa · ${selection.stageName}`
      : `Análises dos membros da etapa · ${selection.stageName}`;
  const subtitle = locale === 'en'
    ? 'Choose one configured Equipment member, then explicitly open one independent read-only report.'
    : locale === 'es'
      ? 'Elija un miembro de equipo configurado y abra explícitamente uno de sus informes independientes y de solo lectura.'
      : 'Escolha um Equipamento associado e abra explicitamente um dos relatórios independentes e somente leitura.';
  return {
    ...draft,
    key,
    name: title,
    route,
    elements: [
      {
        id: allocateId(), key: 'intelligence-process-stage-insights-title', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 36, width: 1200, height: 52,
          [VISUAL_PROPERTY_KEYS.text]: title,
          [VISUAL_PROPERTY_KEYS.fontSize]: 32,
          [VISUAL_PROPERTY_KEYS.fontWeight]: 700,
          [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-process-stage-insights-subtitle', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 98, width: 1200, height: 36,
          [VISUAL_PROPERTY_KEYS.text]: subtitle,
          [VISUAL_PROPERTY_KEYS.fontSize]: 16,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-process-stage-insights-v1',
        type: BUILTIN_VISUAL_OBJECT_TYPES.intelligenceProcessStageInsightsV1,
        properties: {
          x: 64, y: 150, width: 1792, height: 790,
          [VISUAL_PROPERTY_KEYS.intelligenceStageId]: selection.stageId,
          [VISUAL_PROPERTY_KEYS.intelligenceStageKey]: selection.stageKey,
          [VISUAL_PROPERTY_KEYS.intelligenceStageName]: selection.stageName,
          [VISUAL_PROPERTY_KEYS.intelligenceStageMembers]: JSON.stringify(selection.members)
        }
      },
      {
        id: allocateId(), key: 'intelligence-process-stage-insights-limits', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 954, width: 1792, height: 86,
          [VISUAL_PROPERTY_KEYS.text]: locale === 'en'
            ? 'Reports remain attributable to the selected Equipment assignment. No process-wide score, timestamp join, diagnosis or efficiency claim is produced.'
            : locale === 'es'
              ? 'Los informes siguen atribuidos a la asignación de equipo seleccionada. No se produce una puntuación de proceso, unión de marcas de tiempo, diagnóstico ni afirmación de eficiencia.'
              : 'Os relatórios permanecem atribuídos ao Equipamento selecionado. Não é gerado score do processo, cruzamento de timestamps, diagnóstico ou afirmação de eficiência.',
          [VISUAL_PROPERTY_KEYS.fontSize]: 14,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      }
    ],
    metadata: {
      ...(draft.metadata ?? {}),
      intelligenceDashboardGenerator: 'stage5b.11',
      intelligenceProcessStageInsightsStageId: selection.stageId
    }
  };
}

/**
 * Creates a Screen-only Runtime V2 contextual-deviation visual. The draft stores
 * stable signal references and caller-declared canonical ranges only; time
 * windows, Historian samples and analysis results remain transient Runtime state.
 */
export function generateIntelligenceContextualDeviationScreenDraft(
  existingScreens: readonly ScreenEngineering[],
  selection: IntelligenceContextualDeviationDashboardSelection,
  locale: EngineeringLocale,
  createId: () => string = () => crypto.randomUUID()
): ScreenEngineering {
  const identifiers = [selection.scopeId, selection.assignmentId, selection.profileId,
    ...selection.targetSignalIds, selection.firstContextSignalId, selection.secondContextSignalId];
  const allSignalIds = [...selection.targetSignalIds, selection.firstContextSignalId, selection.secondContextSignalId];
  if (identifiers.some(value => !isCanonicalUuid(value)) ||
      selection.targetSignalIds.length < 2 || selection.targetSignalIds.length > 8 ||
      new Set(allSignalIds.map(id => id.toLowerCase())).size !== allSignalIds.length ||
      !validCanonicalRange(selection.firstContextLowerInclusiveCanonical, selection.firstContextUpperExclusiveCanonical) ||
      !validCanonicalRange(selection.secondContextLowerInclusiveCanonical, selection.secondContextUpperExclusiveCanonical)) {
    throw new Error('A contextual deviation screen requires canonical Equipment, assignment, profile and unique signal UUIDs, two to eight targets, and finite lower-inclusive/upper-exclusive canonical ranges.');
  }

  const displayName = selection.displayName.trim() || selection.scopeId;
  const key = uniqueScreenKey(existingScreens, `intelligence-contextual-deviation-${slug(displayName)}`);
  const route = uniqueScreenRoute(key, existingScreens.map(screen => screen.route));
  const draft = createScreenDraft(existingScreens, locale);
  const allocateId = createUniqueIdAllocator(createId);
  const title = locale === 'en' ? `Contextual deviation · ${displayName}`
    : locale === 'es' ? `Desviación contextual · ${displayName}`
      : `Desvio contextual · ${displayName}`;
  const subtitle = locale === 'en'
    ? 'Runtime-only · explicit comparison · per-signal descriptive evidence · transient result'
    : locale === 'es'
      ? 'Solo Runtime · comparación explícita · evidencia descriptiva por señal · resultado transitorio'
      : 'Somente Runtime · comparação explícita · evidência descritiva por sinal · resultado temporário';
  const caution = locale === 'en'
    ? 'Each target is compared with its own joint-context reference distribution. The result is not a combined anomaly, diagnosis, fault classification or remaining-life estimate.'
    : locale === 'es'
      ? 'Cada señal objetivo se compara con su propia distribución de referencia en el contexto conjunto. El resultado no es una anomalía combinada, diagnóstico, clasificación de falla ni estimación de vida restante.'
      : 'Cada sinal-alvo é comparado com sua própria distribuição de referência no contexto conjunto. O resultado não é anomalia combinada, diagnóstico, classificação de falha nem estimativa de vida restante.';

  return {
    ...draft,
    key,
    name: title,
    route,
    elements: [
      {
        id: allocateId(), key: 'intelligence-contextual-deviation-title', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 36, width: 1792, height: 52,
          [VISUAL_PROPERTY_KEYS.text]: title,
          [VISUAL_PROPERTY_KEYS.fontSize]: 32,
          [VISUAL_PROPERTY_KEYS.fontWeight]: 700,
          [VISUAL_PROPERTY_KEYS.textColor]: '#E2E8F0',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-contextual-deviation-subtitle', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 98, width: 1792, height: 34,
          [VISUAL_PROPERTY_KEYS.text]: subtitle,
          [VISUAL_PROPERTY_KEYS.fontSize]: 16,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      },
      {
        id: allocateId(), key: 'intelligence-contextual-deviation-v2',
        type: BUILTIN_VISUAL_OBJECT_TYPES.intelligenceContextualDeviationV2,
        properties: {
          x: 64, y: 152, width: 1792, height: 720,
          [VISUAL_PROPERTY_KEYS.intelligenceScopeId]: selection.scopeId,
          [VISUAL_PROPERTY_KEYS.intelligenceAssignmentId]: selection.assignmentId,
          [VISUAL_PROPERTY_KEYS.intelligenceProfileId]: selection.profileId,
          [VISUAL_PROPERTY_KEYS.intelligenceSignalIds]: JSON.stringify(selection.targetSignalIds),
          [VISUAL_PROPERTY_KEYS.intelligenceFirstContextSignalId]: selection.firstContextSignalId,
          [VISUAL_PROPERTY_KEYS.intelligenceFirstContextLowerInclusiveCanonical]: selection.firstContextLowerInclusiveCanonical,
          [VISUAL_PROPERTY_KEYS.intelligenceFirstContextUpperExclusiveCanonical]: selection.firstContextUpperExclusiveCanonical,
          [VISUAL_PROPERTY_KEYS.intelligenceSecondContextSignalId]: selection.secondContextSignalId,
          [VISUAL_PROPERTY_KEYS.intelligenceSecondContextLowerInclusiveCanonical]: selection.secondContextLowerInclusiveCanonical,
          [VISUAL_PROPERTY_KEYS.intelligenceSecondContextUpperExclusiveCanonical]: selection.secondContextUpperExclusiveCanonical
        }
      },
      {
        id: allocateId(), key: 'intelligence-contextual-deviation-limits', type: BUILTIN_VISUAL_OBJECT_TYPES.text,
        properties: {
          x: 64, y: 900, width: 1792, height: 74,
          [VISUAL_PROPERTY_KEYS.text]: caution,
          [VISUAL_PROPERTY_KEYS.fontSize]: 14,
          [VISUAL_PROPERTY_KEYS.textColor]: '#94A3B8',
          [VISUAL_PROPERTY_KEYS.textWrap]: true
        }
      }
    ],
    metadata: {
      ...(draft.metadata ?? {}),
      intelligenceDashboardGenerator: 'stage5b.6',
      intelligenceContextualDeviationRuleId: 'contextual-deviation-vector.v2',
      intelligenceContextualDeviationRuleVersion: '2.0.0'
    }
  };
}

export function isNumericTagDataType(dataType: string | null | undefined): boolean {
  const normalized = dataType?.trim().toLowerCase();
  return normalized !== undefined && NUMERIC_DATA_TYPES.has(normalized);
}

function guidedDashboardNavigationLabels(locale: EngineeringLocale): Readonly<Record<
  'values-trend' | 'profile-coverage' | 'performance' | 'contextual-deviation', string
>> {
  if (locale === 'en') return {
    'values-trend': 'Values & trend',
    'profile-coverage': 'Coverage',
    performance: 'Performance',
    'contextual-deviation': 'Contextual deviation'
  };
  if (locale === 'es') return {
    'values-trend': 'Valores y tendencia',
    'profile-coverage': 'Cobertura',
    performance: 'Rendimiento',
    'contextual-deviation': 'Desviación contextual'
  };
  return {
    'values-trend': 'Valores e tendência',
    'profile-coverage': 'Cobertura',
    performance: 'Desempenho',
    'contextual-deviation': 'Desvio contextual'
  };
}

function guidedDashboardSummary(locale: EngineeringLocale): string {
  if (locale === 'en') return 'Values and trend are shown here. Profile coverage, recorded performance and contextual deviation are separate linked screens; each analysis runs only after an explicit Runtime request.';
  if (locale === 'es') return 'Aquí se muestran valores y tendencia. La cobertura del perfil, el rendimiento registrado y la desviación contextual son pantallas vinculadas independientes; cada análisis solo se ejecuta tras una solicitud explícita en Runtime.';
  return 'Esta tela mostra valores e tendência. Cobertura do perfil, desempenho registrado e desvio contextual ficam em telas vinculadas separadas; cada análise só é executada após solicitação explícita no Runtime.';
}

function tagBinding(tag: TagEngineering & { id: string }, destination: string) {
  const unit = tag.engineeringUnit?.trim();
  return {
    key: destination,
    kind: 'Tag',
    target: tag.path,
    ...(unit ? { metadata: { engineeringUnit: unit } } : {}),
    tagReference: { tagId: tag.id }
  };
}

function validScale(minimum: number | null | undefined, maximum: number | null | undefined): { minimum: number; maximum: number } | null {
  return typeof minimum === 'number' && Number.isFinite(minimum) &&
    typeof maximum === 'number' && Number.isFinite(maximum) && minimum < maximum
    ? { minimum, maximum }
    : null;
}

function validCanonicalRange(lowerInclusive: number, upperExclusive: number): boolean {
  return Number.isFinite(lowerInclusive) && Number.isFinite(upperExclusive) && lowerInclusive < upperExclusive;
}

function createUniqueIdAllocator(createId: () => string): () => string {
  const used = new Set<string>();
  return () => {
    const id = createId().trim();
    if (!id || used.has(id)) throw new Error('Dashboard visual identities must be non-empty and unique.');
    used.add(id);
    return id;
  };
}

function uniqueScreenKey(screens: readonly ScreenEngineering[], base: string): string {
  const used = new Set(screens.map(screen => screen.key.trim().toLowerCase()));
  let candidate = base.slice(0, 80);
  let suffix = 2;
  while (used.has(candidate.toLowerCase())) {
    const tail = `-${suffix++}`;
    candidate = `${base.slice(0, 80 - tail.length)}${tail}`;
  }
  return candidate;
}

function uniqueScreenRoute(key: string, routes: readonly (string | null | undefined)[]): string {
  const used = new Set(routes.map(route => route?.trim().toLowerCase()).filter((route): route is string => Boolean(route)));
  let candidate = `/${key}`;
  let suffix = 2;
  while (used.has(candidate.toLowerCase())) candidate = `/${key}-${suffix++}`;
  return candidate;
}

function slug(value: string): string {
  return value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
    .replace(/[^a-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 64) || 'source';
}

function isCanonicalUuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

function dashboardText(locale: EngineeringLocale): DashboardText {
  if (locale === 'en') return {
    generatedTitle: source => `Intelligence · ${source}`,
    assignmentSubtitle: source => source.kind === 'process-stage'
      ? `Process stage · ${source.displayName} · read-only TAG overview`
      : `Equipment and profile · ${source.displayName} · read-only TAG overview`,
    signalLabel: item => `${item.assignment.equipmentName} · ${item.signal.key}${item.tag.engineeringUnit ? ` (${item.tag.engineeringUnit})` : ''}`,
    summary: counts => `${counts.shown} value cards from ${counts.supported} numeric TAGs; ${counts.trended} TAG references configured in the historical trend, initially hidden until enabled in Runtime.${counts.missing ? ` ${counts.missing} missing references.` : ''}${counts.nonNumeric ? ` ${counts.nonNumeric} non-numeric signals omitted.` : ''}${counts.duplicates ? ` ${counts.duplicates} duplicate TAG references omitted.` : ''}${counts.omitted ? ` ${counts.omitted} additional numeric signals omitted from value cards.` : ''}${counts.profileCoverage === 'included' ? ' A separate profile-coverage report is available on this screen and runs only after an explicit Runtime request.' : counts.profileCoverage === 'unavailable' ? ' Profile coverage was omitted because canonical Equipment/profile references are unavailable.' : ''} Starter screen only: it does not calculate KPIs, aggregate coverage, deviations, efficiency, diagnosis, or predictions.`,
    noNumericSignals: 'This source has no resolvable numeric TAGs to place on a read-only dashboard.'
  };
  if (locale === 'es') return {
    generatedTitle: source => `Inteligencia · ${source}`,
    assignmentSubtitle: source => source.kind === 'process-stage'
      ? `Etapa del proceso · ${source.displayName} · resumen de TAG de solo lectura`
      : `Equipo y perfil · ${source.displayName} · resumen de TAG de solo lectura`,
    signalLabel: item => `${item.assignment.equipmentName} · ${item.signal.key}${item.tag.engineeringUnit ? ` (${item.tag.engineeringUnit})` : ''}`,
    summary: counts => `${counts.shown} tarjetas de valor de ${counts.supported} TAG numéricos; ${counts.trended} referencias TAG configuradas en la tendencia histórica, ocultas hasta activarlas en Runtime.${counts.missing ? ` ${counts.missing} referencias ausentes.` : ''}${counts.nonNumeric ? ` ${counts.nonNumeric} señales no numéricas omitidas.` : ''}${counts.duplicates ? ` ${counts.duplicates} referencias TAG duplicadas omitidas.` : ''}${counts.omitted ? ` ${counts.omitted} señales numéricas adicionales omitidas de las tarjetas.` : ''}${counts.profileCoverage === 'included' ? ' Esta pantalla incluye un informe separado de cobertura del perfil que solo consulta Historian tras una solicitud explícita en Runtime.' : counts.profileCoverage === 'unavailable' ? ' Se omitió cobertura del perfil porque faltan referencias canónicas de Equipo/perfil.' : ''} Pantalla inicial: no deriva KPI de proceso, desviaciones, eficiencia, diagnóstico ni predicciones.`,
    noNumericSignals: 'Esta fuente no tiene TAG numéricos resolubles para una pantalla de solo lectura.'
  };
  return {
    generatedTitle: source => `Inteligência · ${source}`,
    assignmentSubtitle: source => source.kind === 'process-stage'
      ? `Etapa do processo · ${source.displayName} · visão somente leitura dos TAGs`
      : `Equipamento e perfil · ${source.displayName} · visão somente leitura dos TAGs`,
    signalLabel: item => `${item.assignment.equipmentName} · ${item.signal.key}${item.tag.engineeringUnit ? ` (${item.tag.engineeringUnit})` : ''}`,
    summary: counts => `${counts.shown} cartões de valor para ${counts.supported} TAGs numéricos; ${counts.trended} referências TAG configuradas na tendência histórica, ocultas até ativação no Runtime.${counts.missing ? ` ${counts.missing} referências ausentes.` : ''}${counts.nonNumeric ? ` ${counts.nonNumeric} sinais não numéricos omitidos.` : ''}${counts.duplicates ? ` ${counts.duplicates} referências TAG duplicadas omitidas.` : ''}${counts.omitted ? ` ${counts.omitted} sinais numéricos adicionais omitidos dos cartões.` : ''}${counts.profileCoverage === 'included' ? ' Esta tela inclui um relatório separado de cobertura do perfil, consultado somente após ação explícita no Runtime.' : counts.profileCoverage === 'unavailable' ? ' A cobertura do perfil foi omitida porque faltam referências canônicas de Equipamento/perfil.' : ''} Tela inicial: não calcula KPIs de processo, desvios, eficiência, diagnóstico nem predições.`,
    noNumericSignals: 'Esta origem não tem TAGs numéricos resolvíveis para uma tela somente leitura.'
  };
}
