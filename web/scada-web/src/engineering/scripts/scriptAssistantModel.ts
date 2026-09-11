import type {
  ClientMemorySourceDefinition,
  ClientMemoryTagDefinition
} from '../../runtime/clientMemory';
import {
  CLIENT_VISUAL_PYTHON_CAPABILITIES,
  type ClientVisualPythonCapability
} from '../../python-runtime/pythonRuntimeContracts';
import { getBuiltinVisualObjectSchema } from '../../visual-runtime/builtinVisualObjectSchemas';
import type { VisualPropertyDefinition, VisualPropertyValue } from '../../visual-runtime/visualPropertyTypes';
import type {
  DataSourceEngineering,
  DynamoEngineering,
  EngineeringPackageView,
  PopupEngineering,
  ScreenEngineering,
  TagEngineering,
  VisualElementEngineering
} from '../types';

export type ScriptAssistantSnippetKind =
  | 'tag-read'
  | 'tag-write'
  | 'client-memory-read'
  | 'client-memory-write'
  | 'visual-property-read'
  | 'visual-property-write'
  | 'visual-property-clear'
  | 'visual-tween';

export type ScriptAssistantSnippet = Readonly<{
  kind: ScriptAssistantSnippetKind;
  code: string;
  enabled: boolean;
  reason?: string;
}>;

export type ScriptAssistantTag = Readonly<{
  kind: 'tag';
  id: string;
  name: string;
  path: string;
  canonicalReference: string | null;
  dataType: string;
  engineeringUnit: string | null;
  description: string | null;
  readOnly: boolean;
  sourceLabel: string | null;
  dataSourceId: string | null;
  driver: string | null;
  sourceIdentityStatus: 'stable' | 'legacy' | 'unresolved' | 'none';
  snippets: readonly ScriptAssistantSnippet[];
}>;

export type ScriptAssistantVisualProperty = Readonly<{
  kind: 'visual-property';
  key: string;
  type: VisualPropertyDefinition['type'];
  category: string | null;
  currentValue: VisualPropertyValue;
  defaultValue: VisualPropertyValue;
  runtimeReadable: boolean;
  runtimeWritable: boolean;
  supportsBinding: boolean;
  animatable: boolean;
  allowedValues: readonly string[];
  snippets: readonly ScriptAssistantSnippet[];
}>;

export type ScriptAssistantDynamoParameter = Readonly<{
  kind: 'dynamo-public-parameter';
  key: string;
  parameterKind: string;
  required: boolean;
  value: unknown;
  tagReference: string | null;
}>;

export type ScriptAssistantVisualObject = Readonly<{
  kind: 'visual-object';
  id: string;
  key: string;
  type: string;
  canonicalReference: string;
  dynamoKey: string | null;
  equipmentPath: string | null;
  events: readonly string[];
  properties: readonly ScriptAssistantVisualProperty[];
  publicDynamoParameters: readonly ScriptAssistantDynamoParameter[];
  children: readonly ScriptAssistantVisualObject[];
  schemaStatus: 'canonical' | 'unknown';
}>;

export type ScriptAssistantVisualDefinition = Readonly<{
  kind: 'screen' | 'popup';
  id: string;
  key: string;
  name: string;
  route: string | null;
  objects: readonly ScriptAssistantVisualObject[];
}>;

export type ScriptAssistantClientMemory = Readonly<{
  kind: 'client-memory';
  id: string;
  name: string;
  path: string;
  dataType: string;
  readOnly: boolean;
  initialValue: unknown;
  sourceKey: string;
  sourceName: string;
  snippets: readonly ScriptAssistantSnippet[];
}>;

export type ScriptAssistantCapability = Readonly<{
  capability: ClientVisualPythonCapability;
  pythonApi: string;
}>;

export type ScriptAssistantCatalog = Readonly<{
  tags: readonly ScriptAssistantTag[];
  screens: readonly ScriptAssistantVisualDefinition[];
  popups: readonly ScriptAssistantVisualDefinition[];
  clientMemory: readonly ScriptAssistantClientMemory[];
  capabilities: readonly ScriptAssistantCapability[];
}>;

export function buildScriptAssistantCatalog(
  engineeringPackage: EngineeringPackageView,
  clientMemorySources: readonly ClientMemorySourceDefinition[] = []
): ScriptAssistantCatalog {
  const dataSourcesById = new Map<string, DataSourceEngineering>();
  for (const source of engineeringPackage.dataSources ?? []) {
    if (source.id?.trim()) dataSourcesById.set(source.id, source);
  }

  const dynamosByKey = new Map<string, DynamoEngineering>();
  for (const definition of engineeringPackage.dynamos ?? []) {
    dynamosByKey.set(definition.key, definition);
  }

  return Object.freeze({
    tags: Object.freeze((engineeringPackage.tags ?? []).map(tag => buildTag(tag, dataSourcesById))),
    screens: Object.freeze((engineeringPackage.screens ?? []).map(screen => buildVisualDefinition('screen', screen, dynamosByKey))),
    popups: Object.freeze((engineeringPackage.popups ?? []).map(popup => buildVisualDefinition('popup', popup, dynamosByKey))),
    clientMemory: Object.freeze(clientMemorySources.flatMap(source => source.tags.map(tag => buildClientMemory(source, tag)))),
    capabilities: Object.freeze(CLIENT_VISUAL_PYTHON_CAPABILITIES.map(capability => Object.freeze({
      capability,
      pythonApi: pythonApiForCapability(capability)
    })))
  });
}

export function filterScriptAssistantCatalog(
  catalog: ScriptAssistantCatalog,
  query: string
): ScriptAssistantCatalog {
  const normalized = query.trim().toLocaleLowerCase('en-US');
  if (!normalized) return catalog;

  const matches = (...values: readonly unknown[]) => values
    .filter(value => value !== null && value !== undefined)
    .some(value => String(value).toLocaleLowerCase('en-US').includes(normalized));

  const filterObject = (object: ScriptAssistantVisualObject): ScriptAssistantVisualObject | null => {
    const children = object.children
      .map(filterObject)
      .filter((item): item is ScriptAssistantVisualObject => item !== null);
    const properties = object.properties.filter(property => matches(
      property.key,
      property.type,
      property.category
    ));
    const publicDynamoParameters = object.publicDynamoParameters.filter(parameter => matches(
      parameter.key,
      parameter.parameterKind,
      parameter.tagReference
    ));
    const selfMatch = matches(object.id, object.key, object.type, object.dynamoKey, object.equipmentPath, ...object.events);
    if (!selfMatch && children.length === 0 && properties.length === 0 && publicDynamoParameters.length === 0) return null;
    return Object.freeze({
      ...object,
      children: Object.freeze(children),
      properties: selfMatch ? object.properties : Object.freeze(properties),
      publicDynamoParameters: selfMatch ? object.publicDynamoParameters : Object.freeze(publicDynamoParameters)
    });
  };

  const filterVisual = (definition: ScriptAssistantVisualDefinition): ScriptAssistantVisualDefinition | null => {
    if (matches(definition.id, definition.key, definition.name, definition.route)) return definition;
    const objects = definition.objects
      .map(filterObject)
      .filter((item): item is ScriptAssistantVisualObject => item !== null);
    return objects.length > 0 ? Object.freeze({ ...definition, objects: Object.freeze(objects) }) : null;
  };

  return Object.freeze({
    tags: Object.freeze(catalog.tags.filter(tag => matches(
      tag.id,
      tag.name,
      tag.path,
      tag.canonicalReference,
      tag.dataType,
      tag.engineeringUnit,
      tag.description,
      tag.sourceLabel,
      tag.dataSourceId,
      tag.driver
    ))),
    screens: Object.freeze(catalog.screens.map(filterVisual).filter((item): item is ScriptAssistantVisualDefinition => item !== null)),
    popups: Object.freeze(catalog.popups.map(filterVisual).filter((item): item is ScriptAssistantVisualDefinition => item !== null)),
    clientMemory: Object.freeze(catalog.clientMemory.filter(memory => matches(
      memory.id,
      memory.name,
      memory.path,
      memory.dataType,
      memory.sourceKey,
      memory.sourceName
    ))),
    capabilities: Object.freeze(catalog.capabilities.filter(item => matches(item.capability, item.pythonApi)))
  });
}

function buildTag(
  tag: TagEngineering,
  dataSourcesById: ReadonlyMap<string, DataSourceEngineering>
): ScriptAssistantTag {
  const id = tag.id?.trim() ?? '';
  const dataSourceId = readStableDataSourceId(tag);
  const dataSource = dataSourceId ? dataSourcesById.get(dataSourceId) ?? null : null;
  const sourceIdentityStatus = dataSourceId
    ? (dataSource ? 'stable' : 'unresolved')
    : tag.source?.trim() ? 'legacy' : 'none';
  const canonicalReference = id || null;
  const snippets: ScriptAssistantSnippet[] = [];

  snippets.push(canonicalReference
    ? enabledSnippet('tag-read', tagReadCode(canonicalReference))
    : disabledSnippet('tag-read', 'TAG has no stable canonical identity.'));

  if (!canonicalReference) {
    snippets.push(disabledSnippet('tag-write', 'TAG has no stable canonical identity.'));
  } else if (tag.readOnly) {
    snippets.push(disabledSnippet('tag-write', 'TAG is read-only.'));
  } else {
    const sample = samplePythonValueForDataType(tag.dataType);
    snippets.push(sample.supported
      ? enabledSnippet('tag-write', tagWriteCode(canonicalReference, sample.literal))
      : disabledSnippet('tag-write', `TAG data type '${tag.dataType}' has no safe scalar write sample.`));
  }

  return Object.freeze({
    kind: 'tag',
    id,
    name: tag.name,
    path: tag.path,
    canonicalReference,
    dataType: tag.dataType,
    engineeringUnit: tag.engineeringUnit ?? null,
    description: tag.description ?? null,
    readOnly: tag.readOnly,
    sourceLabel: tag.source?.trim() || dataSource?.name || null,
    dataSourceId,
    driver: dataSource?.driver ?? null,
    sourceIdentityStatus,
    snippets: Object.freeze(snippets)
  });
}

function buildVisualDefinition(
  kind: 'screen' | 'popup',
  definition: ScreenEngineering | PopupEngineering,
  dynamosByKey: ReadonlyMap<string, DynamoEngineering>
): ScriptAssistantVisualDefinition {
  return Object.freeze({
    kind,
    id: definition.id?.trim() || definition.key,
    key: definition.key,
    name: definition.name,
    route: kind === 'screen' ? (definition as ScreenEngineering).route ?? null : null,
    objects: Object.freeze((definition.elements ?? []).map(element => buildVisualObject(element, dynamosByKey)))
  });
}

function buildVisualObject(
  element: VisualElementEngineering,
  dynamosByKey: ReadonlyMap<string, DynamoEngineering>
): ScriptAssistantVisualObject {
  const canonicalReference = element.id?.trim() || element.key;
  const dynamoDefinition = element.dynamoKey ? dynamosByKey.get(element.dynamoKey) ?? null : null;
  const publicDynamoParameters = dynamoDefinition
    ? buildDynamoPublicParameters(element, dynamoDefinition)
    : [];

  const propertyModel = buildVisualProperties(element, canonicalReference);
  const children = element.dynamoKey
    ? []
    : (element.children ?? []).map(child => buildVisualObject(child, dynamosByKey));

  return Object.freeze({
    kind: 'visual-object',
    id: element.id?.trim() || '',
    key: element.key,
    type: element.type,
    canonicalReference,
    dynamoKey: element.dynamoKey ?? null,
    equipmentPath: element.equipmentPath ?? null,
    events: Object.freeze(element.type === 'core.button' ? ['Click'] : []),
    properties: Object.freeze(propertyModel.properties),
    publicDynamoParameters: Object.freeze(publicDynamoParameters),
    children: Object.freeze(children),
    schemaStatus: propertyModel.schemaStatus
  });
}

function buildVisualProperties(
  element: VisualElementEngineering,
  canonicalReference: string
): { schemaStatus: 'canonical' | 'unknown'; properties: ScriptAssistantVisualProperty[] } {
  let definitions: readonly VisualPropertyDefinition[];
  try {
    definitions = getBuiltinVisualObjectSchema(element.type).definitions();
  } catch {
    return { schemaStatus: 'unknown', properties: [] };
  }

  return {
    schemaStatus: 'canonical',
    properties: definitions.map(definition => buildVisualProperty(element, canonicalReference, definition))
  };
}

function buildVisualProperty(
  element: VisualElementEngineering,
  canonicalReference: string,
  definition: VisualPropertyDefinition
): ScriptAssistantVisualProperty {
  const currentValue = Object.hasOwn(element.properties ?? {}, definition.key)
    ? (element.properties?.[definition.key] as VisualPropertyValue)
    : definition.defaultValue;
  const snippets: ScriptAssistantSnippet[] = [];

  snippets.push(definition.runtimeReadable
    ? enabledSnippet('visual-property-read', visualReadCode(canonicalReference, definition.key))
    : disabledSnippet('visual-property-read', 'Property is not runtime-readable.'));

  if (definition.runtimeWritable && definition.type !== 'assetRef') {
    const literal = pythonLiteral(definition.defaultValue);
    snippets.push(literal === null
      ? disabledSnippet('visual-property-write', 'Property default cannot be represented as a safe Python scalar.')
      : enabledSnippet('visual-property-write', visualWriteCode(canonicalReference, definition.key, literal)));
    snippets.push(enabledSnippet('visual-property-clear', visualClearCode(canonicalReference, definition.key)));
  } else {
    snippets.push(disabledSnippet('visual-property-write', 'Property is not runtime-script-writable.'));
    snippets.push(disabledSnippet('visual-property-clear', 'Property is not runtime-script-writable.'));
  }

  if (definition.animatable && definition.runtimeWritable && definition.type !== 'assetRef') {
    const literal = pythonLiteral(definition.defaultValue);
    snippets.push(literal === null
      ? disabledSnippet('visual-tween', 'Property target cannot be represented as a safe Python scalar.')
      : enabledSnippet('visual-tween', visualTweenCode(canonicalReference, definition.key, literal)));
  }

  return Object.freeze({
    kind: 'visual-property',
    key: definition.key,
    type: definition.type,
    category: definition.category ?? null,
    currentValue,
    defaultValue: definition.defaultValue,
    runtimeReadable: definition.runtimeReadable,
    runtimeWritable: definition.runtimeWritable,
    supportsBinding: definition.supportsBinding,
    animatable: definition.animatable,
    allowedValues: Object.freeze(definition.type === 'enum' ? [...definition.allowedValues] : []),
    snippets: Object.freeze(snippets)
  });
}

function buildDynamoPublicParameters(
  instance: VisualElementEngineering,
  definition: DynamoEngineering
): ScriptAssistantDynamoParameter[] {
  const values = new Map((instance.dynamoParameters ?? []).map(value => [value.key, value]));
  return (definition.parameters ?? []).map(parameter => {
    const assigned = values.get(parameter.key);
    return Object.freeze({
      kind: 'dynamo-public-parameter' as const,
      key: parameter.key,
      parameterKind: parameter.kind,
      required: parameter.required === true,
      value: assigned?.value ?? parameter.defaultValue ?? null,
      tagReference: assigned?.tagReference?.tagId ?? parameter.defaultTagReference?.tagId ?? null
    });
  });
}

function buildClientMemory(
  source: ClientMemorySourceDefinition,
  tag: ClientMemoryTagDefinition
): ScriptAssistantClientMemory {
  const reference = tag.id?.trim() || tag.path;
  const snippets: ScriptAssistantSnippet[] = [
    enabledSnippet('client-memory-read', clientMemoryReadCode(reference))
  ];

  if (tag.readOnly) {
    snippets.push(disabledSnippet('client-memory-write', 'Client Memory entry is read-only.'));
  } else {
    const sample = samplePythonValueForDataType(tag.dataType);
    snippets.push(sample.supported
      ? enabledSnippet('client-memory-write', clientMemoryWriteCode(reference, sample.literal))
      : disabledSnippet('client-memory-write', `Client Memory data type '${tag.dataType}' has no safe sample.`));
  }

  return Object.freeze({
    kind: 'client-memory',
    id: tag.id,
    name: tag.name,
    path: tag.path,
    dataType: tag.dataType,
    readOnly: tag.readOnly,
    initialValue: tag.initialValue,
    sourceKey: source.dataSourceKey,
    sourceName: source.name,
    snippets: Object.freeze(snippets)
  });
}

function readStableDataSourceId(tag: TagEngineering): string | null {
  const record = tag as TagEngineering & { dataSourceId?: unknown; DataSourceId?: unknown };
  for (const candidate of [record.dataSourceId, record.DataSourceId, tag.metadata?.dataSourceId, tag.metadata?.DataSourceId]) {
    if (typeof candidate === 'string' && candidate.trim()) return candidate.trim();
  }
  return null;
}

function pythonApiForCapability(capability: ClientVisualPythonCapability): string {
  switch (capability) {
    case 'tag.read': return 'elite_scada.tag_read';
    case 'tag.write': return 'elite_scada.tag_write';
    case 'clientMemory.read': return 'elite_scada.client_memory_read';
    case 'clientMemory.write': return 'elite_scada.client_memory_write';
    case 'visualProperty.read': return 'elite_scada.visual_property_read';
    case 'visualProperty.write': return 'elite_scada.visual_property_write / visual_property_clear';
    case 'visualTween.request': return 'elite_scada.visual_tween_request';
    case 'backendOperation.request': return 'elite_scada.backend_operation_request';
  }
}

function enabledSnippet(kind: ScriptAssistantSnippetKind, code: string): ScriptAssistantSnippet {
  return Object.freeze({ kind, code, enabled: true });
}

function disabledSnippet(kind: ScriptAssistantSnippetKind, reason: string): ScriptAssistantSnippet {
  return Object.freeze({ kind, code: '', enabled: false, reason });
}

function tagReadCode(reference: string): string {
  return `from elite_scada import tag_read\nvalue = await tag_read(${pythonString(reference)})`;
}

function tagWriteCode(reference: string, literal: string): string {
  return `from elite_scada import tag_write\nawait tag_write(${pythonString(reference)}, ${literal})`;
}

function clientMemoryReadCode(reference: string): string {
  return `from elite_scada import client_memory_read\nvalue = await client_memory_read(${pythonString(reference)})`;
}

function clientMemoryWriteCode(reference: string, literal: string): string {
  return `from elite_scada import client_memory_write\nawait client_memory_write(${pythonString(reference)}, ${literal})`;
}

function visualReadCode(reference: string, propertyKey: string): string {
  return `from elite_scada import visual_property_read\nvalue = await visual_property_read(${pythonString(reference)}, ${pythonString(propertyKey)})`;
}

function visualWriteCode(reference: string, propertyKey: string, literal: string): string {
  return `from elite_scada import visual_property_write\nawait visual_property_write(${pythonString(reference)}, ${pythonString(propertyKey)}, ${literal})`;
}

function visualClearCode(reference: string, propertyKey: string): string {
  return `from elite_scada import visual_property_clear\nawait visual_property_clear(${pythonString(reference)}, ${pythonString(propertyKey)})`;
}

function visualTweenCode(reference: string, propertyKey: string, literal: string): string {
  return [
    'from elite_scada import visual_tween_request',
    'await visual_tween_request({',
    `    "targetReference": ${pythonString(reference)},`,
    `    "propertyKey": ${pythonString(propertyKey)},`,
    `    "targetValue": ${literal},`,
    '    "durationMs": 300',
    '})'
  ].join('\n');
}

function pythonString(value: string): string {
  return JSON.stringify(value);
}

function pythonLiteral(value: VisualPropertyValue): string | null {
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  if (typeof value === 'number' && Number.isFinite(value)) return String(value);
  if (typeof value === 'string') return pythonString(value);
  return null;
}

function samplePythonValueForDataType(dataType: string): { supported: true; literal: string } | { supported: false; literal: '' } {
  switch (dataType.trim().toLocaleLowerCase('en-US')) {
    case 'boolean':
    case 'bool':
      return { supported: true, literal: 'False' };
    case 'int16':
    case 'int32':
    case 'int64':
    case 'uint16':
    case 'uint32':
    case 'uint64':
    case 'float':
    case 'double':
    case 'single':
    case 'decimal':
    case 'number':
    case 'enum':
      return { supported: true, literal: '0' };
    case 'string':
    case 'datetime':
      return { supported: true, literal: '""' };
    default:
      return { supported: false, literal: '' };
  }
}
