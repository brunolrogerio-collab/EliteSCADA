import React, { useMemo, useState } from 'react';
import type { DynamoEngineering } from '../types';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { c07VisualEditorText } from './c07VisualEditorI18n';
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
  const text = c07VisualEditorText(locale).library;
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
          {categories.map(value => <option key={value} value={value}>{categoryLabel(value, text.categories)}</option>)}
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
          <small>{categoryLabel(entry.category, text.categories)} · {entry.width}×{entry.height}</small>
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

function categoryLabel(
  value: string,
  labels: Readonly<Record<'pump' | 'motor' | 'valve' | 'tank' | 'other', string>>
): string {
  return labels[value as keyof typeof labels] ?? value;
}
