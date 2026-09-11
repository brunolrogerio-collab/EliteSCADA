import { expect, test } from '@playwright/test';
import { resolveVisualDynamicState, visualTagSampleKey } from '../src/engineering/visual-editor/visualDynamicRuntime';
import type { VisualElementEngineering } from '../src/engineering/types';
import { parseRuntimeTagRealtimeMessage } from '../src/runtime/liveTagTransport';

const qualifiedStates = ['Bad', 'Stale', 'Unavailable'] as const;

for (const quality of qualifiedStates) {
  test(`C12 Server Script ${quality} quality survives realtime transport and is refused by visual binding`, () => {
    const tagId = `71200000-0000-0000-0000-00000000000${qualifiedStates.indexOf(quality) + 1}`;
    const realtime = parseRuntimeTagRealtimeMessage(JSON.stringify({
      type: 'tagValueChanged',
      tag: {
        id: tagId,
        name: 'ProcessValue',
        path: 'Simulation.ProcessValue'
      },
      value: 41,
      quality,
      timestamp: '2026-09-03T23:40:00Z',
      source: 'memory.server'
    }));

    expect(realtime).not.toBeNull();
    expect(realtime?.quality).toBe(quality);

    const element: VisualElementEngineering = {
      key: 'GenericState',
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
      dataType: 'Int32',
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
    expect(resolved.diagnostics[0].message).toContain(quality);
  });
}
