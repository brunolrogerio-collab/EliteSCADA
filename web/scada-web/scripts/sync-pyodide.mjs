import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, '..');
const source = resolve(webRoot, 'node_modules', 'pyodide');
const target = resolve(webRoot, 'public', 'pyodide');

if (!existsSync(source)) {
  throw new Error(`Pinned Pyodide package was not installed at ${source}. Run npm install first.`);
}

mkdirSync(dirname(target), { recursive: true });
rmSync(target, { recursive: true, force: true });
cpSync(source, target, {
  recursive: true,
  filter(sourcePath) {
    const relative = sourcePath.slice(source.length).replaceAll('\\', '/');
    if (!relative || relative === '/') return true;
    return !relative.includes('/node_modules/') &&
      !relative.endsWith('/package.json') &&
      !relative.endsWith('/README.md') &&
      !relative.endsWith('/LICENSE');
  }
});

console.log('Published pinned Pyodide runtime assets to public/pyodide.');
