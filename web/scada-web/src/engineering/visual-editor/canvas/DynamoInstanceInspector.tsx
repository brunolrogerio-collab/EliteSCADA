import React, { useEffect, useMemo, useState } from 'react';
import type {
  CommandEngineering,
  DynamoEngineering,
  ScreenEngineering,
  TagEngineering,
  VisualElementEngineering,
  VisualValueSourceEngineering
} from '../../types';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterValueEngineering
} from '../../../runtime/visual-navigation/runtimeVisualNavigationModel';
import { useC07VisualEditorText } from '../c07VisualEditorI18n';
import { useDynamoAuthoringCatalog } from '../DynamoAuthoringCatalogContext';
import { isVisualElementEffectivelyAuthoringLocked } from '../visualEditorAuthoringModel';
import type { VisualEditorKeyboardCommand } from '../visualEditorKeyboardModel';
import {
  listDynamoPublicParameters,
  listDynamoPublicParameterValues,
  resolveDynamoParameterEditorKind
} from '../dynamo/dynamoPublicInterfaceModel';
import {
  resolveDynamoVisualState,
  type DynamoCommandIntent,
  type DynamoQualityState,
  type DynamoSettledState
} from '../dynamo/dynamoStateModel';
import './DynamoInstanceInspector.css';

export function DynamoInstanceInspector({
  screen,
  selectedObjectIds,
  onCommand
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const catalog = useDynamoAuthoringCatalog();
  if (selectedObjectIds.length !== 1) return null;
  const instance = findElement(screen.elements ?? [], selectedObjectIds[0]);
  if (!instance?.id || !instance.dynamoKey?.trim()) return null;

  const definition = catalog.definitions.find(item => equalsKey(item.key, instance.dynamoKey!));
  if (!definition) {
    return <details className="visual-editor-dynamo-inspector" open data-testid="dynamo-instance-inspector">
      <summary><strong>{text.name}</strong><code>{instance.dynamoKey}</code></summary>
      <p className="visual-editor-dynamo-inspector__error">{text.definitionNotFound}</p>
    </details>;
  }

  return <DynamoInspectorBody
    screen={screen}
    instance={instance}
    definition={definition}
    tags={catalog.tags}
    commands={catalog.commands}
    onCommand={onCommand}
  />;
}

function DynamoInspectorBody({
  screen,
  instance,
  definition,
  tags,
  commands,
  onCommand
}: {
  screen: ScreenEngineering;
  instance: VisualElementEngineering & { id: string };
  definition: DynamoEngineering;
  tags: readonly TagEngineering[];
  commands: readonly CommandEngineering[];
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const parameters = useMemo(() => listDynamoPublicParameters(definition), [definition]);
  const values = useMemo(() => new Map(
    listDynamoPublicParameterValues(instance, definition)
      .map(value => [normalizeKey(value.key), value] as const)
  ), [instance, definition]);
  const locked = isVisualElementEffectivelyAuthoringLocked(screen, instance.id);
  const tagOptions = useMemo(
    () => tags.filter(tag => Boolean(tag.id?.trim())).sort((a, b) =>
      (a.path || a.name).localeCompare(b.path || b.name)),
    [tags]
  );

  const setValue = (value: DynamoParameterValueEngineering) => onCommand?.({
    kind: 'dynamoParameter.set',
    objectId: instance.id,
    definition,
    value
  });
  const removeValue = (parameterKey: string) => onCommand?.({
    kind: 'dynamoParameter.remove',
    objectId: instance.id,
    definition,
    parameterKey
  });

  return <details className="visual-editor-dynamo-inspector" open data-testid="dynamo-instance-inspector">
    <summary>
      <span><strong>{definition.name}</strong><code>{definition.key}</code></span>
      <small>{locked ? text.locked : `${parameters.length} ${text.publicSuffix}`}</small>
    </summary>
    <div className="visual-editor-dynamo-inspector__body">
      <div className="visual-editor-dynamo-inspector__identity">
        <span>{text.instance}</span><code>{instance.key}</code>
      </div>
      <DynamoStatePreview />
      {parameters.length === 0 ? <p className="visual-editor-dynamo-inspector__empty">{text.noPublicParameters}</p> : parameters.map(parameter => {
        const value = values.get(normalizeKey(parameter.key));
        return <ParameterEditor
          key={parameter.key}
          parameter={parameter}
          value={value}
          instance={instance}
          tags={tagOptions}
          commands={commands}
          disabled={locked || !onCommand}
          onSet={setValue}
          onRemove={() => removeValue(parameter.key)}
        />;
      })}
    </div>
  </details>;
}

function DynamoStatePreview() {
  const copy = useC07VisualEditorText();
  const text = copy.dynamo;
  const [quality, setQuality] = useState<DynamoQualityState>('good');
  const [fault, setFault] = useState(false);
  const [alarm, setAlarm] = useState(false);
  const [commandIntent, setCommandIntent] = useState<DynamoCommandIntent>(null);
  const [settledState, setSettledState] = useState<DynamoSettledState>('inactive');
  const resolved = resolveDynamoVisualState({ quality, fault, alarm, commandIntent, settledState });

  return <section className="visual-editor-dynamo-state-preview" data-testid="dynamo-engineering-state-preview">
    <header><strong>{text.statePreview}</strong><span data-state={resolved.kind}>{dynamoStateLabel(resolved.kind, copy.runtimeState)}</span></header>
    <div className="visual-editor-dynamo-state-preview__grid">
      <label><span>{text.quality}</span><select value={quality} onChange={event => setQuality(event.currentTarget.value as DynamoQualityState)}>
        <option value="good">{text.good}</option><option value="uncertain">{text.uncertain}</option><option value="bad">{text.bad}</option><option value="stale">{text.stale}</option><option value="unknown">{text.unknown}</option>
      </select></label>
      <label><span>{text.settled}</span><select value={settledState} onChange={event => setSettledState(event.currentTarget.value as DynamoSettledState)}>
        <option value="inactive">{text.inactive}</option><option value="active">{text.active}</option><option value="transitioning">{text.transitioning}</option><option value="unknown">{text.unknown}</option>
      </select></label>
      <label><span>{text.command}</span><select value={commandIntent ?? ''} onChange={event => setCommandIntent((event.currentTarget.value || null) as DynamoCommandIntent)}>
        <option value="">{text.none}</option><option value="start">{text.start}</option><option value="stop">{text.stop}</option><option value="open">{text.open}</option><option value="close">{text.close}</option><option value="increase">{text.increase}</option><option value="decrease">{text.decrease}</option><option value="setpoint">{text.setpoint}</option>
      </select></label>
      <div className="visual-editor-dynamo-state-preview__checks">
        <label><input type="checkbox" checked={fault} onChange={event => setFault(event.currentTarget.checked)} />{text.fault}</label>
        <label><input type="checkbox" checked={alarm} onChange={event => setAlarm(event.currentTarget.checked)} />{text.alarm}</label>
      </div>
    </div>
    <footer><span>{text.resolvedPriority}</span><strong>{resolved.priority}</strong><small>{text.previewOnly}</small></footer>
  </section>;
}

function ParameterEditor({
  parameter,
  value,
  instance,
  tags,
  commands,
  disabled,
  onSet,
  onRemove
}: {
  parameter: DynamoParameterDefinitionEngineering;
  value: DynamoParameterValueEngineering | undefined;
  instance: VisualElementEngineering;
  tags: readonly TagEngineering[];
  commands: readonly CommandEngineering[];
  disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
  onRemove: () => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const editor = resolveDynamoParameterEditorKind(parameter.kind);
  const hasStoredValue = Boolean(
    instance.dynamoParameters?.some(item => equalsKey(item.key, parameter.key))
    || (parameter.kind === 'EquipmentPath' && instance.equipmentPath?.trim())
  );
  const requiredMissing = parameter.required === true && value === undefined
    && parameter.defaultValue === undefined && !parameter.defaultTagReference && !parameter.defaultValueSource;
  const removeAllowed = hasStoredValue && parameter.required !== true && !disabled;

  return <div className={`visual-editor-dynamo-parameter${requiredMissing ? ' is-required-missing' : ''}`}>
    <header>
      <span><strong>{parameter.key}</strong>{parameter.required ? <sup>*</sup> : null}</span>
      <code>{parameter.kind}</code>
    </header>
    {editor === 'boolean' ? <label className="visual-editor-dynamo-parameter__boolean">
      <input
        type="checkbox"
        checked={booleanValue(value?.value ?? parameter.defaultValue)}
        disabled={disabled}
        onChange={event => onSet({
          key: parameter.key,
          kind: parameter.kind,
          value: event.currentTarget.checked,
          version: parameter.version
        })}
      />
      <span>{booleanValue(value?.value ?? parameter.defaultValue) ? text.trueValue : text.falseValue}</span>
    </label> : editor === 'tag-reference' ? <TagParameterEditor
      parameter={parameter}
      value={value}
      tags={tags}
      disabled={disabled}
      onSet={onSet}
      onRemove={onRemove}
    /> : editor === 'value-source' ? <ValueSourceParameterEditor
      parameter={parameter}
      value={value}
      tags={tags}
      disabled={disabled}
      onSet={onSet}
      onRemove={onRemove}
    /> : editor === 'command' ? <CommandParameterEditor
      parameter={parameter}
      value={value}
      commands={commands}
      disabled={disabled}
      onSet={onSet}
      onRemove={onRemove}
    /> : <ScalarParameterEditor
      parameter={parameter}
      value={value}
      disabled={disabled}
      onSet={onSet}
      onRemove={onRemove}
    />}
    <footer>
      {requiredMissing ? <span>{text.requiredMissing}</span> : <span>{hasStoredValue ? text.instanceValue : text.defaultUnset}</span>}
      {removeAllowed && editor !== 'tag-reference' ? <button type="button" onClick={onRemove}>{text.reset}</button> : null}
    </footer>
  </div>;
}

function ValueSourceParameterEditor({
  parameter,
  value,
  tags,
  disabled,
  onSet,
  onRemove
}: {
  parameter: DynamoParameterDefinitionEngineering;
  value: DynamoParameterValueEngineering | undefined;
  tags: readonly TagEngineering[];
  disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
  onRemove: () => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const source = value?.valueSource ?? parameter.defaultValueSource ?? undefined;
  const [mode, setMode] = useState<'Tag' | 'Expression'>(source?.kind === 'Expression' ? 'Expression' : 'Tag');
  const [tagId, setTagId] = useState(source?.kind === 'Expression'
    ? source.expression?.dependencies?.[0]?.tagReference.tagId ?? ''
    : source?.tagReference?.tagId ?? '');
  const [expressionText, setExpressionText] = useState(source?.expression?.text ?? 'source');
  const [resultType, setResultType] = useState<'Boolean' | 'Number'>(parameter.valueSourceType ?? source?.valueType ?? 'Boolean');
  const selectedTag = tags.find(tag => tag.id === tagId);

  useEffect(() => {
    const next = value?.valueSource ?? parameter.defaultValueSource ?? undefined;
    setMode(next?.kind === 'Expression' ? 'Expression' : 'Tag');
    setTagId(next?.kind === 'Expression'
      ? next.expression?.dependencies?.[0]?.tagReference.tagId ?? ''
      : next?.tagReference?.tagId ?? '');
    setExpressionText(next?.expression?.text ?? 'source');
    setResultType(parameter.valueSourceType ?? next?.valueType ?? 'Boolean');
  }, [parameter.key, parameter.defaultValueSource, value?.valueSource]);

  const save = () => {
    if (disabled || !selectedTag?.id) return;
    const valueType = parameter.valueSourceType ?? (mode === 'Expression' ? resultType : tagValueType(selectedTag.dataType));
    let valueSource: VisualValueSourceEngineering;
    if (mode === 'Expression') {
      valueSource = {
        kind: 'Expression',
        valueType,
        expression: {
          text: expressionText.trim() || 'source',
          resultType: valueType,
          dependencies: [{
            symbol: 'source',
            kind: 'Tag',
            valueType: tagValueType(selectedTag.dataType),
            tagReference: { tagId: selectedTag.id },
            target: selectedTag.path
          }]
        }
      };
    } else {
      valueSource = {
        kind: 'Tag',
        valueType,
        target: selectedTag.path,
        tagReference: { tagId: selectedTag.id }
      };
    }
    onSet({ key: parameter.key, kind: 'ValueSource', valueSource, version: parameter.version });
  };

  return <div className="visual-editor-dynamo-parameter__scalar">
    <label>
      <span>{text.valueSource}</span>
      <select value={mode} disabled={disabled} onChange={event => setMode(event.currentTarget.value as 'Tag' | 'Expression')}>
        <option value="Tag">TAG</option>
        <option value="Expression">{text.expression}</option>
      </select>
    </label>
    <select value={tagId} disabled={disabled} onChange={event => setTagId(event.currentTarget.value)}>
      <option value="">{text.selectTag}</option>
      {tags.map(tag => <option key={tag.id!} value={tag.id!}>{tag.name} · {tag.path} · {tag.dataType}</option>)}
    </select>
    {mode === 'Expression' ? <>
      {parameter.valueSourceType ? <small>{text.sourceResult}: {parameter.valueSourceType}</small> : <label>
          <span>{text.sourceResult}</span>
          <select value={resultType} disabled={disabled} onChange={event => setResultType(event.currentTarget.value as 'Boolean' | 'Number')}>
            <option value="Boolean">Boolean</option>
            <option value="Number">Number</option>
          </select>
        </label>}
      <input value={expressionText} disabled={disabled} onChange={event => setExpressionText(event.currentTarget.value)} aria-label={text.expression} />
      <small>{text.expressionHint}</small>
    </> : null}
    <button type="button" className="secondary" disabled={disabled || !selectedTag || (mode === 'Tag' && parameter.valueSourceType != null && parameter.valueSourceType !== tagValueType(selectedTag.dataType))} onClick={save}>{text.valueSource}</button>
    {value?.valueSource && parameter.required !== true ? <button type="button" className="secondary" disabled={disabled} onClick={onRemove}>{text.reset}</button> : null}
  </div>;
}

function tagValueType(dataType: string): 'Boolean' | 'Number' {
  return /bool/i.test(dataType) ? 'Boolean' : 'Number';
}

function CommandParameterEditor({
  parameter,
  value,
  commands,
  disabled,
  onSet,
  onRemove
}: {
  parameter: DynamoParameterDefinitionEngineering;
  value: DynamoParameterValueEngineering | undefined;
  commands: readonly CommandEngineering[];
  disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
  onRemove: () => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const current = value?.commandId ?? '';
  return <select
    value={current}
    disabled={disabled}
    onChange={event => {
      const commandId = event.currentTarget.value;
      if (!commandId) {
        if (parameter.required !== true) onRemove();
        return;
      }
      onSet({
        key: parameter.key,
        kind: 'Command',
        commandId,
        version: parameter.version
      });
    }}
  >
    <option value="">{parameter.required ? text.selectCommand : text.notAssigned}</option>
    {commands.map(command => <option key={command.id!} value={command.id!}>
      {command.name} · {command.key}
    </option>)}
  </select>;
}

function ScalarParameterEditor({
  parameter,
  value,
  disabled,
  onSet,
  onRemove
}: {
  parameter: DynamoParameterDefinitionEngineering;
  value: DynamoParameterValueEngineering | undefined;
  disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
  onRemove: () => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const effective = value?.value ?? parameter.defaultValue ?? '';
  const [draft, setDraft] = useState(String(effective ?? ''));
  const [invalid, setInvalid] = useState(false);
  const colorParameter = parameter.kind === 'String' && /color$/i.test(parameter.key);
  useEffect(() => {
    setDraft(String(effective ?? ''));
    setInvalid(false);
  }, [effective, parameter.key]);
  if (parameter.kind === 'String' && parameter.key.toLowerCase() === 'actionmode') {
    const actionOptions = [
      ['command', text.buttonActionCommand],
      ['set-analog', text.buttonActionAnalog],
      ['set-bool', text.buttonActionSetBoolean],
      ['toggle-bool', text.buttonActionToggleBoolean]
    ] as const;
    return <select
      aria-label={text.buttonAction}
      value={String(effective)}
      disabled={disabled}
      onChange={event => onSet({ key: parameter.key, kind: parameter.kind, value: event.currentTarget.value, version: parameter.version })}
    >{actionOptions.map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select>;
  }

  const commit = () => {
    if (disabled) return;
    if (parameter.kind === 'EquipmentPath' && !draft.trim()) {
      if (parameter.required !== true) onRemove();
      else setInvalid(true);
      return;
    }
    if (parameter.kind === 'Number') {
      const number = Number(draft);
      if (!draft.trim() || !Number.isFinite(number)) {
        setInvalid(true);
        return;
      }
      setInvalid(false);
      onSet({ key: parameter.key, kind: parameter.kind, value: number, version: parameter.version });
      return;
    }
    setInvalid(false);
    if (colorParameter && !/^#[0-9a-f]{6}$/i.test(draft.trim())) {
      setInvalid(true);
      return;
    }
    onSet({
      key: parameter.key,
      kind: parameter.kind,
      value: parameter.kind === 'EquipmentPath' ? draft.trim() : draft,
      version: parameter.version
    });
  };

  return <div className="visual-editor-dynamo-parameter__scalar">
    <input
      type={colorParameter ? 'color' : parameter.kind === 'Number' ? 'number' : 'text'}
      value={draft}
      disabled={disabled}
      aria-invalid={invalid || undefined}
      placeholder={parameter.kind === 'EquipmentPath' ? 'Plant.P01' : undefined}
      onChange={event => setDraft(event.currentTarget.value)}
      onBlur={commit}
      onKeyDown={event => {
        if (event.key === 'Enter') {
          event.preventDefault();
          commit();
        }
      }}
    />
    {invalid ? <small>{text.invalidValue}: {parameter.kind}.</small> : null}
  </div>;
}

function TagParameterEditor({
  parameter,
  value,
  tags,
  disabled,
  onSet,
  onRemove
}: {
  parameter: DynamoParameterDefinitionEngineering;
  value: DynamoParameterValueEngineering | undefined;
  tags: readonly TagEngineering[];
  disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
  onRemove: () => void;
}) {
  const text = useC07VisualEditorText().dynamo;
  const currentTagId = value?.tagReference?.tagId ?? parameter.defaultTagReference?.tagId ?? '';
  return <>
  <select
    value={currentTagId}
    disabled={disabled}
    onChange={event => {
      const tagId = event.currentTarget.value;
      if (!tagId) {
        if (parameter.required !== true) onRemove();
        return;
      }
      onSet({
        key: parameter.key,
        kind: 'TagReference',
        tagReference: { tagId },
        version: parameter.version
      });
    }}
  >
    <option value="">{parameter.required ? text.selectTag : text.notAssigned}</option>
    {tags.map(tag => <option key={tag.id!} value={tag.id!}>
      {tag.name} · {tag.path} · {tag.dataType}
    </option>)}
  </select>
  {parameter.key === 'state' ? <small className="visual-editor-dynamo-parameter__hint">{text.stateTagHint}</small> : null}
  </>;
}

function dynamoStateLabel(
  kind: string,
  text: ReturnType<typeof useC07VisualEditorText>['runtimeState']
): string {
  switch (kind) {
    case 'bad-quality': return text.badQuality;
    case 'fault': return text.fault;
    case 'alarm': return text.alarm;
    case 'uncertain-quality': return text.uncertain;
    case 'command-intent': return text.command;
    case 'transitioning': return text.transition;
    case 'active': return text.active;
    case 'inactive': return text.inactive;
    default: return text.unknown;
  }
}

function findElement(
  elements: readonly VisualElementEngineering[],
  objectId: string
): (VisualElementEngineering & { id: string }) | null {
  for (const element of elements) {
    if (element.id === objectId) return element as VisualElementEngineering & { id: string };
    const nested = findElement(element.children ?? [], objectId);
    if (nested) return nested;
  }
  return null;
}

function equalsKey(left: string, right: string): boolean {
  return normalizeKey(left) === normalizeKey(right);
}

function normalizeKey(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
}

function booleanValue(value: unknown): boolean {
  return value === true;
}
