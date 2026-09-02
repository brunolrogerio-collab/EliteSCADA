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
import { EventsEditor } from '../events-editor/EventsEditor';
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
  copy?: Partial<PropertyInspectorCopy>;
}>;

const FONT_FAMILY_SUGGESTIONS = Object.freeze([
  'system',
  'Arial',
  'Helvetica',
  'Verdana',
  'Tahoma',
  'Georgia',
  'Times New Roman',
  'Courier New',
  'monospace'
]);

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
  assetBrowserHint: 'Choose a project-owned asset with the Asset browser below.',
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
    control: 'Control'
  }
};

export function PropertyInspector({
  selectedElements,
  onMutationIntent,
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
              onMutationIntent={onMutationIntent}
            />
          ))}
        </section>
      ))}

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
  onMutationIntent: VisualEditorPropertyInspectorContractProps['onMutationIntent'];
}>;

function PropertyField({ model, row, text, onMutationIntent }: PropertyFieldProps) {
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
        <label htmlFor={`visual-property-${definition.key}`}>{definition.key}</label>
        <span className={`property-inspector__state property-inspector__state--${row.state}`}>{stateLabel(row, text)}</span>
      </div>

      <EditorControl definition={definition} row={row} text={text} commit={commit} setError={setError} />

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

type EditorControlProps = Readonly<{
  definition: VisualPropertyDefinition;
  row: PropertyInspectorRow;
  text: PropertyInspectorCopy;
  commit: (value: VisualEngineeringPropertyValue) => boolean;
  setError: (message: string | null) => void;
}>;

function EditorControl({ definition, row, text, commit, setError }: EditorControlProps) {
  if (definition.type === 'boolean') {
    return <BooleanControl definition={definition} row={row} text={text} commit={commit} />;
  }

  if (definition.type === 'enum') {
    return <EnumControl definition={definition} row={row} text={text} commit={commit} />;
  }

  if (definition.type === 'color') {
    return <ColorControl definition={definition} row={row} text={text} commit={commit} setError={setError} />;
  }

  if (definition.type === 'assetRef' || definition.presentationHint === 'project-asset') {
    return <AssetReferenceControl definition={definition} row={row} text={text} />;
  }

  if (definition.type === 'string' && definition.presentationHint === 'font-family') {
    return <FontFamilyControl definition={definition} row={row} text={text} commit={commit} setError={setError} />;
  }

  return <TextualControl definition={definition} row={row} text={text} commit={commit} setError={setError} />;
}

function BooleanControl({ definition, row, text, commit }: Omit<EditorControlProps, 'setError'>) {
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
      <span>{row.state === 'mixed' ? text.mixed : displayValue ? text.trueLabel : text.falseLabel}</span>
    </label>
  );
}

function EnumControl({ definition, row, text, commit }: Omit<EditorControlProps, 'setError'>) {
  if (definition.type !== 'enum') return null;
  const value = row.state === 'mixed' ? '__mixed__' : String(row.value);

  return (
    <select
      id={`visual-property-${definition.key}`}
      value={value}
      disabled={!definition.engineeringEditable}
      onChange={event => commit(event.currentTarget.value)}
    >
      {row.state === 'mixed' ? <option value="__mixed__" disabled>{text.mixed}</option> : null}
      {definition.allowedValues.map(option => <option key={option} value={option}>{option}</option>)}
    </select>
  );
}

function AssetReferenceControl({ definition, row, text }: Pick<EditorControlProps, 'definition' | 'row' | 'text'>) {
  const displayValue = row.state === 'mixed' ? '' : formatPropertyInspectorValue(row.value ?? row.defaultValue);
  return (
    <div className="property-inspector__asset-reference">
      <input
        id={`visual-property-${definition.key}`}
        type="text"
        value={displayValue}
        placeholder={row.state === 'mixed' ? text.mixed : text.noAsset}
        readOnly
        aria-readonly="true"
      />
      <small>{text.assetBrowserHint}</small>
    </div>
  );
}

function FontFamilyControl({ definition, row, text, commit, setError }: EditorControlProps) {
  const displayValue = row.state === 'mixed' ? '' : formatPropertyInspectorValue(row.value ?? row.defaultValue);
  const [draft, setDraft] = useState(displayValue);
  const [dirty, setDirty] = useState(false);
  const listId = `visual-property-${definition.key}-fonts`;

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

  return (
    <>
      <input
        id={`visual-property-${definition.key}`}
        type="text"
        list={listId}
        value={draft}
        placeholder={row.state === 'mixed' ? text.mixed : text.fontFamilyPlaceholder}
        disabled={!definition.engineeringEditable}
        onChange={event => {
          setDraft(event.currentTarget.value);
          setDirty(true);
          setError(null);
        }}
        onBlur={applyDraft}
        onKeyDown={event => {
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
        }}
      />
      <datalist id={listId}>
        {FONT_FAMILY_SUGGESTIONS.map(font => <option key={font} value={font} />)}
      </datalist>
    </>
  );
}

function ColorControl({ definition, row, text, commit, setError }: EditorControlProps) {
  const displayValue = row.state === 'mixed'
    ? formatPropertyInspectorValue(row.defaultValue)
    : formatPropertyInspectorValue(row.value ?? row.defaultValue);
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

  const pickerColor = colorPickerValue(draft || displayValue);
  const alpha = colorAlphaPercent(draft || displayValue);

  return (
    <div className="property-inspector__color-control">
      <div className="property-inspector__color-row">
        <input
          id={`visual-property-${definition.key}`}
          className="property-inspector__color-picker"
          type="color"
          value={pickerColor}
          disabled={!definition.engineeringEditable}
          aria-label={`${definition.key} color`}
          onChange={event => {
            const next = withColorAlpha(event.currentTarget.value, alpha);
            setDraft(next);
            setDirty(false);
            commit(next);
          }}
        />
        <input
          className="property-inspector__color-text"
          type="text"
          value={row.state === 'mixed' && !dirty ? '' : draft}
          placeholder={row.state === 'mixed' ? text.mixed : '#RRGGBB or #RRGGBBAA'}
          disabled={!definition.engineeringEditable}
          onChange={event => {
            setDraft(event.currentTarget.value);
            setDirty(true);
            setError(null);
          }}
          onBlur={applyDraft}
          onKeyDown={event => {
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
          }}
        />
      </div>
      <label className="property-inspector__alpha-control">
        <span>{text.alpha}</span>
        <input
          type="range"
          min={0}
          max={100}
          step={1}
          value={alpha}
          disabled={!definition.engineeringEditable}
          onChange={event => {
            const next = withColorAlpha(pickerColor, Number(event.currentTarget.value));
            setDraft(next);
            setDirty(false);
            commit(next);
          }}
        />
        <output>{alpha}%</output>
      </label>
      <button
        type="button"
        className="property-inspector__transparent"
        disabled={!definition.engineeringEditable}
        onClick={() => {
          setDraft('#00000000');
          setDirty(false);
          commit('#00000000');
        }}
      >
        {text.transparent}
      </button>
    </div>
  );
}

function TextualControl({ definition, row, text, commit, setError }: EditorControlProps) {
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
      placeholder={row.state === 'mixed' ? text.mixed : undefined}
      min={definition.type === 'number' ? definition.minimum : undefined}
      max={definition.type === 'number' ? definition.maximum : undefined}
      step={definition.type === 'number' ? (definition.integer ? 1 : 'any') : undefined}
      disabled={!definition.engineeringEditable}
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

function colorPickerValue(value: string): string {
  const match = /^#([0-9A-Fa-f]{6})(?:[0-9A-Fa-f]{2})?$/.exec(value);
  return match ? `#${match[1]}` : '#000000';
}

function colorAlphaPercent(value: string): number {
  const match = /^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})$/.exec(value);
  if (!match) return 100;
  return Math.round((Number.parseInt(match[1], 16) / 255) * 100);
}

function withColorAlpha(color: string, alphaPercent: number): string {
  const normalizedAlpha = Math.max(0, Math.min(100, Math.round(alphaPercent)));
  if (normalizedAlpha === 100) return color;
  const alpha = Math.round((normalizedAlpha / 100) * 255).toString(16).padStart(2, '0').toUpperCase();
  return `${color}${alpha}`;
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
