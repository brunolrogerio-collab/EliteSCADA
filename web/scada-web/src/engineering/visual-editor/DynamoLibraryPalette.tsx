import React, { useMemo, useState } from 'react';
import type { DynamoEngineering } from '../types';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import {
  buildDynamoLibraryEntries,
  filterDynamoLibraryEntries,
  listDynamoLibraryCategories
} from './dynamoLibraryModel';
import './DynamoLibraryPalette.css';

export function DynamoLibraryPalette({
  definitions,
  onMutationIntent,
  locale
}: {
  definitions: readonly DynamoEngineering[];
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  locale: 'pt-BR' | 'en' | 'es';
}) {
  const text = copy(locale);
  const entries = useMemo(() => buildDynamoLibraryEntries(definitions, locale), [definitions, locale]);
  const categories = useMemo(() => listDynamoLibraryCategories(entries), [entries]);
  const [selectedKey, setSelectedKey] = useState(entries[0]?.definition.key ?? '');
  const [equipmentPath, setEquipmentPath] = useState('');
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const visible = useMemo(
    () => filterDynamoLibraryEntries(entries, { query, category }),
    [entries, query, category]
  );
  const selected = visible.find(entry => entry.definition.key === selectedKey) ?? visible[0] ?? null;

  if (entries.length === 0) return null;

  return <section className="visual-dynamo-library" data-testid="visual-dynamo-library">
    <header><strong>{text.title}</strong><span>{text.hint}</span></header>

    <div className="visual-dynamo-library__filters">
      <label>
        <span>{text.search}</span>
        <input
          type="search"
          value={query}
          placeholder={text.searchPlaceholder}
          onChange={event => setQuery(event.currentTarget.value)}
          data-testid="dynamo-library-search"
        />
      </label>
      <label>
        <span>{text.category}</span>
        <select value={category} onChange={event => setCategory(event.currentTarget.value)}>
          <option value="">{text.allCategories}</option>
          {categories.map(value => <option key={value} value={value}>{categoryLabel(value, locale)}</option>)}
        </select>
      </label>
    </div>

    {visible.length > 0 ? <div className="visual-dynamo-library__grid" role="list" aria-label={text.results}>
      {visible.map(entry => <button
        key={entry.definition.key}
        type="button"
        role="listitem"
        className={`visual-dynamo-library__card${selected?.definition.key === entry.definition.key ? ' is-selected' : ''}`}
        aria-pressed={selected?.definition.key === entry.definition.key}
        onClick={() => setSelectedKey(entry.definition.key)}
      >
        <span className="visual-dynamo-library__thumbnail" aria-hidden="true">{entry.glyph}</span>
        <span className="visual-dynamo-library__card-copy">
          <strong>{entry.definition.name}</strong>
          <code>{entry.definition.key}</code>
          <small>{categoryLabel(entry.category, locale)} · {entry.width}×{entry.height}</small>
        </span>
      </button>)}
    </div> : <p className="visual-dynamo-library__empty">{text.noResults}</p>}

    {selected ? <div className="visual-dynamo-library__selection" data-testid="dynamo-library-selection">
      <div className="visual-dynamo-library__preview" aria-label={text.preview}>
        <span aria-hidden="true">{selected.glyph}</span>
        <div><strong>{selected.definition.name}</strong><small>{selected.width}×{selected.height}</small></div>
      </div>
      <div className="visual-dynamo-library__interface">
        <span>{text.publicInterface}</span>
        <div>
          {(selected.definition.parameters ?? []).slice(0, 6).map(parameter => <code key={parameter.key}>{parameter.key}</code>)}
          {selected.parameterCount > 6 ? <small>+{selected.parameterCount - 6}</small> : null}
          {selected.parameterCount === 0 ? <small>{text.noParameters}</small> : null}
        </div>
      </div>
      <label>
        <span>{text.equipmentPath}</span>
        <input value={equipmentPath} placeholder="Plant.P01" onChange={event => setEquipmentPath(event.currentTarget.value)} />
      </label>
      <button className="visual-dynamo-library__add" type="button" onClick={() => onMutationIntent({
        kind: 'dynamo.add',
        dynamoKey: selected.definition.key,
        equipmentPath: equipmentPath.trim() || null,
        defaultWidth: selected.width,
        defaultHeight: selected.height
      })}>{text.add}</button>
    </div> : null}
  </section>;
}

function categoryLabel(value: string, locale: 'pt-BR' | 'en' | 'es'): string {
  const labels: Record<string, readonly [string, string, string]> = {
    pump: ['Bomba', 'Pump', 'Bomba'],
    motor: ['Motor', 'Motor', 'Motor'],
    valve: ['Válvula', 'Valve', 'Válvula'],
    tank: ['Tanque', 'Tank', 'Tanque'],
    other: ['Outros', 'Other', 'Otros']
  };
  const selected = labels[value];
  if (!selected) return value;
  return locale === 'en' ? selected[1] : locale === 'es' ? selected[2] : selected[0];
}

function copy(locale: 'pt-BR' | 'en' | 'es') {
  if (locale === 'en') return {
    title: 'Dynamo library', hint: 'Search reusable process components and place configured instances.',
    search: 'Search', searchPlaceholder: 'Pump, valve, VFD…', category: 'Category', allCategories: 'All categories', results: 'Dynamo results', noResults: 'No Dynamo matches this filter.',
    preview: 'Selected Dynamo preview', publicInterface: 'Public interface', noParameters: 'No public parameters', equipmentPath: 'Equipment path (optional)', add: 'Add Dynamo'
  };
  if (locale === 'es') return {
    title: 'Biblioteca de dínamos', hint: 'Busque componentes de proceso reutilizables y coloque instancias configuradas.',
    search: 'Buscar', searchPlaceholder: 'Bomba, válvula, VFD…', category: 'Categoría', allCategories: 'Todas las categorías', results: 'Resultados de dínamos', noResults: 'Ningún dínamo coincide con este filtro.',
    preview: 'Preview del dínamo seleccionado', publicInterface: 'Interfaz pública', noParameters: 'Sin parámetros públicos', equipmentPath: 'Ruta del equipo (opcional)', add: 'Agregar dínamo'
  };
  return {
    title: 'Biblioteca de dínamos', hint: 'Busque componentes reutilizáveis de processo e insira instâncias configuradas.',
    search: 'Buscar', searchPlaceholder: 'Bomba, válvula, VFD…', category: 'Categoria', allCategories: 'Todas as categorias', results: 'Resultados de dínamos', noResults: 'Nenhum dínamo corresponde ao filtro.',
    preview: 'Preview do dínamo selecionado', publicInterface: 'Interface pública', noParameters: 'Sem parâmetros públicos', equipmentPath: 'Caminho do equipamento (opcional)', add: 'Adicionar dínamo'
  };
}
