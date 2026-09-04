import type { EngineeringLocale } from '../i18n';
import {
  VISUAL_PROPERTY_KEYS,
  type TrendVisualPen
} from '../../visual-runtime';

export type TrendAuthoringTag = Readonly<{
  id: string;
  path: string;
  name: string;
  engineeringUnit?: string | null;
}>;

export type TrendInspectorControlCopy = Readonly<{
  useDefault: string;
  mixed: string;
  trueLabel: string;
  falseLabel: string;
  defaultState: string;
  engineeringState: string;
  mixedState: (explicitCount: number, selectionCount: number) => string;
}>;

const TREND_PROPERTY_LABELS: Readonly<Record<EngineeringLocale, Readonly<Record<string, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({
    [VISUAL_PROPERTY_KEYS.trendMode]: 'Modo do Trend',
    [VISUAL_PROPERTY_KEYS.trendWindowSeconds]: 'Janela do Trend',
    [VISUAL_PROPERTY_KEYS.trendRefreshSeconds]: 'Atualização do Trend',
    [VISUAL_PROPERTY_KEYS.trendLegendVisible]: 'Mostrar legenda',
    [VISUAL_PROPERTY_KEYS.trendGridVisible]: 'Mostrar grade',
    [VISUAL_PROPERTY_KEYS.trendAxesVisible]: 'Mostrar eixos',
    [VISUAL_PROPERTY_KEYS.trendQualityVisible]: 'Mostrar qualidade'
  }),
  en: Object.freeze({
    [VISUAL_PROPERTY_KEYS.trendMode]: 'Trend mode',
    [VISUAL_PROPERTY_KEYS.trendWindowSeconds]: 'Trend window',
    [VISUAL_PROPERTY_KEYS.trendRefreshSeconds]: 'Trend refresh',
    [VISUAL_PROPERTY_KEYS.trendLegendVisible]: 'Show legend',
    [VISUAL_PROPERTY_KEYS.trendGridVisible]: 'Show grid',
    [VISUAL_PROPERTY_KEYS.trendAxesVisible]: 'Show axes',
    [VISUAL_PROPERTY_KEYS.trendQualityVisible]: 'Show quality'
  }),
  es: Object.freeze({
    [VISUAL_PROPERTY_KEYS.trendMode]: 'Modo del Trend',
    [VISUAL_PROPERTY_KEYS.trendWindowSeconds]: 'Ventana del Trend',
    [VISUAL_PROPERTY_KEYS.trendRefreshSeconds]: 'Actualización del Trend',
    [VISUAL_PROPERTY_KEYS.trendLegendVisible]: 'Mostrar leyenda',
    [VISUAL_PROPERTY_KEYS.trendGridVisible]: 'Mostrar cuadrícula',
    [VISUAL_PROPERTY_KEYS.trendAxesVisible]: 'Mostrar ejes',
    [VISUAL_PROPERTY_KEYS.trendQualityVisible]: 'Mostrar calidad'
  })
});

const TREND_MODE_LABELS: Readonly<Record<EngineeringLocale, Readonly<Record<string, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({ history: 'Histórico', live: 'Tempo real' }),
  en: Object.freeze({ history: 'History', live: 'Live' }),
  es: Object.freeze({ history: 'Histórico', live: 'En vivo' })
});

const TREND_CONTROL_COPY: Readonly<Record<EngineeringLocale, TrendInspectorControlCopy>> = Object.freeze({
  'pt-BR': Object.freeze({
    useDefault: 'Usar padrão',
    mixed: 'Misto',
    trueLabel: 'Verdadeiro',
    falseLabel: 'Falso',
    defaultState: 'Padrão',
    engineeringState: 'Configurado',
    mixedState: (explicitCount, selectionCount) => `Misto · ${explicitCount}/${selectionCount} explícitos`
  }),
  en: Object.freeze({
    useDefault: 'Use default',
    mixed: 'Mixed',
    trueLabel: 'True',
    falseLabel: 'False',
    defaultState: 'Default',
    engineeringState: 'Configured',
    mixedState: (explicitCount, selectionCount) => `Mixed · ${explicitCount}/${selectionCount} explicit`
  }),
  es: Object.freeze({
    useDefault: 'Usar predeterminado',
    mixed: 'Mixto',
    trueLabel: 'Verdadero',
    falseLabel: 'Falso',
    defaultState: 'Predeterminado',
    engineeringState: 'Configurado',
    mixedState: (explicitCount, selectionCount) => `Mixto · ${explicitCount}/${selectionCount} explícitos`
  })
});

/**
 * Rebinds a Pen to another canonical TAG without carrying stale catalog defaults.
 * User overrides remain stable; labels/units still matching the old TAG catalog
 * follow the new TAG automatically.
 */
export function rebindTrendPenToTag(
  previous: TrendVisualPen,
  previousTag: TrendAuthoringTag | undefined,
  nextTag: TrendAuthoringTag
): TrendVisualPen {
  const previousCatalogLabel = previousTag?.name.trim() || previousTag?.path.trim() || '';
  const previousCatalogUnit = previousTag?.engineeringUnit?.trim() ?? '';
  const nextLabel = nextTag.name.trim() || nextTag.path.trim();
  const nextUnit = nextTag.engineeringUnit?.trim() ?? '';

  const labelWasAutomatic = previous.label.trim().length === 0
    || previous.label === previous.tagPath
    || (previousCatalogLabel.length > 0 && previous.label === previousCatalogLabel);
  const unitWasAutomatic = previous.unit.trim().length === 0
    || (previousCatalogUnit.length > 0 && previous.unit === previousCatalogUnit);

  return Object.freeze({
    ...previous,
    tagId: nextTag.id,
    tagPath: nextTag.path,
    label: labelWasAutomatic ? nextLabel : previous.label,
    unit: unitWasAutomatic ? nextUnit : previous.unit
  });
}

export function trendPropertyLabel(locale: EngineeringLocale, propertyKey: string): string | null {
  return TREND_PROPERTY_LABELS[locale][propertyKey] ?? null;
}

export function trendPropertyOptionLabel(
  locale: EngineeringLocale,
  propertyKey: string,
  option: string
): string {
  if (propertyKey !== VISUAL_PROPERTY_KEYS.trendMode) return option;
  return TREND_MODE_LABELS[locale][option] ?? option;
}

export function trendInspectorControlCopy(locale: EngineeringLocale): TrendInspectorControlCopy {
  return TREND_CONTROL_COPY[locale];
}
