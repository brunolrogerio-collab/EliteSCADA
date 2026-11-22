import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';
import { installHelpShell } from './helpTestSupport';

test('the exact installed Client Visual examples execute in the real Python worker', async ({ page }) => {
  test.slow();
  const examples = Object.fromEntries(['client-compare', 'client-hover', 'client-memory', 'client-read', 'client-store', 'client-pointer'].map(name => [name,
    readFileSync(resolve('../../src/Scada.Api/Runtime/HelpExamples', `${name}.py`), 'utf8')]));
  await installHelpShell(page);
  await page.route('**/api/help?*', route => route.fulfill({ json: { locale: 'pt-BR', supportedLocales: ['pt-BR'], topics: [], serverScriptApi: [] } }));
  await page.goto('/help');
  const result = await page.evaluate(async sources => {
    const load = new Function('path', 'return import(path)') as (path: string) => Promise<any>;
    const { ClientVisualPythonRuntime } = await load('/src/python-runtime/clientVisualPythonRuntime.ts');
    const calls: unknown[][] = [];
    let quality = 'Good';
    let current = 12;
    const provider = {
      readTag: (reference: string) => ({ value: reference.endsWith('ReferenceCurrent') ? 10 : current, quality }),
      writeVisualProperty: (target: string, key: string, value: unknown) => { calls.push(['visual', target, key, value]); },
      clearVisualProperty: (target: string, key: string) => { calls.push(['clear', target, key]); },
      writeClientMemory: (reference: string, value: unknown) => { calls.push(['memory', reference, value]); }
    };
    const statuses: string[] = [];
    const errors: string[] = [];
    const collect = (result: { status: string; sanitizedError?: string }) => { statuses.push(result.status); if (result.sanitizedError) errors.push(result.sanitizedError); };
    for (const [name, source] of Object.entries(sources)) {
      const handlers = name === 'client-hover' ? ['on_enter', 'on_leave'] : name === 'client-memory' ? ['remember_pressure'] : name === 'client-pointer' ? ['on_pointer_enter'] : ['on_click'];
      const runtime = new ClientVisualPythonRuntime({
        identity: { scriptId: name, runtimeInstanceId: `help-${name}` }, source, handlerNames: handlers, capabilityProvider: provider
      });
      try {
        await runtime.initialize();
        for (const handler of handlers) collect(await runtime.dispatchEvent(handler, 'help-example', { visualObjectId: 'rectangle-help', eventKey: 'pointer.enter' }));
        if (name === 'client-compare') {
          current = 8;
          collect(await runtime.dispatchEvent('on_click', 'below-reference', { visualObjectId: 'rectangle-help' }));
          quality = 'Bad';
          collect(await runtime.dispatchEvent('on_click', 'bad-quality', { visualObjectId: 'rectangle-help' }));
          quality = 'Good';
        }
      } finally { await runtime.dispose(); }
    }
    return { statuses, calls, errors };
  }, examples);
  expect(result.statuses, JSON.stringify(result.errors)).toEqual(Array(9).fill('completed'));
  expect(result.calls).toEqual([
    ['visual', 'rectangle-help', 'fillColor', '#FFB300'],
    ['visual', 'rectangle-help', 'fillColor', '#2384C6'],
    ['visual', 'rectangle-help', 'fillColor', '#808080'],
    ['visual', 'rectangle-help', 'opacity', 0.7],
    ['clear', 'rectangle-help', 'opacity'],
    ['memory', 'UI.LastPressure', 8],
    ['memory', 'UI.LastPressure', 8]
  ]);
});
