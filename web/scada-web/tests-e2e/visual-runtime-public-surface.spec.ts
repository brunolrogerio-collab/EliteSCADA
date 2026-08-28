import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('public visual runtime index hides internal registry-port adaptation seams', async () => {
  const index = await source('../src/visual-runtime/index.ts');

  expect(index).not.toContain("export * from './runtimeVisualPropertyPort'");
  expect(index).not.toContain("export * from './runtimeVisualPropertyAdapter'");
  expect(index).toContain("export * from './runtimeVisualInstance'");
  expect(index).toContain("export * from './visualPythonPropertyCapabilityProvider'");
});

test('Runtime Visual Instance public options require the typed object schema', async () => {
  const runtime = await source('../src/visual-runtime/runtimeVisualInstance.ts');

  expect(runtime).toContain('schema: VisualObjectPropertySchema;');
  expect(runtime).not.toMatch(/registry\?:\s*VisualRuntimePropertyRegistryPort/);
  expect(runtime).toContain('VISUAL_RUNTIME_OBJECT_TYPE_MISMATCH');
  expect(runtime).toContain('readPropertyState(propertyKey: string)');
  expect(runtime).toContain('this.readRuntimeReadable(propertyKey)');
});
