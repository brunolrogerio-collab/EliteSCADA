import React, { useMemo } from 'react';
import type { ScreenEngineering } from '../../types';
import { useC07VisualEditorText } from '../c07VisualEditorI18n';
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
  canUndo,
  canRedo
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  onOperation?: (operation: VisualEditorAuthoringOperation) => void;
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
  canUndo?: boolean;
  canRedo?: boolean;
}) {
  const text = useC07VisualEditorText().toolbar;
  const state = useMemo(
    () => buildVisualEditorAuthoringToolbarState(screen, selectedObjectIds),
    [screen, selectedObjectIds]
  );
  const authoringAvailable = Boolean(onOperation || onKeyboardCommand);

  const dispatchOperation = (operation: VisualEditorAuthoringOperation): void => {
    if (onOperation) {
      onOperation(operation);
      return;
    }
    if (!onKeyboardCommand) return;
    switch (operation.kind) {
      case 'align':
        onKeyboardCommand({ kind: 'align', operation: operation.operation });
        return;
      case 'distribute':
        onKeyboardCommand({ kind: 'distribute', operation: operation.operation });
        return;
      case 'size':
        onKeyboardCommand({ kind: 'size', operation: operation.operation });
        return;
      case 'group':
        onKeyboardCommand({ kind: 'group' });
        return;
      case 'ungroup':
        onKeyboardCommand({ kind: 'ungroup' });
        return;
      case 'lock':
        onKeyboardCommand({ kind: 'lock', locked: operation.locked });
        return;
    }
  };

  const align = (operation: VisualEditorAlignmentOperation) => dispatchOperation({
    kind: 'align', objectIds: state.selectedObjectIds, operation
  });
  const distribute = (operation: VisualEditorDistributionOperation) => dispatchOperation({
    kind: 'distribute', objectIds: state.selectedObjectIds, operation
  });
  const size = (operation: VisualEditorSizeOperation) => {
    if (!state.referenceObjectId) return;
    dispatchOperation({
      kind: 'size', objectIds: state.selectedObjectIds,
      referenceObjectId: state.referenceObjectId, operation
    });
  };

  return <div className="visual-editor-authoring-toolbar" role="toolbar" aria-label={text.aria} data-testid="visual-editor-authoring-toolbar">
    <ToolbarGroup label={text.history}>
      <Tool label={text.undo} disabled={!onKeyboardCommand || canUndo === false} onClick={() => onKeyboardCommand?.({ kind: 'undo' })}>↶</Tool>
      <Tool label={text.redo} disabled={!onKeyboardCommand || canRedo === false} onClick={() => onKeyboardCommand?.({ kind: 'redo' })}>↷</Tool>
      <Tool label={text.copy} disabled={!onKeyboardCommand || state.selectionCount === 0} onClick={() => onKeyboardCommand?.({ kind: 'copy' })}>{text.copy}</Tool>
      <Tool label={text.paste} disabled={!onKeyboardCommand} onClick={() => onKeyboardCommand?.({ kind: 'paste' })}>{text.paste}</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.align}>
      <Tool label={text.alignLeft} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('left')}>L</Tool>
      <Tool label={text.alignHorizontalCenters} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('horizontalCenter')}>HC</Tool>
      <Tool label={text.alignRight} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('right')}>R</Tool>
      <Tool label={text.alignTop} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('top')}>T</Tool>
      <Tool label={text.alignVerticalMiddles} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('verticalMiddle')}>VM</Tool>
      <Tool label={text.alignBottom} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('bottom')}>B</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.distribute}>
      <Tool label={text.distributeHorizontalCenters} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('horizontalCenters')}>H·</Tool>
      <Tool label={text.distributeHorizontalSpacing} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('horizontalSpacing')}>H↔</Tool>
      <Tool label={text.distributeVerticalCenters} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('verticalCenters')}>V·</Tool>
      <Tool label={text.distributeVerticalSpacing} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('verticalSpacing')}>V↕</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.size}>
      <Tool label={text.sameWidth} disabled={!authoringAvailable || !state.canSize} onClick={() => size('sameWidth')}>W</Tool>
      <Tool label={text.sameHeight} disabled={!authoringAvailable || !state.canSize} onClick={() => size('sameHeight')}>H</Tool>
      <Tool label={text.sameSize} disabled={!authoringAvailable || !state.canSize} onClick={() => size('sameSize')}>WH</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.structure}>
      <Tool label={text.group} disabled={!authoringAvailable || !state.canGroup} onClick={() => dispatchOperation({ kind: 'group', objectIds: state.selectedObjectIds })}>{text.group}</Tool>
      <Tool label={text.ungroup} disabled={!authoringAvailable || !state.canUngroup} onClick={() => dispatchOperation({ kind: 'ungroup', objectIds: state.selectedObjectIds })}>{text.ungroup}</Tool>
      <Tool
        label={state.nextLockedValue ? text.lockSelection : text.unlockSelection}
        disabled={!authoringAvailable || !state.canToggleLock}
        onClick={() => dispatchOperation({ kind: 'lock', objectIds: state.selectedObjectIds, locked: state.nextLockedValue })}
      >{state.nextLockedValue ? text.lock : text.unlock}</Tool>
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
