using Scada.Api.Security;
using Scada.DriverHost.Engineering;

namespace Scada.Api.Runtime;

public sealed record ContextualHelpSection(string Heading, string Body, string? Code = null);

public sealed record ContextualHelpTopic(
    string Id,
    string Category,
    string Title,
    string Summary,
    IReadOnlyCollection<ContextualHelpSection> Sections,
    IReadOnlyCollection<string>? RelatedTopicIds = null);

public sealed record ContextualHelpScriptApi(
    string Name,
    string Signature,
    string Parameters,
    string Result,
    string Safety,
    string Example);

public sealed record ContextualHelpCatalogView(
    string Locale,
    IReadOnlyCollection<string> SupportedLocales,
    IReadOnlyCollection<ContextualHelpTopic> Topics,
    IReadOnlyCollection<string> ServerScriptApi,
    IReadOnlyCollection<ContextualHelpScriptApi> ServerScriptApiDetails);

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
        "libraries.reusable-resources",
        "getting-started.neutral-bootstrap",
        "engineering.shell-navigation",
        "drivers.overview",
        "tags.copy-duplicate-sequential",
        "visual.properties",
        "visual.dynamics",
        "visual.events",
        "scripts.engineering",
        "scripts.python-validation",
        "engineering.object-browser",
        "memory.client",
        "security.scopes-authority",
        "security.engineering-lock",
        "licensing.generator",
        "runtime.session-classes",
        "application.export-import"
    };

    private static readonly IReadOnlyCollection<TopicDefinition> ManualTopics = BuildManualTopics()
        .Concat(BuildPhase1TaskTopics())
        .GroupBy(topic => topic.Id, StringComparer.Ordinal)
        .Select(group => group.Last())
        .ToArray();

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
            ServerScriptApiFunctions,
            BuildServerScriptApi(locale));
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
                "O backend e a revisão Active são autoridade canônica. A sessão recebe capabilities efetivas e lease do servidor. viewOnly reduz capacidades; comandos e escritas continuam bloqueados server-side quando não autorizados. Perda ou expiração do lease significa perda de autoridade interativa, nunca permissão implícita.",
                "The backend and Active revision are canonical authority. The session receives effective capabilities and a server lease. viewOnly reduces capabilities; commands and writes remain server-blocked when unauthorized. Lease loss or expiry means loss of interactive authority, never implicit permission.",
                "El backend y la revisión Active son autoridad canónica. La sesión recibe capabilities efectivas y lease del servidor. viewOnly reduce capabilities; comandos y escrituras siguen bloqueados server-side cuando no están autorizados. La pérdida o expiración del lease significa pérdida de autoridad interactiva, nunca permiso implícito.")),

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
                "Um controle visível não torna um TAG gravável. A escrita depende do contrato do TAG/fonte e das capabilities efetivas, com enforcement server-side. viewOnly ou ausência da capability correspondente permanecem incapazes de escrever.",
                "A visible control does not make a TAG writable. Writing depends on the TAG/source contract and effective capabilities, with server-side enforcement. viewOnly or a missing capability remain unable to write.",
                "Un control visible no vuelve un TAG escribible. La escritura depende del contrato TAG/fuente y de las capabilities efectivas, con enforcement server-side. viewOnly o falta de capability siguen sin poder escribir.")),

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
                "Commands representam intenção do operador e passam por validação/autorização server-side. viewOnly ou ausência de capability bloqueiam a ação mesmo quando existe controle visual.",
                "Commands represent operator intent and pass server-side validation/authorization. viewOnly or a missing capability block the action even when a visual control exists.",
                "Commands representan intención del operador y pasan por validación/autorización server-side. viewOnly o falta de capability bloquean la acción aunque exista control visual."))),

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
                "Reproduza o problema, identifique a primeira camada que divergiu do estado esperado e corrija a causa. Não contorne Authority, Engineering Lock, Licensing, lifecycle, packages ou Runtime. Registre a mensagem e o contexto antes de repetir a ação.",
                "Reproduce the problem, identify the first layer that differs from the expected state, and fix the cause. Do not bypass Authority, Engineering Lock, Licensing, lifecycle, packages, or Runtime. Record the message and context before repeating the action.",
                "Reproduzca el problema, identifique la primera capa que difiere del estado esperado y corrija la causa. No eluda Authority, Engineering Lock, Licensing, lifecycle, packages ni Runtime. Registre el mensaje y el contexto antes de repetir la acción.")),

        BuildReusableLibrariesTopic()
    };


    private static IReadOnlyCollection<TopicDefinition> BuildPhase1TaskTopics() => new[]
    {
        TaskGuide(
            "engineering.lifecycle", "engineering",
            Tx("Working, Save, Revision, Publish e Activate", "Working, Save, Revision, Publish and Activate", "Working, Save, Revision, Publish y Activate"),
            Tx("Ciclo que leva uma edição de Working até a revisão executada no Runtime.", "Lifecycle that takes a Working edit to the revision executed by Runtime.", "Ciclo que lleva una edición de Working hasta la revisión ejecutada por Runtime."),
            Tx("Use ao salvar trabalho, criar uma Revision, publicar ou ativar uma revisão.", "Use when saving work, creating a Revision, publishing, or activating a revision.", "Úselo al guardar trabajo, crear una Revision, publicar o activar una revisión."),
            Tx("Tenha o projeto correto aberto, alterações de Working validadas e permissão para a transição desejada.", "Open the correct project, validate Working changes, and have permission for the intended transition.", "Abra el proyecto correcto, valide los cambios de Working y tenga permiso para la transición deseada."),
            Tx("Edite em Working; use Save; crie a Revision; use Publish quando a revisão estiver pronta; use Activate somente quando ela deve assumir a operação.", "Edit in Working; use Save; create the Revision; use Publish when the revision is ready; use Activate only when it should take over operation.", "Edite en Working; use Save; cree la Revision; use Publish cuando la revisión esté lista; use Activate solo cuando deba asumir la operación."),
            Tx("A alteração permanece em Working até as transições explícitas; Publish não ativa sozinho; Activate muda a revisão operacional.", "The change stays in Working until explicit transitions occur; Publish does not activate by itself; Activate changes the operational revision.", "El cambio permanece en Working hasta las transiciones explícitas; Publish no activa por sí solo; Activate cambia la revisión operativa."),
            Tx("Ações desabilitadas, conflito de versão, validação pendente ou revisão diferente da esperada.", "Disabled actions, version conflict, pending validation, or a revision different from the expected one.", "Acciones deshabilitadas, conflicto de versión, validación pendiente o revisión diferente de la esperada."),
            Tx("Confirme projeto e estado de Working, revise mensagens de validação e recarregue o estado antes de repetir uma transição rejeitada.", "Confirm the project and Working state, review validation messages, and reload state before repeating a rejected transition.", "Confirme el proyecto y el estado de Working, revise los mensajes de validación y recargue el estado antes de repetir una transición rechazada."),
            "engineering.shell-navigation", "diagnostics.overview"),

        TaskGuide(
            "packages.escadapkg", "packages",
            Tx("Pacotes .escadapkg", ".escadapkg packages", "Paquetes .escadapkg"),
            Tx("Pacote portátil e autocontido de uma aplicação EliteSCADA.", "Portable self-contained package for an EliteSCADA application.", "Paquete portátil y autocontenido de una aplicación EliteSCADA."),
            Tx("Use para transportar uma aplicação entre instalações ou manter um artefato de entrega.", "Use it to move an application between installations or keep a delivery artifact.", "Úselo para mover una aplicación entre instalaciones o conservar un artefacto de entrega."),
            Tx("Tenha acesso de Engineering e preserve uma cópia segura do estado atual antes de aplicar um pacote em um projeto existente.", "Have Engineering access and keep a safe copy of the current state before applying a package to an existing project.", "Tenga acceso de Engineering y conserve una copia segura del estado actual antes de aplicar un paquete a un proyecto existente."),
            Tx("Exporte quando precisar transportar; ao importar, inspecione o pacote, execute Preview e aplique somente se o resultado estiver correto.", "Export when portability is needed; on import, inspect the package, run Preview, and apply only when the result is correct.", "Exporte cuando necesite portabilidad; al importar, inspeccione el paquete, ejecute Preview y aplique solo si el resultado es correcto."),
            Tx("O conteúdo validado entra no fluxo normal de Engineering; Apply não equivale a Activate.", "Validated content enters the normal Engineering flow; Apply is not Activate.", "El contenido validado entra en el flujo normal de Engineering; Apply no equivale a Activate."),
            Tx("Pacote inválido, conflito com o projeto atual, referências ausentes ou Preview com erros.", "Invalid package, conflict with the current project, missing references, or Preview errors.", "Paquete inválido, conflicto con el proyecto actual, referencias ausentes o errores de Preview."),
            Tx("Use Inspect e Preview para localizar o item rejeitado; corrija a origem ou a configuração do projeto antes de Apply.", "Use Inspect and Preview to locate the rejected item; fix the source or project configuration before Apply.", "Use Inspect y Preview para localizar el elemento rechazado; corrija el origen o la configuración del proyecto antes de Apply."),
            "application.export-import", "engineering.lifecycle"),

        TaskGuide(
            "diagnostics.overview", "diagnostics",
            Tx("Diagnostics", "Diagnostics", "Diagnostics"),
            Tx("Ferramentas para localizar falhas por camada sem alterar a operação.", "Tools for locating failures by layer without changing operation.", "Herramientas para localizar fallas por capa sin alterar la operación."),
            Tx("Use quando uma tela, TAG, Data Source, Driver, Script ou sessão não apresentar o resultado esperado.", "Use when a screen, TAG, Data Source, Driver, Script, or session does not show the expected result.", "Úselo cuando una pantalla, TAG, Data Source, Driver, Script o sesión no muestre el resultado esperado."),
            Tx("Reproduza o problema e anote o recurso afetado, horário, usuário e ação que falhou.", "Reproduce the problem and note the affected resource, time, user, and failed action.", "Reproduzca el problema y anote el recurso afectado, la hora, el usuario y la acción fallida."),
            Tx("Verifique sessão e permissões; depois Working/Active; Data Source e Driver; binding do TAG; quality/timestamp; por fim a apresentação. Use testes de conexão ou descoberta apenas quando disponíveis.", "Check session and permissions; then Working/Active; Data Source and Driver; TAG binding; quality/timestamp; finally presentation. Use connection tests or discovery only when available.", "Verifique sesión y permisos; después Working/Active; Data Source y Driver; binding del TAG; quality/timestamp; por último la presentación. Use pruebas de conexión o descubrimiento solo cuando estén disponibles."),
            Tx("Você identifica a primeira camada que diverge do esperado e corrige a causa sem mascarar o erro.", "You identify the first layer that differs from the expected state and fix the cause without masking the error.", "Usted identifica la primera capa que difiere de lo esperado y corrige la causa sin ocultar el error."),
            Tx("Permissão negada, fonte desconectada, configuração inválida, quality ruim, dado sem atualização ou estado de Working/Active diferente.", "Permission denied, disconnected source, invalid configuration, bad quality, stale data, or an unexpected Working/Active state.", "Permiso denegado, fuente desconectada, configuración inválida, quality deficiente, dato sin actualización o estado Working/Active inesperado."),
            Tx("Comece pela mensagem mais próxima do usuário, confirme o estado no Diagnostics e avance para a fonte somente quando a camada anterior estiver correta.", "Start with the message closest to the user, confirm the state in Diagnostics, and move toward the source only after the previous layer is correct.", "Comience con el mensaje más cercano al usuario, confirme el estado en Diagnostics y avance hacia la fuente solo cuando la capa anterior esté correcta."),
            "troubleshooting.overview", "sources.data-sources", "tags.overview"),

        TaskGuide(
            "recovery.backup-system-recovery", "recovery",
            Tx("Backup e System Recovery", "Backup and System Recovery", "Backup y System Recovery"),
            Tx("Fluxos protegidos para preservar e recuperar Application e Authority conforme o tipo de backup.", "Protected flows for preserving and recovering Application and Authority according to the backup type.", "Flujos protegidos para preservar y recuperar Application y Authority según el tipo de backup."),
            Tx("Use antes de mudanças relevantes e quando precisar restaurar um estado previamente exportado pelo produto.", "Use before significant changes and when a state previously exported by the product must be restored.", "Úselo antes de cambios importantes y cuando deba restaurar un estado exportado previamente por el producto."),
            Tx("Tenha acesso administrativo apropriado, o backup correto e a credencial de proteção quando o arquivo exigir.", "Have the appropriate administrative access, the correct backup, and the protection credential when the file requires it.", "Tenga el acceso administrativo adecuado, el backup correcto y la credencial de protección cuando el archivo la requiera."),
            Tx("Crie ou selecione o backup pelo fluxo suportado; verifique o arquivo; execute a recuperação pelo produto; autentique novamente quando solicitado.", "Create or select the backup through the supported flow; verify the file; run recovery through the product; authenticate again when requested.", "Cree o seleccione el backup mediante el flujo soportado; verifique el archivo; ejecute la recuperación desde el producto; autentíquese nuevamente cuando se solicite."),
            Tx("O estado é substituído de forma controlada e sessões antigas não ganham autoridade sobre o estado restaurado.", "State is replaced in a controlled way and old sessions do not gain authority over the restored state.", "El estado se reemplaza de forma controlada y las sesiones antiguas no obtienen autoridad sobre el estado restaurado."),
            Tx("Arquivo incorreto, proteção inválida, backup corrompido ou tentativa de restaurar dados incompatíveis.", "Wrong file, invalid protection credential, corrupted backup, or an attempt to restore incompatible data.", "Archivo incorrecto, credencial de protección inválida, backup corrupto o intento de restaurar datos incompatibles."),
            Tx("Interrompa a recuperação quando houver rejeição; confirme origem e tipo do backup e não edite stores, tokens ou material de assinatura manualmente.", "Stop recovery when it is rejected; confirm the backup source and type and do not manually edit stores, tokens, or signing material.", "Detenga la recuperación cuando sea rechazada; confirme el origen y el tipo del backup y no edite manualmente stores, tokens ni material de firma."),
            "application.export-import", "security.users-roles-capabilities"),

        TaskGuide(
            "getting-started.neutral-bootstrap", "getting-started",
            Tx("Inicialização neutra", "Neutral bootstrap", "Inicialización neutral"),
            Tx("Estado inicial em que nenhuma aplicação é assumida automaticamente.", "Initial state where no application is assumed automatically.", "Estado inicial en el que no se asume automáticamente ninguna aplicación."),
            Tx("Use quando o produto iniciar sem uma aplicação selecionada e oferecer criação, importação ou recuperação.", "Use when the product starts without a selected application and offers create, import, or recovery.", "Úselo cuando el producto inicie sin una aplicación seleccionada y ofrezca crear, importar o recuperar."),
            Tx("Conclua autenticação ou bootstrap administrativo quando solicitado.", "Complete authentication or administrative bootstrap when requested.", "Complete la autenticación o el bootstrap administrativo cuando se solicite."),
            Tx("Escolha a opção apresentada pelo produto: criar um projeto, importar uma aplicação ou recuperar um backup válido.", "Choose the option presented by the product: create a project, import an application, or recover a valid backup.", "Elija la opción presentada por el producto: crear un proyecto, importar una aplicación o recuperar un backup válido."),
            Tx("Um Working explícito passa a existir; nada é tratado como Active sem a transição de lifecycle correspondente.", "An explicit Working state is created; nothing is treated as Active without the corresponding lifecycle transition.", "Se crea un estado Working explícito; nada se trata como Active sin la transición de lifecycle correspondiente."),
            Tx("Opções ausentes, autenticação pendente ou artefato de importação/recuperação rejeitado.", "Missing options, pending authentication, or a rejected import/recovery artifact.", "Opciones ausentes, autenticación pendiente o artefacto de importación/recuperación rechazado."),
            Tx("Confirme autenticação, permissões e saúde do serviço; valide o arquivo pela própria tela antes de tentar novamente.", "Confirm authentication, permissions, and service health; validate the file through the product screen before trying again.", "Confirme autenticación, permisos y salud del servicio; valide el archivo desde la propia pantalla antes de intentarlo nuevamente."),
            "getting-started.startup-authentication", "engineering.lifecycle", "application.export-import"),

        TaskGuide(
            "engineering.shell-navigation", "engineering",
            Tx("Área de Engineering", "Engineering workspace", "Área de Engineering"),
            Tx("Área para navegar pelas tarefas de configuração, edição, segurança e diagnóstico do projeto.", "Workspace for navigating project configuration, editing, security, and diagnostic tasks.", "Área para navegar por tareas de configuración, edición, seguridad y diagnóstico del proyecto."),
            Tx("Use durante configuração e manutenção do projeto.", "Use during project configuration and maintenance.", "Úsela durante la configuración y el mantenimiento del proyecto."),
            Tx("Tenha uma sessão com acesso de Engineering e o projeto correto aberto.", "Have an Engineering-enabled session and the correct project open.", "Tenga una sesión con acceso de Engineering y el proyecto correcto abierto."),
            Tx("Escolha a seção no menu; confira projeto e estado do Workspace no cabeçalho; execute a tarefa na área central; salve ou aplique somente quando a tela indicar.", "Choose a section in the menu; verify project and Workspace state in the header; perform the task in the main area; save or apply only when the screen indicates.", "Elija una sección en el menú; verifique proyecto y estado del Workspace en el encabezado; realice la tarea en el área central; guarde o aplique solo cuando la pantalla lo indique."),
            Tx("A seção selecionada mostra dados e ações do projeto sem mudar silenciosamente o lifecycle.", "The selected section shows project data and actions without silently changing lifecycle state.", "La sección seleccionada muestra datos y acciones del proyecto sin cambiar silenciosamente el lifecycle."),
            Tx("Seção indisponível, dados não carregados ou ação bloqueada por permissão/Engineering Lock.", "Unavailable section, unloaded data, or an action blocked by permission/Engineering Lock.", "Sección no disponible, datos no cargados o acción bloqueada por permiso/Engineering Lock."),
            Tx("Confirme a sessão, o projeto e o estado do Engineering Lock; use Diagnostics para falhas de carregamento.", "Confirm the session, project, and Engineering Lock state; use Diagnostics for loading failures.", "Confirme la sesión, el proyecto y el estado de Engineering Lock; use Diagnostics para fallas de carga."),
            "engineering.lifecycle", "diagnostics.overview"),

        TaskGuide(
            "drivers.overview", "drivers",
            Tx("Drivers de comunicação", "Communication Drivers", "Drivers de comunicación"),
            Tx("Integrações industriais que conectam Data Sources a protocolos e equipamentos suportados.", "Industrial integrations that connect Data Sources to supported protocols and equipment.", "Integraciones industriales que conectan Data Sources con protocolos y equipos soportados."),
            Tx("Use ao criar uma Data Source que se comunica com equipamento ou software externo.", "Use when creating a Data Source that communicates with external equipment or software.", "Úselos al crear una Data Source que se comunica con equipos o software externo."),
            Tx("Conheça o protocolo, endpoint e parâmetros exigidos pelo equipamento e tenha acesso de Engineering.", "Know the protocol, endpoint, and parameters required by the equipment and have Engineering access.", "Conozca el protocolo, endpoint y parámetros requeridos por el equipo y tenga acceso de Engineering."),
            Tx("Escolha o Driver pelo catálogo; preencha somente os campos mostrados; salve; use Connection Test, Discovery, Browse, Import ou Reconcile apenas quando a tela disponibilizar.", "Choose the Driver from the catalog; fill only the fields shown; save; use Connection Test, Discovery, Browse, Import, or Reconcile only when the screen provides it.", "Elija el Driver del catálogo; complete solo los campos mostrados; guarde; use Connection Test, Discovery, Browse, Import o Reconcile solo cuando la pantalla lo ofrezca."),
            Tx("A Data Source fica configurada e os TAGs podem usar o binding oferecido pelo Driver.", "The Data Source is configured and TAGs can use the binding offered by the Driver.", "La Data Source queda configurada y los TAGs pueden usar el binding ofrecido por el Driver."),
            Tx("Endpoint inacessível, credencial/certificado inválido, campo obrigatório ausente ou binding incompatível.", "Unreachable endpoint, invalid credential/certificate, missing required field, or incompatible binding.", "Endpoint inaccesible, credencial/certificado inválido, campo obligatorio ausente o binding incompatible."),
            Tx("Abra o tópico específico do Driver e siga seus campos, capacidades e diagnóstico; não copie sintaxe de outro protocolo.", "Open the Driver-specific topic and follow its fields, capabilities, and diagnostics; do not copy syntax from another protocol.", "Abra el tema específico del Driver y siga sus campos, capacidades y diagnóstico; no copie sintaxis de otro protocolo."),
            "sources.data-sources", "tags.addressing", "diagnostics.overview"),

        TaskGuide(
            "tags.copy-duplicate-sequential", "tags",
            Tx("Copiar, colar, duplicar e gerar TAGs em sequência", "Copy, paste, duplicate, and generate sequential TAGs", "Copiar, pegar, duplicar y generar TAGs en secuencia"),
            Tx("Ferramentas para criar novos TAGs a partir de configuração existente sem copiar estado de Runtime.", "Tools for creating new TAGs from existing configuration without copying Runtime state.", "Herramientas para crear nuevos TAGs desde configuración existente sin copiar estado de Runtime."),
            Tx("Use para repetir configurações semelhantes ou criar uma sequência Modbus.", "Use to repeat similar configurations or create a Modbus sequence.", "Úselas para repetir configuraciones similares o crear una secuencia Modbus."),
            Tx("Selecione um ou mais TAGs; para sequência, selecione exatamente um TAG ligado a uma Data Source Modbus TCP.", "Select one or more TAGs; for a sequence, select exactly one TAG bound to a Modbus TCP Data Source.", "Seleccione uno o más TAGs; para una secuencia, seleccione exactamente un TAG ligado a una Data Source Modbus TCP."),
            Tx("Use Copiar/Colar ou Duplicar; para sequência, defina quantidade e padrões; revise nomes, paths e endereços gerados; execute Preview; corrija colisões; use Apply somente após Preview válido.", "Use Copy/Paste or Duplicate; for a sequence, set count and patterns; review generated names, paths, and addresses; run Preview; fix collisions; use Apply only after a valid Preview.", "Use Copiar/Pegar o Duplicar; para secuencia, defina cantidad y patrones; revise nombres, paths y direcciones generadas; ejecute Preview; corrija colisiones; use Apply solo después de un Preview válido."),
            Tx("Cada novo TAG recebe identidade própria; o TAG original permanece intacto; Runtime value, quality, timestamp, histórico, alarmes e diagnósticos não são copiados.", "Each new TAG receives its own identity; the original TAG remains unchanged; Runtime value, quality, timestamp, history, alarms, and diagnostics are not copied.", "Cada TAG nuevo recibe identidad propia; el TAG original permanece intacto; Runtime value, quality, timestamp, histórico, alarmas y diagnósticos no se copian."),
            Tx("Seleção vazia, clipboard vazio, sequência em Driver não Modbus, path/nome/endereço duplicado ou Workspace alterado depois do Preview.", "Empty selection, empty clipboard, sequence on a non-Modbus Driver, duplicate path/name/address, or Workspace changed after Preview.", "Selección vacía, clipboard vacío, secuencia en Driver no Modbus, path/nombre/dirección duplicado o Workspace cambiado después del Preview."),
            Tx("Corrija as colisões mostradas, gere novamente quando o Workspace mudar e repita Preview antes de Apply.", "Fix the reported collisions, regenerate when the Workspace changes, and repeat Preview before Apply.", "Corrija las colisiones mostradas, genere nuevamente cuando cambie el Workspace y repita Preview antes de Apply."),
            "tags.overview", "tags.addressing", "sources.data-sources"),

        TaskGuide(
            "visual.properties", "visual",
            Tx("Propriedades visuais", "Visual properties", "Propiedades visuales"),
            Tx("Valores editáveis que controlam aparência, posição, tamanho e comportamento suportado de um objeto visual.", "Editable values that control the supported appearance, position, size, and behavior of a visual object.", "Valores editables que controlan apariencia, posición, tamaño y comportamiento soportado de un objeto visual."),
            Tx("Use ao configurar um objeto selecionado no Editor de Tela ou Editor de Popup.", "Use when configuring a selected object in the Screen Editor or Popup Editor.", "Úselas al configurar un objeto seleccionado en el Editor de Pantalla o Editor de Popup."),
            Tx("Selecione um objeto e mantenha o Working atual sem conflito.", "Select an object and keep the current Working state free of conflicts.", "Seleccione un objeto y mantenga el estado Working actual sin conflictos."),
            Tx("Selecione o objeto; altere somente propriedades disponíveis; revise o resultado na área de edição; salve a alteração.", "Select the object; change only available properties; review the result in the editing area; save the change.", "Seleccione el objeto; cambie solo propiedades disponibles; revise el resultado en el área de edición; guarde el cambio."),
            Tx("A propriedade é persistida no objeto selecionado e reaparece ao reabrir o Working.", "The property is persisted on the selected object and reappears when Working is reopened.", "La propiedad se persiste en el objeto seleccionado y reaparece al reabrir Working."),
            Tx("Campo desabilitado, valor inválido, seleção perdida ou alteração que não aparece após reabrir.", "Disabled field, invalid value, lost selection, or a change that does not appear after reopening.", "Campo deshabilitado, valor inválido, selección perdida o cambio que no aparece después de reabrir."),
            Tx("Confirme o objeto selecionado, corrija validações do campo e verifique se a alteração foi salva no Working correto.", "Confirm the selected object, fix field validation errors, and verify the change was saved in the correct Working state.", "Confirme el objeto seleccionado, corrija errores de validación del campo y verifique que el cambio se guardó en el Working correcto."),
            "screens.overview", "popups.overview", "visual.dynamics"),

        TaskGuide(
            "visual.dynamics", "visual",
            Tx("Dynamics", "Dynamics", "Dynamics"),
            Tx("Regras que ligam dados do projeto a propriedades visuais suportadas.", "Rules that connect project data to supported visual properties.", "Reglas que conectan datos del proyecto con propiedades visuales soportadas."),
            Tx("Use quando aparência ou comportamento visual deve responder a TAGs, Client Memory ou expressões suportadas.", "Use when visual appearance or behavior must respond to TAGs, Client Memory, or supported expressions.", "Úselas cuando la apariencia o el comportamiento visual deba responder a TAGs, Client Memory o expresiones soportadas."),
            Tx("Tenha o objeto visual, a propriedade de destino e a referência de dados disponíveis no projeto.", "Have the visual object, target property, and data reference available in the project.", "Tenga el objeto visual, la propiedad de destino y la referencia de datos disponibles en el proyecto."),
            Tx("Escolha a propriedade; adicione a Dynamic; selecione a referência; configure a transformação permitida; valide e salve.", "Choose the property; add the Dynamic; select the reference; configure the allowed transformation; validate and save.", "Elija la propiedad; agregue la Dynamic; seleccione la referencia; configure la transformación permitida; valide y guarde."),
            Tx("No Runtime, a propriedade acompanha o valor de entrada conforme a regra configurada.", "In Runtime, the property follows the input value according to the configured rule.", "En Runtime, la propiedad sigue el valor de entrada según la regla configurada."),
            Tx("Referência ausente, tipo incompatível, expressão inválida ou quality de entrada inadequada.", "Missing reference, incompatible type, invalid expression, or unsuitable input quality.", "Referencia ausente, tipo incompatible, expresión inválida o quality de entrada inadecuada."),
            Tx("Verifique a referência no Object Browser, confira tipo/quality e reduza a regra até identificar a parte inválida.", "Verify the reference in the Object Browser, check type/quality, and simplify the rule until the invalid part is identified.", "Verifique la referencia en el Object Browser, compruebe tipo/quality y simplifique la regla hasta identificar la parte inválida."),
            "visual.properties", "engineering.object-browser", "tags.quality"),

        TaskGuide(
            "visual.events", "visual",
            Tx("Eventos visuais", "Visual events", "Eventos visuales"),
            Tx("Ações configuradas para eventos suportados de objetos no Editor de Tela e Editor de Popup.", "Actions configured for supported object events in the Screen Editor and Popup Editor.", "Acciones configuradas para eventos soportados de objetos en el Editor de Pantalla y Editor de Popup."),
            Tx("Use quando uma interação do usuário deve executar navegação, popup, comando ou Script suportado.", "Use when a user interaction must run supported navigation, popup, command, or Script behavior.", "Úselos cuando una interacción del usuario deba ejecutar navegación, popup, comando o Script soportado."),
            Tx("Selecione um objeto que ofereça o evento e tenha o destino/ação já existente no projeto.", "Select an object that offers the event and have the destination/action already available in the project.", "Seleccione un objeto que ofrezca el evento y tenga el destino/acción ya disponible en el proyecto."),
            Tx("Escolha o evento; selecione a ação suportada; configure o destino; valide referências; salve e teste no Runtime apropriado.", "Choose the event; select the supported action; configure the target; validate references; save and test in the appropriate Runtime.", "Elija el evento; seleccione la acción soportada; configure el destino; valide referencias; guarde y pruebe en el Runtime apropiado."),
            Tx("A interação executa somente a ação configurada e continua sujeita às permissões da sessão.", "The interaction runs only the configured action and remains subject to session permissions.", "La interacción ejecuta solo la acción configurada y sigue sujeta a los permisos de la sesión."),
            Tx("Evento sem destino, referência removida, ação não suportada ou comando bloqueado por permissão.", "Event without a target, removed reference, unsupported action, or command blocked by permission.", "Evento sin destino, referencia eliminada, acción no soportada o comando bloqueado por permiso."),
            Tx("Confirme o evento no objeto, revalide o destino e use Diagnostics/Audit para ações rejeitadas.", "Confirm the event on the object, revalidate the target, and use Diagnostics/Audit for rejected actions.", "Confirme el evento en el objeto, revalide el destino y use Diagnostics/Audit para acciones rechazadas."),
            "bindings-commands.overview", "scripts.engineering", "audit.overview"),

        TaskGuide(
            "scripts.engineering", "scripts",
            Tx("Editor de Scripts", "Script Editor", "Editor de Scripts"),
            Tx("Área de Engineering para criar, revisar e configurar Scripts e seus eventos.", "Engineering area for creating, reviewing, and configuring Scripts and their events.", "Área de Engineering para crear, revisar y configurar Scripts y sus eventos."),
            Tx("Use para editar Script Server ou Client conforme as opções disponibilizadas pelo produto.", "Use to edit Server or Client Script according to the options provided by the product.", "Úselo para editar Script Server o Client según las opciones ofrecidas por el producto."),
            Tx("Tenha permissão de Engineering, escolha o Script correto e configure trigger/evento e dependências exigidas.", "Have Engineering permission, choose the correct Script, and configure the required trigger/event and dependencies.", "Tenga permiso de Engineering, elija el Script correcto y configure el trigger/evento y las dependencias requeridas."),
            Tx("Selecione o Script; configure escopo e evento; edite no Editor de código; use validação/diagnósticos; revise referências; salve e siga o lifecycle.", "Select the Script; configure scope and event; edit in the code editor; use validation/diagnostics; review references; save and follow lifecycle.", "Seleccione el Script; configure scope y evento; edite en el Editor de código; use validación/diagnósticos; revise referencias; guarde y siga el lifecycle."),
            Tx("O Script salvo permanece associado ao Working e só participa da operação quando o lifecycle correspondente permitir.", "The saved Script remains associated with Working and only participates in operation when the corresponding lifecycle allows it.", "El Script guardado permanece asociado a Working y solo participa en operación cuando el lifecycle correspondiente lo permite."),
            Tx("Erro de sintaxe, trigger incompleto, referência ausente, API não disponível ou execução bloqueada.", "Syntax error, incomplete trigger, missing reference, unavailable API, or blocked execution.", "Error de sintaxis, trigger incompleto, referencia ausente, API no disponible o ejecución bloqueada."),
            Tx("Use os diagnósticos do editor, confira trigger e dependências e abra a referência de API correspondente antes de alterar a lógica.", "Use editor diagnostics, check trigger and dependencies, and open the corresponding API reference before changing logic.", "Use los diagnósticos del editor, compruebe trigger y dependencias y abra la referencia de API correspondiente antes de cambiar la lógica."),
            "scripts.python-validation", "engineering.object-browser", "scripts.server"),

        TaskGuide(
            "scripts.python-validation", "scripts",
            Tx("Validação e diagnósticos Python", "Python validation and diagnostics", "Validación y diagnósticos de Python"),
            Tx("Feedback de edição que detecta problemas de sintaxe, referência e uso de APIs suportadas antes do lifecycle operacional.", "Editing feedback that detects syntax, reference, and supported-API problems before the operational lifecycle.", "Feedback de edición que detecta problemas de sintaxis, referencia y uso de APIs soportadas antes del lifecycle operacional."),
            Tx("Use antes de salvar/aplicar um Script e sempre que o editor indicar diagnóstico.", "Use before saving/applying a Script and whenever the editor reports a diagnostic.", "Úselo antes de guardar/aplicar un Script y siempre que el editor muestre un diagnóstico."),
            Tx("Abra o Script no Editor de código e mantenha as referências do projeto disponíveis.", "Open the Script in the code editor and keep project references available.", "Abra el Script en el Editor de código y mantenga disponibles las referencias del proyecto."),
            Tx("Execute a validação disponível; leia cada diagnóstico; navegue até a linha/referência; corrija; valide novamente antes de salvar.", "Run the available validation; read each diagnostic; navigate to the line/reference; fix it; validate again before saving.", "Ejecute la validación disponible; lea cada diagnóstico; vaya a la línea/referencia; corríjalo; valide nuevamente antes de guardar."),
            Tx("O editor fica sem erros bloqueantes conhecidos e o Script pode seguir para o fluxo normal de Engineering.", "The editor has no known blocking errors and the Script can continue through the normal Engineering flow.", "El editor queda sin errores bloqueantes conocidos y el Script puede seguir el flujo normal de Engineering."),
            Tx("Sintaxe inválida, nome não resolvido, referência removida ou função fora da API suportada.", "Invalid syntax, unresolved name, removed reference, or a function outside the supported API.", "Sintaxis inválida, nombre no resuelto, referencia eliminada o función fuera de la API soportada."),
            Tx("Corrija o primeiro diagnóstico relevante, valide de novo e use Object Browser/referência de API para confirmar identificadores.", "Fix the first relevant diagnostic, validate again, and use the Object Browser/API reference to confirm identifiers.", "Corrija el primer diagnóstico relevante, valide de nuevo y use Object Browser/referencia de API para confirmar identificadores."),
            "scripts.engineering", "engineering.object-browser", "scripts.server"),

        TaskGuide(
            "engineering.object-browser", "engineering",
            Tx("Object Browser e autoria guiada", "Object Browser and guided authoring", "Object Browser y autoría guiada"),
            Tx("Navegação por objetos e referências existentes para inserir identificadores válidos sem digitação por tentativa.", "Navigation through existing objects and references so valid identifiers can be inserted without guesswork.", "Navegación por objetos y referencias existentes para insertar identificadores válidos sin escribir por prueba y error."),
            Tx("Use ao configurar bindings, Dynamics, eventos ou Scripts que precisam referenciar recursos do projeto.", "Use when configuring bindings, Dynamics, events, or Scripts that need project references.", "Úselo al configurar bindings, Dynamics, eventos o Scripts que necesitan referencias del proyecto."),
            Tx("Tenha o projeto carregado e o recurso de destino selecionado.", "Have the project loaded and the target resource selected.", "Tenga el proyecto cargado y el recurso de destino seleccionado."),
            Tx("Abra o browser; filtre ou expanda a Structure; escolha o objeto/propriedade; insira a referência pela ação guiada; valide antes de salvar.", "Open the browser; filter or expand the Structure; choose the object/property; insert the reference through the guided action; validate before saving.", "Abra el browser; filtre o expanda la Estructura; elija el objeto/propiedad; inserte la referencia mediante la acción guiada; valide antes de guardar."),
            Tx("A referência inserida corresponde a um objeto real do Working atual.", "The inserted reference corresponds to a real object in the current Working state.", "La referencia insertada corresponde a un objeto real del Working actual."),
            Tx("Objeto não aparece, filtro oculta o item, referência ficou obsoleta após renomear/remover ou contexto errado foi selecionado.", "Object not shown, filter hides the item, reference became stale after rename/removal, or the wrong context was selected.", "Objeto no visible, filtro oculta el elemento, referencia quedó obsoleta tras renombrar/eliminar o se seleccionó el contexto incorrecto."),
            Tx("Limpe filtros, confirme o Working e reinsira a referência a partir do browser em vez de editar o identificador por tentativa.", "Clear filters, confirm Working, and reinsert the reference from the browser instead of editing the identifier by guesswork.", "Limpie filtros, confirme Working y vuelva a insertar la referencia desde el browser en lugar de editar el identificador por prueba."),
            "visual.dynamics", "scripts.engineering"),

        TaskGuide(
            "memory.client", "memory",
            Tx("Client Memory", "Client Memory", "Client Memory"),
            Tx("Memória não retentiva pertencente à sessão/cliente Runtime para estado local suportado.", "Non-retentive memory owned by the Runtime client/session for supported local state.", "Memoria no retentiva perteneciente a la sesión/cliente Runtime para estado local soportado."),
            Tx("Use para estado local de interface que não precisa sobreviver como dado retentivo do servidor.", "Use for local interface state that does not need to survive as retentive server data.", "Úsela para estado local de interfaz que no necesita persistir como dato retentivo del servidor."),
            Tx("Confirme que o dado é realmente local ao cliente e não é um valor de processo que precisa de Server Memory/TAG.", "Confirm the data is truly client-local and not a process value that requires Server Memory/TAG.", "Confirme que el dato sea realmente local al cliente y no un valor de proceso que requiera Server Memory/TAG."),
            Tx("Crie/configure a referência Client Memory pela superfície oferecida; use-a em bindings/expressões suportados; teste reinício/reconexão de acordo com a necessidade.", "Create/configure the Client Memory reference through the provided surface; use it in supported bindings/expressions; test restart/reconnect behavior as needed.", "Cree/configure la referencia Client Memory desde la superficie ofrecida; úsela en bindings/expresiones soportados; pruebe reinicio/reconexión según sea necesario."),
            Tx("O valor existe somente no contexto do cliente e pode ser perdido quando esse contexto termina.", "The value exists only in the client context and may be lost when that context ends.", "El valor existe solo en el contexto del cliente y puede perderse cuando ese contexto termina."),
            Tx("Expectativa incorreta de retenção, referência inexistente ou uso de Client Memory para dado que deveria ser compartilhado.", "Incorrect retention expectation, missing reference, or use of Client Memory for data that should be shared.", "Expectativa incorrecta de retención, referencia inexistente o uso de Client Memory para dato que debería compartirse."),
            Tx("Reavalie se o estado deve ser local ou retentivo; para estado compartilhado, use o recurso de servidor apropriado.", "Reassess whether the state should be local or retentive; for shared state, use the appropriate server resource.", "Reevalúe si el estado debe ser local o retentivo; para estado compartido, use el recurso de servidor adecuado."),
            "sources.internal-memory", "bindings-commands.overview"),

        TaskGuide(
            "security.scopes-authority", "security",
            Tx("Scopes e Authority", "Scopes and Authority", "Scopes y Authority"),
            Tx("Regras que combinam identidade, roles, capabilities e escopo para determinar o que uma sessão pode fazer.", "Rules that combine identity, roles, capabilities, and scope to determine what a session can do.", "Reglas que combinan identidad, roles, capabilities y scope para determinar lo que una sesión puede hacer."),
            Tx("Use ao revisar por que um usuário pode ou não executar uma ação em parte do sistema.", "Use when reviewing why a user can or cannot perform an action in part of the system.", "Úselo al revisar por qué un usuario puede o no realizar una acción en una parte del sistema."),
            Tx("Tenha acesso administrativo autorizado e identifique o usuário, roles e recurso em questão.", "Have authorized administrative access and identify the user, roles, and resource in question.", "Tenga acceso administrativo autorizado e identifique el usuario, roles y recurso en cuestión."),
            Tx("Revise as roles atribuídas; confira capabilities; verifique o scope/hierarquia; consulte a permissão efetiva; ajuste somente por fluxo administrativo suportado.", "Review assigned roles; check capabilities; verify scope/hierarchy; inspect effective permission; adjust only through the supported administration flow.", "Revise roles asignadas; compruebe capabilities; verifique scope/jerarquía; consulte el permiso efectivo; ajuste solo mediante el flujo administrativo soportado."),
            Tx("A permissão efetiva reflete a combinação configurada e a ação continua sendo validada pelo serviço.", "Effective permission reflects the configured combination and the action continues to be validated by the service.", "El permiso efectivo refleja la combinación configurada y la acción sigue siendo validada por el servicio."),
            Tx("Role atribuída sem capability necessária, scope que não inclui o recurso ou sessão antiga após mudança administrativa.", "Assigned role without the required capability, scope that does not include the resource, or an old session after an administrative change.", "Role asignada sin la capability necesaria, scope que no incluye el recurso o sesión antigua después de un cambio administrativo."),
            Tx("Use a visão de permissão efetiva e Audit; não tente contornar a restrição alterando a UI.", "Use the effective-permission view and Audit; do not try to bypass the restriction by changing the UI.", "Use la vista de permiso efectivo y Audit; no intente eludir la restricción cambiando la UI."),
            "security.users-roles-capabilities", "security.engineering-lock", "audit.overview"),

        TaskGuide(
            "security.engineering-lock", "security",
            Tx("Engineering Lock", "Engineering Lock", "Engineering Lock"),
            Tx("Proteção que controla quando alterações de Engineering são permitidas.", "Protection that controls when Engineering changes are allowed.", "Protección que controla cuándo se permiten cambios de Engineering."),
            Tx("Use ao abrir uma sessão de edição protegida ou ao diagnosticar ações de Engineering bloqueadas.", "Use when opening a protected editing session or diagnosing blocked Engineering actions.", "Úselo al abrir una sesión de edición protegida o diagnosticar acciones de Engineering bloqueadas."),
            Tx("Tenha identidade autorizada e a credencial/fluxo exigido pela configuração do produto.", "Have an authorized identity and the credential/flow required by product configuration.", "Tenga una identidad autorizada y la credencial/flujo exigido por la configuración del producto."),
            Tx("Confira o estado do Lock; desbloqueie somente pelo controle fornecido; execute a tarefa; encerre/renove o estado conforme a política apresentada.", "Check Lock state; unlock only through the provided control; perform the task; end/renew the state according to the displayed policy.", "Compruebe el estado del Lock; desbloquee solo mediante el control proporcionado; realice la tarea; cierre/renueve el estado según la política mostrada."),
            Tx("A edição fica disponível apenas durante o estado autorizado e ações protegidas continuam sujeitas às permissões da sessão.", "Editing is available only during the authorized state and protected actions remain subject to session permissions.", "La edición está disponible solo durante el estado autorizado y las acciones protegidas siguen sujetas a los permisos de la sesión."),
            Tx("Lock ativo, credencial rejeitada, sessão expirada ou usuário sem capability necessária.", "Active Lock, rejected credential, expired session, or user without the required capability.", "Lock activo, credencial rechazada, sesión expirada o usuario sin la capability necesaria."),
            Tx("Confirme identidade, estado do Lock e permissão efetiva; não edite arquivos/stores para contornar o bloqueio.", "Confirm identity, Lock state, and effective permission; do not edit files/stores to bypass the protection.", "Confirme identidad, estado del Lock y permiso efectivo; no edite archivos/stores para eludir la protección."),
            "security.scopes-authority", "engineering.shell-navigation"),

        TaskGuide(
            "licensing.generator", "licensing",
            Tx("License Generator", "License Generator", "License Generator"),
            Tx("Ferramenta administrativa para criar artefatos de licença a partir de entitlements configurados.", "Administrative tool for creating license artifacts from configured entitlements.", "Herramienta administrativa para crear artefactos de licencia desde entitlements configurados."),
            Tx("Use somente em fluxo administrativo autorizado para emitir uma licença destinada à instalação correta.", "Use only in an authorized administrative flow to issue a license for the correct installation.", "Úselo solo en un flujo administrativo autorizado para emitir una licencia para la instalación correcta."),
            Tx("Tenha os dados de licença aprovados e acesso ao ambiente protegido de emissão; material privado de assinatura não deve ser copiado para a aplicação.", "Have approved license data and access to the protected issuing environment; private signing material must not be copied into the application.", "Tenga los datos de licencia aprobados y acceso al entorno protegido de emisión; el material privado de firma no debe copiarse a la aplicación."),
            Tx("Preencha identidade/validade/limites e entitlements exibidos; valide os dados; gere o artefato; transfira somente o arquivo final pelo processo autorizado.", "Fill in the displayed identity/validity/limits and entitlements; validate the data; generate the artifact; transfer only the final file through the authorized process.", "Complete identidad/validez/límites y entitlements mostrados; valide los datos; genere el artefacto; transfiera solo el archivo final mediante el proceso autorizado."),
            Tx("O artefato pode ser inspecionado/instalado pelo fluxo de Licensing sem expor segredo de assinatura.", "The artifact can be inspected/installed through Licensing without exposing signing secrets.", "El artefacto puede inspeccionarse/instalarse mediante Licensing sin exponer secretos de firma."),
            Tx("Campo obrigatório ausente, entitlement incompatível, validade incorreta ou artefato rejeitado na inspeção.", "Missing required field, incompatible entitlement, incorrect validity, or artifact rejected during inspection.", "Campo obligatorio ausente, entitlement incompatible, validez incorrecta o artefacto rechazado en la inspección."),
            Tx("Corrija os dados de emissão e gere um novo artefato; nunca copie chaves privadas, tokens ou segredos para o Help ou para o projeto.", "Correct issuance data and generate a new artifact; never copy private keys, tokens, or secrets into Help or the project.", "Corrija los datos de emisión y genere un nuevo artefacto; nunca copie claves privadas, tokens ni secretos en Help o en el proyecto."),
            "licensing.overview", "runtime.session-classes"),

        TaskGuide(
            "runtime.session-classes", "runtime",
            Tx("Runtime Interactive e View Only", "Runtime Interactive and View Only", "Runtime Interactive y View Only"),
            Tx("Classes de sessão que definem o teto de interação permitido para uma sessão Runtime.", "Session classes that define the maximum interaction allowed for a Runtime session.", "Clases de sesión que definen el máximo de interacción permitido para una sesión Runtime."),
            Tx("Use ao entender por que uma sessão permite interação ou fica somente para visualização.", "Use when understanding why a session allows interaction or is limited to viewing.", "Úselo para entender por qué una sesión permite interacción o queda limitada a visualización."),
            Tx("Entre com identidade válida; a classe concedida depende de disponibilidade de licença e permissões efetivas.", "Sign in with a valid identity; the granted class depends on license availability and effective permissions.", "Ingrese con una identidad válida; la clase concedida depende de disponibilidad de licencia y permisos efectivos."),
            Tx("Solicite a sessão pelo fluxo normal; confira a classe concedida; em View Only, use apenas visualização; para ações interativas, obtenha uma sessão Interactive autorizada.", "Request the session through the normal flow; check the granted class; in View Only, use viewing only; for interactive actions, obtain an authorized Interactive session.", "Solicite la sesión mediante el flujo normal; compruebe la clase concedida; en View Only, use solo visualización; para acciones interactivas, obtenga una sesión Interactive autorizada."),
            Tx("A UI e o serviço refletem a classe concedida; View Only não realiza comandos ou escritas operacionais.", "The UI and service reflect the granted class; View Only does not perform operational commands or writes.", "La UI y el servicio reflejan la clase concedida; View Only no realiza comandos ni escrituras operacionales."),
            Tx("Fallback para View Only, sessão expirada, limite Interactive atingido ou permissão insuficiente.", "Fallback to View Only, expired session, Interactive limit reached, or insufficient permission.", "Fallback a View Only, sesión expirada, límite Interactive alcanzado o permiso insuficiente."),
            Tx("Confira status de Licensing e a permissão efetiva do usuário; renove a sessão quando necessário.", "Check Licensing status and the user's effective permission; renew the session when needed.", "Compruebe el estado de Licensing y el permiso efectivo del usuario; renueve la sesión cuando sea necesario."),
            "licensing.overview", "security.scopes-authority", "runtime.overview"),

        TaskGuide(
            "application.export-import", "application",
            Tx("Exportar e importar Application", "Export and import Application", "Exportar e importar Application"),
            Tx("Fluxo para transportar a aplicação por pacote sem transformar importação em ativação automática.", "Flow for moving the application by package without turning import into automatic activation.", "Flujo para transportar la aplicación mediante paquete sin convertir importación en activación automática."),
            Tx("Use para backup portátil da aplicação, migração controlada ou criação de outro Working a partir de pacote.", "Use for portable application backup, controlled migration, or creating another Working state from a package.", "Úselo para backup portátil de la aplicación, migración controlada o creación de otro Working desde un paquete."),
            Tx("Tenha acesso de Engineering, confirme o projeto de origem/destino e proteja Authority por seu fluxo próprio quando ela também precisar ser preservada.", "Have Engineering access, confirm source/destination project, and protect Authority through its own flow when it also needs preservation.", "Tenga acceso de Engineering, confirme proyecto de origen/destino y proteja Authority mediante su propio flujo cuando también deba preservarse."),
            Tx("Exporte a Application; no destino, selecione o arquivo; use Inspect/Preview; resolva erros; use Apply; depois siga Save/Revision/Publish/Activate conforme necessário.", "Export the Application; at the destination, select the file; use Inspect/Preview; resolve errors; use Apply; then follow Save/Revision/Publish/Activate as needed.", "Exporte la Application; en el destino, seleccione el archivo; use Inspect/Preview; resuelva errores; use Apply; luego siga Save/Revision/Publish/Activate según sea necesario."),
            Tx("A aplicação importada entra como conteúdo de Engineering e não substitui Authority nem muda Active por conta própria.", "The imported application enters as Engineering content and does not replace Authority or change Active by itself.", "La aplicación importada entra como contenido de Engineering y no reemplaza Authority ni cambia Active por sí sola."),
            Tx("Pacote inválido, Preview com conflitos, dependência ausente ou expectativa de que Authority/licença sejam transportadas junto com a Application.", "Invalid package, Preview conflicts, missing dependency, or expectation that Authority/license travels with the Application.", "Paquete inválido, conflictos de Preview, dependencia ausente o expectativa de que Authority/licencia viaje con la Application."),
            Tx("Separe Application, Authority e Licensing como fluxos distintos; corrija o pacote ou o destino antes de Apply.", "Treat Application, Authority, and Licensing as separate flows; fix the package or destination before Apply.", "Trate Application, Authority y Licensing como flujos separados; corrija el paquete o el destino antes de Apply."),
            "packages.escadapkg", "recovery.backup-system-recovery", "engineering.lifecycle")
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
        S(Tx("Receita: estado visual permitido", "Recipe: allowed visual state", "Receta: estado visual permitido"), Tx(
            "Leia TAGs estáveis declarados, avalie a condição e escreva somente um TAG de estado autorizado que já esteja ligado à propriedade visual. O script não altera objetos de tela diretamente e não cria uma API visual paralela.",
            "Read declared stable TAGs, evaluate the condition, and write only an authorized state TAG already bound to the visual property. The script does not mutate screen objects directly or create a parallel visual API.",
            "Lea TAGs estables declarados, evalúe la condición y escriba solamente un TAG de estado autorizado ya ligado a la propiedad visual. El script no muta objetos de pantalla directamente ni crea una API visual paralela."),
            "left = read_tag(\"<stable-left-tag-id>\")\nright = read_tag(\"<stable-right-tag-id>\")\nif left > right:\n    write_tag(\"<stable-visual-state-tag-id>\", True)"),
        S(Tx("Lifecycle e triggers", "Lifecycle and triggers", "Lifecycle y triggers"), Tx(
            "Somente scripts habilitados de scope Server são hospedados na revisão Active. O host atual despacha Initialize, Dispose, TagChanged, Timer e ServerRuntimeEvent. Ao trocar Active, a geração anterior é cancelada; acesso de TAG e emissão de Operational Event usam revision gate para impedir execução obsoleta sobre uma revisão nova.",
            "Only enabled Server-scope scripts are hosted on the Active revision. The current host dispatches Initialize, Dispose, TagChanged, Timer and ServerRuntimeEvent. When Active changes, the previous generation is cancelled; TAG access and Operational Event emission use a revision gate to prevent obsolete execution against a new revision.",
            "Solo scripts habilitados de scope Server se hospedan en la revisión Active. El host actual despacha Initialize, Dispose, TagChanged, Timer y ServerRuntimeEvent. Al cambiar Active, la generación anterior se cancela; acceso TAG y emisión de Operational Event usan revision gate para impedir ejecución obsoleta sobre una revisión nueva.")),
        S(Tx("Execução e falhas", "Execution and failures", "Ejecución y fallas"), Tx(
            "A política padrão usa timeout de handler de 250 ms, fila limitada a 128 eventos, Timer mínimo de 50 ms e entra em cooldown após 5 falhas consecutivas. Ao fim do cooldown configurável, somente um evento é aceito como probe de recuperação; sucesso restaura o processamento e nova falha reinicia o cooldown. Fila, timeout, cancelamento, fault isolation e diagnósticos pertencem à instância do script e não concedem fallback de Authority.",
            "The default policy uses a 250 ms handler timeout, a queue bounded to 128 events, a 50 ms minimum Timer and enters cooldown after 5 consecutive failures. After the configurable cooldown, only one event is accepted as a recovery probe; success restores processing and another failure starts the cooldown again. Queue, timeout, cancellation, fault isolation and diagnostics belong to the script instance and grant no Authority fallback.",
            "La política por defecto usa timeout de handler de 250 ms, cola limitada a 128 eventos, Timer mínimo de 50 ms y entra en cooldown después de 5 fallas consecutivas. Al terminar el cooldown configurable, solo un evento se acepta como prueba de recuperación; el éxito restaura el procesamiento y una nueva falla reinicia el cooldown. Cola, timeout, cancelación, fault isolation y diagnósticos pertenecen a la instancia y no conceden fallback de Authority.")),
        S(Tx("Sandbox e segurança", "Sandbox and security", "Sandbox y seguridad"), Tx(
            "A superfície de Server Script permite leitura de TAGs compartilhados, leitura/escrita de Server Memory e escrita de TAGs conforme o contrato. O sandbox nega filesystem, sistema operacional, shell/process execution, rede arbitrária, database, acesso direto a industrial drivers, secrets, browser DOM e browser storage. O preflight rejeita imports/calls obviamente proibidos, mas é feedback de editor; enforcement de sandbox não deve depender de scan de texto.",
            "The Server Script surface allows shared TAG reads, Server Memory reads/writes and TAG writes according to contract. The sandbox denies filesystem, operating system, shell/process execution, arbitrary network, database, direct industrial-driver access, secrets, browser DOM and browser storage. Preflight rejects obviously prohibited imports/calls but is editor feedback; sandbox enforcement must not depend on text scanning.",
            "La superficie Server Script permite lectura de TAGs compartidos, lectura/escritura de Server Memory y escritura de TAGs según contrato. El sandbox niega filesystem, sistema operativo, shell/process execution, red arbitraria, database, acceso directo a industrial drivers, secrets, browser DOM y browser storage. Preflight rechaza imports/calls claramente prohibidos, pero es feedback del editor; enforcement del sandbox no debe depender de escaneo de texto.")),
        S(Tx("Exemplo Server Memory", "Server Memory example", "Ejemplo Server Memory"), Tx(
            "Use somente funções expostas pelo build e referências estáveis declaradas. Não documente convenience functions históricas ou planejadas como se existissem.",
            "Use only functions exposed by the build and declared stable references. Do not document historical or planned convenience functions as if they existed.",
            "Use solo funciones expuestas por el build y referencias estables declaradas. No documente convenience functions históricas o planificadas como si existieran."),
            "value = read_server_memory(\"<stable-tag-id>\")\nwrite_server_memory(\"<stable-tag-id>\", value)\npublish_server_memory_sample(\"<stable-tag-id>\", value, \"Good\")"));

    private static IReadOnlyCollection<ContextualHelpScriptApi> BuildServerScriptApi(string locale) => new[]
    {
        ScriptApi("read_tag", "read_tag(tag_id)", "tag_id: stable declared TAG id", "Returns the current TAG value.", "Read only; the TAG must be a declared dependency.", "value = read_tag(\"<stable-tag-id>\")", locale),
        ScriptApi("read_server_memory", "read_server_memory(tag_id)", "tag_id: stable declared ServerMemoryTag id", "Returns the current Server Memory value.", "Read only; only ServerMemoryTag references are accepted.", "value = read_server_memory(\"<stable-server-memory-tag-id>\")", locale),
        ScriptApi("write_tag", "write_tag(tag_id, value)", "tag_id: stable declared TAG id; value: serializable value", "Writes the TAG and returns None.", "Only declared, write-authorized TAGs are allowed; no UI object is mutated directly.", "write_tag(\"<stable-visual-state-tag-id>\", True)", locale),
        ScriptApi("write_server_memory", "write_server_memory(tag_id, value)", "tag_id: stable declared ServerMemoryTag id; value: serializable value", "Writes Server Memory and returns None.", "Only ServerMemoryTag references are accepted.", "write_server_memory(\"<stable-server-memory-tag-id>\", value)", locale),
        ScriptApi("publish_server_memory_sample", "publish_server_memory_sample(tag_id, value, quality)", "tag_id: stable declared ServerMemoryTag id; value: serializable value; quality: quality string", "Publishes a Server Memory sample and returns None.", "Requires exactly three arguments and a valid ServerMemoryTag.", "publish_server_memory_sample(\"<stable-server-memory-tag-id>\", value, \"Good\")", locale),
        ScriptApi("emit_operational_event", "emit_operational_event(definition_id, message=None, context=None)", "definition_id: stable event definition id; message: optional string; context: optional dictionary", "Emits the operational event and returns None.", "Inputs are validated and bounded; it does not grant Authority fallback.", "emit_operational_event(\"<definition-id>\", \"threshold crossed\", {\"source\": \"script\"})", locale)
    };

    private static ContextualHelpScriptApi ScriptApi(
        string name,
        string signature,
        string parameters,
        string result,
        string safety,
        string example,
        string locale) => new(
        name,
        signature,
        Pick(locale, parameters.Replace("stable declared", "TAG estável declarado", StringComparison.Ordinal), parameters, parameters),
        Pick(locale, result.Replace("Returns", "Retorna", StringComparison.Ordinal).Replace("Writes", "Escreve", StringComparison.Ordinal).Replace("Publishes", "Publica", StringComparison.Ordinal).Replace("Emits", "Emite", StringComparison.Ordinal), result, result),
        Pick(locale, safety.Replace("Read only", "Somente leitura", StringComparison.Ordinal).Replace("Only", "Somente", StringComparison.Ordinal).Replace("Requires", "Exige", StringComparison.Ordinal).Replace("Inputs", "Entradas", StringComparison.Ordinal), safety, safety),
        example);

    private static TopicDefinition BuildReusableLibrariesTopic() => Detailed(
        "libraries.reusable-resources", "libraries",
        Tx("Reusable Resource Libraries", "Reusable Resource Libraries", "Reusable Resource Libraries"),
        Tx("Reuso seletivo em Engineering sem dependência externa de Runtime.", "Selective Engineering reuse without external Runtime dependency.", "Reutilización selectiva en Engineering sin dependencia externa de Runtime."),
        S(Tx("Criar e exportar .escadalib", "Create and export .escadalib", "Crear y exportar .escadalib"), Tx(
            "A exportação cria .escadalib a partir de recursos selecionados do Working e inclui automaticamente o closure de dependências. O build exporta Equipment Template, Dynamo, Screen, Popup, Script e Visual Asset. O pacote contém manifest, hashes de integridade e limites de segurança; Inspect valida formato, manifest, arquivos, hashes e payloads antes do uso.",
            "Export creates .escadalib from selected Working resources and automatically includes the dependency closure. The build exports Equipment Template, Dynamo, Screen, Popup, Script and Visual Asset. The package contains a manifest, integrity hashes and safety limits; Inspect validates format, manifest, files, hashes and payloads before use.",
            "Export crea .escadalib desde recursos seleccionados de Working e incluye automáticamente el closure de dependencias. El build exporta Equipment Template, Dynamo, Screen, Popup, Script y Visual Asset. El paquete contiene manifest, hashes de integridad y límites de seguridad; Inspect valida formato, manifest, archivos, hashes y payloads antes del uso.")),
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
            "Desasociar elimina disponibilidad del catálogo y no borra contenido ya incorporado. El .escadapkg final permanece self-contained; Runtime y Active nunca dependen de .escadalib para ejecutar contenido incorporado.")));

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
                    : CleanUserCopy(source.Description))
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
            CleanUserCopy(driver.DisplayName),
            Pick(locale,
                "Driver de comunicação de produção registrado neste build.",
                "Production communication driver registered in this build.",
                "Driver de comunicación de producción registrado en este build."),
            new[]
            {
                new ContextualHelpSection(
                    Pick(locale, "Finalidade e perfil comprovado", "Proven purpose and profile", "Finalidad y perfil comprobado"),
                    $"Type key: {driver.TypeKey}\n{(string.IsNullOrWhiteSpace(driver.Description) ? Pick(locale, "Sem descrição adicional no descriptor.", "No additional descriptor description.", "Sin descripción adicional en el descriptor.") : CleanUserCopy(driver.Description))}\n{schemaIdentity}"),
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
            driver.Capabilities.SupportsReconcile ? Pick(locale, "Reconciliação", "Reconcile", "Reconcilación") : null,
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
                invalid.Add($"{field.Key}=<malformed; expected {CleanUserCopy(field.ExpectedFormat)}>" );
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
        var parts = new List<string> { $"{field.Key} - {CleanUserCopy(field.DisplayName)} [{field.ValueKind}, {required}]" };
        if (!string.IsNullOrWhiteSpace(field.Description)) parts.Add(CleanUserCopy(field.Description));
        if (!string.IsNullOrWhiteSpace(field.ExpectedFormat)) parts.Add($"{Pick(locale, "Formato", "Format", "Formato")}: {CleanUserCopy(field.ExpectedFormat)}");
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
        new(
            id,
            category,
            title,
            summary,
            new[]
            {
                S(Tx("O que é?", "What is it?", "¿Qué es?"), summary),
                S(Tx("Para que serve?", "What is it for?", "¿Para qué sirve?"), body),
                S(Tx("Quando usar?", "When to use it?", "¿Cuándo usarlo?"), Tx(
                    $"Use este tópico ao trabalhar com {title.Portuguese}.",
                    $"Use this topic when working with {title.English}.",
                    $"Use este tema al trabajar con {title.Spanish}.")),
                S(Tx("Pré-requisitos", "Prerequisites", "Requisitos previos"), Tx(
                    "Abra o projeto/recurso correto e use uma sessão com acesso suficiente para a tarefa.",
                    "Open the correct project/resource and use a session with sufficient access for the task.",
                    "Abra el proyecto/recurso correcto y use una sesión con acceso suficiente para la tarea.")),
                S(Tx("Passos", "Steps", "Pasos"), body),
                S(Tx("Resultado esperado", "Expected result", "Resultado esperado"), Tx(
                    "A tela deve refletir o estado aceito pelo produto sem alterar outros recursos silenciosamente.",
                    "The screen should reflect the state accepted by the product without silently changing unrelated resources.",
                    "La pantalla debe reflejar el estado aceptado por el producto sin cambiar silenciosamente otros recursos.")),
                S(Tx("Problemas comuns", "Common problems", "Problemas comunes"), Tx(
                    "Permissão insuficiente, configuração inválida, referência ausente ou estado do projeto diferente do esperado.",
                    "Insufficient permission, invalid configuration, missing reference, or project state different from what was expected.",
                    "Permiso insuficiente, configuración inválida, referencia ausente o estado del proyecto diferente de lo esperado.")),
                S(Tx("Como diagnosticar/corrigir", "How to diagnose/fix", "Cómo diagnosticar/corregir"), Tx(
                    "Leia a mensagem apresentada, confirme sessão e estado do projeto, valide referências e use Diagnostics antes de repetir a ação.",
                    "Read the displayed message, confirm session and project state, validate references, and use Diagnostics before repeating the action.",
                    "Lea el mensaje mostrado, confirme sesión y estado del proyecto, valide referencias y use Diagnostics antes de repetir la acción."))
            });

    private static TopicDefinition TaskGuide(
        string id,
        string category,
        LocalizedText title,
        LocalizedText summary,
        LocalizedText whenToUse,
        LocalizedText prerequisites,
        LocalizedText steps,
        LocalizedText expectedResult,
        LocalizedText commonProblems,
        LocalizedText diagnostics,
        params string[] relatedTopicIds) =>
        new(
            id,
            category,
            title,
            summary,
            new[]
            {
                S(Tx("O que é?", "What is it?", "¿Qué es?"), summary),
                S(Tx("Para que serve?", "What is it for?", "¿Para qué sirve?"), summary),
                S(Tx("Quando usar?", "When to use it?", "¿Cuándo usarlo?"), whenToUse),
                S(Tx("Pré-requisitos", "Prerequisites", "Requisitos previos"), prerequisites),
                S(Tx("Passos", "Steps", "Pasos"), steps),
                S(Tx("Resultado esperado", "Expected result", "Resultado esperado"), expectedResult),
                S(Tx("Problemas comuns", "Common problems", "Problemas comunes"), commonProblems),
                S(Tx("Como diagnosticar/corrigir", "How to diagnose/fix", "Cómo diagnosticar/corregir"), diagnostics)
            },
            relatedTopicIds);

    private static TopicDefinition Detailed(string id, string category, LocalizedText title, LocalizedText summary, params SectionDefinition[] sections) =>
        new(id, category, title, summary, sections);

    private static SectionDefinition S(LocalizedText heading, LocalizedText body, string? code = null) => new(heading, body, code);
    private static LocalizedText Tx(string pt, string en, string es) => new(pt, en, es);

    private static string Pick(string locale, string pt, string en, string es) =>
        CleanUserCopy(locale switch
        {
            "en" => en,
            "es" => es,
            _ => pt
        });

    private static string CleanUserCopy(string value) => value
        .Replace("canônico", "do produto", StringComparison.OrdinalIgnoreCase)
        .Replace("canônica", "do produto", StringComparison.OrdinalIgnoreCase)
        .Replace("canonical", "product", StringComparison.OrdinalIgnoreCase)
        .Replace("canónico", "del producto", StringComparison.OrdinalIgnoreCase)
        .Replace("canónica", "del producto", StringComparison.OrdinalIgnoreCase);

    private sealed record LocalizedText(string Portuguese, string English, string Spanish)
    {
        public string For(string locale) => CleanUserCopy(locale switch { "en" => English, "es" => Spanish, _ => Portuguese });
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
        IReadOnlyCollection<SectionDefinition> Sections,
        IReadOnlyCollection<string>? RelatedTopicIds = null)
    {
        public ContextualHelpTopic Build(string locale) => new(
            Id,
            Category,
            Title.For(locale),
            Summary.For(locale),
            Sections.Select(section => section.Build(locale)).ToArray(),
            RelatedTopicIds ?? Array.Empty<string>());
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
