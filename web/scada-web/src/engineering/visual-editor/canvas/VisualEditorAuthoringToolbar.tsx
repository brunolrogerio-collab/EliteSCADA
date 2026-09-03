import React, { useMemo } from 'react';
import type { ScreenEngineering } from '../../types';
import type {
  VisualEditorAuthoringOperation,
  VisualEditorAlignmentOperation,
  VisualEditorDistributionOperation,
  VisualEditorSizeOperation
} from '../visualEditorAuthoringModel';
import type { VisualEditorKeyboardCommand } from '../visualEditorKeyboardModel';
import { buildVisualEditorAuthoringToolbarState } from './visualEditorAuthoringToolbarModel';
import './VisualEditorAuthoringToolbar.css';

export function VisualEditorAuthoringToolbar({
  screen,
  selectedObjectIds,
  onOperation,
  onKeyboardCommand,
  canUndo = false,
  canRedo = false
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  onOperation?: (operation: VisualEditorAuthoringOperation) => void;
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
  canUndo?: boolean;
  canRedo?: boolean;
}) {
  const state = useMemo(
    () => buildVisualEditorAuthoringToolbarState(screen, selectedObjectIds),
    [screen, selectedObjectIds]
  );
  const disabled = !onOperation;

  const align = (operation: VisualEditorAlignmentOperation) => onOperation?.({
    kind: 'align', objectIds: state.selectedObjectIds, operation
  });
  const distribute = (operation: VisualEditorDistributionOperation) => onOperation?.({
    kind: 'distribute', objectIds: state.selectedObjectIds, operation
  });
  const size = (operation: VisualEditorSizeOperation) => {
    if (!state.referenceObjectId) return;
    onOperation?.({
      kind: 'size', objectIds: state.selectedObjectIds,
      referenceObjectId: state.referenceObjectId, operation
    });
  };

  return <div className="visual-editor-authoring-toolbar" role="toolbar" aria-label="Visual authoring operations" data-testid="visual-editor-authoring-toolbar">
    <ToolbarGroup label="History">
      <Tool label="Undo" disabled={!onKeyboardCommand || !canUndo} onClick={() => onKeyboardCommand?.({ kind: 'undo' })}>↶</Tool>
      <Tool label="Redo" disabled={!onKeyboardCommand || !canRedo} onClick={() => onKeyboardCommand?.({ kind: 'redo' })}>↷</Tool>
      <Tool label="Copy" disabled={!onKeyboardCommand || state.selectionCount === 0} onClick={() => onKeyboardCommand?.({ kind: 'copy' })}>Copy</Tool>
      <Tool label="Paste" disabled={!onKeyboardCommand} onClick={() => onKeyboardCommand?.({ kind: 'paste' })}>Paste</Tool>
    </ToolbarGroup>

    <ToolbarGroup label="Align">
      <Tool label="Align left" disabled={disabled || !state.canAlign} onClick={() => align('left')}>L</Tool>
      <Tool label="Align horizontal centers" disabled={disabled || !state.canAlign} onClick={() => align('horizontalCenter')}>HC</Tool>
      <Tool label="Align right" disabled={disabled || !state.canAlign} onClick={() => align('right')}>R</Tool>
      <Tool label="Align top" disabled={disabled || !state.canAlign} onClick={() => align('top')}>T</Tool>
      <Tool label="Align vertical middles" disabled={disabled || !state.canAlign} onClick={() => align('verticalMiddle')}>VM</Tool>
      <Tool label="Align bottom" disabled={disabled || !state.canAlign} onClick={() => align('bottom')}>B</Tool>
    </ToolbarGroup>

    <ToolbarGroup label="Distribute">
      <Tool label="Distribute horizontal centers" disabled={disabled || !state.canDistribute} onClick={() => distribute('horizontalCenters')}>H·</Tool>
      <Tool label="Distribute horizontal spacing" disabled={disabled || !state.canDistribute} onClick={() => distribute('horizontalSpacing')}>H↔</Tool>
      <Tool label="Distribute vertical centers" disabled={disabled || !state.canDistribute} onClick={() => distribute('verticalCenters')}>V·</Tool>
      <Tool label="Distribute vertical spacing" disabled={disabled || !state.canDistribute} onClick={() => distribute('verticalSpacing')}>V↕</Tool>
    </ToolbarGroup>

    <ToolbarGroup label="Size">
      <Tool label="Same width" disabled={disabled || !state.canSize} onClick={() => size('sameWidth')}>W</Tool>
      <Tool label="Same height" disabled={disabled || !state.canSize} onClick={() => size('sameHeight')}>H</Tool>
      <Tool label="Same size" disabled={disabled || !state.canSize} onClick={() => size('sameSize')}>WH</Tool>
    </ToolbarGroup>

    <ToolbarGroup label="Structure">
      <Tool label="Group" disabled={disabled || !state.canGroup} onClick={() => onOperation?.({ kind: 'group', objectIds: state.selectedObjectIds })}>Group</Tool>
      <Tool label="Ungroup" disabled={disabled || !state.canUngroup} onClick={() => onOperation?.({ kind: 'ungroup', objectIds: state.selectedObjectIds })}>Ungroup</Tool>
      <Tool
        label={state.nextLockedValue ? 'Lock selection' : 'Unlock selection'}
        disabled={disabled || !state.canToggleLock}
        onClick={() => onOperation?.({ kind: 'lock', objectIds: state.selectedObjectIds, locked: state.nextLockedValue })}
      >{state.nextLockedValue ? 'Lock' : 'Unlock'}</Tool>
    </ToolbarGroup>
  </div>;
}

function ToolbarGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return <div className="visual-editor-authoring-toolbar__group" role="group" aria-label={label}>{children}</div>;
}

function Tool({
  label,
  disabled,
  onClick,
  children
}: {
  label: string;
  disabled: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return <button type="button" title={label} aria-label={label} disabled={disabled} onClick={onClick}>{children}</button>;
}
