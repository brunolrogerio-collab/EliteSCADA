import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { VisualAssetEngineering } from '../types';
import { staticArtworkContentUrl, useStaticArtwork, type StaticArtwork } from '../staticArtworkApi';
import { libraryCatalogCategoryPathLabel, libraryCatalogCategoryMatches, listLibraryCatalogCategories } from '../libraryCatalogTaxonomy';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../visual-runtime';
import './static-artwork-palette.css';

export function StaticArtworkPalette({ locale, enabled, disabled, onImport, onMutationIntent }: {
  locale: EngineeringLocale; enabled: boolean; disabled: boolean;
  onImport: (entry: StaticArtwork) => Promise<VisualAssetEngineering | null>;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}) {
  const { entries, loading, error } = useStaticArtwork(enabled);
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const [page, setPage] = useState(0);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState('');
  const text = (pt: string, en: string, es: string) => locale === 'en' ? en : locale === 'es' ? es : pt;
  const categories = useMemo(() => listLibraryCatalogCategories().filter(item => entries.some(entry => libraryCatalogCategoryMatches(entry.category, item.id))), [entries]);
  const results = useMemo(() => entries.filter(entry => libraryCatalogCategoryMatches(entry.category, category) &&
    `${entry.name} ${entry.key} ${entry.style} ${entry.tags.join(' ')} ${libraryCatalogCategoryPathLabel(entry.category, locale)}`.toLocaleLowerCase().includes(query.toLocaleLowerCase())), [entries, category, query, locale]);
  const insert = async (entry: StaticArtwork) => {
    setBusy(true); setFailure('');
    try {
      const asset = await onImport(entry);
      if (!asset?.id) return;
      const box = (asset.metadata?.['elitescada.svg.viewBox'] ?? '').split(/[ ,]+/).map(Number);
      const ratio = box.length === 4 && box[2] > 0 && box[3] > 0 ? box[3] / box[2] : 1;
      onMutationIntent({ kind: 'object.add', objectType: BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol, initialProperties: {
        [VISUAL_PROPERTY_KEYS.assetRef]: { assetId: asset.id }, [VISUAL_PROPERTY_KEYS.width]: 160,
        [VISUAL_PROPERTY_KEYS.height]: Math.max(20, Math.min(600, 160 * ratio))
      } });
    } catch (reason) { setFailure(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(false); }
  };
  return <section className="static-artwork-palette" data-testid="static-artwork-palette">
    <header><strong>{text('Biblioteca de SVGs estáticos', 'Static SVG library', 'Biblioteca SVG estática')}</strong><span>{entries.length} SVG</span></header>
    <input type="search" aria-label={text('Buscar SVG na biblioteca', 'Search library SVG', 'Buscar SVG en biblioteca')} value={query} onChange={event => { setQuery(event.target.value); setPage(0); }}/>
    <select aria-label={text('Categoria dos SVGs', 'SVG category', 'Categoría SVG')} value={category} onChange={event => { setCategory(event.target.value); setPage(0); }}>
      <option value="">{text('Todas as categorias', 'All categories', 'Todas las categorías')}</option>
      {categories.map(item => <option key={item.id} value={item.id}>{libraryCatalogCategoryPathLabel(item.id, locale)}</option>)}
    </select>
    {(failure || error) && <p role="alert">{failure || error}</p>}
    {loading && <p>{text('Carregando SVGs…', 'Loading SVGs…', 'Cargando SVG…')}</p>}
    <div className="static-artwork-palette__grid">
      {results.slice(page * 24, page * 24 + 24).map(entry => <button key={entry.id} type="button" disabled={disabled || busy}
        data-testid="static-artwork-insert" data-artwork-id={entry.id} title={libraryCatalogCategoryPathLabel(entry.category, locale)} onClick={() => void insert(entry)}>
        <img src={staticArtworkContentUrl(entry.id)} alt="" loading="lazy"/>
        <strong>{entry.name}</strong><span>{text('Inserir SVG', 'Insert SVG', 'Insertar SVG')}</span>
      </button>)}
    </div>
    {!loading && !results.length && <p>{text('Nenhum SVG neste filtro.', 'No matching SVGs.', 'Sin SVG coincidentes.')}</p>}
    <footer><button type="button" disabled={page === 0} onClick={() => setPage(value => value - 1)}>←</button>
      <span>{results.length} SVG · {page + 1}/{Math.max(1, Math.ceil(results.length / 24))}</span>
      <button type="button" disabled={(page + 1) * 24 >= results.length} onClick={() => setPage(value => value + 1)}>→</button></footer>
  </section>;
}
