import React, { useRef, useState } from 'react';
import { deleteVisualAsset, importVisualAsset, renameVisualAsset, visualAssetContentUrl } from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot, VisualAssetEngineering } from './types';
import './visual-asset-management.css';
import { FactoryArtworkLibrary } from './FactoryArtworkLibrary';
import { listUserVisualAssets } from './visualAssetCatalogModel';

export function VisualAssetManagementWorkspace({
  snapshot,
  locale,
  onApplied
}: {
  snapshot: EngineeringSnapshot;
  locale: EngineeringLocale;
  onApplied: () => Promise<void>;
}) {
  const copy = assetCopy(locale);
  const fileInput = useRef<HTMLInputElement>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const assets = listUserVisualAssets(snapshot.package.visualAssets);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await action();
    } catch (cause) {
      setError(errorMessage(cause));
    } finally {
      setBusy(false);
    }
  }

  function beginRename(asset: VisualAssetEngineering) {
    if (!asset.id) return;
    setEditingId(asset.id);
    setName(asset.name);
    setConfirmDeleteId(null);
    setError(null);
  }

  async function saveName(asset: VisualAssetEngineering) {
    if (!asset.id || !name.trim() || name.trim() === asset.name) return;
    await run(async () => {
      await renameVisualAsset(asset.id!, name.trim(), snapshot.workspace.changeVersion);
      setEditingId(null);
      setNotice(copy.renamed);
      await onApplied();
    });
  }

  async function remove(asset: VisualAssetEngineering) {
    if (!asset.id) return;
    await run(async () => {
      await deleteVisualAsset(asset.id!, snapshot.workspace.changeVersion);
      setConfirmDeleteId(null);
      setNotice(copy.deleted);
      await onApplied();
    });
  }

  async function upload(file: File) {
    await run(async () => {
      await importVisualAsset(file, snapshot.workspace.changeVersion, {
        fileName: file.name,
        name: file.name.replace(/\.[^.]+$/, '')
      });
      setNotice(copy.uploaded);
      await onApplied();
    });
    if (fileInput.current) fileInput.current.value = '';
  }

  return <div className="eng-section visual-asset-management" data-testid="visual-asset-management">
    <header className="eng-section-header">
      <div><span className="eng-eyebrow">{copy.eyebrow}</span><h1>{copy.title}</h1><p>{copy.description}</p></div>
      <div className="eng-section-meta"><strong>{assets.length} {copy.count}</strong></div>
    </header>

    {error && <p className="visual-asset-management__message visual-asset-management__message--error" role="alert">{error}</p>}
    {notice && <p className="visual-asset-management__message" role="status">{notice}</p>}
    <FactoryArtworkLibrary snapshot={snapshot} locale={locale} onApplied={onApplied}/>

    <section className="eng-panel visual-asset-management__panel">
      <div className="visual-asset-management__toolbar">
        <div><h2>{copy.projectAssets}</h2><p>{copy.projectAssetsHint}</p></div>
        <label className="visual-asset-management__upload">
          <span>{busy ? copy.uploading : copy.upload}</span>
          <input ref={fileInput} type="file" accept="image/png,image/jpeg,image/bmp,image/svg+xml,.png,.jpg,.jpeg,.bmp,.svg,.glb,.gltf,model/gltf-binary,model/gltf+json" disabled={busy}
            onChange={event => { const file = event.currentTarget.files?.[0]; if (file) void upload(file); }}/>
        </label>
      </div>

      {assets.length === 0 ? <p className="visual-asset-management__empty">{copy.empty}</p> : (
        <div className="visual-asset-management__grid">
          {assets.map(asset => <AssetCard key={asset.id ?? asset.key} asset={asset} copy={copy}
            editing={editingId === asset.id} name={name} busy={busy} confirmingDelete={confirmDeleteId === asset.id}
            onNameChange={setName} onRename={() => beginRename(asset)} onSave={() => void saveName(asset)}
            onCancel={() => setEditingId(null)} onDelete={() => setConfirmDeleteId(asset.id ?? null)}
            onCancelDelete={() => setConfirmDeleteId(null)} onConfirmDelete={() => void remove(asset)} />)}
        </div>
      )}
    </section>
  </div>;
}

function AssetCard({ asset, copy, editing, name, busy, confirmingDelete, onNameChange, onRename, onSave, onCancel, onDelete, onCancelDelete, onConfirmDelete }: {
  asset: VisualAssetEngineering;
  copy: ReturnType<typeof assetCopy>;
  editing: boolean;
  name: string;
  busy: boolean;
  confirmingDelete: boolean;
  onNameChange: (value: string) => void;
  onRename: () => void;
  onSave: () => void;
  onCancel: () => void;
  onDelete: () => void;
  onCancelDelete: () => void;
  onConfirmDelete: () => void;
}) {
  return <article className="visual-asset-management__card" data-testid="visual-asset-card">
    <div className="visual-asset-management__preview">
      {asset.id && asset.mediaType.startsWith('image/') ? <img src={visualAssetContentUrl(asset.id)} alt="" loading="lazy"/> : asset.mediaType.startsWith('model/') ? <span aria-label="3D model">◈ 3D</span> : <span>{copy.noPreview}</span>}
    </div>
    <div className="visual-asset-management__details">
      {editing ? <label><span>{copy.displayName}</span><input autoFocus maxLength={128} value={name} onChange={event => onNameChange(event.currentTarget.value)}/></label>
        : <strong title={asset.name}>{asset.name}</strong>}
      <small title={asset.originalFileName}>{asset.originalFileName}</small>
      <small>{asset.mediaType} · {formatBytes(asset.byteLength)}{asset.pixelWidth && asset.pixelHeight ? ` · ${asset.pixelWidth}×${asset.pixelHeight}` : ''}</small>
    </div>
    <div className="visual-asset-management__actions">
      {editing ? <><button type="button" onClick={onSave} disabled={busy || !name.trim() || name.trim() === asset.name}>{copy.saveName}</button><button type="button" className="secondary" onClick={onCancel} disabled={busy}>{copy.cancel}</button></>
        : <button type="button" className="secondary" onClick={onRename} disabled={busy}>{copy.rename}</button>}
      {!confirmingDelete ? <button type="button" className="danger" onClick={onDelete} disabled={busy}>{copy.delete}</button>
        : <div className="visual-asset-management__confirm"><span>{copy.confirmDelete}</span><button type="button" className="danger" onClick={onConfirmDelete} disabled={busy}>{copy.confirm}</button><button type="button" className="secondary" onClick={onCancelDelete} disabled={busy}>{copy.cancel}</button></div>}
    </div>
  </article>;
}

function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function errorMessage(error: unknown): string {
  if (!(error instanceof Error)) return String(error);
  try {
    const parsed = JSON.parse(error.message) as { error?: unknown; title?: unknown };
    if (typeof parsed.error === 'string') return parsed.error;
    if (typeof parsed.title === 'string') return parsed.title;
  } catch { /* use the plain response text */ }
  return error.message;
}

function assetCopy(locale: EngineeringLocale) {
  if (locale === 'en') return { eyebrow: 'Project media', title: 'Visual assets', description: 'Manage images used by screen objects, screen backgrounds and branding. Assets in use cannot be deleted.', count: 'assets', projectAssets: 'Project assets', projectAssetsHint: 'System IDs stay hidden; the stable identity is preserved when you rename an asset.', upload: 'Import image', uploading: 'Importing…', empty: 'No image assets yet. Import a PNG, JPG, BMP or SVG to get started.', noPreview: 'Preview unavailable', displayName: 'Display name', rename: 'Rename', saveName: 'Save name', delete: 'Delete', confirmDelete: 'Delete this image?', confirm: 'Confirm', cancel: 'Cancel', renamed: 'Asset renamed.', deleted: 'Asset deleted.', uploaded: 'Image imported into the project.' } as const;
  if (locale === 'es') return { eyebrow: 'Medios del proyecto', title: 'Assets visuales', description: 'Administre imágenes de pantallas, fondos y branding. No se pueden eliminar assets que están en uso.', count: 'assets', projectAssets: 'Assets del proyecto', projectAssetsHint: 'Los IDs del sistema permanecen ocultos; al renombrar, se conserva la identidad estable.', upload: 'Importar imagen', uploading: 'Importando…', empty: 'Aún no hay imágenes. Importe PNG, JPG, BMP o SVG para comenzar.', noPreview: 'Vista previa no disponible', displayName: 'Nombre visible', rename: 'Renombrar', saveName: 'Guardar nombre', delete: 'Eliminar', confirmDelete: '¿Eliminar esta imagen?', confirm: 'Confirmar', cancel: 'Cancelar', renamed: 'Asset renombrado.', deleted: 'Asset eliminado.', uploaded: 'Imagen importada al proyecto.' } as const;
  return { eyebrow: 'Mídia do projeto', title: 'Assets visuais', description: 'Gerencie as imagens usadas por objetos, fundos de telas e branding. Assets em uso não podem ser excluídos.', count: 'assets', projectAssets: 'Assets do projeto', projectAssetsHint: 'IDs de sistema ficam ocultos; ao renomear, a identidade estável é preservada.', upload: 'Importar imagem', uploading: 'Importando…', empty: 'Ainda não há imagens. Importe um PNG, JPG, BMP ou SVG para começar.', noPreview: 'Prévia indisponível', displayName: 'Nome de exibição', rename: 'Renomear', saveName: 'Salvar nome', delete: 'Excluir', confirmDelete: 'Excluir esta imagem?', confirm: 'Confirmar', cancel: 'Cancelar', renamed: 'Asset renomeado.', deleted: 'Asset excluído.', uploaded: 'Imagem importada para o projeto.' } as const;
}
