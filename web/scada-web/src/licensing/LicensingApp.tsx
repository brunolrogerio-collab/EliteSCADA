import React, { useCallback, useEffect, useMemo, useState } from 'react';
import './licensing.css';

type Locale = 'pt-BR' | 'en' | 'es';

type LicenseStatus = {
  state: string;
  tier?: string | null;
  maximumTags?: number | null;
  demoMaximumContinuousMinutes?: number | null;
  licenseId?: string | null;
  issuedAtUtc?: string | null;
  notAfterUtc?: string | null;
  keyId?: string | null;
  diagnostic?: string | null;
};

type RuntimeEntitlementStatus = {
  state: string;
  activeLicenseState?: string | null;
  activeTier?: string | null;
  maximumTags?: number | null;
  demoStartedAtUtc?: string | null;
  demoExpiresAtUtc?: string | null;
  demoRemaining?: string | null;
  lastDiagnostic?: string | null;
};

type StatusResponse = {
  license: LicenseStatus;
  runtime: RuntimeEntitlementStatus;
};

type RequestResponse = {
  schemaVersion: number;
  requestCode: string;
  machineFingerprint: string;
};

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');
const localeKey = 'elitescada.engineering.locale';

const messages = {
  'pt-BR': {
    title: 'Licenciamento', subtitle: 'Estado da licença, modo Demo e ativação desta máquina.',
    refresh: 'Atualizar', license: 'Licença', runtime: 'Runtime', state: 'Estado', tier: 'Faixa', tags: 'Limite de TAGs',
    licenseId: 'ID da licença', expires: 'Validade', diagnostic: 'Diagnóstico', demoRemaining: 'Demo restante',
    unlimited: 'Ilimitado', none: 'Não informado', machineRequest: 'Solicitação da máquina', requestHelp: 'Copie este código e envie para a autoridade de licenciamento EliteSCADA. Identificadores brutos de hardware não são exibidos.',
    copy: 'Copiar código', copied: 'Código da máquina copiado.', install: 'Instalar licença', licenseCode: 'Código de licença assinado',
    validateInstall: 'Validar e instalar', remove: 'Remover licença', installed: 'Licença validada e instalada.', removed: 'Licença removida. As próximas ativações usarão o modo Demo.',
    loading: 'Carregando…', loadError: 'Não foi possível carregar o estado de licenciamento.', installError: 'Não foi possível instalar a licença.', removeConfirm: 'Remover a licença instalada desta máquina?'
  },
  en: {
    title: 'Licensing', subtitle: 'License state, Demo mode and activation for this machine.',
    refresh: 'Refresh', license: 'License', runtime: 'Runtime', state: 'State', tier: 'Tier', tags: 'TAG limit',
    licenseId: 'License ID', expires: 'Expiry', diagnostic: 'Diagnostic', demoRemaining: 'Demo remaining',
    unlimited: 'Unlimited', none: 'Not provided', machineRequest: 'Machine request', requestHelp: 'Copy this code and send it to the EliteSCADA licensing authority. Raw hardware identifiers are not exposed.',
    copy: 'Copy request', copied: 'Machine request copied.', install: 'Install license', licenseCode: 'Signed license code',
    validateInstall: 'Validate and install', remove: 'Remove license', installed: 'License validated and installed.', removed: 'License removed. Future activations will use Demo mode.',
    loading: 'Loading…', loadError: 'Licensing status could not be loaded.', installError: 'License could not be installed.', removeConfirm: 'Remove the installed license from this machine?'
  },
  es: {
    title: 'Licenciamiento', subtitle: 'Estado de licencia, modo Demo y activación de esta máquina.',
    refresh: 'Actualizar', license: 'Licencia', runtime: 'Runtime', state: 'Estado', tier: 'Nivel', tags: 'Límite de TAGs',
    licenseId: 'ID de licencia', expires: 'Validez', diagnostic: 'Diagnóstico', demoRemaining: 'Demo restante',
    unlimited: 'Ilimitado', none: 'No informado', machineRequest: 'Solicitud de la máquina', requestHelp: 'Copie este código y envíelo a la autoridad de licenciamiento EliteSCADA. No se muestran identificadores brutos de hardware.',
    copy: 'Copiar código', copied: 'Código de máquina copiado.', install: 'Instalar licencia', licenseCode: 'Código de licencia firmado',
    validateInstall: 'Validar e instalar', remove: 'Eliminar licencia', installed: 'Licencia validada e instalada.', removed: 'Licencia eliminada. Las próximas activaciones usarán el modo Demo.',
    loading: 'Cargando…', loadError: 'No fue posible cargar el estado de licenciamiento.', installError: 'No fue posible instalar la licencia.', removeConfirm: '¿Eliminar la licencia instalada de esta máquina?'
  }
} as const;

function resolveLocale(): Locale {
  const stored = window.localStorage.getItem(localeKey);
  if (stored === 'en' || stored === 'es') return stored;
  return 'pt-BR';
}

async function readJson<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null) as T | { error?: string } | null;
  if (!response.ok) {
    const message = body && typeof body === 'object' && 'error' in body && body.error
      ? body.error
      : `${response.status} ${response.statusText}`;
    throw new Error(message);
  }
  return body as T;
}

function displayTags(maximumTags: number | null | undefined, state: string, unlimited: string, none: string) {
  if (maximumTags != null) return maximumTags.toLocaleString();
  return state === 'Valid' ? unlimited : none;
}

function displayDate(value: string | null | undefined, none: string) {
  if (!value) return none;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
}

export function LicensingApp() {
  const locale = useMemo(resolveLocale, []);
  const t = messages[locale];
  const [status, setStatus] = useState<StatusResponse | null>(null);
  const [request, setRequest] = useState<RequestResponse | null>(null);
  const [licenseCode, setLicenseCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setError('');
    const [statusResponse, requestResponse] = await Promise.all([
      fetch(`${API}/api/licensing/status`, { headers: { accept: 'application/json' } }),
      fetch(`${API}/api/licensing/request`, { headers: { accept: 'application/json' } })
    ]);
    setStatus(await readJson<StatusResponse>(statusResponse));
    setRequest(await readJson<RequestResponse>(requestResponse));
  }, []);

  useEffect(() => {
    void load().catch(() => setError(t.loadError));
  }, [load, t.loadError]);

  const copyRequest = async () => {
    if (!request?.requestCode) return;
    await navigator.clipboard.writeText(request.requestCode);
    setNotice(t.copied);
    setError('');
  };

  const install = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!licenseCode.trim()) return;
    setBusy(true);
    setNotice('');
    setError('');
    try {
      const response = await fetch(`${API}/api/licensing/install`, {
        method: 'POST',
        headers: { accept: 'application/json', 'content-type': 'application/json' },
        body: JSON.stringify({ licenseCode: licenseCode.trim() })
      });
      await readJson(response);
      setLicenseCode('');
      setNotice(t.installed);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.installError);
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!window.confirm(t.removeConfirm)) return;
    setBusy(true);
    setNotice('');
    setError('');
    try {
      const response = await fetch(`${API}/api/licensing/license`, {
        method: 'DELETE', headers: { accept: 'application/json' }
      });
      await readJson(response);
      setNotice(t.removed);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.loadError);
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="shell licensing-page">
      <header className="licensing-heading">
        <div><h1>{t.title}</h1><p>{t.subtitle}</p></div>
        <button type="button" onClick={() => void load().catch(() => setError(t.loadError))} disabled={busy}>{t.refresh}</button>
      </header>

      {!status && !error && <div className="licensing-message">{t.loading}</div>}
      {error && <div className="licensing-message error" role="alert">{error}</div>}
      {notice && <div className="licensing-message success" role="status">{notice}</div>}

      {status && (
        <div className="licensing-grid">
          <section className="licensing-card">
            <h2>{t.license}</h2>
            <dl>
              <dt>{t.state}</dt><dd><strong>{status.license.state}</strong></dd>
              <dt>{t.tier}</dt><dd>{status.license.tier ?? t.none}</dd>
              <dt>{t.tags}</dt><dd>{displayTags(status.license.maximumTags, status.license.state, t.unlimited, t.none)}</dd>
              <dt>{t.licenseId}</dt><dd className="licensing-mono">{status.license.licenseId ?? t.none}</dd>
              <dt>{t.expires}</dt><dd>{displayDate(status.license.notAfterUtc, t.none)}</dd>
              <dt>{t.diagnostic}</dt><dd>{status.license.diagnostic ?? t.none}</dd>
            </dl>
          </section>

          <section className="licensing-card">
            <h2>{t.runtime}</h2>
            <dl>
              <dt>{t.state}</dt><dd><strong>{status.runtime.state}</strong></dd>
              <dt>{t.tier}</dt><dd>{status.runtime.activeTier ?? t.none}</dd>
              <dt>{t.tags}</dt><dd>{displayTags(status.runtime.maximumTags, status.runtime.activeLicenseState ?? '', t.unlimited, t.none)}</dd>
              <dt>{t.demoRemaining}</dt><dd>{status.runtime.demoRemaining ?? t.none}</dd>
              <dt>{t.expires}</dt><dd>{displayDate(status.runtime.demoExpiresAtUtc, t.none)}</dd>
              <dt>{t.diagnostic}</dt><dd>{status.runtime.lastDiagnostic ?? t.none}</dd>
            </dl>
          </section>

          <section className="licensing-card licensing-wide">
            <h2>{t.machineRequest}</h2>
            <textarea className="licensing-request" readOnly value={request?.requestCode ?? ''} aria-label={t.machineRequest} />
            <div className="licensing-actions">
              <button type="button" onClick={() => void copyRequest()} disabled={!request?.requestCode}>{t.copy}</button>
            </div>
            <p className="licensing-help">{t.requestHelp}</p>
          </section>

          <section className="licensing-card licensing-wide">
            <h2>{t.install}</h2>
            <form onSubmit={install}>
              <label htmlFor="license-code">{t.licenseCode}</label>
              <textarea id="license-code" value={licenseCode} onChange={event => setLicenseCode(event.target.value)} placeholder="ESLIC1..." />
              <div className="licensing-actions">
                <button type="submit" disabled={busy || !licenseCode.trim()}>{t.validateInstall}</button>
                <button type="button" className="licensing-danger" disabled={busy} onClick={() => void remove()}>{t.remove}</button>
              </div>
            </form>
          </section>
        </div>
      )}
    </main>
  );
}
