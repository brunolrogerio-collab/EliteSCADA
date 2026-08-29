import {
  compileVisualExpression as compileVisualExpressionCore,
  evaluateVisualExpression as evaluateVisualExpressionCore,
  type CompiledVisualExpression,
  type VisualExpressionCompileResult,
  type VisualExpressionDependency,
  type VisualExpressionEvaluationResult,
  type VisualExpressionLimits,
  type VisualExpressionSourceResolver,
  type VisualExpressionSourceSample,
  type VisualExpressionValueType
} from './visualExpressionCore';

/**
 * Integration-facing expression compiler. It snapshots canonical dependencies so
 * later mutation of authoring/DTO objects cannot change the meaning of an already
 * compiled expression.
 */
export function compileVisualExpression(
  text: string,
  resultType: VisualExpressionValueType,
  dependencies: readonly VisualExpressionDependency[],
  limitsOverride?: Partial<VisualExpressionLimits>
): VisualExpressionCompileResult {
  const stableDependencies = Object.freeze(dependencies.map(snapshotDependency));
  return compileVisualExpressionCore(text, resultType, stableDependencies, limitsOverride);
}

/**
 * Integration-facing evaluator. Source quality/unavailability remains owned by
 * the core evaluator; this seam additionally rejects numeric samples whose value
 * shape does not match the declared canonical data type.
 */
export function evaluateVisualExpression(
  expression: CompiledVisualExpression,
  resolveSource: VisualExpressionSourceResolver
): VisualExpressionEvaluationResult {
  return evaluateVisualExpressionCore(expression, dependency =>
    normalizeSourceSample(dependency, resolveSource(dependency))
  );
}

function snapshotDependency(dependency: VisualExpressionDependency): VisualExpressionDependency {
  const selector = dependency.tagReference.selector;
  const tagReference = Object.freeze({
    tagId: dependency.tagReference.tagId,
    ...(selector
      ? { selector: Object.freeze({ kind: selector.kind, index: selector.index }) }
      : {})
  });

  return Object.freeze({
    symbol: dependency.symbol,
    kind: dependency.kind,
    valueType: dependency.valueType,
    tagReference,
    ...(dependency.target === undefined ? {} : { target: dependency.target })
  });
}

function normalizeSourceSample(
  dependency: VisualExpressionDependency,
  sample: VisualExpressionSourceSample | null | undefined
): VisualExpressionSourceSample | null | undefined {
  if (!sample || dependency.valueType !== 'number') return sample;
  if (isCanonicalExpressionNumber(sample.value, sample.dataType)) return sample;

  return Object.freeze({
    ...sample,
    dataType: 'InvalidNumeric'
  });
}

function isCanonicalExpressionNumber(value: unknown, dataType: string): boolean {
  const normalized = dataType.trim().toLowerCase();
  switch (normalized) {
    case 'int16':
      return typeof value === 'number' && Number.isInteger(value) && value >= -32768 && value <= 32767;

    case 'int32':
    case 'enum':
      return typeof value === 'number' && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647;

    case 'int64':
      return isSafeInt64ExpressionValue(value);

    case 'float':
    case 'double':
      return typeof value === 'number' && Number.isFinite(value);

    default:
      return false;
  }
}

function isSafeInt64ExpressionValue(value: unknown): boolean {
  if (typeof value === 'number') return Number.isSafeInteger(value);
  if (typeof value === 'bigint') {
    return value >= BigInt(Number.MIN_SAFE_INTEGER) && value <= BigInt(Number.MAX_SAFE_INTEGER);
  }
  if (typeof value !== 'string' || !/^[+-]?\d+$/.test(value.trim())) return false;

  try {
    const integer = BigInt(value.trim());
    return integer >= BigInt(Number.MIN_SAFE_INTEGER) && integer <= BigInt(Number.MAX_SAFE_INTEGER);
  } catch {
    return false;
  }
}
