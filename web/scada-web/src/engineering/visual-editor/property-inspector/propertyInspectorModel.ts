import type {
  VisualElementEngineering,
  VisualEngineeringAssetReference,
  VisualEngineeringPropertyValue
} from '../../types';
import {
  getBuiltinVisualObjectSchema,
  type VisualObjectPropertySchema,
  type VisualPropertyDefinition,
  type VisualPropertyValidationFailure
} from '../../../visual-runtime';
import type { VisualEditorMutationIntent } from '../visualEditorContracts';

export type PropertyInspectorValueState = 'default' | 'engineered' | 'mixed';

export type PropertyInspectorRow = Readonly<{
  definition: VisualPropertyDefinition;
  state: PropertyInspectorValueState;
  value?: VisualEngineeringPropertyValue;
  defaultValue: VisualEngineeringPropertyValue;
  explicitCount: number;
  selectionCount: number;
}>;

export type PropertyInspectorModel = Readonly<{
  objectIds: readonly string[];
  objectTypes: readonly string[];
  rows: readonly PropertyInspectorRow[];
  error?: string;
}>;

export type PropertyInspectorIntentResult =
  | Readonly<{ ok: true; intent: VisualEditorMutationIntent }>
  | Readonly<{ ok: false; error: string; validation?: VisualPropertyValidationFailure }>;

export function buildPropertyInspectorModel(
  selectedElements: readonly VisualElementEngineering[]
): PropertyInspectorModel {
  if (selectedElements.length === 0) {
    return { objectIds: [], objectTypes: [], rows: [] };
  }

  const objectIds: string[] = [];
  const schemas: VisualObjectPropertySchema[] = [];
  const objectTypes: string[] = [];

  for (const element of selectedElements) {
    const objectId = element.id?.trim();
    if (!objectId) {
      return {
        objectIds: [],
        objectTypes: [],
        rows: [],
        error: `Visual object '${element.key}' has no stable canonical id.`
      };
    }

    try {
      schemas.push(getBuiltinVisualObjectSchema(element.type));
    } catch {
      return {
        objectIds: [],
        objectTypes: [],
        rows: [],
        error: `Visual object type '${element.type}' is not registered for property editing.`
      };
    }

    objectIds.push(objectId);
    objectTypes.push(element.type);
  }

  const firstSchema = schemas[0];
  const commonKeys = firstSchema.propertyKeys.filter(propertyKey => {
    const definition = firstSchema.getRequired(propertyKey);
    return definition.engineeringEditable && schemas.every(schema => schema.declares(propertyKey));
  });

  const rows = commonKeys.map(propertyKey => {
    const definition = firstSchema.getRequired(propertyKey);
    const explicitValues: VisualEngineeringPropertyValue[] = [];

    for (const element of selectedElements) {
      if (hasOwn(element.properties, propertyKey)) {
        explicitValues.push(element.properties![propertyKey]);
      }
    }

    if (explicitValues.length === 0) {
      return {
        definition,
        state: 'default' as const,
        value: cloneEngineeringValue(definition.defaultValue),
        defaultValue: cloneEngineeringValue(definition.defaultValue),
        explicitCount: 0,
        selectionCount: selectedElements.length
      };
    }

    const allExplicit = explicitValues.length === selectedElements.length;
    const allEqual = allExplicit && explicitValues.every(value => valuesEqual(value, explicitValues[0]));

    if (allEqual) {
      return {
        definition,
        state: 'engineered' as const,
        value: cloneEngineeringValue(explicitValues[0]),
        defaultValue: cloneEngineeringValue(definition.defaultValue),
        explicitCount: explicitValues.length,
        selectionCount: selectedElements.length
      };
    }

    return {
      definition,
      state: 'mixed' as const,
      defaultValue: cloneEngineeringValue(definition.defaultValue),
      explicitCount: explicitValues.length,
      selectionCount: selectedElements.length
    };
  });

  return {
    objectIds: Object.freeze(objectIds),
    objectTypes: Object.freeze(objectTypes),
    rows: Object.freeze(rows)
  };
}

export function buildPropertyInspectorSetIntent(
  model: PropertyInspectorModel,
  propertyKey: string,
  value: VisualEngineeringPropertyValue
): PropertyInspectorIntentResult {
  if (model.error) return { ok: false, error: model.error };
  if (model.objectIds.length === 0) return { ok: false, error: 'No canonical visual objects are selected.' };

  const row = model.rows.find(candidate => candidate.definition.key === propertyKey);
  if (!row) {
    return { ok: false, error: `Property '${propertyKey}' is not common and Engineering-editable for the current registered selection.` };
  }
  if (!row.definition.engineeringEditable) {
    return { ok: false, error: `Property '${propertyKey}' is not Engineering-editable.` };
  }

  for (const objectType of model.objectTypes) {
    const validation = getBuiltinVisualObjectSchema(objectType).validate(propertyKey, value);
    if (!validation.ok) {
      return {
        ok: false,
        error: validation.detail ?? `Property '${propertyKey}' rejected value (${validation.code}).`,
        validation
      };
    }
  }

  return {
    ok: true,
    intent: {
      kind: 'property.set',
      objectIds: model.objectIds,
      propertyKey,
      value: cloneEngineeringValue(value)
    }
  };
}

export function buildPropertyInspectorRemoveIntent(
  model: PropertyInspectorModel,
  propertyKey: string
): PropertyInspectorIntentResult {
  if (model.error) return { ok: false, error: model.error };
  if (model.objectIds.length === 0) return { ok: false, error: 'No canonical visual objects are selected.' };

  const row = model.rows.find(candidate => candidate.definition.key === propertyKey);
  if (!row) {
    return { ok: false, error: `Property '${propertyKey}' is not common and Engineering-editable for the current registered selection.` };
  }
  if (!row.definition.engineeringEditable) {
    return { ok: false, error: `Property '${propertyKey}' is not Engineering-editable.` };
  }

  return {
    ok: true,
    intent: {
      kind: 'property.remove',
      objectIds: model.objectIds,
      propertyKey
    }
  };
}

export function parsePropertyInspectorInput(
  definition: VisualPropertyDefinition,
  rawValue: string
): Readonly<{ ok: true; value: VisualEngineeringPropertyValue }> | Readonly<{ ok: false; error: string }> {
  switch (definition.type) {
    case 'number': {
      if (rawValue.trim() === '') return { ok: false, error: 'A numeric value is required.' };
      const value = Number(rawValue);
      if (!Number.isFinite(value)) return { ok: false, error: 'A finite numeric value is required.' };
      return { ok: true, value };
    }
    case 'assetRef': {
      const assetId = rawValue.trim();
      return { ok: true, value: assetId === '' ? null : { assetId } };
    }
    case 'boolean':
      if (rawValue === 'true') return { ok: true, value: true };
      if (rawValue === 'false') return { ok: true, value: false };
      return { ok: false, error: 'Expected true or false.' };
    case 'string':
    case 'color':
    case 'enum':
      return { ok: true, value: rawValue };
  }
}

export function formatPropertyInspectorValue(value: VisualEngineeringPropertyValue): string {
  if (value === null) return '';
  if (isAssetReference(value)) return value.assetId;
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

function hasOwn(
  properties: VisualElementEngineering['properties'],
  propertyKey: string
): boolean {
  return properties != null && Object.prototype.hasOwnProperty.call(properties, propertyKey);
}

function valuesEqual(
  left: VisualEngineeringPropertyValue,
  right: VisualEngineeringPropertyValue
): boolean {
  if (left === right) return true;
  if (left === null || right === null || typeof left !== 'object' || typeof right !== 'object') return false;
  if (isAssetReference(left) || isAssetReference(right)) {
    return isAssetReference(left) && isAssetReference(right) && left.assetId === right.assetId;
  }
  return JSON.stringify(left) === JSON.stringify(right);
}

function cloneEngineeringValue(value: VisualEngineeringPropertyValue): VisualEngineeringPropertyValue {
  if (value === null || typeof value !== 'object') return value;
  return JSON.parse(JSON.stringify(value)) as VisualEngineeringPropertyValue;
}

function isAssetReference(value: VisualEngineeringPropertyValue): value is VisualEngineeringAssetReference {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false;
  return typeof (value as { assetId?: unknown }).assetId === 'string';
}
