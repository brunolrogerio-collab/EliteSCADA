import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent
} from 'react';
import type { VisualEditorPropertyInspectorContractProps } from '../visualEditorContracts';
import type { VisualEngineeringPropertyValue } from '../../types';
import type { VisualPropertyDefinition } from '../../../visual-runtime';
import {
  buildPropertyInspectorModel,
  buildPropertyInspectorRemoveIntent,
  buildPropertyInspectorSetIntent,
  formatPropertyInspectorValue,
  parsePropertyInspectorInput,
  type PropertyInspectorModel,
  type PropertyInspectorRow
} from './propertyInspectorModel';
import './PropertyInspector.css';

export function PropertyInspector({
  selectedElements,
  onMutationIntent
}: VisualEditorPropertyInspectorContractProps) {
  const model = useMemo(() => buildPropertyInspectorModel(selectedElements), [selectedElements]);
  const groupedRows = useMemo(() => groupRows(model.rows), [model.rows]);

  if (selectedElements.length === 0) {
    return (
      <aside className="property-inspector" data-testid="visual-property-inspector">
        <header className="property-inspector__header">
          <strong>Properties</strong>
          <span>No selection</span>
        </header>
        <p className="property-inspector__empty">Select a visual object to inspect its registered properties.</p>
      </aside>
    );
  }

  if (model.error) {
    return (
      <aside className="property-inspector" data-testid="visual-property-inspector">
        <header className="property-inspector__header">
          <strong>Properties</strong>
          <span>{selectedElements.length} selected</span>
        </header>
        <p className="property-inspector__error" role="alert">{model.error}</p>
      </aside>
    );
  }

  return (
    <aside className="property-inspector" data-testid="visual-property-inspector">
      <header className="property-inspector__header">
        <strong>Properties</strong>
        <span>{selectedElements.length === 1 ? selectedElements[0].key : `${selectedElements.length} selected`}</span>
      </header>

      {groupedRows.map(([category, rows]) => (
        <section className="property-inspector__group" key={category}>
          <h3>{category}</h3>
          {rows.map(row => (
            <PropertyField
              key={row.definition.key}
              model={model}
              row={row}
              onMutationIntent={onMutationIntent}
            />
          ))}
        </section>
      ))}
    </aside>
  );
}

type PropertyFieldProps = Readonly<{
  model: PropertyInspectorModel;
  row: PropertyInspectorRow;
  onMutationIntent: VisualEditorPropertyInspectorContractProps['onMutationIntent'];
}>;

function PropertyField({ model, row, onMutationIntent }: PropertyFieldProps) {
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
    <div className="property-inspector__field" data-property-key={definition.key}>
      <div className="property-inspector__field-heading">
        <label htmlFor={`visual-property-${definition.key}`}>{definition.key}</label>
        <span className={`property-inspector__state property-inspector__state--${row.state}`}>{stateLabel(row)}</span>
      </div>

      <EditorControl definition={definition} row={row} commit={commit} setError={setError} />

      <div className="property-inspector__field-meta">
        <span>{definition.type}{definition.unit ? ` · ${definition.unit}` : ''}</span>
        <button
          type="button"
          className="property-inspector__reset"
          disabled={row.state === 'default' || !definition.engineeringEditable}
          onClick={remove}
        >
          Use default
        </button>
      </div>

      {error ? <p className="property-inspector__validation" role="alert">{error}</p> : null}
    </div>
  );
}

type EditorControlProps = Readonly<{
  definition: VisualPropertyDefinition;
  row: PropertyInspectorRow;
  commit: (value: VisualEngineeringPropertyValue) => boolean;
  setError: (message: string | null) => void;
}>;

function EditorControl({ definition, row, commit, setError }: EditorControlProps) {
  if (definition.type === 'boolean') {
    return <BooleanControl definition={definition} row={row} commit={commit} />;
  }

  if (definition.type === 'enum') {
    return <EnumControl definition={definition} row={row} commit={commit} />;
  }

  return <TextualControl definition={definition} row={row} commit={commit} setError={setError} />;
}

function BooleanControl({ definition, row, commit }: Omit<EditorControlProps, 'setError'>) {
  const inputRef = useRef<HTMLInputElement>(null);
  const displayValue = row.state === 'mixed' ? false : Boolean(row.value);

  useEffect(() => {
    if (inputRef.current) inputRef.current.indeterminate = row.state === 'mixed';
  }, [row.state]);

  return (
    <label className="property-inspector__boolean-control">
      <input
        id={`visual-property-${definition.key}`}
        ref={inputRef}
        type="checkbox"
        checked={displayValue}
        disabled={!definition.engineeringEditable}
        onChange={event => commit(event.currentTarget.checked)}
      />
      <span>{row.state === 'mixed' ? 'Mixed' : displayValue ? 'True' : 'False'}</span>
    </label>
  );
}

function EnumControl({ definition, row, commit }: Omit<EditorControlProps, 'setError'>) {
  if (definition.type !== 'enum') return null;
  const value = row.state === 'mixed' ? '__mixed__' : String(row.value);

  return (
    <select
      id={`visual-property-${definition.key}`}
      value={value}
      disabled={!definition.engineeringEditable}
      onChange={event => commit(event.currentTarget.value)}
    >
      {row.state === 'mixed' ? <option value="__mixed__" disabled>Mixed</option> : null}
      {definition.allowedValues.map(option => <option key={option} value={option}>{option}</option>)}
    </select>
  );
}

function TextualControl({ definition, row, commit, setError }: EditorControlProps) {
  const displayValue = row.state === 'mixed' ? '' : formatPropertyInspectorValue(row.value ?? row.defaultValue);
  const [draft, setDraft] = useState(displayValue);
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    setDraft(displayValue);
    setDirty(false);
  }, [displayValue]);

  const applyDraft = () => {
    if (!dirty) return;
    const parsed = parsePropertyInspectorInput(definition, draft);
    if (!parsed.ok) {
      setError(parsed.error);
      return;
    }
    if (commit(parsed.value)) setDirty(false);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      applyDraft();
      event.currentTarget.blur();
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      setDraft(displayValue);
      setDirty(false);
      setError(null);
      event.currentTarget.blur();
    }
  };

  return (
    <input
      id={`visual-property-${definition.key}`}
      type={definition.type === 'number' ? 'number' : 'text'}
      value={draft}
      placeholder={row.state === 'mixed' ? 'Mixed' : definition.type === 'assetRef' && row.defaultValue === null ? 'No asset' : undefined}
      min={definition.type === 'number' ? definition.minimum : undefined}
      max={definition.type === 'number' ? definition.maximum : undefined}
      step={definition.type === 'number' ? (definition.integer ? 1 : 'any') : undefined}
      disabled={!definition.engineeringEditable}
      aria-invalid={Boolean(false)}
      onChange={event => {
        setDraft(event.currentTarget.value);
        setDirty(true);
        setError(null);
      }}
      onBlur={applyDraft}
      onKeyDown={onKeyDown}
    />
  );
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

function stateLabel(row: PropertyInspectorRow): string {
  switch (row.state) {
    case 'default': return 'Default';
    case 'engineered': return 'Engineering';
    case 'mixed': return `Mixed · ${row.explicitCount}/${row.selectionCount} explicit`;
  }
}

export default PropertyInspector;
