import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';

async function source(relativePath: string): Promise<string> {
  return await readFile(new URL(relativePath, import.meta.url), 'utf8');
}

test('visual property foundation stays renderer independent and contains no arbitrary any property authority', async () => {
  const types = await source('../src/visual-runtime/visualPropertyTypes.ts');
  const registry = await source('../src/visual-runtime/visualPropertyRegistry.ts');
  const projection = await source('../src/visual-runtime/visualEngineeringProjection.ts');
  const combined = `${types}\n${registry}\n${projection}`;

  expect(combined).not.toMatch(/Record<string,\s*any>/);
  expect(combined).not.toMatch(/:\s*any(?:[;>,]|\s)/);
  expect(combined).not.toContain('HTMLElement');
  expect(combined).not.toContain('React.');
  expect(combined).not.toContain('document.');
  expect(combined).not.toContain('querySelector');
  expect(combined).not.toContain('CSSStyleDeclaration');
  expect(combined).not.toContain('window.');
});

test('AssetReference source exposes only stable project asset identity and no URL or filesystem authority', async () => {
  const types = await source('../src/visual-runtime/visualPropertyTypes.ts');
  const registry = await source('../src/visual-runtime/visualPropertyRegistry.ts');

  expect(types).toContain('assetId: string;');
  expect(types).toContain("keys.length === 1 && keys[0] === 'assetId'");
  expect(types).not.toMatch(/\b(name|mediaType|path|url|href|filePath)\??:\s*string/);
  expect(registry).toContain('defaultValue: null');
  expect(registry).not.toContain('asset:none');
  expect(registry).toContain('if (value === null) return success(definition.key, null);');
  expect(registry).toContain("runtimeWritable: false");
});

test('common registry uses Wave 07 assetRef and native image-fit naming rather than older private resource names', async () => {
  const registry = await source('../src/visual-runtime/visualPropertyRegistry.ts');

  expect(registry).toContain("assetRef: 'assetRef'");
  expect(registry).toContain("['contain', 'cover', 'fill', 'native']");
  expect(registry).not.toContain('imageResourceId');
  expect(registry).not.toContain("['contain', 'cover', 'fill', 'none']");
});
