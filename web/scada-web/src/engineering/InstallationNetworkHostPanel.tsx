import React, { useEffect, useState } from 'react';
import type { EngineeringLocale } from './i18n';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const BASE = API + '/api/installation/host-network';
const SCHEMA = 'scadivo-network-port-plan-v1';
type Module = 'access' | 'database' | 'media';
type State = 'applying' | 'applied' | 'recovered' | 'recovery_required';
type Operation = { id: string; module: Module; state: State; before: Record<string, number>; desired: Record<string, number>; detail: string };
type Snapshot = { available: boolean; profile?: string; instanceId?: string; configuredPorts?: Record<string, number>; modules?: Module[]; operation?: Operation | null };
type Plan = { planId: string; module: Module; ports: Record<string, number>; before: Record<string, number>; expiresAt: number; requiresRestart: boolean };
const fields: Record<Module, Array<[string, string]>> = {
  access: [['webPort', 'Web TCP'], ['apiPort', 'API TCP']],
  database: [['databasePort', 'PostgreSQL local TCP']],
  media: [['mediaControlPort', 'Mídia: controle TCP'], ['mediaPlaybackPort', 'Mídia: HLS TCP']]
};
const translation = {
  'pt-BR': {
    title: 'Aplicação administrativa da instalação', unavailable: 'Executor administrativo não registrado ou indisponível neste host. O navegador não pode aplicar mudanças de portas; use o procedimento administrativo protegido da instalação.', unsupported: 'Este perfil não oferece aplicação pela interface. Use o adaptador administrativo do host correspondente.',
    description: 'Portas atuais reportadas pelo executor local. A prévia valida a configuração sem reiniciar serviços. Aplicar pode interromper a sessão e exige confirmação explícita.',
    fieldTitle: 'Serviço a alterar', preflight: 'Validar alteração no host', applying: 'Solicitar aplicação', confirm: 'Confirmo a interrupção e o novo endereço; os serviços poderão reiniciar.', recovery: 'Solicitar recuperação do estado anterior',
    operation: 'Última operação administrativa', status: 'Consultar execução', executing: 'Aplicação recebida, ainda não confirmada. Se a sessão cair, abra novamente pelo endereço novo e consulte o estado.',
    ready: 'Aplicação confirmada pelo executor: o adaptador concluiu a verificação de saúde e releu a configuração registrada.', recoveryRequired: 'Aplicação não confirmada. O executor exige recuperação administrativa antes de uma nova tentativa.',
    newWeb: 'Novo endereço Web esperado', pending: 'Prévia aprovada. Nenhum serviço foi alterado.', source: 'Estado da configuração do host (não é prova independente de publicação externa ou mídia reproduzida).',
    invalid: 'Portas devem ser números inteiros entre 1 e 65535 e não podem colidir.', retry: 'Falha ao consultar ou executar. Confira a autorização SystemAdmin e o agente registrado.', update: 'Atualizar estado'
  },
  en: {
    title: 'Installation host administration', unavailable: 'No registered compatible host executor is available. A browser cannot apply host port changes. Use the protected host administration procedure.', unsupported: 'This profile does not support application from the interface. Use its host administrative adapter.',
    description: 'Ports reported by the local executor. Preflight validates without restarting services. Applying may interrupt the current session and requires confirmation.',
    fieldTitle: 'Service to change', preflight: 'Validate host changes', applying: 'Request application', confirm: 'I acknowledge the interruption and new URL. Services may restart.', recovery: 'Recover previous host state',
    operation: 'Most recent host operation', status: 'Check execution', executing: 'Operation accepted, not yet confirmed. If this session drops, open the new URL and check status.',
    ready: 'Host executor confirmed application: the adapter passed its health check and registered configuration was reread.', recoveryRequired: 'Application is not confirmed; protected host recovery is required before trying again.',
    newWeb: 'Expected new Web URL', pending: 'Preflight accepted. No service has been changed.', source: 'Host configuration state (not independent verification of external publication or video playback).',
    invalid: 'Ports must be distinct integers between 1 and 65535.', retry: 'Could not query or execute. Check SystemAdmin authorization and host executor.', update: 'Refresh status'
  },
  es: {
    title: 'Administración del host de instalación', unavailable: 'No existe un ejecutor compatible registrado. El navegador no puede aplicar cambios de puertos; use el procedimiento protegido del host.', unsupported: 'Este perfil no aplica cambios desde la interfaz. Use el adaptador administrativo del host.',
    description: 'Puertos informados por el ejecutor local. La verificación previa no reinicia servicios. Aplicar puede interrumpir la sesión y requiere confirmación.',
    fieldTitle: 'Servicio que se cambiará', preflight: 'Validar cambios en el host', applying: 'Solicitar aplicación', confirm: 'Confirmo la interrupción y la nueva dirección; pueden reiniciarse servicios.', recovery: 'Recuperar estado anterior',
    operation: 'Última operación administrativa', status: 'Consultar ejecución', executing: 'Operación aceptada, aún no confirmada. Si pierde la sesión, abra la nueva dirección y consulte el estado.',
    ready: 'El ejecutor confirmó el cambio: verificó salud y volvió a leer la configuración registrada.', recoveryRequired: 'No se confirmó la aplicación; se requiere recuperación protegida del host.',
    newWeb: 'Nueva dirección Web prevista', pending: 'Validación aceptada. Ningún servicio cambió.', source: 'Estado de configuración del host (sin verificación independiente de publicación externa o reproducción de video).',
    invalid: 'Los puertos deben ser enteros distintos entre 1 y 65535.', retry: 'No se pudo consultar ni ejecutar. Verifique SystemAdmin y el ejecutor.', update: 'Actualizar estado'
  }
} as const;

async function request<T>(route: string, body?: object): Promise<T> {
  const response = await fetch(BASE + '/' + route, {
    method: body ? 'POST' : 'GET',
    headers: { accept: 'application/json', ...(body ? { 'content-type': 'application/json' } : {}) },
    credentials: 'same-origin',
    body: body ? JSON.stringify(body) : undefined
  });
  const payload: unknown = await response.json();
  if (!response.ok) {
    const reported = payload && typeof payload === 'object' && 'error' in payload && typeof payload.error === 'string' ? payload.error : 'HTTP ' + response.status;
    throw new Error(reported);
  }
  return payload as T;
}

export function InstallationNetworkHostPanel({ locale }: { locale: EngineeringLocale }) {
  const t = translation[locale];
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [module, setModule] = useState<Module>('access');
  const [values, setValues] = useState<Record<string, string>>({});
  const [plan, setPlan] = useState<Plan | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [accepted, setAccepted] = useState(false);

  async function refresh(reset = false) {
    try {
      const status = await request<Snapshot>('status');
      if (!status.available || !status.configuredPorts || !Array.isArray(status.modules)) throw new Error(t.unsupported);
      setSnapshot(status);
      if (reset) {
        setModule(status.modules.includes(module) ? module : status.modules[0] ?? 'access');
        setValues(Object.fromEntries(Object.entries(status.configuredPorts).map(([key, port]) => [key, String(port)])));
        setPlan(null); setConfirmed(false);
      }
      setError('');
    } catch (cause) {
      if (reset) setSnapshot(null);
      setError(cause instanceof Error ? cause.message : t.retry);
    }
  }
  useEffect(() => { void refresh(true); }, []);
  useEffect(() => {
    if (!accepted || snapshot?.operation?.state !== 'applying') return;
    const timer = window.setInterval(() => { void refresh(); }, 2500);
    return () => window.clearInterval(timer);
  }, [accepted, snapshot?.operation?.state]);

  const available = snapshot?.modules ?? [];
  const selected = available.includes(module) ? module : (available[0] ?? 'access');
  const requested = fields[selected].map(([key]) => values[key]);
  const valid = requested.every(value => /^\d+$/.test(value ?? '') && Number(value) >= 1 && Number(value) <= 65535)
    && new Set(requested.map(Number)).size === requested.length;
  const newWeb = selected === 'access' && valid ? window.location.protocol + '//' + window.location.hostname + ':' + values.webPort + '/' : null;
  const isRunning = snapshot?.operation?.state === 'applying';
  const requiresRecovery = snapshot?.operation?.state === 'recovery_required';

  async function preflight() {
    if (!valid) return;
    setBusy(true); setError(''); setPlan(null); setAccepted(false);
    try {
      const ports = Object.fromEntries(fields[selected].map(([key]) => [key, Number(values[key])]));
      setPlan(await request<Plan>('preflight', { schema: SCHEMA, module: selected, ports }));
      setConfirmed(false);
    } catch (cause) { setError(cause instanceof Error ? cause.message : t.retry); }
    finally { setBusy(false); }
  }
  async function apply() {
    if (!plan || !confirmed) return;
    setBusy(true); setError('');
    try {
      await request('apply', { schema: SCHEMA, planId: plan.planId, confirm: true });
      setPlan(null); setConfirmed(false); setAccepted(true);
      await refresh();
    } catch (cause) { setError(cause instanceof Error ? cause.message : t.retry); }
    finally { setBusy(false); }
  }
  async function recover() {
    if (!window.confirm(t.recovery)) return;
    setBusy(true); setError('');
    try {
      await request('recover', { schema: SCHEMA, confirm: true });
      setAccepted(true);
      await refresh();
    } catch (cause) { setError(cause instanceof Error ? cause.message : t.retry); }
    finally { setBusy(false); }
  }

  return <section className="installation-network__form" data-testid="installation-host-executor">
    <h3>{t.title}</h3><p>{t.description}</p>
    <button className="secondary" type="button" onClick={() => { void refresh(true); }} disabled={busy}>{t.update}</button>
    {!snapshot && <p role="status">{t.unavailable}</p>}
    {snapshot && <>
      <p>{snapshot.instanceId} · {snapshot.profile}</p><small>{t.source}</small>
      <label>{t.fieldTitle}<select value={selected} disabled={busy || isRunning || requiresRecovery} onChange={event => { setModule(event.target.value as Module); setPlan(null); setConfirmed(false); }}>
        {available.map(value => <option key={value} value={value}>{value === 'access' ? 'Web / API' : value === 'database' ? 'PostgreSQL' : 'Mídia / HLS'}</option>)}
      </select></label>
      {fields[selected].map(([key, label]) => <label key={key}>{label}<input type="number" min="1" max="65535" step="1" value={values[key] ?? ''} disabled={busy || isRunning || requiresRecovery}
        onChange={event => { setValues(current => ({ ...current, [key]: event.target.value })); setPlan(null); setConfirmed(false); }}/></label>)}
      {!valid && <p role="alert">{t.invalid}</p>}
      {newWeb && <p>{t.newWeb}: <code>{newWeb}</code></p>}
      <button type="button" disabled={!valid || busy || isRunning || requiresRecovery} onClick={() => void preflight()}>{t.preflight}</button>
      {plan && <div role="status"><p>{t.pending}</p><p><code>{JSON.stringify(plan.before)} → {JSON.stringify(plan.ports)}</code></p>
        <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)}/>{t.confirm}</label>
        <button type="button" disabled={!confirmed || busy} onClick={() => void apply()}>{t.applying}</button>
      </div>}
      {snapshot.operation && <div role="status">
        <h4>{t.operation}</h4><p><code>{snapshot.operation.id}</code> · {snapshot.operation.state}</p>
        {snapshot.operation.state === 'applied' && <p>{t.ready}</p>}
        {isRunning && <p>{t.executing}</p>}
        {requiresRecovery && <><p role="alert">{t.recoveryRequired}</p><button type="button" disabled={busy} onClick={() => void recover()}>{t.recovery}</button></>}
        <button type="button" className="secondary" disabled={busy} onClick={() => void refresh()}>{t.status}</button>
      </div>}
    </>}
    {error && <p role="alert">{error}</p>}
  </section>;
}
