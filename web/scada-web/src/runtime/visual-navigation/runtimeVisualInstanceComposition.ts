import type {
  VisualElementEngineering,
  VisualEngineeringPropertyMap,
  VisualEngineeringPropertyValue
} from '../../engineering/types';
import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  VISUAL_PROPERTY_KEYS,
  getBuiltinVisualObjectSchema,
  projectVisualEngineeringDefinition,
  RuntimeVisualInstance
} from '../../visual-runtime';

const builtinVisualTypes = new Set<string>(Object.values(BUILTIN_VISUAL_OBJECT_TYPES));

export function createRuntimeVisualInstances(
  elements: readonly VisualElementEngineering[] | null | undefined,
  runtimeContextId: string
): ReadonlyMap<string, RuntimeVisualInstance> {
  const instances = new Map<string, RuntimeVisualInstance>();
  collectRuntimeVisualInstances(elements ?? [], runtimeContextId, instances);
  return instances;
}

export function projectRuntimeVisualElements(
  elements: readonly VisualElementEngineering[] | null | undefined,
  instances: ReadonlyMap<string, RuntimeVisualInstance>
): readonly VisualElementEngineering[] {
  return Object.freeze((elements ?? []).map(element => projectRuntimeVisualElement(element, instances)));
}

function collectRuntimeVisualInstances(
  elements: readonly VisualElementEngineering[],
  runtimeContextId: string,
  target: Map<string, RuntimeVisualInstance>
): void {
  for (const element of elements) {
    const objectId = element.id?.trim();
    if (objectId && builtinVisualTypes.has(element.type)) {
      const schema = getBuiltinVisualObjectSchema(element.type);
      const baseProperties: Record<string, unknown> = Object.create(null) as Record<string, unknown>;
      const engineered = element.properties ?? {};

      for (const propertyKey of schema.propertyKeys) {
        const definition = schema.getRequired(propertyKey);
        if (definition.engineeringEditable && Object.hasOwn(engineered, propertyKey)) {
          baseProperties[propertyKey] = engineered[propertyKey];
        }
      }

      const projection = projectVisualEngineeringDefinition({
        objectId,
        key: element.key,
        objectType: element.type,
        baseProperties
      }, schema);

      target.set(objectId, new RuntimeVisualInstance({
        definition: projection,
        schema,
        runtimeInstanceId: `${runtimeContextId}/${objectId}`
      }));
    }

    if (element.children?.length) {
      collectRuntimeVisualInstances(element.children, runtimeContextId, target);
    }
  }
}

function projectRuntimeVisualElement(
  element: VisualElementEngineering,
  instances: ReadonlyMap<string, RuntimeVisualInstance>
): VisualElementEngineering {
  const objectId = element.id?.trim();
  const instance = objectId ? instances.get(objectId) : undefined;
  const children = element.children?.length
    ? projectRuntimeVisualElements(element.children, instances)
    : element.children;

  if (!instance || !builtinVisualTypes.has(element.type)) {
    return children === element.children ? element : { ...element, children: children ? [...children] : children };
  }

  const schema = getBuiltinVisualObjectSchema(element.type);
  const overrideKeys = new Set<string>();
  const properties: VisualEngineeringPropertyMap = { ...(element.properties ?? {}) };

  for (const propertyKey of schema.propertyKeys) {
    const state = instance.readEffective(propertyKey);
    if (state.source !== 'script' && state.source !== 'animation') continue;
    overrideKeys.add(propertyKey);
    properties[propertyKey] = state.value as VisualEngineeringPropertyValue;
  }

  if (overrideKeys.size === 0) {
    return children === element.children ? element : { ...element, children: children ? [...children] : children };
  }

  // Script/Animation are projected only into this transient renderer tree.
  // Lower-priority dynamic sources for the same property are removed here so
  // the public precedence remains Animation > Script > Binding/Expression >
  // Engineering > Default without mutating canonical Engineering.
  const bindings = element.bindings?.filter(binding => !overrideKeys.has(binding.key)) ?? element.bindings;
  const propertyExpressions = element.propertyExpressions
    ?.filter(expression => !overrideKeys.has(expression.propertyKey)) ?? element.propertyExpressions;
  const booleanConditions = element.booleanConditions
    ?.filter(condition => !overrideKeys.has(condition.propertyKey)) ?? element.booleanConditions;
  const analogFill = overrideKeys.has(VISUAL_PROPERTY_KEYS.fillColor)
    ? null
    : element.analogFill;

  return {
    ...element,
    properties,
    bindings: bindings ? [...bindings] : bindings,
    propertyExpressions: propertyExpressions ? [...propertyExpressions] : propertyExpressions,
    booleanConditions: booleanConditions ? [...booleanConditions] : booleanConditions,
    analogFill,
    children: children ? [...children] : children
  };
}
