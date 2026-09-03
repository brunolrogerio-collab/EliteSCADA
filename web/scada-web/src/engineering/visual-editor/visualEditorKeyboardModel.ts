import type { DynamoEngineering } from '../types';
import type { DynamoParameterValueEngineering } from '../../runtime/visual-navigation/runtimeVisualNavigationModel';
import type {
  VisualEditorAlignmentOperation,
  VisualEditorDistributionOperation,
  VisualEditorSizeOperation
} from './visualEditorAuthoringModel';

export type VisualEditorKeyboardCommand =
  | Readonly<{ kind: 'undo' }>
  | Readonly<{ kind: 'redo' }>
  | Readonly<{ kind: 'copy' }>
  | Readonly<{ kind: 'paste' }>
  | Readonly<{ kind: 'duplicate' }>
  | Readonly<{ kind: 'delete' }>
  | Readonly<{ kind: 'group' }>
  | Readonly<{ kind: 'ungroup' }>
  | Readonly<{ kind: 'align'; operation: VisualEditorAlignmentOperation }>
  | Readonly<{ kind: 'distribute'; operation: VisualEditorDistributionOperation }>
  | Readonly<{ kind: 'size'; operation: VisualEditorSizeOperation }>
  | Readonly<{ kind: 'lock'; locked: boolean }>
  | Readonly<{
      kind: 'dynamoParameter.set';
      objectId: string;
      definition: DynamoEngineering;
      value: DynamoParameterValueEngineering;
    }>
  | Readonly<{
      kind: 'dynamoParameter.remove';
      objectId: string;
      definition: DynamoEngineering;
      parameterKey: string;
    }>
  | Readonly<{ kind: 'selectAll' }>
  | Readonly<{ kind: 'nudge'; deltaX: number; deltaY: number }>;

export type VisualEditorKeyboardInput = Readonly<{
  key: string;
  ctrlKey?: boolean;
  metaKey?: boolean;
  shiftKey?: boolean;
  altKey?: boolean;
  targetIsEditable?: boolean;
  coarseNudge?: number;
  fineNudge?: number;
}>;

export function resolveVisualEditorKeyboardCommand(
  input: VisualEditorKeyboardInput
): VisualEditorKeyboardCommand | null {
  if (input.targetIsEditable) return null;

  const key = input.key.toLocaleLowerCase('en-US');
  const primary = input.ctrlKey === true || input.metaKey === true;
  const shift = input.shiftKey === true;
  const alt = input.altKey === true;

  if (primary && !alt) {
    if (key === 'z') return Object.freeze({ kind: shift ? 'redo' : 'undo' });
    if (key === 'y' && !shift) return Object.freeze({ kind: 'redo' });
    if (key === 'c' && !shift) return Object.freeze({ kind: 'copy' });
    if (key === 'v' && !shift) return Object.freeze({ kind: 'paste' });
    if (key === 'd' && !shift) return Object.freeze({ kind: 'duplicate' });
    if (key === 'g') return Object.freeze({ kind: shift ? 'ungroup' : 'group' });
    if (key === 'a' && !shift) return Object.freeze({ kind: 'selectAll' });
    return null;
  }

  if (key === 'delete' || key === 'backspace') return Object.freeze({ kind: 'delete' });
  if (alt) return null;

  const delta = shift ? (input.coarseNudge ?? 10) : (input.fineNudge ?? 1);
  if (!Number.isFinite(delta) || delta <= 0) return null;
  switch (key) {
    case 'arrowleft': return Object.freeze({ kind: 'nudge', deltaX: -delta, deltaY: 0 });
    case 'arrowright': return Object.freeze({ kind: 'nudge', deltaX: delta, deltaY: 0 });
    case 'arrowup': return Object.freeze({ kind: 'nudge', deltaX: 0, deltaY: -delta });
    case 'arrowdown': return Object.freeze({ kind: 'nudge', deltaX: 0, deltaY: delta });
    default: return null;
  }
}
