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

test('C04 multilingual resources contain pt-BR, en and es and localize validation, errors and protocol choices', async () => {
  const copy = await readFile(new URL('../src/engineering/c04I18n.ts', import.meta.url), 'utf8');
  const protocolLabels = await readFile(new URL('../src/engineering/c04ProtocolLabels.ts', import.meta.url), 'utf8');

  for (const source of [copy, protocolLabels]) {
    expect(source).toContain("'pt-BR'");
    expect(source).toContain('en');
    expect(source).toContain('es');
  }

  expect(copy).toContain('integerRequired');
  expect(copy).toContain('schemaUnavailable');
  expect(copy).toContain('bulkSelectionRequired');
  expect(copy).toContain('applyConfirm');
  expect(copy).toContain('unresolved');

  expect(protocolLabels).toContain('modbusArea');
  expect(protocolLabels).toContain('dnp3PointKind');
  expect(protocolLabels).toContain('iec104CommandMode');

  const tagAddressEditor = await readFile(new URL('../src/engineering/TagAddressEditor.tsx', import.meta.url), 'utf8');
  const dnp3 = await readFile(new URL('../src/engineering/Dnp3TagAddressAssistant.tsx', import.meta.url), 'utf8');
  const iec104 = await readFile(new URL('../src/engineering/Iec104TagAddressAssistant.tsx', import.meta.url), 'utf8');
  expect(tagAddressEditor).toContain("from './c04ProtocolLabels'");
  expect(dnp3).toContain("from './c04ProtocolLabels'");
  expect(iec104).toContain("from './c04ProtocolLabels'");
});
