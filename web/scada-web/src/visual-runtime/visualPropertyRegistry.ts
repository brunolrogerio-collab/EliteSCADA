import {
  cloneVisualPropertyValue,
  isAssetReference,
  VisualPropertyContractError,
  type AssetRefVisualPropertyDefinition,
  type BooleanVisualPropertyDefinition,
  type ColorVisualPropertyDefinition,
  type EnumVisualPropertyDefinition,
  type NumberVisualPropertyDefinition,
  type StringVisualPropertyDefinition,
  type VisualPropertyDefinition,
  type VisualPropertyValidationResult,
  type VisualPropertyValue
} from './visualPropertyTypes';

export const VISUAL_PROPERTY_KEYS = {
  x: 'x',
  y: 'y',
  width: 'width',
  height: 'height',
  rotation: 'rotation',
  scaleX: 'scaleX',
  scaleY: 'scaleY',
  zIndex: 'zIndex',
  visible: 'visible',
  opacity: 'opacity',
  fillColor: 'fillColor',
  strokeColor: 'strokeColor',
  strokeWidth: 'strokeWidth',
  cornerRadius: 'cornerRadius',
  text: 'text',
  textColor: 'textColor',
  fontSize: 'fontSize',
  assetRef: 'assetRef',
  imageFit: 'imageFit'
} as const;

export type CommonVisualPropertyKey = typeof VISUAL_PROPERTY_KEYS[keyof typeof VISUAL_PROPERTY_KEYS];

export const IMAGE_FIT_VALUES = ['contain', 'cover', 'fill', 'native'] as const;
export type ImageFitValue = typeof IMAGE_FIT_VALUES[number];

export class VisualPropertyRegistry {
  readonly #definitions = new Map<string, VisualPropertyDefinition>();

  constructor(definitions: readonly VisualPropertyDefinition[]) {
    for (const candidate of definitions) {
      const definition = freezeDefinition(candidate);
      validateDefinition(definition);
      if (this.#definitions.has(definition.key)) {
        throw new VisualPropertyContractError(
          'definition.duplicateKey',
          `Visual property '${definition.key}' is already registered.`,
          definition.key
        );
      }
      this.#definitions.set(definition.key, definition);
    }
  }

  has(propertyKey: string): boolean {
    return this.#definitions.has(propertyKey);
  }

  get(propertyKey: string): VisualPropertyDefinition | undefined {
    return this.#definitions.get(propertyKey);
  }

  getRequired(propertyKey: string): VisualPropertyDefinition {
    const definition = this.#definitions.get(propertyKey);
    if (!definition) {
      throw new VisualPropertyContractError(
        'property.unregistered',
        `Visual property '${propertyKey}' is not registered.`,
        propertyKey
      );
    }
    return definition;
  }

  list(): readonly VisualPropertyDefinition[] {
    return Object.freeze([...this.#definitions.values()]);
  }

  validate(propertyKey: string, value: unknown): VisualPropertyValidationResult {
    const definition = this.#definitions.get(propertyKey);
    if (!definition) {
      return { ok: false, propertyKey, code: 'property.unregistered' };
    }
    return validateValueForDefinition(definition, value);
  }
}

export class VisualObjectPropertySchema {
  readonly objectTypeKey: string;
  readonly #registry: VisualPropertyRegistry;
  readonly #propertyKeys: readonly string[];
  readonly #declaredKeys: ReadonlySet<string>;

  constructor(
    objectTypeKey: string,
    propertyKeys: readonly string[],
    registry: VisualPropertyRegistry = COMMON_VISUAL_PROPERTY_REGISTRY
  ) {
    if (!isStableRegistryToken(objectTypeKey)) {
      throw new VisualPropertyContractError(
        'schema.invalidObjectType',
        'Visual object type key must be a stable non-path token.'
      );
    }

    const declared = new Set<string>();
    for (const propertyKey of propertyKeys) {
      registry.getRequired(propertyKey);
      if (!declared.add(propertyKey)) {
        throw new VisualPropertyContractError(
          'schema.duplicateProperty',
          `Visual object type '${objectTypeKey}' declares '${propertyKey}' more than once.`,
          propertyKey
        );
      }
    }

    this.objectTypeKey = objectTypeKey;
    this.#registry = registry;
    this.#propertyKeys = Object.freeze([...declared]);
    this.#declaredKeys = declared;
  }

  get propertyKeys(): readonly string[] {
    return this.#propertyKeys;
  }

  declares(propertyKey: string): boolean {
    return this.#declaredKeys.has(propertyKey);
  }

  getRequired(propertyKey: string): VisualPropertyDefinition {
    if (!this.declares(propertyKey)) {
      throw new VisualPropertyContractError(
        'schema.propertyNotDeclared',
        `Visual object type '${this.objectTypeKey}' does not declare '${propertyKey}'.`,
        propertyKey
      );
    }
    return this.#registry.getRequired(propertyKey);
  }

  definitions(): readonly VisualPropertyDefinition[] {
    return Object.freeze(this.#propertyKeys.map(key => this.#registry.getRequired(key)));
  }

  createDefaultBaseValues(): Readonly<Record<string, VisualPropertyValue>> {
    const values: Record<string, VisualPropertyValue> = Object.create(null) as Record<string, VisualPropertyValue>;
    for (const propertyKey of this.#propertyKeys) {
      const definition = this.#registry.getRequired(propertyKey);
      values[propertyKey] = cloneVisualPropertyValue(definition.defaultValue);
    }
    return Object.freeze(values);
  }

  validate(propertyKey: string, value: unknown): VisualPropertyValidationResult {
    if (!this.declares(propertyKey)) {
      return { ok: false, propertyKey, code: 'property.unregistered' };
    }
    return this.#registry.validate(propertyKey, value);
  }
}

const COMMON_FLAGS = {
  engineeringEditable: true,
  runtimeReadable: true,
  runtimeWritable: true,
  supportsBinding: true
} as const;

function numberProperty(
  key: string,
  defaultValue: number,
  options: Readonly<{
    minimum?: number;
    maximum?: number;
    integer?: boolean;
    animatable?: boolean;
    unit?: string;
    category?: string;
  }> = {}
): NumberVisualPropertyDefinition {
  return {
    key,
    type: 'number',
    defaultValue,
    ...COMMON_FLAGS,
    animatable: options.animatable ?? false,
    minimum: options.minimum,
    maximum: options.maximum,
    integer: options.integer,
    unit: options.unit,
    category: options.category
  };
}

function booleanProperty(
  key: string,
  defaultValue: boolean,
  category: string
): BooleanVisualPropertyDefinition {
  return {
    key,
    type: 'boolean',
    defaultValue,
    ...COMMON_FLAGS,
    animatable: false,
    category
  };
}

function stringProperty(
  key: string,
  defaultValue: string,
  category: string
): StringVisualPropertyDefinition {
  return {
    key,
    type: 'string',
    defaultValue,
    ...COMMON_FLAGS,
    animatable: false,
    category
  };
}

function colorProperty(
  key: string,
  defaultValue: string,
  category: string
): ColorVisualPropertyDefinition {
  return {
    key,
    type: 'color',
    defaultValue,
    ...COMMON_FLAGS,
    animatable: true,
    category
  };
}

function enumProperty(
  key: string,
  defaultValue: string,
  allowedValues: readonly string[],
  category: string
): EnumVisualPropertyDefinition {
  return {
    key,
    type: 'enum',
    defaultValue,
    ...COMMON_FLAGS,
    animatable: false,
    allowedValues,
    category
  };
}

const COMMON_VISUAL_PROPERTY_DEFINITIONS: readonly VisualPropertyDefinition[] = [
  numberProperty(VISUAL_PROPERTY_KEYS.x, 0, { animatable: true, unit: 'px', category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.y, 0, { animatable: true, unit: 'px', category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.width, 100, { minimum: 0, animatable: true, unit: 'px', category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.height, 100, { minimum: 0, animatable: true, unit: 'px', category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.rotation, 0, { animatable: true, unit: 'deg', category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.scaleX, 1, { minimum: 0, animatable: true, category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.scaleY, 1, { minimum: 0, animatable: true, category: 'geometry' }),
  numberProperty(VISUAL_PROPERTY_KEYS.zIndex, 0, { integer: true, category: 'geometry' }),
  booleanProperty(VISUAL_PROPERTY_KEYS.visible, true, 'appearance'),
  numberProperty(VISUAL_PROPERTY_KEYS.opacity, 1, { minimum: 0, maximum: 1, animatable: true, category: 'appearance' }),
  colorProperty(VISUAL_PROPERTY_KEYS.fillColor, '#00000000', 'appearance'),
  colorProperty(VISUAL_PROPERTY_KEYS.strokeColor, '#000000', 'appearance'),
  numberProperty(VISUAL_PROPERTY_KEYS.strokeWidth, 1, { minimum: 0, animatable: true, unit: 'px', category: 'appearance' }),
  numberProperty(VISUAL_PROPERTY_KEYS.cornerRadius, 0, { minimum: 0, animatable: true, unit: 'px', category: 'appearance' }),
  stringProperty(VISUAL_PROPERTY_KEYS.text, '', 'text'),
  colorProperty(VISUAL_PROPERTY_KEYS.textColor, '#000000', 'text'),
  numberProperty(VISUAL_PROPERTY_KEYS.fontSize, 14, { minimum: 1, animatable: true, unit: 'px', category: 'text' }),
  {
    key: VISUAL_PROPERTY_KEYS.assetRef,
    type: 'assetRef',
    defaultValue: { assetId: 'asset:none' },
    engineeringEditable: true,
    runtimeReadable: true,
    runtimeWritable: false,
    supportsBinding: false,
    animatable: false,
    category: 'image',
    presentationHint: 'project-asset'
  } satisfies AssetRefVisualPropertyDefinition,
  enumProperty(VISUAL_PROPERTY_KEYS.imageFit, 'contain', IMAGE_FIT_VALUES, 'image')
];

export const COMMON_VISUAL_PROPERTY_REGISTRY = new VisualPropertyRegistry(
  COMMON_VISUAL_PROPERTY_DEFINITIONS
);

function validateDefinition(definition: VisualPropertyDefinition): void {
  if (!/^[A-Za-z][A-Za-z0-9]*$/.test(definition.key)) {
    throw new VisualPropertyContractError(
      'definition.invalidKey',
      `Visual property key '${definition.key}' is not stable.`,
      definition.key
    );
  }

  if (definition.type === 'number') {
    if (definition.minimum !== undefined && !Number.isFinite(definition.minimum)) {
      throw new VisualPropertyContractError('definition.invalidMinimum', 'Numeric minimum must be finite.', definition.key);
    }
    if (definition.maximum !== undefined && !Number.isFinite(definition.maximum)) {
      throw new VisualPropertyContractError('definition.invalidMaximum', 'Numeric maximum must be finite.', definition.key);
    }
    if (definition.minimum !== undefined && definition.maximum !== undefined && definition.minimum > definition.maximum) {
      throw new VisualPropertyContractError('definition.invalidRange', 'Numeric minimum cannot exceed maximum.', definition.key);
    }
  }

  if (definition.type === 'enum') {
    if (definition.allowedValues.length === 0) {
      throw new VisualPropertyContractError('definition.enumEmpty', 'Enum properties require allowed values.', definition.key);
    }
    const values = new Set(definition.allowedValues);
    if (values.size !== definition.allowedValues.length || definition.allowedValues.some(value => value.length === 0 || value !== value.trim())) {
      throw new VisualPropertyContractError('definition.enumInvalid', 'Enum allowed values must be unique, non-empty and trimmed.', definition.key);
    }
  }

  if (definition.animatable && definition.type !== 'number' && definition.type !== 'color') {
    throw new VisualPropertyContractError(
      'definition.invalidAnimationType',
      `Property '${definition.key}' is animatable but type '${definition.type}' is not supported for animation.`,
      definition.key
    );
  }

  const defaultValidation = validateValueForDefinition(definition, definition.defaultValue);
  if (!defaultValidation.ok) {
    throw new VisualPropertyContractError(
      `definition.${defaultValidation.code}`,
      `Default value for '${definition.key}' is invalid: ${defaultValidation.code}.`,
      definition.key
    );
  }
}

function validateValueForDefinition(
  definition: VisualPropertyDefinition,
  value: unknown
): VisualPropertyValidationResult {
  switch (definition.type) {
    case 'number': {
      if (typeof value !== 'number') return failure(definition.key, 'value.type', 'Expected number.');
      if (!Number.isFinite(value)) return failure(definition.key, 'number.nonFinite');
      if (definition.integer === true && !Number.isInteger(value)) return failure(definition.key, 'number.integer');
      if (definition.minimum !== undefined && value < definition.minimum) return failure(definition.key, 'number.minimum');
      if (definition.maximum !== undefined && value > definition.maximum) return failure(definition.key, 'number.maximum');
      return success(definition.key, value);
    }
    case 'boolean':
      return typeof value === 'boolean'
        ? success(definition.key, value)
        : failure(definition.key, 'value.type', 'Expected boolean.');
    case 'string':
      return typeof value === 'string'
        ? success(definition.key, value)
        : failure(definition.key, 'value.type', 'Expected string.');
    case 'color':
      if (typeof value !== 'string') return failure(definition.key, 'value.type', 'Expected color string.');
      return /^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$/.test(value)
        ? success(definition.key, value)
        : failure(definition.key, 'color.format', 'Expected #RRGGBB or #RRGGBBAA.');
    case 'enum':
      if (typeof value !== 'string') return failure(definition.key, 'value.type', 'Expected enum string.');
      return definition.allowedValues.includes(value)
        ? success(definition.key, value)
        : failure(definition.key, 'enum.value');
    case 'assetRef':
      return isAssetReference(value)
        ? success(definition.key, Object.freeze({ ...value }))
        : failure(definition.key, 'assetRef.shape', 'Expected a stable project AssetReference.');
  }
}

function success(propertyKey: string, value: VisualPropertyValue): VisualPropertyValidationResult {
  return { ok: true, propertyKey, value: cloneVisualPropertyValue(value) };
}

function failure(
  propertyKey: string,
  code: Exclude<VisualPropertyValidationResult, { ok: true }>['code'],
  detail?: string
): VisualPropertyValidationResult {
  return { ok: false, propertyKey, code, detail };
}

function freezeDefinition(definition: VisualPropertyDefinition): VisualPropertyDefinition {
  if (definition.type === 'enum') {
    return Object.freeze({
      ...definition,
      allowedValues: Object.freeze([...definition.allowedValues])
    });
  }
  if (definition.type === 'assetRef') {
    return Object.freeze({
      ...definition,
      defaultValue: Object.freeze({ ...definition.defaultValue })
    });
  }
  return Object.freeze({ ...definition });
}

function isStableRegistryToken(value: string): boolean {
  return /^[A-Za-z0-9][A-Za-z0-9._:-]{0,159}$/.test(value);
}
