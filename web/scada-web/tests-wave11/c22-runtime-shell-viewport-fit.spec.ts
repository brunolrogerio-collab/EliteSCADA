import { expect, test } from '@playwright/test';

test('C22 Active HMI Runtime fits the browser viewport with Runtime views navigation', async ({ page }) => {
  await page.goto('/');

  const activeApplication = page.getByTestId('runtime-engineering-application');
  await expect(activeApplication).toBeVisible();
  await expect(page.locator('.runtime-view-navigation')).toBeVisible();
  await expect(page.getByTestId('runtime-visual-navigator')).toBeVisible();

  const metrics = await page.evaluate(() => {
    const runtime = document.querySelector<HTMLElement>('[data-testid="runtime-engineering-application"]');
    const navigation = document.querySelector<HTMLElement>('.runtime-view-navigation');
    if (!runtime || !navigation) throw new Error('C22 Runtime shell elements are missing.');

    const runtimeRect = runtime.getBoundingClientRect();
    const navigationRect = navigation.getBoundingClientRect();
    return {
      viewportHeight: window.innerHeight,
      documentScrollHeight: document.documentElement.scrollHeight,
      bodyScrollHeight: document.body.scrollHeight,
      runtimeBottom: runtimeRect.bottom,
      runtimeHeight: runtimeRect.height,
      navigationHeight: navigationRect.height
    };
  });

  expect(metrics.navigationHeight).toBe(46);
  expect(metrics.documentScrollHeight).toBeLessThanOrEqual(metrics.viewportHeight + 1);
  expect(metrics.bodyScrollHeight).toBeLessThanOrEqual(metrics.viewportHeight + 1);
  expect(metrics.runtimeBottom).toBeLessThanOrEqual(metrics.viewportHeight + 1);
  expect(metrics.runtimeHeight).toBeGreaterThan(0);
});
