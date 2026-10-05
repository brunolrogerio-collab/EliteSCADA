import {
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type KeyboardEvent
} from 'react';
import type { VisualAssetEngineering, VisualEngineeringPropertyValue } from '../../types';
import { VISUAL_PROPERTY_KEYS, type VisualPropertyDefinition } from '../../../visual-runtime';
import { normalizeCanonicalStrokeStyle, svgStrokeDasharray } from '../visualStrokePresentation';
import type { PropertyInspectorCopy } from './PropertyInspector';
import {
  formatPropertyInspectorValue,
  parsePropertyInspectorInput,
  type PropertyInspectorRow
} from './propertyInspectorModel';

export type PropertyEditorControlProps = Readonly<{
  definition: VisualPropertyDefinition;
  objectType?: string;
  row: PropertyInspectorRow;
  text: PropertyInspectorCopy;
  visualAssets: readonly VisualAssetEngineering[];
  onImportImage?: (file: File) => Promise<string | null | void> | string | null | void;
  imageImportDisabled?: boolean;
  imageImportBusy?: boolean;
  commit: (value: VisualEngineeringPropertyValue) => boolean;
  setError: (message: string | null) => void;
}>;

const FONT_FAMILY_OPTIONS = Object.freeze(['system', 'Arimo Variable', 'Lato', 'Tinos', 'Cousine']);

export function PropertyEditorControl({
  definition,
  objectType,
  row,
  text,
  visualAssets,
  onImportImage,
  imageImportDisabled,
  imageImportBusy,
  commit,
  setError
}: PropertyEditorControlProps) {
  if (definition.type === 'number' && [VISUAL_PROPERTY_KEYS.imagePositionX, VISUAL_PROPERTY_KEYS.imagePositionY, VISUAL_PROPERTY_KEYS.imageZoom].includes(definition.key as never)) {
    return <ImageAdjustmentControl definition={definition} row={row} text={text} commit={commit} />;
  }

  if (definition.type === 'number' && definition.key === VISUAL_PROPERTY_KEYS.fontWeight) {
    return <BoldControl definition={definition} row={row} text={text} commit={commit} />;
  }

  if (definition.type === 'boolean') {
    return <BooleanControl definition={definition} row={row} text={text} commit={commit} />;
  }

  if (definition.type === 'enum' && definition.presentationHint === 'stroke-style') {
    return <StrokeStyleControl definition={definition} row={row} text={text} commit={commit} />;
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
      objectType={objectType}
      row={row}
      text={text}
      visualAssets={visualAssets}
      commit={commit}
      onImportImage={onImportImage}
      imageImportDisabled={imageImportDisabled}
      imageImportBusy={imageImportBusy}
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

function BoldControl({ definition, row, text, commit }: BasicEditorProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const weight = Number(row.value ?? row.defaultValue ?? 400);
  const checked = row.state !== 'mixed' && weight >= 600;

  useEffect(() => {
    if (inputRef.current) inputRef.current.indeterminate = row.state === 'mixed';
  }, [row.state]);

  return (
    <label className="property-inspector__boolean-control">
      <input
        id={`visual-property-${definition.key}`}
        ref={inputRef}
        type="checkbox"
        checked={checked}
        disabled={!definition.engineeringEditable}
        onChange={event => commit(event.currentTarget.checked ? 700 : 400)}
      />
      <span>{text.fontWeightLabel}</span>
    </label>
  );
}

function StrokeStyleControl({ definition, row, text, commit }: BasicEditorProps) {
  if (definition.type !== 'enum') return null;
  const current = row.state === 'mixed' ? '__mixed__' : String(row.value);

  return (
    <div
      className="property-inspector__stroke-style-control"
      role="radiogroup"
      aria-label={definition.key}
      data-property-editor="stroke-style"
    >
      {row.state === 'mixed' ? <span className="property-inspector__stroke-style-mixed">{text.mixed}</span> : null}
      {definition.allowedValues.map(option => {
        const canonical = normalizeCanonicalStrokeStyle(option);
        const selected = current === option;
        return (
          <button
            key={option}
            type="button"
            className={selected ? 'selected' : ''}
            role="radio"
            aria-checked={selected}
            aria-label={option}
            disabled={!definition.engineeringEditable}
            onClick={() => commit(option)}
          >
            <svg viewBox="0 0 64 10" aria-hidden="true" focusable="false">
              {canonical === 'none' ? (
                <path d="M4 8 L60 2" stroke="currentColor" strokeWidth="1.5" opacity="0.45" />
              ) : (
                <line
                  x1="2"
                  y1="5"
                  x2="62"
                  y2="5"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap={canonical === 'dotted' ? 'round' : 'butt'}
                  strokeDasharray={svgStrokeDasharray(canonical)}
                />
              )}
            </svg>
            <span>{option}</span>
          </button>
        );
      })}
    </div>
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
      {definition.allowedValues.map(option => <option key={option} value={option}>{definition.key === VISUAL_PROPERTY_KEYS.imageFit ? text.fitOptions[option] ?? option : option}</option>)}
    </select>
  );
}

function ImageAdjustmentControl({ definition, row, commit }: BasicEditorProps) {
  const isZoom = definition.key === VISUAL_PROPERTY_KEYS.imageZoom;
  const value = Number(row.state === 'mixed' ? row.defaultValue : row.value ?? row.defaultValue);
  const sliderValue = isZoom ? value : value * 100;
  const minimum = isZoom ? 1 : 0;
  const maximum = isZoom ? 8 : 100;
  const step = isZoom ? 0.1 : 1;
  return <div className="property-inspector__image-adjustment" data-property-editor="image-adjustment">
    <input
      id={`visual-property-${definition.key}`}
      type="range"
      min={minimum}
      max={maximum}
      step={step}
      value={Number.isFinite(sliderValue) ? sliderValue : minimum}
      disabled={!definition.engineeringEditable}
      onChange={event => {
        const next = Number(event.currentTarget.value);
        commit(isZoom ? next : next / 100);
      }}
    />
    <output>{isZoom ? `${sliderValue.toFixed(1)}×` : `${Math.round(sliderValue)}%`}</output>
  </div>;
}

function AssetReferenceControl({
  definition,
  objectType,
  row,
  text,
  visualAssets,
  commit,
  onImportImage,
  imageImportDisabled = false,
  imageImportBusy = false
}: Pick<PropertyEditorControlProps, 'definition' | 'objectType' | 'row' | 'text' | 'visualAssets' | 'commit' | 'onImportImage' | 'imageImportDisabled' | 'imageImportBusy'>) {
  const fileInput = useRef<HTMLInputElement>(null);
  const current = row.state === 'mixed'
    ? '__mixed__'
    : formatPropertyInspectorValue(row.value ?? row.defaultValue);
  const acceptedTypes = objectType === 'core.videoPlayer'
    ? ['video/mp4', 'video/webm']
    : objectType === 'core.pdfViewer'
      ? ['application/pdf']
      : objectType === 'core.svgSymbol'
        ? ['image/svg+xml']
        : ['image/png', 'image/jpeg', 'image/bmp', 'image/svg+xml'];
  const assets = visualAssets.filter(asset => typeof asset.id === 'string' && asset.id.length > 0 && acceptedTypes.includes(asset.mediaType));
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
      {onImportImage ? <>
        <input
          ref={fileInput}
          type="file"
          accept={acceptedTypes.join(',')}
          hidden
          onChange={event => {
            const file = event.currentTarget.files?.[0];
            event.currentTarget.value = '';
            if (file) void Promise.resolve(onImportImage(file)).then(id => {
              if (typeof id === 'string' && id) commit({ assetId: id });
            });
          }}
        />
        <button
          type="button"
          className="property-inspector__asset-import"
          disabled={!definition.engineeringEditable || imageImportDisabled || imageImportBusy}
          onClick={() => fileInput.current?.click()}
        >{imageImportBusy ? text.importingAsset : text.chooseImage}</button>
      </> : null}
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
  const displayValue = row.state === 'mixed' ? '__mixed__' : formatPropertyInspectorValue(row.value ?? row.defaultValue);
  const isKnownFamily = FONT_FAMILY_OPTIONS.includes(displayValue);
  const [customDraft, setCustomDraft] = useState(isKnownFamily ? '' : displayValue === '__mixed__' ? '' : displayValue);
  const [customMode, setCustomMode] = useState(!isKnownFamily);

  useEffect(() => {
    const known = FONT_FAMILY_OPTIONS.includes(displayValue);
    setCustomDraft(known || displayValue === '__mixed__' ? '' : displayValue);
    setCustomMode(!known && displayValue !== '__mixed__');
  }, [displayValue]);

  const selectValue = row.state === 'mixed' ? '__mixed__' : customMode ? '__custom__' : displayValue;
  const applyCustom = () => {
    if (!customDraft.trim()) return;
    const parsed = parsePropertyInspectorInput(definition, customDraft);
    if (!parsed.ok) {
      setError(parsed.error);
      return;
    }
    setError(null);
    commit(parsed.value);
  };

  return (
    <>
      <select
        id={`visual-property-${definition.key}`}
        value={selectValue}
        disabled={!definition.engineeringEditable}
        onChange={event => {
          const value = event.currentTarget.value;
          if (value === '__mixed__') return;
          if (value === '__custom__') {
            setCustomMode(true);
            if (!customDraft) setCustomDraft('');
            return;
          }
          setCustomMode(false);
          setError(null);
          commit(value);
        }}
      >
        {row.state === 'mixed' ? <option value="__mixed__" disabled>{text.mixed}</option> : null}
        {FONT_FAMILY_OPTIONS.map(font => <option key={font} value={font}>{text.fontFamilyOptions[font] ?? font}</option>)}
        <option value="__custom__">{text.fontFamilyOptions.custom ?? 'Custom…'}</option>
      </select>
      {customMode ? <input
        type="text"
        aria-label={text.fontFamilyPlaceholder}
        value={customDraft}
        placeholder={text.fontFamilyPlaceholder}
        disabled={!definition.engineeringEditable}
        onChange={event => {
          setCustomDraft(event.currentTarget.value);
          setError(null);
        }}
        onBlur={applyCustom}
        onKeyDown={(event: KeyboardEvent<HTMLInputElement>) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            event.currentTarget.blur();
          } else if (event.key === 'Escape') {
            setCustomDraft(displayValue === '__mixed__' ? '' : displayValue);
            setCustomMode(displayValue !== '__mixed__' && !FONT_FAMILY_OPTIONS.includes(displayValue));
            event.currentTarget.blur();
          }
        }}
      /> : null}
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
    if (commit(parsed.value)) {
      setDraft(String(parsed.value));
      setDirty(false);
    }
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
            // A first color choice must be immediately visible. Canonical color defaults may be
            // fully transparent (#RRGGBB00); preserving that zero alpha makes the picker look broken.
            // Preserve intentional translucency, but promote the transparent default to opaque.
            const next = withColorAlpha(event.currentTarget.value, alpha === 0 ? 100 : alpha);
            setDraft(next);
            setDirty(false);
            commit(next);
          }}
        />
        <input
          className="property-inspector__color-text"
          type="text"
          value={row.state === 'mixed' && !dirty ? '' : draft}
          placeholder={row.state === 'mixed' ? text.mixed : '#RRGGBB, #RRGGBBAA, rgb() or rgba()'}
          disabled={!definition.engineeringEditable}
          aria-label={`${definition.key} value`}
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
          aria-label={`${definition.key} alpha`}
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

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
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

  const sharedProps = {
    id: `visual-property-${definition.key}`,
    value: draft,
    placeholder: row.state === 'mixed' ? text.mixed : undefined,
    disabled: !definition.engineeringEditable,
    onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      setDraft(event.currentTarget.value);
      setDirty(true);
      setError(null);
    },
    onBlur: applyDraft,
    onKeyDown
  };

  if (definition.key === VISUAL_PROPERTY_KEYS.bezierPath) {
    return <textarea {...sharedProps} rows={4} spellCheck={false} data-property-editor="bezier-path" />;
  }

  return <input
    {...sharedProps}
    type={definition.type === 'number' ? 'number' : 'text'}
    min={definition.type === 'number' ? definition.minimum : undefined}
    max={definition.type === 'number' ? definition.maximum : undefined}
    step={definition.type === 'number' ? (definition.integer ? 1 : 'any') : undefined}
  />;
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
