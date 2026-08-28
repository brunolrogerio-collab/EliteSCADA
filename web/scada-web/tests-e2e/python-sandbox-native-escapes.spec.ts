import { expect, test } from '@playwright/test';

test('Client Visual Python cannot recover native Pyodide JavaScript authority', async ({ page }) => {
  test.slow();
  await page.goto('/');

  const result = await page.evaluate(async () => {
    const importModule = new Function('specifier', 'return import(specifier)') as
      (specifier: string) => Promise<any>;
    const runtimeModule = await importModule('/src/python-runtime/clientVisualPythonRuntime.ts');

    const source = `
def run_js_probe(event):
    try:
        from pyodide.code import run_js
    except ImportError:
        return None

    exposed = run_js("typeof globalThis")
    if str(exposed) == "object":
        raise RuntimeError("pyodide.code.run_js recovered JavaScript global scope")


def pyodide_js_probe(event):
    try:
        import pyodide_js
    except ImportError:
        return None

    load_package = getattr(pyodide_js, "loadPackage", None)
    if load_package is not None:
        raise RuntimeError("pyodide_js exposed native package-loading authority")
    raise RuntimeError("pyodide_js exposed the native Pyodide JavaScript API")
`;

    const runtime = new runtimeModule.ClientVisualPythonRuntime({
      identity: {
        scriptId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        runtimeInstanceId: 'native-pyodide-escape-guard'
      },
      source,
      handlerNames: ['run_js_probe', 'pyodide_js_probe'],
      capabilityProvider: {}
    });

    try {
      await runtime.initialize();
      const runJs = await runtime.dispatchEvent('run_js_probe', 'security:pyodide-run-js', null);
      runtime.resetThrottle();
      const pyodideJs = await runtime.dispatchEvent('pyodide_js_probe', 'security:pyodide-js-api', null);
      return { runJs, pyodideJs };
    } finally {
      await runtime.dispose();
    }
  });

  expect(result.runJs.status).toBe('completed');
  expect(result.runJs.sanitizedError).toBeUndefined();
  expect(result.pyodideJs.status).toBe('completed');
  expect(result.pyodideJs.sanitizedError).toBeUndefined();
});
