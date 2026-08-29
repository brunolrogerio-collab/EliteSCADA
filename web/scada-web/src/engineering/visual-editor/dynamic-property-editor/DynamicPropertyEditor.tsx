import { useMemo, useState } from 'react';
import type {
  VisualAnalogFillEngineering,
  VisualBooleanConditionEngineering,
  VisualElementEngineering,
  VisualExpressionDependencyEngineering,
  VisualPropertyExpressionEngineering
} from '../../types';
import type {
  VisualEditorBindingSourceCatalogItem,
  VisualEditorMutationIntent
} from '../visualEditorContracts';
import {
  compatibleBindingSources,
  createBindingRemoveIntent,
  createBindingSetIntent,
  createTagBitBindingSource
} from '../binding-editor/bindingEditorModel';
import {
  createAnalogFillEngineering,
  createDirectBooleanCondition,
  createExpressionDependency,
  createNumericIntervalCondition,
  createValueSource,
  createVisualExpressionEngineering,
  listDynamicPropertyDestinations,
  validateVisualExpressionAuthoring,
  type DynamicPropertyDestination,
  type DynamicPropertySourceMode
} from './visualDynamicAuthoringModel';
import './DynamicPropertyEditor.css';

export type DynamicPropertyEditorProps = Readonly<{
  element: VisualElementEngineering;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  onBindingIntent: (intent: VisualEditorMutationIntent) => void;
  onSetExpression: (configuration: VisualPropertyExpressionEngineering) => void;
  onRemoveExpression: (propertyKey: string) => void;
  onSetBooleanCondition: (configuration: VisualBooleanConditionEngineering) => void;
  onRemoveBooleanCondition: (propertyKey: string) => void;
  onSetAnalogFill: (configuration: VisualAnalogFillEngineering) => void;
  onRemoveAnalogFill: () => void;
}>;

type ExpressionDraft = Readonly<{
  text: string;
  dependencies: readonly VisualExpressionDependencyEngineering[];
}>;

const EMPTY_EXPRESSION: ExpressionDraft = Object.freeze({ text: '', dependencies: Object.freeze([]) });

/**
 * Transient FOLLOW-B authoring surface. Form state is intentionally disposable;
 * Apply callbacks emit only the shared canonical Engineering DTOs/intents.
 */
export function DynamicPropertyEditor(props: DynamicPropertyEditorProps) {
  const { element } = props;
  const destinations = useMemo(() => listDynamicPropertyDestinations(element), [element.type]);
  const [propertyKey, setPropertyKey] = useState(() => destinations[0]?.propertyKey ?? '');
  const destination = destinations.find(item => item.propertyKey === propertyKey) ?? destinations[0] ?? null;
  const [mode, setMode] = useState<DynamicPropertySourceMode>(() => destination ? effectiveMode(element, destination.propertyKey) : 'Constant');

  if (!destination) {
    return <section className="dynamic-property-editor" data-testid="visual-dynamic-property-editor">
      <header><strong>Dynamic source</strong><span>No Boolean or numeric bindable properties.</span></header>
    </section>;
  }

  const selectProperty = (nextKey: string) => {
    const next = destinations.find(item => item.propertyKey === nextKey);
    setPropertyKey(nextKey);
    setMode(next ? effectiveMode(element, next.propertyKey) : 'Constant');
  };

  return <section className="dynamic-property-editor" data-testid="visual-dynamic-property-editor">
    <header><strong>Dynamic source</strong><span>Canonical Binding/Expression configuration</span></header>
    <label><span>Visual property</span><select value={destination.propertyKey} onChange={event => selectProperty(event.currentTarget.value)}>
      {destinations.map(item => <option key={item.propertyKey} value={item.propertyKey}>{item.propertyKey} · {item.propertyType}</option>)}
    </select></label>
    <label><span>Source mode</span><select value={mode} onChange={event => setMode(event.currentTarget.value as DynamicPropertySourceMode)}>
      {destination.sourceModes.map(item => <option key={item} value={item}>{modeLabel(item)}</option>)}
    </select></label>

    {mode === 'Constant' ? <ConstantMode key={`constant:${destination.propertyKey}`} destination={destination} {...props} /> : null}
    {mode === 'DirectBinding' ? <DirectBindingMode key={`binding:${destination.propertyKey}`} destination={destination} {...props} /> : null}
    {mode === 'BooleanCondition' && destination.propertyType === 'boolean'
      ? <BooleanConditionMode key={`condition:${destination.propertyKey}`} destination={destination} {...props} />
      : null}
    {mode === 'Expression' ? <ExpressionMode key={`expression:${destination.propertyKey}`} destination={destination} {...props} /> : null}
    {supportsAnalogFill(element.type) ? <AnalogFillMode key={`analog:${element.id ?? element.key}`} {...props} /> : null}
  </section>;
}

type DestinationProps = DynamicPropertyEditorProps & Readonly<{ destination: DynamicPropertyDestination }>;

function ConstantMode({
  destination,
  element,
  onBindingIntent,
  onRemoveExpression,
  onRemoveBooleanCondition
}: DestinationProps) {
  return <div className="dynamic-property-editor__panel">
    <p>Engineering/default value remains authoritative for this property.</p>
    <button type="button" onClick={() => {
      removeBindingIfPossible(element, destination.propertyKey, onBindingIntent);
      onRemoveExpression(destination.propertyKey);
      onRemoveBooleanCondition(destination.propertyKey);
    }}>Use constant</button>
  </div>;
}

function DirectBindingMode({
  destination,
  element,
  sourceCatalog,
  onBindingIntent,
  onRemoveExpression,
  onRemoveBooleanCondition
}: DestinationProps) {
  const sources = useMemo(
    () => compatibleBindingSources({ key: destination.propertyKey, type: destination.propertyType }, sourceCatalog),
    [destination.propertyKey, destination.propertyType, sourceCatalog]
  );
  const [selected, setSelected] = useState(sources[0]?.target ?? '');
  const [bitIndex, setBitIndex] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const source = sources.find(item => item.target === selected) ?? sources[0];
  const bitCapability = source?.selectorCapability?.kind === 'bit' ? source.selectorCapability : null;

  return <div className="dynamic-property-editor__panel">
    <SourceSelect sources={sources} source={source} onChange={setSelected} />
    {bitCapability ? <BitIndexControl capability={bitCapability} value={bitIndex} onChange={setBitIndex} /> : null}
    <button type="button" disabled={!source || !element.id} onClick={() => {
      if (!source) return;
      try {
        const effectiveSource = bitCapability ? createTagBitBindingSource(source, bitIndex) : source;
        onBindingIntent(createBindingSetIntent(element, destination.propertyKey, effectiveSource));
        onRemoveExpression(destination.propertyKey);
        onRemoveBooleanCondition(destination.propertyKey);
        setError(null);
      } catch (reason) { setError(errorText(reason)); }
    }}>Apply direct binding</button>
    {!source ? <p className="dynamic-property-editor__warning">No compatible canonical source is available.</p> : null}
    {error ? <ErrorText message={error} /> : null}
  </div>;
}

function BooleanConditionMode({
  destination,
  element,
  sourceCatalog,
  onBindingIntent,
  onRemoveExpression,
  onSetBooleanCondition
}: DestinationProps) {
  const [kind, setKind] = useState<'Direct' | 'NumericInterval'>('Direct');
  const [sourceTarget, setSourceTarget] = useState('');
  const [bitIndex, setBitIndex] = useState(0);
  const [minimum, setMinimum] = useState('');
  const [maximum, setMaximum] = useState('');
  const [minimumInclusive, setMinimumInclusive] = useState(true);
  const [maximumInclusive, setMaximumInclusive] = useState(true);
  const [outside, setOutside] = useState(false);
  const [negate, setNegate] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sources = sourceCatalog.filter(source => kind === 'Direct'
    ? sourceValueType(source) === 'Boolean' || hasBitCapability(source)
    : sourceValueType(source) === 'Number');
  const source = sources.find(item => item.target === sourceTarget) ?? sources[0];
  const bitCapability = kind === 'Direct' && source && sourceValueType(source) !== 'Boolean' && hasBitCapability(source)
    ? source.selectorCapability!
    : null;

  return <div className="dynamic-property-editor__panel">
    <label><span>Condition preset</span><select value={kind} onChange={event => { setKind(event.currentTarget.value as typeof kind); setSourceTarget(''); }}>
      <option value="Direct">Direct Boolean</option><option value="NumericInterval">Numeric interval</option>
    </select></label>
    <SourceSelect sources={sources} source={source} onChange={setSourceTarget} />
    {bitCapability ? <BitIndexControl capability={bitCapability} value={bitIndex} onChange={setBitIndex} /> : null}
    {kind === 'NumericInterval' ? <div className="dynamic-property-editor__grid">
      <label><span>Minimum</span><input type="number" value={minimum} onChange={event => setMinimum(event.currentTarget.value)} placeholder="optional" /></label>
      <label><span>Maximum</span><input type="number" value={maximum} onChange={event => setMaximum(event.currentTarget.value)} placeholder="optional" /></label>
      <Check label="Minimum inclusive" checked={minimumInclusive} onChange={setMinimumInclusive} />
      <Check label="Maximum inclusive" checked={maximumInclusive} onChange={setMaximumInclusive} />
      <Check label="Outside interval" checked={outside} onChange={setOutside} />
    </div> : null}
    <Check label="Negate result" checked={negate} onChange={setNegate} />
    <button type="button" disabled={!source} onClick={() => {
      if (!source) return;
      try {
        const effectiveSource = bitCapability ? createTagBitBindingSource(source, bitIndex) : source;
        const valueType = kind === 'Direct' ? 'Boolean' : 'Number';
        const valueSource = createValueSource(valueType, effectiveSource);
        const condition = kind === 'Direct'
          ? createDirectBooleanCondition(destination.propertyKey, valueSource, negate)
          : createNumericIntervalCondition(destination.propertyKey, valueSource, {
              minimum: optionalNumber(minimum), maximum: optionalNumber(maximum),
              minimumInclusive, maximumInclusive,
              intervalMode: outside ? 'Outside' : 'Inside', negate
            });
        removeBindingIfPossible(element, destination.propertyKey, onBindingIntent);
        onRemoveExpression(destination.propertyKey);
        onSetBooleanCondition(condition);
        setError(null);
      } catch (reason) { setError(errorText(reason)); }
    }}>Apply condition</button>
    {error ? <ErrorText message={error} /> : null}
  </div>;
}

function ExpressionMode({
  destination,
  element,
  sourceCatalog,
  onBindingIntent,
  onRemoveBooleanCondition,
  onSetExpression
}: DestinationProps) {
  const existing = element.propertyExpressions?.find(item => item.propertyKey === destination.propertyKey)?.expression;
  const [draft, setDraft] = useState<ExpressionDraft>(() => existing
    ? Object.freeze({ text: existing.text, dependencies: Object.freeze([...(existing.dependencies ?? [])]) })
    : EMPTY_EXPRESSION);
  const [sourceTarget, setSourceTarget] = useState('');
  const [bitIndex, setBitIndex] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const sources = sourceCatalog.filter(source => sourceValueType(source) !== null || hasBitCapability(source));
  const source = sources.find(item => item.target === sourceTarget) ?? sources[0];
  const bitCapability = source && hasBitCapability(source) ? source.selectorCapability! : null;
  const resultType = destination.propertyType === 'boolean' ? 'Boolean' : 'Number';
  const validation = validateVisualExpressionAuthoring(resultType, draft.text, draft.dependencies);

  const insert = (asBit: boolean) => {
    if (!source) return;
    try {
      const effectiveSource = asBit
        ? createTagBitBindingSource(source, bitIndex)
        : source;
      const valueType = sourceValueType(effectiveSource);
      if (!valueType) throw new Error(`Source '${effectiveSource.target}' is not Boolean or numeric.`);
      const symbol = uniqueSymbol(symbolForSource(effectiveSource), draft.dependencies);
      const dependency = createExpressionDependency(symbol, valueType, effectiveSource);
      setDraft(current => Object.freeze({
        text: current.text ? `${current.text} ${symbol}` : symbol,
        dependencies: Object.freeze([...current.dependencies, dependency])
      }));
      setError(null);
    } catch (reason) { setError(errorText(reason)); }
  };

  return <div className="dynamic-property-editor__panel">
    <label><span>Expression</span><textarea rows={4} value={draft.text}
      onChange={event => setDraft(current => Object.freeze({ ...current, text: event.currentTarget.value }))}
      placeholder={resultType === 'Boolean' ? 'fault or level > 80' : '(level1 + level2) * 3'} /></label>
    <div className="dynamic-property-editor__insert">
      <select value={source?.target ?? ''} onChange={event => setSourceTarget(event.currentTarget.value)}>
        {sources.map(item => <option key={`${item.kind}:${item.target}`} value={item.target}>{item.label} · {item.dataType ?? 'unknown'}</option>)}
      </select>
      <button type="button" disabled={!source} onClick={() => insert(false)}>Insert source</button>
    </div>
    {bitCapability ? <div className="dynamic-property-editor__insert">
      <BitIndexControl capability={bitCapability} value={bitIndex} onChange={setBitIndex} />
      <button type="button" onClick={() => insert(true)}>Insert selected bit</button>
    </div> : null}
    <div className="dynamic-property-editor__dependencies">
      {draft.dependencies.map(item => <code key={`${item.symbol}:${item.target ?? item.tagReference.tagId}`}>{item.symbol} → {item.target ?? item.tagReference.tagId}</code>)}
    </div>
    {!validation.ok && draft.text.trim() ? <ErrorText message={validation.diagnostics[0]?.message ?? 'Expression is invalid.'} /> : null}
    {error ? <ErrorText message={error} /> : null}
    <button type="button" disabled={!draft.text.trim() || !validation.ok} onClick={() => {
      try {
        const expression = createVisualExpressionEngineering(resultType, draft.text, draft.dependencies);
        removeBindingIfPossible(element, destination.propertyKey, onBindingIntent);
        onRemoveBooleanCondition(destination.propertyKey);
        onSetExpression(Object.freeze({ propertyKey: destination.propertyKey, expression, version: 1 }));
        setError(null);
      } catch (reason) { setError(errorText(reason)); }
    }}>Apply expression</button>
  </div>;
}

function AnalogFillMode({ element, sourceCatalog, onSetAnalogFill, onRemoveAnalogFill }: DynamicPropertyEditorProps) {
  const numericSources = sourceCatalog.filter(source => sourceValueType(source) === 'Number');
  const existing = element.analogFill;
  const [enabled, setEnabled] = useState(Boolean(existing));
  const [sourceTarget, setSourceTarget] = useState(existing?.source.target ?? numericSources[0]?.target ?? '');
  const [minimum, setMinimum] = useState(String(existing?.inputMinimum ?? 0));
  const [maximum, setMaximum] = useState(String(existing?.inputMaximum ?? 100));
  const [fillColor, setFillColor] = useState(existing?.fillColor ?? '#00AAFF');
  const [direction, setDirection] = useState(existing?.direction ?? 'BottomToTop');
  const [clamp, setClamp] = useState(existing?.clamp ?? true);
  const [invertScale, setInvertScale] = useState(existing?.invertScale ?? false);
  const [error, setError] = useState<string | null>(null);
  const source = numericSources.find(item => item.target === sourceTarget) ?? numericSources[0];

  return <fieldset className="dynamic-property-editor__analog-fill"><legend>Analog Fill</legend>
    <Check label="Enabled" checked={enabled} onChange={next => { setEnabled(next); if (!next) onRemoveAnalogFill(); }} />
    {enabled ? <>
      <SourceSelect sources={numericSources} source={source} onChange={setSourceTarget} />
      <div className="dynamic-property-editor__grid">
        <label><span>Input minimum</span><input type="number" value={minimum} onChange={event => setMinimum(event.currentTarget.value)} /></label>
        <label><span>Input maximum</span><input type="number" value={maximum} onChange={event => setMaximum(event.currentTarget.value)} /></label>
        <label><span>Fill color</span><input type="text" value={fillColor} onChange={event => setFillColor(event.currentTarget.value)} /></label>
        <label><span>Direction</span><select value={direction} onChange={event => setDirection(event.currentTarget.value as typeof direction)}>
          <option value="BottomToTop">Bottom to top</option><option value="TopToBottom">Top to bottom</option>
          <option value="LeftToRight">Left to right</option><option value="RightToLeft">Right to left</option>
        </select></label>
      </div>
      <Check label="Clamp to 0..100%" checked={clamp} onChange={setClamp} />
      <Check label="Invert scale" checked={invertScale} onChange={setInvertScale} />
      <button type="button" disabled={!source} onClick={() => {
        if (!source) return;
        try {
          onSetAnalogFill(createAnalogFillEngineering(createValueSource('Number', source), {
            inputMinimum: requiredNumber(minimum, 'Input minimum'),
            inputMaximum: requiredNumber(maximum, 'Input maximum'),
            fillColor, direction, clamp, invertScale
          }));
          setError(null);
        } catch (reason) { setError(errorText(reason)); }
      }}>Apply Analog Fill</button>
      {error ? <ErrorText message={error} /> : null}
    </> : null}
  </fieldset>;
}

function SourceSelect({
  sources,
  source,
  onChange
}: Readonly<{
  sources: readonly VisualEditorBindingSourceCatalogItem[];
  source: VisualEditorBindingSourceCatalogItem | undefined;
  onChange: (target: string) => void;
}>) {
  return <label><span>Canonical source</span><select value={source?.target ?? ''} onChange={event => onChange(event.currentTarget.value)}>
    {sources.map(item => <option key={`${item.kind}:${item.target}`} value={item.target}>{item.label} · {item.dataType ?? 'unknown'}</option>)}
  </select></label>;
}

function BitIndexControl({
  capability,
  value,
  onChange
}: Readonly<{
  capability: Readonly<{ minIndex: number; maxIndex: number }>;
  value: number;
  onChange: (value: number) => void;
}>) {
  return <label><span>Bit index ({capability.minIndex}..{capability.maxIndex})</span><input type="number"
    min={capability.minIndex} max={capability.maxIndex} step={1} value={value}
    onChange={event => onChange(Number(event.currentTarget.value))} /></label>;
}

function Check({ label, checked, onChange }: Readonly<{ label: string; checked: boolean; onChange: (value: boolean) => void }>) {
  return <label className="dynamic-property-editor__check"><input type="checkbox" checked={checked} onChange={event => onChange(event.currentTarget.checked)} /><span>{label}</span></label>;
}

function ErrorText({ message }: Readonly<{ message: string }>) {
  return <p className="dynamic-property-editor__error" role="alert">{message}</p>;
}

function removeBindingIfPossible(
  element: VisualElementEngineering,
  propertyKey: string,
  emit: DynamicPropertyEditorProps['onBindingIntent']
) {
  if (element.id) emit(createBindingRemoveIntent(element, propertyKey));
}

function effectiveMode(element: VisualElementEngineering, propertyKey: string): DynamicPropertySourceMode {
  if (element.booleanConditions?.some(item => item.propertyKey === propertyKey)) return 'BooleanCondition';
  if (element.propertyExpressions?.some(item => item.propertyKey === propertyKey)) return 'Expression';
  if (element.bindings?.some(item => item.key === propertyKey)) return 'DirectBinding';
  return 'Constant';
}

function supportsAnalogFill(objectType: string): boolean {
  return objectType === 'core.rectangle' || objectType === 'core.ellipse';
}

function hasBitCapability(source: VisualEditorBindingSourceCatalogItem): boolean {
  return source.kind === 'Tag' && source.selectorCapability?.kind === 'bit' && Boolean(source.tagReference?.tagId);
}

function sourceValueType(source: VisualEditorBindingSourceCatalogItem): 'Boolean' | 'Number' | null {
  const dataType = source.dataType?.trim().toLowerCase();
  if (dataType === 'boolean') return 'Boolean';
  if (dataType && ['int16', 'int32', 'int64', 'float', 'double'].includes(dataType)) return 'Number';
  return null;
}

function symbolForSource(source: VisualEditorBindingSourceCatalogItem): string {
  const raw = source.target.split(/[./:]/).filter(Boolean).pop() ?? 'source';
  const normalized = raw.replace(/[^A-Za-z0-9_]/g, '_').replace(/^([0-9])/, '_$1');
  return normalized || 'source';
}

function uniqueSymbol(base: string, dependencies: readonly VisualExpressionDependencyEngineering[]): string {
  const used = new Set(dependencies.map(item => item.symbol.toLocaleLowerCase()));
  if (!used.has(base.toLocaleLowerCase())) return base;
  let index = 2;
  while (used.has(`${base}_${index}`.toLocaleLowerCase())) index += 1;
  return `${base}_${index}`;
}

function optionalNumber(value: string): number | null {
  return value.trim() ? requiredNumber(value, 'Interval bound') : null;
}

function requiredNumber(value: string, label: string): number {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) throw new Error(`${label} must be a finite number.`);
  return parsed;
}

function modeLabel(mode: DynamicPropertySourceMode): string {
  switch (mode) {
    case 'Constant': return 'Engineering constant';
    case 'DirectBinding': return 'Direct binding / TAG bit';
    case 'BooleanCondition': return 'Boolean condition';
    case 'Expression': return 'Typed expression';
  }
}

function errorText(reason: unknown): string {
  return reason instanceof Error ? reason.message : String(reason);
}

export default DynamicPropertyEditor;
