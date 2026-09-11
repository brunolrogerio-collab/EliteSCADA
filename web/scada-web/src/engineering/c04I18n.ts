import type { EngineeringLocale } from './i18n';

export type C04Text = Readonly<{
  tagSource: Readonly<{
    label: string;
    search: string;
    none: string;
    legacy: string;
    unresolved: string;
    empty: string;
  }>;
  address: Readonly<{
    address: string;
    manualHelp: string;
    modbusManualHelp: string;
    opcUaManualHelp: string;
    dnp3ManualHelp: string;
    iec104ManualHelp: string;
    modbusTitle: string;
    modbusHelp: string;
    area: string;
    reference: string;
    referenceBase: string;
    zeroBased: string;
    oneBased: string;
    unitId: string;
    valueType: string;
    wordOrder: string;
    scale: string;
    offset: string;
    bit: string;
    auto: string;
    defaultValue: string;
    build: string;
    building: string;
    canonical: string;
    readOnlyWarning: string;
    integerRequired: string;
    integerInvalid: string;
    numberInvalid: string;
  }>;
  generic: Readonly<{
    title: string;
    help: string;
    loading: string;
    apply: string;
    addressRequired: string;
    schemaUnavailable: string;
    required: string;
    integer: string;
    number: string;
    enumValue: string;
    protectedMaterial: string;
    protectedMaterialHint: string;
  }>;
  dnp3: Readonly<{
    title: string;
    help: string;
    kind: string;
    index: string;
    writable: string;
    apply: string;
    applying: string;
    indexInvalid: string;
    schemaUnavailable: string;
    catalogMismatch: string;
  }>;
  iec104: Readonly<{
    title: string;
    help: string;
    commonAddress: string;
    ioa: string;
    typeId: string;
    writable: string;
    commandType: string;
    commandMode: string;
    qualifier: string;
    apply: string;
    applying: string;
    commonAddressInvalid: string;
    ioaInvalid: string;
    typeInvalid: string;
    commandTypeInvalid: string;
    qualifierInvalid: string;
    schemaUnavailable: string;
    catalogMismatch: string;
  }>;
  opcUa: Readonly<{
    title: string;
    help: string;
    test: string;
    testing: string;
    discover: string;
    discovering: string;
    browse: string;
    browsing: string;
    connectionOk: string;
    connectionFailed: string;
    objects: string;
    back: string;
    nodes: string;
    search: string;
    searchPlaceholder: string;
    open: string;
    useCurrent: string;
    loadMore: string;
    bulkTitle: string;
    selected: string;
    pathPrefix: string;
    preview: string;
    previewing: string;
    apply: string;
    applying: string;
    previewResult: string;
    create: string;
    update: string;
    errors: string;
    applyConfirm: string;
    stableIdRequired: string;
    schemaMissing: string;
    readWrite: string;
    readOnly: string;
    noAccess: string;
    unknownType: string;
    bulkStableIdRequired: string;
    bulkSchemaUnavailable: string;
    bulkSelectionRequired: string;
    pathPrefixRequired: string;
    uniquePathFailed: string;
  }>;
}>;

const ptBR: C04Text = {
  tagSource: {
    label: 'Data Source',
    search: 'Pesquisar Data Sources configurados',
    none: 'Sem Data Source',
    legacy: 'Referência legada por chave. Preview/Apply migrará para a identidade estável do Source.',
    unresolved: 'Referência de Source inválida',
    empty: 'Nenhum Data Source está configurado no projeto Working.'
  },
  address: {
    address: 'Endereço',
    manualHelp: 'Use o formato de endereço portátil exigido pelo Driver selecionado.',
    modbusManualHelp: "A sintaxe manual canônica é área:offset-base-0, por exemplo 'holding:0'.",
    opcUaManualHelp: "OPC UA manual aceita o endereço portátil canônico, por exemplo 'node=ns%3D2%3Bs%3DTemperature'. O NodeId cru legado continua disponível para migração.",
    dnp3ManualHelp: "A sintaxe DNP3 canônica é 'dnp3:<pointKind>:<index>', por exemplo 'dnp3:analogInput:0'.",
    iec104ManualHelp: "A identidade IEC-104 canônica é 'ca=<0..65535>;ioa=<0..16777215>'. O assistente também configura o Type ID obrigatório.",
    modbusTitle: 'Assistente de endereço Modbus',
    modbusHelp: 'Monta o mesmo endereço canônico consumido pelo Runtime. A base é explícita e nenhuma notação 40001 é adivinhada.',
    area: 'Área de dados', reference: 'Referência', referenceBase: 'Base da referência', zeroBased: 'Offset base 0', oneBased: 'Referência base 1',
    unitId: 'Override de Unit ID', valueType: 'Tipo do valor', wordOrder: 'Ordem de words', scale: 'Escala', offset: 'Offset', bit: 'Índice do bit',
    auto: 'Inferir pelo TAG', defaultValue: 'Padrão do Driver', build: 'Usar endereço assistido', building: 'Montando...', canonical: 'Endereço canônico',
    readOnlyWarning: 'Esta área Modbus é somente leitura. Marque o TAG como read-only antes do Preview/Apply.',
    integerRequired: 'valor inteiro obrigatório', integerInvalid: 'valor inteiro inválido', numberInvalid: 'valor numérico inválido'
  },
  generic: {
    title: 'Configuração do binding do Driver',
    help: 'Os campos vêm do catálogo backend do Driver. O Endereço portátil manual continua sendo a identidade.',
    loading: 'Carregando schema de binding do Driver…',
    apply: 'Usar configurações de binding',
    addressRequired: 'Informe um Endereço portátil antes de aplicar as configurações do binding.',
    schemaUnavailable: 'O schema backend de TAG binding não está disponível.',
    required: 'valor obrigatório', integer: 'inteiro inválido', number: 'número inválido', enumValue: 'valor não suportado',
    protectedMaterial: 'material protegido pertence a secretReferences da Data Source',
    protectedMaterialHint: 'Material protegido deve permanecer no limite secretReferences da Data Source.'
  },
  dnp3: {
    title: 'Assistente de endereço DNP3', help: 'Monta a identidade canônica do ponto DNP3 usada pelo Runtime. Variações avançadas e ajustes de comando permanecem no binding canônico.',
    kind: 'Tipo de ponto', index: 'Índice do ponto', writable: 'Saída gravável', apply: 'Usar endereço assistido', applying: 'Aplicando...',
    indexInvalid: 'O índice DNP3 deve ser inteiro entre 0 e 65535.', schemaUnavailable: 'O schema de binding DNP3 não está disponível.',
    catalogMismatch: 'A opção selecionada não é mais permitida pelo catálogo DNP3 atual.'
  },
  iec104: {
    title: 'Assistente de endereço IEC-104', help: 'Monta a identidade canônica CA/IOA e o Type ID monitorado exigido pelo Runtime. Pontos graváveis exigem perfil de comando compatível explícito.',
    commonAddress: 'Common Address', ioa: 'Information Object Address (IOA)', typeId: 'Type ID monitorado', writable: 'Ponto de comando gravável',
    commandType: 'Command Type ID', commandMode: 'Modo de comando', qualifier: 'Qualifier (QOC)', apply: 'Usar endereço assistido', applying: 'Aplicando...',
    commonAddressInvalid: 'Common Address deve ser inteiro entre 0 e 65535.', ioaInvalid: 'IOA deve ser inteiro entre 0 e 16777215.',
    typeInvalid: 'Selecione um Type ID IEC-104 monitorado suportado.', commandTypeInvalid: 'O Command Type ID deve usar o mesmo tipo canônico de TAG do Type ID monitorado.',
    qualifierInvalid: 'O qualifier deve ser inteiro no intervalo anunciado pelo Driver.', schemaUnavailable: 'O schema de binding IEC-104 não está disponível.',
    catalogMismatch: 'A opção selecionada não é mais permitida pelo catálogo IEC-104 atual.'
  },
  opcUa: {
    title: 'Ferramentas OPC UA de Engineering', help: 'Teste a fonte configurada, descubra endpoints e navegue pelos nós sem alterar o Runtime. Os nós selecionados só viram TAGs por Preview/Apply.',
    test: 'Testar conexão', testing: 'Testando…', discover: 'Descobrir endpoints', discovering: 'Descobrindo…', browse: 'Navegar Objects', browsing: 'Navegando…',
    connectionOk: 'Teste de conexão concluído', connectionFailed: 'Teste de conexão falhou', objects: 'Objects', back: 'Voltar', nodes: 'nós',
    search: 'Pesquisar nós carregados', searchPlaceholder: 'Nome, NodeId ou endereço portátil', open: 'Abrir', useCurrent: 'Usar no TAG atual', loadMore: 'Carregar mais',
    bulkTitle: 'Criar TAGs dos nós selecionados', selected: 'selecionados', pathPrefix: 'Prefixo do path dos TAGs', preview: 'Pré-visualizar importação', previewing: 'Validando…',
    apply: 'Aplicar importação', applying: 'Aplicando…', previewResult: 'Preview', create: 'criar', update: 'atualizar', errors: 'erros',
    applyConfirm: 'Aplicar a importação OPC UA validada ao workspace de Engineering?',
    stableIdRequired: 'Salve/aplique primeiro este Data Source para obter um Id estável antes de usar as ferramentas OPC UA.',
    schemaMissing: 'O schema OPC UA autoritativo do backend não está disponível.', readWrite: 'leitura/escrita', readOnly: 'somente leitura', noAccess: 'sem leitura', unknownType: 'tipo desconhecido',
    bulkStableIdRequired: 'A importação OPC UA exige um Id estável da Data Source.', bulkSchemaUnavailable: 'O schema de binding OPC UA não está disponível.',
    bulkSelectionRequired: 'Selecione ao menos uma variável navegável com endereço portátil.', pathPrefixRequired: 'Informe um prefixo de path para os TAGs.',
    uniquePathFailed: 'Não foi possível criar um path único para o TAG.'
  }
};

const en: C04Text = {
  tagSource: {
    label: 'Data Source', search: 'Search configured Data Sources', none: 'No Data Source',
    legacy: 'Legacy key reference. Preview/Apply will migrate it to stable Source identity.', unresolved: 'Invalid Source reference',
    empty: 'No Data Sources are configured in the Working project.'
  },
  address: {
    address: 'Address', manualHelp: 'Use the portable address format required by the selected Driver.',
    modbusManualHelp: "Canonical manual syntax is area:0-based-offset, for example 'holding:0'.",
    opcUaManualHelp: "Manual OPC UA accepts the canonical portable address, for example 'node=ns%3D2%3Bs%3DTemperature'. Legacy raw NodeId remains available for migration.",
    dnp3ManualHelp: "Canonical DNP3 syntax is 'dnp3:<pointKind>:<index>', for example 'dnp3:analogInput:0'.",
    iec104ManualHelp: "Canonical IEC-104 identity is 'ca=<0..65535>;ioa=<0..16777215>'. The assistant also authors the required Type ID binding.",
    modbusTitle: 'Modbus address assistant', modbusHelp: 'Build the same canonical address consumed by Runtime. Reference base is explicit; no 40001-style guessing is performed.',
    area: 'Data area', reference: 'Reference', referenceBase: 'Reference base', zeroBased: '0-based offset', oneBased: '1-based reference',
    unitId: 'Unit ID override', valueType: 'Value type', wordOrder: 'Word order', scale: 'Scale', offset: 'Offset', bit: 'Bit index',
    auto: 'Infer from TAG', defaultValue: 'Driver default', build: 'Use assisted address', building: 'Building...', canonical: 'Canonical address',
    readOnlyWarning: 'This Modbus area is read-only. Mark the TAG as read-only before Preview/Apply.',
    integerRequired: 'integer value required', integerInvalid: 'invalid integer value', numberInvalid: 'invalid numeric value'
  },
  generic: {
    title: 'Driver binding settings', help: 'Fields come from the backend Driver catalog. The manual portable Address remains the identity boundary.',
    loading: 'Loading Driver binding schema…', apply: 'Use binding settings', addressRequired: 'Enter a portable Address before applying Driver binding settings.',
    schemaUnavailable: 'The backend TAG binding schema is unavailable.', required: 'required value', integer: 'invalid integer', number: 'invalid number', enumValue: 'unsupported value',
    protectedMaterial: 'protected material belongs to Data Source secretReferences', protectedMaterialHint: 'Protected material must remain on the Data Source secretReferences boundary.'
  },
  dnp3: {
    title: 'DNP3 address assistant', help: 'Builds the canonical DNP3 point identity used by Runtime. Advanced variations and command tuning remain editable through the canonical binding contract.',
    kind: 'Point kind', index: 'Point index', writable: 'Writable output', apply: 'Use assisted address', applying: 'Applying...',
    indexInvalid: 'DNP3 point index must be an integer from 0 to 65535.', schemaUnavailable: 'The DNP3 binding schema is unavailable.',
    catalogMismatch: 'The selected value is no longer allowed by the current DNP3 catalog.'
  },
  iec104: {
    title: 'IEC-104 address assistant', help: 'Builds canonical CA/IOA identity plus the monitored Type ID required by Runtime. Writable points require an explicit compatible command profile.',
    commonAddress: 'Common Address', ioa: 'Information Object Address (IOA)', typeId: 'Monitored Type ID', writable: 'Writable command point', commandType: 'Command Type ID',
    commandMode: 'Command mode', qualifier: 'Qualifier (QOC)', apply: 'Use assisted address', applying: 'Applying...',
    commonAddressInvalid: 'Common Address must be an integer from 0 to 65535.', ioaInvalid: 'IOA must be an integer from 0 to 16777215.',
    typeInvalid: 'Select a supported monitored IEC-104 Type ID.', commandTypeInvalid: 'Command Type ID must use the same canonical TAG data type as the monitored Type ID.',
    qualifierInvalid: 'Command qualifier must be an integer within the Driver-advertised range.', schemaUnavailable: 'The IEC-104 binding schema is unavailable.',
    catalogMismatch: 'The selected value is no longer allowed by the current IEC-104 catalog.'
  },
  opcUa: {
    title: 'OPC UA Engineering tools', help: 'Test the configured source, discover endpoints and browse nodes without changing Runtime. Selected nodes become TAGs only through Preview/Apply.',
    test: 'Test connection', testing: 'Testing…', discover: 'Discover endpoints', discovering: 'Discovering…', browse: 'Browse Objects', browsing: 'Browsing…',
    connectionOk: 'Connection test succeeded', connectionFailed: 'Connection test failed', objects: 'Objects', back: 'Back', nodes: 'nodes', search: 'Search loaded nodes',
    searchPlaceholder: 'Name, NodeId or portable address', open: 'Open', useCurrent: 'Use for current TAG', loadMore: 'Load more',
    bulkTitle: 'Create TAGs from selected nodes', selected: 'selected', pathPrefix: 'TAG path prefix', preview: 'Preview import', previewing: 'Previewing…', apply: 'Apply import', applying: 'Applying…',
    previewResult: 'Preview', create: 'create', update: 'update', errors: 'errors', applyConfirm: 'Apply the previewed OPC UA TAG import to the Engineering workspace?',
    stableIdRequired: 'Save/Apply this Data Source first so it has a stable Id before using OPC UA Engineering tools.', schemaMissing: 'The backend-authoritative OPC UA binding schema is unavailable.',
    readWrite: 'read/write', readOnly: 'read-only', noAccess: 'no read access', unknownType: 'unknown type',
    bulkStableIdRequired: 'OPC UA bulk import requires a stable Data Source Id.', bulkSchemaUnavailable: 'The OPC UA Driver binding schema is unavailable.',
    bulkSelectionRequired: 'Select at least one browse variable with a portable address.', pathPrefixRequired: 'A TAG path prefix is required.',
    uniquePathFailed: 'Could not create a unique TAG path.'
  }
};

const es: C04Text = {
  tagSource: {
    label: 'Data Source', search: 'Buscar Data Sources configurados', none: 'Sin Data Source',
    legacy: 'Referencia heredada por clave. Preview/Apply la migrará a la identidad estable del Source.', unresolved: 'Referencia de Source inválida',
    empty: 'No hay Data Sources configurados en el proyecto Working.'
  },
  address: {
    address: 'Dirección', manualHelp: 'Use el formato de dirección portátil requerido por el Driver seleccionado.',
    modbusManualHelp: "La sintaxis manual canónica es área:offset-base-0, por ejemplo 'holding:0'.",
    opcUaManualHelp: "OPC UA manual acepta la dirección portátil canónica, por ejemplo 'node=ns%3D2%3Bs%3DTemperature'. El NodeId crudo legado sigue disponible para migración.",
    dnp3ManualHelp: "La sintaxis DNP3 canónica es 'dnp3:<pointKind>:<index>', por ejemplo 'dnp3:analogInput:0'.",
    iec104ManualHelp: "La identidad IEC-104 canónica es 'ca=<0..65535>;ioa=<0..16777215>'. El asistente también configura el Type ID requerido.",
    modbusTitle: 'Asistente de dirección Modbus', modbusHelp: 'Construye la misma dirección canónica consumida por Runtime. La base es explícita y no se adivina la notación 40001.',
    area: 'Área de datos', reference: 'Referencia', referenceBase: 'Base de referencia', zeroBased: 'Offset base 0', oneBased: 'Referencia base 1',
    unitId: 'Override Unit ID', valueType: 'Tipo de valor', wordOrder: 'Orden de palabras', scale: 'Escala', offset: 'Offset', bit: 'Índice de bit',
    auto: 'Inferir del TAG', defaultValue: 'Default del Driver', build: 'Usar dirección asistida', building: 'Construyendo...', canonical: 'Dirección canónica',
    readOnlyWarning: 'Esta área Modbus es de solo lectura. Marque el TAG como read-only antes de Preview/Apply.',
    integerRequired: 'se requiere un valor entero', integerInvalid: 'valor entero inválido', numberInvalid: 'valor numérico inválido'
  },
  generic: {
    title: 'Configuración del binding del Driver', help: 'Los campos provienen del catálogo backend del Driver. La dirección portátil manual sigue siendo la identidad.',
    loading: 'Cargando schema de binding del Driver…', apply: 'Usar configuración de binding', addressRequired: 'Ingrese una dirección portátil antes de aplicar el binding.',
    schemaUnavailable: 'El schema backend de TAG binding no está disponible.', required: 'valor requerido', integer: 'entero inválido', number: 'número inválido', enumValue: 'valor no soportado',
    protectedMaterial: 'el material protegido pertenece a secretReferences del Data Source', protectedMaterialHint: 'El material protegido debe permanecer en el límite secretReferences del Data Source.'
  },
  dnp3: {
    title: 'Asistente de dirección DNP3', help: 'Construye la identidad canónica del punto DNP3 usada por Runtime. Variaciones avanzadas y comandos permanecen en el binding canónico.',
    kind: 'Tipo de punto', index: 'Índice del punto', writable: 'Salida escribible', apply: 'Usar dirección asistida', applying: 'Aplicando...',
    indexInvalid: 'El índice DNP3 debe ser un entero entre 0 y 65535.', schemaUnavailable: 'El schema de binding DNP3 no está disponible.',
    catalogMismatch: 'La opción seleccionada ya no está permitida por el catálogo DNP3 actual.'
  },
  iec104: {
    title: 'Asistente de dirección IEC-104', help: 'Construye la identidad canónica CA/IOA y el Type ID monitoreado exigido por Runtime. Los puntos escribibles requieren un perfil de comando compatible explícito.',
    commonAddress: 'Common Address', ioa: 'Information Object Address (IOA)', typeId: 'Type ID monitoreado', writable: 'Punto de comando escribible', commandType: 'Command Type ID',
    commandMode: 'Modo de comando', qualifier: 'Qualifier (QOC)', apply: 'Usar dirección asistida', applying: 'Aplicando...',
    commonAddressInvalid: 'Common Address debe ser un entero entre 0 y 65535.', ioaInvalid: 'IOA debe ser un entero entre 0 y 16777215.',
    typeInvalid: 'Seleccione un Type ID IEC-104 monitoreado compatible.', commandTypeInvalid: 'Command Type ID debe usar el mismo tipo canónico de TAG que el Type ID monitoreado.',
    qualifierInvalid: 'El qualifier debe ser un entero dentro del rango anunciado por el Driver.', schemaUnavailable: 'El schema de binding IEC-104 no está disponible.',
    catalogMismatch: 'La opción seleccionada ya no está permitida por el catálogo IEC-104 actual.'
  },
  opcUa: {
    title: 'Herramientas OPC UA de Engineering', help: 'Prueba la fuente configurada, descubre endpoints y navega nodos sin cambiar Runtime. Los nodos seleccionados se vuelven TAGs solo mediante Preview/Apply.',
    test: 'Probar conexión', testing: 'Probando…', discover: 'Descubrir endpoints', discovering: 'Descubriendo…', browse: 'Navegar Objects', browsing: 'Navegando…',
    connectionOk: 'Prueba de conexión correcta', connectionFailed: 'Prueba de conexión fallida', objects: 'Objects', back: 'Volver', nodes: 'nodos', search: 'Buscar nodos cargados',
    searchPlaceholder: 'Nombre, NodeId o dirección portátil', open: 'Abrir', useCurrent: 'Usar en el TAG actual', loadMore: 'Cargar más',
    bulkTitle: 'Crear TAGs desde nodos seleccionados', selected: 'seleccionados', pathPrefix: 'Prefijo de path de TAG', preview: 'Preview de importación', previewing: 'Validando…', apply: 'Aplicar importación', applying: 'Aplicando…',
    previewResult: 'Preview', create: 'crear', update: 'actualizar', errors: 'errores', applyConfirm: '¿Aplicar la importación OPC UA validada al workspace de Engineering?',
    stableIdRequired: 'Guarde/aplique primero este Data Source para obtener un Id estable antes de usar las herramientas OPC UA.', schemaMissing: 'El esquema OPC UA autoritativo del backend no está disponible.',
    readWrite: 'lectura/escritura', readOnly: 'solo lectura', noAccess: 'sin lectura', unknownType: 'tipo desconocido',
    bulkStableIdRequired: 'La importación masiva OPC UA requiere un Id estable del Data Source.', bulkSchemaUnavailable: 'El schema de binding OPC UA no está disponible.',
    bulkSelectionRequired: 'Seleccione al menos una variable navegable con dirección portátil.', pathPrefixRequired: 'Se requiere un prefijo de path para los TAGs.',
    uniquePathFailed: 'No fue posible crear un path único para el TAG.'
  }
};

const resources: Record<EngineeringLocale, C04Text> = {
  'pt-BR': ptBR,
  en,
  es
};

export function c04Text(locale: EngineeringLocale): C04Text {
  return resources[locale] ?? ptBR;
}
