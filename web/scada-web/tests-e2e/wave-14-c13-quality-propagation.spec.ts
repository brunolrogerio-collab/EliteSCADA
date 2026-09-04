import { expect, test } from '@playwright/test';
import { resolveVisualDynamicState, visualTagSampleKey } from '../src/engineering/visual-editor/visualDynamicRuntime';
import type { VisualElementEngineering } from '../src/engineering/types';
import { parseRuntimeTagRealtimeMessage } from '../src/runtime/liveTagTransport';

test('C13 realtime wire preserves Unavailable quality and visual binding refuses it', () => {
  const tagId = '71300000-0000-0000-0000-000000000001';
  const realtime = parseRuntimeTagRealtimeMessage(JSON.stringify({
    type: 'tagValueChanged',
    tag: {
      id: tagId,
      name: 'ProcessValue',
      path: 'Simulation.ProcessValue'
    },
    value: false,
    quality: 'Unavailable',
    timestamp: '2026-09-03T23:24:28Z',
    source: 'memory.server'
  }));

  expect(realtime).not.toBeNull();
  expect(realtime?.quality).toBe('Unavailable');

  const element: VisualElementEngineering = {
    key: 'Pump',
    type: 'core.rectangle',
    bindings: [{
      key: 'visible',
      kind: 'Tag',
      target: 'Simulation.ProcessValue',
      tagReference: { tagId }
    }]
  };
  const samples = new Map([[visualTagSampleKey(tagId), Object.freeze({
    reference: realtime!.tag.path,
    tagId: realtime!.tag.id,
    value: realtime!.value,
    dataType: 'Boolean',
    quality: realtime!.quality,
    timestamp: realtime!.timestamp
  })]]);

  const resolved = resolveVisualDynamicState(
    element,
    Object.freeze({ visible: true }),
    samples
  );

  expect(resolved.values.visible).toBe(true);
  expect(resolved.diagnostics).toHaveLength(1);
  expect(resolved.diagnostics[0].message).toContain('Unavailable');
});
