import type {
  BindingEngineering,
  TagValueReferenceEngineering,
  VisualElementEngineering
} from '../../engineering/types';
import type { DynamoParameterValueEngineering } from './runtimeVisualNavigationModel';

export function resolveDynamoRuntimeEquipmentPath(
  legacyEquipmentPath: string | null | undefined,
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>
): string | null {
  const typed = findParameter(parameters, 'equipmentPath');
  if (typed?.kind === 'EquipmentPath' && typeof typed.value === 'string' && typed.value.trim()) {
    return typed.value.trim();
  }

  const legacy = legacyEquipmentPath?.trim();
  return legacy || null;
}

/**
 * Projects a Dynamo definition into one runtime instance without mutating the
 * definition. Public TagReference parameters override only bindings that opt in
 * through metadata.dynamoParameter. Missing optional parameters preserve the
 * legacy equipmentPath target so old projects remain valid.
 */
export function projectDynamoRuntimeElements(
  elements: readonly VisualElementEngineering[],
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>,
  equipmentPath: string | null
): readonly VisualElementEngineering[] {
  return Object.freeze(elements.map(element => projectElement(element, parameters, equipmentPath)));
}

function projectElement(
  element: VisualElementEngineering,
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>,
  equipmentPath: string | null
): VisualElementEngineering {
  const bindings = element.bindings?.map(binding => projectBinding(binding, parameters, equipmentPath));
  const children = projectDynamoRuntimeElements(element.children ?? [], parameters, equipmentPath);

  return Object.freeze({
    ...element,
    bindings: bindings ? [...bindings] : element.bindings,
    propertyMaps: element.propertyMaps?.map(propertyMap => {
      const stateParameterKey = element.metadata?.dynamoStateColorParameter?.trim();
      const stateParameter = stateParameterKey ? findParameter(parameters, stateParameterKey) : undefined;
      const colorProfile = element.metadata?.dynamoStateColorProfile?.split(',').map(value => value.trim()) ?? [];
      const rules = propertyMap.rules.map((rule, index) => {
        const profileKey = colorProfile[index];
        const colorParameter = profileKey ? findParameter(parameters, `${profileKey}Color`) : undefined;
        const configuredColor = colorParameter?.kind === 'String' && typeof colorParameter.value === 'string'
          ? colorParameter.value.trim()
          : '';
        return configuredColor && /^#[0-9a-f]{6}$/i.test(configuredColor)
          ? Object.freeze({ ...rule, value: configuredColor })
          : rule;
      });
      return Object.freeze({
        ...propertyMap,
        rules: Object.freeze(rules),
        source: Object.freeze({
          ...propertyMap.source,
          target: substituteEquipmentPath(propertyMap.source.target ?? '', equipmentPath),
          tagReference: stateParameter?.kind === 'TagReference' && stateParameter.tagReference
            ? cloneTagReference(stateParameter.tagReference)
            : propertyMap.source.tagReference ? cloneTagReference(propertyMap.source.tagReference) : propertyMap.source.tagReference
        })
      });
    }),
    children: [...children]
  });
}

function projectBinding(
  binding: BindingEngineering,
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>,
  equipmentPath: string | null
): BindingEngineering {
  const parameterKey = binding.metadata?.dynamoParameter?.trim();
  const parameter = parameterKey ? findParameter(parameters, parameterKey) : undefined;
  const target = substituteEquipmentPath(binding.target, equipmentPath);

  if (parameter?.kind !== 'TagReference' || !parameter.tagReference) {
    return Object.freeze({
      ...binding,
      target,
      tagReference: binding.tagReference ? cloneTagReference(binding.tagReference) : binding.tagReference
    });
  }

  return Object.freeze({
    ...binding,
    target,
    tagReference: cloneTagReference(parameter.tagReference)
  });
}

function substituteEquipmentPath(target: string, equipmentPath: string | null): string {
  if (!equipmentPath) return target;
  return target.replaceAll('{equipmentPath}', equipmentPath);
}

function findParameter(
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>,
  key: string
): DynamoParameterValueEngineering | undefined {
  const normalized = normalizeKey(key);
  for (const [parameterKey, value] of parameters.entries()) {
    if (normalizeKey(parameterKey) === normalized || normalizeKey(value.key) === normalized) return value;
  }
  return undefined;
}

function cloneTagReference(reference: TagValueReferenceEngineering): TagValueReferenceEngineering {
  return Object.freeze({
    tagId: reference.tagId,
    selector: reference.selector ? Object.freeze({ ...reference.selector }) : reference.selector
  });
}

function normalizeKey(value: string): string {
  return value.trim().toLocaleLowerCase('en-US');
}
