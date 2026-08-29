import { expect, test } from '@playwright/test';
import {
  compileVisualExpression,
  evaluateVisualExpression,
  type CompiledVisualExpression,
  type VisualExpressionDependency,
  type VisualExpressionEvaluationResult,
  type VisualExpressionSourceSample
} from '../src/expressions';

const TAG_ID = '11111111-1111-4111-8111-111111111111';

function numericDependency(symbol = 'value'): VisualExpressionDependency {
  return Object.freeze({
    symbol,
    kind: 'tag',
    valueType: 'number',
    tagReference: Object.freeze({ tagId: TAG_ID })
  });
}

function compileOrThrow(
  text: string,
  dependencies: readonly VisualExpressionDependency[]
): CompiledVisualExpression {
  const compiled = compileVisualExpression(text, 'number', dependencies);
  if (!compiled.ok) throw new Error(compiled.diagnostics.map(item => `${item.code}: ${item.message}`).join('\n'));
  return compiled.expression;
}

function expectValueInvalid(
  expression: CompiledVisualExpression,
  sample: VisualExpressionSourceSample
): void {
  const result = evaluateVisualExpression(expression, () => sample);
  expect(result.ok).toBeFalsy();
  if (result.ok) throw new Error(`Expected invalid numeric sample, received ${String(result.value)}.`);
  expect(result.diagnostic.code).toBe('DEPENDENCY_VALUE_INVALID');
}

function expectValue(
  result: VisualExpressionEvaluationResult,
  expected: number
): void {
  expect(result.ok).toBeTruthy();
  if (!result.ok) throw new Error(`${result.diagnostic.code}: ${result.diagnostic.message}`);
  expect(result.value).toBe(expected);
}

test('integration-facing evaluator rejects numeric values that violate their canonical data type', () => {
  const expression = compileOrThrow('value + 1', [numericDependency()]);

  expectValueInvalid(expression, { value: 1.5, dataType: 'Int16', quality: 'Good' });
  expectValueInvalid(expression, { value: 32768, dataType: 'Int16', quality: 'Good' });
  expectValueInvalid(expression, { value: 2147483648, dataType: 'Int32', quality: 'Good' });
  expectValueInvalid(expression, { value: 1.25, dataType: 'Enum', quality: 'Good' });
  expectValueInvalid(expression, { value: Number.MAX_SAFE_INTEGER + 1, dataType: 'Int64', quality: 'Good' });
  expectValueInvalid(expression, { value: '9007199254740992', dataType: 'Int64', quality: 'Good' });
  expectValueInvalid(expression, { value: '3.5', dataType: 'Int64', quality: 'Good' });
  expectValueInvalid(expression, { value: '12', dataType: 'Double', quality: 'Good' });
});

test('integration-facing evaluator preserves valid canonical numeric boundary values', () => {
  const expression = compileOrThrow('value + 1', [numericDependency()]);

  expectValue(evaluateVisualExpression(expression, () => ({
    value: -32768,
    dataType: 'Int16',
    quality: 'Good'
  })), -32767);

  expectValue(evaluateVisualExpression(expression, () => ({
    value: 2147483647,
    dataType: 'Int32',
    quality: 'Good'
  })), 2147483648);

  expectValue(evaluateVisualExpression(expression, () => ({
    value: '9007199254740991',
    dataType: 'Int64',
    quality: 'Good'
  })), 9007199254740992);

  expectValue(evaluateVisualExpression(expression, () => ({
    value: 2.5,
    dataType: 'Double',
    quality: 'Good'
  })), 3.5);
});

test('integration-facing compiler snapshots dependency identity before compilation', () => {
  const dependency = {
    symbol: 'value',
    kind: 'tag' as const,
    valueType: 'number' as const,
    tagReference: { tagId: TAG_ID },
    target: 'Demo.Value'
  };

  const compiled = compileVisualExpression('value + 1', 'number', [dependency]);
  expect(compiled.ok).toBeTruthy();
  if (!compiled.ok) throw new Error(compiled.diagnostics.map(item => item.message).join('\n'));

  dependency.symbol = 'mutated';
  dependency.tagReference.tagId = '99999999-9999-4999-8999-999999999999';
  dependency.target = 'Mutated.Value';

  const stable = compiled.expression.dependencies[0]!;
  expect(stable.symbol).toBe('value');
  expect(stable.tagReference.tagId).toBe(TAG_ID);
  expect(stable.target).toBe('Demo.Value');
  expect(Object.isFrozen(stable)).toBeTruthy();
  expect(Object.isFrozen(stable.tagReference)).toBeTruthy();

  let resolvedSymbol = '';
  let resolvedTagId = '';
  const result = evaluateVisualExpression(compiled.expression, current => {
    resolvedSymbol = current.symbol;
    resolvedTagId = current.tagReference.tagId;
    return { value: 4, dataType: 'Int32', quality: 'Good' };
  });

  expectValue(result, 5);
  expect(resolvedSymbol).toBe('value');
  expect(resolvedTagId).toBe(TAG_ID);
});

test('guarded API keeps core quality and availability failures intact', () => {
  const expression = compileOrThrow('value + 1', [numericDependency()]);

  const badQuality = evaluateVisualExpression(expression, () => ({
    value: 4,
    dataType: 'Int32',
    quality: 'BadCommunication'
  }));
  expect(badQuality.ok).toBeFalsy();
  if (badQuality.ok) throw new Error('Bad quality must remain unavailable.');
  expect(badQuality.diagnostic.code).toBe('DEPENDENCY_QUALITY_UNUSABLE');

  const unavailable = evaluateVisualExpression(expression, () => ({
    value: null,
    dataType: 'Int32',
    quality: 'Good'
  }));
  expect(unavailable.ok).toBeFalsy();
  if (unavailable.ok) throw new Error('Missing source value must remain unavailable.');
  expect(unavailable.diagnostic.code).toBe('DEPENDENCY_UNAVAILABLE');
});
