import { useMemo, useState } from 'react';
import type { VisualEditorPropertyInspectorContractProps } from '../visualEditorContracts';
import type { VisualAssetEngineering, VisualEngineeringPropertyValue } from '../../types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../../../visual-runtime';
import { EventsEditor } from '../events-editor/EventsEditor';
import { TrendPenEditor } from '../TrendPenEditor';
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
  const text: PropertyInspectorCopy = {
    ...DEFAULT_COPY,
    ...copy,
    category: { ...DEFAULT_COPY.category, ...(copy?.category ?? {}) }
  };
  const model = useMemo(() => buildPropertyInspectorModel(selectedElements), [selectedElements]);
  const groupedRows = useMemo(() => groupRows(model.rows), [model.rows]);

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

  return (
    <aside className="property-inspector" data-testid="visual-property-inspector">
      <header className="property-inspector__header">
        <strong>{text.title}</strong>
        <span>{selectedElements.length === 1 ? selectedElements[0].key : text.selected(selectedElements.length)}</span>
      </header>

      {groupedRows.map(([category, rows]) => (
        <section className="property-inspector__group" key={category}>
          <h3>{text.category[category] ?? category}</h3>
          {rows.map(row => (
            <PropertyField
              key={row.definition.key}
              model={model}
              row={row}
              text={text}
              visualAssets={visualAssets}
              onMutationIntent={onMutationIntent}
            />
          ))}
        </section>
      ))}

      {selectedTrend ? <TrendPenEditor element={selectedTrend} onMutationIntent={onMutationIntent} /> : null}

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
  visualAssets: readonly VisualAssetEngineering[];
  onMutationIntent: VisualEditorPropertyInspectorContractProps['onMutationIntent'];
}>;

function PropertyField({ model, row, text, visualAssets, onMutationIntent }: PropertyFieldProps) {
  const [error, setError] = useState<string | null>(null);
  const definition = row.definition;

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

  return (
    <div
      className="property-inspector__field"
      data-property-key={definition.key}
      data-editor-type={definition.type}
      data-editor-hint={definition.presentationHint ?? undefined}
    >
      <div className="property-inspector__field-heading">
        <div className="property-inspector__field-label">
          <label htmlFor={`visual-property-${definition.key}`}>{humanizeVisualPropertyKey(definition.key)}</label>
          <code title="Canonical property key">{definition.key}</code>
        </div>
        <span className={`property-inspector__state property-inspector__state--${row.state}`}>{stateLabel(row, text)}</span>
      </div>

      <PropertyEditorControl
        definition={definition}
        row={row}
        text={text}
        visualAssets={visualAssets}
        commit={commit}
        setError={setError}
      />

      <div className="property-inspector__field-meta">
        <span>{definition.type}{definition.unit ? ` · ${definition.unit}` : ''}</span>
        <button
          type="button"
          className="property-inspector__reset"
          disabled={row.state === 'default' || !definition.engineeringEditable}
          onClick={remove}
        >
          {text.useDefault}
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

export default PropertyInspector;