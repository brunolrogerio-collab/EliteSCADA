export type CanonicalStrokeStyle =
  | 'none'
  | 'solid'
  | 'dashed'
  | 'dotted'
  | 'dash-dot'
  | 'dash-dot-dot';

export function normalizeCanonicalStrokeStyle(value: unknown): CanonicalStrokeStyle {
  switch (value) {
    case 'none':
    case 'solid':
    case 'dashed':
    case 'dotted':
    case 'dash-dot':
    case 'dash-dot-dot':
      return value;
    default:
      return 'solid';
  }
}

export function effectiveStrokeWidth(style: CanonicalStrokeStyle, configuredWidth: number): number {
  if (style === 'none') return 0;
  return Number.isFinite(configuredWidth) && configuredWidth >= 0 ? configuredWidth : 0;
}

/**
 * HTML/CSS borders do not expose dash-dot variants. Keep the canonical value
 * intact and use the closest deterministic CSS presentation for box objects.
 * SVG-backed objects use svgStrokeDasharray for the exact pattern.
 */
export function cssStrokeStyle(style: CanonicalStrokeStyle): 'none' | 'solid' | 'dashed' | 'dotted' {
  switch (style) {
    case 'none': return 'none';
    case 'dotted': return 'dotted';
    case 'dashed':
    case 'dash-dot':
    case 'dash-dot-dot':
      return 'dashed';
    case 'solid':
      return 'solid';
  }
}

export function svgStrokeDasharray(style: CanonicalStrokeStyle): string | undefined {
  switch (style) {
    case 'dashed': return '8 5';
    case 'dotted': return '2 4';
    case 'dash-dot': return '8 4 2 4';
    case 'dash-dot-dot': return '8 4 2 4 2 4';
    case 'none':
    case 'solid':
      return undefined;
  }
}
