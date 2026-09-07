using Scada.Api.Security;
using Scada.DriverHost.Engineering;

namespace Scada.Api.Runtime;

public sealed record ContextualHelpSection(
    string Heading,
    string Body,
    string? Code = null);

public sealed record ContextualHelpTopic(
    string Id,
    string Category,
    string Title,
    string Summary,
    IReadOnlyCollection<ContextualHelpSection> Sections);

public sealed record ContextualHelpCatalogView(
    string Locale,
    IReadOnlyCollection<string> SupportedLocales,
    IReadOnlyCollection<ContextualHelpTopic> Topics,
    IReadOnlyCollection<string> ServerScriptApi);

public static class ContextualHelpCatalog
{
    public static readonly IReadOnlyCollection<string> SupportedLocales = new[] { "pt-BR", "en", "es" };

    public static readonly IReadOnlyCollection<string> ServerScriptApiFunctions = new[]
    {
        "read_tag",
        "read_server_memory",
        "write_tag",
        "write_server_memory",
        "publish_server_memory_sample",
        "emit_operational_event"
    };

    public static readonly IReadOnlyCollection<string> RequiredManualTopicIds = new[]
    {
        "getting-started.startup-authentication",
        "runtime.overview",
        "engineering.lifecycle",
        "packages.escadapkg",
        "sources.data-sources",
        "tags.overview",
        "tags.quality",
        "tags.timestamps",
        "tags.writeability",
        "tags.addressing",
        "tags.scaling-formatting",
        "sources.internal-memory",
        "gateway.overview",
        "scripts.server",
        "alarms.overview",
        "operational-events.overview",
        "audit.overview",
        "historian.overview",
        "trends.overview",
        "reports.overview",
        "screens.overview",
        "popups.overview",
        "dynamos.overview",
        "bindings-commands.overview",
        "security.users-roles-capabilities",
        "licensing.overview",
        "recovery.backup-system-recovery",
        "diagnostics.overview",
        "troubleshooting.overview",
        "libraries.reusable-resources"
    };

    private static readonly IReadOnlyCollection<TopicDefinition> ManualTopics = new[]
    {
        Manual(
            "getting-started.startup-authentication", "getting-started",
            Tx("Primeiro startup e autenticação", "First startup and authentication", "Primer inicio y autenticación"),
            Tx("Entrada segura no EliteSCADA e preparação da primeira sessão.", "Secure entry into EliteSCADA and preparation of the first session.", "Entrada segura en EliteSCADA y preparación de la primera sesión."),
            Sec(Tx("Primeiro startup", "First startup", "Primer inicio"), Tx(
                "Inicie o backend e a interface do produto, conclua qualquer bootstrap administrativo solicitado pelo próprio sistema e confirme que os serviços necessários estão saudáveis antes de abrir Engineering ou Runtime. Não contorne o fluxo de bootstrap por arquivos ou chamadas privadas.",
                "Start the product backend and UI, complete any administrative bootstrap requested by the product itself, and confirm the required services are healthy before opening Engineering or Runtime. Do not bypass bootstrap through files or private calls.",
                "Inicie el backend y la interfaz del producto, complete cualquier bootstrap administrativo solicitado por el propio sistema y confirme que los servicios necesarios están saludables antes de abrir Engineering o Runtime. No evite el bootstrap mediante archivos o llamadas privadas.")),
            Sec(Tx("Autenticação", "Authentication", "Autenticación"), Tx(
                "Quando autenticação estiver habilitada, use uma identidade válida. Roles e capabilities efetivas são resolvidas pelo backend; ocultar ou mostrar controles na UI não substitui autorização server-side.",
                "When authentication is enabled, use a valid identity. Effective roles and capabilities are resolved by the backend; hiding or showing UI controls does not replace server-side authorization.",
                "Cuando la autenticación esté habilitada, use una identidad válida. Los roles y capabilities efectivos son resueltos por el backend; ocultar o mostrar controles en la UI no sustituye la autorización server-side."))),

        Manual(
            "runtime.overview", "runtime",
            Tx("Runtime para o operador", "Runtime operator guide", "Guía de Runtime para el operador"),
            Tx("Operação da revisão Active sob a autoridade do backend.", "Operation of the Active revision under backend authority.", "Operación de la revisión Active bajo la autoridad del backend."),
            Sec(Tx("Autoridade", "Authority", "Autoridad"), Tx(
                "O backend e a revisão Active são a autoridade canônica do Runtime. A interface apresenta esse estado e não cria um segundo motor de processo nem ativa alterações de Engineering por conta própria.",
                "The backend and the Active revision are the canonical Runtime authority. The UI presents that state and does not create a second process engine or activate Engineering changes on its own.",
                "El backend y la revisión Active son la autoridad canónica del Runtime. La interfaz presenta ese estado y no crea un segundo motor de proceso ni activa cambios de Engineering por sí misma.")),
            Sec(Tx("Sessão e capabilities", "Session and capabilities", "Sesión y capabilities"), Tx(
                "A sessão recebe capabilities efetivas do servidor. Viewer/Interactive e View Only reduzem o que a sessão pode fazer; comandos e escritas continuam bloqueados no servidor quando a capability correspondente não existe. A perda ou expiração de lease deve ser tratada como perda de autoridade interativa, não como permissão implícita.",
                "The session receives effective capabilities from the server. Viewer/Interactive and View Only reduce what the session may do; commands and writes remain server-blocked when the corresponding capability is absent. Lease loss or expiry must be treated as loss of interactive authority, not as implicit permission.",
                "La sesión recibe capabilities efectivas del servidor. Viewer/Interactive y View Only reducen lo que la sesión puede hacer; comandos y escrituras siguen bloqueados en el servidor cuando falta la capability correspondiente. La pérdida o expiración del lease debe tratarse como pérdida de autoridad interactiva, no como permiso implícito."))),

        Manual(
            "runtime.history", "runtime",
            Tx("Histórico no Runtime", "Runtime history", "Histórico en Runtime"),
            Tx("Consulta de histórico dentro da visibilidade autorizada da sessão.", "Historical query within the session's authorized visibility.", "Consulta histórica dentro de la visibilidad autorizada de la sesión."),
            Sec(Tx("Consulta", "Query", "Consulta"), Tx(
                "Consultas de histórico usam os serviços canônicos do produto e respeitam a visibilidade de TAGs e capabilities da sessão. Uma tela de histórico não concede acesso a dados que a sessão não está autorizada a ler.",
                "Historical queries use canonical product services and respect the session's TAG visibility and capabilities. A history screen does not grant access to data the session is not authorized to read.",
                "Las consultas históricas usan los servicios canónicos del producto y respetan la visibilidad de TAGs y capabilities de la sesión. Una pantalla histórica no concede acceso a datos que la sesión no está autorizada a leer."))),

        Manual(
            "engineering.overview", "engineering",
            Tx("Engineering", "Engineering", "Engineering"),
            Tx("Configuração do projeto sem confundir edição, publicação e ativação.", "Project configuration without confusing editing, publishing and activation.", "Configuración del proyecto sin confundir edición, publicación y activación."),
            Sec(Tx("Estados", "States", "Estados"), Tx(
                "Working, Revision, Published e Active são estados distintos. A UI deve projetar o estado fornecido pelo backend e não inferir que Save, Publish ou Activate aconteceram apenas porque uma edição foi aceita localmente.",
                "Working, Revision, Published and Active are distinct states. The UI must project backend-provided state and must not infer that Save, Publish or Activate happened merely because an edit was accepted locally.",
                "Working, Revision, Published y Active son estados distintos. La UI debe proyectar el estado proporcionado por el backend y no inferir que Save, Publish o Activate ocurrieron solo porque una edición fue aceptada localmente."))),

        Manual(
            "engineering.lifecycle", "engineering",
            Tx("Working -> Save -> Revision -> Publish -> Activate", "Working -> Save -> Revision -> Publish -> Activate", "Working -> Save -> Revision -> Publish -> Activate"),
            Tx("Fluxo canônico de ciclo de vida de uma aplicação.", "Canonical application lifecycle flow.", "Flujo canónico del ciclo de vida de una aplicación."),
            Sec(Tx("Working e Save", "Working and Save", "Working y Save"), Tx(
                "Working é o estado editável. Save persiste o trabalho conforme o contrato de Engineering, mas não transforma silenciosamente esse estado em Published ou Active.",
                "Working is the editable state. Save persists work according to the Engineering contract, but does not silently turn that state into Published or Active.",
                "Working es el estado editable. Save persiste el trabajo según el contrato de Engineering, pero no convierte silenciosamente ese estado en Published o Active.")),
            Sec(Tx("Revision e Publish", "Revision and Publish", "Revision y Publish"), Tx(
                "Crie uma Revision identificável a partir do Working validado. Publish torna a revisão elegível ao fluxo publicado sem alterar por si só a revisão Active que o Runtime executa.",
                "Create an identifiable Revision from validated Working state. Publish makes the revision eligible for the published flow without by itself changing the Active revision executed by Runtime.",
                "Cree una Revision identificable a partir del Working validado. Publish vuelve la revisión elegible para el flujo publicado sin cambiar por sí solo la revisión Active ejecutada por Runtime.")),
            Sec(Tx("Activate", "Activate", "Activate"), Tx(
                "Activate é a transição explícita que muda a autoridade Active do backend. Validação, autorização e contratos de lifecycle continuam sendo obrigatórios; não use atalhos de UI, banco ou package para simular ativação.",
                "Activate is the explicit transition that changes backend Active authority. Validation, authorization and lifecycle contracts remain mandatory; do not use UI, database or package shortcuts to simulate activation.",
                "Activate es la transición explícita que cambia la autoridad Active del backend. Validación, autorización y contratos de lifecycle siguen siendo obligatorios; no use atajos de UI, base de datos o package para simular activación."))),

        Manual(
            "packages.escadapkg", "packages",
            Tx("Pacotes .escadapkg", ".escadapkg packages", "Paquetes .escadapkg"),
            Tx("Portabilidade da aplicação com inspeção e aplicação explícitas.", "Application portability with explicit inspection and apply steps.", "Portabilidad de la aplicación con inspección y aplicación explícitas."),
            Sec(Tx("Export e Import", "Export and Import", "Export e Import"), Tx(
                "Export produz um .escadapkg portátil e self-contained conforme o contrato do projeto. Import recebe o pacote como entrada de Engenharia; receber o arquivo não deve contornar validação, identidade, segurança ou lifecycle.",
                "Export produces a portable, self-contained .escadapkg according to the project contract. Import receives the package as Engineering input; receiving the file must not bypass validation, identity, security or lifecycle.",
                "Export produce un .escadapkg portátil y self-contained según el contrato del proyecto. Import recibe el paquete como entrada de Engineering; recibir el archivo no debe evitar validación, identidad, seguridad ni lifecycle.")),
            Sec(Tx("Inspect e Preview", "Inspect and Preview", "Inspect y Preview"), Tx(
                "Inspect examina conteúdo e metadados antes de mutação. Preview apresenta o resultado previsto sem tornar o conteúdo Active. Use essas etapas para entender impacto e problemas antes de Apply.",
                "Inspect examines content and metadata before mutation. Preview presents the expected result without making content Active. Use these steps to understand impact and issues before Apply.",
                "Inspect examina contenido y metadatos antes de la mutación. Preview presenta el resultado previsto sin volver el contenido Active. Use estas etapas para comprender impacto y problemas antes de Apply.")),
            Sec(Tx("Apply", "Apply", "Apply"), Tx(
                "Apply incorpora o conteúdo validado ao fluxo de Engineering definido pelo produto. Apply não equivale a Activate; a revisão Active continua sob autoridade explícita do backend.",
                "Apply incorporates validated content into the product-defined Engineering flow. Apply is not Activate; the Active revision remains under explicit backend authority.",
                "Apply incorpora el contenido validado al flujo de Engineering definido por el producto. Apply no equivale a Activate; la revisión Active sigue bajo autoridad explícita del backend."))),

        Manual(
            "sources.data-sources", "sources",
            Tx("Data Sources", "Data Sources", "Data Sources"),
            Tx("Fontes declaradas pelo catálogo canônico desta compilação.", "Sources declared by this build's canonical catalog.", "Fuentes declaradas por el catálogo canónico de esta compilación."),
            Sec(Tx("Catálogo", "Catalog", "Catálogo"), Tx(
                "Crie Data Sources apenas com tipos disponíveis no catálogo de Engineering desta compilação. Drivers de comunicação e source providers são conceitos distintos; Simulation não é apresentado como driver de comunicação de produção.",
                "Create Data Sources only with types available in this build's Engineering catalog. Communication drivers and source providers are distinct concepts; Simulation is not presented as a production communication driver.",
                "Cree Data Sources solo con tipos disponibles en el catálogo de Engineering de esta compilación. Drivers de comunicación y source providers son conceptos distintos; Simulation no se presenta como driver de comunicación de producción.")),
            Sec(Tx("Configuração", "Configuration", "Configuración"), Tx(
                "Os campos válidos, obrigatoriedade, formatos, limites e referências protegidas vêm do configuration schema canônico do tipo selecionado. Não copie configurações de outro protocolo nem invente chaves privadas.",
                "Valid fields, requiredness, formats, limits and protected references come from the selected type's canonical configuration schema. Do not copy settings from another protocol or invent private keys.",
                "Los campos válidos, obligatoriedad, formatos, límites y referencias protegidas provienen del configuration schema canónico del tipo seleccionado. No copie configuraciones de otro protocolo ni invente claves privadas."))),

        Manual(
            "tags.overview", "tags",
            Tx("TAGs", "TAGs", "TAGs"),
            Tx("Identidade, associação a fonte e valor operacional dentro do TAG Engine.", "Identity, source association and operational value inside the TAG Engine.", "Identidad, asociación a fuente y valor operacional dentro del TAG Engine."),
            Sec(Tx("Identidade e binding", "Identity and binding", "Identidad y binding"), Tx(
                "Use a identidade estável do TAG e associe-o ao Data Source por meio do binding suportado pelo tipo. A configuração de apresentação não substitui addressing nem altera a identidade canônica.",
                "Use the TAG's stable identity and associate it with the Data Source through the binding supported by that type. Presentation configuration does not replace addressing or change canonical identity.",
                "Use la identidad estable del TAG y asócielo al Data Source mediante el binding soportado por ese tipo. La configuración de presentación no sustituye addressing ni cambia la identidad canónica.")),
            Sec(Tx("Runtime", "Runtime", "Runtime"), Tx(
                "Leituras, escritas, quality e timestamps são projetados pelo Runtime sob a autoridade do backend. Consumidores devem usar os contratos do TAG Engine/cache/eventos em vez de acessar drivers por caminhos privados.",
                "Reads, writes, quality and timestamps are projected by Runtime under backend authority. Consumers must use TAG Engine/cache/event contracts instead of accessing drivers through private paths.",
                "Lecturas, escrituras, quality y timestamps son proyectados por Runtime bajo la autoridad del backend. Los consumidores deben usar los contratos de TAG Engine/cache/eventos en lugar de acceder a drivers por rutas privadas."))),

        Manual(
            "tags.quality", "tags",
            Tx("Quality", "Quality", "Quality"),
            Tx("Estado de confiança que acompanha uma amostra de TAG.", "Trust state that accompanies a TAG sample.", "Estado de confianza que acompaña una muestra de TAG."),
            Sec(Tx("Origem", "Origin", "Origen"), Tx(
                "Quality deve vir do pipeline canônico da fonte/TAG. A UI pode apresentar esse estado, mas não deve transformar falha, ausência ou dado stale em qualidade boa apenas para manter uma tela visualmente estável.",
                "Quality must come from the canonical source/TAG pipeline. The UI may present that state, but must not turn failure, absence or stale data into good quality merely to keep a screen visually stable.",
                "Quality debe provenir del pipeline canónico de la fuente/TAG. La UI puede presentar ese estado, pero no debe convertir falla, ausencia o dato stale en buena calidad solo para mantener una pantalla visualmente estable.")),
            Sec(Tx("Diagnóstico", "Diagnostics", "Diagnóstico"), Tx(
                "Ao investigar quality degradada, verifique primeiro Data Source, conexão, binding/addressing e diagnóstico do driver antes de alterar telas, trends ou relatórios.",
                "When investigating degraded quality, verify Data Source, connection, binding/addressing and driver diagnostics before changing screens, trends or reports.",
                "Al investigar quality degradada, verifique primero Data Source, conexión, binding/addressing y diagnóstico del driver antes de cambiar pantallas, trends o informes."))),

        Manual(
            "tags.timestamps", "tags",
            Tx("Timestamps", "Timestamps", "Timestamps"),
            Tx("Tempo associado às amostras sem substituir autoridade da fonte por horário de tela.", "Sample time without replacing source authority with screen time.", "Tiempo asociado a las muestras sin sustituir autoridad de la fuente por horario de pantalla."),
            Sec(Tx("Semântica", "Semantics", "Semántica"), Tx(
                "Preserve o timestamp fornecido pelo pipeline canônico conforme o contrato da fonte. Não substitua timestamps de protocolo ou Runtime pelo relógio do navegador apenas para exibição ou persistência.",
                "Preserve the timestamp supplied by the canonical pipeline according to the source contract. Do not replace protocol or Runtime timestamps with browser time merely for display or persistence.",
                "Preserve el timestamp entregado por el pipeline canónico según el contrato de la fuente. No sustituya timestamps de protocolo o Runtime por la hora del navegador solo para visualización o persistencia."))),

        Manual(
            "tags.writeability", "tags",
            Tx("Writeability", "Writeability", "Writeability"),
            Tx("Escrita somente quando TAG, sessão e backend permitem.", "Write only when TAG, session and backend allow it.", "Escritura solo cuando TAG, sesión y backend lo permiten."),
            Sec(Tx("Autoridade de escrita", "Write authority", "Autoridad de escritura"), Tx(
                "Um controle visível ou um binding de comando não torna um TAG gravável. A escrita depende do contrato do TAG/fonte e das capabilities efetivas da sessão, com enforcement server-side.",
                "A visible control or command binding does not make a TAG writable. Writing depends on the TAG/source contract and the session's effective capabilities, with server-side enforcement.",
                "Un control visible o un command binding no vuelve un TAG escribible. La escritura depende del contrato del TAG/fuente y de las capabilities efectivas de la sesión, con enforcement server-side.")),
            Sec(Tx("View Only", "View Only", "View Only"), Tx(
                "View Only e sessões sem capability de comando devem permanecer incapazes de escrever mesmo se uma tela antiga ainda contiver um controle interativo.",
                "View Only and sessions without command capability must remain unable to write even if an older screen still contains an interactive control.",
                "View Only y sesiones sin capability de comando deben seguir sin poder escribir aunque una pantalla antigua todavía contenga un control interactivo."))),

        Manual(
            "tags.addressing", "tags",
            Tx("Addressing", "Addressing", "Addressing"),
            Tx("Endereçamento de TAG conforme o binding schema real do driver.", "TAG addressing according to the driver's real binding schema.", "Direccionamiento de TAG según el binding schema real del driver."),
            Sec(Tx("Binding schema", "Binding schema", "Binding schema"), Tx(
                "Use somente campos expostos pelo TagBinding schema do driver selecionado. Nome, tipo, formato e obrigatoriedade desses campos são parte do contrato do build e aparecem também no tópico específico de cada driver.",
                "Use only fields exposed by the selected driver's TagBinding schema. Field name, type, format and requiredness are part of the build contract and also appear in each driver's specific topic.",
                "Use solo campos expuestos por el TagBinding schema del driver seleccionado. Nombre, tipo, formato y obligatoriedad de esos campos forman parte del contrato del build y también aparecen en el tema específico de cada driver.")),
            Sec(Tx("Validação", "Validation", "Validación"), Tx(
                "Não invente sintaxe de endereço a partir de outro SCADA ou de outro protocolo. Corrija o binding na origem quando o backend rejeitar campos ou formatos inválidos.",
                "Do not invent address syntax from another SCADA or another protocol. Correct the binding at its source when the backend rejects invalid fields or formats.",
                "No invente sintaxis de dirección a partir de otro SCADA u otro protocolo. Corrija el binding en su origen cuando el backend rechace campos o formatos inválidos."))),

        Manual(
            "tags.scaling-formatting", "tags",
            Tx("Scaling versus formatação", "Scaling versus presentation formatting", "Scaling versus formato de presentación"),
            Tx("Conversão de valor e aparência visual são responsabilidades diferentes.", "Value conversion and visual appearance are different responsibilities.", "La conversión de valor y la apariencia visual son responsabilidades diferentes."),
            Sec(Tx("Scaling", "Scaling", "Scaling"), Tx(
                "Scaling transforma o valor conforme o contrato de engenharia do TAG antes do consumo operacional. Alterar scaling pode alterar o valor de engenharia usado por lógica, alarmes, histórico e telas.",
                "Scaling transforms the value according to the TAG engineering contract before operational consumption. Changing scaling may change the engineering value used by logic, alarms, history and screens.",
                "Scaling transforma el valor según el contrato de ingeniería del TAG antes del consumo operacional. Cambiar scaling puede cambiar el valor de ingeniería usado por lógica, alarmas, histórico y pantallas.")),
            Sec(Tx("Formatação", "Formatting", "Formato"), Tx(
                "Formatação de apresentação controla como um valor é exibido, por exemplo precisão ou texto visual, sem reescrever o valor canônico do Runtime. Não use formatação como substituto de scaling.",
                "Presentation formatting controls how a value is displayed, for example precision or visual text, without rewriting the canonical Runtime value. Do not use formatting as a substitute for scaling.",
                "El formato de presentación controla cómo se muestra un valor, por ejemplo precisión o texto visual, sin reescribir el valor canónico de Runtime. No use formato como sustituto de scaling."))),

        Manual(
            "sources.internal-memory", "sources",
            Tx("Internal Memory", "Internal Memory", "Internal Memory"),
            Tx("Memória interna é source provider, não driver de comunicação.", "Internal memory is a source provider, not a communication driver.", "La memoria interna es un source provider, no un driver de comunicación."),
            Sec(Tx("Server Memory", "Server Memory", "Server Memory"), Tx(
                "Server Memory é memória retentiva pertencente ao servidor e aparece no catálogo canônico como source provider. Use-a quando o estado precisa pertencer à autoridade do servidor.",
                "Server Memory is retentive server-owned memory and appears in the canonical catalog as a source provider. Use it when state must belong to server authority.",
                "Server Memory es memoria retentiva perteneciente al servidor y aparece en el catálogo canónico como source provider. Úsela cuando el estado deba pertenecer a la autoridad del servidor.")),
            Sec(Tx("Client Memory", "Client Memory", "Client Memory"), Tx(
                "Client Memory é memória não retentiva pertencente ao cliente Runtime e também é source provider. Nenhuma das duas entra na contagem dos drivers de comunicação de produção.",
                "Client Memory is non-retentive Runtime-client-owned memory and is also a source provider. Neither memory source counts as a production communication driver.",
                "Client Memory es memoria no retentiva perteneciente al cliente Runtime y también es source provider. Ninguna de las dos cuenta como driver de comunicación de producción."))),

        Manual(
            "gateway.overview", "gateway",
            Tx("TAG Gateway", "TAG Gateway", "TAG Gateway"),
            Tx("Coordenação de fluxo de TAGs sem se apresentar como driver de comunicação.", "TAG flow coordination without pretending to be a communication driver.", "Coordinación del flujo de TAGs sin presentarse como driver de comunicación."),
            Sec(Tx("Papel", "Role", "Rol"), Tx(
                "TAG Gateway participa da composição e distribuição do fluxo de TAGs conforme os contratos do Runtime. Ele é um conceito separado dos drivers de comunicação e não entra na contagem dos oito drivers de produção.",
                "TAG Gateway participates in TAG-flow composition and distribution according to Runtime contracts. It is distinct from communication drivers and does not count among the eight production drivers.",
                "TAG Gateway participa en la composición y distribución del flujo de TAGs según los contratos de Runtime. Es un concepto separado de los drivers de comunicación y no cuenta entre los ocho drivers de producción.")),
            Sec(Tx("Consumidores", "Consumers", "Consumidores"), Tx(
                "Consumidores usam TAG Engine, cache e eventos públicos do produto. Não crie acesso privado direto ao driver para contornar Gateway, authority ou lifecycle.",
                "Consumers use the product's TAG Engine, cache and public events. Do not create private direct driver access to bypass Gateway, authority or lifecycle.",
                "Los consumidores usan TAG Engine, cache y eventos públicos del producto. No cree acceso privado directo al driver para evitar Gateway, authority o lifecycle."))),

        Manual(
            "scripts.server", "scripts",
            Tx("Server Scripts", "Server Scripts", "Server Scripts"),
            Tx("Subset determinístico de Python com superfície específica limitada ao build.", "Deterministic Python subset with a build-limited specific surface.", "Subconjunto determinístico de Python con una superficie específica limitada al build."),
            Sec(Tx("API suportada", "Supported API", "API soportada"), Tx(
                "Somente as funções listadas pelo próprio catálogo deste build pertencem à API específica de Server Script. Não use convenience functions históricas, planejadas ou imaginadas. TAGs usados pelo script devem ser dependências declaradas.",
                "Only functions listed by this build's own catalog belong to the Server Script-specific API. Do not use historical, planned or imagined convenience functions. TAGs used by the script must be declared dependencies.",
                "Solo las funciones listadas por el propio catálogo de este build pertenecen a la API específica de Server Script. No use convenience functions históricas, planificadas o imaginadas. Los TAGs usados por el script deben ser dependencias declaradas."),
                "value = read_tag(\"<stable-tag-id>\")\nwrite_tag(\"<stable-tag-id>\", value)\nemit_operational_event(\"<definition-id>\", \"message\", {\"source\": \"script\"})"),
            Sec(Tx("Server Memory", "Server Memory", "Server Memory"), Tx(
                "As funções específicas de Server Memory exigem dependência explícita ServerMemoryTag. O script não deve criar um caminho privado que faça Runtime depender de estado externo ao projeto.",
                "Server Memory-specific functions require an explicit ServerMemoryTag dependency. A script must not create a private path that makes Runtime depend on state outside the project.",
                "Las funciones específicas de Server Memory requieren una dependencia explícita ServerMemoryTag. Un script no debe crear una ruta privada que haga que Runtime dependa de estado externo al proyecto."),
                "value = read_server_memory(\"<stable-tag-id>\")\nwrite_server_memory(\"<stable-tag-id>\", value)\npublish_server_memory_sample(\"<stable-tag-id>\", value, \"Good\")")),

        Manual(
            "alarms.overview", "alarms",
            Tx("Alarmes", "Alarms", "Alarmas"),
            Tx("Condições operacionais de alarme permanecem um domínio próprio.", "Operational alarm conditions remain their own domain.", "Las condiciones operacionales de alarma siguen siendo un dominio propio."),
            Sec(Tx("Separação semântica", "Semantic separation", "Separación semántica"), Tx(
                "Alarm é diferente de Operational Event e de Audit. Use Alarm para o contrato de condição operacional configurada; não registre toda ocorrência operacional ou ação de segurança como alarme apenas para reutilizar uma lista.",
                "Alarm is distinct from Operational Event and Audit. Use Alarm for the configured operational-condition contract; do not record every operational occurrence or security action as an alarm merely to reuse a list.",
                "Alarm es distinto de Operational Event y Audit. Use Alarm para el contrato de condición operacional configurada; no registre cada ocurrencia operacional o acción de seguridad como alarma solo para reutilizar una lista."))),

        Manual(
            "operational-events.overview", "operational-events",
            Tx("Eventos Operacionais", "Operational Events", "Eventos Operacionales"),
            Tx("Ocorrências operacionais registradas sem serem confundidas com Alarm ou Audit.", "Operational occurrences recorded without being confused with Alarm or Audit.", "Ocurrencias operacionales registradas sin confundirse con Alarm o Audit."),
            Sec(Tx("Uso", "Use", "Uso"), Tx(
                "Operational Event registra uma ocorrência operacional prevista pelo contrato do produto. Ele não substitui o estado de Alarm e não é o ledger de Audit para ações de segurança e engenharia.",
                "Operational Event records an operational occurrence defined by the product contract. It does not replace Alarm state and is not the Audit ledger for security and engineering actions.",
                "Operational Event registra una ocurrencia operacional definida por el contrato del producto. No sustituye el estado de Alarm y no es el ledger de Audit para acciones de seguridad e ingeniería."))),

        Manual(
            "audit.overview", "audit",
            Tx("Auditoria", "Audit", "Auditoría"),
            Tx("Rastreabilidade de ações de segurança e engenharia.", "Traceability for security and engineering actions.", "Trazabilidad de acciones de seguridad e ingeniería."),
            Sec(Tx("Domínio", "Domain", "Dominio"), Tx(
                "Audit é semanticamente distinto de Alarm e Operational Event. Use-o para rastreabilidade das ações que o contrato de segurança/engenharia exige, preservando ator, ação e contexto fornecidos pela autoridade canônica.",
                "Audit is semantically distinct from Alarm and Operational Event. Use it for traceability of actions required by the security/engineering contract, preserving actor, action and context supplied by canonical authority.",
                "Audit es semánticamente distinto de Alarm y Operational Event. Úselo para trazabilidad de las acciones exigidas por el contrato de seguridad/ingeniería, preservando actor, acción y contexto suministrados por la autoridad canónica."))),

        Manual(
            "historian.overview", "historian",
            Tx("Historian", "Historian", "Historian"),
            Tx("Persistência e consulta histórica de amostras autorizadas.", "Persistence and historical query of authorized samples.", "Persistencia y consulta histórica de muestras autorizadas."),
            Sec(Tx("Dados", "Data", "Datos"), Tx(
                "Historian recebe valores, quality e timestamps pelo pipeline canônico. Não corrija histórico reescrevendo dados na UI; diagnostique fonte, TAG e persistência responsáveis pela amostra.",
                "Historian receives values, quality and timestamps through the canonical pipeline. Do not correct history by rewriting data in the UI; diagnose the source, TAG and persistence responsible for the sample.",
                "Historian recibe valores, quality y timestamps mediante el pipeline canónico. No corrija el histórico reescribiendo datos en la UI; diagnostique fuente, TAG y persistencia responsables de la muestra."))),

        Manual(
            "trends.overview", "trends",
            Tx("Trends", "Trends", "Trends"),
            Tx("Visualização temporal de TAGs sem criar uma fonte paralela de dados.", "Time visualization of TAGs without creating a parallel data source.", "Visualización temporal de TAGs sin crear una fuente de datos paralela."),
            Sec(Tx("Origem", "Source", "Origen"), Tx(
                "Configure séries a partir de TAGs autorizados e use dados atuais ou históricos fornecidos pelos serviços canônicos. Trend apresenta os dados; não deve substituir quality, timestamp, scaling ou autoridade do Historian.",
                "Configure series from authorized TAGs and use current or historical data supplied by canonical services. Trend presents the data; it must not replace quality, timestamp, scaling or Historian authority.",
                "Configure series a partir de TAGs autorizados y use datos actuales o históricos suministrados por servicios canónicos. Trend presenta los datos; no debe sustituir quality, timestamp, scaling ni autoridad de Historian."))),

        Manual(
            "reports.overview", "reports",
            Tx("Reports", "Reports", "Reports"),
            Tx("Relatórios derivados de dados e permissões canônicas.", "Reports derived from canonical data and permissions.", "Informes derivados de datos y permisos canónicos."),
            Sec(Tx("Execução", "Execution", "Ejecución"), Tx(
                "Um relatório consulta apenas dados que seu contrato e a sessão permitem. Filtros e formatação de relatório não concedem acesso adicional nem mudam o valor canônico dos TAGs.",
                "A report queries only data allowed by its contract and the session. Report filters and formatting do not grant additional access or change canonical TAG values.",
                "Un informe consulta solo datos permitidos por su contrato y la sesión. Los filtros y el formato del informe no conceden acceso adicional ni cambian los valores canónicos de TAGs."))),

        Manual(
            "screens.overview", "screens",
            Tx("Screens", "Screens", "Screens"),
            Tx("Telas operacionais vinculadas a recursos canônicos do projeto.", "Operational screens bound to canonical project resources.", "Pantallas operacionales vinculadas a recursos canónicos del proyecto."),
            Sec(Tx("Conteúdo", "Content", "Contenido"), Tx(
                "Screens pertencem ao projeto e devem referenciar TAGs, dynamos, popups, bindings e commands por contratos válidos. Uma tela não deve incorporar conexão privada de driver nem autoridade própria de Runtime.",
                "Screens belong to the project and must reference TAGs, dynamos, popups, bindings and commands through valid contracts. A screen must not embed a private driver connection or its own Runtime authority.",
                "Screens pertenecen al proyecto y deben referenciar TAGs, dynamos, popups, bindings y commands mediante contratos válidos. Una pantalla no debe incorporar una conexión privada de driver ni autoridad propia de Runtime."))),

        Manual(
            "popups.overview", "popups",
            Tx("Popups", "Popups", "Popups"),
            Tx("Conteúdo reutilizável de interface aberto dentro do contexto operacional.", "Reusable UI content opened within operational context.", "Contenido reutilizable de interfaz abierto dentro del contexto operacional."),
            Sec(Tx("Contexto", "Context", "Contexto"), Tx(
                "Passe contexto e parâmetros por contratos suportados. Abrir um Popup não aumenta capabilities da sessão; qualquer comando ou escrita continua sujeito à mesma autorização server-side do Runtime.",
                "Pass context and parameters through supported contracts. Opening a Popup does not increase session capabilities; any command or write remains subject to the same Runtime server-side authorization.",
                "Pase contexto y parámetros mediante contratos soportados. Abrir un Popup no aumenta las capabilities de la sesión; cualquier comando o escritura sigue sujeto a la misma autorización server-side de Runtime."))),

        Manual(
            "dynamos.overview", "dynamos",
            Tx("Dynamos", "Dynamos", "Dynamos"),
            Tx("Recursos visuais reutilizáveis com dependências explícitas.", "Reusable visual resources with explicit dependencies.", "Recursos visuales reutilizables con dependencias explícitas."),
            Sec(Tx("Reuso", "Reuse", "Reutilización"), Tx(
                "Dynamos encapsulam comportamento visual reutilizável e devem declarar as dependências necessárias. Instâncias usam bindings do projeto; não copie conexões privadas ou IDs acidentais de outro projeto.",
                "Dynamos encapsulate reusable visual behavior and must declare required dependencies. Instances use project bindings; do not copy private connections or accidental IDs from another project.",
                "Dynamos encapsulan comportamiento visual reutilizable y deben declarar las dependencias necesarias. Las instancias usan bindings del proyecto; no copie conexiones privadas ni IDs accidentales de otro proyecto."))),

        Manual(
            "bindings-commands.overview", "bindings-commands",
            Tx("Bindings e Commands", "Bindings and Commands", "Bindings y Commands"),
            Tx("Ligação visual e ações operacionais preservando autoridade do servidor.", "Visual binding and operational actions while preserving server authority.", "Vinculación visual y acciones operacionales preservando autoridad del servidor."),
            Sec(Tx("Bindings", "Bindings", "Bindings"), Tx(
                "Bindings conectam propriedades da interface a recursos canônicos do projeto. Eles não devem duplicar TAG Engine, scaling ou lógica de autoridade no navegador.",
                "Bindings connect UI properties to canonical project resources. They must not duplicate TAG Engine, scaling or authority logic in the browser.",
                "Bindings conectan propiedades de la interfaz con recursos canónicos del proyecto. No deben duplicar TAG Engine, scaling ni lógica de autoridad en el navegador.")),
            Sec(Tx("Commands", "Commands", "Commands"), Tx(
                "Commands representam intenção do operador e precisam passar pela autorização e validação server-side. Estado Viewer/View Only ou ausência de capability deve bloquear a ação mesmo que o controle visual exista.",
                "Commands represent operator intent and must pass server-side authorization and validation. Viewer/View Only state or missing capability must block the action even when the visual control exists.",
                "Commands representan la intención del operador y deben pasar por autorización y validación server-side. El estado Viewer/View Only o la falta de capability debe bloquear la acción aunque exista el control visual."))),

        Manual(
            "security.users-roles-capabilities", "security",
            Tx("Usuários, roles, capabilities e segurança", "Users, roles, capabilities and security", "Usuarios, roles, capabilities y seguridad"),
            Tx("Identidade e autorização permanecem sob autoridade do backend.", "Identity and authorization remain under backend authority.", "Identidad y autorización permanecen bajo autoridad del backend."),
            Sec(Tx("Usuários e roles", "Users and roles", "Usuarios y roles"), Tx(
                "Administre identidades e roles pelos fluxos suportados do produto. Role é entrada para autorização, não permissão automática para toda ação existente na interface.",
                "Manage identities and roles through supported product flows. A role is an authorization input, not automatic permission for every action present in the UI.",
                "Administre identidades y roles mediante los flujos soportados del producto. Un role es una entrada de autorización, no permiso automático para toda acción presente en la UI.")),
            Sec(Tx("Capabilities", "Capabilities", "Capabilities"), Tx(
                "Capabilities efetivas são calculadas e aplicadas pelo servidor. A UI usa essas capabilities para apresentação e UX, mas o backend continua responsável por negar operações não autorizadas.",
                "Effective capabilities are calculated and enforced by the server. The UI uses them for presentation and UX, but the backend remains responsible for denying unauthorized operations.",
                "Las capabilities efectivas son calculadas y aplicadas por el servidor. La UI las usa para presentación y UX, pero el backend sigue siendo responsable de negar operaciones no autorizadas."))),

        Manual(
            "licensing.overview", "licensing",
            Tx("Licensing", "Licensing", "Licensing"),
            Tx("Recursos comerciais sem substituir segurança ou autoridade de Runtime.", "Commercial feature control without replacing security or Runtime authority.", "Control de funciones comerciales sin sustituir seguridad ni autoridad de Runtime."),
            Sec(Tx("Contrato", "Contract", "Contrato"), Tx(
                "Licensing pode limitar recursos e capacidades comerciais do produto. Ele não substitui autenticação, autorização, Engineering Lock, lifecycle, package validation ou Runtime authority.",
                "Licensing may limit product features and commercial capabilities. It does not replace authentication, authorization, Engineering Lock, lifecycle, package validation or Runtime authority.",
                "Licensing puede limitar funciones y capacidades comerciales del producto. No sustituye autenticación, autorización, Engineering Lock, lifecycle, validación de package ni Runtime authority."))),

        Manual(
            "recovery.backup-system-recovery", "recovery",
            Tx("Backup e System Recovery", "Backup and System Recovery", "Backup y System Recovery"),
            Tx("Proteção e recuperação da Authority sem atalhos destrutivos.", "Authority protection and recovery without destructive shortcuts.", "Protección y recuperación de Authority sin atajos destructivos."),
            Sec(Tx("Backup", "Backup", "Backup"), Tx(
                "Use o fluxo de backup suportado para preservar os dados e metadados previstos pelo contrato. Trate o backup como artefato sensível e mantenha validação criptográfica e de formato quando exigida pelo produto.",
                "Use the supported backup flow to preserve data and metadata defined by the contract. Treat backup as a sensitive artifact and retain cryptographic and format validation when required by the product.",
                "Use el flujo de backup soportado para preservar datos y metadatos definidos por el contrato. Trate el backup como un artefacto sensible y mantenga validación criptográfica y de formato cuando el producto la requiera.")),
            Sec(Tx("System Recovery", "System Recovery", "System Recovery"), Tx(
                "Recovery substitui estado apenas pelo contrato atômico e validado do produto. Não edite stores manualmente, não pule restore-first quando aplicável e não use recovery para contornar identidade, lifecycle ou Authority.",
                "Recovery replaces state only through the product's validated atomic contract. Do not edit stores manually, skip restore-first when applicable, or use recovery to bypass identity, lifecycle or Authority.",
                "Recovery sustituye estado solo mediante el contrato atómico y validado del producto. No edite stores manualmente, omita restore-first cuando corresponda ni use recovery para evitar identidad, lifecycle o Authority."))),

        Manual(
            "diagnostics.overview", "diagnostics",
            Tx("Diagnostics", "Diagnostics", "Diagnostics"),
            Tx("Diagnóstico por camadas sem mascarar a causa raiz.", "Layered diagnostics without masking the root cause.", "Diagnóstico por capas sin ocultar la causa raíz."),
            Sec(Tx("Ordem de verificação", "Verification order", "Orden de verificación"), Tx(
                "Verifique saúde do backend, autenticação/capabilities, estado Active, Data Source, configuração do driver, conexão, binding do TAG, quality/timestamp e só então a apresentação na tela. Use capacidades de Connection Test, Discover, Browse, Import ou Reconcile apenas quando o descriptor real do driver as declarar.",
                "Verify backend health, authentication/capabilities, Active state, Data Source, driver configuration, connection, TAG binding, quality/timestamp and only then screen presentation. Use Connection Test, Discover, Browse, Import or Reconcile only when the real driver descriptor declares those capabilities.",
                "Verifique salud del backend, autenticación/capabilities, estado Active, Data Source, configuración del driver, conexión, binding del TAG, quality/timestamp y solo entonces presentación en pantalla. Use Connection Test, Discover, Browse, Import o Reconcile solo cuando el descriptor real del driver declare esas capabilities."))),

        Manual(
            "troubleshooting.overview", "troubleshooting",
            Tx("Troubleshooting", "Troubleshooting", "Troubleshooting"),
            Tx("Investigação de falhas preservando os contratos do produto.", "Failure investigation while preserving product contracts.", "Investigación de fallas preservando los contratos del producto."),
            Sec(Tx("Princípio", "Principle", "Principio"), Tx(
                "Reproduza o problema, identifique a camada que divergiu da autoridade canônica e corrija a causa genérica. Não crie workaround específico de demo, não enfraqueça testes e não bypass Authority, Engineering Lock, licensing, lifecycle, packages ou Runtime para obter um resultado visualmente aceitável.",
                "Reproduce the problem, identify the layer that diverged from canonical authority and fix the generic cause. Do not create demo-specific workarounds, weaken tests, or bypass Authority, Engineering Lock, licensing, lifecycle, packages or Runtime to obtain a visually acceptable result.",
                "Reproduzca el problema, identifique la capa que divergió de la autoridad canónica y corrija la causa genérica. No cree workarounds específicos de demo, debilite tests ni evite Authority, Engineering Lock, licensing, lifecycle, packages o Runtime para obtener un resultado visualmente aceptable.")),
            Sec(Tx("CI", "CI", "CI"), Tx(
                "Se um gate falhar, diagnostique logs e causa antes de rerun. Um rerun não é correção e um teste removido não é evidência de conformidade.",
                "If a gate fails, diagnose logs and cause before rerun. A rerun is not a fix and a removed test is not evidence of compliance.",
                "Si un gate falla, diagnostique logs y causa antes de rerun. Un rerun no es una corrección y un test eliminado no es evidencia de conformidad."))),

        Manual(
            "libraries.reusable-resources", "libraries",
            Tx("Reusable Resource Libraries", "Reusable Resource Libraries", "Reusable Resource Libraries"),
            Tx("Reuso seletivo em Engineering sem criar dependência de Runtime em biblioteca externa.", "Selective Engineering reuse without creating Runtime dependency on an external library.", "Reutilización selectiva en Engineering sin crear dependencia de Runtime en una biblioteca externa."),
            Sec(Tx(".escadalib versus .escadapkg", ".escadalib versus .escadapkg", ".escadalib versus .escadapkg"), Tx(
                ".escadalib é uma biblioteca reutilizável e é diferente de .escadapkg. Associar uma Library não importa conteúdo e, sozinho, não altera Working; a associação apenas torna recursos compatíveis disponíveis no catálogo de Engineering.",
                ".escadalib is a reusable library and is different from .escadapkg. Associating a Library does not import content and, by itself, does not change Working; association only makes compatible resources available in the Engineering catalog.",
                ".escadalib es una biblioteca reutilizable y es diferente de .escadapkg. Asociar una Library no importa contenido y, por sí solo, no cambia Working; la asociación solo vuelve disponibles recursos compatibles en el catálogo de Engineering.")),
            Sec(Tx("Usar", "Use", "Usar"), Tx(
                "Usar incorpora seletivamente o recurso escolhido e o closure validado de suas dependências. O conteúdo incorporado passa a ser conteúdo canônico pertencente ao projeto, sujeito às mesmas validações e ao mesmo lifecycle do restante do Working.",
                "Use selectively incorporates the chosen resource and its validated dependency closure. Incorporated content becomes canonical project-owned content, subject to the same validations and lifecycle as the rest of Working.",
                "Usar incorpora selectivamente el recurso elegido y el closure validado de sus dependencias. El contenido incorporado pasa a ser contenido canónico perteneciente al proyecto, sujeto a las mismas validaciones y al mismo lifecycle que el resto de Working.")),
            Sec(Tx("Desassociar e Runtime", "Detach and Runtime", "Desasociar y Runtime"), Tx(
                "Desassociar remove a disponibilidade da Library no catálogo, mas não apaga conteúdo já incorporado. O .escadapkg final permanece self-contained; Runtime e Active nunca dependem de .escadalib para executar o conteúdo incorporado.",
                "Detaching removes Library availability from the catalog but does not delete already incorporated content. The final .escadapkg remains self-contained; Runtime and Active never depend on .escadalib to execute incorporated content.",
                "Desasociar elimina la disponibilidad de la Library del catálogo, pero no borra contenido ya incorporado. El .escadapkg final permanece self-contained; Runtime y Active nunca dependen de .escadalib para ejecutar contenido incorporado.")))
    };

    public static ContextualHelpCatalogView Build(string? requestedLocale)
    {
        var locale = NormalizeLocale(requestedLocale);
        var topics = ManualTopics.Select(topic => topic.Build(locale)).ToList();
        var dataSourceTypes = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes;

        foreach (var source in dataSourceTypes
            .Where(item => string.Equals(item.Kind, "sourceProvider", StringComparison.Ordinal))
            .OrderBy(item => item.TypeKey, StringComparer.Ordinal))
        {
            topics.Add(BuildSourceProviderTopic(source, locale));
        }

        foreach (var driver in dataSourceTypes
            .Where(IsProductionCommunicationDriver)
            .OrderBy(item => item.TypeKey, StringComparer.Ordinal))
        {
            topics.Add(BuildDriverTopic(driver, locale));
        }

        return new ContextualHelpCatalogView(
            locale,
            SupportedLocales,
            topics.OrderBy(topic => topic.Id, StringComparer.Ordinal).ToArray(),
            ServerScriptApiFunctions);
    }

    public static string NormalizeLocale(string? locale) => locale switch
    {
        "en" => "en",
        "es" => "es",
        _ => "pt-BR"
    };

    private static bool IsProductionCommunicationDriver(EngineeringDataSourceTypeView source) =>
        string.Equals(source.Kind, "communicationDriver", StringComparison.Ordinal) &&
        !string.Equals(source.TypeKey, "simulation", StringComparison.OrdinalIgnoreCase);

    private static ContextualHelpTopic BuildSourceProviderTopic(EngineeringDataSourceTypeView source, string locale) =>
        new(
            $"source.{source.TypeKey}",
            "sources",
            source.DisplayName,
            Pick(locale,
                "Source provider disponível nesta compilação.",
                "Source provider available in this build.",
                "Source provider disponible en esta compilación."),
            new[]
            {
                new ContextualHelpSection(
                    Pick(locale, "Identidade", "Identity", "Identidad"),
                    $"{Pick(locale, "Type key", "Type key", "Type key")}: {source.TypeKey}"),
                new ContextualHelpSection(
                    Pick(locale, "Contrato", "Contract", "Contrato"),
                    string.IsNullOrWhiteSpace(source.Description)
                        ? Pick(locale,
                            "A fonte vem do catálogo canônico e não é um driver de comunicação.",
                            "The source comes from the canonical catalog and is not a communication driver.",
                            "La fuente proviene del catálogo canónico y no es un driver de comunicación.")
                        : source.Description)
            });

    private static ContextualHelpTopic BuildDriverTopic(EngineeringDataSourceTypeView driver, string locale)
    {
        var schema = driver.ConfigurationSchema;
        var capabilities = new[]
        {
            driver.Capabilities.SupportsConnectionTest ? Pick(locale, "Teste de conexão", "Connection test", "Prueba de conexión") : null,
            driver.Capabilities.SupportsDiscovery ? Pick(locale, "Descoberta", "Discovery", "Descubrimiento") : null,
            driver.Capabilities.SupportsBrowse ? "Browse" : null,
            driver.Capabilities.SupportsFileImport ? Pick(locale, "Importação de arquivo", "File import", "Importación de archivo") : null,
            driver.Capabilities.SupportsReconcile ? Pick(locale, "Reconciliação", "Reconcile", "Reconciliación") : null,
            driver.Capabilities.SupportsSharedTransportInfrastructure ? Pick(locale, "Transporte compartilhado", "Shared transport", "Transporte compartido") : null
        }.Where(value => value is not null).Cast<string>().ToArray();

        var sourceFields = FormatFields(schema?.DataSourceFields ?? Array.Empty<EngineeringDriverConfigurationFieldView>(), locale);
        var bindingFields = FormatFields(schema?.TagBindingFields ?? Array.Empty<EngineeringDriverConfigurationFieldView>(), locale);
        var protectedFields = (schema?.DataSourceFields ?? Array.Empty<EngineeringDriverConfigurationFieldView>())
            .Concat(schema?.TagBindingFields ?? Array.Empty<EngineeringDriverConfigurationFieldView>())
            .Where(field => field.ValueKind is "secretReference" or "certificateReference")
            .Select(field => field.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var schemaIdentity = schema is null
            ? Pick(locale, "Nenhum configuration schema declarado.", "No configuration schema declared.", "No hay configuration schema declarado.")
            : $"{schema.SchemaId} v{schema.SchemaVersion}; TagBinding: {driver.TagBindingSchemaId ?? schema.SchemaId} v{driver.TagBindingSchemaVersion ?? schema.SchemaVersion}";

        return new ContextualHelpTopic(
            $"driver.{driver.TypeKey}",
            "drivers",
            driver.DisplayName,
            Pick(locale,
                "Driver de comunicação de produção registrado nesta compilação do EliteSCADA.",
                "Production communication driver registered in this EliteSCADA build.",
                "Driver de comunicación de producción registrado en esta compilación de EliteSCADA."),
            new[]
            {
                new ContextualHelpSection(
                    Pick(locale, "Identidade canônica", "Canonical identity", "Identidad canónica"),
                    $"Type key: {driver.TypeKey}{(string.IsNullOrWhiteSpace(driver.Description) ? string.Empty : $"\n{driver.Description}")}"),
                new ContextualHelpSection(
                    Pick(locale, "Schemas registrados", "Registered schemas", "Schemas registrados"),
                    schemaIdentity),
                new ContextualHelpSection(
                    Pick(locale, "Configuração do Data Source", "Data Source configuration", "Configuración del Data Source"),
                    sourceFields),
                new ContextualHelpSection(
                    Pick(locale, "Addressing / binding do TAG", "TAG addressing / binding", "Addressing / binding del TAG"),
                    bindingFields),
                new ContextualHelpSection(
                    Pick(locale, "Capabilities de Engineering", "Engineering capabilities", "Capabilities de Engineering"),
                    capabilities.Length == 0
                        ? Pick(locale, "Nenhuma capability opcional declarada pelo descriptor.", "No optional capability declared by the descriptor.", "Ninguna capability opcional declarada por el descriptor.")
                        : string.Join("\n", capabilities.Select(item => $"• {item}"))),
                new ContextualHelpSection(
                    Pick(locale, "Quality, timestamps e writeability", "Quality, timestamps and writeability", "Quality, timestamps y writeability"),
                    Pick(locale,
                        "O driver alimenta o pipeline canônico de TAGs. Preserve quality e timestamps recebidos pelo Runtime e trate writeability como contrato do TAG/fonte mais capabilities efetivas; a UI não deve inventar qualidade, horário ou permissão de escrita.",
                        "The driver feeds the canonical TAG pipeline. Preserve quality and timestamps received by Runtime and treat writeability as the TAG/source contract plus effective capabilities; the UI must not invent quality, time or write permission.",
                        "El driver alimenta el pipeline canónico de TAGs. Preserve quality y timestamps recibidos por Runtime y trate writeability como contrato del TAG/fuente más capabilities efectivas; la UI no debe inventar quality, tiempo ni permiso de escritura.")),
                new ContextualHelpSection(
                    Pick(locale, "Diagnóstico", "Diagnostics", "Diagnóstico"),
                    Pick(locale,
                        "Comece pelos campos obrigatórios e formatos abaixo, depois use somente as capabilities declaradas pelo descriptor. Falhas de conexão, discovery ou binding devem ser corrigidas no Data Source/driver, não escondidas por fallback visual.",
                        "Start with the required fields and formats below, then use only capabilities declared by the descriptor. Connection, discovery or binding failures must be corrected in the Data Source/driver, not hidden by visual fallback.",
                        "Comience por los campos obligatorios y formatos indicados, luego use solo las capabilities declaradas por el descriptor. Fallas de conexión, discovery o binding deben corregirse en Data Source/driver, no ocultarse con fallback visual.")),
                new ContextualHelpSection(
                    Pick(locale, "Material protegido", "Protected material", "Material protegido"),
                    protectedFields.Length == 0
                        ? Pick(locale,
                            "O schema não declara campos de secret/certificate reference. Isso não reduz autenticação, autorização ou demais controles do produto.",
                            "The schema declares no secret/certificate reference fields. This does not reduce authentication, authorization or other product controls.",
                            "El schema no declara campos de secret/certificate reference. Esto no reduce autenticación, autorización ni otros controles del producto.")
                        : $"{Pick(locale, "Referências protegidas declaradas pelo schema", "Protected references declared by the schema", "Referencias protegidas declaradas por el schema")}: {string.Join(", ", protectedFields)}")
            });
    }

    private static string FormatFields(IReadOnlyCollection<EngineeringDriverConfigurationFieldView> fields, string locale)
    {
        if (fields.Count == 0)
            return Pick(locale, "Nenhum campo declarado neste schema.", "No fields declared in this schema.", "No hay campos declarados en este schema.");

        return string.Join("\n", fields.Select(field => FormatField(field, locale)));
    }

    private static string FormatField(EngineeringDriverConfigurationFieldView field, string locale)
    {
        var required = field.Required
            ? Pick(locale, "obrigatório", "required", "obligatorio")
            : Pick(locale, "opcional", "optional", "opcional");
        var parts = new List<string>
        {
            $"{field.Key} - {field.DisplayName} [{field.ValueKind}, {required}]"
        };

        if (!string.IsNullOrWhiteSpace(field.Description))
            parts.Add(field.Description);
        if (!string.IsNullOrWhiteSpace(field.ExpectedFormat))
            parts.Add($"{Pick(locale, "Formato", "Format", "Formato")}: {field.ExpectedFormat}");
        if (!string.IsNullOrWhiteSpace(field.DefaultValue))
            parts.Add($"{Pick(locale, "Default", "Default", "Default")}: {field.DefaultValue}");
        if (field.AllowedValues.Count > 0)
            parts.Add($"{Pick(locale, "Valores", "Values", "Valores")}: {string.Join(" | ", field.AllowedValues)}");
        if (field.Minimum.HasValue || field.Maximum.HasValue)
            parts.Add($"{Pick(locale, "Limites", "Limits", "Límites")}: {field.Minimum?.ToString() ?? "-∞"} .. {field.Maximum?.ToString() ?? "+∞"}");
        if (!string.IsNullOrWhiteSpace(field.ExampleValue) && !IsExternalHttpValue(field.ExampleValue))
            parts.Add($"{Pick(locale, "Exemplo", "Example", "Ejemplo")}: {field.ExampleValue}");
        if (field.Advanced)
            parts.Add(Pick(locale, "campo avançado", "advanced field", "campo avanzado"));

        return string.Join("; ", parts);
    }

    private static bool IsExternalHttpValue(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static TopicDefinition Manual(
        string id,
        string category,
        LocalizedText title,
        LocalizedText summary,
        params SectionDefinition[] sections) =>
        new(id, category, title, summary, sections);

    private static SectionDefinition Sec(LocalizedText heading, LocalizedText body, string? code = null) =>
        new(heading, body, code);

    private static LocalizedText Tx(string pt, string en, string es) => new(pt, en, es);

    private static string Pick(string locale, string pt, string en, string es) => locale switch
    {
        "en" => en,
        "es" => es,
        _ => pt
    };

    private sealed record LocalizedText(string Portuguese, string English, string Spanish)
    {
        public string For(string locale) => locale switch
        {
            "en" => English,
            "es" => Spanish,
            _ => Portuguese
        };
    }

    private sealed record SectionDefinition(LocalizedText Heading, LocalizedText Body, string? Code)
    {
        public ContextualHelpSection Build(string locale) => new(Heading.For(locale), Body.For(locale), Code);
    }

    private sealed record TopicDefinition(
        string Id,
        string Category,
        LocalizedText Title,
        LocalizedText Summary,
        IReadOnlyCollection<SectionDefinition> Sections)
    {
        public ContextualHelpTopic Build(string locale) => new(
            Id,
            Category,
            Title.For(locale),
            Summary.For(locale),
            Sections.Select(section => section.Build(locale)).ToArray());
    }
}

public static class ContextualHelpApi
{
    public static IEndpointRouteBuilder MapContextualHelpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/help", (
            string? locale,
            string? topic,
            HttpContext context,
            ApiAuthorizationService security) =>
        {
            if (security.AuthenticationEnabled)
            {
                var principal = security.GetPrincipal(context);
                if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
                    return Results.Unauthorized();
            }

            var catalog = ContextualHelpCatalog.Build(locale);
            if (string.IsNullOrWhiteSpace(topic))
                return Results.Ok(catalog);

            var selected = catalog.Topics.FirstOrDefault(item =>
                item.Id.Equals(topic, StringComparison.Ordinal));
            return selected is null
                ? Results.NotFound(new { error = "Help topic not found.", topic })
                : Results.Ok(new
                {
                    catalog.Locale,
                    catalog.SupportedLocales,
                    topic = selected,
                    catalog.ServerScriptApi
                });
        });

        return endpoints;
    }
}
