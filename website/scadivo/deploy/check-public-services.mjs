import assert from 'node:assert/strict';

// Read-only post-deployment checks: the demo index may still return 200 when
// its API is unreachable because a shared Docker DNS alias points elsewhere.
const site = new URL(process.argv[2] ?? 'https://scadivo.com/');
const demo = new URL(process.argv[3] ?? 'https://scadivo-demo.jywwfm.easypanel.host/');

async function get(base, path, json = false) {
  const url = new URL(path, base);
  const response = await fetch(url, { signal: AbortSignal.timeout(20000) });
  assert.equal(response.status, 200, `${url.pathname} returned HTTP ${response.status}`);
  if (json) {
    assert.match(response.headers.get('content-type') ?? '', /application\/json/,
      `${url.pathname} did not return API JSON`);
    return response.json();
  }
  await response.arrayBuffer();
}

await get(site, '/');
await get(site, '/demo');
await get(demo, '/health');
const auth = await get(demo, '/api/auth/config', true);
assert.equal(auth.publicRuntimeEnabled, true, 'Anonymous Runtime is unavailable');
assert.equal(auth.localLoginEnabled, true, 'Optional demo sign-in is unavailable');

// fetch has no cookie jar: all these requests use the anonymous visitor path.
const capabilities = await get(demo, '/api/auth/effective-capabilities', true);
assert.ok(capabilities.runtime.includes('View'), 'Visitor cannot view Runtime');
assert.ok(capabilities.runtime.includes('TagRead'), 'Visitor cannot read live tags');
assert.equal(capabilities.workspace.length, 0, 'Visitor unexpectedly has workspace permissions');
const application = await get(demo, '/api/runtime/application', true);
assert.equal(application.mode, 'engineering', 'The configured demo is not active');
assert.ok(application.package?.screens?.length > 0, 'No demo screens reached the visitor');

const portal = await get(site, '/portal/api/session', true);
assert.equal(portal.authenticated, false, 'Fresh portal session is not anonymous');
console.log(JSON.stringify({
  result: 'PASS', anonymousRuntime: true, demoScreens: application.package.screens.length,
  optionalLoginEnabled: true, anonymousWorkspacePermissions: 0, portalSession: 'healthy'
}));
