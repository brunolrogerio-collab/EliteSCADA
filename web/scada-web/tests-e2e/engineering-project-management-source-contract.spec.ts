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
}
