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
  createBindingRemoveIntent,
  createBindingSetIntent,
  compatibleBindingSources
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
 * Transient authoring surface for FOLLOW-B dynamic visual configuration.
 *
 * This component never becomes canonical state: Apply callbacks emit only the
 * public Engineering DTOs already owned by the shared contract/reducer boundary.
 */
export function DynamicPropertyEditor({
  element,
  sourceCatalog,
  onBindingIntent,
  onSetExpression,
  onRemoveExpression,
  onSetBooleanCondition,
  onRemoveBooleanCondition,
  onSetAnalogFill,
  onRemoveAnalogFill
}: DynamicPropertyEditorProps) {
  const destinations = useMemo(() => listDynamicPropertyDestinations(element), [element.type]);
  const [propertyKey, setPropertyKey] = useState(() => destinations[0]?.propertyKey ?? '');
  const destination = destinations.find(item => item.propertyKey === propertyKey) ?? destinations[0] ?? null;
  const currentMode = destination ? effectiveMode(element, destination.propertyKey) : 'Constant';
  const [mode, setMode] = useState<DynamicPropertySourceMode>(currentMode);

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
    <header>
      <strong>Dynamic source</strong>
      <span>Canonical Binding/Expression configuration</span>
    </header>

    <label>
      <span>Visual property</span>
      <select value={destination.propertyKey} onChange={event => selectProperty(event.currentTarget.value)}>
        {destinations.map(item => <option key={item.propertyKey} value={item.propertyKey}>{item.propertyKey} · {item.propertyType}</option>)}
      </select>
    </label>

    <label>
      <span>Source mode</span>
      <select value={mode} onChange={event => setMode(event.currentTarget.value as DynamicPropertySourceMode)}>
        {destination.sourceModes.map(item => <option key={item} value={item}>{modeLabel(item)}</option>)}
      </select>
    </label>

    {mode === 'Constant' ? <ConstantMode
      destination={destination}
      element={element}
      onBindingIntent={onBindingIntent}
      onRemoveExpression={onRemoveExpression}
      onRemoveBooleanCondition={onRemoveBooleanCondition}
    /> : null}

    {mode === 'DirectBinding' ? <DirectBindingMode
      key={`binding:${destination.propertyKey}`}
      destination={destination}
      element={element}
      sourceCatalog={sourceCatalog}
      onBindingIntent={onBindingIntent}
      onRemoveExpression={onRemoveExpression}
      onRemoveBooleanCondition={onRemoveBooleanCondition}
    /> : null}

    {mode === 'BooleanCondition' && destination.propertyType === 'boolean' ? <BooleanConditionMode
      key={`condition:${destination.propertyKey}`}
      destination={destination}
      sourceCatalog={sourceCatalog}
      onSetBooleanCondition={onSetBooleanCondition}
      onBindingIntent={onBindingIntent}
      element={element}
      onRemoveExpression={onRemoveExpression}
    /> : null}

    {mode === 'Expression' ? <ExpressionMode
      key={`expression:${destination.propertyKey}`}
      destination={destination}
      sourceCatalog={sourceCatalog}
      onSetExpression={onSetExpression}
      onBindingIntent={onBindingIntent}
      element={element}
      onRemoveBooleanCondition={onRemoveBooleanCondition}
    /> : null}

    {supportsAnalogFill(element.type) ? <AnalogFillMode
      key={`analog-fill:${element.id ?? element.key}`}
      element={element}
      sourceCatalog={sourceCatalog}
      onSetAnalogFill={onSetAnalogFill}
      onRemoveAnalogFill={onRemoveAnalogFill}
    /> : null}
  </section>;
}

function ConstantMode({
  destination,
  element,
  onBindingIntent,
  onRemoveExpression,
  onRemoveBooleanCondition
}: Readonly<{
  destination: DynamicPropertyDestination;
  element: VisualElementEngineering;
  onBindingIntent: DynamicPropertyEditorProps['onBindingIntent'];
  onRemoveExpression: DynamicPropertyEditorProps['onRemoveExpression'];
  onRemoveBooleanCondition: DynamicPropertyEditorProps['onRemoveBooleanCondition'];
}>) {
  return <div className="dynamic-property-editor__panel">
    <p>Engineering/default value remains authoritative for this property.</p>
    <button type="button" onClick={() => {
      if (element.id) onBindingIntent(createBindingRemoveIntent(element, destination.propertyKey));
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
}: Readonly<{
  destination: DynamicPropertyDestination;
  element: VisualElementEngineering;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  onBindingIntent: DynamicPropertyEditorProps['onBindingIntent'];
  onRemoveExpression: DynamicPropertyEditorProps['onRemoveExpression'];
  onRemoveBooleanCondition: DynamicPropertyEditorProps['onRemoveBooleanCondition'];
}>) {
  const sources = useMemo(
    () => compatibleBindingSources({ key: destination.propertyKey, type: destination.propertyType }, sourceCatalog),
    [destination.propertyKey, destination.propertyType, sourceCatalog]
  );
  const [selected, setSelected] = useState(sources[0]?.target ?? '');
  const [error, setError] = useState<string | null>(null);
  const source = sources.find(item => item.target === selected) ?? sources[0];

  return <div className="dynamic-property-editor__panel">
    <label><span>Canonical source</span><select value={source?.target ?? ''} onChange={event => setSelected(event.currentTarget.value)}>
      {sources.map(item => <option key={`${item.kind}:${item.target}`} value={item.target}>{item.label} · {item.dataType ?? 'unknown'}</option>)}
    </select></label>
    <button type="button" disabled={!source || !element.id} onClick={() => {
      if (!source) return;
      try {
        onBindingIntent(createBindingSetIntent(element, destination.propertyKey, source));
        onRemoveExpression(destination.propertyKey);
        onRemoveBooleanCondition(destination.propertyKey);
        setError(null);
      } catch (reason) {
        setError(errorText(reason));
      }
    }}>Apply direct binding</button>
    {sources.length === 0 ? <p className="dynamic-property-editor__warning">No compatible canonical source is available.</p> : null}
    {error ? <p className="dynamic-property-editor__error" role="alert">{error}</p> : null}
  </div>;
}

function BooleanConditionMode({
  destination,
  sourceCatalog,
  onSetBooleanCondition,
  onBindingIntent,
  element,
  onRemoveExpression
}: Readonly<{
  destination: DynamicPropertyDestination;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  onSetBooleanCondition: DynamicPropertyEditorProps['onSetBooleanCondition'];
  onBindingIntent: DynamicPropertyEditorProps['onBindingIntent'];
  element: VisualElementEngineering;
  onRemoveExpression: DynamicPropertyEditorProps['onRemoveExpression'];
}>) {
  const [conditionKind, setConditionKind] = useState<'Direct' | 'NumericInterval'>('Direct');
  const [sourceTarget, setSourceTarget] = useState('');
  const [minimum, setMinimum] = useState('');
  const [maximum, setMaximum] = useState('');
  const [minimumInclusive, setMinimumInclusive] = useState(true);
  const [maximumInclusive, setMaximumInclusive] = useState(true);
  const [outside, setOutside] = useState(false);
  const [negate, setNegate] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const wantedType = conditionKind === 'Direct' ? 'Boolean' : 'Number';
  const sources = sourceCatalog.filter(source => sourceValueType(source) === wantedType);
  const source = sources.find(item => item.target === sourceTarget) ?? sources[0];

  return <div className="dynamic-property-editor__panel">
    <label><span>Condition preset</span><select value={conditionKind} onChange={event => {
      setConditionKind(event.currentTarget.value as 'Direct' | 'NumericInterval');
      setSourceTarget('');
    }}>
      <option value="Direct">Direct Boolean</option>
      <option value="NumericInterval">Numeric interval</option>
    </select></label>
    <label><span>Canonical source</span><select value={source?.target ?? ''} onChange={event => setSourceTarget(event.currentTarget.value)}>
      {sources.map(item => <option key={`${item.kind}:${item.target}`} value={item.target}>{item.label} · {item.dataType ?? 'unknown'}</option>)}
    </select></label>

    {conditionKind === 'NumericInterval' ? <div className="dynamic-property-editor__grid">
      <label><span>Minimum</span><input type="number" value={minimum} onChange={event => setMinimum(event.currentTarget.value)} placeholder="optional" /></label>
      <label><span>Maximum</span><input type="number" value={maximum} onChange={event => setMaximum(event.currentTarget.value)} placeholder="optional" /></label>
      <label className="dynamic-property-editor__check"><input type="checkbox" checked={minimumInclusive} onChange={event => setMinimumInclusive(event.currentTarget.checked)} /><span>Minimum inclusive</span></label>
      <label className="dynamic-property-editor__check"><input type="checkbox" checked={maximumInclusive} onChange={event => setMaximumInclusive(event.currentTarget.checked)} /><span>Maximum inclusive</span></label>
      <label className="dynamic-property-editor__check"><input type="checkbox" checked={outside} onChange={event => setOutside(event.currentTarget.checked)} /><span>Outside interval</span></label>
    </div> : null}

    <label className="dynamic-property-editor__check"><input type="checkbox" checked={negate} onChange={event => setNegate(event.currentTarget.checked)} /><span>Negate result</span></label>
    <button type="button" disabled={!source} onClick={() => {
      if (!source) return;
      try {
        const valueSource = createValueSource(wantedType, source);
        const condition = conditionKind === 'Direct'
          ? createDirectBooleanCondition(destination.propertyKey, valueSource, negate)
          : createNumericIntervalCondition(destination.propertyKey, valueSource, {
              minimum: optionalNumber(minimum),
              maximum: optionalNumber(maximum),
              minimumInclusive,
              maximumInclusive,
              intervalMode: outside ? 'Outside' : 'Inside',
              negate
            });
        if (element.id) onBindingIntent(createBindingRemoveIntent(element, destination.propertyKey));
        onRemoveExpression(destination.propertyKey);
        onSetBooleanCondition(condition);
        setError(null);
      } catch (reason) {
        setError(errorText(reason));
      }
    }}>Apply condition</button>
    {error ? <p className="dynamic-property-editor__error" role="alert">{error}</p> : null}
  </div>;
}

function ExpressionMode({
  destination,
  sourceCatalog,
  onSetExpression,
  onBindingIntent,
  element,
  onRemoveBooleanCondition
}: Readonly<{
  destination: DynamicPropertyDestination;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  onSetExpression: DynamicPropertyEditorProps['onSetExpression'];
  onBindingIntent: DynamicPropertyEditorProps['onBindingIntent'];
  element: VisualElementEngineering;
  onRemoveBooleanCondition: DynamicPropertyEditorProps['onRemoveBooleanCondition'];
}>) {
  const existing = element.propertyExpressions?.find(item => item.propertyKey === destination.propertyKey)?.expression;
  const [draft, setDraft] = useState<ExpressionDraft>(() => existing
    ? Object.freeze({ text: existing.text, dependencies: Object.freeze([...(existing.dependencies ?? [])]) })
    : EMPTY_EXPRESSION);
  const [sourceTarget, setSourceTarget] = useState('');
  const [error, setError] = useState<string | null>(null);
  const source = sourceCatalog.find(item => item.target === sourceTarget) ?? sourceCatalog[0];
  const resultType = destination.propertyType === 'boolean' ? 'Boolean' : 'Number';
  const validation = validateVisualExpressionAuthoring(resultType, draft.text, draft.dependencies);

  const insertSource = () => {
    if (!source) return;
    try {
      const valueType = sourceValueType(source);
      if (!valueType) throw new Error(`Source '${source.target}' is not Boolean or numeric.`);
      const symbol = uniqueSymbol(symbolForSource(source), draft.dependencies);
      const dependency = createExpressionDependency(symbol, valueType, source);
      setDraft(current => Object.freeze({
        text: current.text ? `${current.text} ${symbol}` : symbol,
        dependencies: Object.freeze([...current.dependencies, dependency])
      }));
      setError(null);
    } catch (reason) {
      setError(errorText(reason));
    }
  };

  return <div className="dynamic-property-editor__panel">
    <label><span>Expression</span><textarea rows={4} value={draft.text} onChange={event => setDraft(current => Object.freeze({ ...current, text: event.currentTarget.value }))} placeholder={resultType === 'Boolean' ? 'fault or level > 80' : '(level1 + level2) * 3'} /></label>
    <div className="dynamic-property-editor__insert">
      <select value={source?.target ?? ''} onChange={event => setSourceTarget(event.currentTarget.value)}>
        {sourceCatalog.map(item => <option key={`${item.kind}:${item.target}`} value={item.target}>{item.label} · {item.dataType ?? 'unknown'}</option>)}
      </select>
      <button type="button" disabled={!source} onClick={insertSource}>Insert source</button>
    </div>
    <div className="dynamic-property-editor__dependencies">
      {draft.dependencies.map(item => <code key={`${item.symbol}:${item.target ?? item.tagReference.tagId}`}>{item.symbol} → {item.target ?? item.tagReference.tagId}</code>)}
    </div>
    {!validation.ok && draft.text.trim() ? <p className="dynamic-property-editor__error" role="alert">{validation.diagnostics[0]?.message ?? 'Expression is invalid.'}</p> : null}
    {error ? <p className="dynamic-property-editor__error" role="alert">{error}</p> : null}
    <button type="button" disabled={!draft.text.trim() || !validation.ok} onClick={() => {
      try {
        const expression = createVisualExpressionEngineering(resultType, draft.text, draft.dependencies);
        if (element.id) onBindingIntent(createBindingRemoveIntent(element, destination.propertyKey));
        onRemoveBooleanCondition(destination.propertyKey);
        onSetExpression(Object.freeze({ propertyKey: destination.propertyKey, expression, version: 1 }));
        setError(null);
      } catch (reason) {
        setError(errorText(reason));
      }
    }}>Apply expression</button>
  </div>;
}

function AnalogFillMode({
  element,
  sourceCatalog,
  onSetAnalogFill,
  onRemoveAnalogFill
}: Readonly<{
  element: VisualElementEngineering;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  onSetAnalogFill: DynamicPropertyEditorProps['onSetAnalogFill'];
  onRemoveAnalogFill: DynamicPropertyEditorProps['onRemoveAnalogFill'];
}>) {
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

  return <fieldset className="dynamic-property-editor__analog-fill">
    <legend>Analog Fill</legend>
    <label className="dynamic-property-editor__check"><input type="checkbox" checked={enabled} onChange={event => {
      setEnabled(event.currentTarget.checked);
      if (!event.currentTarget.checked) onRemoveAnalogFill();
    }} /><span>Enabled</span></label>
    {enabled ? <>
      <label><span>Numeric source</span><select value={source?.target ?? ''} onChange={event => setSourceTarget(event.currentTarget.value)}>
        {numericSources.map(item => <option key={`${item.kind}:${item.target}`} value={item.target}>{item.label}</option>)}
      </select></label>
      <div className="dynamic-property-editor__grid">
        <label><span>Input minimum</span><input type="number" value={minimum} onChange={event => setMinimum(event.currentTarget.value)} /></label>
        <label><span>Input maximum</span><input type="number" value={maximum} onChange={event => setMaximum(event.currentTarget.value)} /></label>
        <label><span>Fill color</span><input type="text" value={fillColor} onChange={event => setFillColor(event.currentTarget.value)} /></label>
        <label><span>Direction</span><select value={direction} onChange={event => setDirection(event.currentTarget.value as typeof direction)}>
          <option value="BottomToTop">Bottom to top</option>
          <option value="TopToBottom">Top to bottom</option>
          <option value="LeftToRight">Left to right</option>
          <option value="RightToLeft">Right to left</option>
        </select></label>
      </div>
      <label className="dynamic-property-editor__check"><input type="checkbox" checked={clamp} onChange={event => setClamp(event.currentTarget.checked)} /><span>Clamp to 0..100%</span></label>
      <label className="dynamic-property-editor__check"><input type="checkbox" checked={invertScale} onChange={event => setInvertScale(event.currentTarget.checked)} /><span>Invert scale</span></label>
      <button type="button" disabled={!source} onClick={() => {
        if (!source) return;
        try {
          const valueSource = createValueSource('Number', source);
          onSetAnalogFill(createAnalogFillEngineering(valueSource, {
            inputMinimum: requiredNumber(minimum, 'Input minimum'),
            inputMaximum: requiredNumber(maximum, 'Input maximum'),
            fillColor,
            direction,
            clamp,
            invertScale
          }));
          setError(null);
        } catch (reason) {
          setError(errorText(reason));
        }
      }}>Apply Analog Fill</button>
      {error ? <p className="dynamic-property-editor__error" role="alert">{error}</p> : null}
    </> : null}
  </fieldset>;
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
