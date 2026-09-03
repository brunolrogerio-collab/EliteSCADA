import React, { useMemo, useState } from 'react';
import type { ScreenEngineering } from '../../types';
import type { VisualEditorSelectionMode } from '../visualEditorContracts';
import { selectionModeFromModifiers } from './canvasInteractionModel';
import {
  buildVisualEditorOutliner,
  countVisualEditorOutlinerNodes,
  type VisualEditorOutlinerNode
} from './visualEditorOutlinerModel';
import './VisualEditorOutliner.css';

export function VisualEditorOutliner({
  screen,
  selectedObjectIds,
  onSelection
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  onSelection: (objectId: string, mode: VisualEditorSelectionMode) => void;
}) {
  const nodes = useMemo(() => buildVisualEditorOutliner(screen), [screen]);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(() => new Set<string>());
  const selected = useMemo(() => new Set(selectedObjectIds), [selectedObjectIds]);

  const toggle = (objectId: string) => setCollapsed(current => {
    const next = new Set(current);
    if (next.has(objectId)) next.delete(objectId);
    else next.add(objectId);
    return next;
  });

  return <details className="visual-editor-outliner" open data-testid="visual-editor-outliner">
    <summary><strong>Outliner</strong><span>{countVisualEditorOutlinerNodes(nodes)}</span></summary>
    <div className="visual-editor-outliner__tree" role="tree" aria-label="Visual object hierarchy">
      {nodes.map(node => <OutlinerNode
        key={node.objectId}
        node={node}
        depth={0}
        selected={selected}
        collapsed={collapsed}
        onToggle={toggle}
        onSelection={onSelection}
      />)}
      {nodes.length === 0 ? <small className="visual-editor-outliner__empty">∅</small> : null}
    </div>
  </details>;
}

function OutlinerNode({
  node,
  depth,
  selected,
  collapsed,
  onToggle,
  onSelection
}: {
  node: VisualEditorOutlinerNode;
  depth: number;
  selected: ReadonlySet<string>;
  collapsed: ReadonlySet<string>;
  onToggle: (objectId: string) => void;
  onSelection: (objectId: string, mode: VisualEditorSelectionMode) => void;
}) {
  const hasChildren = node.children.length > 0;
  const isCollapsed = collapsed.has(node.objectId);
  return <div role="treeitem" aria-expanded={hasChildren ? !isCollapsed : undefined} aria-selected={selected.has(node.objectId)}>
    <div className={`visual-editor-outliner__row${selected.has(node.objectId) ? ' is-selected' : ''}`} style={{ paddingLeft: 6 + depth * 14 }}>
      {hasChildren ? <button
        type="button"
        className="visual-editor-outliner__toggle"
        aria-label={isCollapsed ? 'Expand' : 'Collapse'}
        onClick={() => onToggle(node.objectId)}
      >{isCollapsed ? '›' : '⌄'}</button> : <span className="visual-editor-outliner__spacer" />}
      <button
        type="button"
        className="visual-editor-outliner__select"
        title={`${node.key} · ${node.type}`}
        onClick={event => onSelection(node.objectId, selectionModeFromModifiers(event))}
      >
        <span className="visual-editor-outliner__name">{node.key}</span>
        <code>{node.dynamoKey ? 'DYN' : shortType(node.type)}</code>
        {node.effectiveLocked ? <span className={`visual-editor-outliner__lock${node.directLocked ? ' is-direct' : ''}`} title={node.directLocked ? 'Locked' : 'Locked by parent'}>L</span> : null}
      </button>
    </div>
    {hasChildren && !isCollapsed ? <div role="group">
      {node.children.map(child => <OutlinerNode
        key={child.objectId}
        node={child}
        depth={depth + 1}
        selected={selected}
        collapsed={collapsed}
        onToggle={onToggle}
        onSelection={onSelection}
      />)}
    </div> : null}
  </div>;
}

function shortType(type: string): string {
  const value = type.split('.').at(-1) ?? type;
  return value.slice(0, 4).toUpperCase();
}
