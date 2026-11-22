import { chmod, lstat, readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { BLOB_DRIVER_ID, KV_DRIVER_ID } from './encrypted-storage.mjs';

function fail(code) {
  throw new Error(code);
}

export async function inspectStorageDirectory(directory, activeDriver = null) {
  const directoryInfo = await lstat(directory);
  if (!directoryInfo.isDirectory() || directoryInfo.isSymbolicLink()) fail('MATTER_STORAGE_DIRECTORY_INVALID');
  const entries = await readdir(directory, { withFileTypes: true });
  let driver = activeDriver;
  const descriptorEntry = entries.find(entry => entry.name === 'driver.json');
  if (descriptorEntry) {
    const descriptorPath = join(directory, 'driver.json');
    const descriptorInfo = await lstat(descriptorPath);
    if (!descriptorInfo.isFile() || descriptorInfo.isSymbolicLink()) fail('MATTER_STORAGE_DESCRIPTOR_INVALID');
    let descriptor;
    try { descriptor = JSON.parse(await readFile(descriptorPath, 'utf8')); }
    catch { fail('MATTER_STORAGE_DESCRIPTOR_INVALID'); }
    const supported = descriptor?.type === 'kv' && descriptor.kind === KV_DRIVER_ID ||
      descriptor?.type === 'blob' && descriptor.kind === BLOB_DRIVER_ID;
    if (!supported) fail('MATTER_STORAGE_LEGACY_OR_INCOMPATIBLE');
    driver = descriptor.kind;
  }

  for (const entry of entries) {
    if (entry.name === 'driver.json') continue;
    const path = join(directory, entry.name);
    const info = await lstat(path);
    if (info.isSymbolicLink()) fail('MATTER_STORAGE_SYMLINK_FORBIDDEN');
    if (entry.isDirectory()) {
      await inspectStorageDirectory(path, driver);
      continue;
    }
    if (!entry.isFile()) fail('MATTER_STORAGE_ENTRY_UNSUPPORTED');
    if (driver === null) fail('MATTER_STORAGE_UNOWNED_OR_PLAINTEXT_STATE');
  }
}

export async function hardenStorageTree(directory) {
  const directoryInfo = await lstat(directory);
  if (!directoryInfo.isDirectory() || directoryInfo.isSymbolicLink()) fail('MATTER_STORAGE_DIRECTORY_INVALID');
  await chmod(directory, 0o700);
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    const info = await lstat(path);
    if (info.isSymbolicLink()) fail('MATTER_STORAGE_SYMLINK_FORBIDDEN');
    if (entry.isDirectory()) {
      await hardenStorageTree(path);
    } else if (entry.isFile()) {
      await chmod(path, 0o600);
    } else {
      fail('MATTER_STORAGE_ENTRY_UNSUPPORTED');
    }
  }
}
