import type { PopupEngineering, VisualElementEngineering } from '../../engineering/types';
import type { RuntimeLogicalSize } from './runtimeLogicalCanvas';

/**
 * Popup definitions intentionally do not own canonical width/height fields.
 * Keep at least this much of the Popup's top-left region inside the logical HMI
 * stage so an off-canvas authored position remains reachable.
 */
export const POPUP_MIN_VISIBLE_LOGICAL_PX = 48;

export type RuntimePopupLogicalBounds = Readonly<{
  width: number;
  height: number;
}>;

export type RuntimePopupLogicalPosition = Readonly<{
  x: number;
  y: number;
}>;

/**
 * Derive the Runtime Popup box from authored visual geometry. Popup definitions
 * deliberately have no frontend-owned width/height contract, so the canonical
 * content is the authority. Nested children are included relative to their
 * authored parent origin; no project- or Popup-specific dimensions are invented.
 */
export function resolvePopupLogicalBounds(
  popup: Pick<PopupEngineering, 'elements'>
): RuntimePopupLogicalBounds {
  let maxX = 0;
  let maxY = 0;

  const visit = (element: VisualElementEngineering, parentX: number, parentY: number) => {
    const x = logicalNumber(element.properties?.x, 0);
    const y = logicalNumber(element.properties?.y, 0);
    const width = Math.max(0, logicalNumber(element.properties?.width, 100));
    const height = Math.max(0, logicalNumber(element.properties?.height, 100));
    const absoluteX = parentX + x;
    const absoluteY = parentY + y;

    maxX = Math.max(maxX, absoluteX + width);
    maxY = Math.max(maxY, absoluteY + height);

    for (const child of element.children ?? []) {
      visit(child, absoluteX, absoluteY);
    }
  };

  for (const element of popup.elements ?? []) visit(element, 0, 0);

  return Object.freeze({
    width: Math.max(1, maxX),
    height: Math.max(1, maxY)
  });
}

/**
 * Resolve a Popup origin in the fixed logical HMI coordinate space. The
 * optional authored bounds extend the original contract: callers that do not
 * provide bounds retain the historical minimum-visible clamp, while Runtime
 * composition can keep a known authored box fully visible when it fits.
 */
export function resolvePopupLogicalPosition(
  popup: Pick<PopupEngineering, 'x' | 'y'>,
  designSize: RuntimeLogicalSize,
  bounds?: RuntimePopupLogicalBounds
): RuntimePopupLogicalPosition {
  const authoredX = typeof popup.x === 'number' && Number.isFinite(popup.x) ? popup.x : 0;
  const authoredY = typeof popup.y === 'number' && Number.isFinite(popup.y) ? popup.y : 0;
  const visibleWidth = bounds
    ? Math.min(designSize.width, Math.max(POPUP_MIN_VISIBLE_LOGICAL_PX, bounds.width))
    : POPUP_MIN_VISIBLE_LOGICAL_PX;
  const visibleHeight = bounds
    ? Math.min(designSize.height, Math.max(POPUP_MIN_VISIBLE_LOGICAL_PX, bounds.height))
    : POPUP_MIN_VISIBLE_LOGICAL_PX;
  const maxX = Math.max(0, designSize.width - visibleWidth);
  const maxY = Math.max(0, designSize.height - visibleHeight);

  return Object.freeze({
    x: Math.min(Math.max(authoredX, 0), maxX),
    y: Math.min(Math.max(authoredY, 0), maxY)
  });
}

function logicalNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}
