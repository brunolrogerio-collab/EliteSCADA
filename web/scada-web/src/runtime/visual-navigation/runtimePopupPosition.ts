import type { PopupEngineering } from '../../engineering/types';
import type { RuntimeLogicalSize } from './runtimeLogicalCanvas';

/**
 * Popup definitions intentionally do not own canonical width/height fields.
 * Keep at least this much of the Popup's top-left region inside the logical HMI
 * stage so an off-canvas authored position remains reachable without inventing
 * frontend-only Popup dimensions.
 */
export const POPUP_MIN_VISIBLE_LOGICAL_PX = 48;

export type RuntimePopupLogicalPosition = Readonly<{
  x: number;
  y: number;
}>;

export function resolvePopupLogicalPosition(
  popup: Pick<PopupEngineering, 'x' | 'y'>,
  designSize: RuntimeLogicalSize
): RuntimePopupLogicalPosition {
  const authoredX = typeof popup.x === 'number' && Number.isFinite(popup.x) ? popup.x : 0;
  const authoredY = typeof popup.y === 'number' && Number.isFinite(popup.y) ? popup.y : 0;
  const maxX = Math.max(0, designSize.width - POPUP_MIN_VISIBLE_LOGICAL_PX);
  const maxY = Math.max(0, designSize.height - POPUP_MIN_VISIBLE_LOGICAL_PX);

  return Object.freeze({
    x: Math.min(Math.max(authoredX, 0), maxX),
    y: Math.min(Math.max(authoredY, 0), maxY)
  });
}
