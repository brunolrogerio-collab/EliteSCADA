import { expect, test } from '@playwright/test';
import type { ScreenEngineering, VisualElementEngineering } from '../src/engineering/types';
import {
  copyVisualEditorElements,
  deleteVisualEditorElements,
  duplicateVisualEditorElements,
  nudgeVisualEditorElements,
  pasteVisualEditorElements
} from '../src/engineering/visual-editor/visualEditorClipboardModel';

function element(
  id: string,
  key: string,
  x: number,
  y: number,
  children?: VisualElementEngineering[]
): VisualElementEngineering {
  return {
    id,
    key,
    type: children ? 'core.group' : 'core.rectangle',
    properties: { x, y, width: 20, height: 20 },
    children
  };
}

function screen(elements: VisualElementEngineering[]): ScreenEngineering {
  return { id: 'screen-1', key: 'main', name: 'Main', elements };
}

function ids(...values: string[]) {
  let index = 0;
  return () => values[index++] ?? `generated-${index}`;
}

test('duplicate preserves selected painter order, offsets roots and assigns fresh identities', () => {
  const source = screen([
    element('a', 'A', 0, 0),
    element('b', 'B', 40, 0),
    element('c', 'C', 80, 0)
  ]);

  const result = duplicateVisualEditorElements(source, ['b', 'a'], {
    createObjectId: ids('copy-a', 'copy-b'),
    offsetX: 12,
    offsetY: 8
  });

  expect(result.objectIds).toEqual(['copy-a', 'copy-b']);
  expect(result.screen.elements?.map(item => item.id)).toEqual(['a', 'b', 'c', 'copy-a', 'copy-b']);
  expect(result.screen.elements?.[3]?.key).toBe('A-copy');
  expect(result.screen.elements?.[3]?.properties).toMatchObject({ x: 12, y: 8 });
  expect(result.screen.elements?.[4]?.properties).toMatchObject({ x: 52, y: 8 });
  expect(source.elements?.length).toBe(3);
});

test('copy and paste regenerate nested group identities while preserving child-relative coordinates', () => {
  const source = screen([
    element('group', 'Group', 100, 50, [
      element('child-1', 'Child', 5, 6),
      element('child-2', 'Child2', 30, 6)
    ])
  ]);
  const payload = copyVisualEditorElements(source, ['group']);
  const result = pasteVisualEditorElements(source, payload, null, {
    createObjectId: ids('group-copy', 'child-copy-1', 'child-copy-2')
  });
  const copy = result.screen.elements?.[1];

  expect(copy?.id).toBe('group-copy');
  expect(copy?.properties).toMatchObject({ x: 110, y: 60 });
  expect(copy?.children?.map(child => child.id)).toEqual(['child-copy-1', 'child-copy-2']);
  expect(copy?.children?.[0]?.properties).toMatchObject({ x: 5, y: 6 });
});

test('Dynamo duplication preserves definition reference and public values but gets a fresh instance identity', () => {
  const dynamo: VisualElementEngineering = {
    id: 'dyn-1',
    key: 'P101',
    type: 'core.group',
    dynamoKey: 'dynamo.pump.standard',
    equipmentPath: 'Area.P101',
    dynamoParameters: [
      { key: 'running', kind: 'TagReference', tagReference: { tagId: 'tag-running' } }
    ],
    properties: { x: 10, y: 20, width: 132, height: 92 }
  };

  const result = duplicateVisualEditorElements(screen([dynamo]), ['dyn-1'], {
    createObjectId: ids('dyn-2')
  });
  const copy = result.screen.elements?.[1];

  expect(copy?.id).toBe('dyn-2');
  expect(copy?.dynamoKey).toBe('dynamo.pump.standard');
  expect(copy?.equipmentPath).toBe('Area.P101');
  expect(copy?.dynamoParameters).toEqual(dynamo.dynamoParameters);
  expect(copy?.dynamoParameters).not.toBe(dynamo.dynamoParameters);
});

test('duplicate keys are made unique against the whole hierarchy', () => {
  const source = screen([
    element('a', 'Pump', 0, 0),
    element('existing', 'Pump-copy', 30, 0)
  ]);
  const result = duplicateVisualEditorElements(source, ['a'], { createObjectId: ids('copy') });

  expect(result.screen.elements?.[2]?.key).toBe('Pump-copy-2');
});

test('delete removes only selected siblings and returns an empty selection', () => {
  const source = screen([element('a', 'A', 0, 0), element('b', 'B', 20, 0)]);
  const result = deleteVisualEditorElements(source, ['a']);

  expect(result.screen.elements?.map(item => item.id)).toEqual(['b']);
  expect(result.objectIds).toEqual([]);
});

test('arrow-key nudge is logical-coordinate based and supports coarse delta', () => {
  const source = screen([element('a', 'A', 10, 20), element('b', 'B', 40, 50)]);
  const fine = nudgeVisualEditorElements(source, ['a', 'b'], 1, -1);
  const coarse = nudgeVisualEditorElements(fine.screen, fine.objectIds, 10, 10);

  expect(fine.screen.elements?.[0]?.properties).toMatchObject({ x: 11, y: 19 });
  expect(coarse.screen.elements?.[0]?.properties).toMatchObject({ x: 21, y: 29 });
  expect(coarse.screen.elements?.[1]?.properties).toMatchObject({ x: 51, y: 59 });
});

test('clipboard operations reject selections spanning different coordinate spaces', () => {
  const source = screen([
    element('root', 'Root', 0, 0),
    element('group', 'Group', 50, 50, [element('child', 'Child', 2, 2)])
  ]);

  expect(() => copyVisualEditorElements(source, ['root', 'child']))
    .toThrow(/same coordinate space/);
});
