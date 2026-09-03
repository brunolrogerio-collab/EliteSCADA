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
 * C09 owns the Runtime presentation transform, not the C07 Screen schema.
 * Until C07 defines a canonical authored logical resolution, Runtime uses one
 * deterministic 1920x1080 logical canvas and does not inspect Screen properties
 * for undeclared sizing keys.
 */
export function resolveRuntimeLogicalSize(): RuntimeLogicalSize {
  return Object.freeze({
    width: DEFAULT_RUNTIME_DESIGN_WIDTH,
    height: DEFAULT_RUNTIME_DESIGN_HEIGHT
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
