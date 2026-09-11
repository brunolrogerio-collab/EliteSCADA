import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import { BUILTIN_VISUAL_OBJECT_TYPES } from '../src/visual-runtime';
import {
  applyVisualEditorAuthoringOperation,
  isVisualElementAuthoringLocked,
  VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY
} from '../src/engineering/visual-editor/visualEditorAuthoringModel';

function rectangle(id: string, x: number, y: number, width: number, height: number): VisualElementEngineering {
  return {
    id,
    key: id,
    type: BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
    properties: { x, y, width, height }
  };
}

function screen(elements: VisualElementEngineering[]): ScreenEngineering {
  return { key: 'screen', name: 'Screen', route: '/screen', elements };
}

test('alignment uses deterministic selection bounds in canonical logical coordinates', () => {
  const base = screen([
    rectangle('a', 10, 20, 20, 20),
    rectangle('b', 50, 40, 40, 20),
    rectangle('c', 100, 80, 10, 40)
  ]);

  const left = applyVisualEditorAuthoringOperation(base, {
    kind: 'align', objectIds: ['a', 'b', 'c'], operation: 'left'
  });
  expect(left.elements?.map(element => element.properties?.x)).toEqual([10, 10, 10]);

  const middle = applyVisualEditorAuthoringOperation(base, {
    kind: 'align', objectIds: ['a', 'b', 'c'], operation: 'verticalMiddle'
  });
  expect(middle.elements?.map(element => element.properties?.y)).toEqual([60, 60, 50]);
  expect(base.elements?.map(element => element.properties?.x)).toEqual([10, 50, 100]);
});

test('distribution supports equal center and equal edge spacing without changing outer anchors', () => {
  const base = screen([
    rectangle('a', 0, 0, 10, 10),
    rectangle('b', 30, 20, 20, 20),
    rectangle('c', 100, 100, 10, 10)
  ]);

  const horizontalSpacing = applyVisualEditorAuthoringOperation(base, {
    kind: 'distribute', objectIds: ['a', 'b', 'c'], operation: 'horizontalSpacing'
  });
  expect(horizontalSpacing.elements?.map(element => element.properties?.x)).toEqual([0, 45, 100]);

  const verticalCenters = applyVisualEditorAuthoringOperation(base, {
    kind: 'distribute', objectIds: ['a', 'b', 'c'], operation: 'verticalCenters'
  });
  expect(verticalCenters.elements?.map(element => element.properties?.y)).toEqual([0, 45, 100]);

  expect(() => applyVisualEditorAuthoringOperation(base, {
    kind: 'distribute', objectIds: ['a', 'b'], operation: 'horizontalSpacing'
  })).toThrow(/at least 3 selected objects/);
});

test('same-size tools use an explicit deterministic reference object', () => {
  const base = screen([
    rectangle('a', 0, 0, 10, 20),
    rectangle('b', 30, 0, 80, 60),
    rectangle('c', 140, 0, 30, 40)
  ]);

  const sameSize = applyVisualEditorAuthoringOperation(base, {
    kind: 'size',
    objectIds: ['a', 'b', 'c'],
    referenceObjectId: 'b',
    operation: 'sameSize'
  });
  expect(sameSize.elements?.map(element => [element.properties?.width, element.properties?.height])).toEqual([
    [80, 60], [80, 60], [80, 60]
  ]);
});

test('group and nested group persist hierarchy with child coordinates relative to canonical parent bounds', () => {
  const base = screen([
    rectangle('a', 20, 30, 20, 10),
    rectangle('b', 60, 50, 30, 20),
    rectangle('c', 120, 80, 10, 10)
  ]);

  const grouped = applyVisualEditorAuthoringOperation(base, {
    kind: 'group', objectIds: ['a', 'b']
  }, { createObjectId: () => 'group-1' });
  expect(grouped.elements).toHaveLength(2);
  expect(grouped.elements?.[0]).toMatchObject({
    id: 'group-1', key: 'group', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 20, y: 30, width: 70, height: 40 },
    children: [
      { id: 'a', properties: { x: 0, y: 0, width: 20, height: 10 } },
      { id: 'b', properties: { x: 40, y: 20, width: 30, height: 20 } }
    ]
  });

  const nested = applyVisualEditorAuthoringOperation(grouped, {
    kind: 'group', objectIds: ['group-1', 'c']
  }, { createObjectId: () => 'group-2' });
  expect(nested.elements).toHaveLength(1);
  expect(nested.elements?.[0].id).toBe('group-2');
  expect(nested.elements?.[0].children?.[0].id).toBe('group-1');

  const ungroupedOuter = applyVisualEditorAuthoringOperation(nested, {
    kind: 'ungroup', objectIds: ['group-2']
  });
  expect(ungroupedOuter.elements?.map(element => element.id)).toEqual(['group-1', 'c']);
  expect(ungroupedOuter.elements?.[0].properties).toMatchObject({ x: 20, y: 30 });
  expect(ungroupedOuter.elements?.[1].properties).toMatchObject({ x: 120, y: 80 });

  const ungroupedInner = applyVisualEditorAuthoringOperation(ungroupedOuter, {
    kind: 'ungroup', objectIds: ['group-1']
  });
  expect(ungroupedInner.elements?.map(element => element.id)).toEqual(['a', 'b', 'c']);
  expect(ungroupedInner.elements?.[0].properties).toMatchObject({ x: 20, y: 30 });
  expect(ungroupedInner.elements?.[1].properties).toMatchObject({ x: 60, y: 50 });
});

test('Dynamo instances stay encapsulated and are never treated as authoring groups', () => {
  const dynamo: VisualElementEngineering = {
    id: 'motor-1', key: 'motor', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    dynamoKey: 'process.motor.standard', properties: { x: 10, y: 10, width: 106, height: 92 }
  };
  const base = screen([dynamo]);
  expect(() => applyVisualEditorAuthoringOperation(base, {
    kind: 'ungroup', objectIds: ['motor-1']
  })).toThrow(/not an authoring group/);
});

test('authoring lock is persisted in metadata, inherits through groups, and is not Runtime interactionEnabled', () => {
  const base = screen([{
    id: 'group-1', key: 'group', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
    properties: { x: 0, y: 0, width: 100, height: 100 },
    children: [rectangle('a', 10, 10, 20, 20), rectangle('b', 40, 10, 20, 20)]
  }]);

  const locked = applyVisualEditorAuthoringOperation(base, {
    kind: 'lock', objectIds: ['group-1'], locked: true
  });
  expect(locked.elements?.[0].metadata?.[VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY]).toBe('true');
  expect(locked.elements?.[0].properties?.interactionEnabled).toBeUndefined();
  expect(isVisualElementAuthoringLocked(locked.elements![0])).toBe(true);

  expect(() => applyVisualEditorAuthoringOperation(locked, {
    kind: 'align', objectIds: ['a', 'b'], operation: 'left'
  })).toThrow(/locked for Engineering authoring/);

  const unlocked = applyVisualEditorAuthoringOperation(locked, {
    kind: 'lock', objectIds: ['group-1'], locked: false
  });
  expect(unlocked.elements?.[0].metadata?.[VISUAL_EDITOR_AUTHORING_LOCK_METADATA_KEY]).toBeUndefined();
});

test('multi-object operations reject mixed parent coordinate spaces', () => {
  const base = screen([
    rectangle('outside', 0, 0, 20, 20),
    {
      id: 'group-1', key: 'group', type: BUILTIN_VISUAL_OBJECT_TYPES.group,
      properties: { x: 50, y: 50, width: 100, height: 100 },
      children: [rectangle('inside', 10, 10, 20, 20)]
    }
  ]);

  expect(() => applyVisualEditorAuthoringOperation(base, {
    kind: 'align', objectIds: ['outside', 'inside'], operation: 'left'
  })).toThrow(/same parent coordinate space/);
});
