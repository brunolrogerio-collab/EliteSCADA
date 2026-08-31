namespace Scada.Api.Licensing;

internal static class ProductLicensingPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>EliteSCADA Licensing</title>
  <style>
    :root { color-scheme: light dark; font-family: system-ui, -apple-system, Segoe UI, sans-serif; }
    body { margin: 0; background: Canvas; color: CanvasText; }
    main { max-width: 920px; margin: 0 auto; padding: 32px 20px 56px; }
    h1 { margin: 0 0 4px; font-size: 30px; }
    .subtitle { margin: 0 0 28px; opacity: .72; }
    .grid { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
    section { border: 1px solid color-mix(in srgb, CanvasText 18%, transparent); border-radius: 12px; padding: 18px; }
    h2 { margin: 0 0 14px; font-size: 18px; }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: 8px 14px; margin: 0; }
    dt { opacity: .7; }
    dd { margin: 0; overflow-wrap: anywhere; }
    label { display: block; margin: 12px 0 6px; font-weight: 600; }
    input, textarea { width: 100%; box-sizing: border-box; padding: 10px; border: 1px solid color-mix(in srgb, CanvasText 24%, transparent); border-radius: 8px; background: Canvas; color: CanvasText; font: inherit; }
    textarea { min-height: 118px; resize: vertical; }
    button { padding: 9px 14px; border-radius: 8px; border: 1px solid color-mix(in srgb, CanvasText 24%, transparent); cursor: pointer; font: inherit; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 12px; }
    .request { min-height: 92px; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }
    .notice { min-height: 24px; margin-top: 14px; font-weight: 600; }
    .muted { opacity: .7; font-size: 13px; }
    .wide { grid-column: 1 / -1; }
  </style>
</head>
<body>
<main>
  <h1>EliteSCADA Licensing</h1>
  <p class="subtitle">Demo status, machine request code and controlled license installation.</p>

  <section class="wide">
    <h2>Authentication</h2>
    <p class="muted">When API authentication is enabled, paste an Engineering-authorized bearer token below. It is kept only in this page's memory and is not stored by the page.</p>
    <label for="token">Bearer token</label>
    <input id="token" type="password" autocomplete="off" placeholder="Optional when authentication is disabled">
    <div class="actions"><button id="refresh">Refresh</button></div>
  </section>

  <div class="grid" style="margin-top:16px">
    <section>
      <h2>License</h2>
      <dl>
        <dt>State</dt><dd id="license-state">-</dd>
        <dt>Tier</dt><dd id="license-tier">-</dd>
        <dt>TAG limit</dt><dd id="license-tags">-</dd>
        <dt>License ID</dt><dd id="license-id">-</dd>
        <dt>Expires</dt><dd id="license-expiry">-</dd>
        <dt>Diagnostic</dt><dd id="license-diagnostic">-</dd>
      </dl>
    </section>

    <section>
      <h2>Runtime entitlement</h2>
      <dl>
        <dt>State</dt><dd id="runtime-state">-</dd>
        <dt>Active tier</dt><dd id="runtime-tier">-</dd>
        <dt>TAG limit</dt><dd id="runtime-tags">-</dd>
        <dt>Demo remaining</dt><dd id="runtime-remaining">-</dd>
        <dt>Diagnostic</dt><dd id="runtime-diagnostic">-</dd>
      </dl>
    </section>

    <section class="wide">
      <h2>Machine request</h2>
      <label for="request-code">Request code</label>
      <textarea id="request-code" class="request" readonly></textarea>
      <div class="actions">
        <button id="load-request">Load request</button>
        <button id="copy-request">Copy request code</button>
      </div>
      <p class="muted">Send only this request code to the controlled EliteSCADA licensing authority. Raw hardware identifiers are not included.</p>
    </section>

    <section class="wide">
      <h2>Install license</h2>
      <label for="license-code">Signed license code</label>
      <textarea id="license-code" placeholder="ESLIC1..."></textarea>
      <div class="actions">
        <button id="install">Validate and install</button>
        <button id="remove">Remove installed license</button>
      </div>
      <div id="notice" class="notice" aria-live="polite"></div>
    </section>
  </div>
</main>
<script>
(() => {
  const byId = id => document.getElementById(id);
  const text = (id, value) => byId(id).textContent = value == null || value === '' ? '-' : String(value);
  const token = () => byId('token').value.trim();
  const headers = (json = false) => {
    const result = {};
    if (json) result['Content-Type'] = 'application/json';
    const value = token();
    if (value) result['Authorization'] = 'Bearer ' + value;
    return result;
  };
  const notify = message => byId('notice').textContent = message || '';
  const api = async (path, options = {}) => {
    const response = await fetch(path, options);
    const body = await response.json().catch(() => null);
    if (!response.ok) {
      const error = body && (body.error || body.title) ? (body.error || body.title) : `HTTP ${response.status}`;
      throw new Error(error);
    }
    return body;
  };
  const minutes = value => {
    if (!value) return '-';
    if (typeof value === 'string') return value;
    const totalSeconds = Math.max(0, Math.floor(value.totalSeconds ?? 0));
    const hours = Math.floor(totalSeconds / 3600);
    const mins = Math.floor((totalSeconds % 3600) / 60);
    const secs = totalSeconds % 60;
    return `${hours}h ${mins}m ${secs}s`;
  };
  async function refresh() {
    notify('');
    const result = await api('/api/licensing/status', { headers: headers() });
    const license = result.license || {};
    const runtime = result.runtime || {};
    text('license-state', license.state);
    text('license-tier', license.tier);
    text('license-tags', license.maximumTags == null && license.state === 'Valid' ? 'Unlimited' : license.maximumTags);
    text('license-id', license.licenseId);
    text('license-expiry', license.notAfterUtc || 'Never');
    text('license-diagnostic', license.diagnostic);
    text('runtime-state', runtime.state);
    text('runtime-tier', runtime.activeTier);
    text('runtime-tags', runtime.maximumTags == null && runtime.activeLicenseState === 'Valid' ? 'Unlimited' : runtime.maximumTags);
    text('runtime-remaining', minutes(runtime.demoRemaining));
    text('runtime-diagnostic', runtime.lastDiagnostic);
  }
  async function loadRequest() {
    notify('');
    const result = await api('/api/licensing/request', { headers: headers() });
    byId('request-code').value = result.requestCode || '';
  }
  byId('refresh').addEventListener('click', () => refresh().catch(e => notify(e.message)));
  byId('load-request').addEventListener('click', () => loadRequest().catch(e => notify(e.message)));
  byId('copy-request').addEventListener('click', async () => {
    try {
      if (!byId('request-code').value) await loadRequest();
      await navigator.clipboard.writeText(byId('request-code').value);
      notify('Machine request code copied.');
    } catch (e) { notify(e.message); }
  });
  byId('install').addEventListener('click', async () => {
    try {
      notify('');
      await api('/api/licensing/install', {
        method: 'POST',
        headers: headers(true),
        body: JSON.stringify({ licenseCode: byId('license-code').value })
      });
      byId('license-code').value = '';
      notify('License validated and installed. The active runtime is not silently restarted.');
      await refresh();
    } catch (e) { notify(e.message); }
  });
  byId('remove').addEventListener('click', async () => {
    try {
      notify('');
      await api('/api/licensing/license', { method: 'DELETE', headers: headers() });
      notify('Installed license removed. Future Run activations use Demo entitlement.');
      await refresh();
    } catch (e) { notify(e.message); }
  });
  refresh().catch(() => {});
  loadRequest().catch(() => {});
})();
</script>
</body>
</html>
""";
}
