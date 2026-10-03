import type { EngineeringLocale } from './i18n';

type Localized = Readonly<Record<EngineeringLocale, string>>;

const resources: Readonly<Record<string, Localized>> = {
  'driver.modbus.tcp.datasource.host.label': {
    'pt-BR': 'Host', en: 'Host', es: 'Host'
  },
  'driver.modbus.tcp.datasource.host.description': {
    'pt-BR': 'Nome DNS ou endereço IPv4/IPv6 do controlador.',
    en: 'Controller hostname or IPv4/IPv6 address.',
    es: 'Nombre DNS o dirección IPv4/IPv6 del controlador.'
  },
  'driver.modbus.tcp.datasource.port.label': {
    'pt-BR': 'Porta', en: 'Port', es: 'Puerto'
  },
  'driver.modbus.tcp.datasource.port.description': {
    'pt-BR': 'Porta TCP do Modbus.', en: 'Modbus TCP port.', es: 'Puerto TCP de Modbus.'
  },
  'driver.modbus.tcp.datasource.scanIntervalMilliseconds.label': {
    'pt-BR': 'Intervalo de varredura (ms)', en: 'Scan interval (ms)', es: 'Intervalo de sondeo (ms)'
  },
  'driver.modbus.tcp.datasource.scanIntervalMilliseconds.description': {
    'pt-BR': 'Intervalo de polling em milissegundos.', en: 'Polling interval in milliseconds.', es: 'Intervalo de sondeo en milisegundos.'
  },
  'driver.modbus.tcp.datasource.requestTimeoutMilliseconds.label': {
    'pt-BR': 'Timeout da requisição (ms)', en: 'Request timeout (ms)', es: 'Timeout de la solicitud (ms)'
  },
  'driver.modbus.tcp.datasource.requestTimeoutMilliseconds.description': {
    'pt-BR': 'Tempo máximo de espera por uma requisição Modbus.', en: 'Maximum time to wait for a Modbus request.', es: 'Tiempo máximo de espera para una solicitud Modbus.'
  },
  'driver.modbus.tcp.datasource.maxGapElements.label': {
    'pt-BR': 'Lacuna máxima do bloco', en: 'Maximum block gap', es: 'Separación máxima del bloque'
  },
  'driver.modbus.tcp.datasource.maxGapElements.description': {
    'pt-BR': 'Maior lacuna de endereços que pode ser agregada em um único bloco de polling.',
    en: 'Maximum address gap merged into one polling block.',
    es: 'Separación máxima de direcciones que puede agruparse en un único bloque de sondeo.'
  },
  'driver.modbus.tcp.datasource.unitId.label': {
    'pt-BR': 'Unit ID', en: 'Unit ID', es: 'Unit ID'
  },
  'driver.modbus.tcp.datasource.unitId.description': {
    'pt-BR': 'Identificador Modbus Unit ID padrão da Fonte de dados.', en: 'Default Modbus unit identifier.', es: 'Identificador Unit ID Modbus predeterminado de la Fuente de datos.'
  },

  'driver.modbus.rtu.displayName': {
    'pt-BR': 'Modbus RTU Mestre', en: 'Modbus RTU Master', es: 'Modbus RTU Maestro'
  },
  'driver.modbus.rtu.description': {
    'pt-BR': 'Mestre Modbus RTU usando barramento serial compartilhado no servidor.',
    en: 'Modbus RTU master using a host-owned shared serial bus.',
    es: 'Maestro Modbus RTU usando un bus serie compartido en el servidor.'
  },
  'driver.modbus.tcp.server.displayName': {
    'pt-BR': 'Modbus TCP Servidor', en: 'Modbus TCP Server', es: 'Modbus TCP Servidor'
  },
  'driver.modbus.tcp.server.description': {
    'pt-BR': 'Servidor Modbus TCP que expõe TAGs canônicos como Holding Registers.',
    en: 'Modbus TCP server exposing canonical TAGs as Holding Registers.',
    es: 'Servidor Modbus TCP que expone TAGs canónicos como Holding Registers.'
  },
  'driver.modbus.rtu.server.displayName': {
    'pt-BR': 'Modbus RTU Servidor', en: 'Modbus RTU Server', es: 'Modbus RTU Servidor'
  },
  'driver.modbus.rtu.server.description': {
    'pt-BR': 'Servidor Modbus RTU sobre porta serial exclusiva do servidor.',
    en: 'Modbus RTU server over an exclusive server serial port.',
    es: 'Servidor Modbus RTU sobre un puerto serie exclusivo del servidor.'
  },
  'driver.modbus.rtu.datasource.serialPort.label': {
    'pt-BR': 'Porta serial no servidor', en: 'Server serial port', es: 'Puerto serie del servidor'
  },
  'driver.modbus.rtu.datasource.serialPort.description': {
    'pt-BR': 'Dispositivo serial visível ao processo API/DriverHost. Pode ser configurado mesmo estando offline.',
    en: 'Serial device visible to the API/DriverHost process. It may be configured while offline.',
    es: 'Dispositivo serie visible para el proceso API/DriverHost. Puede configurarse aun estando desconectado.'
  },
  'driver.modbus.rtu.server.datasource.serialPort.label': {
    'pt-BR': 'Porta serial no servidor', en: 'Server serial port', es: 'Puerto serie del servidor'
  },
  'driver.modbus.rtu.server.datasource.serialPort.description': {
    'pt-BR': 'Porta serial exclusiva do RTU Server enquanto o Runtime estiver ativo.',
    en: 'Serial port exclusively owned by the RTU Server while Runtime is active.',
    es: 'Puerto serie de uso exclusivo del RTU Server mientras el Runtime está activo.'
  },
  'driver.modbus.rtu.datasource.baudRate.label': {
    'pt-BR': 'Baud rate', en: 'Baud rate', es: 'Velocidad en baudios'
  },
  'driver.modbus.rtu.datasource.baudRate.description': {
    'pt-BR': 'Velocidade física da linha serial.', en: 'Physical serial line speed.', es: 'Velocidad física de la línea serie.'
  },
  'driver.modbus.rtu.datasource.dataBits.label': {
    'pt-BR': 'Bits de dados', en: 'Data bits', es: 'Bits de datos'
  },
  'driver.modbus.rtu.datasource.dataBits.description': {
    'pt-BR': 'Quantidade de bits de dados da linha serial.', en: 'Number of serial data bits.', es: 'Cantidad de bits de datos de la línea serie.'
  },
  'driver.modbus.rtu.datasource.parity.label': {
    'pt-BR': 'Paridade', en: 'Parity', es: 'Paridad'
  },
  'driver.modbus.rtu.datasource.parity.description': {
    'pt-BR': 'Paridade da linha serial.', en: 'Serial line parity.', es: 'Paridad de la línea serie.'
  },
  'driver.modbus.rtu.datasource.stopBits.label': {
    'pt-BR': 'Stop bits', en: 'Stop bits', es: 'Bits de parada'
  },
  'driver.modbus.rtu.datasource.stopBits.description': {
    'pt-BR': 'Quantidade de stop bits da linha serial.', en: 'Serial line stop bits.', es: 'Bits de parada de la línea serie.'
  },
  'driver.modbus.rtu.datasource.scanIntervalMilliseconds.label': {
    'pt-BR': 'Intervalo de varredura (ms)', en: 'Scan interval (ms)', es: 'Intervalo de sondeo (ms)'
  },
  'driver.modbus.rtu.datasource.scanIntervalMilliseconds.description': {
    'pt-BR': 'Intervalo de polling do RTU Master.', en: 'RTU Master polling interval.', es: 'Intervalo de sondeo del RTU Maestro.'
  },
  'driver.modbus.rtu.datasource.requestTimeoutMilliseconds.label': {
    'pt-BR': 'Timeout da requisição (ms)', en: 'Request timeout (ms)', es: 'Timeout de la solicitud (ms)'
  },
  'driver.modbus.rtu.datasource.requestTimeoutMilliseconds.description': {
    'pt-BR': 'Tempo máximo para receber uma resposta RTU.', en: 'Maximum time to receive one RTU response.', es: 'Tiempo máximo para recibir una respuesta RTU.'
  },
  'driver.modbus.rtu.datasource.maxGapElements.label': {
    'pt-BR': 'Lacuna máxima do bloco', en: 'Maximum block gap', es: 'Separación máxima del bloque'
  },
  'driver.modbus.rtu.datasource.maxGapElements.description': {
    'pt-BR': 'Maior lacuna agregada em um bloco de polling.', en: 'Maximum gap merged into one polling block.', es: 'Separación máxima agrupada en un bloque de sondeo.'
  },
  'driver.modbus.rtu.datasource.unitId.label': {
    'pt-BR': 'Unit ID padrão', en: 'Default Unit ID', es: 'Unit ID predeterminado'
  },
  'driver.modbus.rtu.datasource.unitId.description': {
    'pt-BR': 'Endereço RTU padrão usado quando a TAG não sobrescreve o Unit ID.', en: 'Default RTU address when a TAG does not override Unit ID.', es: 'Dirección RTU predeterminada cuando la TAG no sobrescribe Unit ID.'
  },
  'driver.modbus.tcp.server.datasource.bindAddress.label': {
    'pt-BR': 'Endereço de escuta', en: 'Bind address', es: 'Dirección de escucha'
  },
  'driver.modbus.tcp.server.datasource.bindAddress.description': {
    'pt-BR': 'Endereço local do servidor. Use 0.0.0.0 para todas as interfaces IPv4.', en: 'Local server address. Use 0.0.0.0 for all IPv4 interfaces.', es: 'Dirección local del servidor. Use 0.0.0.0 para todas las interfaces IPv4.'
  },
  'driver.modbus.tcp.server.datasource.port.label': {
    'pt-BR': 'Porta de escuta', en: 'Listen port', es: 'Puerto de escucha'
  },
  'driver.modbus.tcp.server.datasource.port.description': {
    'pt-BR': 'Porta TCP pertencente a esta Fonte de dados.', en: 'TCP listen port owned by this Data Source.', es: 'Puerto TCP perteneciente a esta Fuente de datos.'
  },
  'driver.modbus.tcp.server.datasource.unitId.label': {
    'pt-BR': 'Unit ID do servidor', en: 'Server Unit ID', es: 'Unit ID del servidor'
  },
  'driver.modbus.tcp.server.datasource.unitId.description': {
    'pt-BR': 'Unit ID aceito neste endpoint TCP.', en: 'Unit ID accepted on this TCP endpoint.', es: 'Unit ID aceptado en este endpoint TCP.'
  },
  'driver.modbus.rtu.server.datasource.unitId.label': {
    'pt-BR': 'Unit ID do servidor', en: 'Server Unit ID', es: 'Unit ID del servidor'
  },
  'driver.modbus.rtu.server.datasource.unitId.description': {
    'pt-BR': 'Endereço slave servido neste barramento RTU.', en: 'Slave address served on this RTU bus.', es: 'Dirección slave servida en este bus RTU.'
  },
  'driver.modbus.server.datasource.holdingRanges.label': {
    'pt-BR': 'Ranges de Holding Registers', en: 'Holding Register ranges', es: 'Rangos de Holding Registers'
  },
  'driver.modbus.server.datasource.holdingRanges.description': {
    'pt-BR': 'Faixas explícitas de endereços 0-based expostas pelo servidor.', en: 'Explicit 0-based address ranges exposed by the server.', es: 'Rangos explícitos de direcciones base 0 expuestos por el servidor.'
  },
  'driver.modbus.server.tag.clientAccess.label': {
    'pt-BR': 'Acesso do cliente externo', en: 'External client access', es: 'Acceso del cliente externo'
  },
  'driver.modbus.server.tag.clientAccess.description': {
    'pt-BR': 'Define se clientes Modbus externos podem escrever esta TAG; escritas internas permanecem separadas.',
    en: 'Controls whether external Modbus clients may write this TAG; internal writes remain separate.',
    es: 'Controla si clientes Modbus externos pueden escribir esta TAG; las escrituras internas permanecen separadas.'
  },

  'driver.opcua.datasource.endpointUrl.label': {
    'pt-BR': 'URL do endpoint', en: 'Endpoint URL', es: 'URL del endpoint'
  },
  'driver.opcua.datasource.securityMode.label': {
    'pt-BR': 'Modo de segurança', en: 'Security mode', es: 'Modo de seguridad'
  },
  'driver.opcua.datasource.securityPolicyUri.label': {
    'pt-BR': 'URI da política de segurança', en: 'Security policy URI', es: 'URI de la política de seguridad'
  },
  'driver.opcua.datasource.serverApplicationUri.label': {
    'pt-BR': 'ApplicationUri aprovada do servidor', en: 'Approved server ApplicationUri', es: 'ApplicationUri aprobada del servidor'
  },
  'driver.opcua.datasource.serverCertificateSha256.label': {
    'pt-BR': 'SHA-256 aprovado do certificado do servidor', en: 'Approved server certificate SHA-256', es: 'SHA-256 aprobado del certificado del servidor'
  },
  'driver.opcua.datasource.authenticationMode.label': {
    'pt-BR': 'Modo de autenticação', en: 'Authentication mode', es: 'Modo de autenticación'
  },
  'driver.opcua.datasource.userName.label': {
    'pt-BR': 'Nome de usuário', en: 'User name', es: 'Nombre de usuario'
  },
  'driver.opcua.datasource.passwordSecretReference.label': {
    'pt-BR': 'Referência protegida da senha', en: 'Password secret reference', es: 'Referencia protegida de la contraseña'
  },
  'driver.opcua.datasource.clientCertificateReference.label': {
    'pt-BR': 'Referência do certificado cliente', en: 'Client certificate reference', es: 'Referencia del certificado cliente'
  },
  'driver.opcua.datasource.userCertificateReference.label': {
    'pt-BR': 'Referência do certificado do usuário', en: 'User certificate reference', es: 'Referencia del certificado del usuario'
  },
  'driver.opcua.datasource.sessionTimeout.label': {
    'pt-BR': 'Timeout da sessão', en: 'Session timeout', es: 'Timeout de la sesión'
  },
  'driver.opcua.datasource.publishingInterval.label': {
    'pt-BR': 'Intervalo de publicação', en: 'Publishing interval', es: 'Intervalo de publicación'
  },
  'driver.opcua.datasource.trustUntrustedServerCertificateForSession.label': {
    'pt-BR': 'Permitir certificado não confiável na sessão temporária de Engenharia',
    en: 'Allow untrusted certificate for temporary Engineering session',
    es: 'Permitir certificado no confiable en la sesión temporal de Ingeniería'
  }
};

export function resolveDriverCatalogResource(
  locale: EngineeringLocale,
  resourceKey: string | null | undefined,
  fallback: string | null | undefined
): string {
  const normalized = resourceKey?.trim();
  if (normalized) {
    const resource = resources[normalized];
    if (resource) return resource[locale] ?? resource['pt-BR'];
  }
  return fallback ?? '';
}

export function hasDriverCatalogResource(resourceKey: string | null | undefined): boolean {
  const normalized = resourceKey?.trim();
  return Boolean(normalized && resources[normalized]);
}
