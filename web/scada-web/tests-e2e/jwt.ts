import { createHmac } from 'node:crypto';

export const E2E_AUTH_ISSUER = 'EliteSCADA.E2E';
export const E2E_AUTH_AUDIENCE = 'EliteSCADA.Api';
export const E2E_AUTH_SIGNING_KEY = 'elitescada-e2e-only-signing-key-2026-keep-out-of-production';

function base64Url(value: string | Buffer): string {
  return Buffer.from(value)
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}

export function createE2eJwt(
  subject: string,
  roles: string[],
  displayName = subject
): string {
  const now = Math.floor(Date.now() / 1000);
  const header = base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = base64Url(JSON.stringify({
    iss: E2E_AUTH_ISSUER,
    aud: E2E_AUTH_AUDIENCE,
    sub: subject,
    name: displayName,
    role: roles,
    iat: now,
    nbf: now - 5,
    exp: now + 3600
  }));
  const unsigned = `${header}.${payload}`;
  const signature = createHmac('sha256', E2E_AUTH_SIGNING_KEY)
    .update(unsigned)
    .digest();
  return `${unsigned}.${base64Url(signature)}`;
}
