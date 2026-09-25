import { useMemo, useState } from 'react';
import type { EngineeringLocale } from '../../i18n';
import type { VisualEditorPropertyInspectorContractProps } from '../visualEditorContracts';
import type { VisualAssetEngineering, VisualEngineeringPropertyValue } from '../../types';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../../visual-runtime';
import { BrowserConfigurationEditor } from '../BrowserConfigurationEditor';
import { EventsEditor } from '../events-editor/EventsEditor';
import { TrendPenEditor } from '../TrendPenEditor';
import { c07VisualEditorText, useC07VisualEditorText } from '../c07VisualEditorI18n';
import {
  trendInspectorControlCopy,
  trendPropertyLabel,
  trendPropertyOptionLabel
} from '../trendAuthoringModel';
import { PropertyEditorControl } from './PropertyEditorControl';
import {
  buildPropertyInspectorModel,
  buildPropertyInspectorRemoveIntent,
  buildPropertyInspectorSetIntent,
  type PropertyInspectorModel,
  type PropertyInspectorRow
} from './propertyInspectorModel';
import './PropertyInspector.css';

export type PropertyInspectorCopy = Readonly<{
  title: string;
  noSelection: string;
  selectHint: string;
  selected: (count: number) => string;
  useDefault: string;
  mixed: string;
  trueLabel: string;
  falseLabel: string;
  noAsset: string;
  assetBrowserHint: string;
  transparent: string;
  alpha: string;
  fontFamilyPlaceholder: string;
  defaultState: string;
  engineeringState: string;
  mixedState: (explicitCount: number, selectionCount: number) => string;
  filterLabel: string;
  filterPlaceholder: string;
  noMatches: string;
  category: Readonly<Record<string, string>>;
}>;

export type PropertyInspectorProps = VisualEditorPropertyInspectorContractProps & Readonly<{
  visualAssets?: readonly VisualAssetEngineering[];
  copy?: Partial<PropertyInspectorCopy>;
}>;

const DEFAULT_COPY: PropertyInspectorCopy = {
  title: 'Properties',
  noSelection: 'No selection',
  selectHint: 'Select a visual object to inspect its registered properties.',
  selected: count => `${count} selected`,
  useDefault: 'Use default',
  mixed: 'Mixed',
  trueLabel: 'True',
  falseLabel: 'False',
  noAsset: 'No asset',
  assetBrowserHint: 'Project asset library',
  transparent: 'Transparent',
  alpha: 'Alpha',
  fontFamilyPlaceholder: 'Choose or type a font family',
  defaultState: 'Default',
  engineeringState: 'Engineering',
  mixedState: (explicitCount, selectionCount) => `Mixed · ${explicitCount}/${selectionCount} explicit`,
  filterLabel: 'Filter properties',
  filterPlaceholder: 'Name or canonical key',
  noMatches: 'No properties match this filter.',
  category: {
    general: 'General',
    geometry: 'Geometry',
    appearance: 'Appearance',
    text: 'Text',
    image: 'Image',
    control: 'Control',
    trend: 'Trend'
  }
};

export function PropertyInspector({
  selectedElements,
  onMutationIntent,
  visualAssets = [],
  copy
}: PropertyInspectorProps) {
  const currentVisualText = useC07VisualEditorText();
  const locale = localeForVisualText(currentVisualText);
  const chromeText = propertyInspectorChromeText(locale);
  const text: PropertyInspectorCopy = {
    ...DEFAULT_COPY,
    ...chromeText,
    ...copy,
    category: { ...DEFAULT_COPY.category, ...(copy?.category ?? {}) }
  };
  const [filter, setFilter] = useState('');
  const [collapsedCategories, setCollapsedCategories] = useState<ReadonlySet<string>>(() => new Set());
  const model = useMemo(() => buildPropertyInspectorModel(selectedElements), [selectedElements]);
  const filteredRows = useMemo(() => filterPropertyRows(model.rows, filter, text), [model.rows, filter, text]);
  const groupedRows = useMemo(() => groupRows(filteredRows), [filteredRows]);

  const toggleCategory = (category: string) => {
    setCollapsedCategories(current => {
      const next = new Set(current);
      if (next.has(category)) next.delete(category);
      else next.add(category);
      return next;
    });
  };

  if (selectedElements.length === 0) {
    return (
      <aside className="property-inspector" data-testid="visual-property-inspector">
        <header className="property-inspector__header">
          <strong>{text.title}</strong>
          <span>{text.noSelection}</span>
        </header>
        <p className="property-inspector__empty">{text.selectHint}</p>
      </aside>
    );
  }

  if (model.error) {
    return (
      <aside className="property-inspector" data-testid="visual-property-inspector">
        <header className="property-inspector__header">
          <strong>{text.title}</strong>
          <span>{text.selected(selectedElements.length)}</span>
        </header>
        <p className="property-inspector__error" role="alert">{model.error}</p>
      </aside>
    );
  }

  const selectedTrend = selectedElements.length === 1 && selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.trend
    ? selectedElements[0]
    : null;
  const selectedBrowser = selectedElements.length === 1 && (
    selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser ||
    selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser
  ) ? selectedElements[0] : null;

  return (
    <aside className="property-inspector" data-testid="visual-property-inspector">
      <header className="property-inspector__header">
        <strong>{text.title}</strong>
        <span>{selectedElements.length === 1 ? selectedElements[0].key : text.selected(selectedElements.length)}</span>
      </header>

      {model.diagnostic ? <p className="property-inspector__diagnostic" role="status">{model.diagnostic}</p> : null}

      <label className="property-inspector__filter">
        <span>{text.filterLabel}</span>
        <input
          type="search"
          value={filter}
          aria-label={text.filterLabel}
          placeholder={text.filterPlaceholder}
          onChange={event => setFilter(event.currentTarget.value)}
        />
      </label>

      {groupedRows.length === 0 ? <p className="property-inspector__empty">{text.noMatches}</p> : null}
      {groupedRows.map(([category, rows]) => {
        const collapsed = collapsedCategories.has(category);
        return <section className={`property-inspector__group property-inspector__group--${category}`} key={category}>
          <button
            type="button"
            className="property-inspector__group-toggle"
            aria-expanded={!collapsed}
            onClick={() => toggleCategory(category)}
          >
            <span>{text.category[category] ?? category}</span>
            <small>{rows.length}</small>
          </button>
          {!collapsed ? <div className="property-inspector__group-fields">
            {rows.map(row => (
              <PropertyField
                key={row.definition.key}
                model={model}
                row={row}
                text={text}
                locale={locale}
                visualAssets={visualAssets}
                onMutationIntent={onMutationIntent}
              />
            ))}
          </div> : null}
        </section>;
      })}

      {selectedTrend ? <TrendPenEditor element={selectedTrend} onMutationIntent={onMutationIntent} /> : null}
      {selectedBrowser ? <BrowserConfigurationEditor element={selectedBrowser} locale={locale} onMutationIntent={onMutationIntent} /> : null}

      {selectedElements.length === 1 && selectedElements[0].id ? (
        <EventsEditor visualObjectId={selectedElements[0].id} />
      ) : null}
    </aside>
  );
}

type PropertyFieldProps = Readonly<{
  model: PropertyInspectorModel;
  row: PropertyInspectorRow;
  text: PropertyInspectorCopy;
  locale: EngineeringLocale;
  visualAssets: readonly VisualAssetEngineering[];
  onMutationIntent: VisualEditorPropertyInspectorContractProps['onMutationIntent'];
}>;

function PropertyField({ model, row, text, locale, visualAssets, onMutationIntent }: PropertyFieldProps) {
  const [error, setError] = useState<string | null>(null);
  const definition = row.definition;
  const localizedTrendLabel = trendPropertyLabel(locale, definition.key);
  const rowText: PropertyInspectorCopy = localizedTrendLabel
    ? { ...text, ...trendInspectorControlCopy(locale) }
    : text;

  const commit = (value: VisualEngineeringPropertyValue) => {
    const result = buildPropertyInspectorSetIntent(model, definition.key, value);
    if (!result.ok) {
      setError(result.error);
      return false;
    }
    setError(null);
    onMutationIntent(result.intent);
    return true;
  };

  const remove = () => {
    const result = buildPropertyInspectorRemoveIntent(model, definition.key);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setError(null);
    onMutationIntent(result.intent);
  };

  const trendModeControl = definition.key === VISUAL_PROPERTY_KEYS.trendMode && definition.type === 'enum';
  const trendModeValue = row.state === 'mixed' ? '__mixed__' : String(row.value);

  return (
    <div
      className="property-inspector__field"
      data-property-key={definition.key}
      data-editor-type={definition.type}
      data-editor-hint={definition.presentationHint ?? undefined}
    >
      <div className="property-inspector__field-heading">
        <div className="property-inspector__field-label">
          <label htmlFor={`visual-property-${definition.key}`}>
            {localizedTrendLabel ?? humanizeVisualPropertyKey(definition.key)}
          </label>
          <code title="Canonical property key">{definition.key}</code>
        </div>
        <span className={`property-inspector__state property-inspector__state--${row.state}`}>{stateLabel(row, rowText)}</span>
      </div>

      {trendModeControl ? (
        <select
          id={`visual-property-${definition.key}`}
          value={trendModeValue}
          disabled={!definition.engineeringEditable}
          onChange={event => commit(event.currentTarget.value)}
        >
          {row.state === 'mixed' ? <option value="__mixed__" disabled>{rowText.mixed}</option> : null}
          {definition.allowedValues.map(option => (
            <option key={option} value={option}>{trendPropertyOptionLabel(locale, definition.key, option)}</option>
          ))}
        </select>
      ) : (
        <PropertyEditorControl
          definition={definition}
          row={row}
          text={rowText}
          visualAssets={visualAssets}
          commit={commit}
          setError={setError}
        />
      )}

      <div className="property-inspector__field-meta">
        <span>{definition.type}{definition.unit ? ` · ${definition.unit}` : ''}</span>
        <button
          type="button"
          className="property-inspector__reset"
          disabled={row.state === 'default' || !definition.engineeringEditable}
          onClick={remove}
        >
          {rowText.useDefault}
        </button>
      </div>

      {error ? <p className="property-inspector__validation" role="alert">{error}</p> : null}
    </div>
  );
}

export function humanizeVisualPropertyKey(propertyKey: string): string {
  if (!propertyKey) return propertyKey;
  const words = propertyKey.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  return `${words[0].toUpperCase()}${words.slice(1)}`;
}

function filterPropertyRows(
  rows: readonly PropertyInspectorRow[],
  filter: string,
  text: PropertyInspectorCopy
): readonly PropertyInspectorRow[] {
  const query = filter.trim().toLocaleLowerCase();
  if (!query) return rows;
  return rows.filter(row => {
    const category = row.definition.category ?? 'general';
    return [
      row.definition.key,
      humanizeVisualPropertyKey(row.definition.key),
      text.category[category] ?? category
    ].some(value => value.toLocaleLowerCase().includes(query));
  });
}

function groupRows(rows: readonly PropertyInspectorRow[]): readonly [string, readonly PropertyInspectorRow[]][] {
  const groups = new Map<string, PropertyInspectorRow[]>();
  for (const row of rows) {
    const category = row.definition.category ?? 'general';
    const existing = groups.get(category);
    if (existing) existing.push(row);
    else groups.set(category, [row]);
  }
  return [...groups.entries()];
}

function stateLabel(row: PropertyInspectorRow, text: PropertyInspectorCopy): string {
  switch (row.state) {
    case 'default': return text.defaultState;
    case 'engineered': return text.engineeringState;
    case 'mixed': return text.mixedState(row.explicitCount, row.selectionCount);
  }
}

function propertyInspectorChromeText(locale: EngineeringLocale) {
  if (locale === 'pt-BR') return {
    filterLabel: 'Filtrar propriedades',
    filterPlaceholder: 'Nome ou chave canônica',
    noMatches: 'Nenhuma propriedade corresponde ao filtro.'
  };
  if (locale === 'es') return {
    filterLabel: 'Filtrar propiedades',
    filterPlaceholder: 'Nombre o clave canónica',
    noMatches: 'Ninguna propiedad coincide con el filtro.'
  };
  return {
    filterLabel: 'Filter properties',
    filterPlaceholder: 'Name or canonical key',
    noMatches: 'No properties match this filter.'
  };
}

function localeForVisualText(text: ReturnType<typeof useC07VisualEditorText>): EngineeringLocale {
  if (text === c07VisualEditorText('en')) return 'en';
  if (text === c07VisualEditorText('es')) return 'es';
  return 'pt-BR';
}

export default PropertyInspector;
