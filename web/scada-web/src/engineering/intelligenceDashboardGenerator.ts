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
const NUMERIC_DATA_TYPES = new Set([
  'byte', 'sbyte', 'int8', 'uint8', 'short', 'int16', 'uint16', 'int', 'integer', 'int32', 'uint32',
  'long', 'int64', 'uint64', 'single', 'float', 'float32', 'double', 'float64', 'decimal', 'number'
]);

export type IntelligenceDashboardAssignmentSource = Readonly<{
  equipmentName: string;
  equipmentPath: string;
  profile: AssetProcessIntelligenceProfileEngineering;
}>;

export type IntelligenceDashboardSource = Readonly<{
  kind: 'equipment-profile' | 'process-stage';
  stableKey: string;
  displayName: string;
  assignments: readonly IntelligenceDashboardAssignmentSource[];
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
  const shown = resolved.slice(0, INTELLIGENCE_DASHBOARD_MAX_VALUE_CARDS);
  const trendSignals = resolved.slice(0, MAX_TREND_PENS);
  const screenName = copy.generatedTitle(source.displayName);
  const summary = copy.summary({
    shown: shown.length,
    supported: resolved.length,
    missing,
    nonNumeric,
    duplicates,
    omitted: Math.max(0, resolved.length - shown.length),
    trended: trendSignals.length
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

  const columns = 5;
  const cardWidth = 330;
  const cardHeight = 132;
  const columnGap = 24;
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
          [VISUAL_PROPERTY_KEYS.showEngineeringUnit]: true,
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
      intelligenceDashboardGenerator: 'stage5b.1',
      intelligenceDashboardSourceKind: source.kind,
      intelligenceDashboardSourceKey: source.stableKey,
      intelligenceDashboardSummary: summary
    }
  };
}

export function isNumericTagDataType(dataType: string | null | undefined): boolean {
  const normalized = dataType?.trim().toLowerCase();
  return normalized !== undefined && NUMERIC_DATA_TYPES.has(normalized);
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

function dashboardText(locale: EngineeringLocale): DashboardText {
  if (locale === 'en') return {
    generatedTitle: source => `Intelligence · ${source}`,
    assignmentSubtitle: source => source.kind === 'process-stage'
      ? `Process stage · ${source.displayName} · read-only TAG overview`
      : `Equipment and profile · ${source.displayName} · read-only TAG overview`,
    signalLabel: item => `${item.assignment.equipmentName} · ${item.signal.key}${item.tag.engineeringUnit ? ` (${item.tag.engineeringUnit})` : ''}`,
    summary: counts => `${counts.shown} value cards from ${counts.supported} numeric TAGs; ${counts.trended} TAG references configured in the historical trend, initially hidden until enabled in Runtime.${counts.missing ? ` ${counts.missing} missing references.` : ''}${counts.nonNumeric ? ` ${counts.nonNumeric} non-numeric signals omitted.` : ''}${counts.duplicates ? ` ${counts.duplicates} duplicate TAG references omitted.` : ''}${counts.omitted ? ` ${counts.omitted} additional numeric signals omitted from value cards.` : ''} Starter screen only: it does not calculate KPIs, coverage, deviations, efficiency, diagnosis, or predictions.`,
    noNumericSignals: 'This source has no resolvable numeric TAGs to place on a read-only dashboard.'
  };
  if (locale === 'es') return {
    generatedTitle: source => `Inteligencia · ${source}`,
    assignmentSubtitle: source => source.kind === 'process-stage'
      ? `Etapa del proceso · ${source.displayName} · resumen de TAG de solo lectura`
      : `Equipo y perfil · ${source.displayName} · resumen de TAG de solo lectura`,
    signalLabel: item => `${item.assignment.equipmentName} · ${item.signal.key}${item.tag.engineeringUnit ? ` (${item.tag.engineeringUnit})` : ''}`,
    summary: counts => `${counts.shown} tarjetas de valor de ${counts.supported} TAG numéricos; ${counts.trended} referencias TAG configuradas en la tendencia histórica, ocultas hasta activarlas en Runtime.${counts.missing ? ` ${counts.missing} referencias ausentes.` : ''}${counts.nonNumeric ? ` ${counts.nonNumeric} señales no numéricas omitidas.` : ''}${counts.duplicates ? ` ${counts.duplicates} referencias TAG duplicadas omitidas.` : ''}${counts.omitted ? ` ${counts.omitted} señales numéricas adicionales omitidas de las tarjetas.` : ''} Pantalla inicial: no calcula KPI, cobertura, desviaciones, eficiencia, diagnóstico ni predicciones.`,
    noNumericSignals: 'Esta fuente no tiene TAG numéricos resolubles para una pantalla de solo lectura.'
  };
  return {
    generatedTitle: source => `Inteligência · ${source}`,
    assignmentSubtitle: source => source.kind === 'process-stage'
      ? `Etapa do processo · ${source.displayName} · visão somente leitura dos TAGs`
      : `Equipamento e perfil · ${source.displayName} · visão somente leitura dos TAGs`,
    signalLabel: item => `${item.assignment.equipmentName} · ${item.signal.key}${item.tag.engineeringUnit ? ` (${item.tag.engineeringUnit})` : ''}`,
    summary: counts => `${counts.shown} cartões de valor para ${counts.supported} TAGs numéricos; ${counts.trended} referências TAG configuradas na tendência histórica, ocultas até ativação no Runtime.${counts.missing ? ` ${counts.missing} referências ausentes.` : ''}${counts.nonNumeric ? ` ${counts.nonNumeric} sinais não numéricos omitidos.` : ''}${counts.duplicates ? ` ${counts.duplicates} referências TAG duplicadas omitidas.` : ''}${counts.omitted ? ` ${counts.omitted} sinais numéricos adicionais omitidos dos cartões.` : ''} Tela inicial: não calcula KPIs, cobertura, desvios, eficiência, diagnóstico nem predições.`,
    noNumericSignals: 'Esta origem não tem TAGs numéricos resolvíveis para uma tela somente leitura.'
  };
}
