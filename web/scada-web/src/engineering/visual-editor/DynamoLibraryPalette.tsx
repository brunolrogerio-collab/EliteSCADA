import React, { useMemo, useState } from 'react';
import type { DynamoEngineering } from '../types';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { c07VisualEditorText } from './c07VisualEditorI18n';
import { CanonicalVisualPreview } from './CanonicalVisualPreview';
import {
  buildDynamoLibraryEntries,
  filterDynamoLibraryEntries,
  listDynamoLibraryCategoryPaths,
  selectDefaultDynamoCatalog
} from './dynamoLibraryModel';
import {
  libraryCatalogCategoryPathLabel
} from '../libraryCatalogTaxonomy';
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
  const text = c07VisualEditorText(locale).library;
  const entries = useMemo(() => buildDynamoLibraryEntries(
    selectDefaultDynamoCatalog(definitions),
    locale
  ), [definitions, locale]);
  const categoryPaths = useMemo(() => listDynamoLibraryCategoryPaths(entries), [entries]);
  const [selectedKey, setSelectedKey] = useState(entries[0]?.definition.key ?? '');
  const [equipmentPath, setEquipmentPath] = useState('');
  const [query, setQuery] = useState('');
  const [categoryPath, setCategoryPath] = useState('');
  const [source, setSource] = useState<'builtin' | 'project' | ''>('');
  const visible = useMemo(
    () => filterDynamoLibraryEntries(entries, { query, categoryPath, source }),
    [entries, query, categoryPath, source]
  );
  const selected = visible.find(entry => entry.definition.key === selectedKey) ?? visible[0] ?? null;

  if (entries.length === 0) return null;

  return <section className="visual-dynamo-library" data-testid="visual-dynamo-library">
    <header><strong>{text.title}</strong><span>{text.hint}</span></header>

    <div className="visual-dynamo-library__filters">
      <label>
        <span>{text.search}</span>
        <input type="search" value={query} placeholder={text.searchPlaceholder}
          onChange={event => setQuery(event.currentTarget.value)} data-testid="dynamo-library-search" />
      </label>
      <label>
        <span>{text.category}</span>
        <select
          value={categoryPath}
          onChange={event => setCategoryPath(event.currentTarget.value)}
          data-testid="dynamo-library-category-path"
        >
          <option value="">{text.allCategories}</option>
          {categoryPaths.map(value => <option key={value} value={value}>{libraryCatalogCategoryPathLabel(value, locale)}</option>)}
        </select>
      </label>
      <label>
        <span>{sourceHeading(locale)}</span>
        <select
          value={source}
          onChange={event => setSource(event.currentTarget.value as 'builtin' | 'project' | '')}
          data-testid="dynamo-library-source"
        >
          <option value="">{sourceLabel('', locale)}</option>
          <option value="builtin">{sourceLabel('builtin', locale)}</option>
          <option value="project">{sourceLabel('project', locale)}</option>
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
        data-dynamo-key={entry.definition.key}
        data-dynamo-style={entry.visualStyle}
        onClick={() => setSelectedKey(entry.definition.key)}
      >
        <CanonicalVisualPreview elements={entry.definition.elements} locale={locale} width={entry.width} height={entry.height}
          emptyLabel={text.noVisual} variant="thumbnail" testId="dynamo-library-canonical-thumbnail" />
        <span className="visual-dynamo-library__card-copy">
          <strong>{entry.definition.name}</strong>
          <code>{entry.definition.key}</code>
          <small>{libraryCatalogCategoryPathLabel(entry.categoryPath, locale)} · {visualStyleLabel(entry.visualStyle, locale)} · {entry.width}×{entry.height}</small>
        </span>
      </button>)}
    </div> : <p className="visual-dynamo-library__empty">{text.noResults}</p>}

    {selected ? <div className="visual-dynamo-library__selection" data-testid="dynamo-library-selection">
      <div className="visual-dynamo-library__preview" aria-label={text.preview}>
        <CanonicalVisualPreview elements={selected.definition.elements} locale={locale} width={selected.width} height={selected.height}
          emptyLabel={text.noVisual} variant="detail" testId="dynamo-library-canonical-preview" />
      </div>
      <dl className="visual-dynamo-library__metadata">
        <div><dt>{text.dimensions}</dt><dd>{selected.width}×{selected.height}</dd></div>
        <div><dt>{text.version}</dt><dd>{selected.definition.properties?.libraryVersion ?? '—'}</dd></div>
        <div><dt>{text.source}</dt><dd>{sourceLabel(selected.source, locale)}</dd></div>
        <div><dt>{visualStyleHeading(locale)}</dt><dd>{visualStyleLabel(selected.visualStyle, locale)}</dd></div>
      </dl>
      <div className="visual-dynamo-library__interface">
        <span>{text.publicInterface}</span>
        <div>
          {(selected.definition.parameters ?? []).slice(0, 8).map(parameter => <code key={parameter.key}>
            {parameter.key} · {parameter.kind}{parameter.required ? ` · ${text.required}` : ''}
          </code>)}
          {selected.parameterCount > 8 ? <small>+{selected.parameterCount - 8}</small> : null}
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
        dynamoDefinitionId: selected.definition.id ?? null,
        equipmentPath: equipmentPath.trim() || null,
        defaultWidth: selected.width,
        defaultHeight: selected.height
      })}>{text.add}</button>
    </div> : null}
  </section>;
}

function visualStyleHeading(locale: 'pt-BR' | 'en' | 'es'): string {
  return locale === 'pt-BR' ? 'Estilo' : locale === 'es' ? 'Estilo' : 'Style';
}

function visualStyleLabel(value: string, locale: 'pt-BR' | 'en' | 'es'): string {
  const labels = {
    'pt-BR': {
      'detailed-2d': '2D detalhado',
      'dimensional-front': '3D frontal',
      'high-performance': 'High Performance'
    },
    en: {
      'detailed-2d': 'Detailed 2D',
      'dimensional-front': 'Front 3D',
      'high-performance': 'High Performance'
    },
    es: {
      'detailed-2d': '2D detallado',
      'dimensional-front': '3D frontal',
      'high-performance': 'High Performance'
    }
  } as const;
  return labels[locale][value as keyof typeof labels['pt-BR']] ?? value;
}

function sourceHeading(locale: 'pt-BR' | 'en' | 'es'): string {
  return locale === 'pt-BR' ? 'Origem' : locale === 'es' ? 'Origen' : 'Origin';
}

function sourceLabel(value: 'builtin' | 'project' | '', locale: 'pt-BR' | 'en' | 'es'): string {
  if (!value) return locale === 'pt-BR' ? 'Todas as origens' : locale === 'es' ? 'Todos los orígenes' : 'All origins';
  if (value === 'builtin') return 'EliteSCADA Built-in';
  return locale === 'pt-BR' ? 'Projeto / Usuário' : locale === 'es' ? 'Proyecto / Usuario' : 'Project / User';
}
