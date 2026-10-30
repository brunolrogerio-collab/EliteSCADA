import React, { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import './installation-network.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type EndpointProfile = { transport: string; bindAddress: string; publishedPort: number; internalPort: number; publishedToHost: boolean };
type DriverPortProfile = EndpointProfile;
type NetworkProfile = {
  profile: string;
  instanceId: string;
  web: EndpointProfile;
  api: EndpointProfile;
  database: EndpointProfile;
  mediaControl: EndpointProfile;
  mediaPlayback: EndpointProfile;
  driverPorts: DriverPortProfile[];
  applyThroughUi: boolean;
  hostMappingIndependentlyVerified: boolean;
};
type DriverPortDraft = { transport: 'tcp' | 'udp'; bindAddress: '127.0.0.1' | '0.0.0.0'; publishedPort: string; internalPort: string };
type Draft = { instanceId: string; webBind: '127.0.0.1' | '0.0.0.0'; apiBind: '127.0.0.1' | '0.0.0.0'; webPort: string; apiPort: string; databasePort: string; driverPorts: DriverPortDraft[] };

const copy = {
  'pt-BR': {
    title: 'Rede e serviços', intro: 'Prepare uma configuração de portas para o pacote Docker estável. A mudança é aplicada no computador que executa o Docker, não pelo navegador.',
    admin: 'Esta área exige a capacidade SystemAdmin da Security Authority.', loading: 'Lendo a configuração informada por esta instalação…', unavailable: 'Esta instalação não informa o perfil Docker estável suportado. A tela não altera instalações nativas ou serviços externos.',
    error: 'Não foi possível ler a configuração desta instalação.', retry: 'Atualizar', current: 'Configuração informada pelo container atual', currentNote: 'Estes valores foram enviados ao container pelo Compose. O SCADIVO não consegue verificar, de dentro do container, se o host publicou as portas corretamente.',
    desired: 'Preparar nova configuração', identity: 'ID desta instância', identityHelp: 'Use letras minúsculas, números e hífens. Para uma segunda instalação, use outra pasta, gere um .env com segredos próprios e defina nele o ID escolhido antes de executar o comando. Nunca copie chaves ou credenciais.',
    bind: 'Endereço de acesso', loopback: 'Somente neste computador', allInterfaces: 'Interfaces de rede', webPort: 'Porta Web no host', apiPort: 'Porta direta da API no host', dbPort: 'Porta do banco no host', internal: 'Porta dentro do container', transport: 'Protocolo', exposed: 'Publicada no host', private: 'Somente rede Docker', fixed: 'Fixa neste pacote', databaseBind: 'Banco limitado a 127.0.0.1 para reduzir exposição.',
    preview: 'Endereços após aplicar', webUrl: 'Interface Web', apiUrl: 'API direta', restart: 'Impacto: o Compose recria serviços afetados; a navegação pode cair durante a troca. O banco e os volumes permanecem.',
    limitsTitle: 'O que este perfil controla', limits: 'Este assistente altera as portas do host para Web, API e PostgreSQL e pode publicar listeners TCP/UDP que já estejam configurados nos Drivers. Ele não muda firewall, banco remoto, endereços dos peers de High Availability nem a porta interna definida no Data Source.',
    mediaLabel: 'Mídia:', mediaDetails: 'o controle (9997) e o HLS (8888) permanecem na rede privada do Compose. O Runtime entrega o vídeo pelo relay da API usando tíquetes temporários; não publique essas portas diretamente.',
    driversLabel: 'Equipamentos e protocolos:', driversDetails: 'A porta interna precisa ser igual à porta de escuta já definida no Data Source. O mapeamento só publica essa porta no host; ele não altera o Driver. Drivers clientes, como Modbus TCP, conectam-se ao equipamento remoto e não precisam de publicação. No Docker estável, configure Modbus TCP Server em porta interna 1502 (ou outra a partir de 1024) e bind 0.0.0.0: a API roda sem privilégios e não pode abrir portas internas abaixo de 1024. Se os clientes usam a porta padrão 502, publique host 502 para container 1502, se o host permitir. BACnet/IP pode usar publicação UDP para tráfego unicast; descoberta por broadcast não é garantida pela rede Docker bridge.',
    driverPortsTitle: 'Listeners de Drivers (opcional)', driverPortsHelp: 'Adicione apenas listeners configurados em Data Sources deste SCADIVO. A porta interna é a de escuta no container; a porta do host é a usada pelos clientes externos. O padrão fica sem portas de Driver publicadas.', driverPortAdd: 'Adicionar listener', driverPortRemove: 'Remover', driverPortProtocol: 'Protocolo', driverPortHost: 'Porta no host', driverPortInternal: 'Porta de escuta no container', driverPortBind: 'Publicar em', driverPortCollision: 'A porta TCP do Driver conflita com Web, API ou banco.', driverPortInternalCollision: 'A porta TCP 5080 já pertence à API e não pode ser usada por um listener de Driver.', driverPortPrivilegedInternal: 'A API no Docker roda sem privilégios e não pode abrir portas internas abaixo de 1024.', driverPortDuplicateHost: 'Há portas de host repetidas para o mesmo protocolo.', driverPortDuplicateInternal: 'Há listeners repetidos na mesma porta/protocolo dentro da API.', driverPortLimit: 'É possível configurar até 32 listeners.', driverPortExposure: 'Este listener ficará disponível em todas as interfaces. O script não altera o firewall. Restrinja o acesso e revise a segurança do Driver antes de aplicar.', driverPortDefaults: 'No Docker, use 1502 (ou outra porta >= 1024) como porta interna do Modbus TCP Server; o processo da API não tem privilégio para escutar abaixo de 1024. Se necessário, publique a porta 502 do host para a 1502 interna. BACnet/IP usa UDP 47808 por padrão; broadcast ainda depende da rede.',
    hostTitle: 'Aplicação no host', hostText: 'Baixe o script da plataforma, coloque-o na pasta que contém docker-compose.stable.yml e .env, revise-o e execute o comando abaixo nessa pasta em uma conta autorizada a administrar o Docker. O script valida portas e Compose antes de trocar, guarda cópias da configuração, verifica saúde e tenta restaurá-las se falhar. Não altera firewall nem apaga volumes.',
    powershell: 'Baixar script PowerShell', bash: 'Baixar script Linux', command: 'Comando para aplicar', copyCommand: 'Copiar comando', copied: 'Comando copiado.', noClipboard: 'O navegador não permitiu copiar; selecione o comando e copie manualmente.',
    invalidId: 'Use um ID de 1 a 48 caracteres: minúsculas, números ou hífen; comece com letra ou número.', invalidPort: 'Cada porta deve ser um número inteiro entre 1 e 65535.', duplicatePorts: 'Web, API e banco precisam usar portas diferentes.', wildcardWarn: 'Com 0.0.0.0, o serviço escuta em todas as interfaces. O SCADIVO não abre firewall nem configura TLS; use proxy TLS e política de firewall antes de permitir acesso externo.', portPrivilege: 'Portas abaixo de 1024 podem exigir configuração privilegiada no host Docker.', newInstance: 'Para criar outra instalação, o comando só funciona em uma pasta nova, com .env independente e o ID escolhido já definido nela. O script recusa reaproveitar ID ou volumes existentes.',
    tableService: 'Serviço', appPort: 'Porta interna', hostPort: 'Porta do host', status: 'Exposição', clickHelp: 'Abrir instruções completas na Ajuda',
    driverPortTcp: 'TCP', driverPortUdp: 'UDP'
  },
  en: {
    title: 'Network and services', intro: 'Prepare a port profile for the stable Docker package. Apply it on the computer running Docker, not through the browser.',
    admin: 'This area requires the Security Authority SystemAdmin capability.', loading: 'Reading the configuration reported by this installation…', unavailable: 'This installation does not report the supported stable Docker profile. This page does not change native installations or external services.',
    error: 'Could not read this installation configuration.', retry: 'Refresh', current: 'Configuration reported by the current container', currentNote: 'These values were passed into the container by Compose. SCADIVO cannot verify from inside the container whether the host published the ports correctly.',
    desired: 'Prepare a new configuration', identity: 'Instance ID', identityHelp: 'Use lowercase letters, numbers, and hyphens. For a second installation, use another folder, create an .env with its own secrets, and set the chosen ID in it before running the command. Never copy keys or credentials.',
    bind: 'Access address', loopback: 'This computer only', allInterfaces: 'Network interfaces', webPort: 'Web host port', apiPort: 'Direct API host port', dbPort: 'Database host port', internal: 'Inside the container', transport: 'Protocol', exposed: 'Published on host', private: 'Docker network only', fixed: 'Fixed in this package', databaseBind: 'Database remains bound to 127.0.0.1 to reduce exposure.',
    preview: 'Addresses after applying', webUrl: 'Web interface', apiUrl: 'Direct API', restart: 'Impact: Compose recreates affected services; the page may disconnect during the change. The database and volumes remain.',
    limitsTitle: 'What this profile controls', limits: 'This assistant changes host ports for Web, API, and PostgreSQL and can publish TCP/UDP listeners already configured in Drivers. It does not change firewall rules, remote databases, High Availability peer addresses, or the internal port set in a Data Source.',
    mediaLabel: 'Media:', mediaDetails: 'control (9997) and HLS (8888) stay on the private Compose network. Runtime sends video through the API relay using short-lived tickets; do not publish these ports directly.',
    driversLabel: 'Equipment and protocols:', driversDetails: 'The internal port must match the listener already configured in the Data Source. This mapping only publishes that port on the host; it does not change the Driver. Client drivers such as Modbus TCP connect to remote equipment and need no host publication. In stable Docker, configure Modbus TCP Server on internal port 1502 (or another port at or above 1024) and bind it to 0.0.0.0: the API runs without elevated privileges and cannot bind internal ports below 1024. If clients expect standard port 502, publish host 502 to container 1502 when the host allows it. BACnet/IP can use UDP publication for unicast traffic; broadcast discovery is not guaranteed over Docker bridge networking.',
    driverPortsTitle: 'Driver listeners (optional)', driverPortsHelp: 'Add only listeners configured in this SCADIVO installation. The internal port is the listener inside the container; external clients use the host port. By default, no Driver ports are published.', driverPortAdd: 'Add listener', driverPortRemove: 'Remove', driverPortProtocol: 'Protocol', driverPortHost: 'Host port', driverPortInternal: 'Listener port in container', driverPortBind: 'Publish on', driverPortCollision: 'The Driver TCP port conflicts with Web, API, or database.', driverPortInternalCollision: 'TCP port 5080 belongs to the API and cannot be used by a Driver listener.', driverPortPrivilegedInternal: 'The Docker API runs without elevated privileges and cannot bind internal ports below 1024.', driverPortDuplicateHost: 'Host ports repeat for the same protocol.', driverPortDuplicateInternal: 'Listeners repeat on the same API port/protocol.', driverPortLimit: 'Up to 32 listeners can be configured.', driverPortExposure: 'This listener will be available on all interfaces. The script does not change the firewall. Restrict access and review Driver security before applying.', driverPortDefaults: 'In Docker, use 1502 (or another port at or above 1024) as the Modbus TCP Server internal port; the API process cannot bind ports below 1024. If needed, publish host port 502 to internal port 1502. BACnet/IP uses UDP 47808 by default; broadcast still depends on network configuration.',
    hostTitle: 'Apply on the host', hostText: 'Download the platform script, place it in the folder containing docker-compose.stable.yml and .env, review it, then run the command below from that folder with an account allowed to administer Docker. It validates ports and Compose before changing anything, backs up the configuration files, checks health, and attempts to restore them on failure. It does not change firewall rules or delete volumes.',
    powershell: 'Download PowerShell script', bash: 'Download Linux script', command: 'Apply command', copyCommand: 'Copy command', copied: 'Command copied.', noClipboard: 'The browser did not allow copying; select and copy the command manually.',
    invalidId: 'Use an ID of 1–48 characters: lowercase letters, numbers, or hyphens; begin with a letter or number.', invalidPort: 'Each port must be an integer from 1 through 65535.', duplicatePorts: 'Web, API, and database must use different ports.', wildcardWarn: 'With 0.0.0.0, the service listens on all interfaces. SCADIVO does not open firewalls or configure TLS; use a TLS proxy and firewall policy before allowing external access.', portPrivilege: 'Ports below 1024 may require privileged host configuration for Docker.', newInstance: 'To create another installation, run the command only in a new folder with its own .env and the chosen ID already set there. The script refuses existing IDs or volumes.',
    tableService: 'Service', appPort: 'Internal port', hostPort: 'Host port', status: 'Exposure', clickHelp: 'Open full instructions in Help',
    driverPortTcp: 'TCP', driverPortUdp: 'UDP'
  },
  es: {
    title: 'Red y servicios', intro: 'Prepare un perfil de puertos para el paquete Docker estable. Aplíquelo en el equipo que ejecuta Docker, no desde el navegador.',
    admin: 'Esta área requiere la capability SystemAdmin de Security Authority.', loading: 'Leyendo la configuración informada por esta instalación…', unavailable: 'Esta instalación no informa el perfil Docker estable compatible. Esta página no cambia instalaciones nativas ni servicios externos.',
    error: 'No fue posible leer la configuración de esta instalación.', retry: 'Actualizar', current: 'Configuración informada por el contenedor actual', currentNote: 'Compose pasó estos valores al contenedor. Desde dentro del contenedor, SCADIVO no puede verificar si el host publicó las puertos correctamente.',
    desired: 'Preparar una nueva configuración', identity: 'ID de instancia', identityHelp: 'Use minúsculas, números y guiones. Para una segunda instalación, use otra carpeta, cree un .env con secretos propios y defina allí el ID elegido antes de ejecutar el comando. Nunca copie claves ni credenciales.',
    bind: 'Dirección de acceso', loopback: 'Solo este equipo', allInterfaces: 'Interfaces de red', webPort: 'Puerto Web en el host', apiPort: 'Puerto directo de API en el host', dbPort: 'Puerto de base de datos en el host', internal: 'Dentro del contenedor', transport: 'Protocolo', exposed: 'Publicada en el host', private: 'Solo red Docker', fixed: 'Fija en este paquete', databaseBind: 'La base de datos permanece vinculada a 127.0.0.1 para reducir la exposición.',
    preview: 'Direcciones después de aplicar', webUrl: 'Interfaz Web', apiUrl: 'API directa', restart: 'Impacto: Compose recrea los servicios afectados; la página puede desconectarse durante el cambio. La base de datos y los volúmenes permanecen.',
    limitsTitle: 'Qué controla este perfil', limits: 'Este asistente cambia los puertos del host para Web, API y PostgreSQL y puede publicar listeners TCP/UDP ya configurados en Drivers. No cambia firewalls, bases remotas, direcciones de peers de High Availability ni el puerto interno definido en el Data Source.',
    mediaLabel: 'Multimedia:', mediaDetails: 'el control (9997) y HLS (8888) permanecen en la red privada de Compose. Runtime entrega el vídeo por el relay de la API mediante tickets temporales; no publique estos puertos directamente.',
    driversLabel: 'Equipos y protocolos:', driversDetails: 'El puerto interno debe coincidir con el listener configurado en el Data Source. Este mapeo solo publica ese puerto en el host; no cambia el Driver. Los drivers cliente, como Modbus TCP, se conectan al equipo remoto y no necesitan publicación. En Docker estable, configure Modbus TCP Server en el puerto interno 1502 (u otro desde 1024) y enlácelo a 0.0.0.0: la API se ejecuta sin privilegios y no puede abrir puertos internos inferiores a 1024. Si los clientes esperan el puerto estándar 502, publique el puerto 502 del host hacia el 1502 del contenedor, si el host lo permite. BACnet/IP puede publicar UDP para tráfico unicast; el descubrimiento por broadcast no está garantizado con la red bridge de Docker.',
    driverPortsTitle: 'Listeners de Drivers (opcional)', driverPortsHelp: 'Agregue solo listeners configurados en esta instalación de SCADIVO. El puerto interno es el listener dentro del contenedor; los clientes externos usan el puerto del host. De forma predeterminada, no se publican puertos de Driver.', driverPortAdd: 'Agregar listener', driverPortRemove: 'Quitar', driverPortProtocol: 'Protocolo', driverPortHost: 'Puerto del host', driverPortInternal: 'Puerto listener en el contenedor', driverPortBind: 'Publicar en', driverPortCollision: 'El puerto TCP del Driver entra en conflicto con Web, API o base de datos.', driverPortInternalCollision: 'El puerto TCP 5080 pertenece a la API y no puede usarse para un listener de Driver.', driverPortPrivilegedInternal: 'La API en Docker se ejecuta sin privilegios y no puede abrir puertos internos inferiores a 1024.', driverPortDuplicateHost: 'Se repiten puertos del host para el mismo protocolo.', driverPortDuplicateInternal: 'Se repiten listeners en el mismo puerto/protocolo de la API.', driverPortLimit: 'Se pueden configurar hasta 32 listeners.', driverPortExposure: 'Este listener quedará disponible en todas las interfaces. El script no cambia el firewall. Restrinja el acceso y revise la seguridad del Driver antes de aplicar.', driverPortDefaults: 'En Docker, use 1502 (u otro puerto desde 1024) como puerto interno de Modbus TCP Server; el proceso de la API no puede escuchar en puertos inferiores a 1024. Si hace falta, publique el puerto 502 del host hacia el puerto interno 1502. BACnet/IP usa UDP 47808 por defecto; broadcast aún depende de la red.',
    hostTitle: 'Aplicación en el host', hostText: 'Descargue el script de la plataforma, colóquelo en la carpeta que contiene docker-compose.stable.yml y .env, revíselo y ejecute el comando siguiente desde esa carpeta con una cuenta autorizada para administrar Docker. Valida puertos y Compose antes del cambio, respalda los archivos de configuración, comprueba la salud e intenta restaurarlos si falla. No modifica el firewall ni elimina volúmenes.',
    powershell: 'Descargar script PowerShell', bash: 'Descargar script Linux', command: 'Comando de aplicación', copyCommand: 'Copiar comando', copied: 'Comando copiado.', noClipboard: 'El navegador no permitió copiar; seleccione y copie el comando manualmente.',
    invalidId: 'Use un ID de 1 a 48 caracteres: minúsculas, números o guiones; comience con letra o número.', invalidPort: 'Cada puerto debe ser un entero de 1 a 65535.', duplicatePorts: 'Web, API y base de datos necesitan puertos distintos.', wildcardWarn: 'Con 0.0.0.0, el servicio escucha en todas las interfaces. SCADIVO no abre firewalls ni configura TLS; use un proxy TLS y una política de firewall antes del acceso externo.', portPrivilege: 'Los puertos inferiores a 1024 pueden requerir configuración privilegiada del host Docker.', newInstance: 'Para crear otra instalación, ejecute el comando solo en una carpeta nueva, con su .env propio y el ID elegido ya definido. El script rechaza IDs o volúmenes existentes.',
    tableService: 'Servicio', appPort: 'Puerto interno', hostPort: 'Puerto del host', status: 'Exposición', clickHelp: 'Abrir instrucciones completas en Ayuda',
    driverPortTcp: 'TCP', driverPortUdp: 'UDP'
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
  const [draft, setDraft] = useState<Draft>({ instanceId: '', webBind: '127.0.0.1', apiBind: '127.0.0.1', webPort: '18080', apiPort: '15080', databasePort: '15432', driverPorts: [] });

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
        driverPorts: (profile.driverPorts ?? []).map(port => ({
          transport: port.transport.toLowerCase() === 'udp' ? 'udp' : 'tcp',
          bindAddress: port.bindAddress === '0.0.0.0' ? '0.0.0.0' : '127.0.0.1',
          publishedPort: String(port.publishedPort),
          internalPort: String(port.internalPort)
        }))
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
    const ports = [draft.webPort, draft.apiPort, draft.databasePort].map(Number);
    if (ports.some(port => !Number.isInteger(port) || port < 1 || port > 65535)) errors.push(t.invalidPort);
    if (ports.every(port => Number.isInteger(port) && port >= 1 && port <= 65535) && new Set(ports).size !== ports.length) errors.push(t.duplicatePorts);
    if (draft.driverPorts.length > 32) errors.push(t.driverPortLimit);
    const mappingPorts = draft.driverPorts.flatMap(mapping => [Number(mapping.publishedPort), Number(mapping.internalPort)]);
    if (mappingPorts.some(port => !Number.isInteger(port) || port < 1 || port > 65535)) errors.push(t.invalidPort);
    const validDriverPorts = draft.driverPorts.filter(mapping =>
      Number.isInteger(Number(mapping.publishedPort)) && Number(mapping.publishedPort) >= 1 && Number(mapping.publishedPort) <= 65535 &&
      Number.isInteger(Number(mapping.internalPort)) && Number(mapping.internalPort) >= 1 && Number(mapping.internalPort) <= 65535);
    if (validDriverPorts.some(mapping => Number(mapping.internalPort) < 1024)) errors.push(t.driverPortPrivilegedInternal);
    const publishedKeys = validDriverPorts.map(mapping => `${mapping.transport}:${Number(mapping.publishedPort)}`);
    if (new Set(publishedKeys).size !== publishedKeys.length) errors.push(t.driverPortDuplicateHost);
    const internalKeys = validDriverPorts.map(mapping => `${mapping.transport}:${Number(mapping.internalPort)}`);
    if (new Set(internalKeys).size !== internalKeys.length) errors.push(t.driverPortDuplicateInternal);
    if (validDriverPorts.some(mapping => mapping.transport === 'tcp' && ports.includes(Number(mapping.publishedPort)))) errors.push(t.driverPortCollision);
    if (validDriverPorts.some(mapping => mapping.transport === 'tcp' && Number(mapping.internalPort) === 5080)) errors.push(t.driverPortInternalCollision);
    return errors;
  }, [draft, t]);

  const command = useMemo(() => {
    if (!current || validation.length) return '';
    const newInstance = draft.instanceId !== current.instanceId;
    const newInstall = newInstance ? ` -NewInstallation -SourceInstanceId ${current.instanceId} -ConfirmIsolatedSecrets` : '';
    const newInstallBash = newInstance ? ` --new-installation --source-instance-id ${current.instanceId} --confirm-isolated-secrets` : '';
    const driverPorts = draft.driverPorts.map(port => `${port.transport}:${port.bindAddress}:${Number(port.publishedPort)}:${Number(port.internalPort)}`).join(';');
    return commandPlatform === 'powershell'
      ? `powershell -NoProfile -ExecutionPolicy Bypass -File .\\apply-network-profile.ps1 -InstallDirectory . -InstanceId ${draft.instanceId} -WebBind ${draft.webBind} -ApiBind ${draft.apiBind} -WebPort ${draft.webPort} -ApiPort ${draft.apiPort} -DatabasePort ${draft.databasePort} -ReplaceDriverPorts -DriverPorts '${driverPorts}'${newInstall}`
      : `bash ./apply-network-profile.sh --install-directory . --instance-id ${draft.instanceId} --web-bind ${draft.webBind} --api-bind ${draft.apiBind} --web-port ${draft.webPort} --api-port ${draft.apiPort} --database-port ${draft.databasePort} --replace-driver-ports --driver-ports '${driverPorts}'${newInstallBash}`;
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

  function addDriverPort() {
    setDraft(value => value.driverPorts.length >= 32 ? value : ({
      ...value,
      driverPorts: [...value.driverPorts, { transport: 'tcp', bindAddress: '127.0.0.1', publishedPort: '1502', internalPort: '1502' }]
    }));
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
        <p className="installation-network__help">{t.identityHelp}</p>
        <label>{t.identity}<input value={draft.instanceId} onChange={event => setDraft(value => ({ ...value, instanceId: event.target.value }))} maxLength={48} autoComplete="off" spellCheck={false}/></label>
        {draft.instanceId !== current.instanceId && <p className="installation-network__warning" role="note">{t.newInstance}</p>}
        <div className="installation-network__grid">
          <label>{t.bind} — Web<select value={draft.webBind} onChange={event => setDraft(value => ({ ...value, webBind: event.target.value as Draft['webBind'] }))}><option value="127.0.0.1">{t.loopback} (127.0.0.1)</option><option value="0.0.0.0">{t.allInterfaces} (0.0.0.0)</option></select></label>
          <label>{t.webPort}<input type="number" min="1" max="65535" step="1" value={draft.webPort} onChange={event => setDraft(value => ({ ...value, webPort: event.target.value }))}/></label>
          <label>{t.bind} — API<select value={draft.apiBind} onChange={event => setDraft(value => ({ ...value, apiBind: event.target.value as Draft['apiBind'] }))}><option value="127.0.0.1">{t.loopback} (127.0.0.1)</option><option value="0.0.0.0">{t.allInterfaces} (0.0.0.0)</option></select></label>
          <label>{t.apiPort}<input type="number" min="1" max="65535" step="1" value={draft.apiPort} onChange={event => setDraft(value => ({ ...value, apiPort: event.target.value }))}/></label>
          <label>{t.dbPort}<input type="number" min="1" max="65535" step="1" value={draft.databasePort} onChange={event => setDraft(value => ({ ...value, databasePort: event.target.value }))}/><small>{t.databaseBind}</small></label>
        </div>
        <div className="installation-network__driver-ports">
          <div><h4>{t.driverPortsTitle}</h4><p>{t.driverPortsHelp}</p></div>
          {draft.driverPorts.map((mapping, index) => <fieldset className="installation-network__driver-port" key={index}>
            <legend>{t.driverPortsTitle} {index + 1}</legend>
            <label>{t.driverPortProtocol}<select value={mapping.transport} onChange={event => setDraft(value => ({ ...value, driverPorts: value.driverPorts.map((item, itemIndex) => itemIndex === index ? { ...item, transport: event.target.value as DriverPortDraft['transport'] } : item) }))}><option value="tcp">{t.driverPortTcp}</option><option value="udp">{t.driverPortUdp}</option></select></label>
            <label>{t.driverPortBind}<select value={mapping.bindAddress} onChange={event => setDraft(value => ({ ...value, driverPorts: value.driverPorts.map((item, itemIndex) => itemIndex === index ? { ...item, bindAddress: event.target.value as DriverPortDraft['bindAddress'] } : item) }))}><option value="127.0.0.1">{t.loopback} (127.0.0.1)</option><option value="0.0.0.0">{t.allInterfaces} (0.0.0.0)</option></select></label>
            <label>{t.driverPortHost}<input type="number" min="1" max="65535" step="1" value={mapping.publishedPort} onChange={event => setDraft(value => ({ ...value, driverPorts: value.driverPorts.map((item, itemIndex) => itemIndex === index ? { ...item, publishedPort: event.target.value } : item) }))}/></label>
            <label>{t.driverPortInternal}<input type="number" min="1" max="65535" step="1" value={mapping.internalPort} onChange={event => setDraft(value => ({ ...value, driverPorts: value.driverPorts.map((item, itemIndex) => itemIndex === index ? { ...item, internalPort: event.target.value } : item) }))}/></label>
            <button type="button" className="secondary" aria-label={`${t.driverPortRemove} ${index + 1}`} onClick={() => setDraft(value => ({ ...value, driverPorts: value.driverPorts.filter((_, itemIndex) => itemIndex !== index) }))}>{t.driverPortRemove}</button>
          </fieldset>)}
          <p>{t.driverPortDefaults}</p>
          <button type="button" className="secondary" disabled={draft.driverPorts.length >= 32} onClick={addDriverPort}>{t.driverPortAdd}</button>
          {draft.driverPorts.some(mapping => mapping.bindAddress === '0.0.0.0') && <p className="installation-network__warning" role="note">{t.driverPortExposure}</p>}
        </div>
        {(draft.webBind === '0.0.0.0' || draft.apiBind === '0.0.0.0') && <p className="installation-network__warning" role="note">{t.wildcardWarn}</p>}
        {[draft.webPort, draft.apiPort, draft.databasePort, ...draft.driverPorts.flatMap(mapping => [mapping.publishedPort, mapping.internalPort])].some(port => Number(port) > 0 && Number(port) < 1024) && <p className="installation-network__warning" role="note">{t.portPrivilege}</p>}
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
  const rows: Array<[string, EndpointProfile]> = [
    ['Web', profile.web], ['API', profile.api], [t.dbPort, profile.database], ['Media control', profile.mediaControl], ['HLS', profile.mediaPlayback],
    ...(profile.driverPorts ?? []).map((port, index) => [`${t.driverPortsTitle} ${index + 1}`, port] as [string, EndpointProfile])
  ];
  return <div className="installation-network__table-wrap"><table><thead><tr><th>{t.tableService}</th><th>{t.transport}</th><th>{t.internal}</th><th>{t.hostPort}</th><th>{t.status}</th></tr></thead><tbody>{rows.map(([name, port]) => <tr key={name}><th>{name}</th><td>{port.transport}</td><td>{port.internalPort}</td><td>{port.publishedToHost ? `${port.bindAddress}:${port.publishedPort}` : '—'}</td><td>{port.publishedToHost ? t.exposed : t.private}</td></tr>)}</tbody></table></div>;
}
