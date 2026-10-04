import type {
  BindingEngineering,
  TagValueReferenceEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyValue,
  VisualValueSourceEngineering
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
  const properties: Record<string, VisualEngineeringPropertyValue> = {
    ...(element.properties ?? {})
  };
  const animationParameterKey = element.metadata?.dynamoAnimationEnabledParameter?.trim();
  const animationParameter = animationParameterKey ? findParameter(parameters, animationParameterKey) : undefined;
  const animationEnabled = animationParameter?.kind !== 'Boolean' || animationParameter.value !== false;
  if (!animationEnabled) {
    const stateParameterKey = element.metadata?.dynamoFixedStateParameter?.trim();
    const stateParameter = stateParameterKey ? findParameter(parameters, stateParameterKey) : undefined;
    const profile = element.metadata?.dynamoStateColorProfile?.split(',').map(value => value.trim()) ?? [];
    const stateIndex = stateParameter?.kind === 'Number' && typeof stateParameter.value === 'number'
      ? Math.trunc(stateParameter.value)
      : -1;
    const colorKey = stateIndex >= 0 && stateIndex < profile.length ? `${profile[stateIndex]}Color` : '';
    const colorParameter = colorKey ? findParameter(parameters, colorKey) : undefined;
    if (colorParameter?.kind === 'String' && typeof colorParameter.value === 'string') {
      if (element.type === 'core.svgSymbol') {
        properties.svgPaintOverrides = Object.freeze({
          version: 1,
          palette: Object.freeze({}),
          slots: Object.freeze({ state: Object.freeze({ fill: colorParameter.value }) })
        });
      } else {
        properties.fillColor = colorParameter.value;
      }
    }
  }
  const bindings: BindingEngineering[] = [];

  for (const binding of element.bindings ?? []) {
    const parameterKey = binding.metadata?.dynamoParameter?.trim();
    const parameter = parameterKey ? findParameter(parameters, parameterKey) : undefined;
    const scalarValue = scalarParameterValue(parameter);
    if (parameter && parameter.kind !== 'TagReference' && scalarValue !== undefined) {
      properties[binding.key] = scalarValue;
      continue;
    }
    bindings.push(projectBinding(binding, parameters, equipmentPath));
  }

  const children = projectDynamoRuntimeElements(element.children ?? [], parameters, equipmentPath);

  return Object.freeze({
    ...element,
    properties,
    bindings: element.bindings ? [...bindings] : element.bindings,
    propertyMaps: animationEnabled ? element.propertyMaps?.map(propertyMap => {
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
        source: projectValueSource(
          stateParameter?.kind === 'TagReference' && stateParameter.tagReference
            ? Object.freeze({ ...propertyMap.source, tagReference: cloneTagReference(stateParameter.tagReference) })
            : propertyMap.source,
          parameters,
          equipmentPath)
      });
    }) : undefined,
    booleanConditions: element.booleanConditions?.map(condition => Object.freeze({
      ...condition,
      source: projectValueSource(condition.source, parameters, equipmentPath)
    })),
    analogFill: element.analogFill ? Object.freeze({
      ...element.analogFill,
      source: projectValueSource(element.analogFill.source, parameters, equipmentPath)
    }) : element.analogFill,
    actions: element.actions?.map(action => projectAction(action, parameters)) ?? element.actions,
    children: [...children]
  });
}

function projectAction(
  action: NonNullable<VisualElementEngineering['actions']>[number],
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>
): NonNullable<VisualElementEngineering['actions']>[number] {
  let projected = action;
  const commandParameterKey = action.commandParameterKey?.trim();
  if (commandParameterKey) {
    const parameter = findParameter(parameters, commandParameterKey);
    if (!parameter || parameter.kind !== 'Command' || !parameter.commandId?.trim()) {
      throw new Error(`Dynamo ExecuteCommand action '${action.eventKey}' requires mapped Command parameter '${commandParameterKey}'.`);
    }
    projected = { ...projected, commandId: parameter.commandId, commandParameterKey: null };
  }

  const targetParameterKey = parameterToken(action.targetKey);
  if (targetParameterKey && (action.kind === 'SetTagValue' || action.kind === 'ToggleTagBoolean')) {
    const parameter = findParameter(parameters, targetParameterKey);
    if (!parameter || parameter.kind !== 'TagReference' || !parameter.tagReference?.tagId.trim()) {
      throw new Error(`Dynamo visual action '${action.eventKey}' requires mapped TagReference parameter '${targetParameterKey}'.`);
    }
    projected = { ...projected, targetKey: parameter.tagReference.tagId };
  }

  if (action.parameters) {
    const values = Object.fromEntries(Object.entries(action.parameters).map(([key, value]) => {
      const parameterKey = parameterToken(value);
      if (!parameterKey) return [key, value];
      const parameter = findParameter(parameters, parameterKey);
      if (!parameter || parameter.value === undefined || parameter.value === null ||
          parameter.kind === 'TagReference' || parameter.kind === 'Command' || parameter.kind === 'EquipmentPath') {
        throw new Error(`Dynamo visual action '${action.eventKey}' requires a scalar parameter '${parameterKey}'.`);
      }
      return [key, parameter.value];
    }));
    projected = { ...projected, parameters: values };
  }
  return Object.freeze(projected);
}

function parameterToken(value: unknown): string | null {
  if (typeof value !== 'string' || !/^\{[^{}]+\}$/.test(value.trim())) return null;
  return value.trim().slice(1, -1).trim() || null;
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

function projectValueSource(
  source: VisualValueSourceEngineering,
  parameters: ReadonlyMap<string, DynamoParameterValueEngineering>,
  equipmentPath: string | null
): VisualValueSourceEngineering {
  const target = substituteEquipmentPath(source.target ?? '', equipmentPath);
  const parameterKey = dynamoParameterFromTarget(target);
  const parameter = parameterKey ? findParameter(parameters, parameterKey) : undefined;
  const projectedValue = parameter?.kind === 'Boolean' && typeof parameter.value === 'boolean'
    ? parameter.value
    : parameter?.kind === 'Number' && typeof parameter.value === 'number' && Number.isFinite(parameter.value)
      ? parameter.value
      : undefined;
  return Object.freeze({
    ...source,
    target,
    ...(projectedValue !== undefined ? { projectedValue } : {}),
    tagReference: parameter?.kind === 'TagReference' && parameter.tagReference
      ? cloneTagReference(parameter.tagReference)
      : source.tagReference ? cloneTagReference(source.tagReference) : source.tagReference
  });
}

function scalarParameterValue(
  parameter: DynamoParameterValueEngineering | undefined
): string | number | boolean | undefined {
  if (!parameter) return undefined;
  if (parameter.kind === 'Boolean' && typeof parameter.value === 'boolean') return parameter.value;
  if (parameter.kind === 'Number' && typeof parameter.value === 'number' && Number.isFinite(parameter.value)) return parameter.value;
  if ((parameter.kind === 'String' || parameter.kind === 'EquipmentPath') && typeof parameter.value === 'string') return parameter.value;
  return undefined;
}

function dynamoParameterFromTarget(target: string): string | null {
  const match = /^\{dynamoParameter:([^{}]+)\}$/.exec(target.trim());
  return match?.[1]?.trim() || null;
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
