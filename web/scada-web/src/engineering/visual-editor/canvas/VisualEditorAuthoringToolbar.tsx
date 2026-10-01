import React, { useMemo } from 'react';
import type { ScreenEngineering } from '../../types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../../visual-runtime';
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
  canRedo,
  canPaste,
  onInsertObject
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  onOperation?: (operation: VisualEditorAuthoringOperation) => void;
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
  canUndo?: boolean;
  canRedo?: boolean;
  canPaste?: boolean;
  onInsertObject?: (objectType: string) => void;
}) {
  const editorText = useC07VisualEditorText();
  const text = editorText.toolbar;
  const palette = editorText.palette as Readonly<Record<string, string>>;
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

  const inserts = [
    [BUILTIN_VISUAL_OBJECT_TYPES.group, '⊞', 'group', 'Group'],
    [BUILTIN_VISUAL_OBJECT_TYPES.rectangle, '▭', 'rectangle', 'Rectangle'],
    [BUILTIN_VISUAL_OBJECT_TYPES.ellipse, '◯', 'ellipse', 'Ellipse'],
    [BUILTIN_VISUAL_OBJECT_TYPES.line, '╱', 'line', 'Line'],
    [BUILTIN_VISUAL_OBJECT_TYPES.polygon, '⬠', 'polygon', 'Polygon'],
    [BUILTIN_VISUAL_OBJECT_TYPES.text, 'T', 'text', 'Text'],
    [BUILTIN_VISUAL_OBJECT_TYPES.image, '▧', 'image', 'Image'],
    [BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay, '#', 'valueDisplay', 'Value display'],
    [BUILTIN_VISUAL_OBJECT_TYPES.trend, '⌁', 'trend', 'Trend'],
    [BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser, '!', 'alarmBrowser', 'Alarm browser'],
    [BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser, '≡', 'eventBrowser', 'Event browser'],
    [BUILTIN_VISUAL_OBJECT_TYPES.button, '▰', 'button', 'Button'],
    [BUILTIN_VISUAL_OBJECT_TYPES.slider, '☷', 'slider', 'Slider'],
    [BUILTIN_VISUAL_OBJECT_TYPES.numericInput, '123', 'numericInput', 'Numeric input']
  ] as const;

  return <div className="visual-editor-authoring-toolbar" role="toolbar" aria-label={text.aria} data-testid="visual-editor-authoring-toolbar">
    <ToolbarGroup label="Insert">
      {inserts.map(([objectType, glyph, labelKey, fallbackLabel]) => <Tool
        key={objectType}
        label={palette[labelKey] ?? fallbackLabel}
        disabled={!onInsertObject}
        onClick={() => onInsertObject?.(objectType)}
        dataObjectType={objectType}
      >{glyph}</Tool>)}
    </ToolbarGroup>
    <ToolbarGroup label={text.history}>
      <Tool label={text.undo} disabled={!onKeyboardCommand || canUndo === false} onClick={() => onKeyboardCommand?.({ kind: 'undo' })}>↶</Tool>
      <Tool label={text.redo} disabled={!onKeyboardCommand || canRedo === false} onClick={() => onKeyboardCommand?.({ kind: 'redo' })}>↷</Tool>
      <Tool label={text.copy} disabled={!onKeyboardCommand || state.selectionCount === 0 || !state.sameParent} onClick={() => onKeyboardCommand?.({ kind: 'copy' })}>⧉</Tool>
      <Tool label={text.paste} disabled={!onKeyboardCommand || canPaste === false} onClick={() => onKeyboardCommand?.({ kind: 'paste' })}>▣</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.align}>
      <Tool label={text.alignLeft} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('left')}>⇤</Tool>
      <Tool label={text.alignHorizontalCenters} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('horizontalCenter')}>↔</Tool>
      <Tool label={text.alignRight} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('right')}>⇥</Tool>
      <Tool label={text.alignTop} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('top')}>⇡</Tool>
      <Tool label={text.alignVerticalMiddles} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('verticalMiddle')}>↕</Tool>
      <Tool label={text.alignBottom} disabled={!authoringAvailable || !state.canAlign} onClick={() => align('bottom')}>⇣</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.distribute}>
      <Tool label={text.distributeHorizontalCenters} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('horizontalCenters')}>⋯</Tool>
      <Tool label={text.distributeHorizontalSpacing} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('horizontalSpacing')}>↔·</Tool>
      <Tool label={text.distributeVerticalCenters} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('verticalCenters')}>⋮</Tool>
      <Tool label={text.distributeVerticalSpacing} disabled={!authoringAvailable || !state.canDistribute} onClick={() => distribute('verticalSpacing')}>↕·</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.size}>
      <Tool label={text.sameWidth} disabled={!authoringAvailable || !state.canSize} onClick={() => size('sameWidth')}>↔</Tool>
      <Tool label={text.sameHeight} disabled={!authoringAvailable || !state.canSize} onClick={() => size('sameHeight')}>↕</Tool>
      <Tool label={text.sameSize} disabled={!authoringAvailable || !state.canSize} onClick={() => size('sameSize')}>□</Tool>
    </ToolbarGroup>

    <ToolbarGroup label={text.structure}>
      <Tool label={text.group} disabled={!authoringAvailable || !state.canGroup} onClick={() => dispatchOperation({ kind: 'group', objectIds: state.selectedObjectIds })}>⊞</Tool>
      <Tool label={text.ungroup} disabled={!authoringAvailable || !state.canUngroup} onClick={() => dispatchOperation({ kind: 'ungroup', objectIds: state.selectedObjectIds })}>⊟</Tool>
      <Tool
        label={state.nextLockedValue ? text.lockSelection : text.unlockSelection}
        disabled={!authoringAvailable || !state.canToggleLock}
        onClick={() => dispatchOperation({ kind: 'lock', objectIds: state.selectedObjectIds, locked: state.nextLockedValue })}
      >{state.nextLockedValue ? '▣' : '▢'}</Tool>
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
  children,
  dataObjectType
}: {
  label: string;
  disabled: boolean;
  onClick: () => void;
  children: React.ReactNode;
  dataObjectType?: string;
}) {
  return <button type="button" title={label} aria-label={label} disabled={disabled} onClick={onClick} data-insert-object-type={dataObjectType}>{children}</button>;
}
