import type { EngineeringLocale } from './i18n';

type Copy = Readonly<{
  title: string;
  help: string;
  discoveryUrl: string;
  discoveryUrlHelp: string;
  discover: string;
  discovering: string;
  test: string;
  testing: string;
  keyRequired: string;
  discoveryUrlRequired: string;
  noCandidates: string;
  candidates: string;
  useCandidate: string;
  selected: string;
  connectionOk: string;
  connectionFailed: string;
  securityMode: string;
  securityPolicy: string;
  authentication: string;
  certificateFingerprint: string;
  trustNote: string;
  catalogMismatch: string;
}>;

const resources: Readonly<Record<EngineeringLocale, Copy>> = {
  'pt-BR': {
    title: 'Descoberta e teste OPC UA',
    help: 'Descubra endpoints a partir de uma URL de discovery, escolha a configuração sugerida e teste o draft antes do Preview/Apply.',
    discoveryUrl: 'URL de discovery',
    discoveryUrlHelp: 'Exemplo: opc.tcp://servidor:4840. Este valor é apenas a semente da descoberta e não é persistido automaticamente.',
    discover: 'Descobrir endpoints',
    discovering: 'Descobrindo…',
    test: 'Testar configuração do draft',
    testing: 'Testando…',
    keyRequired: 'Informe a Chave da Data Source antes de usar discovery/teste.',
    discoveryUrlRequired: 'Informe uma URL de discovery OPC UA.',
    noCandidates: 'Nenhum endpoint OPC UA foi retornado.',
    candidates: 'Endpoints encontrados',
    useCandidate: 'Usar endpoint e segurança',
    selected: 'Configuração sugerida copiada para o draft. Revise os campos e teste antes de aplicar.',
    connectionOk: 'Teste de conexão do draft concluído',
    connectionFailed: 'Teste de conexão do draft falhou',
    securityMode: 'Modo de segurança',
    securityPolicy: 'Política de segurança',
    authentication: 'Autenticação sugerida',
    certificateFingerprint: 'SHA-256 do certificado do servidor',
    trustNote: 'Escolher um endpoint pode copiar o fingerprint SHA-256 observado para o draft. Isso não altera a trust store nem o Runtime; Preview/Apply continua obrigatório.',
    catalogMismatch: 'O endpoint retornou uma configuração que não pertence ao schema atual da Data Source e ela foi ignorada.'
  },
  en: {
    title: 'OPC UA discovery and test',
    help: 'Discover endpoints from a discovery URL, choose the suggested configuration and test the draft before Preview/Apply.',
    discoveryUrl: 'Discovery URL',
    discoveryUrlHelp: 'Example: opc.tcp://server:4840. This value is only the discovery seed and is not persisted automatically.',
    discover: 'Discover endpoints',
    discovering: 'Discovering…',
    test: 'Test draft configuration',
    testing: 'Testing…',
    keyRequired: 'Enter the Data Source Key before using discovery/test.',
    discoveryUrlRequired: 'Enter an OPC UA discovery URL.',
    noCandidates: 'No OPC UA endpoints were returned.',
    candidates: 'Discovered endpoints',
    useCandidate: 'Use endpoint and security',
    selected: 'Suggested configuration copied to the draft. Review the fields and test before applying.',
    connectionOk: 'Draft connection test succeeded',
    connectionFailed: 'Draft connection test failed',
    securityMode: 'Security mode',
    securityPolicy: 'Security policy',
    authentication: 'Suggested authentication',
    certificateFingerprint: 'Server certificate SHA-256',
    trustNote: 'Choosing an endpoint may copy the observed SHA-256 fingerprint into the draft. It does not change the trust store or Runtime; Preview/Apply is still required.',
    catalogMismatch: 'The endpoint returned a setting outside the current Data Source schema and it was ignored.'
  },
  es: {
    title: 'Descubrimiento y prueba OPC UA',
    help: 'Descubra endpoints desde una URL de discovery, elija la configuración sugerida y pruebe el borrador antes de Preview/Apply.',
    discoveryUrl: 'URL de discovery',
    discoveryUrlHelp: 'Ejemplo: opc.tcp://servidor:4840. Este valor es solo la semilla del descubrimiento y no se persiste automáticamente.',
    discover: 'Descubrir endpoints',
    discovering: 'Descubriendo…',
    test: 'Probar configuración del borrador',
    testing: 'Probando…',
    keyRequired: 'Ingrese la Clave de la Data Source antes de usar discovery/prueba.',
    discoveryUrlRequired: 'Ingrese una URL de discovery OPC UA.',
    noCandidates: 'No se devolvieron endpoints OPC UA.',
    candidates: 'Endpoints encontrados',
    useCandidate: 'Usar endpoint y seguridad',
    selected: 'La configuración sugerida fue copiada al borrador. Revise los campos y pruebe antes de aplicar.',
    connectionOk: 'Prueba de conexión del borrador correcta',
    connectionFailed: 'Prueba de conexión del borrador fallida',
    securityMode: 'Modo de seguridad',
    securityPolicy: 'Política de seguridad',
    authentication: 'Autenticación sugerida',
    certificateFingerprint: 'SHA-256 del certificado del servidor',
    trustNote: 'Elegir un endpoint puede copiar el fingerprint SHA-256 observado al borrador. No modifica la trust store ni Runtime; Preview/Apply sigue siendo obligatorio.',
    catalogMismatch: 'El endpoint devolvió una configuración fuera del schema actual de la Data Source y fue ignorada.'
  }
};

export function c04DataSourceToolingText(locale: EngineeringLocale): Copy {
  return resources[locale] ?? resources['pt-BR'];
}
