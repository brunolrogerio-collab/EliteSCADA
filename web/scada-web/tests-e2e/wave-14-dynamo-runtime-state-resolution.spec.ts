import { expect, test } from '@playwright/test';
import type { VisualElementEngineering } from '../src/engineering/types';
import type { VisualLiveScalarSample } from '../src/engineering/visual-editor/visualEditorLiveValues';
import {
  resolveDynamoRuntimeState,
  sampleQuality
} from '../src/engineering/visual-editor/dynamo/dynamoRuntimeStateModel';

function lamp(parameter: string, target: string, tagId: string): VisualElementEngineering {
  return {
    id: `lamp-${parameter}`,
    key: parameter,
    type: 'core.ellipse',
    properties: { x: 0, y: 0, width: 10, height: 10 },
    bindings: [{
      key: 'visible',
      kind: 'Tag',
      target,
      direction: 'read',
      tagReference: { tagId },
      metadata: { dynamoParameter: parameter }
    }]
  };
}

function sample(tagId: string, value: unknown, quality: string | number = 'Good'): VisualLiveScalarSample {
  return {
    reference: `Plant.${tagId}`,
    tagId,
    value,
    dataType: 'Boolean',
    quality
  };
}

function samples(...values: VisualLiveScalarSample[]): ReadonlyMap<string, VisualLiveScalarSample> {
  const map = new Map<string, VisualLiveScalarSample>();
  for (const value of values) {
    map.set(`tag:${value.tagId!.toLowerCase()}`, value);
    map.set(value.reference, value);
  }
  return map;
}

test('bad quality dominates an optimistic running state', () => {
  const result = resolveDynamoRuntimeState(
    [lamp('running', 'Plant.P101.Running', 'tag-running')],
    samples(sample('tag-running', true, 'Bad'))
  );
  expect(result.state.kind).toBe('bad-quality');
  expect(result.state.priority).toBe(600);
});

test('fault dominates active running feedback', () => {
  const result = resolveDynamoRuntimeState(
    [
      lamp('running', 'Plant.P101.Running', 'tag-running'),
      lamp('fault', 'Plant.P101.Fault', 'tag-fault')
    ],
    samples(sample('tag-running', true), sample('tag-fault', true))
  );
  expect(result.state.kind).toBe('fault');
});

test('tank high indication is projected as alarm', () => {
  const result = resolveDynamoRuntimeState(
    [lamp('high', 'Plant.T01.High', 'tag-high')],
    samples(sample('tag-high', true))
  );
  expect(result.state.kind).toBe('alarm');
});

test('open and closed valve feedback resolves active inactive and transitioning', () => {
  const elements = [
    lamp('open', 'Plant.V01.Open', 'tag-open'),
    lamp('closed', 'Plant.V01.Closed', 'tag-closed')
  ];
  expect(resolveDynamoRuntimeState(elements, samples(
    sample('tag-open', true), sample('tag-closed', false)
  )).state.kind).toBe('active');
  expect(resolveDynamoRuntimeState(elements, samples(
    sample('tag-open', false), sample('tag-closed', true)
  )).state.kind).toBe('inactive');
  expect(resolveDynamoRuntimeState(elements, samples(
    sample('tag-open', false), sample('tag-closed', false)
  )).state.kind).toBe('transitioning');
});

test('contradictory valve end switches fail to fault instead of choosing an optimistic state', () => {
  const result = resolveDynamoRuntimeState(
    [
      lamp('open', 'Plant.V01.Open', 'tag-open'),
      lamp('closed', 'Plant.V01.Closed', 'tag-closed')
    ],
    samples(sample('tag-open', true), sample('tag-closed', true))
  );
  expect(result.feedbackMismatch).toBe(true);
  expect(result.state.kind).toBe('fault');
});

test('numeric quality mirrors the canonical Scada.Core TagQuality enum', () => {
  expect(sampleQuality(sample('tag-running', true, 0))).toBe('good');
  expect(sampleQuality(sample('tag-running', true, 1))).toBe('uncertain');
  expect(sampleQuality(sample('tag-running', true, 2))).toBe('bad');
  expect(sampleQuality(sample('tag-running', true, 3))).toBe('bad');
  expect(sampleQuality(sample('tag-running', true, 4))).toBe('bad');
  expect(sampleQuality(sample('tag-running', true, 5))).toBe('bad');
  expect(sampleQuality(sample('tag-running', true, 6))).toBe('stale');
  expect(sampleQuality(sample('tag-running', true, 7))).toBe('bad');
  expect(sampleQuality(sample('tag-running', true, 99))).toBe('unknown');
});

test('numeric Good quality allows normal active state while uncertain and stale keep their safety precedence', () => {
  const elements = [lamp('running', 'Plant.P101.Running', 'tag-running')];

  expect(resolveDynamoRuntimeState(
    elements,
    samples(sample('tag-running', true, 0))
  ).state.kind).toBe('active');

  expect(resolveDynamoRuntimeState(
    elements,
    samples(sample('tag-running', true, 1))
  ).state.kind).toBe('uncertain-quality');

  expect(resolveDynamoRuntimeState(
    elements,
    samples(sample('tag-running', true, 6))
  ).state.kind).toBe('bad-quality');
});

test('only bindings that explicitly declare dynamoParameter affect instance state', () => {
  const privateLamp: VisualElementEngineering = {
    ...lamp('running', 'Plant.P101.Private', 'tag-private'),
    bindings: [{
      key: 'visible',
      kind: 'Tag',
      target: 'Plant.P101.Private',
      direction: 'read',
      tagReference: { tagId: 'tag-private' }
    }]
  };
  const result = resolveDynamoRuntimeState(
    [privateLamp],
    samples(sample('tag-private', true))
  );
  expect(result.parameterSamples.size).toBe(0);
  expect(result.state.kind).toBe('bad-quality');
});
