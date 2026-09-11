import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringBulk,
  deleteEngineeringEntity,
  loadEngineeringWorkspace,
  previewEngineeringBulk,
  type EngineeringBulkPreviewResult,
  type EngineeringBulkRequest
} from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';
import './engineering-mutations.css';

export function DataSourceMutationPanel({ model, locale }: { model: EngineeringPackageView; locale: EngineeringLocale }) {
  const copy = useMemo(() => text(locale), [locale]);
  const entities = useMemo(() => (model.dataSources ?? [])
    .filter(source => Boolean(source.id))
    .map(source => ({ id: source.id!, label: source.key, detail: `${source.name} · ${source.driver}` })), [model]);
  const [deleteId, setDeleteId] = useState(entities[0]?.id ?? '');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());
  const [enabledValue, setEnabledValue] = useState('true');
  const [preview, setPreview] = useState<EngineeringBulkPreviewResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!entities.some(entity => entity.id === deleteId)) setDeleteId(entities[0]?.id ?? '');
    setSelectedIds(current => new Set([...current].filter(id => entities.some(entity => entity.id === id))));
  }, [entities, deleteId]);

  useEffect(() => { setPreview(null); setError(null); }, [selectedIds, enabledValue]);

  const request = (): EngineeringBulkRequest => ({
    entityKind: 'data-source',
    entityIds: [...selectedIds],
    dataSources: { enabled: enabledValue === 'true' }
  });

  const runPreview = async () => {
    if (selectedIds.size === 0) return;
    setBusy(true); setError(null);
    try { setPreview(await previewEngineeringBulk(request())); }
    catch (reason) { setPreview(null); setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };

  const runApply = async () => {
    if (!preview?.preview.canApply || selectedIds.size === 0) return;
    if (!window.confirm(copy.bulkConfirm)) return;
    setBusy(true); setError(null);
    try {
      await applyEngineeringBulk(request(), preview.changeVersion);
      window.location.reload();
    } catch (reason) { setPreview(null); setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };

  const runDelete = async () => {
    const selected = entities.find(entity => entity.id === deleteId);
    if (!selected || !window.confirm(`${copy.deleteConfirm} ${selected.label}?`)) return;
    setBusy(true); setError(null);
    try {
      const workspace = await loadEngineeringWorkspace();
      await deleteEngineeringEntity('data-sources', selected.id, workspace.changeVersion);
      window.location.reload();
    } catch (reason) { setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };

  return <section className="eng-mutation-panel">
    <header className="eng-mutation-header"><div><span>Engineering</span><h2>{copy.title}</h2><p>{copy.description}</p></div></header>
    <div className="eng-mutation-grid">
      <section className="eng-mutation-card">
        <header><strong>{copy.deleteTitle}</strong><span>{copy.deleteHint}</span></header>
        <label className="eng-mutation-field"><span>{copy.entity}</span>
          <select value={deleteId} onChange={event => setDeleteId(event.target.value)} disabled={busy || entities.length === 0}>
            {entities.map(entity => <option key={entity.id} value={entity.id}>{entity.label}</option>)}
          </select>
        </label>
        <button type="button" className="danger" disabled={!deleteId || busy} onClick={() => void runDelete()} data-testid="engineering-delete">{copy.deleteAction}</button>
      </section>

      <section className="eng-mutation-card eng-bulk-card">
        <header><strong>{copy.bulkTitle}</strong><span>{copy.bulkHint}</span></header>
        <div className="eng-bulk-entities">
          {entities.map(entity => <label key={entity.id}>
            <input type="checkbox" checked={selectedIds.has(entity.id)} disabled={busy} onChange={() => setSelectedIds(current => {
              const next = new Set(current); if (next.has(entity.id)) next.delete(entity.id); else next.add(entity.id); return next;
            })}/>
            <span><strong>{entity.label}</strong><code>{entity.detail}</code></span>
          </label>)}
        </div>
        <label className="eng-mutation-field"><span>{copy.enabled}</span>
          <select value={enabledValue} onChange={event => setEnabledValue(event.target.value)} disabled={busy}>
            <option value="true">{copy.trueValue}</option><option value="false">{copy.falseValue}</option>
          </select>
        </label>
        <div className="eng-mutation-actions">
          <button type="button" disabled={selectedIds.size === 0 || busy} onClick={() => void runPreview()} data-testid="engineering-bulk-preview">{copy.preview}</button>
          <button type="button" className="primary" disabled={!preview?.preview.canApply || busy} onClick={() => void runApply()} data-testid="engineering-bulk-apply">{copy.apply}</button>
        </div>
        {preview && <div className={preview.preview.canApply ? 'eng-bulk-preview valid' : 'eng-bulk-preview invalid'}>
          <strong>{preview.preview.canApply ? copy.valid : copy.invalid}</strong><span>{copy.affected}: {preview.affectedCount}</span><span>{copy.errors}: {preview.preview.errorCount}</span>
        </div>}
      </section>
    </div>
    {error && <pre className="eng-mutation-error">{error}</pre>}
  </section>;
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Delete and bulk edit', description: 'Explicit destructive and homogeneous Data Source mutations.',
    deleteTitle: 'Explicit Delete', deleteHint: 'Dependencies are checked by the server.', entity: 'Entity', deleteAction: 'Delete entity', deleteConfirm: 'Delete explicitly',
    bulkTitle: 'Safe bulk edit', bulkHint: 'Change Enabled for selected persisted Data Sources.', enabled: 'Enabled', trueValue: 'True', falseValue: 'False',
    preview: 'Preview bulk change', apply: 'Apply bulk change', bulkConfirm: 'Apply this bulk change to the official Engineering Workspace?', valid: 'Valid candidate', invalid: 'Invalid candidate', affected: 'Affected', errors: 'Errors'
  };
  if (locale === 'es') return {
    title: 'Eliminar y edición por lote', description: 'Mutaciones destructivas y homogéneas explícitas de Data Source.',
    deleteTitle: 'Eliminación explícita', deleteHint: 'El servidor verifica dependencias.', entity: 'Entidad', deleteAction: 'Eliminar entidad', deleteConfirm: 'Eliminar explícitamente',
    bulkTitle: 'Edición segura por lote', bulkHint: 'Cambie Habilitado para las Data Sources seleccionadas.', enabled: 'Habilitado', trueValue: 'Verdadero', falseValue: 'Falso',
    preview: 'Preview del lote', apply: 'Aplicar lote', bulkConfirm: '¿Aplicar este cambio al Engineering Workspace oficial?', valid: 'Candidato válido', invalid: 'Candidato inválido', affected: 'Afectados', errors: 'Errores'
  };
  return {
    title: 'Excluir e editar em lote', description: 'Mutações destrutivas e homogêneas explícitas de Data Source.',
    deleteTitle: 'Delete explícito', deleteHint: 'O servidor verifica dependências.', entity: 'Entidade', deleteAction: 'Excluir entidade', deleteConfirm: 'Excluir explicitamente',
    bulkTitle: 'Edição segura em lote', bulkHint: 'Altere Habilitado nas Data Sources persistidas selecionadas.', enabled: 'Habilitado', trueValue: 'Verdadeiro', falseValue: 'Falso',
    preview: 'Validar lote', apply: 'Aplicar lote', bulkConfirm: 'Aplicar esta alteração ao Engineering Workspace oficial?', valid: 'Candidato válido', invalid: 'Candidato inválido', affected: 'Afetadas', errors: 'Erros'
  };
}
