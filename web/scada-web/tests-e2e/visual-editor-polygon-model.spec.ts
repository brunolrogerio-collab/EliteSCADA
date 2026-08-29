import { expect, test } from '@playwright/test';
import type { ScreenEngineering } from '../src/engineering/types';
import { createCanonicalPolygon, updateCanonicalPolygonPoints } from '../src/engineering/visual-editor/polygonCanonicalMutations';
import { readPolygonPoints } from '../src/engineering/visual-editor/polygonGeometry';

function screen(): ScreenEngineering {
  return { key: 'screen', name: 'Screen', route: '/screen', elements: [] };
}

test('free polygon creation stores local ordered vertices without duplicating the closing point', () => {
  const points = [
    { x: 100, y: 80 },
    { x: 180, y: 100 },
    { x: 160, y: 170 },
    { x: 110, y: 150 }
  ];
  const created = createCanonicalPolygon(screen(), points, () => 'polygon-id');
  const polygon = created.screen.elements?.[0]!;

  expect(polygon).toMatchObject({
    id: 'polygon-id',
    type: 'core.polygon',
    properties: { x: 100, y: 80, width: 80, height: 90 }
  });
  expect(readPolygonPoints(polygon)).toEqual([
    { x: 0, y: 0 },
    { x: 80, y: 20 },
    { x: 60, y: 90 },
    { x: 10, y: 70 }
  ]);
  expect(readPolygonPoints(polygon)).toHaveLength(4);
  expect(readPolygonPoints(polygon)[0]).not.toEqual(readPolygonPoints(polygon)[3]);

  const roundTrip = JSON.parse(JSON.stringify(created.screen)) as ScreenEngineering;
  expect(readPolygonPoints(roundTrip.elements?.[0]!)).toEqual(readPolygonPoints(polygon));
});

test('polygon creation rejects incomplete or degenerate geometry', () => {
  expect(() => createCanonicalPolygon(screen(), [{ x: 0, y: 0 }, { x: 10, y: 10 }])).toThrow(/three/i);
  expect(() => createCanonicalPolygon(screen(), [{ x: 0, y: 0 }, { x: 10, y: 10 }, { x: 20, y: 20 }])).toThrow(/degenerate/i);
  expect(() => createCanonicalPolygon(screen(), [{ x: 0, y: 0 }, { x: Number.NaN, y: 10 }, { x: 20, y: 0 }])).toThrow(/finite/i);
});

test('polygon vertex editing remains canonical and keeps at least three valid points', () => {
  const created = createCanonicalPolygon(screen(), [
    { x: 10, y: 10 }, { x: 110, y: 10 }, { x: 80, y: 90 }
  ], () => 'polygon-id');

  const originalBounds = {
    width: created.screen.elements?.[0].properties?.width,
    height: created.screen.elements?.[0].properties?.height
  };
  const updated = updateCanonicalPolygonPoints(created.screen, 'polygon-id', [
    { x: 0, y: 0 }, { x: 120, y: 0 }, { x: 130, y: 80 }, { x: 30, y: 100 }
  ]);
  const polygon = updated.elements?.[0]!;
  expect(readPolygonPoints(polygon)).toEqual([
    { x: 0, y: 0 }, { x: 120, y: 0 }, { x: 130, y: 80 }, { x: 30, y: 100 }
  ]);
  expect(polygon.properties?.width).toBe(originalBounds.width);
  expect(polygon.properties?.height).toBe(originalBounds.height);

  expect(() => updateCanonicalPolygonPoints(updated, 'polygon-id', [
    { x: 0, y: 0 }, { x: 10, y: 0 }
  ])).toThrow(/three/i);
});
