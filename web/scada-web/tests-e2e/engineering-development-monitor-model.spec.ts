import { expect, test } from '@playwright/test';
import type { CommunicationDriverDiagnostic } from '../src/engineering/types';
import type { ProjectReferenceDescriptor } from '../src/engineering/project-reference/projectReferenceModel';
import {
  applyMonitorRealtimeMessage,
  formatMonitorQuality,
  mergeMonitorBatchSamples,
  resolveMonitorQuickAdd
} from '../src/engineering/development-monitor/developmentMonitorModel';
import type { RuntimeTagSnapshot } from '../src/runtime/liveTagTransport';

function tagReference(index: number, dataType = 'Int32'): ProjectReferenceDescriptor {
  const path = `Plant.Area.Tag${String(index).padStart(3, '0')}`;
  return Object.freeze({
    reference: path,
    label: `Tag ${index}`,
    family: 'tag',
    dataType,
    bindingKind: 'Tag',
    pathSegments: Object.freeze(['Plant', 'Area', `Tag${String(index).padStart(3, '0')}`])
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

test('Development Monitor resolves exact quick-add and rejects ambiguous/not-found references', () => {
  const catalog = [
    tagReference(1),
    Object.freeze({ ...tagReference(2), label: 'Shared Name' }),
    Object.freeze({ ...tagReference(3), label: 'Shared Name' })
  ];

  expect(resolveMonitorQuickAdd(catalog, tagReference(1).reference)).toEqual({ status: 'found', reference: tagReference(1).reference });
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

test('realtime update changes only a selected canonical TAG reference', () => {
  const first = tagReference(1);
  const second = tagReference(2);
  const descriptors = new Map<string, ProjectReferenceDescriptor>([[first.reference, first], [second.reference, second]]);
  const current = new Map([[first.reference, Object.freeze({
    reference: first.reference,
    value: 1,
    dataType: 'Int32',
    quality: 'Good',
    observedAt: '2026-08-29T04:00:00Z'
  })]]);

  const next = applyMonitorRealtimeMessage(current, Object.freeze({
    type: 'tagValueChanged',
    tag: Object.freeze({ id: 'tag-1', name: first.label, path: first.reference }),
    value: 2,
    quality: 'BadCommunication',
    timestamp: '2026-08-29T04:00:02Z'
  }), new Set([first.reference]), descriptors, '2026-08-29T04:00:03Z');

  expect(next.get(first.reference)).toMatchObject({ value: 2, quality: 'BadCommunication', sourceTimestamp: '2026-08-29T04:00:02Z' });
  expect(next.has(second.reference)).toBeFalsy();
});
