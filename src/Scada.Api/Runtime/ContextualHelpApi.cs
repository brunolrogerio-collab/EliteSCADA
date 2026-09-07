using Scada.Api.Security;
using Scada.DriverHost.Engineering;

namespace Scada.Api.Runtime;

public sealed record ContextualHelpSection(string Heading, string Body, string? Code = null);

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
    public const string ExcludedSimulationDriverTypeKey = "builtin.simulation";

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

    public static readonly IReadOnlyCollection<string> ServerScriptRuntimeTriggers = new[]
    {
        "Initialize",
        "Dispose",
        "TagChanged",
        "Timer",
        "ServerRuntimeEvent"
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

    private static readonly IReadOnlyCollection<TopicDefinition> ManualTopics = BuildManualTopics();

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
        !string.Equals(source.TypeKey, ExcludedSimulationDriverTypeKey, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyCollection<TopicDefinition> BuildManualTopics() => new[]
    {
        Basic(
            "getting-started.startup-authentication", "getting-started",
            Tx("Primeiro startup e autenticação", "First startup and authentication", "Primer inicio y autenticación"),
            Tx("Entrada segura no produto.", "Secure product entry.", "Entrada segura al producto."),
            Tx(
                "Inicie backend e UI, conclua o bootstrap administrativo solicitado pelo próprio produto e confirme saúde dos serviços. Quando autenticação estiver habilitada, use identidade válida. Roles e capabilities são resolvidas pelo backend; visibilidade de controles não substitui autorização server-side.",
                "Start backend and UI, complete the administrative bootstrap requested by the product, and confirm service health. When authentication is enabled, use a valid identity. Roles and capabilities are resolved by the backend; control visibility does not replace server-side authorization.",
                "Inicie backend e interfaz, complete el bootstrap administrativo solicitado por el producto y confirme la salud de los servicios. Cuando la autenticación esté habilitada, use una identidad válida. Roles y capabilities son resueltos por el backend; la visibilidad de controles no sustituye autorización server-side.")),

        Basic(
            "runtime.overview", "runtime",
            Tx("Runtime para o operador", "Runtime operator guide", "Guía de Runtime para el operador"),
            Tx("Operação da revisão Active sob autoridade do backend.", "Operation of the Active revision under backend authority.", "Operación de la revisión Active bajo autoridad del backend."),
            Tx(
                "O backend e a revisão Active são autoridade canônica. A sessão recebe capabilities efetivas e lease do servidor. Viewer e View Only reduzem capacidades; comandos e escritas continuam bloqueados server-side quando não autorizados. Perda ou expiração do lease significa perda de autoridade interativa, nunca permissão implícita.",
                "The backend and Active revision are canonical authority. The session receives effective capabilities and a server lease. Viewer and View Only reduce capabilities; commands and writes remain server-blocked when unauthorized. Lease loss or expiry means loss of interactive authority, never implicit permission.",
                "El backend y la revisión Active son autoridad canónica. La sesión recibe capabilities efectivas y lease del servidor. Viewer y View Only reducen capacidades; comandos y escrituras siguen bloqueados server-side cuando no están autorizados. La pérdida o expiración del lease significa pérdida de autoridad interactiva, nunca permiso implícito.")),

        Basic(
            "runtime.history", "runtime",
            Tx("Histórico no Runtime", "Runtime history", "Histórico en Runtime"),
            Tx("Consulta histórica autorizada.", "Authorized historical query.", "Consulta histórica autorizada."),
            Tx(
                "Use os serviços canônicos de histórico. A visibilidade de TAGs e as capabilities da sessão continuam válidas; abrir uma tela histórica não concede acesso adicional.",
                "Use canonical history services. TAG visibility and session capabilities still apply; opening a history screen grants no additional access.",
                "Use los servicios canónicos de histórico. La visibilidad de TAGs y las capabilities de la sesión siguen vigentes; abrir una pantalla histórica no concede acceso adicional.")),

        Basic(
            "engineering.overview", "engineering",
            Tx("Engineering", "Engineering", "Engineering"),
            Tx("Edição sem confundir estados do lifecycle.", "Editing without confusing lifecycle states.", "Edición sin confundir estados del lifecycle."),
            Tx(
                "Working, Revision, Published e Active são estados distintos. A UI projeta o estado informado pelo backend e não presume Save, Publish ou Activate por causa de uma edição local.",
                "Working, Revision, Published and Active are distinct states. The UI projects backend state and does not assume Save, Publish or Activate because of a local edit.",
                "Working, Revision, Published y Active son estados distintos. La UI proyecta el estado del backend y no presume Save, Publish o Activate por una edición local.")),

        Detailed(
            "engineering.lifecycle", "engineering",
            Tx("Working -> Save -> Revision -> Publish -> Activate", "Working -> Save -> Revision -> Publish -> Activate", "Working -> Save -> Revision -> Publish -> Activate"),
            Tx("Fluxo canônico de lifecycle.", "Canonical lifecycle flow.", "Flujo canónico de lifecycle."),
            S(Tx("Working e Save", "Working and Save", "Working y Save"), Tx(
                "Working é editável. Save persiste o trabalho conforme o contrato de Engineering, sem convertê-lo silenciosamente em Published ou Active.",
                "Working is editable. Save persists work according to the Engineering contract without silently turning it into Published or Active.",
                "Working es editable. Save persiste el trabajo según el contrato de Engineering sin convertirlo silenciosamente en Published o Active.")),
            S(Tx("Revision e Publish", "Revision and Publish", "Revision y Publish"), Tx(
                "Crie uma Revision identificável a partir de Working validado. Publish torna a revisão publicada, mas não muda por si só a revisão Active executada pelo Runtime.",
                "Create an identifiable Revision from validated Working. Publish makes the revision published but does not by itself change the Active revision executed by Runtime.",
                "Cree una Revision identificable desde Working validado. Publish vuelve publicada la revisión, pero no cambia por sí solo la revisión Active ejecutada por Runtime.")),
            S(Tx("Activate", "Activate", "Activate"), Tx(
                "Activate é a transição explícita que muda a autoridade Active do backend. Validação, autorização e lifecycle permanecem obrigatórios; não simule ativação por UI, store ou package.",
                "Activate is the explicit transition that changes backend Active authority. Validation, authorization and lifecycle remain mandatory; do not simulate activation through UI, store or package.",
                "Activate es la transición explícita que cambia la autoridad Active del backend. Validación, autorización y lifecycle siguen siendo obligatorios; no simule activación mediante UI, store o package."))),

        Detailed(
            "packages.escadapkg", "packages",
            Tx("Pacotes .escadapkg", ".escadapkg packages", "Paquetes .escadapkg"),
            Tx("Portabilidade self-contained da aplicação.", "Self-contained application portability.", "Portabilidad self-contained de la aplicación."),
            S(Tx("Import e Export", "Import and Export", "Import y Export"), Tx(
                "Export produz um .escadapkg portátil e self-contained. Import recebe o pacote como entrada de Engineering e não contorna identidade, validação, segurança ou lifecycle.",
                "Export produces a portable, self-contained .escadapkg. Import receives the package as Engineering input and does not bypass identity, validation, security or lifecycle.",
                "Export produce un .escadapkg portátil y self-contained. Import recibe el paquete como entrada de Engineering y no evita identidad, validación, seguridad ni lifecycle.")),
            S(Tx("Inspect, Preview e Apply", "Inspect, Preview and Apply", "Inspect, Preview y Apply"), Tx(
                "Inspect examina conteúdo e metadados sem mutação; Preview mostra o resultado previsto; Apply incorpora conteúdo validado ao fluxo de Engineering. Apply não equivale a Activate.",
                "Inspect examines content and metadata without mutation; Preview shows the expected result; Apply incorporates validated content into Engineering. Apply is not Activate.",
                "Inspect examina contenido y metadatos sin mutación; Preview muestra el resultado previsto; Apply incorpora contenido validado a Engineering. Apply no equivale a Activate."))),

        Basic(
            "sources.data-sources", "sources",
            Tx("Data Sources", "Data Sources", "Data Sources"),
            Tx("Fontes registradas no build.", "Sources registered in the build.", "Fuentes registradas en el build."),
            Tx(
                "Crie Data Sources somente com tipos do catálogo canônico. Campos, formatos, limites, defaults e referências protegidas vêm do configuration schema registrado. Drivers de comunicação e source providers são conceitos distintos; Simulation é ferramenta de desenvolvimento/teste e não é apresentada como driver de produção.",
                "Create Data Sources only with types from the canonical catalog. Fields, formats, limits, defaults and protected references come from the registered configuration schema. Communication drivers and source providers are distinct concepts; Simulation is a development/test tool and is not presented as a production driver.",
                "Cree Data Sources solo con tipos del catálogo canónico. Campos, formatos, límites, defaults y referencias protegidas provienen del configuration schema registrado. Drivers de comunicación y source providers son conceptos distintos; Simulation es una herramienta de desarrollo/prueba y no se presenta como driver de producción.")),

        Basic(
            "tags.overview", "tags",
            Tx("TAGs", "TAGs", "TAGs"),
            Tx("Identidade e valor no TAG Engine.", "Identity and value in the TAG Engine.", "Identidad y valor en TAG Engine."),
            Tx(
                "Use a identidade estável do TAG e o binding suportado pelo Data Source. Leituras, escritas, quality e timestamps são projetados pelo Runtime sob autoridade do backend; consumidores usam TAG Engine/cache/eventos públicos, não acesso privado a drivers.",
                "Use the TAG stable identity and the binding supported by the Data Source. Reads, writes, quality and timestamps are projected by Runtime under backend authority; consumers use public TAG Engine/cache/events, not private driver access.",
                "Use la identidad estable del TAG y el binding soportado por el Data Source. Lecturas, escrituras, quality y timestamps son proyectados por Runtime bajo autoridad del backend; consumidores usan TAG Engine/cache/eventos públicos, no acceso privado a drivers.")),

        Basic(
            "tags.quality", "tags",
            Tx("Quality", "Quality", "Quality"),
            Tx("Confiança da amostra.", "Sample trust state.", "Estado de confianza de la muestra."),
            Tx(
                "Quality vem do pipeline canônico da fonte/TAG. Não transforme falha, ausência ou dado stale em Good para estabilizar a UI. Para diagnóstico, verifique Data Source, conexão, addressing/binding e driver antes da apresentação.",
                "Quality comes from the canonical source/TAG pipeline. Do not turn failure, absence or stale data into Good to stabilize the UI. For diagnostics, check Data Source, connection, addressing/binding and driver before presentation.",
                "Quality proviene del pipeline canónico de fuente/TAG. No convierta falla, ausencia o dato stale en Good para estabilizar la UI. Para diagnóstico, verifique Data Source, conexión, addressing/binding y driver antes de la presentación.")),

        Basic(
            "tags.timestamps", "tags",
            Tx("Timestamps", "Timestamps", "Timestamps"),
            Tx("Tempo associado à amostra.", "Time associated with a sample.", "Tiempo asociado a la muestra."),
            Tx(
                "Preserve o timestamp fornecido pelo pipeline canônico conforme o contrato da fonte. Não substitua timestamps de protocolo ou Runtime pelo relógio do navegador para exibição ou persistência.",
                "Preserve the timestamp supplied by the canonical pipeline according to the source contract. Do not replace protocol or Runtime timestamps with browser time for display or persistence.",
                "Preserve el timestamp suministrado por el pipeline canónico según el contrato de la fuente. No sustituya timestamps de protocolo o Runtime por la hora del navegador para visualización o persistencia.")),

        Basic(
            "tags.writeability", "tags",
            Tx("Writeability", "Writeability", "Writeability"),
            Tx("Autoridade de escrita.", "Write authority.", "Autoridad de escritura."),
            Tx(
                "Um controle visível não torna um TAG gravável. A escrita depende do contrato do TAG/fonte e das capabilities efetivas, com enforcement server-side. Viewer, View Only ou ausência da capability correspondente permanecem incapazes de escrever.",
                "A visible control does not make a TAG writable. Writing depends on the TAG/source contract and effective capabilities, with server-side enforcement. Viewer, View Only or a missing capability remain unable to write.",
                "Un control visible no vuelve un TAG escribible. La escritura depende del contrato TAG/fuente y de las capabilities efectivas, con enforcement server-side. Viewer, View Only o falta de capability siguen sin poder escribir.")),

        Basic(
            "tags.addressing", "tags",
            Tx("Addressing", "Addressing", "Addressing"),
            Tx("Endereçamento pelo binding schema real.", "Addressing through the real binding schema.", "Direccionamiento mediante el binding schema real."),
            Tx(
                "Use somente campos, formatos e valores expostos pelo TagBinding schema do driver selecionado. Não transplante sintaxe de outro protocolo ou SCADA; quando o backend rejeitar o binding, corrija a configuração na origem.",
                "Use only fields, formats and values exposed by the selected driver's TagBinding schema. Do not transplant syntax from another protocol or SCADA; when the backend rejects the binding, correct the source configuration.",
                "Use solo campos, formatos y valores expuestos por el TagBinding schema del driver seleccionado. No trasplante sintaxis de otro protocolo o SCADA; cuando el backend rechace el binding, corrija la configuración en origen.")),

        Detailed(
            "tags.scaling-formatting", "tags",
            Tx("Scaling versus formatação", "Scaling versus presentation formatting", "Scaling versus formato de presentación"),
            Tx("Valor de engenharia e aparência são responsabilidades diferentes.", "Engineering value and appearance are different responsibilities.", "Valor de ingeniería y apariencia son responsabilidades diferentes."),
            S(Tx("Scaling", "Scaling", "Scaling"), Tx(
                "Scaling transforma o valor de engenharia antes do consumo operacional e pode afetar lógica, alarmes, histórico e telas.",
                "Scaling transforms the engineering value before operational consumption and can affect logic, alarms, history and screens.",
                "Scaling transforma el valor de ingeniería antes del consumo operacional y puede afectar lógica, alarmas, histórico y pantallas.")),
            S(Tx("Formatação", "Formatting", "Formato"), Tx(
                "Formatação controla somente a apresentação, como precisão ou texto. Não reescreve o valor canônico e não substitui scaling.",
                "Formatting controls presentation only, such as precision or text. It does not rewrite the canonical value and does not replace scaling.",
                "El formato controla solo la presentación, como precisión o texto. No reescribe el valor canónico ni sustituye scaling."))),

        Detailed(
            "sources.internal-memory", "sources",
            Tx("Internal Memory", "Internal Memory", "Internal Memory"),
            Tx("Source providers internos, não drivers de comunicação.", "Internal source providers, not communication drivers.", "Source providers internos, no drivers de comunicación."),
            S(Tx("Server Memory", "Server Memory", "Server Memory"), Tx(
                "Server Memory é memória retentiva pertencente ao servidor e aparece como source provider canônico.",
                "Server Memory is retentive server-owned memory and appears as a canonical source provider.",
                "Server Memory es memoria retentiva perteneciente al servidor y aparece como source provider canónico.")),
            S(Tx("Client Memory", "Client Memory", "Client Memory"), Tx(
                "Client Memory é memória não retentiva pertencente ao cliente Runtime. Nenhuma das duas entra na contagem dos drivers de comunicação de produção.",
                "Client Memory is non-retentive Runtime-client-owned memory. Neither memory provider counts as a production communication driver.",
                "Client Memory es memoria no retentiva perteneciente al cliente Runtime. Ninguna de las dos cuenta como driver de comunicación de producción."))),

        Basic(
            "gateway.overview", "gateway",
            Tx("TAG Gateway", "TAG Gateway", "TAG Gateway"),
            Tx("Coordenação do fluxo de TAGs.", "TAG-flow coordination.", "Coordinación del flujo de TAGs."),
            Tx(
                "TAG Gateway é conceito separado dos drivers de comunicação e não entra na contagem dos oito drivers de produção. Consumidores usam TAG Engine, cache e eventos públicos; não crie acesso privado ao driver para contornar Gateway, Authority ou lifecycle.",
                "TAG Gateway is distinct from communication drivers and does not count among the eight production drivers. Consumers use public TAG Engine, cache and events; do not create private driver access to bypass Gateway, Authority or lifecycle.",
                "TAG Gateway es distinto de los drivers de comunicación y no cuenta entre los ocho drivers de producción. Consumidores usan TAG Engine, cache y eventos públicos; no cree acceso privado al driver para evitar Gateway, Authority o lifecycle.")),

        BuildServerScriptsTopic(),

        Basic(
            "alarms.overview", "alarms",
            Tx("Alarmes", "Alarms", "Alarmas"),
            Tx("Condições operacionais de alarme.", "Operational alarm conditions.", "Condiciones operacionales de alarma."),
            Tx(
                "Alarm é domínio próprio para condições de alarme configuradas. Não use Alarm como substituto de Operational Event ou Audit.",
                "Alarm is its own domain for configured alarm conditions. Do not use Alarm as a substitute for Operational Event or Audit.",
                "Alarm es un dominio propio para condiciones de alarma configuradas. No use Alarm como sustituto de Operational Event o Audit.")),

        Basic(
            "operational-events.overview", "operational-events",
            Tx("Eventos Operacionais", "Operational Events", "Eventos Operacionales"),
            Tx("Ocorrências operacionais explícitas.", "Explicit operational occurrences.", "Ocurrencias operacionales explícitas."),
            Tx(
                "Operational Event registra uma ocorrência operacional prevista pelo contrato do produto. Não substitui estado de Alarm e não é o ledger de Audit.",
                "Operational Event records an operational occurrence defined by the product contract. It does not replace Alarm state and is not the Audit ledger.",
                "Operational Event registra una ocurrencia operacional definida por el contrato del producto. No sustituye el estado de Alarm ni es el ledger de Audit.")),

        Basic(
            "audit.overview", "audit",
            Tx("Auditoria", "Audit", "Auditoría"),
            Tx("Rastreabilidade de ações.", "Action traceability.", "Trazabilidad de acciones."),
            Tx(
                "Audit é semanticamente distinto de Alarm e Operational Event. Use-o para rastreabilidade exigida por segurança e Engineering, preservando ator, ação e contexto fornecidos pela autoridade canônica.",
                "Audit is semantically distinct from Alarm and Operational Event. Use it for traceability required by security and Engineering, preserving actor, action and context supplied by canonical authority.",
                "Audit es semánticamente distinto de Alarm y Operational Event. Úselo para trazabilidad exigida por seguridad y Engineering, preservando actor, acción y contexto suministrados por la autoridad canónica.")),

        Basic(
            "historian.overview", "historian",
            Tx("Historian", "Historian", "Historian"),
            Tx("Persistência histórica canônica.", "Canonical historical persistence.", "Persistencia histórica canónica."),
            Tx(
                "Historian recebe valores, quality e timestamps pelo pipeline canônico. Corrija problemas na fonte, TAG ou persistência; não reescreva histórico na UI.",
                "Historian receives values, quality and timestamps through the canonical pipeline. Fix problems in the source, TAG or persistence; do not rewrite history in the UI.",
                "Historian recibe valores, quality y timestamps mediante el pipeline canónico. Corrija problemas en fuente, TAG o persistencia; no reescriba histórico en la UI.")),

        Basic(
            "trends.overview", "trends",
            Tx("Trends", "Trends", "Trends"),
            Tx("Visualização temporal de TAGs.", "Time visualization of TAGs.", "Visualización temporal de TAGs."),
            Tx(
                "Configure séries com TAGs autorizados e dados atuais ou históricos canônicos. Trend apresenta dados; não substitui quality, timestamp, scaling ou Historian.",
                "Configure series with authorized TAGs and canonical current or historical data. Trend presents data; it does not replace quality, timestamp, scaling or Historian.",
                "Configure series con TAGs autorizados y datos actuales o históricos canónicos. Trend presenta datos; no sustituye quality, timestamp, scaling ni Historian.")),

        Basic(
            "reports.overview", "reports",
            Tx("Reports", "Reports", "Reports"),
            Tx("Relatórios sobre dados autorizados.", "Reports over authorized data.", "Informes sobre datos autorizados."),
            Tx(
                "Reports consultam somente dados permitidos pelo contrato e pela sessão. Filtros e formatação não concedem acesso adicional nem alteram valores canônicos.",
                "Reports query only data allowed by the contract and session. Filters and formatting grant no additional access and do not change canonical values.",
                "Reports consultan solo datos permitidos por el contrato y la sesión. Filtros y formato no conceden acceso adicional ni cambian valores canónicos.")),

        Basic(
            "screens.overview", "screens",
            Tx("Screens", "Screens", "Screens"),
            Tx("Telas operacionais do projeto.", "Project operational screens.", "Pantallas operacionales del proyecto."),
            Tx(
                "Screens referenciam TAGs, dynamos, popups, bindings e commands por contratos válidos. Uma tela não incorpora conexão privada de driver nem autoridade própria de Runtime.",
                "Screens reference TAGs, dynamos, popups, bindings and commands through valid contracts. A screen does not embed a private driver connection or its own Runtime authority.",
                "Screens referencian TAGs, dynamos, popups, bindings y commands mediante contratos válidos. Una pantalla no incorpora conexión privada de driver ni autoridad propia de Runtime.")),

        Basic(
            "popups.overview", "popups",
            Tx("Popups", "Popups", "Popups"),
            Tx("Conteúdo reutilizável no contexto operacional.", "Reusable content in operational context.", "Contenido reutilizable en contexto operacional."),
            Tx(
                "Passe contexto por contratos suportados. Abrir um Popup não aumenta capabilities; commands e escritas continuam sob a mesma autorização server-side.",
                "Pass context through supported contracts. Opening a Popup does not increase capabilities; commands and writes remain under the same server-side authorization.",
                "Pase contexto mediante contratos soportados. Abrir un Popup no aumenta capabilities; commands y escrituras siguen bajo la misma autorización server-side.")),

        Basic(
            "dynamos.overview", "dynamos",
            Tx("Dynamos", "Dynamos", "Dynamos"),
            Tx("Recursos visuais reutilizáveis.", "Reusable visual resources.", "Recursos visuales reutilizables."),
            Tx(
                "Dynamos encapsulam comportamento visual reutilizável e dependências explícitas. Instâncias usam bindings do projeto; não copie conexões privadas ou identidades acidentais de outro projeto.",
                "Dynamos encapsulate reusable visual behavior and explicit dependencies. Instances use project bindings; do not copy private connections or accidental identities from another project.",
                "Dynamos encapsulan comportamiento visual reutilizable y dependencias explícitas. Las instancias usan bindings del proyecto; no copie conexiones privadas ni identidades accidentales de otro proyecto.")),

        Detailed(
            "bindings-commands.overview", "bindings-commands",
            Tx("Bindings e Commands", "Bindings and Commands", "Bindings y Commands"),
            Tx("Ligação visual e intenção operacional.", "Visual binding and operational intent.", "Vinculación visual e intención operacional."),
            S(Tx("Bindings", "Bindings", "Bindings"), Tx(
                "Bindings conectam propriedades da UI a recursos canônicos; não duplicam TAG Engine, scaling ou lógica de Authority no navegador.",
                "Bindings connect UI properties to canonical resources; they do not duplicate TAG Engine, scaling or Authority logic in the browser.",
                "Bindings conectan propiedades de UI con recursos canónicos; no duplican TAG Engine, scaling ni lógica de Authority en el navegador.")),
            S(Tx("Commands", "Commands", "Commands"), Tx(
                "Commands representam intenção do operador e passam por validação/autorização server-side. Viewer, View Only ou ausência de capability bloqueiam a ação mesmo quando existe controle visual.",
                "Commands represent operator intent and pass server-side validation/authorization. Viewer, View Only or a missing capability block the action even when a visual control exists.",
                "Commands representan intención del operador y pasan por validación/autorización server-side. Viewer, View Only o falta de capability bloquean la acción aunque exista control visual."))),

        Basic(
            "security.users-roles-capabilities", "security",
            Tx("Usuários, roles, capabilities e segurança", "Users, roles, capabilities and security", "Usuarios, roles, capabilities y seguridad"),
            Tx("Identidade e autorização no backend.", "Backend identity and authorization.", "Identidad y autorización en backend."),
            Tx(
                "Administre identidades e roles pelos fluxos suportados. Roles alimentam a autorização; capabilities efetivas são calculadas e aplicadas pelo servidor. A UI usa capabilities para UX, nunca como único enforcement.",
                "Manage identities and roles through supported flows. Roles feed authorization; effective capabilities are calculated and enforced by the server. The UI uses capabilities for UX, never as the only enforcement.",
                "Administre identidades y roles mediante flujos soportados. Roles alimentan autorización; capabilities efectivas son calculadas y aplicadas por el servidor. La UI usa capabilities para UX, nunca como único enforcement.")),

        Basic(
            "licensing.overview", "licensing",
            Tx("Licensing", "Licensing", "Licensing"),
            Tx("Controle comercial sem substituir segurança.", "Commercial control without replacing security.", "Control comercial sin sustituir seguridad."),
            Tx(
                "Licensing pode limitar recursos comerciais, mas não substitui autenticação, autorização, Engineering Lock, lifecycle, package validation ou Runtime authority.",
                "Licensing may limit commercial features but does not replace authentication, authorization, Engineering Lock, lifecycle, package validation or Runtime authority.",
                "Licensing puede limitar funciones comerciales, pero no sustituye autenticación, autorización, Engineering Lock, lifecycle, validación de package ni Runtime authority.")),

        Detailed(
            "recovery.backup-system-recovery", "recovery",
            Tx("Backup e System Recovery", "Backup and System Recovery", "Backup y System Recovery"),
            Tx("Proteção e recuperação da Authority.", "Authority protection and recovery.", "Protección y recuperación de Authority."),
            S(Tx("Backup", "Backup", "Backup"), Tx(
                "Use o fluxo suportado para preservar dados e metadados previstos pelo contrato. Trate backup como artefato sensível e mantenha validação de formato e criptográfica quando exigida.",
                "Use the supported flow to preserve contract-defined data and metadata. Treat backup as sensitive and retain format and cryptographic validation when required.",
                "Use el flujo soportado para preservar datos y metadatos definidos por contrato. Trate backup como sensible y mantenga validación de formato y criptográfica cuando sea exigida.")),
            S(Tx("System Recovery", "System Recovery", "System Recovery"), Tx(
                "Recovery substitui estado somente pelo contrato atômico e validado. Não edite stores manualmente nem use recovery para contornar identidade, lifecycle ou Authority.",
                "Recovery replaces state only through the validated atomic contract. Do not edit stores manually or use recovery to bypass identity, lifecycle or Authority.",
                "Recovery sustituye estado solo mediante el contrato atómico validado. No edite stores manualmente ni use recovery para evitar identidad, lifecycle o Authority."))),

        Basic(
            "diagnostics.overview", "diagnostics",
            Tx("Diagnostics", "Diagnostics", "Diagnostics"),
            Tx("Diagnóstico por camadas.", "Layered diagnostics.", "Diagnóstico por capas."),
            Tx(
                "Verifique backend, autenticação/capabilities, Active, Data Source, configuração do driver, conexão, binding, quality/timestamp e só então a apresentação. Use Connection Test, Discover, Browse, Import ou Reconcile somente quando o descriptor real declarar a capability.",
                "Check backend, authentication/capabilities, Active, Data Source, driver configuration, connection, binding, quality/timestamp and only then presentation. Use Connection Test, Discover, Browse, Import or Reconcile only when the real descriptor declares the capability.",
                "Verifique backend, autenticación/capabilities, Active, Data Source, configuración del driver, conexión, binding, quality/timestamp y solo entonces presentación. Use Connection Test, Discover, Browse, Import o Reconcile solo cuando el descriptor real declare la capability.")),

        Basic(
            "troubleshooting.overview", "troubleshooting",
            Tx("Troubleshooting", "Troubleshooting", "Troubleshooting"),
            Tx("Correção da causa genérica.", "Fixing the generic cause.", "Corrección de la causa genérica."),
            Tx(
                "Reproduza o problema, identifique a camada que divergiu da autoridade canônica e corrija a causa. Não crie workaround de demo, não enfraqueça testes e não bypass Authority, Engineering Lock, licensing, lifecycle, packages ou Runtime. Em CI vermelho, diagnostique antes de rerun.",
                "Reproduce the problem, identify the layer that diverged from canonical authority and fix the cause. Do not create demo workarounds, weaken tests, or bypass Authority, Engineering Lock, licensing, lifecycle, packages or Runtime. For red CI, diagnose before rerun.",
                "Reproduzca el problema, identifique la capa que divergió de la autoridad canónica y corrija la causa. No cree workarounds de demo, debilite tests ni evite Authority, Engineering Lock, licensing, lifecycle, packages o Runtime. Con CI rojo, diagnostique antes de rerun.")),

        BuildReusableLibrariesTopic()
    };

    private static TopicDefinition BuildServerScriptsTopic() => Detailed(
        "scripts.server", "scripts",
        Tx("Server Scripts", "Server Scripts", "Server Scripts"),
        Tx("Python isolado, revision-bound e limitado à superfície real do build.", "Isolated, revision-bound Python limited to the build's real surface.", "Python aislado, revision-bound y limitado a la superficie real del build."),
        S(Tx("API suportada", "Supported API", "API soportada"), Tx(
            "A API específica deste build contém somente read_tag, read_server_memory, write_tag, write_server_memory, publish_server_memory_sample e emit_operational_event. TAGs acessados precisam ser dependências declaradas; funções de Server Memory exigem ServerMemoryTag.",
            "This build-specific API contains only read_tag, read_server_memory, write_tag, write_server_memory, publish_server_memory_sample and emit_operational_event. Accessed TAGs must be declared dependencies; Server Memory functions require ServerMemoryTag.",
            "La API específica de este build contiene solo read_tag, read_server_memory, write_tag, write_server_memory, publish_server_memory_sample y emit_operational_event. Los TAGs accedidos deben ser dependencias declaradas; funciones de Server Memory requieren ServerMemoryTag."),
            "value = read_tag(\"<stable-tag-id>\")\nwrite_tag(\"<stable-tag-id>\", value)\nemit_operational_event(\"<definition-id>\", \"message\", {\"source\": \"script\"})"),
        S(Tx("Lifecycle e triggers", "Lifecycle and triggers", "Lifecycle y triggers"), Tx(
            "Somente scripts habilitados de scope Server são hospedados na revisão Active. O host atual despacha Initialize, Dispose, TagChanged, Timer e ServerRuntimeEvent. Ao trocar Active, a geração anterior é cancelada; acesso de TAG e emissão de Operational Event usam revision gate para impedir execução obsoleta sobre uma revisão nova.",
            "Only enabled Server-scope scripts are hosted on the Active revision. The current host dispatches Initialize, Dispose, TagChanged, Timer and ServerRuntimeEvent. When Active changes, the previous generation is cancelled; TAG access and Operational Event emission use a revision gate to prevent obsolete execution against a new revision.",
            "Solo scripts habilitados de scope Server se hospedan en la revisión Active. El host actual despacha Initialize, Dispose, TagChanged, Timer y ServerRuntimeEvent. Al cambiar Active, la generación anterior se cancela; acceso TAG y emisión de Operational Event usan revision gate para impedir ejecución obsoleta sobre una revisión nueva.")),
        S(Tx("Execução e falhas", "Execution and failures", "Ejecución y fallas"), Tx(
            "A política padrão usa timeout de handler de 250 ms, fila limitada a 128 eventos, Timer mínimo de 50 ms e throttle após 5 falhas consecutivas; esses valores podem ser ajustados pela configuração ServerScripts. Fila, timeout, cancelamento, fault isolation e diagnósticos pertencem à instância do script e não concedem fallback de Authority.",
            "The default policy uses a 250 ms handler timeout, a queue bounded to 128 events, a 50 ms minimum Timer and throttling after 5 consecutive failures; ServerScripts configuration can adjust these values. Queue, timeout, cancellation, fault isolation and diagnostics belong to the script instance and grant no Authority fallback.",
            "La política por defecto usa timeout de handler de 250 ms, cola limitada a 128 eventos, Timer mínimo de 50 ms y throttle después de 5 fallas consecutivas; configuración ServerScripts puede ajustar estos valores. Cola, timeout, cancelación, fault isolation y diagnósticos pertenecen a la instancia y no conceden fallback de Authority.")),
        S(Tx("Sandbox e segurança", "Sandbox and security", "Sandbox y seguridad"), Tx(
            "A superfície de Server Script permite leitura de TAGs compartilhados, leitura/escrita de Server Memory e escrita de TAGs conforme o contrato. O sandbox nega filesystem, sistema operacional, shell/process execution, rede arbitrária, database, acesso direto a industrial drivers, secrets, browser DOM e browser storage. O preflight rejeita imports/calls obviamente proibidos, mas é feedback de editor; enforcement de sandbox não deve depender de scan de texto.",
            "The Server Script surface allows shared TAG reads, Server Memory reads/writes and TAG writes according to contract. The sandbox denies filesystem, operating system, shell/process execution, arbitrary network, database, direct industrial-driver access, secrets, browser DOM and browser storage. Preflight rejects obviously prohibited imports/calls but is editor feedback; sandbox enforcement must not depend on text scanning.",
            "La superficie Server Script permite lectura de TAGs compartidos, lectura/escritura de Server Memory y escritura de TAGs según contrato. El sandbox niega filesystem, sistema operativo, shell/process execution, red arbitraria, database, acceso directo a industrial drivers, secrets, browser DOM y browser storage. Preflight rechaza imports/calls claramente prohibidos, pero es feedback del editor; enforcement del sandbox no debe depender de escaneo de texto.")),
        S(Tx("Exemplo Server Memory", "Server Memory example", "Ejemplo Server Memory"), Tx(
            "Use somente funções expostas pelo build e referências estáveis declaradas. Não documente convenience functions históricas ou planejadas como se existissem.",
            "Use only functions exposed by the build and declared stable references. Do not document historical or planned convenience functions as if they existed.",
            "Use solo funciones expuestas por el build y referencias estables declaradas. No documente convenience functions históricas o planificadas como si existieran."),
            "value = read_server_memory(\"<stable-tag-id>\")\nwrite_server_memory(\"<stable-tag-id>\", value)\npublish_server_memory_sample(\"<stable-tag-id>\", value, \"Good\")"));

    private static TopicDefinition BuildReusableLibrariesTopic() => Detailed(
        "libraries.reusable-resources", "libraries",
        Tx("Reusable Resource Libraries", "Reusable Resource Libraries", "Reusable Resource Libraries"),
        Tx("Reuso seletivo em Engineering sem dependência externa de Runtime.", "Selective Engineering reuse without external Runtime dependency.", "Reutilización selectiva en Engineering sin dependencia externa de Runtime."),
        S(Tx("Criar e exportar .escadalib", "Create and export .escadalib", "Crear y exportar .escadalib"), Tx(
            "A exportação cria .escadalib a partir de recursos selecionados do Working e inclui automaticamente o closure de dependências. O build exporta Equipment Template, Dynamo, Screen, Popup, Script e Visual Asset. O pacote contém manifest, hashes SHA-256 e limites de segurança; Inspect valida formato, manifest, arquivos, hashes e payloads antes do uso.",
            "Export creates .escadalib from selected Working resources and automatically includes the dependency closure. The build exports Equipment Template, Dynamo, Screen, Popup, Script and Visual Asset. The package contains a manifest, SHA-256 hashes and safety limits; Inspect validates format, manifest, files, hashes and payloads before use.",
            "Export crea .escadalib desde recursos seleccionados de Working e incluye automáticamente el closure de dependencias. El build exporta Equipment Template, Dynamo, Screen, Popup, Script y Visual Asset. El paquete contiene manifest, hashes SHA-256 y límites de seguridad; Inspect valida formato, manifest, archivos, hashes y payloads antes del uso.")),
        S(Tx("Associar não é importar", "Association is not import", "Asociar no es importar"), Tx(
            ".escadalib é diferente de .escadapkg. Associar uma Library não importa conteúdo e, sozinho, não altera Working; apenas disponibiliza recursos compatíveis no catálogo de Engineering.",
            ".escadalib is different from .escadapkg. Associating a Library does not import content and, by itself, does not change Working; it only makes compatible resources available in the Engineering catalog.",
            ".escadalib es diferente de .escadapkg. Asociar una Library no importa contenido y, por sí solo, no cambia Working; solo vuelve disponibles recursos compatibles en el catálogo de Engineering.")),
        S(Tx("Usar", "Use", "Usar"), Tx(
            "Usar incorpora seletivamente o recurso escolhido e o closure validado de dependências. O conteúdo incorporado torna-se conteúdo canônico pertencente ao projeto e segue validação e lifecycle normais de Working.",
            "Use selectively incorporates the chosen resource and its validated dependency closure. Incorporated content becomes canonical project-owned content and follows normal Working validation and lifecycle.",
            "Usar incorpora selectivamente el recurso elegido y el closure validado de dependencias. El contenido incorporado pasa a ser contenido canónico del proyecto y sigue validación y lifecycle normales de Working.")),
        S(Tx("Desassociar e Runtime", "Detach and Runtime", "Desasociar y Runtime"), Tx(
            "Desassociar remove disponibilidade do catálogo, não apaga conteúdo já incorporado. O .escadapkg final permanece self-contained; Runtime e Active nunca dependem de .escadalib para executar conteúdo incorporado.",
            "Detaching removes catalog availability and does not delete already incorporated content. The final .escadapkg remains self-contained; Runtime and Active never depend on .escadalib to execute incorporated content.",
            "Desasociar elimina disponibilidad del catálogo y no borra contenido ya incorporado. El .escadapkg final permanece self-contained; Runtime y Active nunca dependen de .escadalib para ejecutar contenido incorporado."));

    private static ContextualHelpTopic BuildSourceProviderTopic(EngineeringDataSourceTypeView source, string locale) => new(
        $"source.{source.TypeKey}",
        "sources",
        source.DisplayName,
        Pick(locale, "Source provider disponível neste build.", "Source provider available in this build.", "Source provider disponible en este build."),
        new[]
        {
            new ContextualHelpSection(Pick(locale, "Identidade", "Identity", "Identidad"), $"Type key: {source.TypeKey}"),
            new ContextualHelpSection(
                Pick(locale, "Contrato", "Contract", "Contrato"),
                string.IsNullOrWhiteSpace(source.Description)
                    ? Pick(locale, "Fonte do catálogo canônico; não é driver de comunicação.", "Canonical-catalog source; it is not a communication driver.", "Fuente del catálogo canónico; no es driver de comunicación.")
                    : source.Description)
        });

    private static ContextualHelpTopic BuildDriverTopic(EngineeringDataSourceTypeView driver, string locale)
    {
        var schema = driver.ConfigurationSchema;
        var sourceFields = schema?.DataSourceFields ?? Array.Empty<EngineeringDriverConfigurationFieldView>();
        var bindingFields = schema?.TagBindingFields ?? Array.Empty<EngineeringDriverConfigurationFieldView>();
        var allFields = sourceFields.Concat(bindingFields).ToArray();
        var schemaIdentity = schema is null
            ? Pick(locale, "Nenhum configuration schema declarado.", "No configuration schema declared.", "No hay configuration schema declarado.")
            : $"{schema.SchemaId} v{schema.SchemaVersion}; TagBinding: {driver.TagBindingSchemaId ?? schema.SchemaId} v{driver.TagBindingSchemaVersion ?? schema.SchemaVersion}";

        return new ContextualHelpTopic(
            $"driver.{driver.TypeKey}",
            "drivers",
            driver.DisplayName,
            Pick(locale,
                "Driver de comunicação de produção registrado neste build.",
                "Production communication driver registered in this build.",
                "Driver de comunicación de producción registrado en este build."),
            new[]
            {
                new ContextualHelpSection(
                    Pick(locale, "Finalidade e perfil comprovado", "Proven purpose and profile", "Finalidad y perfil comprobado"),
                    $"Type key: {driver.TypeKey}\n{(string.IsNullOrWhiteSpace(driver.Description) ? Pick(locale, "Sem descrição adicional no descriptor.", "No additional descriptor description.", "Sin descripción adicional en el descriptor.") : driver.Description)}\n{schemaIdentity}"),
                new ContextualHelpSection(
                    Pick(locale, "Configuração do Data Source", "Data Source configuration", "Configuración del Data Source"),
                    FormatFields(sourceFields, locale)),
                new ContextualHelpSection(
                    Pick(locale, "Addressing / binding do TAG", "TAG addressing / binding", "Addressing / binding del TAG"),
                    FormatFields(bindingFields, locale)),
                new ContextualHelpSection(
                    Pick(locale, "Data types e mapping", "Data types and mapping", "Data types y mapping"),
                    DescribeMatchingFields(allFields, locale, "type", "datatype", "dataType", "encoding", "format", "representation")),
                new ContextualHelpSection(
                    Pick(locale, "Acesso a bit", "Bit access", "Acceso a bit"),
                    DescribeMatchingFields(allFields, locale, "bit", "mask", "offset")),
                new ContextualHelpSection(
                    Pick(locale, "Byte/word order", "Byte/word order", "Byte/word order"),
                    DescribeMatchingFields(allFields, locale, "endian", "byteorder", "byteOrder", "wordorder", "wordOrder", "swap")),
                new ContextualHelpSection(
                    Pick(locale, "Read/write e restrições", "Read/write and restrictions", "Read/write y restricciones"),
                    $"{DescribeMatchingFields(allFields, locale, "read", "write", "access", "readonly", "readOnly")}\n{Pick(locale, "Não infira writeability além do contrato do driver/TAG. Escritas continuam sujeitas às capabilities efetivas e ao enforcement server-side.", "Do not infer writeability beyond the driver/TAG contract. Writes remain subject to effective capabilities and server-side enforcement.", "No infiera writeability más allá del contrato driver/TAG. Las escrituras siguen sujetas a capabilities efectivas y enforcement server-side.")}"),
                new ContextualHelpSection(
                    Pick(locale, "Polling/subscription", "Polling/subscription", "Polling/subscription"),
                    DescribeMatchingFields(allFields, locale, "poll", "scan", "interval", "subscription", "sample", "publish", "report")),
                new ContextualHelpSection(
                    Pick(locale, "Reconnect e timeouts", "Reconnect and timeouts", "Reconnect y timeouts"),
                    DescribeMatchingFields(allFields, locale, "timeout", "retry", "reconnect", "keepalive", "keepAlive", "session")),
                new ContextualHelpSection(
                    Pick(locale, "Segurança e certificados", "Security and certificates", "Seguridad y certificados"),
                    DescribeSecurityFields(allFields, locale)),
                new ContextualHelpSection(
                    Pick(locale, "Capabilities de Engineering", "Engineering capabilities", "Capabilities de Engineering"),
                    DescribeEngineeringCapabilities(driver, locale)),
                new ContextualHelpSection(
                    Pick(locale, "Exemplos válidos e inválidos", "Valid and invalid examples", "Ejemplos válidos e inválidos"),
                    BuildValidationExamples(allFields, locale)),
                new ContextualHelpSection(
                    Pick(locale, "Quality, timestamps e writeability", "Quality, timestamps and writeability", "Quality, timestamps y writeability"),
                    Pick(locale,
                        "Preserve quality e timestamps recebidos pelo pipeline canônico. A UI não inventa Good, horário ou permissão de escrita para mascarar falha de fonte ou sessão.",
                        "Preserve quality and timestamps received through the canonical pipeline. The UI does not invent Good, time or write permission to mask a source or session failure.",
                        "Preserve quality y timestamps recibidos por el pipeline canónico. La UI no inventa Good, tiempo ni permiso de escritura para ocultar falla de fuente o sesión.")),
                new ContextualHelpSection(
                    Pick(locale, "Diagnóstico e troubleshooting", "Diagnostics and troubleshooting", "Diagnóstico y troubleshooting"),
                    Pick(locale,
                        "Valide primeiro campos obrigatórios, formatos, limites e referências protegidas; depois use somente capabilities declaradas pelo descriptor. Corrija conexão, discovery ou binding na fonte, nunca por fallback visual.",
                        "Validate required fields, formats, limits and protected references first; then use only capabilities declared by the descriptor. Fix connection, discovery or binding at the source, never through visual fallback.",
                        "Valide primero campos obligatorios, formatos, límites y referencias protegidas; luego use solo capabilities declaradas por el descriptor. Corrija conexión, discovery o binding en la fuente, nunca mediante fallback visual.")),
                new ContextualHelpSection(
                    Pick(locale, "Limites de interoperabilidade", "Interoperability limits", "Límites de interoperabilidad"),
                    Pick(locale,
                        "Este manual afirma somente o TypeKey, descrição, schemas, campos, formatos e capabilities registrados neste build. Perfil, extensão ou comportamento não declarado pelo descriptor/schema não deve ser inferido como suportado.",
                        "This manual claims only the TypeKey, description, schemas, fields, formats and capabilities registered in this build. A profile, extension or behavior not declared by the descriptor/schema must not be inferred as supported.",
                        "Este manual afirma solo TypeKey, descripción, schemas, campos, formatos y capabilities registrados en este build. Perfil, extensión o comportamiento no declarado por descriptor/schema no debe inferirse como soportado."))
            });
    }

    private static string DescribeEngineeringCapabilities(EngineeringDataSourceTypeView driver, string locale)
    {
        var items = new[]
        {
            driver.Capabilities.SupportsConnectionTest ? Pick(locale, "Teste de conexão", "Connection test", "Prueba de conexión") : null,
            driver.Capabilities.SupportsDiscovery ? Pick(locale, "Descoberta", "Discovery", "Descubrimiento") : null,
            driver.Capabilities.SupportsBrowse ? "Browse" : null,
            driver.Capabilities.SupportsFileImport ? Pick(locale, "Importação de arquivo", "File import", "Importación de archivo") : null,
            driver.Capabilities.SupportsReconcile ? Pick(locale, "Reconciliação", "Reconcile", "Reconciliación") : null,
            driver.Capabilities.SupportsSharedTransportInfrastructure ? Pick(locale, "Transporte compartilhado", "Shared transport", "Transporte compartido") : null
        }.Where(value => value is not null).Cast<string>().ToArray();

        return items.Length == 0
            ? Pick(locale, "Nenhuma capability opcional de Engineering declarada pelo descriptor.", "No optional Engineering capability declared by the descriptor.", "Ninguna capability opcional de Engineering declarada por el descriptor.")
            : string.Join("\n", items.Select(item => $"• {item}"));
    }

    private static string DescribeSecurityFields(IReadOnlyCollection<EngineeringDriverConfigurationFieldView> fields, string locale)
    {
        var selected = fields.Where(field =>
            field.ValueKind is "secretReference" or "certificateReference" ||
            Matches(field, "security", "certificate", "cert", "tls", "secret", "password", "username", "userName", "auth", "credential"))
            .ToArray();

        if (selected.Length == 0)
            return Pick(locale,
                "O schema não declara configuração específica de segurança/certificado. Isso não reduz autenticação, autorização ou demais controles do produto.",
                "The schema declares no specific security/certificate configuration. This does not reduce authentication, authorization or other product controls.",
                "El schema no declara configuración específica de seguridad/certificado. Esto no reduce autenticación, autorización ni otros controles del producto.");

        return FormatFields(selected, locale);
    }

    private static string DescribeMatchingFields(
        IReadOnlyCollection<EngineeringDriverConfigurationFieldView> fields,
        string locale,
        params string[] keywords)
    {
        var selected = fields.Where(field => Matches(field, keywords)).ToArray();
        return selected.Length == 0
            ? Pick(locale,
                "O descriptor/schema deste build não declara campos específicos para este aspecto; não inferir comportamento adicional.",
                "This build's descriptor/schema declares no specific fields for this aspect; do not infer additional behavior.",
                "El descriptor/schema de este build no declara campos específicos para este aspecto; no infiera comportamiento adicional.")
            : FormatFields(selected, locale);
    }

    private static bool Matches(EngineeringDriverConfigurationFieldView field, params string[] keywords)
    {
        var haystack = $"{field.Key} {field.DisplayName} {field.Description} {field.ExpectedFormat}";
        return keywords.Any(keyword => haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildValidationExamples(IReadOnlyCollection<EngineeringDriverConfigurationFieldView> fields, string locale)
    {
        var valid = fields
            .Where(field => !string.IsNullOrWhiteSpace(field.ExampleValue) && !IsExternalHttpValue(field.ExampleValue!))
            .Take(6)
            .Select(field => $"{field.Key}={field.ExampleValue}")
            .ToArray();

        var invalid = new List<string>();
        foreach (var field in fields)
        {
            if (invalid.Count >= 6) break;
            if (field.Required && string.IsNullOrWhiteSpace(field.DefaultValue))
                invalid.Add($"{field.Key}=<missing>");
            else if (field.AllowedValues.Count > 0)
                invalid.Add($"{field.Key}=<outside: {string.Join(" | ", field.AllowedValues)}>");
            else if (field.Minimum.HasValue || field.Maximum.HasValue)
                invalid.Add($"{field.Key}=<outside {field.Minimum?.ToString() ?? "-∞"}..{field.Maximum?.ToString() ?? "+∞"}>");
            else if (!string.IsNullOrWhiteSpace(field.ExpectedFormat))
                invalid.Add($"{field.Key}=<malformed; expected {field.ExpectedFormat}>");
        }

        var validText = valid.Length == 0
            ? Pick(locale, "Nenhum exemplo de valor não sensível declarado pelo schema.", "No non-sensitive value example declared by the schema.", "Ningún ejemplo de valor no sensible declarado por el schema.")
            : string.Join("; ", valid);
        var invalidText = invalid.Count == 0
            ? Pick(locale, "Use o validator canônico para rejeitar campos desconhecidos e formatos incompatíveis.", "Use the canonical validator to reject unknown fields and incompatible formats.", "Use el validator canónico para rechazar campos desconocidos y formatos incompatibles.")
            : string.Join("; ", invalid);

        return $"{Pick(locale, "Válidos derivados do schema", "Valid, derived from schema", "Válidos derivados del schema")}: {validText}\n{Pick(locale, "Inválidos derivados das regras", "Invalid, derived from rules", "Inválidos derivados de las reglas")}: {invalidText}";
    }

    private static string FormatFields(IReadOnlyCollection<EngineeringDriverConfigurationFieldView> fields, string locale)
    {
        if (fields.Count == 0)
            return Pick(locale, "Nenhum campo declarado neste schema.", "No fields declared in this schema.", "No hay campos declarados en este schema.");
        return string.Join("\n", fields.Select(field => FormatField(field, locale)));
    }

    private static string FormatField(EngineeringDriverConfigurationFieldView field, string locale)
    {
        var required = field.Required ? Pick(locale, "obrigatório", "required", "obligatorio") : Pick(locale, "opcional", "optional", "opcional");
        var parts = new List<string> { $"{field.Key} - {field.DisplayName} [{field.ValueKind}, {required}]" };
        if (!string.IsNullOrWhiteSpace(field.Description)) parts.Add(field.Description);
        if (!string.IsNullOrWhiteSpace(field.ExpectedFormat)) parts.Add($"{Pick(locale, "Formato", "Format", "Formato")}: {field.ExpectedFormat}");
        if (!string.IsNullOrWhiteSpace(field.DefaultValue)) parts.Add($"Default: {field.DefaultValue}");
        if (field.AllowedValues.Count > 0) parts.Add($"{Pick(locale, "Valores", "Values", "Valores")}: {string.Join(" | ", field.AllowedValues)}");
        if (field.Minimum.HasValue || field.Maximum.HasValue) parts.Add($"{Pick(locale, "Limites", "Limits", "Límites")}: {field.Minimum?.ToString() ?? "-∞"} .. {field.Maximum?.ToString() ?? "+∞"}");
        if (!string.IsNullOrWhiteSpace(field.ExampleValue) && !IsExternalHttpValue(field.ExampleValue)) parts.Add($"{Pick(locale, "Exemplo", "Example", "Ejemplo")}: {field.ExampleValue}");
        if (field.Advanced) parts.Add(Pick(locale, "campo avançado", "advanced field", "campo avanzado"));
        return string.Join("; ", parts);
    }

    private static bool IsExternalHttpValue(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static TopicDefinition Basic(string id, string category, LocalizedText title, LocalizedText summary, LocalizedText body) =>
        Detailed(id, category, title, summary, S(Tx("Guia", "Guide", "Guía"), body));

    private static TopicDefinition Detailed(string id, string category, LocalizedText title, LocalizedText summary, params SectionDefinition[] sections) =>
        new(id, category, title, summary, sections);

    private static SectionDefinition S(LocalizedText heading, LocalizedText body, string? code = null) => new(heading, body, code);
    private static LocalizedText Tx(string pt, string en, string es) => new(pt, en, es);

    private static string Pick(string locale, string pt, string en, string es) => locale switch
    {
        "en" => en,
        "es" => es,
        _ => pt
    };

    private sealed record LocalizedText(string Portuguese, string English, string Spanish)
    {
        public string For(string locale) => locale switch { "en" => English, "es" => Spanish, _ => Portuguese };
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
            if (string.IsNullOrWhiteSpace(topic)) return Results.Ok(catalog);

            var selected = catalog.Topics.FirstOrDefault(item => item.Id.Equals(topic, StringComparison.Ordinal));
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
