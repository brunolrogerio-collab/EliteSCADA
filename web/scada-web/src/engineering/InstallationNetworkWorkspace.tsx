import React, { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import './installation-network.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type EndpointProfile = { transport: string; bindAddress: string; publishedPort: number; internalPort: number; publishedToHost: boolean };
type NetworkProfile = {
  profile: string;
  instanceId: string;
  web: EndpointProfile;
  api: EndpointProfile;
  database: EndpointProfile;
  mediaControl: EndpointProfile;
  mediaPlayback: EndpointProfile;
  applyThroughUi: boolean;
  hostMappingIndependentlyVerified: boolean;
};
type Draft = { instanceId: string; webBind: '127.0.0.1' | '0.0.0.0'; apiBind: '127.0.0.1' | '0.0.0.0'; webPort: string; apiPort: string; databasePort: string; mediaControlPort: string; mediaPlaybackPort: string };

const copy = {
  'pt-BR': {
    title: 'Rede e serviços', intro: 'Consulte os serviços desta instalação. O procedimento para mudar portas depende do perfil de instalação.',
    admin: 'Esta área exige a capacidade SystemAdmin da Security Authority.', loading: 'Lendo a configuração informada por esta instalação…', unavailable: 'Esta instalação não informa o perfil Docker estável suportado. A tela não altera instalações nativas ou serviços externos.',
    error: 'Não foi possível ler a configuração desta instalação.', retry: 'Atualizar', current: 'Configuração informada pelo container atual', currentNote: 'Estes valores foram enviados ao container pelo Compose. O SCADIVO não consegue verificar, de dentro do container, se o host publicou as portas corretamente.',
    defaultsNote: 'A instalação usa as portas padrão automaticamente. Os campos abaixo mostram os valores atuais; altere somente se precisar. Isso inclui a porta de acesso à interface Web.', desired: 'Preparar nova configuração', identity: 'ID desta instância', identityHelp: 'Use letras minúsculas, números e hífens. Para uma segunda instalação, use outra pasta, gere um .env com segredos próprios e defina nele o ID escolhido antes de executar o comando. Nunca copie chaves ou credenciais.',
    bind: 'Endereço de acesso', loopback: 'Somente neste computador', allInterfaces: 'Interfaces de rede', webPort: 'Porta Web no host', apiPort: 'Porta direta da API no host', dbPort: 'Porta do banco no host', mediaControlPort: 'Porta interna do controle de mídia', mediaPlaybackPort: 'Porta interna do HLS', internal: 'Porta dentro do container', transport: 'Protocolo', exposed: 'Publicada no host', private: 'Somente rede Docker', fixed: 'Fixa neste pacote', databaseBind: 'Banco limitado a 127.0.0.1 para reduzir exposição.',
    preview: 'Endereços após aplicar', webUrl: 'Interface Web', apiUrl: 'API direta', restart: 'Impacto: o Compose recria serviços afetados; a navegação pode cair durante a troca. O banco e os volumes permanecem.',
    limitsTitle: 'O que este perfil controla', limits: 'Este perfil prepara as portas publicadas no host para Web, API e PostgreSQL e permite alterar as portas internas do serviço de mídia. Não muda firewall, banco remoto nem endereços dos peers de High Availability. Portas de conexão dos Drivers continuam configuradas em cada Data Source.',
    mediaLabel: 'Mídia:', mediaDetails: 'as portas de controle e HLS podem ser ajustadas abaixo, mas continuam privadas dentro da rede Docker. A API acompanha esses valores e entrega o vídeo pelo relay com tíquetes temporários. Não publique o MediaMTX diretamente no host.',
    driversLabel: 'Equipamentos e protocolos:', driversDetails: 'configure conexões em cada Data Source. A porta de um Driver cliente pertence ao equipamento remoto. Este perfil não publica listeners de Drivers nem configura broadcast BACnet.',
    hostTitle: 'Aplicação no host', hostText: 'Baixe o script da plataforma, coloque-o na pasta que contém docker-compose.stable.yml e .env, revise-o e execute o comando abaixo nessa pasta em uma conta autorizada a administrar o Docker. O script valida portas e Compose antes de trocar, guarda uma cópia de .env, verifica saúde e tenta restaurar a configuração se falhar. Não altera firewall nem apaga volumes.',
    powershell: 'Baixar script PowerShell', bash: 'Baixar script Linux', command: 'Comando para aplicar', copyCommand: 'Copiar comando', copied: 'Comando copiado.', noClipboard: 'O navegador não permitiu copiar; selecione o comando e copie manualmente.',
    invalidId: 'Use um ID de 1 a 48 caracteres: minúsculas, números ou hífen; comece com letra ou número.', invalidPort: 'Cada porta deve ser um número inteiro entre 1 e 65535.', duplicatePorts: 'Web, API e banco precisam usar portas diferentes.', duplicateMediaPorts: 'Controle de mídia e HLS precisam usar portas internas diferentes.', wildcardWarn: 'Com 0.0.0.0, o serviço escuta em todas as interfaces. O SCADIVO não abre firewall nem configura TLS; use proxy TLS e política de firewall antes de permitir acesso externo.', portPrivilege: 'Portas abaixo de 1024 podem exigir configuração privilegiada no host Docker.', newInstance: 'Para criar outra instalação, o comando só funciona em uma pasta nova, com .env independente e o ID escolhido já definido nela. O script recusa reaproveitar ID ou volumes existentes.',
    tableService: 'Serviço', appPort: 'Porta interna', hostPort: 'Porta do host', status: 'Exposição', clickHelp: 'Abrir instruções completas na Ajuda'
  },
  en: {
    title: 'Network and services', intro: 'Inspect this installation’s services. The procedure for changing ports depends on the installation profile.',
    admin: 'This area requires the Security Authority SystemAdmin capability.', loading: 'Reading the configuration reported by this installation…', unavailable: 'This installation does not report the supported stable Docker profile. This page does not change native installations or external services.',
    error: 'Could not read this installation configuration.', retry: 'Refresh', current: 'Configuration reported by the current container', currentNote: 'These values were passed into the container by Compose. SCADIVO cannot verify from inside the container whether the host published the ports correctly.',
    defaultsNote: 'Installation uses default ports automatically. The fields below show current values; change them only when needed, including the Web interface access port.', desired: 'Prepare a new configuration', identity: 'Instance ID', identityHelp: 'Use lowercase letters, numbers, and hyphens. For a second installation, use another folder, create an .env with its own secrets, and set the chosen ID in it before running the command. Never copy keys or credentials.',
    bind: 'Access address', loopback: 'This computer only', allInterfaces: 'Network interfaces', webPort: 'Web host port', apiPort: 'Direct API host port', dbPort: 'Database host port', mediaControlPort: 'Media control internal port', mediaPlaybackPort: 'HLS internal port', internal: 'Inside the container', transport: 'Protocol', exposed: 'Published on host', private: 'Docker network only', fixed: 'Fixed in this package', databaseBind: 'Database remains bound to 127.0.0.1 to reduce exposure.',
    preview: 'Addresses after applying', webUrl: 'Web interface', apiUrl: 'Direct API', restart: 'Impact: Compose recreates affected services; the page may disconnect during the change. The database and volumes remain.',
    limitsTitle: 'What this profile controls', limits: 'This profile prepares host-published ports for Web, API, and PostgreSQL and lets you change the media service internal ports. It does not change firewalls, remote databases, or High Availability peer addresses. Driver connection ports remain configured in each Data Source.',
    mediaLabel: 'Media:', mediaDetails: 'control and HLS ports can be changed below but stay private on the Docker network. The API follows these values and serves video through its relay with short-lived tickets. Do not publish MediaMTX directly on the host.',
    driversLabel: 'Equipment and protocols:', driversDetails: 'configure connections in each Data Source. A client Driver port belongs to the remote device. This profile does not publish Driver listeners or configure BACnet broadcast.',
    hostTitle: 'Apply on the host', hostText: 'Download the platform script, place it in the folder containing docker-compose.stable.yml and .env, review it, then run the command below from that folder with an account allowed to administer Docker. It validates ports and Compose before changing anything, backs up .env, checks health, and attempts to restore the prior configuration on failure. It does not change firewall rules or delete volumes.',
    powershell: 'Download PowerShell script', bash: 'Download Linux script', command: 'Apply command', copyCommand: 'Copy command', copied: 'Command copied.', noClipboard: 'The browser did not allow copying; select and copy the command manually.',
    invalidId: 'Use an ID of 1–48 characters: lowercase letters, numbers, or hyphens; begin with a letter or number.', invalidPort: 'Each port must be an integer from 1 through 65535.', duplicatePorts: 'Web, API, and database must use different ports.', duplicateMediaPorts: 'Media control and HLS must use different internal ports.', wildcardWarn: 'With 0.0.0.0, the service listens on all interfaces. SCADIVO does not open firewalls or configure TLS; use a TLS proxy and firewall policy before allowing external access.', portPrivilege: 'Ports below 1024 may require privileged host configuration for Docker.', newInstance: 'To create another installation, run the command only in a new folder with its own .env and the chosen ID already set there. The script refuses existing IDs or volumes.',
    tableService: 'Service', appPort: 'Internal port', hostPort: 'Host port', status: 'Exposure', clickHelp: 'Open full instructions in Help'
  },
  es: {
    title: 'Red y servicios', intro: 'Consulte los servicios de esta instalación. El procedimiento para cambiar puertos depende del perfil de instalación.',
    admin: 'Esta área requiere la capability SystemAdmin de Security Authority.', loading: 'Leyendo la configuración informada por esta instalación…', unavailable: 'Esta instalación no informa el perfil Docker estable compatible. Esta página no cambia instalaciones nativas ni servicios externos.',
    error: 'No fue posible leer la configuración de esta instalación.', retry: 'Actualizar', current: 'Configuración informada por el contenedor actual', currentNote: 'Compose pasó estos valores al contenedor. Desde dentro del contenedor, SCADIVO no puede verificar si el host publicó las puertos correctamente.',
    defaultsNote: 'La instalación usa los puertos predeterminados automáticamente. Los campos muestran los valores actuales; cámbielos solo si lo necesita, incluido el puerto de acceso a la interfaz Web.', desired: 'Preparar una nueva configuración', identity: 'ID de instancia', identityHelp: 'Use minúsculas, números y guiones. Para una segunda instalación, use otra carpeta, cree un .env con secretos propios y defina allí el ID elegido antes de ejecutar el comando. Nunca copie claves ni credenciales.',
    bind: 'Dirección de acceso', loopback: 'Solo este equipo', allInterfaces: 'Interfaces de red', webPort: 'Puerto Web en el host', apiPort: 'Puerto directo de API en el host', dbPort: 'Puerto de base de datos en el host', mediaControlPort: 'Puerto interno de control multimedia', mediaPlaybackPort: 'Puerto interno HLS', internal: 'Dentro del contenedor', transport: 'Protocolo', exposed: 'Publicada en el host', private: 'Solo red Docker', fixed: 'Fija en este paquete', databaseBind: 'La base de datos permanece vinculada a 127.0.0.1 para reducir la exposición.',
    preview: 'Direcciones después de aplicar', webUrl: 'Interfaz Web', apiUrl: 'API directa', restart: 'Impacto: Compose recrea los servicios afectados; la página puede desconectarse durante el cambio. La base de datos y los volúmenes permanecen.',
    limitsTitle: 'Qué controla este perfil', limits: 'Este perfil prepara los puertos publicados en el host para Web, API y PostgreSQL y permite cambiar los puertos internos del servicio multimedia. No cambia firewalls, bases remotas ni direcciones de peers de High Availability. Los puertos de conexión de Drivers siguen configurándose en cada Data Source.',
    mediaLabel: 'Multimedia:', mediaDetails: 'los puertos de control y HLS se pueden cambiar abajo, pero permanecen privados en la red Docker. La API sigue estos valores y entrega el vídeo por su relay con tickets temporales. No publique MediaMTX directamente en el host.',
    driversLabel: 'Equipos y protocolos:', driversDetails: 'configure las conexiones en cada Data Source. El puerto de un Driver cliente pertenece al equipo remoto. Este perfil no publica listeners de Drivers ni configura broadcast BACnet.',
    hostTitle: 'Aplicación en el host', hostText: 'Descargue el script de la plataforma, colóquelo en la carpeta que contiene docker-compose.stable.yml y .env, revíselo y ejecute el comando siguiente desde esa carpeta con una cuenta autorizada para administrar Docker. Valida puertos y Compose antes del cambio, respalda .env, comprueba la salud e intenta restaurar la configuración si falla. No modifica el firewall ni elimina volúmenes.',
    powershell: 'Descargar script PowerShell', bash: 'Descargar script Linux', command: 'Comando de aplicación', copyCommand: 'Copiar comando', copied: 'Comando copiado.', noClipboard: 'El navegador no permitió copiar; seleccione y copie el comando manualmente.',
    invalidId: 'Use un ID de 1 a 48 caracteres: minúsculas, números o guiones; comience con letra o número.', invalidPort: 'Cada puerto debe ser un entero de 1 a 65535.', duplicatePorts: 'Web, API y base de datos necesitan puertos distintos.', duplicateMediaPorts: 'Control multimedia y HLS necesitan puertos internos distintos.', wildcardWarn: 'Con 0.0.0.0, el servicio escucha en todas las interfaces. SCADIVO no abre firewalls ni configura TLS; use un proxy TLS y una política de firewall antes del acceso externo.', portPrivilege: 'Los puertos inferiores a 1024 pueden requerir configuración privilegiada del host Docker.', newInstance: 'Para crear otra instalación, ejecute el comando solo en una carpeta nueva, con su .env propio y el ID elegido ya definido. El script rechaza IDs o volúmenes existentes.',
    tableService: 'Servicio', appPort: 'Puerto interno', hostPort: 'Puerto del host', status: 'Exposición', clickHelp: 'Abrir instrucciones completas en Ayuda'
  }
} as const;

export function InstallationNetworkWorkspace({ locale, isSystemAdmin }: { locale: EngineeringLocale; isSystemAdmin: boolean }) {
  const t = copy[locale];
  const [current, setCurrent] = useState<NetworkProfile | null>(null);
  const [loading, setLoading] = useState(false);
  const [failure, setFailure] = useState<'unsupported' | 'failed' | null>(null);
  const [copied, setCopied] = useState(false);
  const [copyFailure, setCopyFailure] = useState(false);
  const [commandPlatform, setCommandPlatform] = useState<'powershell' | 'bash'>('powershell');
  const [draft, setDraft] = useState<Draft>({ instanceId: '', webBind: '127.0.0.1', apiBind: '127.0.0.1', webPort: '18080', apiPort: '15120', databasePort: '15432', mediaControlPort: '9997', mediaPlaybackPort: '8888' });

  async function loadProfile() {
    setLoading(true);
    setFailure(null);
    try {
      const response = await fetch(`${API}/api/installation/network-profile`, { credentials: 'same-origin', headers: { accept: 'application/json' } });
      if (response.status === 404) { setFailure('unsupported'); setCurrent(null); return; }
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const profile = await response.json() as NetworkProfile;
      setCurrent(profile);
      setDraft({
        instanceId: profile.instanceId,
        webBind: profile.web.bindAddress === '0.0.0.0' ? '0.0.0.0' : '127.0.0.1',
        apiBind: profile.api.bindAddress === '0.0.0.0' ? '0.0.0.0' : '127.0.0.1',
        webPort: String(profile.web.publishedPort),
        apiPort: String(profile.api.publishedPort),
        databasePort: String(profile.database.publishedPort),
        mediaControlPort: String(profile.mediaControl.internalPort),
        mediaPlaybackPort: String(profile.mediaPlayback.internalPort)
      });
    } catch {
      setFailure('failed');
      setCurrent(null);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { if (isSystemAdmin) void loadProfile(); }, [isSystemAdmin]);

  const validation = useMemo(() => {
    const errors: string[] = [];
    if (!/^[a-z0-9][a-z0-9-]{0,47}$/.test(draft.instanceId)) errors.push(t.invalidId);
    const ports = [draft.webPort, draft.apiPort, draft.databasePort, draft.mediaControlPort, draft.mediaPlaybackPort].map(Number);
    if (ports.some(port => !Number.isInteger(port) || port < 1 || port > 65535)) errors.push(t.invalidPort);
    const hostPorts = ports.slice(0, 3);
    const mediaPorts = ports.slice(3);
    if (hostPorts.every(port => Number.isInteger(port) && port >= 1 && port <= 65535) && new Set(hostPorts).size !== hostPorts.length) errors.push(t.duplicatePorts);
    if (mediaPorts.every(port => Number.isInteger(port) && port >= 1 && port <= 65535) && new Set(mediaPorts).size !== mediaPorts.length) errors.push(t.duplicateMediaPorts);
    return errors;
  }, [draft, t.invalidId, t.invalidPort, t.duplicatePorts, t.duplicateMediaPorts]);

  const command = useMemo(() => {
    if (!current || validation.length) return '';
    const newInstance = draft.instanceId !== current.instanceId;
    const newInstall = newInstance ? ` -NewInstallation -SourceInstanceId ${current.instanceId} -ConfirmIsolatedSecrets` : '';
    const newInstallBash = newInstance ? ` --new-installation --source-instance-id ${current.instanceId} --confirm-isolated-secrets` : '';
    return commandPlatform === 'powershell'
      ? `powershell -NoProfile -ExecutionPolicy Bypass -File .\\apply-network-profile.ps1 -InstallDirectory . -InstanceId ${draft.instanceId} -WebBind ${draft.webBind} -ApiBind ${draft.apiBind} -WebPort ${draft.webPort} -ApiPort ${draft.apiPort} -DatabasePort ${draft.databasePort} -MediaControlPort ${draft.mediaControlPort} -MediaPlaybackPort ${draft.mediaPlaybackPort}${newInstall}`
      : `bash ./apply-network-profile.sh --install-directory . --instance-id ${draft.instanceId} --web-bind ${draft.webBind} --api-bind ${draft.apiBind} --web-port ${draft.webPort} --api-port ${draft.apiPort} --database-port ${draft.databasePort} --media-control-port ${draft.mediaControlPort} --media-playback-port ${draft.mediaPlaybackPort}${newInstallBash}`;
  }, [commandPlatform, current, draft, validation.length]);

  async function copyCommand() {
    try {
      await navigator.clipboard.writeText(command);
      setCopied(true);
      setCopyFailure(false);
    } catch {
      setCopied(false);
      setCopyFailure(true);
    }
  }

  function downloadScript(extension: 'ps1' | 'sh') {
    const anchor = document.createElement('a');
    anchor.href = `/installation/apply-network-profile.${extension}`;
    anchor.download = `apply-network-profile.${extension}`;
    anchor.click();
  }

  if (!isSystemAdmin) return <section className="installation-network" aria-label={t.title}><h2>{t.title}</h2><p role="alert">{t.admin}</p></section>;
  if (loading) return <section className="installation-network" aria-label={t.title} aria-busy="true"><h2>{t.title}</h2><p>{t.loading}</p></section>;

  return <section className="installation-network" aria-label={t.title} data-testid="installation-network">
    <header className="installation-network__header"><div><span>{locale === 'pt-BR' ? 'INSTALAÇÃO' : locale === 'es' ? 'INSTALACIÓN' : 'INSTALLATION'}</span><h2>{t.title}</h2><p>{t.intro}</p></div><button type="button" className="secondary" onClick={() => void loadProfile()}>{t.retry}</button></header>
    <ServiceEndpointInventory locale={locale}/>
    <DatabaseReadinessCheck locale={locale}/>
    <NativeAccessPortPreparation locale={locale}/>
    {failure === 'unsupported' && <p className="installation-network__notice" role="status">{t.unavailable}</p>}
    {failure === 'failed' && <p className="installation-network__error" role="alert">{t.error} <button type="button" className="secondary" onClick={() => void loadProfile()}>{t.retry}</button></p>}
    {current && <>
      <div className="installation-network__current">
        <div><h3>{t.current}</h3><p>{t.currentNote}</p></div>
        <code>{current.instanceId}</code>
        <PortTable profile={current} t={t}/>
      </div>
      <div className="installation-network__form">
        <h3>{t.desired}</h3>
        <p>{t.defaultsNote}</p>
        <p className="installation-network__help">{t.identityHelp}</p>
        <label>{t.identity}<input value={draft.instanceId} onChange={event => setDraft(value => ({ ...value, instanceId: event.target.value }))} maxLength={48} autoComplete="off" spellCheck={false}/></label>
        {draft.instanceId !== current.instanceId && <p className="installation-network__warning" role="note">{t.newInstance}</p>}
        <div className="installation-network__grid">
          <label>{t.bind} — Web<select value={draft.webBind} onChange={event => setDraft(value => ({ ...value, webBind: event.target.value as Draft['webBind'] }))}><option value="127.0.0.1">{t.loopback} (127.0.0.1)</option><option value="0.0.0.0">{t.allInterfaces} (0.0.0.0)</option></select></label>
          <label>{t.webPort}<input type="number" min="1" max="65535" step="1" value={draft.webPort} onChange={event => setDraft(value => ({ ...value, webPort: event.target.value }))}/></label>
          <label>{t.bind} — API<select value={draft.apiBind} onChange={event => setDraft(value => ({ ...value, apiBind: event.target.value as Draft['apiBind'] }))}><option value="127.0.0.1">{t.loopback} (127.0.0.1)</option><option value="0.0.0.0">{t.allInterfaces} (0.0.0.0)</option></select></label>
          <label>{t.apiPort}<input type="number" min="1" max="65535" step="1" value={draft.apiPort} onChange={event => setDraft(value => ({ ...value, apiPort: event.target.value }))}/></label>
          <label>{t.dbPort}<input type="number" min="1" max="65535" step="1" value={draft.databasePort} onChange={event => setDraft(value => ({ ...value, databasePort: event.target.value }))}/><small>{t.databaseBind}</small></label>
          <label>{t.mediaControlPort}<input type="number" min="1" max="65535" step="1" value={draft.mediaControlPort} onChange={event => setDraft(value => ({ ...value, mediaControlPort: event.target.value }))}/></label>
          <label>{t.mediaPlaybackPort}<input type="number" min="1" max="65535" step="1" value={draft.mediaPlaybackPort} onChange={event => setDraft(value => ({ ...value, mediaPlaybackPort: event.target.value }))}/></label>
        </div>
        {(draft.webBind === '0.0.0.0' || draft.apiBind === '0.0.0.0') && <p className="installation-network__warning" role="note">{t.wildcardWarn}</p>}
        {[draft.webPort, draft.apiPort, draft.databasePort].some(port => Number(port) > 0 && Number(port) < 1024) && <p className="installation-network__warning" role="note">{t.portPrivilege}</p>}
        {validation.map(error => <p className="installation-network__error" role="alert" key={error}>{error}</p>)}
      </div>
      <div className="installation-network__preview">
        <h3>{t.preview}</h3>
        <dl><dt>{t.webUrl}</dt><dd>http://localhost:{draft.webPort}</dd><dt>{t.apiUrl}</dt><dd>http://localhost:{draft.apiPort}</dd></dl>
        <p>{t.restart}</p>
      </div>
      <div className="installation-network__limits"><h3>{t.limitsTitle}</h3><p>{t.limits}</p><p><strong>{t.mediaLabel}</strong> {t.mediaDetails}</p><p><strong>{t.driversLabel}</strong> {t.driversDetails}</p></div>
      <div className="installation-network__apply">
        <h3>{t.hostTitle}</h3><p>{t.hostText}</p>
        <div className="installation-network__actions"><button type="button" className="secondary" onClick={() => downloadScript('ps1')}>{t.powershell}</button><button type="button" className="secondary" onClick={() => downloadScript('sh')}>{t.bash}</button></div>
        <label>{t.command}<select value={commandPlatform} onChange={event => setCommandPlatform(event.target.value as typeof commandPlatform)}><option value="powershell">PowerShell</option><option value="bash">Bash (Linux)</option></select></label>
        <textarea aria-label={t.command} readOnly value={command} rows={3}/>
        <button type="button" disabled={!command} onClick={() => void copyCommand()}>{t.copyCommand}</button>
        {copied && <p role="status">{t.copied}</p>}
        {copyFailure && <p role="alert">{t.noClipboard}</p>}
        <a href="/help?topic=installation.network-ports">{t.clickHelp}</a>
      </div>
    </>}
  </section>;
}

function PortTable({ profile, t }: { profile: NetworkProfile; t: (typeof copy)[EngineeringLocale] }) {
  const rows = [
    ['Web', profile.web], ['API', profile.api], [t.dbPort, profile.database], ['Media control', profile.mediaControl], ['HLS', profile.mediaPlayback]
  ] as const;
  return <div className="installation-network__table-wrap"><table><thead><tr><th>{t.tableService}</th><th>{t.transport}</th><th>{t.internal}</th><th>{t.hostPort}</th><th>{t.status}</th></tr></thead><tbody>{rows.map(([name, port]) => <tr key={name}><th>{name}</th><td>{port.transport}</td><td>{port.internalPort}</td><td>{port.publishedToHost ? `${port.bindAddress}:${port.publishedPort}` : '—'}</td><td>{port.publishedToHost ? t.exposed : t.private}</td></tr>)}</tbody></table></div>;
}

function ServiceEndpointInventory({ locale }: { locale: EngineeringLocale }) {
  const t = {
    'pt-BR': ['Serviços configurados', 'Endereços informados pelo servidor. Esta leitura não confirma a disponibilidade dos serviços.', 'Não informado', 'Não foi possível consultar os serviços.', 'Serviço', 'Endereço', 'Porta', 'Consultar novamente', 'Consultando serviços…'],
    en: ['Configured services', 'Addresses reported by the server. This does not confirm service availability.', 'Not reported', 'Could not retrieve services.', 'Service', 'Address', 'Port', 'Refresh services', 'Reading services…'],
    es: ['Servicios configurados', 'Direcciones informadas por el servidor. Esta lectura no confirma la disponibilidad de los servicios.', 'No informado', 'No se pudieron consultar los servicios.', 'Servicio', 'Dirección', 'Puerto', 'Consultar de nuevo', 'Consultando servicios…']
  }[locale];
  const [rows, setRows] = useState<Array<{ service: string; host: string | null; port: number | null }>>([]);
  const [state, setState] = useState<'loading' | 'ready' | 'failed'>('loading');
  async function refresh() {
    setState('loading');
    try {
      const response = await fetch(`${API}/api/installation/service-endpoints`, { credentials: 'same-origin', headers: { accept: 'application/json' } });
      if (!response.ok) throw new Error('Inventory unavailable');
      const result: unknown = await response.json();
      if (!Array.isArray(result)) throw new Error('Invalid inventory');
      setRows(result);
      setState('ready');
    } catch { setRows([]); setState('failed'); }
  }
  useEffect(() => { void refresh(); }, []);
  const names: Record<string, string[]> = {
    EliteScada: ['Banco principal', 'Primary database', 'Base principal'], Historian: ['Banco do histórico', 'Historian database', 'Base histórica'],
    MediaControl: ['Controle de mídia', 'Media control', 'Control multimedia'], MediaPlayback: ['Vídeo HLS', 'HLS video', 'Vídeo HLS']
  };
  const language = locale === 'pt-BR' ? 0 : locale === 'en' ? 1 : 2;
  return <div className="installation-network__current" data-testid="installation-service-inventory">
    <h3>{t[0]}</h3><p>{t[1]}</p>
    {state === 'loading' && <p role="status">{t[8]}</p>}
    {state === 'failed' && <p role="alert">{t[3]}</p>}
    {state === 'ready' && <div className="installation-network__table-wrap"><table><thead><tr><th>{t[4]}</th><th>{t[5]}</th><th>{t[6]}</th></tr></thead><tbody>{rows.map((row, index) => <tr key={`${row.service}-${index}`}><th>{names[row.service]?.[language] ?? row.service}</th><td>{row.host ?? t[2]}</td><td>{row.port ?? t[2]}</td></tr>)}</tbody></table></div>}
    <button type="button" className="secondary" disabled={state === 'loading'} onClick={() => void refresh()}>{t[7]}</button>
  </div>;
}

function DatabaseReadinessCheck({ locale }: { locale: EngineeringLocale }) {
  const t = {
    'pt-BR': { title: 'Verificar o banco da aplicação', text: 'Confere a conexão e a compatibilidade do banco principal e do histórico configurados. Faça esta verificação após uma alteração. O resultado não valida drivers ou o funcionamento completo da aplicação.', button: 'Verificar banco', busy: 'Verificando banco…', ready: 'Banco disponível e compatível.', unavailable: 'O banco ainda não está pronto. Consulte Administração do banco para verificar conexão, operação pendente ou recuperação.', failed: 'Não foi possível verificar o banco. Confira sua sessão e a conexão com o servidor.' },
    en: { title: 'Check application database', text: 'Checks the configured primary and Historian database connection and compatibility. Run after a change. This does not validate drivers or the complete application.', button: 'Check database', busy: 'Checking database…', ready: 'Database available and compatible.', unavailable: 'Database is not ready. Open database administration to check connection, pending operations or recovery.', failed: 'Could not check the database. Check your session and server connection.' },
    es: { title: 'Verificar la base de la aplicación', text: 'Comprueba conexión y compatibilidad de las bases principal e histórica configuradas. Ejecute después de un cambio. No valida drivers ni toda la aplicación.', button: 'Verificar base', busy: 'Verificando base…', ready: 'Base disponible y compatible.', unavailable: 'La base aún no está lista. Consulte Administración de base para revisar conexión, operaciones pendientes o recuperación.', failed: 'No se pudo verificar la base. Compruebe su sesión y la conexión con el servidor.' }
  }[locale];
  const [state, setState] = useState<'idle' | 'busy' | 'ready' | 'unavailable' | 'failed'>('idle');
  async function check() {
    setState('busy');
    try {
      const response = await fetch(`${API}/api/installation/database-readiness`, { credentials: 'same-origin', headers: { accept: 'application/json' } });
      if (response.status !== 200 && response.status !== 503) throw new Error('Readiness unavailable');
      const result = await response.json() as { ready?: unknown };
      if (typeof result.ready !== 'boolean') throw new Error('Invalid readiness');
      setState(response.status === 200 && result.ready ? 'ready' : 'unavailable');
    } catch { setState('failed'); }
  }
  return <div className="installation-network__current" data-testid="installation-database-readiness">
    <h3>{t.title}</h3><p>{t.text}</p>
    <button type="button" disabled={state === 'busy'} onClick={() => void check()}>{state === 'busy' ? t.busy : t.button}</button>
    {state !== 'idle' && state !== 'busy' && <p role={state === 'ready' ? 'status' : 'alert'}>{t[state]}</p>}
  </div>;
}

function NativeAccessPortPreparation({ locale }: { locale: EngineeringLocale }) {
  const t = {
    'pt-BR': ['Portas de acesso nativas', 'Porta Web', 'Porta API', 'Use o comando na pasta administrativa fornecida pela instalação, que contém o perfil e os adaptadores. O comando apresenta o plano; a aplicação exige a opção Apply indicada pelo procedimento. Não muda banco ou mídia.', 'As portas devem ser válidas e diferentes.', 'Não foi possível consultar o perfil nativo.', 'Comando para preparar alteração'],
    en: ['Native access ports', 'Web port', 'API port', 'Run from the administrative folder supplied by the installation, containing its profile and adapters. The command previews the plan; applying requires the Apply option described by the procedure. It does not change database or media.', 'Ports must be valid and distinct.', 'Could not retrieve the native profile.', 'Preview command'],
    es: ['Puertos de acceso nativos', 'Puerto Web', 'Puerto API', 'Ejecute desde la carpeta administrativa de la instalación, con perfil y adaptadores. El comando presenta el plan; aplicar requiere la opción Apply del procedimiento. No cambia base ni multimedia.', 'Los puertos deben ser válidos y distintos.', 'No se pudo consultar el perfil nativo.', 'Comando de vista previa']
  }[locale];
  const [profile, setProfile] = useState<NativePortProfile | null>(null);
  const [failed, setFailed] = useState(false);
  const [web, setWeb] = useState('');
  const [api, setApi] = useState('');
  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const response = await fetch(`${API}/api/installation/native-network-profile`, { credentials: 'same-origin' });
        if (response.status === 404) return;
        if (!response.ok) throw new Error('Profile unavailable');
        const result = await response.json();
        if (!['windows', 'linux'].includes(result.platform) || !Number.isInteger(result.webPort) || !Number.isInteger(result.apiPort)) throw new Error('Invalid profile');
        if (active) { setProfile(result); setWeb(String(result.webPort)); setApi(String(result.apiPort)); }
      } catch { if (active) setFailed(true); }
    })();
    return () => { active = false; };
  }, []);
  if (failed) return <p role="alert">{t[5]}</p>;
  if (!profile) return null;
  const valid = [web, api].every(value => /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= 65535) && Number(web) !== Number(api);
  const command = !valid || !profile.administrativeProfileAvailable ? '' : profile.platform === 'windows'
    ? `powershell -NoProfile -File ./apply-native-network-profile.ps1 -ProfilePath ./native-network-profile.json -WebPort ${Number(web)} -ApiPort ${Number(api)}`
    : `bash ./apply-native-network-profile.sh --profile ./native-network-profile.json --web-port ${Number(web)} --api-port ${Number(api)}`;
  return <div className="installation-network__form" data-testid="native-access-ports"><h3>{t[0]}</h3><p>{t[3]}</p>
    <label>{t[1]}<input type="number" min="1" max="65535" value={web} onChange={event => setWeb(event.target.value)}/></label>
    <label>{t[2]}<input type="number" min="1" max="65535" value={api} onChange={event => setApi(event.target.value)}/></label>
    {!valid && <p role="alert">{t[4]}</p>}
    {!profile.administrativeProfileAvailable && <p role="status">{locale === 'pt-BR' ? 'O perfil administrativo ainda não foi fornecido pela instalação. A preparação do comando ficará disponível quando esse provisionamento for concluído.' : locale === 'es' ? 'La instalación aún no proporcionó el perfil administrativo. El comando estará disponible después del aprovisionamiento.' : 'The installation has not supplied its administrative profile. The command will be available after provisioning.'}</p>}
    <textarea aria-label={t[6]} value={command} readOnly rows={3}/>
    {profile.administrativeProfileAvailable && <NativeDependentPortPreparation profile={profile} locale={locale} web={web} api={api}/>}
  </div>;
}

type NativePortProfile = { platform: 'windows' | 'linux'; webPort: number; apiPort: number; administrativeProfileAvailable: boolean; databasePort?: number | null; mediaControlPort?: number | null; mediaPlaybackPort?: number | null };

function NativeDependentPortPreparation({ profile, locale, web, api }: { profile: NativePortProfile; locale: EngineeringLocale; web: string; api: string }) {
  const [database, setDatabase] = useState(String(profile.databasePort ?? ''));
  const [control, setControl] = useState(String(profile.mediaControlPort ?? ''));
  const [playback, setPlayback] = useState(String(profile.mediaPlaybackPort ?? ''));
  const hasDatabase = Number.isInteger(profile.databasePort) && Number(profile.databasePort) > 0;
  const hasMedia = Number.isInteger(profile.mediaControlPort) && Number.isInteger(profile.mediaPlaybackPort) && Number(profile.mediaControlPort) > 0 && Number(profile.mediaPlaybackPort) > 0;
  if (!hasDatabase && !hasMedia) return null;
  const t = {
    'pt-BR': ['Banco e mídia locais', 'Porta do banco local', 'Porta do controle de mídia', 'Porta HLS', 'Execute um procedimento por vez. Os comandos apresentam planos; aplique somente após revisar. No Windows, use o Python fornecido pela instalação. Banco remoto e Drivers não são alterados.', 'Use portas válidas e distintas para os serviços locais.', 'Plano do banco local', 'Plano da mídia local'],
    en: ['Local database and media', 'Local database port', 'Media control port', 'HLS port', 'Run one procedure at a time. Commands preview plans; apply only after review. On Windows, use the installation-provided Python. Remote databases and Drivers are not changed.', 'Use valid, distinct ports for local services.', 'Local database plan', 'Local media plan'],
    es: ['Base y multimedia locales', 'Puerto de base local', 'Puerto de control multimedia', 'Puerto HLS', 'Ejecute un procedimiento por vez. Los comandos presentan planes; aplique solo después de revisar. En Windows use Python de la instalación. No se cambian bases remotas ni Drivers.', 'Use puertos válidos y distintos para servicios locales.', 'Plan de base local', 'Plan multimedia local']
  }[locale];
  const values = [web, api, ...(hasDatabase ? [database] : []), ...(hasMedia ? [control, playback] : [])];
  const valid = values.every(value => /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= 65535) && new Set(values.map(Number)).size === values.length;
  const windows = profile.platform === 'windows';
  const databaseCommand = !valid ? '' : windows
    ? `powershell -NoProfile -File ./apply-native-database-port.ps1 -ProfilePath ./native-network-profile.json -PythonExecutable $PythonDaInstalacao -DatabasePort ${Number(database)}`
    : `bash ./apply-native-database-port.sh --profile ./native-network-profile.json --database-port ${Number(database)}`;
  const mediaCommand = !valid ? '' : windows
    ? `powershell -NoProfile -File ./apply-native-media-ports.ps1 -ProfilePath ./native-network-profile.json -PythonExecutable $PythonDaInstalacao -ControlPort ${Number(control)} -HlsPort ${Number(playback)}`
    : `bash ./apply-native-media-ports.sh --profile ./native-network-profile.json --control-port ${Number(control)} --hls-port ${Number(playback)}`;
  return <div data-testid="native-dependent-ports"><h4>{t[0]}</h4><p>{t[4]}</p>
    {hasDatabase && <><label>{t[1]}<input type="number" min="1" max="65535" value={database} onChange={event => setDatabase(event.target.value)}/></label><textarea aria-label={t[6]} readOnly value={databaseCommand} rows={3}/></>}
    {hasMedia && <><label>{t[2]}<input type="number" min="1" max="65535" value={control} onChange={event => setControl(event.target.value)}/></label><label>{t[3]}<input type="number" min="1" max="65535" value={playback} onChange={event => setPlayback(event.target.value)}/></label><textarea aria-label={t[7]} readOnly value={mediaCommand} rows={3}/></>}
    {!valid && <p role="alert">{t[5]}</p>}
  </div>;
}
