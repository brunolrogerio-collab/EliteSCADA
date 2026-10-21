export const DEFAULT_RUNTIME_DESIGN_WIDTH = 1920;
export const DEFAULT_RUNTIME_DESIGN_HEIGHT = 1080;

export type RuntimeLogicalSize = Readonly<{
  width: number;
  height: number;
}>;

export type RuntimeLogicalTransform = RuntimeLogicalSize & Readonly<{
  viewportWidth: number;
  viewportHeight: number;
  scale: number;
  offsetX: number;
  offsetY: number;
}>;

/**
 * Runtime keeps a deterministic logical canvas independent of the physical
 * display resolution. Screen orientation changes only its aspect ratio; the
 * Runtime still scales the whole canvas uniformly to fit the available area.
 */
export function resolveRuntimeLogicalSize(orientation: 'landscape' | 'portrait' | null = 'landscape'): RuntimeLogicalSize {
  const portrait = orientation === 'portrait';
  return Object.freeze({
    width: portrait ? DEFAULT_RUNTIME_DESIGN_HEIGHT : DEFAULT_RUNTIME_DESIGN_WIDTH,
    height: portrait ? DEFAULT_RUNTIME_DESIGN_WIDTH : DEFAULT_RUNTIME_DESIGN_HEIGHT
  });
}

export function calculateRuntimeLogicalTransform(
  viewportWidth: number,
  viewportHeight: number,
  designWidth: number,
  designHeight: number
): RuntimeLogicalTransform {
  const width = positiveFinite(designWidth, DEFAULT_RUNTIME_DESIGN_WIDTH);
  const height = positiveFinite(designHeight, DEFAULT_RUNTIME_DESIGN_HEIGHT);
  const availableWidth = Math.max(0, Number.isFinite(viewportWidth) ? viewportWidth : 0);
  const availableHeight = Math.max(0, Number.isFinite(viewportHeight) ? viewportHeight : 0);
  const scale = availableWidth > 0 && availableHeight > 0
    ? Math.min(availableWidth / width, availableHeight / height)
    : 0;

  return Object.freeze({
    width,
    height,
    viewportWidth: availableWidth,
    viewportHeight: availableHeight,
    scale,
    offsetX: (availableWidth - width * scale) / 2,
    offsetY: (availableHeight - height * scale) / 2
  });
}

export function viewportPointToLogical(
  clientX: number,
  clientY: number,
  viewportLeft: number,
  viewportTop: number,
  transform: RuntimeLogicalTransform
): Readonly<{ x: number; y: number }> | null {
  if (!(transform.scale > 0)) return null;
  return Object.freeze({
    x: (clientX - viewportLeft - transform.offsetX) / transform.scale,
    y: (clientY - viewportTop - transform.offsetY) / transform.scale
  });
}

function positiveFinite(value: number, fallback: number): number {
  return Number.isFinite(value) && value > 0 ? value : fallback;
}
