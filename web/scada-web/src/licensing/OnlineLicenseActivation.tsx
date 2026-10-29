import React, { useEffect, useState } from 'react';

type Locale = 'pt-BR' | 'en' | 'es';
type Prepared = {
  activationId: string; expiresAtUtc: string; onlineActivationAvailable: boolean; confirmationRequested: boolean;
  summary: { applicationName: string; licenseCode: string; term: string; entitlementExpiresAtUtc: string | null;
    tier: string; interactiveSeats: number; viewOnlySeats: number; haRuntime: boolean;
    assetProcessIntelligence: boolean; maxActivations: number };
};
const labels = {
  'pt-BR': { title: 'Ativar licença online', help: 'Entre com sua conta SCADIVO e informe o código da licença. Você poderá conferir os dados antes de ativar.', user: 'Login ou e-mail', password: 'Senha', code: 'Código da licença', prepare: 'Consultar licença', review: 'Confira a licença', app: 'Aplicação', term: 'Modalidade', expiry: 'Validade comercial', tier: 'Faixa de TAGs', interactive: 'Clientes interativos', view: 'Clientes de visualização', ha: 'Redundância HA', intelligence: 'Intelligence', machines: 'Ativações permitidas', yes: 'Incluído', no: 'Não incluído', perpetual: 'Perpétua online', monthly: 'Mensal', annual: 'Anual', unlimited: 'Sem vencimento', expires: 'Confirmação disponível até', unavailable: 'A ativação online está temporariamente indisponível.', confirm: 'Confirmar ativação', retry: 'Concluir ativação', cancel: 'Cancelar', success: 'Licença online ativada.', cancelled: 'Consulta cancelada.', cancelledAfterConfirmation: 'Ativação já concluída; a licença foi recuperada e aplicada.', generic: 'Não foi possível concluir. Confira os dados ou tente novamente.', credentials: 'Login ou senha não foram aceitos.', network: 'Não foi possível conectar ao serviço de licenciamento.', mismatch: 'Os dados retornados não correspondem à licença assinada.', pending: 'Uma confirmação já foi enviada. Você ainda pode cancelar. Se o Portal já tiver concluído a ativação, este botão recupera e aplica o resultado com segurança.' },
  en: { title: 'Activate online license', help: 'Enter your SCADIVO account and license code. You can review the details before activating.', user: 'Username or email', password: 'Password', code: 'License code', prepare: 'Find license', review: 'Review the license', app: 'Application', term: 'Plan', expiry: 'Commercial expiry', tier: 'TAG tier', interactive: 'Interactive clients', view: 'View-only clients', ha: 'HA redundancy', intelligence: 'Intelligence', machines: 'Allowed activations', yes: 'Included', no: 'Not included', perpetual: 'Online perpetual', monthly: 'Monthly', annual: 'Annual', unlimited: 'No expiry', expires: 'Confirmation available until', unavailable: 'Online activation is temporarily unavailable.', confirm: 'Confirm activation', retry: 'Complete activation', cancel: 'Cancel', success: 'Online license activated.', cancelled: 'Request cancelled.', cancelledAfterConfirmation: 'Activation was already completed; the license was recovered and applied.', generic: 'Could not complete the request. Check the details or try again.', credentials: 'Username or password was not accepted.', network: 'Could not connect to the licensing service.', mismatch: 'The returned details do not match the signed license.', pending: 'Confirmation was already sent. You can still cancel. If the Portal already completed activation, this button safely recovers and applies the result.' },
  es: { title: 'Activar licencia online', help: 'Ingrese su cuenta SCADIVO y el código de licencia. Podrá revisar los datos antes de activar.', user: 'Usuario o correo', password: 'Contraseña', code: 'Código de licencia', prepare: 'Consultar licencia', review: 'Revise la licencia', app: 'Aplicación', term: 'Modalidad', expiry: 'Vencimiento comercial', tier: 'Nivel de TAGs', interactive: 'Clientes interactivos', view: 'Clientes de visualización', ha: 'Redundancia HA', intelligence: 'Intelligence', machines: 'Activaciones permitidas', yes: 'Incluido', no: 'No incluido', perpetual: 'Perpetua online', monthly: 'Mensual', annual: 'Anual', unlimited: 'Sin vencimiento', expires: 'Confirmación disponible hasta', unavailable: 'La activación online no está disponible temporalmente.', confirm: 'Confirmar activación', retry: 'Completar activación', cancel: 'Cancelar', success: 'Licencia online activada.', cancelled: 'Solicitud cancelada.', cancelledAfterConfirmation: 'La activación ya se completó; la licencia se recuperó y aplicó.', generic: 'No se pudo completar. Revise los datos o inténtelo de nuevo.', credentials: 'El usuario o la contraseña no fueron aceptados.', network: 'No se pudo conectar al servicio de licencias.', mismatch: 'Los datos recibidos no coinciden con la licencia firmada.', pending: 'Ya se envió una confirmación. Todavía puede cancelar. Si el Portal ya completó la activación, este botón recupera y aplica el resultado de forma segura.' }
} as const;

const abandonLabels = {
  'pt-BR': { action: 'Descartar tentativa local', confirm: 'O resultado da ativação não pôde ser recuperado. Confira no Portal se há uma ativação nesta máquina e revogue-a lá se necessário. Descartar aqui não libera uma vaga no Portal. Deseja continuar?', done: 'Tentativa local descartada. Confira a ativação no Portal.' },
  en: { action: 'Discard local attempt', confirm: 'The activation result could not be recovered. Check the Portal for an activation on this machine and revoke it there if necessary. Discarding here does not free a Portal slot. Continue?', done: 'Local attempt discarded. Check the activation in the Portal.' },
  es: { action: 'Descartar intento local', confirm: 'No se pudo recuperar el resultado de la activación. Compruebe en el Portal si existe una activación para esta máquina y revóquela allí si es necesario. Descartar aquí no libera una plaza en el Portal. ¿Continuar?', done: 'Intento local descartado. Revise la activación en el Portal.' }
} as const;

const failureLabels = {
  'pt-BR': {
    ONLINE_LICENSE_PREPARATION_EXPIRED: 'O prazo desta consulta terminou. Cancele a tentativa para verificar o resultado no Portal e consultar a licença novamente. Se já houve confirmação, o resultado precisa ser recuperado antes de reiniciar.',
    ONLINE_LICENSE_PREPARATION_INVALIDATED: 'Esta consulta foi cancelada ou os dados da licença mudaram. Resolva a tentativa com Cancelar antes de consultar novamente.',
    ONLINE_LICENSE_ISSUANCE_UNAVAILABLE: 'O Portal está com a emissão de licenças indisponível. A tentativa foi preservada; tente recuperar o resultado quando o serviço voltar.',
    ONLINE_LICENSE_ACTIVATION_ALREADY_ACTIVE: 'Esta ativação já está ativa no Portal. É necessário recuperar ou reautenticar o vínculo existente.',
    ONLINE_LICENSE_ACTIVATION_LIMIT_REACHED: 'Todas as ativações desta licença estão em uso no Portal. Remover a licença apenas neste computador não libera uma ativação no Portal.',
    ONLINE_LICENSE_ACTIVATION_REVOKED: 'Esta ativação foi revogada no Portal.',
    ONLINE_LICENSE_NOT_AVAILABLE: 'A licença não está disponível para esta conta ou sua validade comercial terminou.'
  },
  en: {
    ONLINE_LICENSE_PREPARATION_EXPIRED: 'This license review expired. Cancel the attempt to resolve its Portal result before reviewing the license again. A previously confirmed activation must be recovered first.',
    ONLINE_LICENSE_PREPARATION_INVALIDATED: 'This review was cancelled or its license details changed. Resolve the attempt with Cancel before trying again.',
    ONLINE_LICENSE_ISSUANCE_UNAVAILABLE: 'The Portal cannot issue licenses right now. The attempt was preserved; recover its result when the service returns.',
    ONLINE_LICENSE_ACTIVATION_ALREADY_ACTIVE: 'This activation is already active in the Portal. Recover or reauthenticate the existing link.',
    ONLINE_LICENSE_ACTIVATION_LIMIT_REACHED: 'All activations for this license are in use. Removing the license only on this computer does not release a Portal activation.',
    ONLINE_LICENSE_ACTIVATION_REVOKED: 'This activation was revoked in the Portal.',
    ONLINE_LICENSE_NOT_AVAILABLE: 'This license is unavailable for this account or its commercial term ended.'
  },
  es: {
    ONLINE_LICENSE_PREPARATION_EXPIRED: 'Esta consulta venció. Cancele el intento para resolver su resultado en el Portal antes de consultar nuevamente. Una activación ya confirmada debe recuperarse primero.',
    ONLINE_LICENSE_PREPARATION_INVALIDATED: 'La consulta fue cancelada o cambiaron sus datos. Resuelva el intento con Cancelar antes de consultar nuevamente.',
    ONLINE_LICENSE_ISSUANCE_UNAVAILABLE: 'El Portal no puede emitir licencias en este momento. El intento se conservó; recupere el resultado cuando vuelva el servicio.',
    ONLINE_LICENSE_ACTIVATION_ALREADY_ACTIVE: 'Esta activación ya está activa en el Portal. Recupere o vuelva a autenticar el vínculo existente.',
    ONLINE_LICENSE_ACTIVATION_LIMIT_REACHED: 'Todas las activaciones están en uso. Quitar la licencia solo de este equipo no libera una activación del Portal.',
    ONLINE_LICENSE_ACTIVATION_REVOKED: 'Esta activación fue revocada en el Portal.',
    ONLINE_LICENSE_NOT_AVAILABLE: 'La licencia no está disponible para esta cuenta o terminó su vigencia comercial.'
  }
};

export function OnlineLicenseActivation({ api, locale, pending, completionPending, onChanged }: {
  api: string; locale: Locale; pending: boolean; completionPending: boolean; onChanged: () => Promise<void>;
}) {
  const t = labels[locale];
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [prepared, setPrepared] = useState<Prepared | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const explain = (value: string) => (failureLabels[locale] as Record<string, string>)[value] ?? (value === 'ONLINE_LICENSE_INVALID_CREDENTIALS' ? t.credentials
    : value === 'ONLINE_LICENSE_PORTAL_UNAVAILABLE' ? t.network
      : value === 'ONLINE_LICENSE_PRESENTATION_MISMATCH' ? t.mismatch : /^ONLINE_LICENSE_[A-Z_]+$/.test(value) ? `${t.generic} (${value})` : t.generic);
  const read = async <T,>(response: Response): Promise<T> => {
    const body = await response.json().catch(() => null) as { error?: string } | null;
    if (!response.ok) throw new Error(body?.error ?? 'error');
    return body as T;
  };
  useEffect(() => {
    if (!pending) return;
    const controller = new AbortController();
    void fetch(`${api}/api/licensing/online/preparation`, { signal: controller.signal, cache: 'no-store' })
      .then(read<Prepared>).then(setPrepared).catch(() => { if (!controller.signal.aborted) setError(t.generic); });
    return () => controller.abort();
  }, [api, pending, t.generic]);

  const prepare = async (event: React.FormEvent) => {
    event.preventDefault(); setBusy(true); setError(''); setMessage('');
    try {
      const response = await fetch(`${api}/api/licensing/online/prepare`, { method: 'POST', cache: 'no-store',
        headers: { 'content-type': 'application/json' }, body: JSON.stringify({ username, password, licenseCode: code }) });
      setPrepared(await read<Prepared>(response));
      await onChanged();
    } catch (reason) { setError(explain(reason instanceof Error ? reason.message : 'error')); }
    finally { setPassword(''); setBusy(false); }
  };
  const execute = async (action: 'confirm' | 'cancel' | 'abandon') => {
    if (!prepared) return;
    if (action === 'abandon' && !window.confirm(abandonLabels[locale].confirm)) return;
    setBusy(true); setError(''); setMessage('');
    try {
      const response = await fetch(`${api}/api/licensing/online/${action}`, { method: 'POST', cache: 'no-store',
        headers: { 'content-type': 'application/json' }, body: JSON.stringify({ activationId: prepared.activationId }) });
      let alreadyActivated = false;
      if (!response.ok) await read(response);
      else if (action === 'cancel' && response.status !== 204) {
        const result = await response.json().catch(() => null) as { alreadyActivated?: boolean } | null;
        alreadyActivated = result?.alreadyActivated === true;
      }
      setPrepared(null); setCode(''); setMessage(action === 'confirm' ? t.success : action === 'abandon' ? abandonLabels[locale].done
        : alreadyActivated ? t.cancelledAfterConfirmation : t.cancelled);
      await onChanged();
    } catch (reason) {
      setError(explain(reason instanceof Error ? reason.message : 'error'));
      if (action === 'confirm') {
        const refreshed = await fetch(`${api}/api/licensing/online/preparation`, { cache: 'no-store' }).then(read<Prepared>).catch(() => null);
        if (refreshed) setPrepared(refreshed);
      }
    } finally { setBusy(false); }
  };
  const date = (value: string | null) => value ? new Date(value).toLocaleString(locale) : t.unlimited;
  const yesNo = (value: boolean) => value ? t.yes : t.no;
  const recover = async () => {
    setBusy(true); setError('');
    try {
      const response = await fetch(`${api}/api/licensing/online/recover`, { method: 'POST', cache: 'no-store' });
      if (!response.ok) await read(response);
      setMessage(t.success); await onChanged();
    } catch (reason) { setError(explain(reason instanceof Error ? reason.message : 'error')); }
    finally { setBusy(false); }
  };
  return <div className="licensing-online-activation">
    <h3>{t.title}</h3>
    {error && <p className="licensing-message error" role="alert">{error}</p>}
    {message && <p className="licensing-message success" role="status">{message}</p>}
    {completionPending ? <div><p>{t.pending}</p><div className="licensing-actions"><button type="button" disabled={busy} onClick={() => void recover()}>{t.retry}</button></div></div>
    : !prepared ? <form onSubmit={prepare}>
      <p className="licensing-help">{t.help}</p>
      <label htmlFor="online-user">{t.user}</label><input id="online-user" autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} required />
      <label htmlFor="online-password">{t.password}</label><input id="online-password" type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required />
      <label htmlFor="online-code">{t.code}</label><input id="online-code" value={code} onChange={e => setCode(e.target.value)} required />
      <div className="licensing-actions"><button disabled={busy} type="submit">{t.prepare}</button></div>
    </form> : <div>
      <h4>{t.review}</h4><dl>
        <dt>{t.app}</dt><dd>{prepared.summary.applicationName}</dd><dt>{t.code}</dt><dd>{prepared.summary.licenseCode}</dd>
        <dt>{t.term}</dt><dd>{prepared.summary.term === 'perpetual' ? t.perpetual : prepared.summary.term === 'monthly' ? t.monthly : t.annual}</dd>
        <dt>{t.expiry}</dt><dd>{date(prepared.summary.entitlementExpiresAtUtc)}</dd><dt>{t.tier}</dt><dd>{prepared.summary.tier}</dd>
        <dt>{t.interactive}</dt><dd>{prepared.summary.interactiveSeats}</dd><dt>{t.view}</dt><dd>{prepared.summary.viewOnlySeats}</dd>
        <dt>{t.ha}</dt><dd>{yesNo(prepared.summary.haRuntime)}</dd><dt>{t.intelligence}</dt><dd>{yesNo(prepared.summary.assetProcessIntelligence)}</dd>
        <dt>{t.machines}</dt><dd>{prepared.summary.maxActivations}</dd><dt>{t.expires}</dt><dd>{date(prepared.expiresAtUtc)}</dd>
      </dl>
      {!prepared.onlineActivationAvailable && <p>{t.unavailable}</p>}
      {prepared.confirmationRequested && <p>{t.pending}</p>}
      <div className="licensing-actions"><button type="button" disabled={busy || !prepared.onlineActivationAvailable} onClick={() => void execute('confirm')}>{prepared.confirmationRequested ? t.retry : t.confirm}</button>
        <button type="button" disabled={busy} onClick={() => void execute('cancel')}>{t.cancel}</button>
        {prepared.confirmationRequested && Date.now() >= Date.parse(prepared.expiresAtUtc) + 24 * 60 * 60 * 1000 &&
          <button type="button" className="licensing-danger" disabled={busy} onClick={() => void execute('abandon')}>{abandonLabels[locale].action}</button>}</div>
    </div>}
  </div>;
}
