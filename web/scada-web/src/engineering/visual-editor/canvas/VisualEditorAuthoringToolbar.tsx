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
  onInsertObject,
  placement = 'all'
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  onOperation?: (operation: VisualEditorAuthoringOperation) => void;
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
  canUndo?: boolean;
  canRedo?: boolean;
  canPaste?: boolean;
  onInsertObject?: (objectType: string) => void;
  placement?: 'all' | 'authoring' | 'arrangement';
}) {
  const editorText = useC07VisualEditorText();
  const text = editorText.toolbar;
  const palette = editorText.palette;
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
    [BUILTIN_VISUAL_OBJECT_TYPES.group, '⊞', 'group'],
    [BUILTIN_VISUAL_OBJECT_TYPES.rectangle, '▭', 'rectangle'],
    [BUILTIN_VISUAL_OBJECT_TYPES.ellipse, '◯', 'ellipse'],
    [BUILTIN_VISUAL_OBJECT_TYPES.line, '╱', 'line'],
    [BUILTIN_VISUAL_OBJECT_TYPES.arc, '◜', 'arc'],
    [BUILTIN_VISUAL_OBJECT_TYPES.bezier, '⌒', 'bezier'],
    [BUILTIN_VISUAL_OBJECT_TYPES.polygon, '⬠', 'polygon'],
    [BUILTIN_VISUAL_OBJECT_TYPES.text, 'T', 'text'],
    [BUILTIN_VISUAL_OBJECT_TYPES.image, '▧', 'image'],
    [BUILTIN_VISUAL_OBJECT_TYPES.model3d, '◈', 'model3d'],
    [BUILTIN_VISUAL_OBJECT_TYPES.videoPlayer, '▶', 'videoPlayer'],
    [BUILTIN_VISUAL_OBJECT_TYPES.pdfViewer, 'PDF', 'pdfViewer'],
    [BUILTIN_VISUAL_OBJECT_TYPES.reportLauncher, '▤', 'reportLauncher'],
    [BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay, '#', 'valueDisplay'],
    [BUILTIN_VISUAL_OBJECT_TYPES.trend, '⌁', 'trend'],
    [BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser, '!', 'alarmBrowser'],
    [BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser, '≡', 'eventBrowser'],
    [BUILTIN_VISUAL_OBJECT_TYPES.button, '▰', 'button'],
    [BUILTIN_VISUAL_OBJECT_TYPES.checkBox, '☑', 'checkBox'],
    [BUILTIN_VISUAL_OBJECT_TYPES.comboBox, '▾', 'comboBox'],
    [BUILTIN_VISUAL_OBJECT_TYPES.slider, '☷', 'slider'],
    [BUILTIN_VISUAL_OBJECT_TYPES.numericInput, '123', 'numericInput'],
    [BUILTIN_VISUAL_OBJECT_TYPES.measurementDial, '◔', 'measurementDial'],
    [BUILTIN_VISUAL_OBJECT_TYPES.measurementRing, '◉', 'measurementRing'],
    [BUILTIN_VISUAL_OBJECT_TYPES.measurementBar, '▰', 'measurementBar'],
    [BUILTIN_VISUAL_OBJECT_TYPES.measurementThermometer, '↕', 'measurementThermometer'],
    [BUILTIN_VISUAL_OBJECT_TYPES.measurementBands, '▥', 'measurementBands'],
    [BUILTIN_VISUAL_OBJECT_TYPES.measurementTarget, '⊙', 'measurementTarget']
  ] as const;

  const embedded = placement === 'arrangement';
  return <div
    className={`visual-editor-authoring-toolbar${embedded ? ' visual-editor-authoring-toolbar--embedded' : ''}`}
    role={embedded ? 'group' : 'toolbar'}
    aria-label={embedded ? [text.align, text.distribute, text.size, text.structure].join(', ') : text.aria}
    data-testid={embedded ? 'visual-editor-arrangement-toolbar' : 'visual-editor-authoring-toolbar'}
  >
    {placement !== 'arrangement' ? <>
    <ToolbarGroup label={text.draw}>
      {inserts.slice(0, 7).map(([objectType, glyph, labelKey]) => <Tool
        key={objectType}
        label={palette[labelKey]}
        disabled={!onInsertObject}
        onClick={() => onInsertObject?.(objectType)}
        dataObjectType={objectType}
      >{glyph}</Tool>)}
    </ToolbarGroup>
    <ToolbarGroup label={text.displayControls}>
      {inserts.slice(7).map(([objectType, glyph, labelKey]) => <Tool
        key={objectType}
        label={palette[labelKey]}
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
    </> : null}

    {placement !== 'authoring' ? <>
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
    </> : null}
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
