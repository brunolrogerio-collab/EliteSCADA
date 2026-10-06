import React, { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { EngineeringLocale } from './i18n';
import './engineering-resource-organizer.css';

export type EngineeringOrganizedResource<T> = Readonly<{
  identity: string;
  name: string;
  details?: string;
  accessibilityLabel?: string;
  value: T;
}>;

type Folder = { id: string; name: string; collapsed: boolean };
type Organization = { version: 1; folders: Folder[]; assignments: Record<string, string> };
type Clipboard<T> = { version: 1; projectKey: string; kind: string; resource: T };
type Menu = { x: number; y: number; target: 'list' | 'folder' | 'resource'; folderId?: string; resource?: unknown; identity?: string };
type FolderEdit = { action: 'create' | 'rename' | 'delete'; folder?: Folder; name: string };

const FOLDER_MIME = 'application/x-elitescada-engineering-resource';

export function EngineeringResourceOrganizer<T>({
  projectKey, kind, locale, label, resources, selectedIdentity, selectedIdentities, onSelect, onPaste, onCopySelection, clipboardActions, emptyLabel
}: {
  projectKey: string;
  kind: 'screens' | 'templates' | 'popups' | 'tags' | 'dataSources' | 'mediaSources' | 'alarms' | 'operationalEvents' | 'gatewayRoutes';
  locale: EngineeringLocale;
  label: string;
  resources: readonly EngineeringOrganizedResource<T>[];
  selectedIdentity: string | null;
  selectedIdentities?: ReadonlySet<string>;
  onSelect: (identity: string) => void;
  onPaste: (resource: T) => void;
  onCopySelection?: () => void;
  clipboardActions?: { canPaste: boolean; copyResource: (resource: T) => void; copySelection: () => void; paste: () => void; duplicateSelection?: () => void };
  emptyLabel?: string;
}) {
  const text = useMemo(() => organizerText(locale), [locale]);
  const scope = `${projectKey}:${kind}`;
  const storageKey = `elitescada.engineering.organization.v1:${scope}`;
  const clipboardKey = `elitescada.engineering.clipboard.v1:${scope}`;
  const [organization, setOrganization] = useState<Organization>(() => readOrganization(storageKey));
  const [menu, setMenu] = useState<Menu | null>(null);
  const [clipboard, setClipboard] = useState<Clipboard<T> | null>(() => readClipboard(clipboardKey));
  const [folderEdit, setFolderEdit] = useState<FolderEdit | null>(null);
  const [overlayStyle, setOverlayStyle] = useState<React.CSSProperties>({});
  const rootRef = useRef<HTMLDivElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    setOrganization(readOrganization(storageKey));
    setClipboard(readClipboard(clipboardKey));
    setMenu(null);
    setFolderEdit(null);
  }, [storageKey, clipboardKey]);

  useLayoutEffect(() => {
    if (!menu || !menuRef.current) return;
    const box = menuRef.current.getBoundingClientRect();
    const x = Math.max(4, Math.min(menu.x, window.innerWidth - box.width - 4));
    const y = Math.max(4, Math.min(menu.y, window.innerHeight - box.height - 4));
    if (x !== menu.x || y !== menu.y) setMenu({ ...menu, x, y });
  }, [menu]);

  useLayoutEffect(() => {
    if (folderEdit && dialogRef.current && !dialogRef.current.open) dialogRef.current.showModal();
  }, [folderEdit]);

  useEffect(() => {
    if (!menu) return;
    const close = (event: Event) => {
      if (event.target instanceof Element && event.target.closest('.engineering-resource-organizer__menu')) return;
      setMenu(null);
    };
    const keydown = (event: KeyboardEvent) => { if (event.key === 'Escape') setMenu(null); };
    window.addEventListener('click', close);
    window.addEventListener('blur', close);
    window.addEventListener('keydown', keydown);
    return () => {
      window.removeEventListener('click', close);
      window.removeEventListener('blur', close);
      window.removeEventListener('keydown', keydown);
    };
  }, [menu]);

  const commit = (next: Organization) => {
    setOrganization(next);
    try { window.localStorage.setItem(storageKey, JSON.stringify(next)); } catch { /* Browser storage can be disabled. */ }
  };

  const writeClipboard = (resource: T) => {
    if (clipboardActions) { clipboardActions.copyResource(resource); return; }
    const next: Clipboard<T> = { version: 1, projectKey, kind, resource };
    setClipboard(next);
    try { window.sessionStorage.setItem(clipboardKey, JSON.stringify(next)); } catch { /* Browser storage can be disabled. */ }
  };

  const openMenu = (event: React.MouseEvent, target: Menu['target'], values: Partial<Menu> = {}) => {
    event.preventDefault();
    event.stopPropagation();
    if (rootRef.current) {
      const computed = getComputedStyle(rootRef.current);
      const tokens: Record<string, string> = { font: computed.font };
      for (const name of ['text', 'panel', 'panel-soft', 'input-bg', 'border', 'hover', 'selected', 'muted', 'accent', 'focus']) {
        const value = computed.getPropertyValue(`--eng-${name}`).trim();
        if (value) tokens[`--eng-${name}`] = value;
      }
      setOverlayStyle(tokens as React.CSSProperties);
    }
    setMenu({ x: event.clientX, y: event.clientY, target, ...values });
  };

  const createFolder = () => {
    setFolderEdit({ action: 'create', name: text.defaultFolder });
    setMenu(null);
  };

  const renameFolder = (folder: Folder) => {
    setFolderEdit({ action: 'rename', folder, name: folder.name });
    setMenu(null);
  };

  const deleteFolder = (folder: Folder) => {
    setFolderEdit({ action: 'delete', folder, name: folder.name });
    setMenu(null);
  };

  const saveFolder = (event: React.FormEvent) => {
    event.preventDefault();
    if (!folderEdit) return;
    const name = folderEdit.name.trim();
    if (folderEdit.action === 'delete' && folderEdit.folder) {
      const id = folderEdit.folder.id;
      const assignments = Object.fromEntries(Object.entries(organization.assignments).filter(([, folderId]) => folderId !== id));
      commit({ ...organization, folders: organization.folders.filter(item => item.id !== id), assignments });
    } else if (name && folderEdit.action === 'create') {
      commit({ ...organization, folders: [...organization.folders, { id: randomFolderId(), name, collapsed: false }] });
    } else if (name && folderEdit.folder) {
      commit({ ...organization, folders: organization.folders.map(item => item.id === folderEdit.folder!.id ? { ...item, name } : item) });
    } else return;
    setFolderEdit(null);
    rootRef.current?.focus();
  };

  const moveToFolder = (identity: string, folderId?: string) => {
    if (!resources.some(resource => resource.identity === identity)) return;
    const assignments = { ...organization.assignments };
    if (folderId) assignments[identity] = folderId;
    else delete assignments[identity];
    commit({ ...organization, assignments });
  };

  const dropResource = (event: React.DragEvent, folderId?: string) => {
    const raw = event.dataTransfer.getData(FOLDER_MIME);
    if (!raw) return;
    event.preventDefault();
    event.stopPropagation();
    try {
      const item = JSON.parse(raw);
      if (item.scope === scope && typeof item.identity === 'string') moveToFolder(item.identity, folderId);
    } catch { /* Ignore unrelated or obsolete drag payloads. */ }
  };

  const pasteClipboard = () => {
    if (clipboardActions?.canPaste) clipboardActions.paste();
    else if (!clipboardActions && clipboard?.projectKey === projectKey && clipboard.kind === kind) onPaste(clipboard.resource);
    setMenu(null);
  };
  const canPaste = clipboardActions ? clipboardActions.canPaste : clipboard?.projectKey === projectKey && clipboard.kind === kind;

  const onListKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (!(event.ctrlKey || event.metaKey)) return;
    if (event.target instanceof HTMLElement && (event.target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(event.target.tagName))) return;
    if (event.key.toLowerCase() === 'c' && selectedIdentity) {
      const selected = resources.find(resource => resource.identity === selectedIdentity);
      if (!selected) return;
      event.preventDefault();
      writeClipboard(selected.value);
      event.stopPropagation();
    } else if (event.key.toLowerCase() === 'c' && selectedIdentities?.size && (clipboardActions || onCopySelection)) {
      event.preventDefault();
      if (clipboardActions) clipboardActions.copySelection();
      else onCopySelection?.();
      event.stopPropagation();
    } else if (event.key.toLowerCase() === 'v' && canPaste) {
      event.preventDefault();
      pasteClipboard();
      event.stopPropagation();
    } else if (event.key.toLowerCase() === 'd' && clipboardActions?.duplicateSelection) {
      event.preventDefault();
      event.stopPropagation();
      clipboardActions.duplicateSelection();
    }
  };

  const liveResources = resources.filter(resource => resource.identity.length > 0);
  const assigned = (identity: string) => organization.assignments[identity];
  const renderResource = (resource: EngineeringOrganizedResource<T>) => (
    <button
      type="button"
      className={`engineering-resource-organizer__resource${selectedIdentity === resource.identity ? ' is-selected' : ''}${selectedIdentities?.has(resource.identity) ? ' is-multi-selected' : ''}`}
      key={resource.identity}
      aria-label={resource.accessibilityLabel}
      aria-current={selectedIdentity === resource.identity ? 'true' : undefined}
      aria-pressed={selectedIdentities ? selectedIdentities.has(resource.identity) : undefined}
      draggable
      onClick={() => onSelect(resource.identity)}
      onContextMenu={event => openMenu(event, 'resource', { resource: resource.value, identity: resource.identity })}
      onDragStart={event => {
        setMenu(null);
        event.dataTransfer.setData(FOLDER_MIME, JSON.stringify({ scope, identity: resource.identity }));
        event.dataTransfer.effectAllowed = 'move';
      }}
    >
      {selectedIdentities ? <i className="engineering-resource-organizer__check" aria-hidden="true">{selectedIdentities.has(resource.identity) ? '✓' : ''}</i> : null}
      <strong>{resource.name}</strong>
      {resource.details ? <small>{resource.details}</small> : null}
    </button>
  );

  return (
    <div
      ref={rootRef}
      className={`engineering-resource-organizer${['screens', 'popups', 'templates'].includes(kind) ? ' visual-editor-screen-list' : ''}`}
      aria-label={label}
      data-testid={`engineering-resource-organizer-${kind}`}
      tabIndex={0}
      onKeyDown={onListKeyDown}
      onContextMenu={event => {
        const target = event.target;
        if (!(target instanceof Element) || (!target.closest('.engineering-resource-organizer__resource') && !target.closest('.engineering-resource-organizer__folder'))) openMenu(event, 'list');
      }}
      onDragOver={event => { if (event.dataTransfer.types.includes(FOLDER_MIME)) event.preventDefault(); }}
      onDrop={event => dropResource(event)}
    >
      {organization.folders.map(folder => {
        const members = liveResources.filter(resource => assigned(resource.identity) === folder.id);
        return (
          <section
            className="engineering-resource-organizer__folder"
            key={folder.id}
            onContextMenu={event => {
              const target = event.target;
              if (target instanceof Element && target.closest('.engineering-resource-organizer__resource')) return;
              openMenu(event, 'folder', { folderId: folder.id });
            }}
            onDragOver={event => { if (event.dataTransfer.types.includes(FOLDER_MIME)) { event.preventDefault(); event.dataTransfer.dropEffect = 'move'; } }}
            onDrop={event => dropResource(event, folder.id)}
          >
            <header>
              <button
                type="button"
                className="engineering-resource-organizer__folder-toggle"
                aria-expanded={!folder.collapsed}
                aria-label={`${folder.collapsed ? text.expand : text.collapse}: ${folder.name}`}
                onClick={() => commit({ ...organization, folders: organization.folders.map(item => item.id === folder.id ? { ...item, collapsed: !item.collapsed } : item) })}
              ><span aria-hidden="true">{folder.collapsed ? '▸' : '▾'}</span><strong>{folder.name}</strong><small>{members.length}</small></button>
            </header>
            {!folder.collapsed ? <div className="engineering-resource-organizer__items">{members.map(renderResource)}</div> : null}
          </section>
        );
      })}
      <div className="engineering-resource-organizer__items engineering-resource-organizer__items--root">
        {liveResources.filter(resource => !organization.folders.some(folder => folder.id === assigned(resource.identity))).map(renderResource)}
        {liveResources.length === 0 ? <span className="engineering-resource-organizer__empty">{emptyLabel ?? text.empty}</span> : null}
      </div>

      {menu ? createPortal(
        <div
          ref={menuRef}
          className="engineering-resource-organizer__menu"
          role="menu"
          style={{ ...overlayStyle, left: menu.x, top: menu.y }}
          onClick={event => event.stopPropagation()}
        >
          <button type="button" role="menuitem" onClick={createFolder}>{text.createFolder}</button>
          {menu.target === 'folder' ? (() => {
            const folder = organization.folders.find(item => item.id === menu.folderId);
            return folder ? <>
              <button type="button" role="menuitem" onClick={() => renameFolder(folder)}>{text.renameFolder}</button>
              <button type="button" role="menuitem" onClick={() => deleteFolder(folder)}>{text.deleteFolder}</button>
            </> : null;
          })() : null}
          {menu.target === 'resource' && menu.resource !== undefined ? (
            <button type="button" role="menuitem" onClick={() => {
              if (clipboardActions && menu.identity && selectedIdentities?.has(menu.identity)) clipboardActions.copySelection();
              else writeClipboard(menu.resource as T);
              setMenu(null);
            }}>{text.copy}</button>
          ) : null}
          {canPaste ? (
            <button type="button" role="menuitem" onClick={pasteClipboard}>{text.paste}</button>
          ) : null}
        </div>, document.body
      ) : null}
      {folderEdit ? createPortal(
        <dialog ref={dialogRef} className="engineering-resource-organizer__dialog" style={overlayStyle}
          aria-label={folderEdit.action === 'create' ? text.createFolder : folderEdit.action === 'rename' ? text.renameFolder : text.deleteFolder}
          onCancel={() => setFolderEdit(null)} onClose={() => setFolderEdit(null)}>
          <form onSubmit={saveFolder}>
            <strong>{folderEdit.action === 'create' ? text.createFolder : folderEdit.action === 'rename' ? text.renameFolder : text.deleteFolder}</strong>
            {folderEdit.action === 'delete' ? <p>{text.deleteFolderConfirm.replace('{name}', folderEdit.name)}</p> : <label>
              {folderEdit.action === 'create' ? text.createFolderPrompt : text.renameFolderPrompt}
              <input autoFocus required maxLength={128} value={folderEdit.name} onFocus={event => event.currentTarget.select()}
                onChange={event => setFolderEdit({ ...folderEdit, name: event.currentTarget.value })} />
            </label>}
            <footer>
              <button type="button" onClick={() => setFolderEdit(null)}>{text.cancel}</button>
              <button type="submit" disabled={folderEdit.action !== 'delete' && !folderEdit.name.trim()}>{folderEdit.action === 'delete' ? text.deleteFolder : text.save}</button>
            </footer>
          </form>
        </dialog>, document.body
      ) : null}
    </div>
  );
}

function readOrganization(key: string): Organization {
  try {
    const value = JSON.parse(window.localStorage.getItem(key) ?? 'null') as Partial<Organization> | null;
    if (value?.version === 1 && Array.isArray(value.folders) && value.assignments && typeof value.assignments === 'object') {
      return { version: 1, folders: value.folders.filter(folder => folder && typeof folder.id === 'string' && typeof folder.name === 'string').map(folder => ({ id: folder.id, name: folder.name, collapsed: folder.collapsed === true })), assignments: value.assignments };
    }
  } catch { /* Start with an empty list when storage is unavailable or malformed. */ }
  return { version: 1, folders: [], assignments: {} };
}

function readClipboard<T>(key: string): Clipboard<T> | null {
  try {
    const value = JSON.parse(window.sessionStorage.getItem(key) ?? 'null') as Clipboard<T> | null;
    return value?.version === 1 && typeof value.kind === 'string' && value.resource !== undefined ? value : null;
  } catch { return null; }
}

function randomFolderId() {
  return globalThis.crypto?.randomUUID?.() ?? `folder-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export function uniqueEngineeringKey(base: string, existing: readonly string[]) {
  const used = new Set(existing.map(value => value.trim().toLocaleLowerCase('en-US')));
  const stem = `${base.trim() || 'item'}-copy`;
  let candidate = stem;
  let index = 2;
  while (used.has(candidate.toLocaleLowerCase('en-US'))) candidate = `${stem}-${index++}`;
  return candidate;
}

export function engineeringCopyName(name: string, locale: EngineeringLocale) {
  const suffix = locale === 'en' ? 'copy' : locale === 'es' ? 'copia' : 'cópia';
  return `${name} (${suffix})`;
}

function organizerText(locale: EngineeringLocale) {
  if (locale === 'en') return { save: 'Save', cancel: 'Cancel', createFolder: 'New folder', createFolderPrompt: 'Folder name', defaultFolder: 'New folder', renameFolder: 'Rename folder', renameFolderPrompt: 'New folder name', deleteFolder: 'Delete folder', deleteFolderConfirm: 'Delete folder "{name}"? Its resources will return to the list.', copy: 'Copy', paste: 'Paste', collapse: 'Collapse folder', expand: 'Expand folder', empty: 'No items' };
  if (locale === 'es') return { save: 'Guardar', cancel: 'Cancelar', createFolder: 'Nueva carpeta', createFolderPrompt: 'Nombre de la carpeta', defaultFolder: 'Nueva carpeta', renameFolder: 'Renombrar carpeta', renameFolderPrompt: 'Nuevo nombre de carpeta', deleteFolder: 'Eliminar carpeta', deleteFolderConfirm: '¿Eliminar la carpeta "{name}"? Sus elementos volverán a la lista.', copy: 'Copiar', paste: 'Pegar', collapse: 'Contraer carpeta', expand: 'Expandir carpeta', empty: 'No hay elementos' };
  return { save: 'Salvar', cancel: 'Cancelar', createFolder: 'Nova pasta', createFolderPrompt: 'Nome da pasta', defaultFolder: 'Nova pasta', renameFolder: 'Renomear pasta', renameFolderPrompt: 'Novo nome da pasta', deleteFolder: 'Excluir pasta', deleteFolderConfirm: 'Excluir a pasta "{name}"? Os itens voltarão para a lista.', copy: 'Copiar', paste: 'Colar', collapse: 'Recolher pasta', expand: 'Expandir pasta', empty: 'Nenhum item' };
}
