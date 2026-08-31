import { expect, test } from '@playwright/test';
import {
  normalizeScriptDefinition,
  normalizeVisualEventReference
} from '../src/engineering/scripts/ScriptEngineeringWorkspace.logic';

const tagId = '63000000-0000-0000-0000-000000000001';
const scriptId = '11111111-1111-4111-8111-111111111111';
const visualDefinitionId = '44000000-0000-0000-0000-000000000001';

test('numeric backend TAG selector enum normalizes to canonical bit identity', () => {
  const script = normalizeScriptDefinition({
    id: scriptId,
    path: 'scripts/tag-bit.py',
    name: 'TAG bit',
    scope: 0,
    source: 'pass\n',
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [{
      eventKind: 3,
      handlerName: 'on_tag',
      targetReference: null,
      tagReference: {
        tagId,
        selector: { kind: 0, index: 7 }
      },
      timerIntervalMs: null
    }],
    dependencies: [],
    description: null,
    metadata: {}
  });

  expect(script.entryPoints[0]?.tagReference).toEqual({
    tagId,
    selector: { kind: 'bit', index: 7 }
  });

  const reference = normalizeVisualEventReference({
    visualDefinitionId,
    visualObjectId: null,
    eventKind: 3,
    scriptId,
    entryPoint: 'on_tag',
    targetReference: null,
    tagReference: {
      tagId,
      selector: { kind: 0, index: 7 }
    },
    timerIntervalMs: null
  });

  expect(reference.tagReference).toEqual({
    tagId,
    selector: { kind: 'bit', index: 7 }
  });
});
