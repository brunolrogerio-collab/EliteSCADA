import type { CSSProperties } from 'react';

export const VISUAL_DEFINITION_SURFACE_KEYS = Object.freeze({
  backgroundColor: 'backgroundColor',
  backgroundImageAssetId: 'backgroundImageAssetId',
  backgroundImageFit: 'backgroundImageFit'
} as const);

export type VisualDefinitionBackgroundFit = 'cover' | 'contain' | 'stretch' | 'center' | 'tile';

export type VisualDefinitionSurfaceConfig = Readonly<{
  backgroundColor: string | null;
  backgroundImageAssetId: string | null;
  backgroundImageFit: VisualDefinitionBackgroundFit;
}>;

export type VisualDefinitionSurfacePatch = Readonly<{
  backgroundColor?: string | null;
  backgroundImageAssetId?: string | null;
  backgroundImageFit?: VisualDefinitionBackgroundFit | null;
}>;

export function readVisualDefinitionSurfaceConfig(
  properties: Readonly<Record<string, string>> | null | undefined
): VisualDefinitionSurfaceConfig {
  const color = normalizeBackgroundColor(properties?.[VISUAL_DEFINITION_SURFACE_KEYS.backgroundColor] ?? null);
  const assetId = normalizeStableText(properties?.[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageAssetId] ?? null);
  const fit = normalizeFit(properties?.[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageFit] ?? null);
  return Object.freeze({ backgroundColor: color, backgroundImageAssetId: assetId, backgroundImageFit: fit });
}

export function applyVisualDefinitionSurfacePatch<T extends Readonly<{
  properties?: Record<string, string> | null;
}>>(
  definition: T,
  patch: VisualDefinitionSurfacePatch
): T {
  const properties = { ...(definition.properties ?? {}) };
  if (patch.backgroundColor !== undefined) {
    const color = normalizeBackgroundColor(patch.backgroundColor);
    if (color === null) delete properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundColor];
    else properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundColor] = color;
  }
  if (patch.backgroundImageAssetId !== undefined) {
    const assetId = normalizeStableText(patch.backgroundImageAssetId);
    if (assetId === null) delete properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageAssetId];
    else properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageAssetId] = assetId;
  }
  if (patch.backgroundImageFit !== undefined) {
    if (patch.backgroundImageFit === null) delete properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageFit];
    else properties[VISUAL_DEFINITION_SURFACE_KEYS.backgroundImageFit] = normalizeFit(patch.backgroundImageFit);
  }
  return { ...definition, properties } as T;
}

export function resolveVisualDefinitionSurfaceStyle(
  properties: Readonly<Record<string, string>> | null | undefined,
  assetUrl?: (assetId: string) => string
): CSSProperties {
  const config = readVisualDefinitionSurfaceConfig(properties);
  const style: CSSProperties = {};
  if (config.backgroundColor) style.backgroundColor = config.backgroundColor;
  if (!config.backgroundImageAssetId || !assetUrl) return style;

  const url = assetUrl(config.backgroundImageAssetId);
  style.backgroundImage = `url(${JSON.stringify(url)})`;
  style.backgroundPosition = 'center';
  switch (config.backgroundImageFit) {
    case 'cover': style.backgroundSize = 'cover'; style.backgroundRepeat = 'no-repeat'; break;
    case 'contain': style.backgroundSize = 'contain'; style.backgroundRepeat = 'no-repeat'; break;
    case 'stretch': style.backgroundSize = '100% 100%'; style.backgroundRepeat = 'no-repeat'; break;
    case 'center': style.backgroundSize = 'auto'; style.backgroundRepeat = 'no-repeat'; break;
    case 'tile': style.backgroundSize = 'auto'; style.backgroundRepeat = 'repeat'; break;
  }
  return style;
}

export function normalizeBackgroundColor(value: string | null | undefined): string | null {
  const normalized = normalizeStableText(value);
  if (normalized === null) return null;
  if (normalized.toLowerCase() === 'transparent') return 'transparent';
  if (/^#[0-9a-f]{3}([0-9a-f]{3})?([0-9a-f]{2})?$/i.test(normalized)) return normalized.toUpperCase();
  throw new Error(`Screen/Popup background color '${normalized}' must be transparent or a canonical hexadecimal color.`);
}

function normalizeFit(value: string | null | undefined): VisualDefinitionBackgroundFit {
  const normalized = normalizeStableText(value);
  if (normalized === null) return 'cover';
  switch (normalized.toLowerCase()) {
    case 'contain': return 'contain';
    case 'stretch': return 'stretch';
    case 'center': return 'center';
    case 'tile': return 'tile';
    case 'cover': return 'cover';
    default: throw new Error(`Unknown visual definition background fit '${value}'.`);
  }
}

function normalizeStableText(value: string | null | undefined): string | null {
  const normalized = value?.trim() ?? '';
  if (!normalized) return null;
  if (/[\u0000-\u001f\u007f]/.test(normalized)) throw new Error('Visual definition surface value contains control characters.');
  return normalized;
}
