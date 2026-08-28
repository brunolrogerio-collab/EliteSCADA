import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

test('real Client Visual Python Worker exposes explicit visual property clear without a null sentinel', async () => {
  const worker = await readFile(
    new URL('../src/python-runtime/clientVisualPythonWorker.ts', import.meta.url),
    'utf8'
  );

  expect(worker).toContain(
    "visual_property_clear: (targetReference: unknown, propertyKey: unknown) => requestCapability('visualProperty.write', 'clear', {\n" +
    '      targetReference: normalizeBridgeValue(targetReference),\n' +
    '      propertyKey: normalizeBridgeValue(propertyKey)\n' +
    '    }),'
  );
});
