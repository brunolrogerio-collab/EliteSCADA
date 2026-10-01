import { test, expect } from '@playwright/test';

test('mounted canonical click reaches Python tween and renders deterministic stable final state', async ({ page }) => {
  await page.goto('/');

  await page.evaluate(async () => {
    // This test mounts a runtime harness over a page that also starts the real
    // app runtime. Remove that app surface so its full-screen viewport cannot
    // intercept clicks intended for the isolated harness.
    document.querySelector('#root')?.remove();
    const host = document.createElement('div');
    host.id = 'wave10-runtime-acceptance-host';
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
