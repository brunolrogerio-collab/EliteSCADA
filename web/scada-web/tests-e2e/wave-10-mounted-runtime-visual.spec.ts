import { test, expect } from '@playwright/test';

test('mounted canonical click reaches Python tween and renders deterministic stable final state', async ({ page }) => {
  await page.goto('/');

  await page.evaluate(async () => {
    // Use the app page only as a same-origin module host. Clear the whole body,
    // not just #root: the app can leave portal surfaces mounted directly under
    // body, where they intercept pointer input despite the root being removed.
    document.body.replaceChildren();
    const host = document.createElement('div');
    host.id = 'wave10-runtime-acceptance-host';
    host.className = 'runtime-visual-screen';
    Object.assign(host.style, {
      position: 'fixed', left: '16px', top: '16px', zIndex: '2147483647',
      width: '640px', height: '480px', pointerEvents: 'auto'
    });
    document.body.append(host);
    const harness = await import('/tests-e2e/support/Wave10RuntimeAcceptanceHarness.tsx');
    harness.mountWave10RuntimeAcceptanceHarness(host);
  });

  const host = page.locator('#wave10-runtime-acceptance-host');
  const visual = host.locator('[data-object-id="rectangle-wave10-runtime"]');
  await expect(visual).toBeVisible();
  await expect(visual).toHaveCSS('left', '0px');

  await visual.click();

  await expect(host).toHaveAttribute('data-python-handler', 'on_click');
  await expect(host).toHaveAttribute('data-script-status', 'completed');

  await page.waitForFunction(() => {
    const element = document.querySelector<HTMLElement>(
      '#wave10-runtime-acceptance-host [data-object-id="rectangle-wave10-runtime"]'
    );
    if (!element) return false;
    const left = Number.parseFloat(getComputedStyle(element).left);
    return Number.isFinite(left) && left > 0 && left < 120;
  });

  await expect(visual).toHaveCSS('left', '120px', { timeout: 5_000 });
  await page.waitForTimeout(150);
  await expect(visual).toHaveCSS('left', '120px');
});
