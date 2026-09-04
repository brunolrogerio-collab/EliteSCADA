import { expect, test } from '@playwright/test';
import type { DynamoEngineering, VisualElementEngineering } from '../src/engineering/types';
import {
  expandRuntimeDynamoVisuals,
  isExpandedRuntimeDynamo
} from '../src/runtime/visual-navigation/runtimeDynamoVisualProjection';

const definitionId = '16000000-0000-0000-0000-00000000d001';
const childId = '16000000-0000-0000-0000-00000000d002';
const instanceId = '16000000-0000-0000-0000-00000000d003';
const commandId = '16000000-0000-0000-0000-00000000d004';

test('expanded Dynamo becomes a canonical renderable group while preserving child ExecuteCommand action', () => {
  const definitions: DynamoEngineering[] = [{
    id: definitionId,
    key: 'c16.dynamo.command.projection',
    name: 'C16 Command Projection',
    parameters: [],
    elements: [{
      id: childId,
      key: 'start',
      type: 'core.button',
      properties: { x: 0, y: 0, width: 120, height: 40, text: 'Start' },
      actions: [{
        eventKey: 'click',
        kind: 'executeCommand',
        targetKey: null,
        commandId,
        parameters: null,
        version: 1
      } as any]
    }]
  } as any];

  const instance: VisualElementEngineering = {
    id: instanceId,
    key: 'instance',
    type: 'dynamo',
    dynamoKey: 'c16.dynamo.command.projection',
    properties: { x: 100, y: 200, width: 160, height: 80 }
  };

  const [expanded] = expandRuntimeDynamoVisuals([instance], definitions);
  expect(isExpandedRuntimeDynamo(expanded)).toBeTruthy();
  expect(expanded.type).toBe('core.group');
  expect(expanded.dynamoKey ?? null).toBeNull();
  expect(expanded.id).toBe(instanceId);
  expect(expanded.children).toHaveLength(1);

  const child = expanded.children![0];
  expect(child.id).toBe(`${instanceId}/${childId}`);
  expect(child.type).toBe('core.button');
  expect(child.actions).toEqual([expect.objectContaining({
    eventKey: 'click',
    kind: 'executeCommand',
    commandId
  })]);
});
