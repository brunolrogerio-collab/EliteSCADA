import type {
  VisualAssetEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyObject,
  VisualEngineeringPropertyValue
} from '../types';

export const SVG_PAINT_OVERRIDES_PROPERTY = 'svgPaintOverrides';
export const SVG_METADATA = Object.freeze({
  version: 'elitescada.svg.version',
  palette: 'elitescada.svg.palette',
  slots: 'elitescada.svg.slots',
  viewBox: 'elitescada.svg.viewBox',
  unsupportedPaint: 'elitescada.svg.unsupportedPaint'
});

export type SvgPaintSlotOverride = Readonly<{
  fill?: string;
  stroke?: string;
  strokeWidth?: number;
}>;

export type SvgPaintOverrides = Readonly<{
  version: 1;
  palette?: Readonly<Record<string, string>>;
  slots?: Readonly<Record<string, SvgPaintSlotOverride>>;
}>;

export type SvgPaintPaletteEntry = Readonly<{
  color: string;
  fill: boolean;
  stroke: boolean;
}>;

export type SvgPaintSlotMetadata = Readonly<{
  name: string;
  fill: boolean;
  stroke: boolean;
  strokeWidth: boolean;
}>;

export function readSvgPaintOverrides(element: VisualElementEngineering): SvgPaintOverrides {
  return normalizeSvgPaintOverrides(element.properties?.[SVG_PAINT_OVERRIDES_PROPERTY]);
}

export function normalizeSvgPaintOverrides(value: unknown): SvgPaintOverrides {
  const input = isRecord(value) ? value : {};
  const palette: Record<string, string> = {};
  const slots: Record<string, SvgPaintSlotOverride> = {};

  if (isRecord(input.palette)) {
    for (const [source, target] of Object.entries(input.palette)) {
      const normalizedSource = normalizeSvgColor(source);
      const normalizedTarget = typeof target === 'string' ? normalizeSvgColor(target) : null;
      if (normalizedSource && normalizedTarget) palette[normalizedSource] = normalizedTarget;
    }
  }

  if (isRecord(input.slots)) {
    for (const [name, raw] of Object.entries(input.slots)) {
      if (!/^[A-Za-z][A-Za-z0-9._-]{0,63}$/.test(name) || !isRecord(raw)) continue;
      const next: { fill?: string; stroke?: string; strokeWidth?: number } = {};
      if (typeof raw.fill === 'string') {
        const fill = normalizeSvgColor(raw.fill);
        if (fill) next.fill = fill;
      }
      if (typeof raw.stroke === 'string') {
        const stroke = normalizeSvgColor(raw.stroke);
        if (stroke) next.stroke = stroke;
      }
      if (typeof raw.strokeWidth === 'number' && Number.isFinite(raw.strokeWidth) && raw.strokeWidth >= 0 && raw.strokeWidth <= 10_000)
        next.strokeWidth = raw.strokeWidth;
      if (Object.keys(next).length > 0) slots[name] = Object.freeze(next);
    }
  }

  return Object.freeze({
    version: 1 as const,
    ...(Object.keys(palette).length ? { palette: Object.freeze(palette) } : {}),
    ...(Object.keys(slots).length ? { slots: Object.freeze(slots) } : {})
  });
}

export function svgPaintOverridesAsEngineeringValue(value: SvgPaintOverrides): VisualEngineeringPropertyObject {
  return value as unknown as VisualEngineeringPropertyObject;
}

export function readSvgPaintMetadata(asset: VisualAssetEngineering | null | undefined): Readonly<{
  palette: readonly SvgPaintPaletteEntry[];
  slots: readonly SvgPaintSlotMetadata[];
}> {
  const metadata = asset?.metadata ?? {};
  return Object.freeze({
    palette: parseArray<SvgPaintPaletteEntry>(metadata[SVG_METADATA.palette])
      .filter(item => Boolean(normalizeSvgColor(item.color))),
    slots: parseArray<SvgPaintSlotMetadata>(metadata[SVG_METADATA.slots])
      .filter(item => /^[A-Za-z][A-Za-z0-9._-]{0,63}$/.test(item.name))
  });
}

export function svgSymbolPropertyIsDriven(element: VisualElementEngineering, key: string): boolean {
  if (element.properties && Object.hasOwn(element.properties, key)) return true;
  if (element.bindings?.some(binding => binding.key === key)) return true;
  if (element.propertyExpressions?.some(expression => expression.propertyKey === key)) return true;
  if (element.booleanConditions?.some(condition => condition.propertyKey === key)) return true;
  if (element.propertyMaps?.some(map => map.propertyKey === key)) return true;
  return false;
}

export function normalizeSvgColor(raw: string | null | undefined): string | null {
  const value = raw?.trim();
  if (!value || value.toLowerCase() === 'none') return null;
  const names: Record<string, string> = {
    black: '#000000', white: '#FFFFFF', red: '#FF0000', green: '#008000',
    blue: '#0000FF', yellow: '#FFFF00', gray: '#808080', grey: '#808080',
    silver: '#C0C0C0', orange: '#FFA500', purple: '#800080', transparent: '#00000000'
  };
  const named = names[value.toLowerCase()];
  if (named) return named;
  const match = /^#([0-9a-f]{3}|[0-9a-f]{4}|[0-9a-f]{6}|[0-9a-f]{8})$/i.exec(value);
  if (match) {
    const hex = match[1].toUpperCase();
    if (hex.length === 3 || hex.length === 4)
      return '#' + [...hex].map(char => char + char).join('');
    return '#' + hex;
  }
  const rgb = /^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$/i.exec(value);
  if (rgb) {
    const parts = rgb.slice(1).map(Number);
    if (parts.every(part => part >= 0 && part <= 255))
      return '#' + parts.map(part => part.toString(16).padStart(2, '0').toUpperCase()).join('');
  }
  return null;
}

function isRecord(value: unknown): value is Record<string, VisualEngineeringPropertyValue | unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseArray<T>(raw: string | undefined): T[] {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed as T[] : [];
  } catch {
    return [];
  }
}
