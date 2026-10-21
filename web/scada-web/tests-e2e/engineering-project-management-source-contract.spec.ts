import { expect, test } from '@playwright/test';
import { projectManagementCopy } from '../src/engineering/EngineeringProjectManagementWorkspace.copy';

for (const locale of ['pt-BR', 'en', 'es'] as const) {
  test(`project backup boundary is explicit in ${locale}`, () => {
    const copy = projectManagementCopy(locale);
    expect(copy.secretsHint.length).toBeGreaterThan(40);
    expect(copy.excluded.length).toBeGreaterThanOrEqual(5);
    const boundary = `${copy.secretsHint} ${copy.excluded.join(' ')}`.toLowerCase();
    expect(boundary).toContain(locale === 'pt-BR' ? 'credenciais' : locale === 'es' ? 'credenciales' : 'credentials');
    expect(boundary).toContain(locale === 'pt-BR' ? 'sessões' : locale === 'es' ? 'sesiones' : 'sessions');
  });

  test(`tabular Engineering import/export boundary is explicit in ${locale}`, () => {
    const copy = projectManagementCopy(locale);
    expect(copy.tabularTitle).toContain('CSV');
    expect(copy.tabularEntities).toHaveLength(5);
    expect(copy.tabularDescription.toLowerCase()).toContain('runtime');
    expect(copy.tabularDescription.toLowerCase()).toContain('fragment');
    expect(copy.tabularIdentityHint.toLowerCase()).toContain(locale === 'en' ? 'internally' : 'internamente');
    expect(copy.tabularIdentityHint).toContain('DataSourceKey');
    expect(copy.tabularApplyDescription.length).toBeGreaterThan(50);
  });
}
