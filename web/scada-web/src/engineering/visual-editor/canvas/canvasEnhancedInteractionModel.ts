import type { ScreenEngineering } from '../../types';
import type { VisualEditorKeyboardCommand } from '../visualEditorKeyboardModel';
import type { VisualEditorLogicalPoint, VisualEditorMarqueeMode } from './visualEditorSelectionModel';

/** CAD-style marquee: left-to-right contains, right-to-left intersects. */
export function visualEditorMarqueeModeForDrag(
  start: VisualEditorLogicalPoint,
  end: VisualEditorLogicalPoint
): VisualEditorMarqueeMode {
  return end.x >= start.x ? 'contain' : 'intersect';
}

export function rootVisualEditorObjectIds(screen: ScreenEngineering): readonly string[] {
  return Object.freeze((screen.elements ?? []).flatMap(element => element.id?.trim() ? [element.id] : []));
}

export function visualEditorKeyboardCommandMutatesSelection(
  command: VisualEditorKeyboardCommand
): boolean {
  return command.kind === 'delete'
    || command.kind === 'duplicate'
    || command.kind === 'group'
    || command.kind === 'ungroup'
    || command.kind === 'align'
    || command.kind === 'distribute'
    || command.kind === 'size'
    || command.kind === 'lock'
    || command.kind === 'nudge';
}
