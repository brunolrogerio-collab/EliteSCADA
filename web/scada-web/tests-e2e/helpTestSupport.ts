import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Page } from '@playwright/test';

export async function installHelpShell(page: Page) {
  await page.route('**/api/runtime/application', route => route.fulfill({ json: { mode: 'neutral' } }));
  await page.route('**/api/auth/config', route => route.fulfill({ json: {
    authenticationEnabled: false, localLoginEnabled: false,
    initialAdministratorRequired: false, initialAdministratorSetupAvailable: false,
    initialAdministratorBlockedReason: null, passwordPolicy: { minimumLength: 8, maximumLength: 1024 }
  } }));
  await page.route('**/api/auth/effective-capabilities', route => route.fulfill({ json: {
    authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 }, authenticationEnabled: false,
    runtime: ['View'], workspace: ['EngineeringView']
  } }));
}

export async function installCurrentHelpCatalog(page: Page) {
  await installHelpShell(page);
  const output = process.env.SCADIVO_HELP_CATALOG_OUTPUT;
  if (!output) return; // Normal CI uses the actual API configured by playwright.config.ts.
  // Local isolated review uses the exact serialized catalog produced by the .NET tests, not authored mock prose.
  await page.route('**/api/help?*', route => {
    const locale = new URL(route.request().url()).searchParams.get('locale') ?? 'pt-BR';
    return route.fulfill({ contentType: 'application/json', body: readFileSync(join(output, `help-${locale}.json`), 'utf8') });
  });
}
