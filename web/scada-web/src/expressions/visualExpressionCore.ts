import type { TagValueReferenceEngineering } from '../engineering/types';
import { projectTagValueReference } from '../engineering/project-reference/tagValueReferenceProjection';

export type VisualExpressionValueType = 'boolean' | 'number';
export type VisualExpressionDependencyKind = 'tag' | 'clientMemory';

export type VisualExpressionDependency = Readonly<{
  symbol: string;
  kind: VisualExpressionDependencyKind;
  valueType: VisualExpressionValueType;
  tagReference: TagValueReferenceEngineering;
  target?: string | null;
}>;

export type VisualExpressionSourceSample = Readonly<{
  value: unknown;
  dataType: string;
  quality?: string | number | null;
  state?: string | null;
  available?: boolean;
  detail?: string | null;
}>;

export type VisualExpressionSourceResolver = (
  dependency: VisualExpressionDependency
) => VisualExpressionSourceSample | null | undefined;

export type VisualExpressionSpan = Readonly<{ start: number; end: number }>;

export type VisualExpressionDiagnosticCode =
  | 'EXPRESSION_EMPTY'
  | 'EXPRESSION_LENGTH_LIMIT'
  | 'TOKEN_LIMIT'
  | 'TOKEN_INVALID'
  | 'SYNTAX_ERROR'
  | 'AST_DEPTH_LIMIT'
  | 'DEPENDENCY_LIMIT'
  | 'DEPENDENCY_SYMBOL_INVALID'
  | 'DEPENDENCY_SYMBOL_RESERVED'
  | 'DEPENDENCY_DUPLICATE_SYMBOL'
  | 'DEPENDENCY_REFERENCE_INVALID'
  | 'DEPENDENCY_UNKNOWN'
  | 'TYPE_MISMATCH'
  | 'FUNCTION_UNKNOWN'
  | 'FUNCTION_ARGUMENT_COUNT'
  | 'RESULT_TYPE_MISMATCH'
  | 'DEPENDENCY_UNAVAILABLE'
  | 'DEPENDENCY_QUALITY_UNUSABLE'
  | 'DEPENDENCY_VALUE_INVALID'
  | 'DIVISION_BY_ZERO'
  | 'NON_FINITE_RESULT'
  | 'INVALID_OPERATION'
  | 'OPERATION_LIMIT';

export type VisualExpressionDiagnostic = Readonly<{
  code: VisualExpressionDiagnosticCode;
  message: string;
  start: number;
  end: number;
}>;

export type VisualExpressionUnaryOperator = 'not' | '+' | '-';
export type VisualExpressionBinaryOperator =
  | 'or'
  | 'and'
  | '=='
  | '!='
  | '<'
  | '<='
  | '>'
  | '>='
  | '+'
  | '-'
  | '*'
  | '/'
  | '%';
export type VisualExpressionFunctionName =
  | 'abs'
  | 'min'
  | 'max'
  | 'clamp'
  | 'round'
  | 'floor'
  | 'ceil'
  | 'bool'
  | 'number';

export type VisualExpressionNode =
  | Readonly<{
      kind: 'literal';
      valueType: VisualExpressionValueType;
      value: boolean | number;
      start: number;
      end: number;
      depth: number;
    }>
  | Readonly<{
      kind: 'dependency';
      valueType: VisualExpressionValueType;
      symbol: string;
      start: number;
      end: number;
      depth: number;
    }>
  | Readonly<{
      kind: 'unary';
      valueType: VisualExpressionValueType;
      operator: VisualExpressionUnaryOperator;
      operand: VisualExpressionNode;
      start: number;
      end: number;
      depth: number;
    }>
  | Readonly<{
      kind: 'binary';
      valueType: VisualExpressionValueType;
      operator: VisualExpressionBinaryOperator;
      left: VisualExpressionNode;
      right: VisualExpressionNode;
      start: number;
      end: number;
      depth: number;
    }>
  | Readonly<{
      kind: 'call';
      valueType: VisualExpressionValueType;
      functionName: VisualExpressionFunctionName;
      arguments: readonly VisualExpressionNode[];
      start: number;
      end: number;
      depth: number;
    }>;

export type VisualExpressionLimits = Readonly<{
  maxLength: number;
  maxTokens: number;
  maxAstDepth: number;
  maxOperations: number;
  maxDependencies: number;
}>;

export const VISUAL_EXPRESSION_DEFAULT_LIMITS: VisualExpressionLimits = Object.freeze({
  maxLength: 4096,
  maxTokens: 1024,
  maxAstDepth: 64,
  maxOperations: 4096,
  maxDependencies: 128
});

export type CompiledVisualExpression = Readonly<{
  text: string;
  resultType: VisualExpressionValueType;
  root: VisualExpressionNode;
  dependencies: readonly VisualExpressionDependency[];
  dependencyLocations: Readonly<Record<string, VisualExpressionSpan>>;
  limits: VisualExpressionLimits;
}>;

export type VisualExpressionCompileResult =
  | Readonly<{ ok: true; expression: CompiledVisualExpression }>
  | Readonly<{ ok: false; diagnostics: readonly VisualExpressionDiagnostic[] }>;

export type VisualExpressionEvaluationResult =
  | Readonly<{
      ok: true;
      value: boolean | number;
      valueType: VisualExpressionValueType;
      operationCount: number;
    }>
  | Readonly<{
      ok: false;
      diagnostic: VisualExpressionDiagnostic;
      operationCount: number;
    }>;

const DEPENDENCY_SYMBOL = /^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)*$/;
const RESERVED_SYMBOLS = new Set([
  'and', 'or', 'not', 'true', 'false',
  'abs', 'min', 'max', 'clamp', 'round', 'floor', 'ceil', 'bool', 'number'
]);
const NUMERIC_DATA_TYPES = new Set(['int16', 'int32', 'int64', 'float', 'double', 'enum']);

export function compileVisualExpression(
  text: string,
  resultType: VisualExpressionValueType,
  dependencies: readonly VisualExpressionDependency[],
  limitsOverride?: Partial<VisualExpressionLimits>
): VisualExpressionCompileResult {
  const limits = normalizeLimits(limitsOverride);
  const dependencyValidation = validateDependencies(dependencies, limits);
  if (dependencyValidation.diagnostics.length > 0) {
    return Object.freeze({ ok: false, diagnostics: Object.freeze(dependencyValidation.diagnostics) });
  }

  if (!text || text.trim().length === 0) {
    return failure(diagnostic('EXPRESSION_EMPTY', 'Expression text is required.', 0, text?.length ?? 0));
  }
  if (text.length > limits.maxLength) {
    return failure(diagnostic(
      'EXPRESSION_LENGTH_LIMIT',
      `Expression exceeds the ${limits.maxLength} character limit.`,
      0,
      text.length
    ));
  }
  if (resultType !== 'boolean' && resultType !== 'number') {
    return failure(diagnostic('RESULT_TYPE_MISMATCH', 'Expression result type must be Boolean or Number.', 0, text.length));
  }

  try {
    const lexer = new Lexer(text, limits);
    const tokens = lexer.tokenize();
    const parser = new Parser(text, tokens, dependencyValidation.bySymbol, limits);
    const root = parser.parse();
    if (root.valueType !== resultType) {
      return failure(diagnostic(
        'RESULT_TYPE_MISMATCH',
        `Expression produces ${displayType(root.valueType)} but the destination requires ${displayType(resultType)}.`,
        root.start,
        root.end
      ));
    }

    const usedDependencies = Object.freeze(parser.usedDependencies());
    const locations = Object.freeze(parser.dependencyLocations());
    return Object.freeze({
      ok: true,
      expression: Object.freeze({
        text,
        resultType,
        root,
        dependencies: usedDependencies,
        dependencyLocations: locations,
        limits
      })
    });
  } catch (reason) {
    if (reason instanceof CompileFailure) return failure(reason.diagnostic);
    throw reason;
  }
}

export function evaluateVisualExpression(
  expression: CompiledVisualExpression,
  resolveSource: VisualExpressionSourceResolver
): VisualExpressionEvaluationResult {
  const values = new Map<string, boolean | number>();
  let operationCount = 0;

  for (const dependency of expression.dependencies) {
    const normalizedSymbol = normalizeSymbol(dependency.symbol);
    const span = expression.dependencyLocations[normalizedSymbol] ?? { start: 0, end: expression.text.length };
    let sample: VisualExpressionSourceSample | null | undefined;
    try {
      sample = resolveSource(dependency);
    } catch {
      return evaluationFailure(
        diagnostic('DEPENDENCY_UNAVAILABLE', `Dependency '${dependency.symbol}' could not be resolved.`, span.start, span.end),
        operationCount
      );
    }

    const resolved = resolveDependencyValue(dependency, sample, span);
    if (!resolved.ok) return evaluationFailure(resolved.diagnostic, operationCount);
    values.set(normalizedSymbol, resolved.value);
  }

  const evaluation = evaluateNode(expression.root, values, expression.limits, { count: 0 });
  operationCount = evaluation.operationCount;
  if (!evaluation.ok) return evaluationFailure(evaluation.diagnostic, operationCount);
  return Object.freeze({
    ok: true,
    value: evaluation.value,
    valueType: expression.resultType,
    operationCount
  });
}

type DependencyValidation = Readonly<{
  bySymbol: ReadonlyMap<string, VisualExpressionDependency>;
  diagnostics: VisualExpressionDiagnostic[];
}>;

function validateDependencies(
  dependencies: readonly VisualExpressionDependency[],
  limits: VisualExpressionLimits
): DependencyValidation {
  const diagnostics: VisualExpressionDiagnostic[] = [];
  const bySymbol = new Map<string, VisualExpressionDependency>();
  if (dependencies.length > limits.maxDependencies) {
    diagnostics.push(diagnostic(
      'DEPENDENCY_LIMIT',
      `Expression declares ${dependencies.length} dependencies; the limit is ${limits.maxDependencies}.`,
      0,
      0
    ));
  }

  for (const dependency of dependencies.slice(0, limits.maxDependencies + 1)) {
    const symbol = dependency.symbol ?? '';
    const normalized = normalizeSymbol(symbol);
    if (!DEPENDENCY_SYMBOL.test(symbol)) {
      diagnostics.push(diagnostic(
        'DEPENDENCY_SYMBOL_INVALID',
        `Dependency symbol '${symbol}' is not a valid expression identifier.`,
        0,
        0
      ));
      continue;
    }
    if (RESERVED_SYMBOLS.has(normalized)) {
      diagnostics.push(diagnostic(
        'DEPENDENCY_SYMBOL_RESERVED',
        `Dependency symbol '${symbol}' is reserved by the expression language.`,
        0,
        0
      ));
      continue;
    }
    if (bySymbol.has(normalized)) {
      diagnostics.push(diagnostic(
        'DEPENDENCY_DUPLICATE_SYMBOL',
        `Dependency symbol '${symbol}' is duplicated case-insensitively.`,
        0,
        0
      ));
      continue;
    }
    if (!dependency.tagReference || !dependency.tagReference.tagId?.trim()) {
      diagnostics.push(diagnostic(
        'DEPENDENCY_REFERENCE_INVALID',
        `Dependency '${symbol}' requires a stable canonical source identity.`,
        0,
        0
      ));
      continue;
    }
    if (dependency.kind !== 'tag' && dependency.kind !== 'clientMemory') {
      diagnostics.push(diagnostic(
        'DEPENDENCY_REFERENCE_INVALID',
        `Dependency '${symbol}' uses unsupported source kind '${String(dependency.kind)}'.`,
        0,
        0
      ));
      continue;
    }
    if (dependency.valueType !== 'boolean' && dependency.valueType !== 'number') {
      diagnostics.push(diagnostic(
        'DEPENDENCY_REFERENCE_INVALID',
        `Dependency '${symbol}' uses unsupported value type '${String(dependency.valueType)}'.`,
        0,
        0
      ));
      continue;
    }

    const selector = dependency.tagReference.selector;
    if (selector) {
      if (
        dependency.kind !== 'tag' ||
        selector.kind !== 'bit' ||
        !Number.isInteger(selector.index) ||
        selector.index < 0 ||
        dependency.valueType !== 'boolean'
      ) {
        diagnostics.push(diagnostic(
          'DEPENDENCY_REFERENCE_INVALID',
          `Dependency '${symbol}' contains an invalid canonical TAG selector.`,
          0,
          0
        ));
        continue;
      }
    }

    bySymbol.set(normalized, dependency);
  }

  return Object.freeze({ bySymbol, diagnostics });
}

function normalizeLimits(override?: Partial<VisualExpressionLimits>): VisualExpressionLimits {
  const merged: VisualExpressionLimits = {
    maxLength: normalizePositiveLimit(override?.maxLength, VISUAL_EXPRESSION_DEFAULT_LIMITS.maxLength),
    maxTokens: normalizePositiveLimit(override?.maxTokens, VISUAL_EXPRESSION_DEFAULT_LIMITS.maxTokens),
    maxAstDepth: normalizePositiveLimit(override?.maxAstDepth, VISUAL_EXPRESSION_DEFAULT_LIMITS.maxAstDepth),
    maxOperations: normalizePositiveLimit(override?.maxOperations, VISUAL_EXPRESSION_DEFAULT_LIMITS.maxOperations),
    maxDependencies: normalizePositiveLimit(override?.maxDependencies, VISUAL_EXPRESSION_DEFAULT_LIMITS.maxDependencies)
  };
  return Object.freeze(merged);
}

function normalizePositiveLimit(value: number | undefined, fallback: number): number {
  return Number.isInteger(value) && (value ?? 0) > 0 ? value! : fallback;
}

type TokenKind =
  | 'number'
  | 'identifier'
  | 'true'
  | 'false'
  | 'and'
  | 'or'
  | 'not'
  | '('
  | ')'
  | ','
  | '+'
  | '-'
  | '*'
  | '/'
  | '%'
  | '=='
  | '!='
  | '<'
  | '<='
  | '>'
  | '>='
  | 'eof';

type Token = Readonly<{
  kind: TokenKind;
  text: string;
  start: number;
  end: number;
  numericValue?: number;
}>;

class Lexer {
  private index = 0;
  private readonly tokens: Token[] = [];

  constructor(
    private readonly source: string,
    private readonly limits: VisualExpressionLimits
  ) {}

  tokenize(): readonly Token[] {
    while (this.index < this.source.length) {
      const char = this.source[this.index]!;
      if (/\s/.test(char)) {
        this.index++;
        continue;
      }
      const start = this.index;

      if (isDigit(char) || (char === '.' && isDigit(this.source[this.index + 1] ?? ''))) {
        this.readNumber();
        continue;
      }
      if (isIdentifierStart(char)) {
        this.readIdentifier();
        continue;
      }

      const two = this.source.slice(this.index, this.index + 2);
      if (two === '==' || two === '!=' || two === '<=' || two === '>=') {
        this.index += 2;
        this.push(two as TokenKind, two, start, this.index);
        continue;
      }
      if ('()+-*/%,<>'.includes(char)) {
        this.index++;
        this.push(char as TokenKind, char, start, this.index);
        continue;
      }

      throw new CompileFailure(diagnostic(
        'TOKEN_INVALID',
        `Unsupported token '${char}'.`,
        start,
        start + 1
      ));
    }

    this.tokens.push(Object.freeze({ kind: 'eof', text: '', start: this.source.length, end: this.source.length }));
    return Object.freeze(this.tokens);
  }

  private readNumber(): void {
    const start = this.index;
    let sawDigits = false;
    while (isDigit(this.source[this.index] ?? '')) {
      sawDigits = true;
      this.index++;
    }
    if (this.source[this.index] === '.') {
      this.index++;
      while (isDigit(this.source[this.index] ?? '')) {
        sawDigits = true;
        this.index++;
      }
    }
    if (!sawDigits) {
      throw new CompileFailure(diagnostic('TOKEN_INVALID', 'Invalid numeric literal.', start, this.index));
    }
    if (this.source[this.index] === 'e' || this.source[this.index] === 'E') {
      const exponentStart = this.index;
      this.index++;
      if (this.source[this.index] === '+' || this.source[this.index] === '-') this.index++;
      const digitsStart = this.index;
      while (isDigit(this.source[this.index] ?? '')) this.index++;
      if (this.index === digitsStart) {
        throw new CompileFailure(diagnostic('TOKEN_INVALID', 'Invalid numeric exponent.', exponentStart, this.index));
      }
    }

    const raw = this.source.slice(start, this.index);
    const numericValue = Number(raw);
    if (!Number.isFinite(numericValue)) {
      throw new CompileFailure(diagnostic('NON_FINITE_RESULT', 'Numeric literals must be finite.', start, this.index));
    }
    this.push('number', raw, start, this.index, numericValue);
  }

  private readIdentifier(): void {
    const start = this.index;
    this.index++;
    while (isIdentifierPart(this.source[this.index] ?? '')) this.index++;
    while (this.source[this.index] === '.' && isIdentifierPart(this.source[this.index + 1] ?? '')) {
      this.index++;
      while (isIdentifierPart(this.source[this.index] ?? '')) this.index++;
    }

    const text = this.source.slice(start, this.index);
    const normalized = text.toLowerCase();
    const keyword: TokenKind | null = normalized === 'and' || normalized === 'or' || normalized === 'not' ||
      normalized === 'true' || normalized === 'false'
      ? normalized as TokenKind
      : null;
    this.push(keyword ?? 'identifier', text, start, this.index);
  }

  private push(kind: TokenKind, text: string, start: number, end: number, numericValue?: number): void {
    if (this.tokens.length >= this.limits.maxTokens) {
      throw new CompileFailure(diagnostic(
        'TOKEN_LIMIT',
        `Expression exceeds the ${this.limits.maxTokens} token limit.`,
        0,
        this.source.length
      ));
    }
    this.tokens.push(Object.freeze({ kind, text, start, end, ...(numericValue === undefined ? {} : { numericValue }) }));
  }
}

class Parser {
  private index = 0;
  private readonly used = new Map<string, VisualExpressionDependency>();
  private readonly locations = new Map<string, VisualExpressionSpan>();

  constructor(
    private readonly source: string,
    private readonly tokens: readonly Token[],
    private readonly dependencies: ReadonlyMap<string, VisualExpressionDependency>,
    private readonly limits: VisualExpressionLimits
  ) {}

  parse(): VisualExpressionNode {
    const expression = this.parseOr();
    const token = this.current();
    if (token.kind !== 'eof') {
      throw new CompileFailure(diagnostic(
        'SYNTAX_ERROR',
        `Unexpected token '${token.text}'.`,
        token.start,
        token.end
      ));
    }
    return expression;
  }

  usedDependencies(): readonly VisualExpressionDependency[] {
    return [...this.used.values()];
  }

  dependencyLocations(): Readonly<Record<string, VisualExpressionSpan>> {
    const result: Record<string, VisualExpressionSpan> = Object.create(null) as Record<string, VisualExpressionSpan>;
    for (const [symbol, span] of this.locations) result[symbol] = Object.freeze({ ...span });
    return result;
  }

  private parseOr(): VisualExpressionNode {
    let left = this.parseAnd();
    while (this.match('or')) {
      const operator = this.previous();
      const right = this.parseAnd();
      left = this.binary(operator, 'or', left, right, 'boolean', 'boolean');
    }
    return left;
  }

  private parseAnd(): VisualExpressionNode {
    let left = this.parseEquality();
    while (this.match('and')) {
      const operator = this.previous();
      const right = this.parseEquality();
      left = this.binary(operator, 'and', left, right, 'boolean', 'boolean');
    }
    return left;
  }

  private parseEquality(): VisualExpressionNode {
    let left = this.parseComparison();
    while (this.match('==', '!=')) {
      const operator = this.previous();
      const right = this.parseComparison();
      if (left.valueType !== right.valueType) {
        this.typeFailure(operator, `${operator.text} requires operands of the same type.`);
      }
      left = this.makeBinary(operator.kind as VisualExpressionBinaryOperator, left, right, 'boolean');
    }
    return left;
  }

  private parseComparison(): VisualExpressionNode {
    let left = this.parseAdditive();
    while (this.match('<', '<=', '>', '>=')) {
      const operator = this.previous();
      const right = this.parseAdditive();
      left = this.binary(operator, operator.kind as VisualExpressionBinaryOperator, left, right, 'number', 'boolean');
    }
    return left;
  }

  private parseAdditive(): VisualExpressionNode {
    let left = this.parseMultiplicative();
    while (this.match('+', '-')) {
      const operator = this.previous();
      const right = this.parseMultiplicative();
      left = this.binary(operator, operator.kind as VisualExpressionBinaryOperator, left, right, 'number', 'number');
    }
    return left;
  }

  private parseMultiplicative(): VisualExpressionNode {
    let left = this.parseUnary();
    while (this.match('*', '/', '%')) {
      const operator = this.previous();
      const right = this.parseUnary();
      left = this.binary(operator, operator.kind as VisualExpressionBinaryOperator, left, right, 'number', 'number');
    }
    return left;
  }

  private parseUnary(): VisualExpressionNode {
    if (this.match('not', '+', '-')) {
      const operator = this.previous();
      const operand = this.parseUnary();
      if (operator.kind === 'not' && operand.valueType !== 'boolean') {
        this.typeFailure(operator, 'not requires a Boolean operand.');
      }
      if ((operator.kind === '+' || operator.kind === '-') && operand.valueType !== 'number') {
        this.typeFailure(operator, `${operator.text} requires a Number operand.`);
      }
      return this.makeUnary(operator, operand);
    }
    return this.parsePrimary();
  }

  private parsePrimary(): VisualExpressionNode {
    if (this.match('number')) {
      const token = this.previous();
      return this.checkedNode(Object.freeze({
        kind: 'literal', valueType: 'number', value: token.numericValue!,
        start: token.start, end: token.end, depth: 1
      }));
    }
    if (this.match('true', 'false')) {
      const token = this.previous();
      return this.checkedNode(Object.freeze({
        kind: 'literal', valueType: 'boolean', value: token.kind === 'true',
        start: token.start, end: token.end, depth: 1
      }));
    }
    if (this.match('identifier')) {
      const identifier = this.previous();
      if (this.match('(')) return this.parseCall(identifier);
      return this.dependency(identifier);
    }
    if (this.match('(')) {
      const opening = this.previous();
      const expression = this.parseOr();
      const closing = this.consume(')', "Expected ')' after expression.");
      const depth = expression.depth + 1;
      if (depth > this.limits.maxAstDepth) this.depthFailure(opening.start, closing.end);
      return Object.freeze({ ...expression, start: opening.start, end: closing.end, depth }) as VisualExpressionNode;
    }

    const token = this.current();
    throw new CompileFailure(diagnostic(
      'SYNTAX_ERROR',
      token.kind === 'eof' ? 'Expected an expression.' : `Expected an expression before '${token.text}'.`,
      token.start,
      token.end
    ));
  }

  private parseCall(identifier: Token): VisualExpressionNode {
    const functionName = identifier.text.toLowerCase();
    if (!isFunctionName(functionName)) {
      throw new CompileFailure(diagnostic(
        'FUNCTION_UNKNOWN',
        `Function '${identifier.text}' is not allowed.`,
        identifier.start,
        identifier.end
      ));
    }

    const args: VisualExpressionNode[] = [];
    if (!this.check(')')) {
      do {
        args.push(this.parseOr());
      } while (this.match(','));
    }
    const closing = this.consume(')', `Expected ')' after ${functionName} arguments.`);
    const valueType = validateFunction(functionName, args, identifier, closing);
    const depth = 1 + Math.max(0, ...args.map(argument => argument.depth));
    const node = Object.freeze({
      kind: 'call' as const,
      valueType,
      functionName,
      arguments: Object.freeze(args),
      start: identifier.start,
      end: closing.end,
      depth
    });
    return this.checkedNode(node);
  }

  private dependency(token: Token): VisualExpressionNode {
    const normalized = normalizeSymbol(token.text);
    const dependency = this.dependencies.get(normalized);
    if (!dependency) {
      throw new CompileFailure(diagnostic(
        'DEPENDENCY_UNKNOWN',
        `Expression symbol '${token.text}' is not backed by a declared canonical dependency.`,
        token.start,
        token.end
      ));
    }
    if (!this.used.has(normalized)) this.used.set(normalized, dependency);
    if (!this.locations.has(normalized)) this.locations.set(normalized, Object.freeze({ start: token.start, end: token.end }));
    return this.checkedNode(Object.freeze({
      kind: 'dependency',
      valueType: dependency.valueType,
      symbol: dependency.symbol,
      start: token.start,
      end: token.end,
      depth: 1
    }));
  }

  private binary(
    operator: Token,
    kind: VisualExpressionBinaryOperator,
    left: VisualExpressionNode,
    right: VisualExpressionNode,
    operandType: VisualExpressionValueType,
    resultType: VisualExpressionValueType
  ): VisualExpressionNode {
    if (left.valueType !== operandType || right.valueType !== operandType) {
      this.typeFailure(operator, `${operator.text} requires ${displayType(operandType)} operands.`);
    }
    return this.makeBinary(kind, left, right, resultType);
  }

  private makeBinary(
    operator: VisualExpressionBinaryOperator,
    left: VisualExpressionNode,
    right: VisualExpressionNode,
    valueType: VisualExpressionValueType
  ): VisualExpressionNode {
    return this.checkedNode(Object.freeze({
      kind: 'binary',
      valueType,
      operator,
      left,
      right,
      start: left.start,
      end: right.end,
      depth: Math.max(left.depth, right.depth) + 1
    }));
  }

  private makeUnary(operatorToken: Token, operand: VisualExpressionNode): VisualExpressionNode {
    const operator = operatorToken.kind as VisualExpressionUnaryOperator;
    return this.checkedNode(Object.freeze({
      kind: 'unary',
      valueType: operator === 'not' ? 'boolean' : 'number',
      operator,
      operand,
      start: operatorToken.start,
      end: operand.end,
      depth: operand.depth + 1
    }));
  }

  private checkedNode<T extends VisualExpressionNode>(node: T): T {
    if (node.depth > this.limits.maxAstDepth) this.depthFailure(node.start, node.end);
    return node;
  }

  private depthFailure(start: number, end: number): never {
    throw new CompileFailure(diagnostic(
      'AST_DEPTH_LIMIT',
      `Expression exceeds the maximum AST depth of ${this.limits.maxAstDepth}.`,
      start,
      end
    ));
  }

  private typeFailure(token: Token, message: string): never {
    throw new CompileFailure(diagnostic('TYPE_MISMATCH', message, token.start, token.end));
  }

  private match(...kinds: readonly TokenKind[]): boolean {
    if (!kinds.includes(this.current().kind)) return false;
    this.index++;
    return true;
  }

  private consume(kind: TokenKind, message: string): Token {
    if (this.current().kind === kind) return this.tokens[this.index++]!;
    const token = this.current();
    throw new CompileFailure(diagnostic('SYNTAX_ERROR', message, token.start, token.end));
  }

  private check(kind: TokenKind): boolean {
    return this.current().kind === kind;
  }

  private current(): Token {
    return this.tokens[this.index]!;
  }

  private previous(): Token {
    return this.tokens[Math.max(0, this.index - 1)]!;
  }
}

function validateFunction(
  functionName: VisualExpressionFunctionName,
  args: readonly VisualExpressionNode[],
  identifier: Token,
  closing: Token
): VisualExpressionValueType {
  const countFailure = (expected: string): never => {
    throw new CompileFailure(diagnostic(
      'FUNCTION_ARGUMENT_COUNT',
      `${functionName} expects ${expected}; received ${args.length}.`,
      identifier.start,
      closing.end
    ));
  };
  const requireType = (type: VisualExpressionValueType): void => {
    const bad = args.find(argument => argument.valueType !== type);
    if (bad) {
      throw new CompileFailure(diagnostic(
        'TYPE_MISMATCH',
        `${functionName} requires ${displayType(type)} arguments.`,
        bad.start,
        bad.end
      ));
    }
  };

  switch (functionName) {
    case 'abs':
    case 'round':
    case 'floor':
    case 'ceil':
      if (args.length !== 1) countFailure('exactly 1 argument');
      requireType('number');
      return 'number';
    case 'min':
    case 'max':
      if (args.length < 2) countFailure('at least 2 arguments');
      requireType('number');
      return 'number';
    case 'clamp':
      if (args.length !== 3) countFailure('exactly 3 arguments');
      requireType('number');
      return 'number';
    case 'bool':
      if (args.length !== 1) countFailure('exactly 1 argument');
      requireType('number');
      return 'boolean';
    case 'number':
      if (args.length !== 1) countFailure('exactly 1 argument');
      requireType('boolean');
      return 'number';
  }
}

type ResolvedDependency =
  | Readonly<{ ok: true; value: boolean | number }>
  | Readonly<{ ok: false; diagnostic: VisualExpressionDiagnostic }>;

function resolveDependencyValue(
  dependency: VisualExpressionDependency,
  sample: VisualExpressionSourceSample | null | undefined,
  span: VisualExpressionSpan
): ResolvedDependency {
  if (!sample || sample.available === false) {
    return unresolvedDependency(dependency, 'DEPENDENCY_UNAVAILABLE', sample?.detail ?? 'Source is unavailable.', span);
  }
  if (sample.state && sample.state !== 'LocalSession') {
    return unresolvedDependency(dependency, 'DEPENDENCY_UNAVAILABLE', sample.detail ?? `Source state is '${sample.state}'.`, span);
  }
  if (sample.quality !== undefined && sample.quality !== null && !isGoodQuality(sample.quality)) {
    return unresolvedDependency(
      dependency,
      'DEPENDENCY_QUALITY_UNUSABLE',
      `Source quality '${String(sample.quality)}' is not usable.`,
      span
    );
  }
  if (sample.value === undefined || sample.value === null) {
    return unresolvedDependency(dependency, 'DEPENDENCY_UNAVAILABLE', sample.detail ?? 'Source has no usable value.', span);
  }

  let sourceValue: unknown = sample.value;
  let sourceDataType = sample.dataType;
  if (dependency.kind === 'tag' && dependency.tagReference.selector) {
    const projection = projectTagValueReference(dependency.tagReference, sourceDataType, sourceValue);
    if (!projection.ok) {
      return unresolvedDependency(
        dependency,
        'DEPENDENCY_VALUE_INVALID',
        projection.detail ?? 'Canonical TAG selector could not be projected.',
        span
      );
    }
    sourceValue = projection.value;
    sourceDataType = projection.dataType;
  }

  if (dependency.valueType === 'boolean') {
    if (sourceDataType.trim().toLowerCase() !== 'boolean' || typeof sourceValue !== 'boolean') {
      return unresolvedDependency(
        dependency,
        'DEPENDENCY_VALUE_INVALID',
        `Source value is not a canonical Boolean value (data type '${sourceDataType}').`,
        span
      );
    }
    return Object.freeze({ ok: true, value: sourceValue });
  }

  if (!NUMERIC_DATA_TYPES.has(sourceDataType.trim().toLowerCase())) {
    return unresolvedDependency(
      dependency,
      'DEPENDENCY_VALUE_INVALID',
      `Source data type '${sourceDataType}' is not numeric.`,
      span
    );
  }
  const number = toFiniteExpressionNumber(sourceValue, sourceDataType);
  if (number === null) {
    return unresolvedDependency(
      dependency,
      'DEPENDENCY_VALUE_INVALID',
      'Source numeric value cannot be represented as a finite deterministic expression number.',
      span
    );
  }
  return Object.freeze({ ok: true, value: number });
}

function unresolvedDependency(
  dependency: VisualExpressionDependency,
  code: Extract<VisualExpressionDiagnosticCode, 'DEPENDENCY_UNAVAILABLE' | 'DEPENDENCY_QUALITY_UNUSABLE' | 'DEPENDENCY_VALUE_INVALID'>,
  detail: string,
  span: VisualExpressionSpan
): ResolvedDependency {
  return Object.freeze({
    ok: false,
    diagnostic: diagnostic(code, `Dependency '${dependency.symbol}' is unavailable: ${detail}`, span.start, span.end)
  });
}

function isGoodQuality(quality: string | number): boolean {
  return quality === 0 || String(quality).trim().toLowerCase() === 'good';
}

function toFiniteExpressionNumber(value: unknown, dataType: string): number | null {
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (typeof value === 'bigint') {
    if (value < BigInt(Number.MIN_SAFE_INTEGER) || value > BigInt(Number.MAX_SAFE_INTEGER)) return null;
    return Number(value);
  }
  if (dataType.trim().toLowerCase() === 'int64' && typeof value === 'string' && /^[+-]?\d+$/.test(value.trim())) {
    try {
      const integer = BigInt(value.trim());
      if (integer < BigInt(Number.MIN_SAFE_INTEGER) || integer > BigInt(Number.MAX_SAFE_INTEGER)) return null;
      return Number(integer);
    } catch {
      return null;
    }
  }
  return null;
}

type EvaluationState = { count: number };
type NodeEvaluation =
  | Readonly<{ ok: true; value: boolean | number; operationCount: number }>
  | Readonly<{ ok: false; diagnostic: VisualExpressionDiagnostic; operationCount: number }>;

function evaluateNode(
  node: VisualExpressionNode,
  values: ReadonlyMap<string, boolean | number>,
  limits: VisualExpressionLimits,
  state: EvaluationState
): NodeEvaluation {
  state.count++;
  if (state.count > limits.maxOperations) {
    return nodeFailure(
      diagnostic('OPERATION_LIMIT', `Expression exceeded the ${limits.maxOperations} operation limit.`, node.start, node.end),
      state
    );
  }

  switch (node.kind) {
    case 'literal':
      return nodeSuccess(node.value, state);
    case 'dependency': {
      const value = values.get(normalizeSymbol(node.symbol));
      if (value === undefined) {
        return nodeFailure(diagnostic(
          'DEPENDENCY_UNAVAILABLE',
          `Dependency '${node.symbol}' was not resolved for this evaluation.`,
          node.start,
          node.end
        ), state);
      }
      return nodeSuccess(value, state);
    }
    case 'unary': {
      const operand = evaluateNode(node.operand, values, limits, state);
      if (!operand.ok) return operand;
      if (node.operator === 'not') return nodeSuccess(!asBoolean(operand.value), state);
      const numeric = asNumber(operand.value);
      return finiteResult(node.operator === '-' ? -numeric : +numeric, node, state);
    }
    case 'binary': {
      const left = evaluateNode(node.left, values, limits, state);
      if (!left.ok) return left;
      const right = evaluateNode(node.right, values, limits, state);
      if (!right.ok) return right;
      return evaluateBinary(node, left.value, right.value, state);
    }
    case 'call': {
      const args: (boolean | number)[] = [];
      for (const argument of node.arguments) {
        const evaluated = evaluateNode(argument, values, limits, state);
        if (!evaluated.ok) return evaluated;
        args.push(evaluated.value);
      }
      return evaluateCall(node, args, state);
    }
  }
}

function evaluateBinary(
  node: Extract<VisualExpressionNode, { kind: 'binary' }>,
  left: boolean | number,
  right: boolean | number,
  state: EvaluationState
): NodeEvaluation {
  switch (node.operator) {
    case 'or': return nodeSuccess(asBoolean(left) || asBoolean(right), state);
    case 'and': return nodeSuccess(asBoolean(left) && asBoolean(right), state);
    case '==': return nodeSuccess(left === right, state);
    case '!=': return nodeSuccess(left !== right, state);
    case '<': return nodeSuccess(asNumber(left) < asNumber(right), state);
    case '<=': return nodeSuccess(asNumber(left) <= asNumber(right), state);
    case '>': return nodeSuccess(asNumber(left) > asNumber(right), state);
    case '>=': return nodeSuccess(asNumber(left) >= asNumber(right), state);
    case '+': return finiteResult(asNumber(left) + asNumber(right), node, state);
    case '-': return finiteResult(asNumber(left) - asNumber(right), node, state);
    case '*': return finiteResult(asNumber(left) * asNumber(right), node, state);
    case '/':
      if (asNumber(right) === 0) {
        return nodeFailure(diagnostic('DIVISION_BY_ZERO', 'Division by zero is not allowed.', node.right.start, node.right.end), state);
      }
      return finiteResult(asNumber(left) / asNumber(right), node, state);
    case '%':
      if (asNumber(right) === 0) {
        return nodeFailure(diagnostic('DIVISION_BY_ZERO', 'Remainder by zero is not allowed.', node.right.start, node.right.end), state);
      }
      return finiteResult(asNumber(left) % asNumber(right), node, state);
  }
}

function evaluateCall(
  node: Extract<VisualExpressionNode, { kind: 'call' }>,
  args: readonly (boolean | number)[],
  state: EvaluationState
): NodeEvaluation {
  switch (node.functionName) {
    case 'abs': return finiteResult(Math.abs(asNumber(args[0]!)), node, state);
    case 'min': return finiteResult(Math.min(...args.map(asNumber)), node, state);
    case 'max': return finiteResult(Math.max(...args.map(asNumber)), node, state);
    case 'round': return finiteResult(Math.round(asNumber(args[0]!)), node, state);
    case 'floor': return finiteResult(Math.floor(asNumber(args[0]!)), node, state);
    case 'ceil': return finiteResult(Math.ceil(asNumber(args[0]!)), node, state);
    case 'bool': return nodeSuccess(asNumber(args[0]!) !== 0, state);
    case 'number': return nodeSuccess(asBoolean(args[0]!) ? 1 : 0, state);
    case 'clamp': {
      const value = asNumber(args[0]!);
      const minimum = asNumber(args[1]!);
      const maximum = asNumber(args[2]!);
      if (minimum > maximum) {
        return nodeFailure(diagnostic(
          'INVALID_OPERATION',
          'clamp minimum cannot be greater than maximum.',
          node.start,
          node.end
        ), state);
      }
      return finiteResult(Math.min(maximum, Math.max(minimum, value)), node, state);
    }
  }
}

function finiteResult(value: number, node: VisualExpressionNode, state: EvaluationState): NodeEvaluation {
  if (!Number.isFinite(value)) {
    return nodeFailure(diagnostic(
      'NON_FINITE_RESULT',
      'Expression arithmetic produced a non-finite result.',
      node.start,
      node.end
    ), state);
  }
  return nodeSuccess(value, state);
}

function nodeSuccess(value: boolean | number, state: EvaluationState): NodeEvaluation {
  return Object.freeze({ ok: true, value, operationCount: state.count });
}

function nodeFailure(diagnosticValue: VisualExpressionDiagnostic, state: EvaluationState): NodeEvaluation {
  return Object.freeze({ ok: false, diagnostic: diagnosticValue, operationCount: state.count });
}

function asNumber(value: boolean | number): number {
  return value as number;
}

function asBoolean(value: boolean | number): boolean {
  return value as boolean;
}

function isFunctionName(value: string): value is VisualExpressionFunctionName {
  return value === 'abs' || value === 'min' || value === 'max' || value === 'clamp' ||
    value === 'round' || value === 'floor' || value === 'ceil' || value === 'bool' || value === 'number';
}

function isIdentifierStart(char: string): boolean {
  return /[A-Za-z_]/.test(char);
}

function isIdentifierPart(char: string): boolean {
  return /[A-Za-z0-9_]/.test(char);
}

function isDigit(char: string): boolean {
  return char >= '0' && char <= '9';
}

function normalizeSymbol(symbol: string): string {
  return symbol.toLowerCase();
}

function displayType(type: VisualExpressionValueType): string {
  return type === 'boolean' ? 'Boolean' : 'Number';
}

function diagnostic(
  code: VisualExpressionDiagnosticCode,
  message: string,
  start: number,
  end: number
): VisualExpressionDiagnostic {
  return Object.freeze({ code, message, start, end });
}

function failure(diagnosticValue: VisualExpressionDiagnostic): VisualExpressionCompileResult {
  return Object.freeze({ ok: false, diagnostics: Object.freeze([diagnosticValue]) });
}

function evaluationFailure(
  diagnosticValue: VisualExpressionDiagnostic,
  operationCount: number
): VisualExpressionEvaluationResult {
  return Object.freeze({ ok: false, diagnostic: diagnosticValue, operationCount });
}

class CompileFailure extends Error {
  constructor(readonly diagnostic: VisualExpressionDiagnostic) {
    super(diagnostic.message);
  }
}
