import {
  useEffect,
  useRef,
  useState,
  type KeyboardEvent
} from 'react';
import type { VisualAssetEngineering, VisualEngineeringPropertyValue } from '../../types';
import type { VisualPropertyDefinition } from '../../../visual-runtime';
import type { PropertyInspectorCopy } from './PropertyInspector';
import {
  formatPropertyInspectorValue,
  parsePropertyInspectorInput,
  type PropertyInspectorRow
} from './propertyInspectorModel';

export type PropertyEditorControlProps = Readonly<{
  definition: VisualPropertyDefinition;
  row: PropertyInspectorRow;
  text: PropertyInspectorCopy;
  visualAssets: readonly VisualAssetEngineering[];
  commit: (value: VisualEngineeringPropertyValue) => boolean;
  setError: (message: string | null) => void;
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

export function PropertyEditorControl({
  definition,
  row,
  text,
  visualAssets,
  commit,
  setError
}: PropertyEditorControlProps) {
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
    return <AssetReferenceControl
      definition={definition}
      row={row}
      text={text}
      visualAssets={visualAssets}
      commit={commit}
    />;
  }

  if (definition.type === 'string' && definition.presentationHint === 'font-family') {
    return <FontFamilyControl definition={definition} row={row} text={text} commit={commit} setError={setError} />;
  }

  return <TextualControl definition={definition} row={row} text={text} commit={commit} setError={setError} />;
}

type BasicEditorProps = Pick<PropertyEditorControlProps, 'definition' | 'row' | 'text' | 'commit'>;

function BooleanControl({ definition, row, text, commit }: BasicEditorProps) {
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

function EnumControl({ definition, row, text, commit }: BasicEditorProps) {
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

function AssetReferenceControl({
  definition,
  row,
  text,
  visualAssets,
  commit
}: Pick<PropertyEditorControlProps, 'definition' | 'row' | 'text' | 'visualAssets' | 'commit'>) {
  const current = row.state === 'mixed'
    ? '__mixed__'
    : formatPropertyInspectorValue(row.value ?? row.defaultValue);
  const assets = visualAssets.filter(asset => typeof asset.id === 'string' && asset.id.length > 0);
  const selectedValue = current.startsWith('asset:') ? current.slice('asset:'.length) : current;

  return (
    <div
      className="property-inspector__asset-reference"
      data-testid="visual-editor-image-asset-picker"
      data-property-editor="project-asset"
    >
      <select
        id={`visual-property-${definition.key}`}
        value={row.state === 'mixed' ? '__mixed__' : selectedValue}
        disabled={!definition.engineeringEditable}
        aria-label={text.assetBrowserHint}
        onChange={event => commit(
          event.currentTarget.value ? { assetId: event.currentTarget.value } : null
        )}
      >
        {row.state === 'mixed' ? <option value="__mixed__" disabled>{text.mixed}</option> : null}
        <option value="">{text.noAsset}</option>
        {assets.map(asset => (
          <option key={asset.id!} value={asset.id!}>
            {asset.name || asset.key} · {asset.originalFileName}
          </option>
        ))}
      </select>
      <small>{text.assetBrowserHint}</small>
    </div>
  );
}

function FontFamilyControl({
  definition,
  row,
  text,
  commit,
  setError
}: Omit<PropertyEditorControlProps, 'visualAssets'>) {
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

function ColorControl({
  definition,
  row,
  text,
  commit,
  setError
}: Omit<PropertyEditorControlProps, 'visualAssets'>) {
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
    <div className="property-inspector__color-control" data-property-editor="color">
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

function TextualControl({
  definition,
  row,
  text,
  commit,
  setError
}: Omit<PropertyEditorControlProps, 'visualAssets'>) {
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
