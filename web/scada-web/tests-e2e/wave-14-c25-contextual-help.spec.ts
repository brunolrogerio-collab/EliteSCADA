import { expect, test, type Page } from '@playwright/test';

test.use({ locale: 'pt-BR' });

const authConfiguration = {
  authenticationEnabled: false,
  localLoginEnabled: false,
  initialAdministratorRequired: false,
  initialAdministratorSetupAvailable: false,
  initialAdministratorBlockedReason: null,
  passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
};

const localeText = {
  'pt-BR': { title: 'Modbus TCP', summary: 'Driver de comunicação registrado nesta compilação do EliteSCADA.' },
  en: { title: 'Modbus TCP', summary: 'Communication driver registered in this EliteSCADA build.' },
  es: { title: 'Modbus TCP', summary: 'Driver de comunicación registrado en esta compilación de EliteSCADA.' }
} as const;

async function installHelpContract(page: Page) {
  await page.route('**/api/auth/config', route => route.fulfill({ json: authConfiguration }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({
    json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false,
      runtime: ['View', 'TrendUse', 'SystemAdmin'],
      workspace: ['EngineeringView', 'EngineeringModify', 'UserRoleAdmin', 'SystemAdmin']
    }
  }));
  await page.route('**/api/help?*', route => {
    const url = new URL(route.request().url());
    const locale = (url.searchParams.get('locale') ?? 'pt-BR') as keyof typeof localeText;
    const text = localeText[locale] ?? localeText['pt-BR'];
    return route.fulfill({
      json: {
        locale,
        supportedLocales: ['pt-BR', 'en', 'es'],
        serverScriptApi: [
          'read_tag',
          'read_server_memory',
          'write_tag',
          'write_server_memory',
          'publish_server_memory_sample',
          'emit_operational_event'
        ],
        topics: [
          {
            id: 'driver.modbus.tcp',
            category: 'drivers',
            title: text.title,
            summary: text.summary,
            sections: [{ heading: 'Type key', body: 'modbus.tcp' }]
          },
          {
            id: 'runtime.overview',
            category: 'runtime',
            title: 'Runtime',
            summary: 'Runtime',
            sections: []
          },
          {
            id: 'scripts.server',
            category: 'scripts',
            title: 'Server Scripts',
            summary: 'Python',
            sections: []
          }
        ]
      }
    });
  });
}

test('contextual Help resolves a stable topic locally and follows the canonical locale', async ({ page }) => {
  await installHelpContract(page);

  await page.goto('/help?topic=driver.modbus.tcp');
  const article = page.locator('[data-help-topic="driver.modbus.tcp"]');
  await expect(article).toBeVisible();
  await expect(article.getByText('Driver de comunicação registrado nesta compilação do EliteSCADA.')).toBeVisible();
  await expect(page.getByRole('link', { name: /Ajuda/ })).toHaveAttribute('aria-current', 'page');

  await page.getByLabel('Idioma').selectOption('en');
  await expect(page.locator('[data-help-locale="en"]')).toBeVisible();
  await expect(article.getByText('Communication driver registered in this EliteSCADA build.')).toBeVisible();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('elitescada.engineering.locale'))).toBe('en');
  await expect.poll(() => page.evaluate(() => document.documentElement.lang)).toBe('en');
});
