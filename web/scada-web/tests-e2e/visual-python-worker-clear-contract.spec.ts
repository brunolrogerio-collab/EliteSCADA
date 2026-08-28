import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test('real Client Visual Python Worker exposes explicit visual property clear without a null sentinel', async () => {
  const worker = await readFile(
    new URL('../src/python-runtime/clientVisualPythonWorker.ts', import.meta.url),
    'utf8'
  );

  expect(worker).toContain('visual_property_clear:');
  expect(worker).toContain("requestCapability('visualProperty.write', 'clear'");
  expect(worker).not.toMatch(/visual_property_clear[^]*value:\s*null/);
});
