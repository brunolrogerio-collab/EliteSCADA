import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

const c04Surfaces = [
  'TagSourceSelector.tsx',
  'TagAddressEditor.tsx',
  'GenericTagBindingAssistant.tsx',
  'Dnp3TagAddressAssistant.tsx',
  'Iec104TagAddressAssistant.tsx',
  'OpcUaTagBrowser.tsx'
] as const;

test('C04 user-facing surfaces consume one multilingual resource instead of local copy tables', async () => {
  for (const fileName of c04Surfaces) {
    const source = await readFile(new URL(`../src/engineering/${fileName}`, import.meta.url), 'utf8');
    expect(source, fileName).toContain("from './c04I18n'");
    expect(source, fileName).not.toContain('function copy(locale');
  }
});

test('C04 multilingual resource contains pt-BR, en and es and localizes validation/error surfaces', async () => {
  const source = await readFile(new URL('../src/engineering/c04I18n.ts', import.meta.url), 'utf8');

  expect(source).toContain("'pt-BR': ptBR");
  expect(source).toContain('en,');
  expect(source).toContain('es');
  expect(source).toContain('integerRequired');
  expect(source).toContain('schemaUnavailable');
  expect(source).toContain('bulkSelectionRequired');
  expect(source).toContain('applyConfirm');
  expect(source).toContain('unresolved');
});
