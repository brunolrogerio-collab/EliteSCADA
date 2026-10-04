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
  if (element.type === 'core.text' && typeof properties.text === 'string') {
    const textParameterKey = parameterToken(properties.text);
    const textParameter = textParameterKey ? findParameter(parameters, textParameterKey) : undefined;
    if (textParameter?.kind === 'String' && typeof textParameter.value === 'string') properties.text = textParameter.value;
  }
  if (element.metadata?.dynamoStateLabelIndex !== undefined) {
    const placementParameter = findParameter(parameters, 'labelPosition');
    if (placementParameter?.kind === 'String' && typeof placementParameter.value === 'string') {
      const placement = placementParameter.value.trim().toLowerCase();
      const [x, y, width, height] = placement === 'above' ? [0, 0, 132, 16]
        : placement === 'on' ? [0, 42, 132, 16]
          : placement === 'left' ? [0, 42, 36, 16]
            : placement === 'right' ? [96, 42, 36, 16]
              : [0, 82, 132, 16];
      Object.assign(properties, { x, y, width, height });
    }
  }
  const outlineColorKey = element.metadata?.dynamoOutlineColorParameter?.trim();
  const outlineColor = outlineColorKey ? findParameter(parameters, outlineColorKey) : undefined;
  if (outlineColor?.kind === 'String' && typeof outlineColor.value === 'string' && outlineColor.value.trim()) {
    if (element.type === 'core.svgSymbol') {
      const currentOverrides = recordValue(properties.svgPaintOverrides);
      const currentSlots = recordValue(currentOverrides?.slots);
      const bezel = recordValue(currentSlots?.bezel);
      const state = recordValue(currentSlots?.state);
      properties.svgPaintOverrides = Object.freeze({
        version: 1,
        palette: Object.freeze(recordValue(currentOverrides?.palette) ?? {}),
        slots: Object.freeze({
          ...(currentSlots ?? {}),
          bezel: Object.freeze({ ...(bezel ?? {}), stroke: outlineColor.value }),
          state: Object.freeze({ ...(state ?? {}), stroke: outlineColor.value })
        })
      });
    } else {
      properties.strokeColor = outlineColor.value;
    }
  }
  const depthEffectKey = element.metadata?.dynamo3dEffectParameter?.trim();
  const depthEffect = depthEffectKey ? findParameter(parameters, depthEffectKey) : undefined;
  if (depthEffect?.kind === 'Boolean' && typeof depthEffect.value === 'boolean') {
    properties.shadowEnabled = depthEffect.value;
    if (depthEffect.value) {
      Object.assign(properties, {
        shadowColor: '#24374699', shadowOffsetX: 1, shadowOffsetY: 2, shadowBlur: 2
      });
    }
  }
  if (!animationEnabled) {
    const stateParameterKey = element.metadata?.dynamoFixedStateParameter?.trim();
    const stateParameter = stateParameterKey ? findParameter(parameters, stateParameterKey) : undefined;
    const labelIndex = Number(element.metadata?.dynamoStateLabelIndex);
    if (Number.isInteger(labelIndex) && stateParameter?.kind === 'Number' && typeof stateParameter.value === 'number') {
      properties.visible = Math.trunc(stateParameter.value) === labelIndex;
    }
    const profile = element.metadata?.dynamoStateColorProfile?.split(',').map(value => value.trim()) ?? [];
    const stateIndex = stateParameter?.kind === 'Number' && typeof stateParameter.value === 'number'
      ? Math.trunc(stateParameter.value)
      : -1;
    const fixedProperty = element.metadata?.dynamoFixedStateProperty?.trim();
    const fixedValues = element.metadata?.dynamoFixedStatePropertyValues?.split(',').map(value => Number(value.trim())) ?? [];
    if (fixedProperty && stateIndex >= 0 && stateIndex < fixedValues.length && Number.isFinite(fixedValues[stateIndex])) {
      properties[fixedProperty] = fixedValues[stateIndex];
    }
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
      const overriddenStateSource = stateParameter?.kind === 'TagReference' && stateParameter.tagReference
        ? Object.freeze({ ...propertyMap.source, target: null, tagReference: cloneTagReference(stateParameter.tagReference) })
        : stateParameter?.kind === 'ValueSource' && stateParameter.valueSource
          ? Object.freeze({ ...propertyMap.source, valueType: stateParameter.valueSource.valueType, target: `{dynamoParameter:${stateParameterKey}}`, tagReference: null })
        : propertyMap.source;
      const projectedStateSource = projectValueSource(overriddenStateSource, parameters, equipmentPath);
      const invertParameterKey = element.metadata?.dynamoBooleanStateInvertParameter?.trim();
      const invertParameter = invertParameterKey ? findParameter(parameters, invertParameterKey) : undefined;
      const invertBoolean = invertParameter?.kind === 'Boolean' && invertParameter.value === true;
      return Object.freeze({
        ...propertyMap,
        rules: Object.freeze(rules),
        source: projectedStateSource.valueType === 'Boolean' && propertyMap.source.valueType === 'Number'
          ? booleanSourceToNumber(projectedStateSource, invertBoolean)
          : projectedStateSource
      });
    }) : undefined,
    booleanConditions: animationEnabled ? element.booleanConditions?.map(condition => Object.freeze({
      ...condition,
      source: projectValueSource(condition.source, parameters, equipmentPath)
    })) : undefined,
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
  const target = source.target === null || source.target === undefined
    ? source.target
    : substituteEquipmentPath(source.target, equipmentPath);
  const parameterKey = dynamoParameterFromTarget(target ?? '');
  const parameter = parameterKey ? findParameter(parameters, parameterKey) : undefined;
  if (parameter?.kind === 'ValueSource' && parameter.valueSource) {
    const replacement = parameter.valueSource;
    if (replacement.valueType !== source.valueType) {
      throw new Error(`Dynamo value-source parameter '${parameter.key}' produces ${replacement.valueType}, but the visual behavior requires ${source.valueType}.`);
    }
    return Object.freeze({
      ...replacement,
      target: substituteEquipmentPath(replacement.target ?? '', equipmentPath) || replacement.target,
      tagReference: replacement.tagReference ? cloneTagReference(replacement.tagReference) : replacement.tagReference,
      expression: replacement.expression ? Object.freeze({
        ...replacement.expression,
        dependencies: replacement.expression.dependencies
          ? Object.freeze(replacement.expression.dependencies.map(dependency => Object.freeze({
              ...dependency,
              tagReference: cloneTagReference(dependency.tagReference),
              target: substituteEquipmentPath(dependency.target ?? '', equipmentPath) || dependency.target
            })))
          : replacement.expression.dependencies
      }) : replacement.expression
    });
  }
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

function booleanSourceToNumber(
  source: VisualValueSourceEngineering,
  invert: boolean
): VisualValueSourceEngineering {
  if (source.valueType !== 'Boolean') return source;
  if (source.kind === 'Expression' && source.expression) {
    const inner = `(${source.expression.text})`;
    return Object.freeze({
      kind: 'Expression',
      valueType: 'Number',
      expression: Object.freeze({
        text: invert ? `number(not ${inner})` : `number(${inner})`,
        resultType: 'Number',
        dependencies: source.expression.dependencies ? Object.freeze([...source.expression.dependencies]) : source.expression.dependencies
      })
    });
  }
  if (!source.tagReference?.tagId.trim()) {
    throw new Error('Boolean Dynamo state sources require a stable TAG or Client Memory identity.');
  }
  return Object.freeze({
    kind: 'Expression',
    valueType: 'Number',
    expression: Object.freeze({
      text: invert ? 'number(not source)' : 'number(source)',
      resultType: 'Number',
      dependencies: Object.freeze([{
        symbol: 'source',
        kind: source.kind === 'ClientMemory' ? 'ClientMemory' as const : 'Tag' as const,
        valueType: 'Boolean' as const,
        tagReference: cloneTagReference(source.tagReference),
        target: source.target
      }])
    })
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

function recordValue(value: VisualEngineeringPropertyValue | undefined): Readonly<Record<string, VisualEngineeringPropertyValue>> | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as Readonly<Record<string, VisualEngineeringPropertyValue>>
    : null;
}
