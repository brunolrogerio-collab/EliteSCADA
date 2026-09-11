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
    'pt-BR': 'Identificador Modbus Unit ID padrão da Data Source.', en: 'Default Modbus unit identifier.', es: 'Identificador Unit ID Modbus predeterminado de la Data Source.'
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
    'pt-BR': 'Permitir certificado não confiável na sessão temporária de Engineering',
    en: 'Allow untrusted certificate for temporary Engineering session',
    es: 'Permitir certificado no confiable en la sesión temporal de Engineering'
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
