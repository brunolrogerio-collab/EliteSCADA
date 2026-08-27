import { expect, test } from '@playwright/test';
import {
  applyRuntimeTagRealtimeEvent,
  buildRuntimeTagInspectorSummary,
  classifyRuntimeTagEndpointIssue,
  filterRuntimeTags,
  normalizeRuntimeTagQuality,
  recentHistoryWindow
} from '../src/runtime/tagInspectorModel';
import type { RuntimeTagListItem } from '../src/runtime/tagInspectorTypes';

function tag(overrides: Partial<RuntimeTagListItem> = {}): RuntimeTagListItem {
  return {
    id: crypto.randomUUID(),
    name: 'Level',
    path: 'Demo.Tank01.Level',
    dataType: 'Double',
    engineeringUnit: '%',
    description: 'Tank level',
    readOnly: true,
    current: {
      tagId: 'sample',
      value: 55.2,
      timestamp: '2026-08-27T18:00:00Z',
      quality: 0,
      source: 'builtin.simulation'
    },
    ...overrides
  };
}

test('normalizes backend numeric and realtime string TAG qualities without collapsing attention into bad', () => {
  expect(normalizeRuntimeTagQuality(0)).toBe('good');
  expect(normalizeRuntimeTagQuality(1)).toBe('uncertain');
  expect(normalizeRuntimeTagQuality(3)).toBe('bad-communication');
  expect(normalizeRuntimeTagQuality(6)).toBe('stale');
  expect(normalizeRuntimeTagQuality('BadConfiguration')).toBe('bad-configuration');
  expect(normalizeRuntimeTagQuality('Bad Device')).toBe('bad-device');
  expect(normalizeRuntimeTagQuality('something-new')).toBe('unknown');
});

test('filters TAGs by search, quality and read-only access using only Runtime facts', () => {
  const tags = [
    tag({ id: 'good', path: 'Demo.A.Level', readOnly: true }),
    tag({ id: 'stale', path: 'Demo.B.Pressure', name: 'Pressure', engineeringUnit: 'bar', readOnly: false, current: { tagId: 'stale', value: 3.2, timestamp: '2026-08-27T18:00:00Z', quality: 'Stale', source: 'plc-1' } }),
    tag({ id: 'bad', path: 'Demo.C.Flow', name: 'Flow', current: { tagId: 'bad', value: 0, timestamp: '2026-08-27T18:00:00Z', quality: 'BadCommunication', source: 'plc-2' } }),
    tag({ id: 'none', path: 'Demo.D.Command', name: 'Command', current: null, readOnly: false })
  ];

  expect(filterRuntimeTags(tags, { query: 'pressure', quality: 'all', access: 'all' }).map(item => item.id)).toEqual(['stale']);
  expect(filterRuntimeTags(tags, { query: 'plc-2', quality: 'bad', access: 'all' }).map(item => item.id)).toEqual(['bad']);
  expect(filterRuntimeTags(tags, { query: '', quality: 'attention', access: 'writable' }).map(item => item.id)).toEqual(['stale']);
  expect(filterRuntimeTags(tags, { query: '', quality: 'no-sample', access: 'writable' }).map(item => item.id)).toEqual(['none']);

  expect(buildRuntimeTagInspectorSummary(tags)).toEqual({
    total: 4,
    good: 1,
    attention: 1,
    bad: 1,
    noSample: 1,
    readOnly: 2,
    writable: 2
  });
});

test('applies realtime current-value events without mutating TAG metadata or fabricating historian samples', () => {
  const original = tag({ id: 'tag-1', description: 'Authoritative metadata' });
  const next = applyRuntimeTagRealtimeEvent([original], {
    type: 'tagValueChanged',
    tag: { id: 'tag-1', name: 'Level', path: 'Demo.Tank01.Level', engineeringUnit: '%' },
    value: 61.8,
    quality: 'Good',
    timestamp: '2026-08-27T18:01:00Z',
    source: 'builtin.simulation'
  });

  expect(next[0].description).toBe('Authoritative metadata');
  expect(next[0].current?.value).toBe(61.8);
  expect(next[0].current?.quality).toBe('Good');
  expect(next[0].current?.timestamp).toBe('2026-08-27T18:01:00Z');
});

test('keeps authorization/not-found/unavailable states explicit and bounds the history window', () => {
  expect(classifyRuntimeTagEndpointIssue(401)).toBe('unauthenticated');
  expect(classifyRuntimeTagEndpointIssue(403)).toBe('forbidden');
  expect(classifyRuntimeTagEndpointIssue(404)).toBe('not-found');
  expect(classifyRuntimeTagEndpointIssue(500)).toBe('unavailable');

  const window = recentHistoryWindow(15, new Date('2026-08-27T18:15:00Z'));
  expect(window.from).toBe('2026-08-27T18:00:00.000Z');
  expect(window.to).toBe('2026-08-27T18:15:00.000Z');
});

test('protected Runtime TAG/detail/history contracts expose the read-only inspector facts', async ({ request }) => {
  const tagsResponse = await request.get('/api/tags');
  expect(tagsResponse.ok()).toBeTruthy();
  const tags = await tagsResponse.json() as RuntimeTagListItem[];
  expect(tags.length).toBeGreaterThan(0);

  const selected = tags.find(item => item.path === 'Demo.Tank01.Level') ?? tags[0];
  expect(selected.id).toBeTruthy();
  expect(selected.path).toBeTruthy();
  expect(typeof selected.readOnly).toBe('boolean');
  expect(selected.current?.timestamp).toBeTruthy();

  const detailResponse = await request.get(`/api/tags/by-path/${selected.path.split('/').map(encodeURIComponent).join('/')}`);
  expect(detailResponse.ok()).toBeTruthy();
  const detail = await detailResponse.json() as { tag: { id: string; path: string; source?: string | null }; current?: { tagId: string } | null };
  expect(detail.tag.id).toBe(selected.id);
  expect(detail.tag.path).toBe(selected.path);

  const end = new Date();
  const start = new Date(end.getTime() - 15 * 60_000);
  let history: Array<{ tagId: string; timestamp: string; quality: string | number }> = [];
  await expect.poll(async () => {
    const response = await request.get(`/api/history/${selected.id}?from=${encodeURIComponent(start.toISOString())}&to=${encodeURIComponent(new Date().toISOString())}&limit=50`);
    if (!response.ok()) return 0;
    history = await response.json() as typeof history;
    return history.length;
  }, { timeout: 12_000 }).toBeGreaterThan(0);

  expect(history.every(sample => sample.tagId === selected.id)).toBeTruthy();
  expect(history.every(sample => Boolean(sample.timestamp))).toBeTruthy();
});

test('protected realtime endpoint publishes TAG current-value facts used by the inspector', async ({ page }) => {
  await page.goto('/');
  const payload = await page.evaluate(() => new Promise<Record<string, unknown>>((resolve, reject) => {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const socket = new WebSocket(`${protocol}//${window.location.host}/ws/tags`);
    const timer = window.setTimeout(() => {
      socket.close();
      reject(new Error('Timed out waiting for tagValueChanged.'));
    }, 10_000);

    socket.addEventListener('message', event => {
      const parsed = JSON.parse(String(event.data)) as Record<string, unknown>;
      if (parsed.type !== 'tagValueChanged') return;
      window.clearTimeout(timer);
      socket.close();
      resolve(parsed);
    });
    socket.addEventListener('error', () => {
      window.clearTimeout(timer);
      reject(new Error('Realtime WebSocket failed.'));
    });
  }));

  expect(payload.type).toBe('tagValueChanged');
  expect(payload.tag).toBeTruthy();
  expect(payload.timestamp).toBeTruthy();
  expect(payload.quality).toBeTruthy();
});
