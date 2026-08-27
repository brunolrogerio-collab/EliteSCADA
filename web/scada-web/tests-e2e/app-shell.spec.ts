import { expect, test } from '@playwright/test';

test.use({ locale: 'pt-BR' });

test('primary shell keeps Runtime, Engineering and Audit navigation coherent', async ({ page }) => {
  await page.goto('/');

  let navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(page.getByRole('link', { name: 'EliteSCADA Runtime' })).toBeVisible();
  await expect(navigation.getByRole('link', { name: /Runtime/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByLabel('Área atual')).toContainText('Runtime');

  await navigation.getByRole('link', { name: /Engineering/ }).click();
  await expect(page).toHaveURL(/\/engineering$/);
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Engineering/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByLabel('Área atual')).toContainText('Engineering');

  await navigation.getByRole('link', { name: /Auditoria/ }).click();
  await expect(page).toHaveURL(/\/audit$/);
  navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Auditoria/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByLabel('Área atual')).toContainText('Auditoria');
});

test('primary shell follows the stored Engineering locale', async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.goto('/engineering');

  const navigation = page.getByRole('navigation', { name: 'EliteSCADA' });
  await expect(navigation.getByRole('link', { name: /Audit/ })).toBeVisible();
  await expect(page.getByText('Industrial platform', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Current area')).toContainText('Engineering');
});
