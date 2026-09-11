export type CanonicalVisualValueSourceKind = 'Tag' | 'ClientMemory' | 'Expression';
export type CanonicalVisualExpressionValueType = 'Boolean' | 'Number';
export type CanonicalVisualExpressionDependencyKind = 'Tag' | 'ClientMemory';
export type CanonicalVisualBooleanConditionKind = 'Direct' | 'NumericInterval';
export type CanonicalVisualNumericIntervalMode = 'Inside' | 'Outside';
export type CanonicalVisualAnalogFillDirection =
  | 'BottomToTop'
  | 'TopToBottom'
  | 'LeftToRight'
  | 'RightToLeft';

export type VisualDynamicWireResult<T extends string> =
  | Readonly<{ ok: true; value: T }>
  | Readonly<{ ok: false; message: string }>;

const valueSourceKinds = Object.freeze({
  Tag: 'Tag',
  tag: 'Tag',
  ClientMemory: 'ClientMemory',
  clientMemory: 'ClientMemory',
  Expression: 'Expression',
  expression: 'Expression'
} satisfies Record<string, CanonicalVisualValueSourceKind>);

const expressionValueTypes = Object.freeze({
  Boolean: 'Boolean',
  boolean: 'Boolean',
  Number: 'Number',
  number: 'Number'
} satisfies Record<string, CanonicalVisualExpressionValueType>);

const expressionDependencyKinds = Object.freeze({
  Tag: 'Tag',
  tag: 'Tag',
  ClientMemory: 'ClientMemory',
  clientMemory: 'ClientMemory'
} satisfies Record<string, CanonicalVisualExpressionDependencyKind>);

const booleanConditionKinds = Object.freeze({
  Direct: 'Direct',
  direct: 'Direct',
  NumericInterval: 'NumericInterval',
  numericInterval: 'NumericInterval'
} satisfies Record<string, CanonicalVisualBooleanConditionKind>);

const numericIntervalModes = Object.freeze({
  Inside: 'Inside',
  inside: 'Inside',
  Outside: 'Outside',
  outside: 'Outside'
} satisfies Record<string, CanonicalVisualNumericIntervalMode>);

const analogFillDirections = Object.freeze({
  BottomToTop: 'BottomToTop',
  bottomToTop: 'BottomToTop',
  TopToBottom: 'TopToBottom',
  topToBottom: 'TopToBottom',
  LeftToRight: 'LeftToRight',
  leftToRight: 'LeftToRight',
  RightToLeft: 'RightToLeft',
  rightToLeft: 'RightToLeft'
} satisfies Record<string, CanonicalVisualAnalogFillDirection>);

export function normalizeVisualValueSourceKind(value: unknown): VisualDynamicWireResult<CanonicalVisualValueSourceKind> {
  return normalizeRequiredEnum(value, valueSourceKinds, 'Visual value source kind');
}

export function normalizeVisualExpressionValueType(value: unknown): VisualDynamicWireResult<CanonicalVisualExpressionValueType> {
  return normalizeRequiredEnum(value, expressionValueTypes, 'Visual expression value type');
}

export function normalizeVisualExpressionDependencyKind(value: unknown): VisualDynamicWireResult<CanonicalVisualExpressionDependencyKind> {
  return normalizeRequiredEnum(value, expressionDependencyKinds, 'Visual expression dependency kind');
}

export function normalizeVisualBooleanConditionKind(value: unknown): VisualDynamicWireResult<CanonicalVisualBooleanConditionKind> {
  return normalizeRequiredEnum(value, booleanConditionKinds, 'Visual Boolean Condition kind');
}

export function normalizeVisualNumericIntervalMode(
  value: unknown
): VisualDynamicWireResult<CanonicalVisualNumericIntervalMode> {
  return normalizeRequiredEnum(value, numericIntervalModes, 'Visual numeric interval mode');
}

export function normalizeVisualAnalogFillDirection(
  value: unknown
): VisualDynamicWireResult<CanonicalVisualAnalogFillDirection> {
  return normalizeRequiredEnum(value, analogFillDirections, 'Visual Analog Fill direction');
}

function normalizeRequiredEnum<T extends string>(
  value: unknown,
  supported: Readonly<Record<string, T>>,
  label: string
): VisualDynamicWireResult<T> {
  if (typeof value !== 'string') {
    return Object.freeze({ ok: false, message: `${label} '${String(value)}' is unsupported.` });
  }

  const normalized = supported[value];
  return normalized
    ? Object.freeze({ ok: true, value: normalized })
    : Object.freeze({ ok: false, message: `${label} '${value}' is unsupported.` });
}
