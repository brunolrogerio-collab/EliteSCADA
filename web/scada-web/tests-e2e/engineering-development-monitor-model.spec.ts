import { expect, test } from '@playwright/test';
import type { CommunicationDriverDiagnostic } from '../src/engineering/types';
import {
  createTagBitProjectReference,
  type ProjectReferenceDescriptor
} from '../src/engineering/project-reference/projectReferenceModel';
import {
  applyMonitorRealtimeMessage,
  formatMonitorQuality,
  mergeMonitorBatchSamples,
  resolveMonitorQuickAdd
} from '../src/engineering/development-monitor/developmentMonitorModel';
import type { RuntimeTagSnapshot } from '../src/runtime/liveTagTransport';

function tagReference(index: number, dataType = 'Int32'): ProjectReferenceDescriptor {
  const path = `Plant.Area.Tag${String(index).padStart(3, '0')}`;
  const width = dataType === 'Int16' ? 16 : dataType === 'Int32' ? 32 : dataType === 'Int64' ? 64 : null;
  return Object.freeze({
    reference: path,
    label: `Tag ${index}`,
    family: 'tag',
    dataType,
    bindingKind: 'Tag',
    pathSegments: Object.freeze(['Plant', 'Area', `Tag${String(index).padStart(3, '0')}`]),
    tagReference: Object.freeze({ tagId: `tag-${index}` }),
    selectorCapability: width === null ? null : Object.freeze({ kind: 'bit', minIndex: 0, maxIndex: width - 1 })
  });
}

function runtimeTag(index: number, value: unknown, dataType = 'Int32', quality: string | number = 'Good'): RuntimeTagSnapshot {
  const descriptor = tagReference(index, dataType);
  return Object.freeze({
    id: `tag-${index}`,
    name: descriptor.label,
    path: descriptor.reference,
    dataType,
    readOnly: true,
    current: Object.freeze({
      tagId: `tag-${index}`,
      value,
      timestamp: '2026-08-29T04:00:00Z',
      quality,
      source: 'simulation',
      sourceTimestamp: '2026-08-29T03:59:59Z',
      serverTimestamp: '2026-08-29T04:00:00Z'
    })
  });
}

test('Development Monitor resolves exact quick-add, including canonical TAG bits, and rejects ambiguous/not-found references', () => {
  const catalog = [
    tagReference(1),
    Object.freeze({ ...tagReference(2), label: 'Shared Name' }),
    Object.freeze({ ...tagReference(3), label: 'Shared Name' })
  ];

  expect(resolveMonitorQuickAdd(catalog, tagReference(1).reference)).toEqual({ status: 'found', reference: tagReference(1).reference });
  expect(resolveMonitorQuickAdd(catalog, `${tagReference(1).reference}.3`)).toEqual({ status: 'found', reference: `${tagReference(1).reference}.03` });
  expect(resolveMonitorQuickAdd(catalog, 'Shared Name')).toEqual({ status: 'ambiguous' });
  expect(resolveMonitorQuickAdd(catalog, 'Missing.Tag')).toEqual({ status: 'notFound' });
});

test('Development Monitor merges 100 monitored TAGs through one shared batch while preserving quality, timestamp and exact Int64', () => {
  const references = Array.from({ length: 100 }, (_, index) => tagReference(index));
  const selected = references.map(reference => reference.reference);
  const descriptors = new Map<string, ProjectReferenceDescriptor>(references.map(reference => [reference.reference, reference] as const));
  const tags = Array.from({ length: 100 }, (_, index) => runtimeTag(index, index));

  const int64Reference = Object.freeze({ ...tagReference(99, 'Int64'), reference: 'Plant.Area.Counter64', label: 'Counter64' });
  descriptors.delete(tagReference(99).reference);
  descriptors.set(int64Reference.reference, int64Reference);
  selected[99] = int64Reference.reference;
  tags[99] = Object.freeze({
    ...runtimeTag(99, '9223372036854775807', 'Int64', 'Uncertain'),
    path: int64Reference.reference,
    current: Object.freeze({
      ...runtimeTag(99, '9223372036854775807', 'Int64', 'Uncertain').current!,
      value: '9223372036854775807',
      quality: 'Uncertain',
      sourceTimestamp: '2026-08-29T03:58:00Z'
    })
  });

  const samples = mergeMonitorBatchSamples(
    new Map(), selected, descriptors, tags, [] as CommunicationDriverDiagnostic[], [], () => undefined,
    '2026-08-29T04:00:01Z'
  );

  expect(samples.size).toBe(100);
  expect(samples.get(tagReference(0).reference)).toMatchObject({ value: 0, dataType: 'Int32', quality: 'Good' });
  expect(samples.get(int64Reference.reference)).toMatchObject({
    value: '9223372036854775807',
    dataType: 'Int64',
    quality: 'Uncertain',
    sourceTimestamp: '2026-08-29T03:58:00Z'
  });
  expect(formatMonitorQuality({
    reference: 'quality', value: 1, dataType: 'Int32', quality: 3, observedAt: '2026-08-29T04:00:00Z'
  })).toBe('BadCommunication');
});

test('batch TAG bit projection resolves by stable TAG ID and inherits quality/timestamp without turning bad quality into false', () => {
  const base = tagReference(10, 'Int16');
  const bit00 = createTagBitProjectReference(base, 0)!;
  const bit15 = createTagBitProjectReference(base, 15)!;
  const descriptors = new Map<string, ProjectReferenceDescriptor>([
    [bit00.reference, bit00],
    [bit15.reference, bit15]
  ]);
  const tag = Object.freeze({
    ...runtimeTag(10, -32768, 'Int16', 'BadCommunication'),
    path: 'Plant.Renamed.WordStatus'
  });

  const samples = mergeMonitorBatchSamples(
    new Map(),
    [bit00.reference, bit15.reference],
    descriptors,
    [tag],
    [],
    [],
    () => undefined,
    '2026-08-29T04:00:01Z'
  );

  expect(samples.get(bit00.reference)).toMatchObject({
    value: false,
    dataType: 'Boolean',
    quality: 'BadCommunication',
    sourceTimestamp: '2026-08-29T03:59:59Z'
  });
  expect(samples.get(bit15.reference)).toMatchObject({
    value: true,
    dataType: 'Boolean',
    quality: 'BadCommunication',
    sourceTimestamp: '2026-08-29T03:59:59Z'
  });
});

test('unsafe numeric Int64 projection becomes unavailable instead of fabricating a bit value', () => {
  const base = tagReference(64, 'Int64');
  const bit63 = createTagBitProjectReference(base, 63)!;
  const descriptors = new Map<string, ProjectReferenceDescriptor>([[bit63.reference, bit63]]);
  const tag = runtimeTag(64, 9223372036854776000, 'Int64', 'Good');

  const samples = mergeMonitorBatchSamples(
    new Map(), [bit63.reference], descriptors, [tag], [], [], () => undefined,
    '2026-08-29T04:00:01Z'
  );

  expect(samples.get(bit63.reference)).toMatchObject({ value: null, dataType: 'Boolean', state: 'Unavailable' });
  expect(samples.get(bit63.reference)?.detail).toContain('cannot be represented safely');
});

test('realtime update changes only selected canonical TAG references and projects selected bits by stable TAG ID', () => {
  const first = tagReference(1);
  const second = tagReference(2);
  const firstBit03 = createTagBitProjectReference(first, 3)!;
  const descriptors = new Map<string, ProjectReferenceDescriptor>([
    [first.reference, first],
    [second.reference, second],
    [firstBit03.reference, firstBit03]
  ]);
  const current = new Map([[first.reference, Object.freeze({
    reference: first.reference,
    value: 1,
    dataType: 'Int32',
    quality: 'Good',
    observedAt: '2026-08-29T04:00:00Z'
  })]]);

  const next = applyMonitorRealtimeMessage(current, Object.freeze({
    type: 'tagValueChanged',
    tag: Object.freeze({ id: 'tag-1', name: first.label, path: 'Plant.Renamed.Tag001' }),
    value: 10,
    quality: 'BadCommunication',
    timestamp: '2026-08-29T04:00:02Z'
  }), new Set([first.reference, firstBit03.reference]), descriptors, '2026-08-29T04:00:03Z');

  expect(next.get(first.reference)).toMatchObject({ value: 10, quality: 'BadCommunication', sourceTimestamp: '2026-08-29T04:00:02Z' });
  expect(next.get(firstBit03.reference)).toMatchObject({ value: true, dataType: 'Boolean', quality: 'BadCommunication', sourceTimestamp: '2026-08-29T04:00:02Z' });
  expect(next.has(second.reference)).toBeFalsy();
});
