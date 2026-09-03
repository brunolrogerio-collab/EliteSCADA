import React, { useEffect, useMemo, useState } from 'react';
import type {
  DynamoEngineering,
  ScreenEngineering,
  TagEngineering,
  VisualElementEngineering
} from '../../types';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterValueEngineering
} from '../../../runtime/visual-navigation/runtimeVisualNavigationModel';
import { useDynamoAuthoringCatalog } from '../DynamoAuthoringCatalogContext';
import { isVisualElementEffectivelyAuthoringLocked } from '../visualEditorAuthoringModel';
import type { VisualEditorKeyboardCommand } from '../visualEditorKeyboardModel';
import {
  listDynamoPublicParameters,
  listDynamoPublicParameterValues,
  resolveDynamoParameterEditorKind
} from '../dynamo/dynamoPublicInterfaceModel';
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
  const catalog = useDynamoAuthoringCatalog();
  if (selectedObjectIds.length !== 1) return null;
  const instance = findElement(screen.elements ?? [], selectedObjectIds[0]);
  if (!instance?.id || !instance.dynamoKey?.trim()) return null;

  const definition = catalog.definitions.find(item => equalsKey(item.key, instance.dynamoKey!));
  if (!definition) {
    return <details className="visual-editor-dynamo-inspector" open data-testid="dynamo-instance-inspector">
      <summary><strong>Dynamo</strong><code>{instance.dynamoKey}</code></summary>
      <p className="visual-editor-dynamo-inspector__error">Definition not found in the canonical Engineering snapshot.</p>
    </details>;
  }

  return <DynamoInspectorBody
    screen={screen}
    instance={instance}
    definition={definition}
    tags={catalog.tags}
    onCommand={onCommand}
  />;
}

function DynamoInspectorBody({
  screen,
  instance,
  definition,
  tags,
  onCommand
}: {
  screen: ScreenEngineering;
  instance: VisualElementEngineering & { id: string };
  definition: DynamoEngineering;
  tags: readonly TagEngineering[];
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
}) {
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
      <small>{locked ? 'Locked' : `${parameters.length} public`}</small>
    </summary>
    <div className="visual-editor-dynamo-inspector__body">
      <div className="visual-editor-dynamo-inspector__identity">
        <span>Instance</span><code>{instance.key}</code>
      </div>
      {parameters.length === 0 ? <p className="visual-editor-dynamo-inspector__empty">No public parameters.</p> : parameters.map(parameter => {
        const value = values.get(normalizeKey(parameter.key));
        return <ParameterEditor
          key={parameter.key}
          parameter={parameter}
          value={value}
          instance={instance}
          tags={tagOptions}
          disabled={locked || !onCommand}
          onSet={setValue}
          onRemove={() => removeValue(parameter.key)}
        />;
      })}
    </div>
  </details>;
}

function ParameterEditor({
  parameter,
  value,
  instance,
  tags,
  disabled,
  onSet,
  onRemove
}: {
  parameter: DynamoParameterDefinitionEngineering;
  value: DynamoParameterValueEngineering | undefined;
  instance: VisualElementEngineering;
  tags: readonly TagEngineering[];
  disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
  onRemove: () => void;
}) {
  const editor = resolveDynamoParameterEditorKind(parameter.kind);
  const hasStoredValue = Boolean(
    instance.dynamoParameters?.some(item => equalsKey(item.key, parameter.key))
    || (parameter.kind === 'EquipmentPath' && instance.equipmentPath?.trim())
  );
  const requiredMissing = parameter.required === true && value === undefined
    && parameter.defaultValue === undefined && !parameter.defaultTagReference;
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
      <span>{booleanValue(value?.value ?? parameter.defaultValue) ? 'True' : 'False'}</span>
    </label> : editor === 'tag-reference' ? <TagParameterEditor
      parameter={parameter}
      value={value}
      tags={tags}
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
      {requiredMissing ? <span>Required value missing</span> : <span>{hasStoredValue ? 'Instance value' : 'Default / unset'}</span>}
      {removeAllowed && editor !== 'tag-reference' ? <button type="button" onClick={onRemove}>Reset</button> : null}
    </footer>
  </div>;
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
  const effective = value?.value ?? parameter.defaultValue ?? '';
  const [draft, setDraft] = useState(String(effective ?? ''));
  const [invalid, setInvalid] = useState(false);
  useEffect(() => {
    setDraft(String(effective ?? ''));
    setInvalid(false);
  }, [effective, parameter.key]);

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
    onSet({
      key: parameter.key,
      kind: parameter.kind,
      value: parameter.kind === 'EquipmentPath' ? draft.trim() : draft,
      version: parameter.version
    });
  };

  return <div className="visual-editor-dynamo-parameter__scalar">
    <input
      type={parameter.kind === 'Number' ? 'number' : 'text'}
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
    {invalid ? <small>Invalid {parameter.kind} value.</small> : null}
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
  const currentTagId = value?.tagReference?.tagId ?? parameter.defaultTagReference?.tagId ?? '';
  return <select
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
    <option value="">{parameter.required ? 'Select TAG…' : 'Not assigned'}</option>
    {tags.map(tag => <option key={tag.id!} value={tag.id!}>
      {tag.name} · {tag.path} · {tag.dataType}
    </option>)}
  </select>;
}

function findElement(
  elements: readonly VisualElementEngineering[],
  objectId: string
): VisualElementEngineering | null {
  for (const element of elements) {
    if (element.id === objectId) return element;
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
