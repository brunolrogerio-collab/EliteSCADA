import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  listBuiltinVisualObjectSchemas
} from '../src/visual-runtime';

const backendFoundationUrl = new URL(
  '../../../src/Scada.Engineering/VisualScripting/VisualPropertyFoundation.cs',
  import.meta.url
);
const backendSchemasUrl = new URL(
  '../../../src/Scada.Engineering/VisualScripting/BuiltinVisualObjectSchemas.cs',
  import.meta.url
);

const LEGACY_BACKEND_ONLY_KEYS = new Set(['imageResourceId']);

test('frontend and backend canonical visual property keys remain in lockstep', async () => {
  const backendFoundation = await readFile(backendFoundationUrl, 'utf8');
  const backendKeys = [...backendFoundation.matchAll(/public const string \w+ = "([^"]+)";/g)]
    .map(match => match[1])
    .filter(key => !LEGACY_BACKEND_ONLY_KEYS.has(key))
    .sort();
  const frontendKeys = COMMON_VISUAL_PROPERTY_REGISTRY.list()
    .map(definition => definition.key)
    .sort();

  expect(backendKeys).toEqual(frontendKeys);
});

test('backend schema source declares every frontend builtin object type and extended enum contract', async () => {
  const backendFoundation = await readFile(backendFoundationUrl, 'utf8');
  const backendSchemas = await readFile(backendSchemasUrl, 'utf8');

  for (const schema of listBuiltinVisualObjectSchemas()) {
    expect(backendSchemas, `backend schema declaration for ${schema.objectTypeKey}`)
      .toContain(`"${schema.objectTypeKey}"`);
  }

  expect(backendFoundation).toContain(
    '["none", "solid", "dashed", "dotted", "dash-dot", "dash-dot-dot"]'
  );
  expect(backendFoundation).toContain('["clip", "ellipsis"]');
  expect(backendFoundation).toContain('presentationHint: "font-family"');
  expect(backendFoundation).toContain('presentationHint: "project-asset"');
});
