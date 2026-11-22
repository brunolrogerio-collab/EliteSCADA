import { expect, test } from '@playwright/test';

test('C22 Active HMI Runtime fits the browser viewport with Runtime toolbar', async ({ page }) => {
  await page.goto('/');

  const activeApplication = page.getByTestId('runtime-engineering-application');
  const runtimeToolbar = page.locator('.runtime-operator-toolbar');
  await expect(activeApplication).toBeVisible();
  await expect(runtimeToolbar).toBeVisible();
  await expect(runtimeToolbar.getByRole('button', { name: 'Overview' })).toBeVisible();
  await expect(page.getByTestId('runtime-visual-navigator')).toBeVisible();

  const metrics = await page.evaluate(() => {
    const runtime = document.querySelector<HTMLElement>('[data-testid="runtime-engineering-application"]');
    const toolbar = document.querySelector<HTMLElement>('.runtime-operator-toolbar');
    if (!runtime || !toolbar) throw new Error('C22 Runtime shell elements are missing.');

    const runtimeRect = runtime.getBoundingClientRect();
    const toolbarRect = toolbar.getBoundingClientRect();
    return {
      viewportHeight: window.innerHeight,
      documentScrollHeight: document.documentElement.scrollHeight,
      bodyScrollHeight: document.body.scrollHeight,
      runtimeBottom: runtimeRect.bottom,
      runtimeHeight: runtimeRect.height,
      toolbarHeight: toolbarRect.height,
      toolbarBottom: toolbarRect.bottom,
      runtimeTop: runtimeRect.top
    };
  });

  expect(metrics.toolbarHeight).toBeGreaterThan(0);
  expect(metrics.toolbarBottom).toBeLessThanOrEqual(metrics.runtimeTop + 48);
  expect(metrics.documentScrollHeight).toBeLessThanOrEqual(metrics.viewportHeight + 1);
  expect(metrics.bodyScrollHeight).toBeLessThanOrEqual(metrics.viewportHeight + 1);
  expect(metrics.runtimeBottom).toBeLessThanOrEqual(metrics.viewportHeight + 1);
  expect(metrics.runtimeHeight).toBeGreaterThan(0);
});
