import { expect, test } from '@playwright/test';
import {
  compileVisualExpression,
  evaluateVisualExpression,
  type CompiledVisualExpression,
  type VisualExpressionDependency,
  type VisualExpressionEvaluationResult,
  type VisualExpressionSourceSample,
  type VisualExpressionValueType
} from '../src/expressions/visualExpressionCore';

const TAG_ID_A = '11111111-1111-4111-8111-111111111111';
const TAG_ID_B = '22222222-2222-4222-8222-222222222222';
const TAG_ID_C = '33333333-3333-4333-8333-333333333333';
const CLIENT_MEMORY_ID = '44444444-4444-4444-8444-444444444444';

function tagDependency(
  symbol: string,
  valueType: VisualExpressionValueType,
  tagId = TAG_ID_A,
  bitIndex?: number
): VisualExpressionDependency {
  return Object.freeze({
    symbol,
    kind: 'tag',
    valueType,
    tagReference: Object.freeze({
      tagId,
      ...(bitIndex === undefined
        ? {}
        : { selector: Object.freeze({ kind: 'bit' as const, index: bitIndex }) })
    })
  });
}

function clientMemoryDependency(
  symbol: string,
  valueType: VisualExpressionValueType,
  tagId = CLIENT_MEMORY_ID
): VisualExpressionDependency {
  return Object.freeze({
    symbol,
    kind: 'clientMemory',
    valueType,
    tagReference: Object.freeze({ tagId })
  });
}

function compileOrThrow(
  text: string,
  resultType: VisualExpressionValueType,
  dependencies: readonly VisualExpressionDependency[] = [],
  limits?: Parameters<typeof compileVisualExpression>[3]
): CompiledVisualExpression {
  const compiled = compileVisualExpression(text, resultType, dependencies, limits);
  if (!compiled.ok) throw new Error(compiled.diagnostics.map(item => `${item.code}: ${item.message}`).join('\n'));
  return compiled.expression;
}

function evaluateOrThrow(
  expression: CompiledVisualExpression,
  samples: Readonly<Record<string, VisualExpressionSourceSample>>
): boolean | number {
  const result = evaluateVisualExpression(expression, dependency => samples[dependency.symbol]);
  if (!result.ok) throw new Error(`${result.diagnostic.code}: ${result.diagnostic.message}`);
  return result.value;
}

function expectEvaluationFailure(
  result: VisualExpressionEvaluationResult,
  code: Extract<VisualExpressionEvaluationResult, { ok: false }>['diagnostic']['code']
): void {
  expect(result.ok).toBeFalsy();
  if (result.ok) throw new Error(`Expected evaluation failure '${code}', received ${String(result.value)}.`);
  expect(result.diagnostic.code).toBe(code);
}

test('numeric arithmetic obeys mathematical precedence and parentheses', () => {
  const a = tagDependency('nivel1', 'number', TAG_ID_A);
  const b = tagDependency('nivel2', 'number', TAG_ID_B);

  const precedence = compileOrThrow('nivel1 + nivel2 * 3', 'number', [a, b]);
  expect(evaluateOrThrow(precedence, {
    nivel1: { value: 2, dataType: 'Double', quality: 'Good' },
    nivel2: { value: 4, dataType: 'Double', quality: 'Good' }
  })).toBe(14);

  const grouped = compileOrThrow('(nivel1 + nivel2) * 3', 'number', [a, b]);
  expect(evaluateOrThrow(grouped, {
    nivel1: { value: 2, dataType: 'Double', quality: 0 },
    nivel2: { value: 4, dataType: 'Double', quality: 0 }
  })).toBe(18);
});

test('boolean precedence is not > comparison/equality > and > or', () => {
  const a = tagDependency('a', 'boolean', TAG_ID_A);
  const b = tagDependency('b', 'boolean', TAG_ID_B);
  const c = tagDependency('c', 'boolean', TAG_ID_C);
  const expression = compileOrThrow('a or b and not c', 'boolean', [a, b, c]);

  expect(evaluateOrThrow(expression, {
    a: { value: false, dataType: 'Boolean', quality: 'Good' },
    b: { value: true, dataType: 'Boolean', quality: 'Good' },
    c: { value: false, dataType: 'Boolean', quality: 'Good' }
  })).toBe(true);

  expect(evaluateOrThrow(expression, {
    a: { value: false, dataType: 'Boolean', quality: 'Good' },
    b: { value: true, dataType: 'Boolean', quality: 'Good' },
    c: { value: true, dataType: 'Boolean', quality: 'Good' }
  })).toBe(false);
});

test('comparisons and equality are typed and deterministic', () => {
  const level = tagDependency('level', 'number');
  const permissive = tagDependency('permissive', 'boolean', TAG_ID_B);
  const expression = compileOrThrow('(level >= 20 and level < 80) == permissive', 'boolean', [level, permissive]);

  expect(evaluateOrThrow(expression, {
    level: { value: 50, dataType: 'Float', quality: 'Good' },
    permissive: { value: true, dataType: 'Boolean', quality: 'Good' }
  })).toBe(true);

  const mixedEquality = compileVisualExpression('level == permissive', 'boolean', [level, permissive]);
  expect(mixedEquality.ok).toBeFalsy();
  if (mixedEquality.ok) throw new Error('Mixed Boolean/Number equality must not compile.');
  expect(mixedEquality.diagnostics[0]?.code).toBe('TYPE_MISMATCH');
});

test('explicit bool(number) and number(boolean) conversions work without implicit coercion', () => {
  const count = tagDependency('count', 'number');
  const flag = tagDependency('flag', 'boolean', TAG_ID_B);

  const boolExpression = compileOrThrow('bool(count)', 'boolean', [count]);
  expect(evaluateOrThrow(boolExpression, { count: { value: 0, dataType: 'Int32', quality: 'Good' } })).toBe(false);
  expect(evaluateOrThrow(boolExpression, { count: { value: -2, dataType: 'Int32', quality: 'Good' } })).toBe(true);

  const numberExpression = compileOrThrow('number(flag)', 'number', [flag]);
  expect(evaluateOrThrow(numberExpression, { flag: { value: false, dataType: 'Boolean', quality: 'Good' } })).toBe(0);
  expect(evaluateOrThrow(numberExpression, { flag: { value: true, dataType: 'Boolean', quality: 'Good' } })).toBe(1);

  const implicit = compileVisualExpression('count and flag', 'boolean', [count, flag]);
  expect(implicit.ok).toBeFalsy();
  if (implicit.ok) throw new Error('Implicit numeric truthiness must not compile.');
  expect(implicit.diagnostics[0]?.code).toBe('TYPE_MISMATCH');
});

test('the pure helper whitelist is deterministic and rejects arbitrary calls', () => {
  const x = tagDependency('x', 'number');
  const helperExpression = compileOrThrow(
    'abs(-5) + min(8, 3, 4) + max(1, 7, 2) + clamp(x, 0, 10) + round(1.6) + floor(1.9) + ceil(1.1)',
    'number',
    [x]
  );
  expect(evaluateOrThrow(helperExpression, { x: { value: 15, dataType: 'Double', quality: 'Good' } })).toBe(29);

  const unknown = compileVisualExpression('sqrt(x)', 'number', [x]);
  expect(unknown.ok).toBeFalsy();
  if (unknown.ok) throw new Error('Arbitrary function calls must not compile.');
  expect(unknown.diagnostics[0]?.code).toBe('FUNCTION_UNKNOWN');
});

test('canonical integer TAG bit selector is consumed as Boolean dependency', () => {
  const bit03 = tagDependency('Word_status.03', 'boolean', TAG_ID_A, 3);
  const bit15 = tagDependency('Word_status.15', 'boolean', TAG_ID_A, 15);
  const expression = compileOrThrow('Word_status.03 or Word_status.15', 'boolean', [bit03, bit15]);

  expect(evaluateOrThrow(expression, {
    'Word_status.03': { value: 8, dataType: 'Int16', quality: 'Good' },
    'Word_status.15': { value: 8, dataType: 'Int16', quality: 'Good' }
  })).toBe(true);

  const signBit = compileOrThrow('Word_status.15', 'boolean', [bit15]);
  expect(evaluateOrThrow(signBit, {
    'Word_status.15': { value: -32768, dataType: 'Int16', quality: 'Good' }
  })).toBe(true);
});

test('friendly .NN text never creates bit identity without a structured selector', () => {
  const fakeBit = tagDependency('Word_status.03', 'boolean');
  const expression = compileOrThrow('Word_status.03', 'boolean', [fakeBit]);
  const result = evaluateVisualExpression(expression, () => ({
    value: 8,
    dataType: 'Int16',
    quality: 'Good'
  }));

  expectEvaluationFailure(result, 'DEPENDENCY_VALUE_INVALID');
});

test('bad quality, missing value and unavailable source fail closed even through boolean or', () => {
  const bit03 = tagDependency('Word_status.03', 'boolean', TAG_ID_A, 3);
  const expression = compileOrThrow('true or Word_status.03', 'boolean', [bit03]);

  expectEvaluationFailure(evaluateVisualExpression(expression, () => ({
    value: 8,
    dataType: 'Int16',
    quality: 'BadCommunication'
  })), 'DEPENDENCY_QUALITY_UNUSABLE');

  expectEvaluationFailure(evaluateVisualExpression(expression, () => ({
    value: null,
    dataType: 'Int16',
    quality: 'Good'
  })), 'DEPENDENCY_UNAVAILABLE');

  expectEvaluationFailure(evaluateVisualExpression(expression, () => null), 'DEPENDENCY_UNAVAILABLE');
});

test('Client Memory values retain client-local semantics and cannot carry TAG bit selectors', () => {
  const localFlag = clientMemoryDependency('local.flag', 'boolean');
  const expression = compileOrThrow('local.flag', 'boolean', [localFlag]);
  expect(evaluateOrThrow(expression, {
    'local.flag': { value: true, dataType: 'Boolean', state: 'LocalSession' }
  })).toBe(true);

  const invalidSelector: VisualExpressionDependency = Object.freeze({
    ...localFlag,
    tagReference: Object.freeze({ tagId: CLIENT_MEMORY_ID, selector: Object.freeze({ kind: 'bit', index: 1 }) })
  });
  const invalid = compileVisualExpression('local.flag', 'boolean', [invalidSelector]);
  expect(invalid.ok).toBeFalsy();
  if (invalid.ok) throw new Error('Client Memory must not acquire TAG bit semantics.');
  expect(invalid.diagnostics[0]?.code).toBe('DEPENDENCY_REFERENCE_INVALID');
});

test('division, remainder and non-finite arithmetic fail rather than inventing values', () => {
  const division = compileOrThrow('10 / 0', 'number');
  expectEvaluationFailure(evaluateVisualExpression(division, () => null), 'DIVISION_BY_ZERO');

  const remainder = compileOrThrow('10 % 0', 'number');
  expectEvaluationFailure(evaluateVisualExpression(remainder, () => null), 'DIVISION_BY_ZERO');

  const overflow = compileOrThrow('1e308 * 1e308', 'number');
  expectEvaluationFailure(evaluateVisualExpression(overflow, () => null), 'NON_FINITE_RESULT');
});

test('clamp rejects inverted bounds', () => {
  const expression = compileOrThrow('clamp(5, 10, 0)', 'number');
  expectEvaluationFailure(evaluateVisualExpression(expression, () => null), 'INVALID_OPERATION');
});

test('unsafe Int64 values fail rather than lose numeric precision', () => {
  const total = tagDependency('total', 'number');
  const expression = compileOrThrow('total + 1', 'number', [total]);
  expectEvaluationFailure(evaluateVisualExpression(expression, () => ({
    value: '9007199254740993',
    dataType: 'Int64',
    quality: 'Good'
  })), 'DEPENDENCY_VALUE_INVALID');
});

test('dependency declarations require stable canonical identity and unique non-reserved symbols', () => {
  const missingIdentity: VisualExpressionDependency = Object.freeze({
    symbol: 'a',
    kind: 'tag',
    valueType: 'number',
    tagReference: Object.freeze({ tagId: '' })
  });
  const missing = compileVisualExpression('a', 'number', [missingIdentity]);
  expect(missing.ok).toBeFalsy();
  if (missing.ok) throw new Error('Missing canonical identity must not compile.');
  expect(missing.diagnostics.some(item => item.code === 'DEPENDENCY_REFERENCE_INVALID')).toBeTruthy();

  const duplicate = compileVisualExpression('a', 'number', [
    tagDependency('a', 'number', TAG_ID_A),
    tagDependency('A', 'number', TAG_ID_B)
  ]);
  expect(duplicate.ok).toBeFalsy();
  if (duplicate.ok) throw new Error('Case-insensitive duplicate dependency symbols must not compile.');
  expect(duplicate.diagnostics.some(item => item.code === 'DEPENDENCY_DUPLICATE_SYMBOL')).toBeTruthy();

  const reserved = compileVisualExpression('and', 'number', [tagDependency('and', 'number')]);
  expect(reserved.ok).toBeFalsy();
  if (reserved.ok) throw new Error('Reserved expression tokens must not be dependency symbols.');
  expect(reserved.diagnostics.some(item => item.code === 'DEPENDENCY_SYMBOL_RESERVED')).toBeTruthy();
});

test('declared result type must match the typed AST root', () => {
  const level = tagDependency('level', 'number');
  const mismatch = compileVisualExpression('level + 1', 'boolean', [level]);
  expect(mismatch.ok).toBeFalsy();
  if (mismatch.ok) throw new Error('Numeric expression must not drive Boolean destination implicitly.');
  expect(mismatch.diagnostics[0]?.code).toBe('RESULT_TYPE_MISMATCH');
});

test('expression, token, AST depth and evaluation operation limits are bounded', () => {
  const lengthLimited = compileVisualExpression('12345', 'number', [], { maxLength: 4 });
  expect(lengthLimited.ok).toBeFalsy();
  if (lengthLimited.ok) throw new Error('Expression length limit must be enforced.');
  expect(lengthLimited.diagnostics[0]?.code).toBe('EXPRESSION_LENGTH_LIMIT');

  const tokenLimited = compileVisualExpression('1 + 2 + 3', 'number', [], { maxTokens: 4 });
  expect(tokenLimited.ok).toBeFalsy();
  if (tokenLimited.ok) throw new Error('Token limit must be enforced.');
  expect(tokenLimited.diagnostics[0]?.code).toBe('TOKEN_LIMIT');

  const depthLimited = compileVisualExpression('((((1))))', 'number', [], { maxAstDepth: 3 });
  expect(depthLimited.ok).toBeFalsy();
  if (depthLimited.ok) throw new Error('AST depth limit must be enforced.');
  expect(depthLimited.diagnostics[0]?.code).toBe('AST_DEPTH_LIMIT');

  const operationLimited = compileOrThrow('1 + 2 + 3', 'number', [], { maxOperations: 3 });
  expectEvaluationFailure(evaluateVisualExpression(operationLimited, () => null), 'OPERATION_LIMIT');
});

test('only dependencies actually referenced by the compiled AST are retained', () => {
  const used = tagDependency('used', 'number', TAG_ID_A);
  const extra = tagDependency('extra', 'number', TAG_ID_B);
  const expression = compileOrThrow('used + 1', 'number', [used, extra]);

  expect(expression.dependencies.map(item => item.symbol)).toEqual(['used']);
  expect(evaluateOrThrow(expression, {
    used: { value: 3, dataType: 'Int32', quality: 'Good' }
  })).toBe(4);
});
