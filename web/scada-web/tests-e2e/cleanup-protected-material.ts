import { rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, resolve, sep } from 'node:path';

export default async function cleanupProtectedMaterialTestStore() {
  const configuredPath = process.env.ELITESCADA_E2E_PROTECTED_MATERIAL_STORE;
  if (!configuredPath) return;

  const storePath = resolve(configuredPath);
  const tempRoot = resolve(tmpdir());
  if (!storePath.startsWith(tempRoot + sep) || !basename(storePath).startsWith('elitescada-e2e-protected-'))
    throw new Error('Refusing to clean an E2E protected-material path outside its dedicated temp directory.');

  await rm(storePath, { recursive: true, force: true });
}
