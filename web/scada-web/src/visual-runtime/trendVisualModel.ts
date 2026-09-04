import type {
  VisualElementEngineering,
  VisualEngineeringPropertyValue
} from '../engineering/types';

export const TREND_PENS_PROPERTY = 'pens';
export const MAX_TREND_PENS = 16;

export type TrendMode = 'history' | 'live';
export type TrendLineStyle = 'solid' | 'dashed' | 'dotted';
export type TrendAxis = 'left' | 'right';
export type TrendPenScale =
  | Readonly<{ mode: 'auto' }>
  | Readonly<{ mode: 'fixed'; minimum: number; maximum: number }>;

export type TrendVisualPen = Readonly<{
  id: string;
  tagId: string;
  tagPath: string;
  label: string;
  visible: boolean;
  unit: string;
  color: string;
  lineWidth: number;
  lineStyle: TrendLineStyle;
  axis: TrendAxis;
  scale: TrendPenScale;
}>;

const DEFAULT_COLORS = Object.freeze([
  '#38BDF8', '#F97316', '#22C55E', '#E879F9',
  '#FACC15', '#A78BFA', '#FB7185', '#2DD4BF'
]);

export function createTrendPen(
  tag: Readonly<{ id: string; path: string; label?: string | null; unit?: string | null }>,
  index = 0
): TrendVisualPen {
  const tagId = stableIdentity(tag.id, 'Trend Pen tagId');
  const tagPath = stableText(tag.path, 'Trend Pen tagPath');
  return Object.freeze({
    id: `pen-${index + 1}-${stableDomToken(tagId)}`,
    tagId,
    tagPath,
    label: tag.label?.trim() || tagPath,
    visible: true,
    unit: tag.unit?.trim() || '',
    color: DEFAULT_COLORS[index % DEFAULT_COLORS.length],
    lineWidth: 2,
    lineStyle: 'solid',
    axis: index % 2 === 0 ? 'left' : 'right',
    scale: Object.freeze({ mode: 'auto' })
  });
}

export function readTrendPens(element: Pick<VisualElementEngineering, 'properties'>): readonly TrendVisualPen[] {
  const raw = element.properties?.[TREND_PENS_PROPERTY];
  if (raw === undefined || raw === null) return Object.freeze([]);
  if (!Array.isArray(raw)) throw new Error('Trend pens must be a JSON array.');
  return normalizeTrendPens(raw);
}

export function normalizeTrendPens(input: readonly unknown[]): readonly TrendVisualPen[] {
  if (input.length > MAX_TREND_PENS) {
    throw new Error(`Trend supports at most ${MAX_TREND_PENS} pens.`);
  }
  const ids = new Set<string>();
  const normalized = input.map((candidate, index) => normalizeTrendPen(candidate, index));
  for (const pen of normalized) {
    if (!ids.add(pen.id)) throw new Error(`Trend Pen id '${pen.id}' is duplicated.`);
  }
  return Object.freeze(normalized);
}

export function trendPensEngineeringValue(pens: readonly TrendVisualPen[]): VisualEngineeringPropertyValue {
  const normalized = normalizeTrendPens(pens);
  return Object.freeze(normalized.map(pen => Object.freeze({
    id: pen.id,
    tagId: pen.tagId,
    tagPath: pen.tagPath,
    label: pen.label,
    visible: pen.visible,
    unit: pen.unit,
    color: pen.color,
    lineWidth: pen.lineWidth,
    lineStyle: pen.lineStyle,
    axis: pen.axis,
    scale: pen.scale.mode === 'fixed'
      ? Object.freeze({ mode: 'fixed', minimum: pen.scale.minimum, maximum: pen.scale.maximum })
      : Object.freeze({ mode: 'auto' })
  }))) as VisualEngineeringPropertyValue;
}

function normalizeTrendPen(candidate: unknown, index: number): TrendVisualPen {
  if (!isRecord(candidate)) throw new Error(`Trend Pen ${index + 1} must be an object.`);
  const id = stableIdentity(candidate.id, `Trend Pen ${index + 1} id`);
  const tagId = stableIdentity(candidate.tagId, `Trend Pen ${index + 1} tagId`);
  const tagPath = stableText(candidate.tagPath, `Trend Pen ${index + 1} tagPath`);
  const label = typeof candidate.label === 'string' ? candidate.label.trim() : '';
  const unit = typeof candidate.unit === 'string' ? candidate.unit.trim() : '';
  const visible = candidate.visible === undefined ? true : boolean(candidate.visible, `Trend Pen ${index + 1} visible`);
  const color = color(candidate.color, `Trend Pen ${index + 1} color`);
  const lineWidth = finite(candidate.lineWidth, `Trend Pen ${index + 1} lineWidth`, 1, 12);
  const lineStyle = enumValue(candidate.lineStyle, ['solid', 'dashed', 'dotted'] as const, `Trend Pen ${index + 1} lineStyle`);
  const axis = enumValue(candidate.axis, ['left', 'right'] as const, `Trend Pen ${index + 1} axis`);
  const scale = normalizeScale(candidate.scale, index);

  return Object.freeze({
    id,
    tagId,
    tagPath,
    label: label || tagPath,
    visible,
    unit,
    color,
    lineWidth,
    lineStyle,
    axis,
    scale
  });
}

function normalizeScale(value: unknown, index: number): TrendPenScale {
  if (!isRecord(value) || value.mode === undefined || value.mode === 'auto') {
    return Object.freeze({ mode: 'auto' });
  }
  if (value.mode !== 'fixed') throw new Error(`Trend Pen ${index + 1} scale mode is invalid.`);
  const minimum = finite(value.minimum, `Trend Pen ${index + 1} minimum`);
  const maximum = finite(value.maximum, `Trend Pen ${index + 1} maximum`);
  if (minimum >= maximum) throw new Error(`Trend Pen ${index + 1} fixed scale minimum must be lower than maximum.`);
  return Object.freeze({ mode: 'fixed', minimum, maximum });
}

function stableIdentity(value: unknown, label: string): string {
  const text = stableText(value, label);
  if (text.length > 160 || /[\u0000-\u001F\u007F]/.test(text)) throw new Error(`${label} is invalid.`);
  return text;
}

function stableText(value: unknown, label: string): string {
  if (typeof value !== 'string' || !value.trim() || value !== value.trim()) throw new Error(`${label} must be a non-empty trimmed string.`);
  return value;
}

function color(value: unknown, label: string): string {
  if (typeof value !== 'string' || !/^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$/.test(value)) {
    throw new Error(`${label} must use #RRGGBB or #RRGGBBAA.`);
  }
  return value.toUpperCase();
}

function finite(value: unknown, label: string, minimum?: number, maximum?: number): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw new Error(`${label} must be finite.`);
  if (minimum !== undefined && value < minimum) throw new Error(`${label} must be >= ${minimum}.`);
  if (maximum !== undefined && value > maximum) throw new Error(`${label} must be <= ${maximum}.`);
  return value;
}

function boolean(value: unknown, label: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${label} must be boolean.`);
  return value;
}

function enumValue<T extends string>(value: unknown, allowed: readonly T[], label: string): T {
  if (typeof value !== 'string' || !allowed.includes(value as T)) throw new Error(`${label} is invalid.`);
  return value as T;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function stableDomToken(value: string): string {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(36);
}
