import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  displayLicenseSchema,
  displaySignedHaEntitlement,
  displaySignedIntelligenceEntitlement,
  displaySignedSeatTotal
} from './licensingEntitlementPresentation';
import './licensing.css';
import { OnlineLicenseActivation } from './OnlineLicenseActivation';

type Locale = 'pt-BR' | 'en' | 'es';

type LicenseStatus = {
  state: string;
  tier?: string | null;
  maximumTags?: number | null;
  demoMaximumContinuousMinutes?: number | null;
  licenseId?: string | null;
  schemaVersion?: number | null;
  viewOnlySeats?: number | null;
  interactiveSeats?: number | null;
  haRuntime?: boolean | null;
  assetProcessIntelligence?: boolean | null;
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
  usage?: { tags: number; interactive: number; viewOnly: number; observedAtUtc: string } | null;
  sessionLimits?: { interactiveSeats: number; viewOnlySeats: number } | null;
  onlineDevice?: OnlineLicenseDeviceStatus;
};

type OnlineLicenseDeviceStatus = {
  configured: boolean;
  available: boolean;
  hasActivation: boolean;
  leasePresent: boolean;
  issuedAtUtc?: string | null;
  notAfterUtc?: string | null;
  renewalDueUtc?: string | null;
  requestPending: boolean;
  clockRollbackDetected: boolean;
  isReset: boolean;
  reauthenticationRequired: boolean;
  installationPending?: boolean;
  confirmationPending?: boolean;
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
    refresh: 'Atualizar', license: 'Licença', runtime: 'Runtime', state: 'Estado', tier: 'Faixa', tags: 'Limite de TAGs', usage: 'Em uso', unavailable: 'Indisponível',
    licenseId: 'ID da licença', schema: 'Esquema da licença', interactiveTotal: 'Clientes Runtime Interactive', viewOnlyTotal: 'Clientes Runtime View Only', haRuntime: 'Redundância / HA Runtime', assetProcessIntelligence: 'Asset & Process Intelligence',
    expires: 'Validade', diagnostic: 'Diagnóstico', demoDuration: 'Duração do Demo', noTimeLimit: 'Sem limite de tempo',
    unlimited: 'Ilimitado', none: 'Não informado', legacy: 'legado', legacyNotSpecified: 'Não especificado pela ESLIC1', yes: 'Licenciado', no: 'Não licenciado', machineRequest: 'Solicitação da máquina', requestHelp: 'Copie este código e envie para a autoridade de licenciamento SCADIVO. Identificadores brutos de hardware não são exibidos.',
    copy: 'Copiar código', copied: 'Código da máquina copiado.', install: 'Instalar licença', licenseCode: 'Código de licença assinado',
    validateInstall: 'Validar e instalar', remove: 'Remover licença', installed: 'Licença validada e instalada.', removed: 'Licença removida. As próximas ativações usarão o modo Demo.',
    onlineTitle: 'Licença online', onlineInfo: 'Resumo local para acompanhamento. Um lease armazenado não concede autorização ao Runtime.',
    onlineConfiguration: 'Configuração', onlineConfigured: 'Configurada', onlineNotConfigured: 'Não configurada', onlineState: 'Estado',
    onlineUnavailable: 'Não foi possível ler o estado online.', onlineNoActivation: 'Nenhuma ativação salva', onlineActivationStored: 'Vínculo salvo',
    onlineReset: 'Vínculo removido', onlinePending: 'Solicitação pendente', onlineLease: 'Lease armazenado', onlineIssued: 'Emitido em',
    onlineExpiry: 'Validade do lease', onlineRenewal: 'Renovação prevista', onlineClockRollback: 'Retrocesso do relógio detectado',
    onlineReauthentication: 'Reautenticação necessária', yesShort: 'Sim', noShort: 'Não',
    onlineResetAction: 'Remover vínculo local', onlineResetConfirm: 'Remover a licença online desta instalação? A vaga continuará ocupada no Portal até ser liberada lá.', onlineResetDone: 'Vínculo local removido. A vaga continua registrada no Portal.',
    loading: 'Carregando…', loadError: 'Não foi possível carregar o estado de licenciamento.', installError: 'Não foi possível instalar a licença.', removeConfirm: 'Remover a licença instalada desta máquina?'
  },
  en: {
    title: 'Licensing', subtitle: 'License state, Demo mode and activation for this machine.',
    refresh: 'Refresh', license: 'License', runtime: 'Runtime', state: 'State', tier: 'Tier', tags: 'TAG limit', usage: 'In use', unavailable: 'Unavailable',
    licenseId: 'License ID', schema: 'License schema', interactiveTotal: 'Interactive Runtime clients', viewOnlyTotal: 'View Only Runtime clients', haRuntime: 'Runtime redundancy / HA', assetProcessIntelligence: 'Asset & Process Intelligence',
    expires: 'Expiry', diagnostic: 'Diagnostic', demoDuration: 'Demo duration', noTimeLimit: 'No time limit',
    unlimited: 'Unlimited', none: 'Not provided', legacy: 'legacy', legacyNotSpecified: 'Not specified by ESLIC1', yes: 'Licensed', no: 'Not licensed', machineRequest: 'Machine request', requestHelp: 'Copy this code and send it to the SCADIVO licensing authority. Raw hardware identifiers are not exposed.',
    copy: 'Copy request', copied: 'Machine request copied.', install: 'Install license', licenseCode: 'Signed license code',
    validateInstall: 'Validate and install', remove: 'Remove license', installed: 'License validated and installed.', removed: 'License removed. Future activations will use Demo mode.',
    onlineTitle: 'Online license', onlineInfo: 'Local summary for reference. A stored lease does not grant Runtime authorization.',
    onlineConfiguration: 'Configuration', onlineConfigured: 'Configured', onlineNotConfigured: 'Not configured', onlineState: 'State',
    onlineUnavailable: 'Online status could not be read.', onlineNoActivation: 'No activation saved', onlineActivationStored: 'Binding saved',
    onlineReset: 'Binding removed', onlinePending: 'Request pending', onlineLease: 'Stored lease', onlineIssued: 'Issued',
    onlineExpiry: 'Lease expiry', onlineRenewal: 'Renewal due', onlineClockRollback: 'Clock rollback detected',
    onlineReauthentication: 'Reauthentication required', yesShort: 'Yes', noShort: 'No',
    onlineResetAction: 'Remove local binding', onlineResetConfirm: 'Remove the online license from this installation? The activation will remain occupied in the Portal until released there.', onlineResetDone: 'Local binding removed. The activation remains registered in the Portal.',
    loading: 'Loading…', loadError: 'Licensing status could not be loaded.', installError: 'License could not be installed.', removeConfirm: 'Remove the installed license from this machine?'
  },
  es: {
    title: 'Licenciamiento', subtitle: 'Estado de licencia, modo Demo y activación de esta máquina.',
    refresh: 'Actualizar', license: 'Licencia', runtime: 'Runtime', state: 'Estado', tier: 'Nivel', tags: 'Límite de TAGs', usage: 'En uso', unavailable: 'No disponible',
    licenseId: 'ID de licencia', schema: 'Esquema de licencia', interactiveTotal: 'Clientes Runtime Interactive', viewOnlyTotal: 'Clientes Runtime Solo visualización', haRuntime: 'Redundancia / HA Runtime', assetProcessIntelligence: 'Asset & Process Intelligence',
    expires: 'Validez', diagnostic: 'Diagnóstico', demoDuration: 'Duración de Demo', noTimeLimit: 'Sin límite de tiempo',
    unlimited: 'Ilimitado', none: 'No informado', legacy: 'legado', legacyNotSpecified: 'No especificado por ESLIC1', yes: 'Licenciado', no: 'No licenciado', machineRequest: 'Solicitud de la máquina', requestHelp: 'Copie este código y envíelo a la autoridad de licenciamiento SCADIVO. No se muestran identificadores brutos de hardware.',
    copy: 'Copiar código', copied: 'Código de máquina copiado.', install: 'Instalar licencia', licenseCode: 'Código de licencia firmado',
    validateInstall: 'Validar e instalar', remove: 'Eliminar licencia', installed: 'Licencia validada e instalada.', removed: 'Licencia eliminada. Las próximas activaciones usarán el modo Demo.',
    onlineTitle: 'Licencia online', onlineInfo: 'Resumen local para consulta. Un lease almacenado no concede autorización al Runtime.',
    onlineConfiguration: 'Configuración', onlineConfigured: 'Configurada', onlineNotConfigured: 'No configurada', onlineState: 'Estado',
    onlineUnavailable: 'No fue posible leer el estado online.', onlineNoActivation: 'No hay activación guardada', onlineActivationStored: 'Vínculo guardado',
    onlineReset: 'Vínculo eliminado', onlinePending: 'Solicitud pendiente', onlineLease: 'Lease almacenado', onlineIssued: 'Emitido',
    onlineExpiry: 'Validez del lease', onlineRenewal: 'Renovación prevista', onlineClockRollback: 'Se detectó un retroceso del reloj',
    onlineReauthentication: 'Se requiere reautenticación', yesShort: 'Sí', noShort: 'No',
    onlineResetAction: 'Eliminar vínculo local', onlineResetConfirm: '¿Eliminar la licencia online de esta instalación? La activación seguirá ocupada en el Portal hasta liberarla allí.', onlineResetDone: 'Vínculo local eliminado. La activación continúa registrada en el Portal.',
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

function describeOnlineState(status: OnlineLicenseDeviceStatus, t: typeof messages[Locale]) {
  if (!status.configured) return t.onlineNotConfigured;
  if (!status.available) return t.onlineUnavailable;
  if (status.isReset) return t.onlineReset;
  if (status.requestPending) return t.onlinePending;
  if (!status.hasActivation) return t.onlineNoActivation;
  return t.onlineActivationStored;
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

  const resetOnline = async () => {
    if (!window.confirm(t.onlineResetConfirm)) return;
    setBusy(true); setError(''); setNotice('');
    try {
      await readJson(await fetch(`${API}/api/licensing/online/reset`, { method: 'POST', cache: 'no-store' }));
      setNotice(t.onlineResetDone); await load();
    } catch { setError(t.loadError); }
    finally { setBusy(false); }
  };

  return (
    <main className="shell licensing-page">
      <header className="licensing-heading">
        <div><h1>{t.title}</h1><details className="app-page-help"><summary>{locale === 'pt-BR' ? 'Ajuda' : locale === 'es' ? 'Ayuda' : 'Help'}</summary><p>{t.subtitle}</p></details></div>
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
              <dt>{t.tags}</dt><dd data-testid="license-tags-total">{displayTags(status.license.maximumTags, status.license.state, t.unlimited, t.none)} <span data-testid="license-tags-usage">({t.usage}: {status.usage?.tags?.toLocaleString(locale) ?? t.unavailable})</span></dd>
              {status.license.state === 'Demo' && <>
                <dt>{t.demoDuration}</dt><dd data-testid="demo-duration">{status.license.demoMaximumContinuousMinutes === null ? t.noTimeLimit : t.none}</dd>
              </>}
              <dt>{t.schema}</dt><dd data-testid="license-schema">{displayLicenseSchema(status.license, t)}</dd>
              <dt>{t.interactiveTotal}</dt><dd data-testid="license-interactive-total">{status.sessionLimits?.interactiveSeats?.toLocaleString(locale) ?? displaySignedSeatTotal(status.license.interactiveSeats, status.license, t)} <span data-testid="license-interactive-usage">({t.usage}: {status.usage?.interactive?.toLocaleString(locale) ?? t.unavailable})</span></dd>
              <dt>{t.viewOnlyTotal}</dt><dd data-testid="license-view-only-total">{status.sessionLimits?.viewOnlySeats?.toLocaleString(locale) ?? displaySignedSeatTotal(status.license.viewOnlySeats, status.license, t)} <span data-testid="license-view-only-usage">({t.usage}: {status.usage?.viewOnly?.toLocaleString(locale) ?? t.unavailable})</span></dd>
              <dt>{t.haRuntime}</dt><dd data-testid="license-ha-runtime">{displaySignedHaEntitlement(status.license.haRuntime, status.license, t)}</dd>
              <dt>{t.assetProcessIntelligence}</dt><dd data-testid="license-asset-process-intelligence">{displaySignedIntelligenceEntitlement(status.license.assetProcessIntelligence, status.license, t)}</dd>
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
              {status.runtime.demoExpiresAtUtc && <>
                <dt>{t.expires}</dt><dd>{displayDate(status.runtime.demoExpiresAtUtc, t.none)}</dd>
              </>}
              <dt>{t.diagnostic}</dt><dd>{status.runtime.lastDiagnostic ?? t.none}</dd>
            </dl>
          </section>

          <section className="licensing-card licensing-machine-card" data-testid="machine-license-panel">
            <h2>{t.machineRequest}</h2>
            <textarea className="licensing-request" readOnly value={request?.requestCode ?? ''} aria-label={t.machineRequest} />
            <div className="licensing-actions">
              <button type="button" onClick={() => void copyRequest()} disabled={!request?.requestCode}>{t.copy}</button>
            </div>
            <p className="licensing-help">{t.requestHelp}</p>
            <div className="licensing-machine-install">
              <h3>{t.install}</h3>
              <form onSubmit={install}>
                <label htmlFor="license-code">{t.licenseCode}</label>
                <textarea id="license-code" value={licenseCode} onChange={event => setLicenseCode(event.target.value)} placeholder="ESLIC1... / ESLIC2... / ESLIC3..." />
                <div className="licensing-actions">
                  <button type="submit" disabled={busy || !licenseCode.trim()}>{t.validateInstall}</button>
                  <button type="button" className="licensing-danger" disabled={busy} onClick={() => void remove()}>{t.remove}</button>
                </div>
              </form>
            </div>
          </section>

          {status.onlineDevice && <section className="licensing-card licensing-online-card" data-testid="online-license-panel">
            <h2>{t.onlineTitle}</h2>
            <div className="licensing-online-status">
              <p className="licensing-help">{t.onlineInfo}</p>
              <dl data-testid="online-license-status">
                <dt>{t.onlineConfiguration}</dt><dd>{status.onlineDevice.configured ? t.onlineConfigured : t.onlineNotConfigured}</dd>
                <dt>{t.onlineState}</dt><dd><strong>{describeOnlineState(status.onlineDevice, t)}</strong></dd>
                {status.onlineDevice.configured && status.onlineDevice.available && <>
                  <dt>{t.onlineLease}</dt><dd>{status.onlineDevice.leasePresent ? t.yesShort : t.noShort}</dd>
                  {status.onlineDevice.issuedAtUtc && <><dt>{t.onlineIssued}</dt><dd>{displayDate(status.onlineDevice.issuedAtUtc, t.none)}</dd></>}
                  {status.onlineDevice.notAfterUtc && <><dt>{t.onlineExpiry}</dt><dd>{displayDate(status.onlineDevice.notAfterUtc, t.none)}</dd></>}
                  {status.onlineDevice.renewalDueUtc && <><dt>{t.onlineRenewal}</dt><dd>{displayDate(status.onlineDevice.renewalDueUtc, t.none)}</dd></>}
                  {status.onlineDevice.clockRollbackDetected && <><dt>{t.onlineClockRollback}</dt><dd>{t.yesShort}</dd></>}
                  {status.onlineDevice.reauthenticationRequired && <><dt>{t.onlineReauthentication}</dt><dd>{t.yesShort}</dd></>}
                </>}
              </dl>
              {status.onlineDevice.configured && status.onlineDevice.available &&
                (!status.onlineDevice.hasActivation || status.onlineDevice.confirmationPending || status.onlineDevice.installationPending || status.onlineDevice.reauthenticationRequired) &&
                <OnlineLicenseActivation api={API} locale={locale} pending={status.onlineDevice.confirmationPending === true}
                  completionPending={status.onlineDevice.installationPending === true} onChanged={load} />}
              {status.onlineDevice.hasActivation && !status.onlineDevice.requestPending && !status.onlineDevice.installationPending &&
                <div className="licensing-actions"><button type="button" className="licensing-danger" disabled={busy} onClick={() => void resetOnline()}>{t.onlineResetAction}</button></div>}
            </div>
          </section>}
        </div>
      )}
    </main>
  );
}
