import React from 'react';
import type { EngineeringLocale } from '../../i18n';
import type { ScreenEngineering } from '../../types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../../visual-runtime';
import { isVisualElementEffectivelyAuthoringLocked } from '../visualEditorAuthoringModel';
import type { VisualEditorMutationIntent } from '../visualEditorContracts';
import type { VisualEditorKeyboardCommand } from '../visualEditorKeyboardModel';
import { buildVisualEditorAuthoringToolbarState } from './visualEditorAuthoringToolbarModel';

export function VisualEditorContextMenu({
  screen,
  objectId,
  selectedObjectIds,
  x,
  y,
  canPaste,
  locale,
  onMutationIntent,
  onKeyboardCommand,
  onInspectorTabRequest,
  onStructureRequest,
  onClose
}: {
  screen: ScreenEngineering;
  objectId: string;
  selectedObjectIds: readonly string[];
  x: number;
  y: number;
  canPaste: boolean;
  locale: EngineeringLocale;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  onKeyboardCommand?: (command: VisualEditorKeyboardCommand) => void;
  onInspectorTabRequest?: (tab: 'properties' | 'dynamics' | 'events', focusRename?: boolean) => void;
  onStructureRequest?: () => void;
  onClose: () => void;
}) {
  const selection = selectedObjectIds.includes(objectId) ? selectedObjectIds : [objectId];
  const state = buildVisualEditorAuthoringToolbarState(screen, selection);
  const locked = selection.some(id => isVisualElementEffectivelyAuthoringLocked(screen, id));
  const text = menuText(locale);
  const mutate = (intent: VisualEditorMutationIntent) => { onClose(); onMutationIntent(intent); };
  const command = (value: VisualEditorKeyboardCommand) => { onClose(); onKeyboardCommand?.(value); };
  const inspect = (tab: 'properties' | 'dynamics' | 'events', rename = false) => {
    onClose();
    onInspectorTabRequest?.(tab, rename);
  };

  return <div
    className="visual-editor-context-menu"
    role="menu"
    data-testid="visual-editor-context-menu"
    style={{ left: x, top: y }}
    onContextMenu={event => event.preventDefault()}
  >
    <MenuGroup>
      <MenuItem disabled={selection.length !== 1} onClick={() => inspect('properties', true)}>{text.rename}</MenuItem>
      {isGroupObject(screen, objectId) ? <MenuItem onClick={() => { onClose(); onStructureRequest?.(); }}>{text.editGroup}</MenuItem> : null}
      <MenuItem onClick={() => inspect('properties')}>{text.properties}</MenuItem>
      <MenuItem disabled={selection.length !== 1} onClick={() => inspect('dynamics')}>{text.dynamics}</MenuItem>
      <MenuItem disabled={selection.length !== 1} onClick={() => inspect('events')}>{text.events}</MenuItem>
    </MenuGroup>
    <MenuGroup>
      <MenuItem disabled={!onKeyboardCommand || !state.sameParent} onClick={() => command({ kind: 'copy' })}>{text.copy}</MenuItem>
      <MenuItem disabled={!onKeyboardCommand || !canPaste} onClick={() => command({ kind: 'paste' })}>{text.paste}</MenuItem>
      <MenuItem disabled={!state.sameParent || locked} onClick={() => mutate({ kind: 'object.duplicate', objectIds: selection })}>{text.duplicate}</MenuItem>
      <MenuItem disabled={!state.sameParent || locked} onClick={() => mutate({ kind: 'object.delete', objectIds: selection })}>{text.delete}</MenuItem>
    </MenuGroup>
    <MenuGroup>
      <MenuItem disabled={!state.sameParent || locked} onClick={() => mutate({ kind: 'object.zOrder', objectIds: selection, operation: 'bringToFront' })}>{text.front}</MenuItem>
      <MenuItem disabled={!state.sameParent || locked} onClick={() => mutate({ kind: 'object.zOrder', objectIds: selection, operation: 'sendToBack' })}>{text.back}</MenuItem>
      <MenuItem disabled={!onKeyboardCommand || !state.canAlign || locked} onClick={() => command({ kind: 'align', operation: 'left' })}>{text.alignLeft}</MenuItem>
      <MenuItem disabled={!onKeyboardCommand || !state.canAlign || locked} onClick={() => command({ kind: 'align', operation: 'top' })}>{text.alignTop}</MenuItem>
      <MenuItem disabled={!onKeyboardCommand || !state.canDistribute || locked} onClick={() => command({ kind: 'distribute', operation: 'horizontalSpacing' })}>{text.distributeH}</MenuItem>
      <MenuItem disabled={!onKeyboardCommand || !state.canDistribute || locked} onClick={() => command({ kind: 'distribute', operation: 'verticalSpacing' })}>{text.distributeV}</MenuItem>
    </MenuGroup>
  </div>;
}

function MenuGroup({ children }: { children: React.ReactNode }) {
  return <div className="visual-editor-context-menu__group">{children}</div>;
}

function MenuItem({ children, disabled = false, onClick }: {
  children: React.ReactNode;
  disabled?: boolean;
  onClick: () => void;
}) {
  return <button type="button" role="menuitem" disabled={disabled} onClick={onClick}>{children}</button>;
}

function isGroupObject(screen: ScreenEngineering, objectId: string): boolean {
  const visit = (elements: readonly NonNullable<ScreenEngineering['elements']>[number][]): boolean => {
    for (const element of elements) {
      if (element.id === objectId) return element.type === BUILTIN_VISUAL_OBJECT_TYPES.group;
      if (element.children && visit(element.children)) return true;
    }
    return false;
  };
  return visit(screen.elements ?? []);
}

function menuText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    rename: 'Rename', editGroup: 'Edit group contents', properties: 'Properties', dynamics: 'Dynamics', events: 'Events',
    copy: 'Copy', paste: 'Paste', duplicate: 'Duplicate', delete: 'Delete',
    front: 'Bring to front', back: 'Send to back', alignLeft: 'Align left', alignTop: 'Align top',
    distributeH: 'Distribute horizontally', distributeV: 'Distribute vertically'
  };
  if (locale === 'es') return {
    rename: 'Renombrar', editGroup: 'Editar contenido del grupo', properties: 'Propiedades', dynamics: 'Dinámicas', events: 'Eventos',
    copy: 'Copiar', paste: 'Pegar', duplicate: 'Duplicar', delete: 'Eliminar',
    front: 'Traer al frente', back: 'Enviar al fondo', alignLeft: 'Alinear a la izquierda', alignTop: 'Alinear arriba',
    distributeH: 'Distribuir horizontalmente', distributeV: 'Distribuir verticalmente'
  };
  return {
    rename: 'Renomear', editGroup: 'Editar conteúdo do grupo', properties: 'Propriedades', dynamics: 'Dinâmicas', events: 'Eventos',
    copy: 'Copiar', paste: 'Colar', duplicate: 'Duplicar', delete: 'Excluir',
    front: 'Trazer para frente', back: 'Enviar para trás', alignLeft: 'Alinhar à esquerda', alignTop: 'Alinhar ao topo',
    distributeH: 'Distribuir horizontalmente', distributeV: 'Distribuir verticalmente'
  };
}
