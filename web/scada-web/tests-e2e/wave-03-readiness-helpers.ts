import { expect, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test';

export type ReadinessIssueClass = 'BLOCKER' | 'MAJOR UX' | 'MINOR UX' | 'TEST GAP';

export const E2E_BASE_URL = 'http://127.0.0.1:5173';
export const LOCAL_DEVELOPER_USERNAME = 'local-developer';
export const LOCAL_DEVELOPER_PASSWORD = 'E2E-local-password-123!';

export function annotateReadinessIssue(
  testInfo: TestInfo,
  classification: ReadinessIssueClass,
  area: string,
  evidence: string
) {
  testInfo.annotations.push({
    type: classification,
    description: `${area}: ${evidence}`
  });
}

export async function openAnonymousContext(browser: Browser): Promise<BrowserContext> {
  return browser.newContext({
    baseURL: E2E_BASE_URL,
    extraHTTPHeaders: { Authorization: '' }
  });
}

export async function loginLocalDeveloper(page: Page) {
  await page.goto('/');
  await expect(page.locator('.auth-card')).toBeVisible();
  await page.locator('input[name="username"]').fill(LOCAL_DEVELOPER_USERNAME);
  await page.locator('input[name="password"]').fill(LOCAL_DEVELOPER_PASSWORD);
  await page.locator('button[type="submit"]').click();
  await expect(page.locator('.app-bar')).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole('navigation', { name: 'EliteSCADA' })).toBeVisible();
}

export async function setProductLocale(page: Page, locale: 'pt-BR' | 'en' | 'es') {
  if (page.url() === 'about:blank') await page.goto('/');
  await page.evaluate(value => window.localStorage.setItem('elitescada.engineering.locale', value), locale);
}
