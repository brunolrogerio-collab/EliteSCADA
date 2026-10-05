import React, { useEffect, useMemo, useState } from 'react';
import { importVisualAsset } from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot } from './types';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type Artwork = { id: string; key: string; name: string; category: string; style: string; status: string; tags: string[] };

export function FactoryArtworkLibrary({ snapshot, locale, onApplied }: {
  snapshot: EngineeringSnapshot; locale: EngineeringLocale; onApplied: () => Promise<void>;
}) {
  const text = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const [open, setOpen] = useState(false);
  const [entries, setEntries] = useState<Artwork[]>([]);
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const [style, setStyle] = useState('');
  const [page, setPage] = useState(0);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  useEffect(() => {
    if (!open || entries.length) return;
    const abort = new AbortController();
    void fetch(`${API}/api/engineering/static-artwork`, { credentials: 'include', signal: abort.signal })
      .then(async response => { if (!response.ok) throw new Error(`HTTP ${response.status}`); return response.json() as Promise<{ entries: Artwork[] }>; })
      .then(result => { if (!abort.signal.aborted) { setEntries(result.entries); setError(''); } })
      .catch(reason => { if (!abort.signal.aborted) setError(String(reason)); });
    return () => abort.abort();
  }, [open, entries.length]);
  useEffect(() => setPage(0), [query, category, style]);
  const categories = useMemo(() => [...new Set(entries.map(item => item.category))].sort(), [entries]);
  const styles = useMemo(() => [...new Set(entries.map(item => item.style))].sort(), [entries]);
  const results = useMemo(() => entries.filter(item => (!category || item.category === category) && (!style || item.style === style) &&
    (!query || `${item.name} ${item.key} ${item.category} ${item.tags.join(' ')}`.toLocaleLowerCase().includes(query.toLocaleLowerCase()))), [entries, query, category, style]);
  const contentUrl = (item: Artwork) => `${API}/api/engineering/static-artwork/${encodeURIComponent(item.id)}/content`;
  const imported = (item: Artwork) => (snapshot.package.visualAssets ?? []).some(asset => asset.key === `factory.${item.id}`);
  const copy = async (item: Artwork) => {
    setBusy(item.id); setError(''); setNotice('');
    try {
      const response = await fetch(contentUrl(item), { credentials: 'include' });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      await importVisualAsset(await response.blob(), snapshot.workspace.changeVersion, { key: `factory.${item.id}`, name: item.name, fileName: `${item.id}.svg` });
      await onApplied();
      setNotice(text('SVG copiado para o projeto. Pode ser inserido nas telas e animado pelas propriedades.', 'SVG copied into the project. Insert it on screens and animate its properties.', 'SVG copiado al proyecto. Insértelo en pantallas y anime sus propiedades.'));
    } catch (reason) { setError(reason instanceof Error ? reason.message : String(reason)); }
    finally { setBusy(null); }
  };
  return <details className="eng-panel" data-testid="factory-artwork-library" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>{text('Biblioteca SVG da fábrica — prévias originais', 'Factory SVG library — original previews', 'Biblioteca SVG de fábrica — vistas previas originales')}{entries.length ? ` · ${entries.length}` : ''}</summary>
    <p>{text('Desenhos estáticos separados por categoria. Revise e copie os desejados para o projeto; a seleção não aprova o lote inteiro nem cria um dínamo. Os SVGs externos ainda dependem da licença de cada fonte.', 'Categorized static artwork. Review and copy selected items to the project; selection does not approve the entire batch or create a Dynamo. External SVGs require source-specific licenses.', 'Dibujos estáticos por categoría. Revise y copie los seleccionados; no aprueba todo el lote ni crea un dínamo. SVG externos requieren licencia por fuente.')}</p>
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <input type="search" aria-label="Search factory SVG" placeholder={text('Buscar desenho', 'Search artwork', 'Buscar dibujo')} value={query} onChange={event => setQuery(event.target.value)}/>
      <select aria-label="Factory SVG category" value={category} onChange={event => setCategory(event.target.value)}><option value="">{text('Todas as categorias', 'All categories', 'Todas las categorías')}</option>{categories.map(item => <option key={item}>{item}</option>)}</select>
      <select aria-label="Factory SVG style" value={style} onChange={event => setStyle(event.target.value)}><option value="">{text('Todos os estilos', 'All styles', 'Todos los estilos')}</option>{styles.map(item => <option key={item}>{item}</option>)}</select>
      <span>{results.length} SVG</span>
    </div>
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {open && <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: 12, marginTop: 12 }}>
      {results.slice(page * 60, page * 60 + 60).map(item => <article key={item.id} style={{ border: '1px solid var(--border, #9aa8b5)', borderRadius: 8, padding: 8 }}>
        <img src={contentUrl(item)} alt={item.name} loading="lazy" style={{ width: '100%', height: 96, objectFit: 'contain', background: '#eef2f5' }}/>
        <div>{item.name}</div><small>{item.category} · {item.status}</small>
        <button type="button" disabled={busy !== null || imported(item)} onClick={() => void copy(item)}>{imported(item) ? text('No projeto', 'In project', 'En proyecto') : busy === item.id ? '…' : text('Copiar para o projeto', 'Copy to project', 'Copiar al proyecto')}</button>
      </article>)}
    </div>}
    <div style={{ display: 'flex', gap: 12, marginTop: 12 }}>
      <button type="button" disabled={page === 0} onClick={() => setPage(value => value - 1)}>←</button>
      <span>{page + 1} / {Math.max(1, Math.ceil(results.length / 60))}</span>
      <button type="button" disabled={(page + 1) * 60 >= results.length} onClick={() => setPage(value => value + 1)}>→</button>
    </div>
  </details>;
}
