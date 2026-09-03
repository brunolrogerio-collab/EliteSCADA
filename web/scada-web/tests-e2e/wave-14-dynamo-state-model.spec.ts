import { expect, test } from '@playwright/test';
import {
  DYNAMO_STATE_PRECEDENCE,
  resolveDynamoVisualState
} from '../src/engineering/visual-editor/dynamo/dynamoStateModel';

test('bad, stale and unknown quality dominate every process indication', () => {
  for (const quality of ['bad', 'stale', 'unknown'] as const) {
    const resolved = resolveDynamoVisualState({
      quality,
      fault: true,
      alarm: true,
      commandIntent: 'start',
      settledState: 'active'
    });

    expect(resolved.kind).toBe('bad-quality');
    expect(resolved.priority).toBe(DYNAMO_STATE_PRECEDENCE.badQuality);
    expect(resolved.quality).toBe(quality);
  }
});

test('fault and alarm dominate operator command intent', () => {
  expect(resolveDynamoVisualState({
    quality: 'good',
    fault: true,
    alarm: true,
    commandIntent: 'open',
    settledState: 'active'
  }).kind).toBe('fault');

  expect(resolveDynamoVisualState({
    quality: 'good',
    alarm: true,
    commandIntent: 'open',
    settledState: 'active'
  }).kind).toBe('alarm');
});

test('uncertain quality stays visible instead of being hidden by command intent', () => {
  const resolved = resolveDynamoVisualState({
    quality: 'uncertain',
    commandIntent: 'setpoint',
    settledState: 'active'
  });

  expect(resolved.kind).toBe('uncertain-quality');
  expect(resolved.priority).toBe(DYNAMO_STATE_PRECEDENCE.uncertainQuality);
});

test('command intent dominates normal and transitional process state', () => {
  const resolved = resolveDynamoVisualState({
    quality: 'good',
    commandIntent: 'start',
    settledState: 'transitioning'
  });

  expect(resolved.kind).toBe('command-intent');
  expect(resolved.commandIntent).toBe('start');
});

test('transitioning dominates settled active or inactive indications', () => {
  expect(resolveDynamoVisualState({ quality: 'good', settledState: 'transitioning' }).kind)
    .toBe('transitioning');
  expect(resolveDynamoVisualState({ quality: 'good', settledState: 'active' }).kind)
    .toBe('active');
  expect(resolveDynamoVisualState({ quality: 'good', settledState: 'inactive' }).kind)
    .toBe('inactive');
});

test('state resolution is deterministic for identical inputs', () => {
  const input = {
    quality: 'good' as const,
    alarm: true,
    commandIntent: 'close' as const,
    settledState: 'inactive' as const
  };

  const first = resolveDynamoVisualState(input);
  const second = resolveDynamoVisualState(input);

  expect(second).toEqual(first);
  expect(Object.isFrozen(first)).toBe(true);
});
