import { expect, test } from '@playwright/test';
import {
  createAnalogFillEngineering,
  createDirectBooleanCondition,
  createExpressionDependency,
  createExpressionValueSource,
  createNumericIntervalCondition,
  createValueSource,
  createVisualExpressionEngineering,
  listDynamicPropertyDestinations,
  resolveDynamicAuthoringSource,
  validateVisualExpressionAuthoring
} from '../src/engineering/visual-editor/dynamic-property-editor/visualDynamicAuthoringModel';

const boolTag = {
  kind: 'Tag',
  target: 'Plant.Pump.Fault',
  label: 'Pump fault',
  dataType: 'Boolean',
  tagReference: { tagId: '11111111-1111-1111-1111-111111111111' },
  bindable: true
} as const;

const levelTag = {
  kind: 'Tag',
  target: 'Plant.Level',
  label: 'Level',
  dataType: 'Double',
  tagReference: { tagId: '22222222-2222-2222-2222-222222222222' },
  bindable: true
} as const;

const statusWord = {
  kind: 'Tag',
  target: 'Plant.Status',
  label: 'Status',
  dataType: 'Int16',
  tagReference: { tagId: '33333333-3333-3333-3333-333333333333' },
  selectorCapability: { kind: 'bit', minIndex: 0, maxIndex: 15 },
  bindable: true
} as const;

const clientFlag = {
  kind: 'ClientMemory',
  target: 'client.selection.enabled',
  label: 'Selection enabled',
  dataType: 'Boolean',
  family: 'clientMemory',
  bindable: true
} as const;

test('boolean and numeric property destinations expose the required source modes', () => {
  const destinations = listDynamicPropertyDestinations({ type: 'core.rectangle' });
  const visible = destinations.find(item => item.propertyKey === 'visible');
  const x = destinations.find(item => item.propertyKey === 'x');

  expect(visible?.sourceModes).toEqual(['Constant', 'DirectBinding', 'BooleanCondition', 'Expression']);
  expect(x?.sourceModes).toEqual(['Constant', 'DirectBinding', 'Expression']);
});

test('bit authoring resolves from canonical base TAG and persists TagId plus selector', () => {
  const source = resolveDynamicAuthoringSource([statusWord], 'Plant.Status', 3);

  expect(source.target).toBe('Plant.Status.03');
  expect(source.tagReference).toEqual({
    tagId: statusWord.tagReference.tagId,
    selector: { kind: 'bit', index: 3 }
  });
});

test('expression authoring validates with DEV1 parser and retains only used canonical dependencies', () => {
  const fault = createExpressionDependency('fault', 'Boolean', boolTag);
  const level = createExpressionDependency('level', 'Number', levelTag);
  const unused = createExpressionDependency('unused', 'Number', levelTag);

  const validation = validateVisualExpressionAuthoring('Boolean', 'fault or level > 80', [fault, level, unused]);
  expect(validation.ok).toBe(true);

  const expression = createVisualExpressionEngineering('Boolean', 'fault or level > 80', [fault, level, unused]);
  expect(expression.resultType).toBe('Boolean');
  expect(expression.dependencies?.map(item => item.symbol)).toEqual(['fault', 'level']);
  expect(expression.dependencies?.[0].tagReference.tagId).toBe(boolTag.tagReference.tagId);
});

test('expression authoring surfaces typed compile diagnostics before Apply', () => {
  const level = createExpressionDependency('level', 'Number', levelTag);
  const validation = validateVisualExpressionAuthoring('Boolean', 'level + 1', [level]);

  expect(validation.ok).toBe(false);
  expect(validation.diagnostics.some(item => item.code === 'RESULT_TYPE_MISMATCH')).toBe(true);
});

test('direct and numeric interval presets create canonical first-class conditions', () => {
  const directSource = createValueSource('Boolean', boolTag);
  const direct = createDirectBooleanCondition('visible', directSource, true);
  expect(direct).toMatchObject({ propertyKey: 'visible', kind: 'Direct', negate: true, version: 1 });

  const numericSource = createValueSource('Number', levelTag);
  const interval = createNumericIntervalCondition('visible', numericSource, {
    minimum: 20,
    maximum: 80,
    minimumInclusive: true,
    maximumInclusive: false,
    intervalMode: 'Outside'
  });
  expect(interval).toMatchObject({
    propertyKey: 'visible',
    kind: 'NumericInterval',
    minimum: 20,
    maximum: 80,
    minimumInclusive: true,
    maximumInclusive: false,
    intervalMode: 'Outside'
  });
});

test('direct Client Memory preserves its canonical client-local path without inventing a TagId', () => {
  const source = createValueSource('Boolean', clientFlag);
  expect(source).toEqual({
    kind: 'ClientMemory',
    valueType: 'Boolean',
    target: 'client.selection.enabled',
    version: 1
  });
  expect(() => createExpressionDependency('clientFlag', 'Boolean', clientFlag))
    .toThrow(/no canonical stable identity in the current integrated contract/);
});

test('expression value source and Analog Fill retain canonical typed configuration only', () => {
  const level = createExpressionDependency('level', 'Number', levelTag);
  const expression = createVisualExpressionEngineering('Number', 'level * 2', [level]);
  const source = createExpressionValueSource(expression);
  const fill = createAnalogFillEngineering(source, {
    inputMinimum: 0,
    inputMaximum: 100,
    fillColor: '#00aaff',
    direction: 'LeftToRight',
    invertScale: true
  });

  expect(fill).toMatchObject({
    inputMinimum: 0,
    inputMaximum: 100,
    fillColor: '#00AAFF',
    clamp: true,
    invertScale: true,
    direction: 'LeftToRight',
    version: 1
  });
  expect('percent' in fill).toBe(false);
});

test('invalid interval and fill configuration fail closed in authoring', () => {
  const numericSource = createValueSource('Number', levelTag);
  expect(() => createNumericIntervalCondition('visible', numericSource, {})).toThrow(/at least one bound/);
  expect(() => createAnalogFillEngineering(numericSource, {
    inputMinimum: 1,
    inputMaximum: 1,
    fillColor: '#00AAFF'
  })).toThrow(/different/);
});
