using Scada.Api.Security;
using Scada.DriverHost.Engineering;

namespace Scada.Api.Runtime;

public sealed record ContextualHelpField(string Key, string Label, string Description, string Example, string Constraints);

public sealed record ContextualHelpFigure(string Source, string Alt, string Caption);

public sealed record ContextualHelpSection(
    string Heading, string Body, string? Code = null,
    IReadOnlyCollection<ContextualHelpField>? Fields = null,
    ContextualHelpFigure? Figure = null);

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

public static partial class ContextualHelpCatalog
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
        "sources.connection-workflow",
        "drivers.modbus-setup",
        "drivers.mqtt-setup",
        "drivers.opcua-setup",
        "home-building.device-workflow",
        "drivers.bacnet-setup",
        "drivers.knx-setup",
        "drivers.homeassistant-setup",
        "drivers.esphome-setup",
        "drivers.shelly-setup",
        "drivers.radio-connections",
        "drivers.siemens-setup",
        "drivers.dnp3-setup",
        "drivers.iec104-setup",
        "drivers.modbus-server-setup",
        "drivers.logix-setup",
        "drivers.melsec-setup",
        "drivers.panasonic-setup",
        "drivers.zigbee-native-setup",
        "drivers.zigbee2mqtt-setup",
        "drivers.zwave-setup",
        "drivers.matter-test-workflow",
        "tags.overview",
        "tags.quality",
        "tags.timestamps",
        "tags.writeability",
        "tags.addressing",
        "tags.scaling-formatting",
        "sources.internal-memory",
        "gateway.configure-test",
        "engineering.tabular-workflow",
        "libraries.associate-incorporate",
        "assets.management",
        "gateway.overview",
        "scripts.server",
        "alarms.configuration",
        "alarms.runtime-operation",
        "operational-events.configuration",
        "alarms.overview",
        "operational-events.overview",
        "audit.query-diagnostics",
        "security.local-accounts",
        "security.policy-scopes",
        "security.lock-workflow",
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
        "scripts.client-visual",
        "scripts.python-validation",
        "engineering.object-browser",
        "memory.client",
        "security.scopes-authority",
        "security.engineering-lock",
        "licensing.generator",
        "runtime.session-classes",
        "application.export-import",
        "intelligence.process-stage-member-insights",
        "runtime.history",
        "ha.overview",
        "installation.switching",
        "intelligence.overview",
        "help.find-your-way",
        "visual.first-screen",
        "visual.object-selection",
        "visual.popup-workflow",
        "scripts.choose-scope",
        "scripts.server-syntax",
        "scripts.triggers-dependencies",
        "scripts.server-recipes",
        "scripts.commands-feedback",
        "scripts.client-recipes",
        "scripts.client-api",
        "intelligence.profiles-equipment",
        "intelligence.operating-contexts",
        "intelligence.dashboard-workflow",
        "help.application-map",
        "templates.overview",
        "equipment.overview",
        "database.topology",
        "ha.configuration-operation",
        "intelligence.activation",
        "intelligence.semantics",
        "intelligence.analysis-guide",
        "application.presentation",
        "sources.media",
        "home-building.organization",
        "reports.data-designer",
        "reports.layout-variables",
        "reports.runtime-output",
        "historian.capture-query"
    };

    private static readonly IReadOnlyCollection<TopicDefinition> ManualTopics = BuildManualTopics()
        .Concat(BuildPhase1TaskTopics())
        .Concat(BuildValidatedSurfaceTaskTopics())
        .Concat(BuildFoundationTagTaskTopics())
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

        topics.AddRange(BuildLearningGuides(locale));
        topics.AddRange(BuildVisualObjectGuides(locale));
        topics.AddRange(BuildSystemGuides(locale));

        return new ContextualHelpCatalogView(
            locale,
            SupportedLocales,
            topics.GroupBy(topic => topic.Id, StringComparer.Ordinal).Select(group => ExpandLearningLinks(group.Last()))
                .OrderBy(topic => topic.Id, StringComparer.Ordinal).ToArray(),
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

        TaskGuide(
            "alarms.overview", "alarms",
            Tx("Alarmes", "Alarms", "Alarmas"),
            Tx("Configure alarmes e entenda o estado que o operador vê no Runtime.", "Configure alarms and understand the state operators see in Runtime.", "Configure alarmas y comprenda el estado que el operador ve en Runtime."),
            Tx(
                "Use Alarm para uma condição de processo associada a um TAG, como nível alto ou equipamento em falha. Alarm tem estado que muda com a condição; não substitui Operational Event (ocorrência) nem Audit (ação rastreável).",
                "Use Alarm for a process condition tied to a TAG, such as a high level or equipment fault. An Alarm has state that changes with the condition; it does not replace an Operational Event (occurrence) or Audit (traceable action).",
                "Use Alarm para una condición de proceso asociada a un TAG, como nivel alto o equipo en falla. Alarm tiene un estado que cambia con la condición; no sustituye Operational Event (ocurrencia) ni Audit (acción trazable)."),
            Tx(
                "Tenha o TAG correto criado e saiba seu tipo, unidade e significado no processo. Defina o limite com quem responde pela operação e confirme que o projeto Working aberto é o correto e pode ser editado.",
                "Have the correct TAG created and know its type, unit and process meaning. Set the limit with the person responsible for operations, and confirm that the open Working project is the correct one and can be edited.",
                "Tenga creado el TAG correcto y conozca su tipo, unidad y significado en el proceso. Defina el límite con la persona responsable de la operación y confirme que el proyecto Working abierto es el correcto y se puede editar."),
            Tx(
                "1. Em Engineering, abra Alarmes e crie uma definição. 2. Informe nome e caminho do TAG. 3. Escolha Digital, High, HighHigh, Low, LowLow, Communication ou System. Para Digital, escolha o valor booleano ativo; para tipos analógicos, informe o setpoint. Communication ativa quando a quality não é Good; não usa setpoint. Não trate System como um limite comum de processo. 4. Defina prioridade (Low, Medium, High ou Critical), classe, área e mensagem. Em Comportamento do alarme, revise habilitação, atraso, reconhecimento, shelving e perfil sonoro; use a prévia do som e observe as limitações abaixo. 5. Salve e valide com valores de teste representativos. 6. Publique e ative a revisão conforme o fluxo do projeto; confira o estado no Runtime Alarm Center.",
                "1. In Engineering, open Alarms and create a definition. 2. Enter a name and the TAG path. 3. Choose Digital, High, HighHigh, Low, LowLow, Communication or System. For Digital, choose the active boolean value; for analog types, enter the setpoint. Communication activates when quality is not Good; it does not use a setpoint. Do not treat System as an ordinary process limit. 4. Set priority (Low, Medium, High or Critical), class, area and message. In Alarm behavior, review enablement, delay, acknowledgement, shelving and sound profile; preview the sound and observe the limitations below. 5. Save and validate with representative test values. 6. Publish and activate the revision using the project workflow; check the state in Runtime Alarm Center.",
                "1. En Engineering, abra Alarmas y cree una definición. 2. Introduzca nombre y ruta del TAG. 3. Elija Digital, High, HighHigh, Low, LowLow, Communication o System. Para Digital, elija el valor booleano activo; para tipos analógicos, introduzca el setpoint. Communication se activa cuando la quality no es Good; no usa setpoint. No trate System como un límite de proceso común. 4. Defina prioridad (Low, Medium, High o Critical), clase, área y mensaje. En Comportamiento de la alarma, revise habilitación, retardo, reconocimiento, shelving y perfil sonoro; pruebe el sonido y observe las limitaciones siguientes. 5. Guarde y valide con valores de prueba representativos. 6. Publique y active la revisión según el flujo del proyecto; compruebe el estado en Runtime Alarm Center."),
            Tx(
                "A definição fica no projeto e a revisão ativa alimenta o Runtime. O estado pode aparecer como Normal, Active, Acknowledged, Returned, Disabled ou Shelved. Reconhecer confirma que o operador tratou a notificação; não muda o valor do TAG nem elimina a condição de processo.",
                "The definition belongs to the project and the active revision supplies Runtime. State can appear as Normal, Active, Acknowledged, Returned, Disabled or Shelved. Acknowledgement records that an operator handled the notification; it does not change the TAG value or remove the process condition.",
                "La definición pertenece al proyecto y la revisión activa alimenta Runtime. El estado puede aparecer como Normal, Active, Acknowledged, Returned, Disabled o Shelved. El reconocimiento registra que un operador atendió la notificación; no cambia el valor del TAG ni elimina la condición del proceso."),
            Tx(
                "O alarme não ativa: confira habilitação, TAG, tipo e revisão ativa; em Digital, confirme o valor ativo. Shelving pode ocultar temporariamente a notificação. Limitações atuais: Engineering salva Atraso de ativação, mas o Runtime atual não o usa para atrasar o alarme. Desmarcar Requer reconhecimento também ainda não dispensa o ACK: um alarme retornado continua pendente na lista ativa. Considere essas limitações ao testar; não dependa dessas opções até a correção e validação do comportamento.",
                "The alarm does not activate: check enablement, TAG, type and active revision; for Digital, confirm the active value. Shelving may temporarily suppress notification. Current limitations: Engineering saves Activation delay, but current Runtime does not use it to delay the alarm. Clearing Requires acknowledgement also does not yet remove the ACK requirement: a returned alarm remains pending in the active list. Account for these limitations in testing; do not rely on these options until their behavior is corrected and verified.",
                "La alarma no se activa: compruebe habilitación, TAG, tipo y revisión activa; en Digital, confirme el valor activo. Shelving puede suprimir temporalmente la notificación. Limitaciones actuales: Engineering guarda Retardo de activación, pero Runtime actual no lo usa para retrasar la alarma. Desmarcar Requiere reconocimiento tampoco elimina todavía el requisito de ACK: una alarma retornada sigue pendiente en la lista activa. Considere estas limitaciones al probar; no dependa de estas opciones hasta corregir y validar el comportamiento."),
            Tx(
                "Compare o valor atual do TAG com o tipo e setpoint: High/HighHigh ativa no limite ou acima; Low/LowLow ativa no limite ou abaixo. Confirme o horário da última transição, atraso, habilitação e shelving. Se o operador não conseguir reconhecer, confira a sessão e a capacidade AlarmAcknowledge. Use primeiro uma simulação controlada; não altere limites de processo só para silenciar o alarme.",
                "Compare the current TAG value with the type and setpoint: High/HighHigh activates at or above the limit; Low/LowLow activates at or below it. Check the last-transition time, delay, enablement and shelving. If an operator cannot acknowledge, check the session and AlarmAcknowledge capability. Start with a controlled simulation; do not change process limits merely to silence an alarm.",
                "Compare el valor actual del TAG con el tipo y el setpoint: High/HighHigh se activa en el límite o por encima; Low/LowLow se activa en el límite o por debajo. Compruebe la hora de la última transición, el retardo, la habilitación y shelving. Si el operador no puede reconocer, revise la sesión y la capacidad AlarmAcknowledge. Empiece con una simulación controlada; no cambie límites del proceso solo para silenciar una alarma."),
            "alarms.configuration", "alarms.runtime-operation", "operational-events.overview", "historian.overview", "runtime.history"),

        TaskGuide(
            "operational-events.overview", "operational-events",
            Tx("Eventos Operacionais", "Operational Events", "Eventos Operacionales"),
            Tx("Defina ocorrências operacionais e consulte as emissões no histórico.", "Define operational occurrences and inspect emitted events in history.", "Defina ocurrencias operacionales y consulte las emisiones en el historial."),
            Tx(
                "Use Operational Event para registrar um marco que aconteceu e deve ser consultável depois, como uma etapa concluída ou mudança de modo. É uma ocorrência, não o estado contínuo de Alarm nem a trilha de ações de Audit.",
                "Use an Operational Event to record a milestone that happened and should be queryable later, such as a completed step or mode change. It is an occurrence, not a continuous Alarm state or the Audit trail of actions.",
                "Use Operational Event para registrar un hito ocurrido que debe ser consultable después, como una etapa completada o un cambio de modo. Es una ocurrencia, no el estado continuo de Alarm ni la trazabilidad de acciones de Audit."),
            Tx(
                "Abra o projeto Working correto com uma sessão autorizada a editar Engineering. Defina uma Key e um Name estáveis, além de Type, Category e Source para identificar tipo, grupo e origem. Se usar Server Script, a definição precisa estar na revisão ativa e a chamada recebe seu ID estável.",
                "Open the correct Working project with a session authorized to edit Engineering. Define a stable Key and Name, plus Type, Category and Source to identify kind, group and origin. If you use a Server Script, the definition must be in the active revision and the call uses its stable ID.",
                "Abra el proyecto Working correcto con una sesión autorizada para editar Engineering. Defina una Key y un Name estables, además de Type, Category y Source para identificar tipo, grupo y origen. Si usa Server Script, la definición debe estar en la revisión activa y la llamada utiliza su ID estable."),
            Tx(
                "1. Abra Eventos Operacionais em Engineering e crie uma definição. 2. Informe Key, Name, Type, Category e Source. 3. Preencha opcionalmente Area, Equipment Path, TAG, mensagem e metadados para dar contexto. 4. Salve a Working e publique/ative a revisão quando estiver pronta. 5. Configure a fonte do Runtime para emitir essa definição. Em Server Script, use emit_operational_event(ID, mensagem, contexto); os valores do contexto devem ser textos. 6. Abra o navegador de histórico e selecione operational.events para consultar as ocorrências.",
                "1. Open Operational Events in Engineering and create a definition. 2. Enter Key, Name, Type, Category and Source. 3. Optionally fill in Area, Equipment Path, TAG, message and metadata to add context. 4. Save Working and publish/activate the revision when ready. 5. Configure the Runtime source to emit this definition. In a Server Script, use emit_operational_event(ID, message, context); context values must be strings. 6. Open the historical browser and select operational.events to query occurrences.",
                "1. Abra Eventos Operacionales en Engineering y cree una definición. 2. Introduzca Key, Name, Type, Category y Source. 3. Complete opcionalmente Area, Equipment Path, TAG, mensaje y metadatos para aportar contexto. 4. Guarde Working y publique/active la revisión cuando esté lista. 5. Configure la fuente de Runtime para emitir esta definición. En Server Script, use emit_operational_event(ID, mensaje, contexto); los valores del contexto deben ser textos. 6. Abra el navegador histórico y seleccione operational.events para consultar las ocurrencias."),
            Tx(
                "Cada emissão válida gera uma ocorrência imutável com horário e os dados da definição, além do contexto fornecido naquele momento. As linhas podem ser consultadas no conjunto operacional do histórico; não alteram por si só o estado de um TAG nem reconhecem um alarme.",
                "Each valid emission creates an immutable occurrence with a timestamp and definition data, plus the context supplied at that time. Rows can be queried from the operational history dataset; they do not change a TAG state or acknowledge an alarm.",
                "Cada emisión válida crea una ocurrencia inmutable con hora y datos de la definición, además del contexto proporcionado en ese momento. Las filas se consultan en el conjunto operativo del historial; no cambian por sí solas el estado de un TAG ni reconocen una alarma."),
            Tx(
                "A chamada falha ou nada aparece: verifique se o ID aponta para uma definição existente na revisão ativa, se o script está autorizado e se os parâmetros têm formato válido. Escolha operational.events, não alarm.events; amplie o intervalo de tempo e confira se o Runtime já emitiu a ocorrência.",
                "The call fails or nothing appears: check that the ID identifies a definition present in the active revision, that the script is authorized and that parameters are valid. Select operational.events, not alarm.events; widen the time range and verify that Runtime emitted the occurrence.",
                "La llamada falla o no aparece nada: verifique que el ID identifique una definición presente en la revisión activa, que el script esté autorizado y que los parámetros sean válidos. Seleccione operational.events, no alarm.events; amplíe el intervalo de tiempo y confirme que Runtime emitió la ocurrencia."),
            Tx(
                "Revise a mensagem de diagnóstico do Runtime e a definição/ID na revisão publicada. Confirme Type, Category e Source e, se usar Server Script, consulte o tópico scripts.server para sintaxe e permissões. Depois filtre pelo horário de emissão e dataset operacional; não procure eventos de processo no Audit.",
                "Review the Runtime diagnostic and the definition/ID in the published revision. Confirm Type, Category and Source; if you use a Server Script, consult scripts.server for syntax and permissions. Then filter by emission time and the operational dataset; do not look for process events in Audit.",
                "Revise el diagnóstico de Runtime y la definición/ID en la revisión publicada. Confirme Type, Category y Source; si usa Server Script, consulte scripts.server para sintaxis y permisos. Después filtre por la hora de emisión y el conjunto operacional; no busque eventos de proceso en Audit."),
            "operational-events.configuration", "alarms.overview", "historian.overview", "scripts.server"),

        TaskGuide(
            "audit.overview", "audit",
            Tx("Auditoria", "Audit", "Auditoría"),
            Tx("Investigue ações administrativas e de Engineering pelos filtros de auditoria.", "Investigate administrative and Engineering actions with audit filters.", "Investigue acciones administrativas y de Engineering con los filtros de auditoría."),
            Tx(
                "Use Audit para investigar quem executou uma ação, quando ocorreu, qual foi o resultado e qual recurso foi afetado. Registra ações instrumentadas pelo produto; não é histórico de valores, não representa cada mudança de processo e não substitui Alarm ou Operational Event. A consulta exige SystemAdmin.",
                "Use Audit to investigate who performed an action, when it happened, the outcome and affected resource. It records actions instrumented by the product; it is not value history, does not represent every process change and does not replace Alarm or Operational Event. Queries require SystemAdmin.",
                "Use Audit para investigar quién realizó una acción, cuándo ocurrió, el resultado y el recurso afectado. Registra acciones instrumentadas por el producto; no es historial de valores, no representa cada cambio del proceso y no sustituye Alarm ni Operational Event. La consulta requiere SystemAdmin."),
            Tx(
                "Tenha uma sessão com a capacidade SystemAdmin da política ativa. Se possível, conheça o período, usuário, ação ou recurso que quer investigar. Os horários informados na interface são locais e enviados ao serviço em UTC.",
                "Use a session granted SystemAdmin by the active policy. If possible, know the time period, user, action or resource you want to investigate. Times entered in the interface are local and sent to the service as UTC.",
                "Use una sesión con SystemAdmin otorgado por la política activa. Si es posible, conozca el período, usuario, acción o recurso que desea investigar. Las horas introducidas en la interfaz son locales y se envían al servicio en UTC."),
            Tx(
                "1. Abra Audit. 2. Defina De/Até para limitar o período; confirme o fuso horário local. 3. Se necessário, filtre por Usuário/subject, Ação, Resultado (Sucesso, Negado ou Falha), Tipo de recurso, Recurso/chave, Área ou Correlation ID. 4. Escolha de 1 a 1000 eventos por página (padrão 100) e clique Consultar. 5. Selecione uma linha para conferir identidade, horário, resultado, recurso e metadados. 6. Se houver mais resultados, use Próxima e mantenha os filtros para não perder o contexto.",
                "1. Open Audit. 2. Set From/To to bound the period; confirm the local time zone. 3. If needed, filter by User/subject, Action, Outcome (Succeeded, Denied or Failed), Resource kind, Resource/key, Area or Correlation ID. 4. Choose 1 to 1,000 events per page (default 100) and select Query. 5. Select a row to inspect identity, time, outcome, resource and metadata. 6. If more results exist, select Next and keep the filters to preserve context.",
                "1. Abra Audit. 2. Defina Desde/Hasta para limitar el período; confirme la zona horaria local. 3. Si hace falta, filtre por Usuario/subject, Acción, Resultado (Éxito, Denegado o Falla), Tipo de recurso, Recurso/clave, Área o Correlation ID. 4. Elija de 1 a 1000 eventos por página (predeterminado 100) y pulse Consultar. 5. Seleccione una fila para revisar identidad, hora, resultado, recurso y metadatos. 6. Si hay más resultados, pulse Próxima y mantenga los filtros para conservar el contexto."),
            Tx(
                "A tabela mostra os registros de Audit que correspondem aos filtros, com resultado e dados de rastreabilidade disponíveis. Um resultado vazio significa apenas que não foram retornados registros para aqueles filtros e período; não prova que nenhum evento de processo ocorreu.",
                "The table shows Audit records matching the filters, with available outcomes and traceability data. An empty result only means no records were returned for those filters and period; it does not prove that no process event occurred.",
                "La tabla muestra los registros de Audit que coinciden con los filtros, con resultados y datos de trazabilidad disponibles. Un resultado vacío solo significa que no se devolvieron registros para esos filtros y período; no demuestra que no ocurrió ningún evento de proceso."),
            Tx(
                "Acesso negado: solicite SystemAdmin conforme a política de segurança. Sem resultados: amplie o período, remova filtros estreitos e verifique se procurava uma ação, um alarme ou um evento operacional. Página incompleta: avance a paginação e reduza o tamanho se a consulta estiver pesada.",
                "Access denied: request SystemAdmin under the security policy. No results: widen the period, remove narrow filters and check whether you were looking for an action, an alarm or an operational event. Incomplete page: continue pagination and reduce the page size if the query is heavy.",
                "Acceso denegado: solicite SystemAdmin según la política de seguridad. Sin resultados: amplíe el período, quite filtros estrechos y verifique si buscaba una acción, una alarma o un evento operacional. Página incompleta: continúe la paginación y reduzca el tamaño si la consulta está pesada."),
            Tx(
                "Confirme primeiro o acesso SystemAdmin e o intervalo From/To convertido do horário local. Depois use um filtro por vez: Outcome para ações negadas/falhas, Resource Kind/ID para um alvo e Correlation ID para agrupar registros relacionados. Para saúde do armazenamento, use Diagnostics; para leituras e ocorrências de processo, use Historian e não Audit.",
                "First confirm SystemAdmin access and the From/To period converted from local time. Then use one filter at a time: Outcome for denied/failed actions, Resource Kind/ID for a target, and Correlation ID to group related records. For storage health, use Diagnostics; for process readings and occurrences, use Historian, not Audit.",
                "Primero confirme el acceso SystemAdmin y el intervalo From/To convertido desde la hora local. Después use un filtro a la vez: Outcome para acciones denegadas/fallidas, Resource Kind/ID para un objetivo y Correlation ID para agrupar registros relacionados. Para la salud del almacenamiento, use Diagnostics; para lecturas y ocurrencias de proceso, use Historian, no Audit."),
            "security.users-roles-capabilities", "diagnostics.overview", "historian.overview"),

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

        Detailed(
            "bindings-commands.overview", "bindings-commands",
            Tx("Vínculos de dados e comandos", "Data bindings and commands", "Vínculos de datos y comandos"),
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
            "screens.overview", "screens",
            Tx("Criar e editar uma Tela", "Create and edit a Screen", "Crear y editar una Pantalla"),
            Tx(
                "Uma Tela organiza objetos visuais, propriedades e referências a recursos do projeto para apresentar uma operação no Runtime.",
                "A Screen organizes visual objects, properties, and references to project resources to present an operation in Runtime.",
                "Una Pantalla organiza objetos visuales, propiedades y referencias a recursos del proyecto para presentar una operación en Runtime."),
            Tx(
                "Use o editor para criar uma composição operacional, associar valores de TAG a objetos e configurar comportamento visual ou interação. Para conteúdo reutilizável em janela contextual, consulte Popups.",
                "Use the editor to create an operational layout, bind TAG values to objects, and configure visual behavior or interaction. For reusable content in a contextual window, see Popups.",
                "Use el editor para crear una composición operacional, asociar valores TAG a objetos y configurar comportamiento visual o interacción. Para contenido reutilizable en una ventana contextual, consulte Popups."),
            Tx(
                "Tenha o projeto correto aberto e uma sessão com acesso de Engineering. Identifique as TAGs e os objetos que a Tela deve apresentar; para comandos ou escrita, confirme quais ações o Runtime permite e as permissões da sessão.",
                "Have the correct project open and an Engineering-enabled session. Identify the TAGs and objects the Screen should present; for commands or writes, check that Runtime supports those actions and that the session permissions allow them.",
                "Tenga abierto el proyecto correcto y una sesión con acceso de Engineering. Identifique los TAGs y objetos que debe presentar la Pantalla; para comandos o escrituras, confirme qué acciones permite Runtime y los permisos de la sesión."),
            Tx(
                "1. Abra Engineering > Telas e selecione uma Tela existente ou crie uma nova. 2. Informe o nome e confira a identidade estável; o endereço de navegação é a rota usada para abrir a Tela e não substitui sua identidade. 3. Escolha o formato Runtime default, Paisagem (1920 × 1080) ou Retrato (1080 × 1920), conforme o uso. No Runtime, a Tela inteira é ajustada para caber no dispositivo. 4. Adicione objetos pela paleta e posicione/dimensione na área de edição visual. Use o zoom; a barra de ferramentas da área visual oferece alinhamento, distribuição, agrupamento e bloqueio. Selecione objetos na área visual ou pela Estrutura. 5. Ajuste propriedades no Inspector. Para mostrar um valor, configure o binding de propriedade visual para uma TAG compatível. Use Dynamics para mudanças visuais condicionais e Events para interações suportadas; veja os tópicos relacionados. 6. Execute Preview e confira layout, referências e diagnósticos. Preview não altera o Working. 7. Quando estiver válido, use Apply ao Workspace. Para disponibilizar a edição no Runtime, siga separadamente o lifecycle de salvar, publicar e ativar a revisão apropriada.",
                "1. Open Engineering > Screens and select an existing Screen or create a new one. 2. Enter its name and check its stable identity; the navigation route is the address used to open the Screen and does not replace its identity. 3. Choose Runtime default, Landscape (1920 × 1080), or Portrait (1080 × 1920) for the intended use. In Runtime, the complete Screen is scaled to fit the device. 4. Add objects from the palette and position/resize them on the design area. Use zoom; the visual-area toolbar provides alignment, distribution, grouping, and locking. Select objects in the visual area or in the Structure. 5. Edit properties in the Inspector. To display a value, bind a visual property to a compatible TAG. Use Dynamics for conditional visual changes and Events for supported interactions; see the related topics. 6. Run Preview and check the layout, references, and diagnostics. Preview does not mutate Working. 7. When valid, use Apply to Workspace. To make the edit available in Runtime, separately follow the lifecycle to save, publish, and activate the appropriate revision.",
                "1. Abra Engineering > Pantallas y seleccione una Pantalla existente o cree una nueva. 2. Introduzca su nombre y compruebe la identidad estable; la ruta de navegación es la dirección usada para abrir la Pantalla y no sustituye su identidad. 3. Elija Runtime default, Paisaje (1920 × 1080) o Retrato (1080 × 1920), según el uso. En Runtime, toda la Pantalla se escala para ajustarse al dispositivo. 4. Añada objetos desde la paleta y colóquelos/cambie su tamaño en el área visual. Use el zoom; la barra de herramientas del área visual ofrece alineación, distribución, agrupación y bloqueo. Seleccione objetos en el área visual o en Estructura. 5. Edite propiedades en el Inspector. Para mostrar un valor, asocie una propiedad visual a un TAG compatible. Use Dynamics para cambios visuales condicionales y Events para interacciones admitidas; consulte los temas relacionados. 6. Ejecute Preview y compruebe el diseño, las referencias y los diagnósticos. Preview no modifica Working. 7. Cuando sea válida, use Aplicar al Workspace. Para que la edición esté disponible en Runtime, siga por separado el lifecycle para guardar, publicar y activar la revisión apropiada."),
            Tx(
                "A Tela configurada e validada é aplicada ao Workspace; o Preview permanece apenas uma conferência. A operação no Runtime depende das etapas explícitas do lifecycle e da revisão ativada.",
                "The configured and validated Screen is applied to the Workspace; Preview remains a validation only. Runtime operation depends on the explicit lifecycle steps and the activated revision.",
                "La Pantalla configurada y validada se aplica al Workspace; Preview solo sirve para comprobarla. La operación en Runtime depende de los pasos explícitos del lifecycle y de la revisión activada."),
            Tx(
                "Área de edição vazia ou objeto ausente; formato ou rota confundidos com identidade; referência de TAG não resolvida ou incompatível; Preview com erro; Apply rejeitado porque o Workspace mudou.",
                "Empty design area or missing object; format or route confused with identity; unresolved or incompatible TAG reference; Preview error; Apply rejected because the Workspace changed.",
                "Área visual vacía u objeto ausente; formato o ruta confundidos con identidad; referencia TAG sin resolver o incompatible; error en Preview; Apply rechazado porque cambió el Workspace."),
            Tx(
                "Confirme primeiro a Tela selecionada e procure o objeto também na Estrutura. Confira separadamente o formato e a rota, sem tratar rota como identidade estável. Revise a TAG e o tipo da propriedade no Inspector; abra Dynamics ou Events conforme o comportamento esperado. Leia os diagnósticos do Preview e, se o Workspace mudou, atualize o estado e execute Preview novamente antes de aplicar. Para saber por que a Tela ainda não aparece no Runtime, confira o lifecycle e a revisão Active.",
                "First confirm the selected Screen and look for the object in the Structure as well. Check format and route separately; do not treat the route as stable identity. Review the TAG and property type in the Inspector; open Dynamics or Events according to the expected behavior. Read Preview diagnostics and, if the Workspace changed, refresh the state and run Preview again before applying. To understand why the Screen is not yet visible in Runtime, check the lifecycle and Active revision.",
                "Primero confirme la Pantalla seleccionada y busque también el objeto en Estructura. Compruebe por separado el formato y la ruta; no trate la ruta como identidad estable. Revise el TAG y el tipo de propiedad en el Inspector; abra Dynamics o Events según el comportamiento esperado. Lea los diagnósticos de Preview y, si cambió el Workspace, actualice el estado y vuelva a ejecutar Preview antes de aplicar. Para saber por qué la Pantalla aún no aparece en Runtime, compruebe el lifecycle y la revisión Active."),
            "engineering.lifecycle", "tags.overview", "tags.addressing", "visual.properties", "visual.dynamics", "visual.events", "popups.overview", "dynamos.overview", "diagnostics.overview"),

        TaskGuide(
            "popups.overview", "popups",
            Tx("Criar e editar um Popup", "Create and edit a Popup", "Crear y editar un Popup"),
            Tx(
                "Um Popup é uma composição visual contextual, com identidade própria e referência opcional a Template, aberta no Runtime por uma interação suportada.",
                "A Popup is a contextual visual composition with its own identity and an optional Template reference, opened in Runtime through a supported interaction.",
                "Un Popup es una composición visual contextual, con identidad propia y referencia opcional a una Plantilla, que se abre en Runtime mediante una interacción compatible."),
            Tx(
                "Use quando uma interação suportada da Screen precisar mostrar conteúdo contextual reutilizável. Abrir um Popup não concede permissões: commands e escritas continuam sujeitos à mesma autorização.",
                "Use it when a supported Screen interaction needs to show reusable contextual content. Opening a Popup grants no permissions: commands and writes remain subject to the same authorization.",
                "Úselo cuando una interacción compatible de una Pantalla necesite mostrar contenido contextual reutilizable. Abrir un Popup no concede permisos: commands y escrituras siguen sujetos a la misma autorización."),
            Tx(
                "Tenha o projeto correto aberto em Working e uma sessão com acesso de Engineering. Decida se o Popup será independente ou usará um Template; identifique TAGs, assets e interações suportadas antes de configurar referências.",
                "Have the correct project open in Working and an Engineering-enabled session. Decide whether the Popup is standalone or uses a Template; identify TAGs, assets, and supported interactions before configuring references.",
                "Tenga abierto el proyecto correcto en Working y una sesión con acceso de Engineering. Decida si el Popup será independiente o usará una Plantilla; identifique TAGs, assets e interacciones compatibles antes de configurar referencias."),
            Tx(
                "1. Abra Engineering > Popups e selecione um Popup existente ou Novo Popup. 2. Informe Nome de exibição e Identificador; mantenha o identificador único e estável, pois ele referencia o Popup. 3. Deixe Template vazio para um Popup independente ou informe a referência do Template desejado. 4. Adicione objetos pela paleta à área de edição visual; selecione-os na composição ou em Estrutura e ajuste propriedades, bindings e comportamento no Inspector. Consulte TAGs, Propriedades, Dynamics e Events para esses detalhes. 5. Confira a composição, os limites lógicos e a posição no Runtime apresentados pelo editor. A posição é limitada à área lógica para manter o Popup acessível. 6. Execute Preview e corrija os diagnósticos; Preview não altera o Working. 7. Quando o candidato estiver válido, use Apply ao Workspace. Para abrir o Popup no Runtime, configure uma interação suportada na Screen e, separadamente, salve, publique e ative a revisão apropriada.",
                "1. Open Engineering > Popups and select an existing Popup or New Popup. 2. Enter the Display name and Identifier; keep the identifier unique and stable because it references the Popup. 3. Leave Template empty for a standalone Popup, or enter the intended Template reference. 4. Add objects from the palette to the design area; select them on the composition or in Structure and edit properties, bindings, and behavior in the Inspector. See TAGs, Properties, Dynamics, and Events for those details. 5. Check the composition, logical bounds, and Runtime position shown by the editor. Position is constrained to the logical area so the Popup remains reachable. 6. Run Preview and resolve its diagnostics; Preview does not mutate Working. 7. When the candidate is valid, select Apply to Workspace. To open the Popup in Runtime, configure a supported interaction on the Screen and separately save, publish, and activate the appropriate revision.",
                "1. Abra Engineering > Popups y seleccione un Popup existente o Nuevo Popup. 2. Introduzca Nombre visible e Identificador; mantenga el identificador único y estable porque referencia el Popup. 3. Deje Plantilla vacía para un Popup independiente o introduzca la referencia de la Plantilla deseada. 4. Añada objetos desde la paleta al área visual; selecciónelos en la composición o en Estructura y ajuste propiedades, bindings y comportamiento en el Inspector. Consulte TAGs, Propiedades, Dynamics y Events para esos detalles. 5. Compruebe la composición, los límites lógicos y la posición en Runtime que muestra el editor. La posición se limita al área lógica para mantener el Popup accesible. 6. Ejecute Preview y corrija sus diagnósticos; Preview no modifica Working. 7. Cuando el candidato sea válido, seleccione Aplicar al Workspace. Para abrir el Popup en Runtime, configure una interacción compatible en la Pantalla y, por separado, guarde, publique y active la revisión apropiada."),
            Tx(
                "O Popup validado é aplicado ao Engineering Workspace e pode ser referenciado por interações suportadas de uma Screen. O Runtime só usa essa edição quando ela faz parte da revisão ativada; abrir o Popup não aumenta as permissões da sessão.",
                "The validated Popup is applied to the Engineering Workspace and can be referenced by supported Screen interactions. Runtime uses the edit only when it is part of the activated revision; opening the Popup does not increase session permissions.",
                "El Popup validado se aplica al Engineering Workspace y puede referenciarse desde interacciones compatibles de una Pantalla. Runtime usa la edición solo cuando forma parte de la revisión activada; abrir el Popup no aumenta los permisos de la sesión."),
            Tx(
                "Identificador duplicado; Template ou referência inválida; objeto não localizado; binding de TAG incompatível ou asset ausente; Preview com erro; Apply recusado porque o Workspace mudou.",
                "Duplicate identifier; invalid Template or reference; object not found; incompatible TAG binding or missing asset; Preview errors; Apply rejected because the Workspace changed.",
                "Identificador duplicado; Plantilla o referencia no válida; objeto no localizado; binding TAG incompatible o asset ausente; error de Preview; Apply rechazado porque cambió el Workspace."),
            Tx(
                "Confirme o Popup selecionado e procure o objeto também em Estrutura. Revise se a referência de Template e os bindings apontam para recursos existentes e compatíveis. Leia os erros e avisos do Preview antes de aplicar. Se o Workspace mudou durante a validação, recarregue o estado e execute Preview de novo. Se não aparecer no Runtime, confirme a interação configurada na Screen e a revisão Active; uma rejeição de comando continua sujeita à autorização da sessão.",
                "Confirm the selected Popup and look for the object in Structure as well. Check that the Template reference and bindings point to existing, compatible resources. Read Preview errors and warnings before applying. If the Workspace changed during validation, reload its state and run Preview again. If the Popup does not appear in Runtime, check the Screen interaction and Active revision; command rejection remains subject to session authorization.",
                "Confirme el Popup seleccionado y busque también el objeto en Estructura. Revise que la referencia de Plantilla y los bindings apunten a recursos existentes y compatibles. Lea los errores y advertencias de Preview antes de aplicar. Si el Workspace cambió durante la validación, recargue su estado y ejecute Preview de nuevo. Si no aparece en Runtime, compruebe la interacción configurada en la Pantalla y la revisión Active; el rechazo de un command sigue sujeto a la autorización de la sesión."),
            "engineering.lifecycle", "screens.overview", "tags.overview", "visual.properties", "visual.dynamics", "visual.events", "dynamos.overview", "diagnostics.overview"),

        TaskGuide(
            "dynamos.overview", "dynamos",
            Tx("Revisar e adaptar um Dínamo", "Review and adapt a Dynamo", "Revisar y adaptar un Dínamo"),
            Tx(
                "Um Dínamo é um desenho visual reutilizável. Sua definição é compartilhada: ao atualizá-la, todas as telas ligadas recebem o novo desenho quando a revisão correspondente é ativada.",
                "A Dynamo is a reusable visual drawing. Its definition is shared: when you update it, every linked screen receives the new drawing when the corresponding revision is activated.",
                "Un Dínamo es un dibujo visual reutilizable. Su definición es compartida: al actualizarla, todas las pantallas vinculadas reciben el nuevo dibujo cuando se activa la revisión correspondiente."),
            Tx(
                "Use quando precisar reutilizar uma composição visual em várias telas ou fornecer valores diferentes, como TAGs ou comandos, em cada uso. Para alterar apenas uma tela, confira primeiro se o objeto não é uma definição compartilhada.",
                "Use it when reusing a visual composition across screens or supplying different values, such as TAGs or commands, for each use. If you intend to change only one screen, first check whether the object is a shared definition.",
                "Úselo para reutilizar una composición visual en varias pantallas o proporcionar valores distintos, como TAGs o comandos, en cada uso. Si quiere cambiar solo una pantalla, compruebe primero si el objeto es una definición compartida."),
            Tx(
                "Abra o projeto correto em Engineering e identifique as telas que usam a definição, pois a edição é compartilhada. Tenha em mãos os recursos que cada instância deverá receber. A tela Dínamos lista definições existentes e não cria uma definição nova; se a lista estiver vazia, consulte Bibliotecas reutilizáveis para verificar se há uma biblioteca disponível.",
                "Open the correct project in Engineering and identify the screens that use the definition because edits are shared. Have the resources each instance should receive ready. The Dynamos page lists existing definitions and does not create a new one; if the list is empty, check Reusable Libraries for an available library.",
                "Abra el proyecto correcto en Engineering e identifique las pantallas que usan la definición, porque la edición es compartida. Prepare los recursos que recibirá cada instancia. La página Dínamos muestra definiciones existentes y no crea una nueva; si la lista está vacía, consulte Bibliotecas reutilizables para comprobar si hay una biblioteca disponible."),
            Tx(
                "1. Abra Engineering > Dínamos, pesquise a definição e confira a prévia e as contagens de objetos e parâmetros. 2. Selecione Editar desenho. No editor, escolha o objeto na área visual ou em Estrutura; use a alça de rotação da seleção para ajustar a orientação e o Inspector para revisar posição, tamanho e propriedades. 3. Abra Interface pública apenas se cada uso precisar receber valores próprios. Adicione um parâmetro com o tipo adequado — Referência TAG, Booleano, Numérico, Texto, Caminho de equipamento, Fonte de valor ou Comando — e marque Obrigatório quando cada instância tiver de fornecer o valor. TAGs e comandos são associados em cada instância, não fixados na definição compartilhada. 4. Na tela que usa o Dínamo, selecione a instância e configure os valores no Inspector. Assim, a mesma definição pode representar equipamentos diferentes. 5. O Preview de estado permite conferir visualmente qualidade, estado, falha, alarme e intenção de comando; é apenas uma conferência visual local: não salva os ajustes nem envia comandos ao processo. 6. Execute o Preview principal, leia os diagnósticos e confirme que todas as telas vinculadas devem receber a alteração. Quando estiver correto, use Apply ao Workspace. 7. Salve, publique e ative a revisão em etapas separadas; depois confira as telas relevantes no Runtime.",
                "1. Open Engineering > Dynamos, search for the definition, and review its preview and object/parameter counts. 2. Select Edit drawing. In the editor, select an object on the design area or in Structure; use the selection rotation handle to adjust its orientation and the Inspector to review position, size, and properties. 3. Open Public interface only when each use needs its own values. Add a parameter of the appropriate type — TAG reference, Boolean, Number, String, Equipment path, Value source, or Command — and mark it Required when every instance must provide a value. TAGs and commands are assigned on each instance, not fixed in the shared definition. 4. On a screen that uses the Dynamo, select its instance and set the values in the Inspector. The same definition can then represent different equipment. 5. State Preview lets you check quality, state, fault, alarm, and command intent visually; it is only a local visual check: it does not save settings or send commands to the process. 6. Run the main Preview, read its diagnostics, and confirm that every linked screen should receive the change. When it is correct, select Apply to Workspace. 7. Save, publish, and activate the revision as separate steps; then check the relevant screens in Runtime.",
                "1. Abra Engineering > Dínamos, busque la definición y revise su vista previa y los recuentos de objetos y parámetros. 2. Seleccione Editar dibujo. En el editor, seleccione el objeto en el área visual o en Estructura; use el asa de rotación de la selección para ajustar la orientación y el Inspector para revisar posición, tamaño y propiedades. 3. Abra Interfaz pública solo si cada uso necesita valores propios. Añada un parámetro del tipo adecuado — Referencia TAG, Booleano, Numérico, Texto, Ruta de equipo, Fuente de valor o Comando — y márquelo Obligatorio cuando cada instancia deba proporcionar un valor. Los TAGs y comandos se asignan en cada instancia, no se fijan en la definición compartida. 4. En una pantalla que use el Dínamo, seleccione la instancia y configure los valores en el Inspector. Así, una misma definición puede representar equipos distintos. 5. Preview de estado permite revisar visualmente calidad, estado, falla, alarma e intención de comando; es solo una comprobación visual local: no guarda los ajustes ni envía comandos al proceso. 6. Ejecute el Preview principal, lea los diagnósticos y confirme que todas las pantallas vinculadas deban recibir el cambio. Cuando sea correcto, seleccione Aplicar al Workspace. 7. Guarde, publique y active la revisión en pasos separados; después compruebe en Runtime las pantallas pertinentes."),
            Tx(
                "O desenho validado atualiza a definição compartilhada sem remover os vínculos existentes. Cada instância mantém seus próprios valores; o Runtime só recebe a edição quando a revisão que contém o Apply é ativada.",
                "The validated drawing updates the shared definition without removing existing links. Each instance keeps its own values; Runtime receives the edit only when the revision containing the Apply is activated.",
                "El dibujo validado actualiza la definición compartida sin eliminar los vínculos existentes. Cada instancia conserva sus propios valores; Runtime recibe la edición solo cuando se activa la revisión que contiene el Apply."),
            Tx(
                "A lista está vazia; objeto bloqueado; parâmetro obrigatório sem valor na instância; TAG ou Command ausente; rotação aplicada ao objeto errado; Preview acusa incompatibilidade; ou uma alteração aparece em várias telas porque a definição é compartilhada.",
                "The list is empty; an object is locked; a required parameter has no instance value; a TAG or Command is missing; rotation was applied to the wrong object; Preview reports an incompatibility; or a change appears on multiple screens because the definition is shared.",
                "La lista está vacía; un objeto está bloqueado; falta un valor de instancia para un parámetro obligatorio; falta un TAG o Command; la rotación se aplicó al objeto equivocado; Preview informa una incompatibilidad; o un cambio aparece en varias pantallas porque la definición es compartida."),
            Tx(
                "Se não encontrar a definição, confirme o projeto/Workspace e consulte Bibliotecas reutilizáveis; esta página não cria Dynamos. Para girar um objeto, selecione-o na área visual ou em Estrutura, confirme que não está bloqueado e use sua alça de rotação. Se faltar um valor, confira a Interface pública da definição e o Inspector da instância. Leia os erros do Preview antes de aplicar. Se a mudança atingir telas não esperadas, reverta o rascunho e confirme quais telas compartilham a definição. O Preview de estado nunca testa uma saída real. Se o Runtime ainda mostrar o desenho anterior, confirme Save/Publish/Activate e a revisão ativa.",
                "If you cannot find the definition, confirm the project/Workspace and check Reusable Libraries; this page does not create Dynamos. To rotate an object, select it on the design area or in Structure, make sure it is not locked, and use its rotation handle. If a value is missing, check the definition's Public interface and the instance Inspector. Resolve Preview errors before applying. If the change affects unexpected screens, revert the draft and confirm which screens share the definition. State Preview never tests a real output. If Runtime still shows the previous drawing, confirm Save/Publish/Activate and the active revision.",
                "Si no encuentra la definición, confirme el proyecto/Workspace y consulte Bibliotecas reutilizables; esta página no crea Dínamos. Para girar un objeto, selecciónelo en el área visual o en Estructura, confirme que no esté bloqueado y use su asa de rotación. Si falta un valor, revise la Interfaz pública de la definición y el Inspector de la instancia. Corrija los errores de Preview antes de aplicar. Si el cambio afecta pantallas inesperadas, revierta el borrador y confirme qué pantallas comparten la definición. Preview de estado nunca prueba una salida real. Si Runtime aún muestra el dibujo anterior, confirme Save/Publish/Activate y la revisión activa."),
            "screens.overview", "tags.overview", "bindings-commands.overview", "visual.properties", "visual.dynamics", "libraries.reusable-resources", "engineering.lifecycle", "diagnostics.overview"),

        TaskGuide(
            "gateway.overview", "gateway",
            Tx("Configurar uma rota TAG Gateway", "Configure a TAG Gateway route", "Configurar una ruta TAG Gateway"),
            Tx(
                "TAG Gateway transfere valores entre TAGs autoritativas do servidor. Ele é um serviço de roteamento do Runtime, não um driver de protocolo nem uma conexão feita pelo navegador.",
                "TAG Gateway transfers values between server-authoritative TAGs. It is a Runtime routing service, not a protocol driver or a connection made by the browser.",
                "TAG Gateway transfiere valores entre TAGs autoritativos del servidor. Es un servicio de enrutamiento de Runtime, no un driver de protocolo ni una conexión realizada por el navegador."),
            Tx(
                "Use uma rota para levar valores de uma TAG origem até uma TAG destino gravável, sem criar acoplamento entre drivers. Cada rota tem configuração e diagnóstico próprios.",
                "Use a route to carry values from a source TAG to a writable destination TAG without coupling drivers. Each route has its own configuration and diagnostics.",
                "Use una ruta para llevar valores de un TAG origen a un TAG destino escribible, sin acoplar drivers. Cada ruta tiene su propia configuración y diagnóstico."),
            Tx(
                "Abra o projeto correto em Engineering com acesso de edição. A origem deve pertencer a uma Data Source habilitada e ser acessível pelo servidor. Client Memory pertence ao navegador e não pode ser origem nem destino do Gateway. A simulação integrada pode fornecer a origem, mas não pode receber o destino. Escolha TAGs diferentes e confirme que a TAG destino é gravável.",
                "Open the correct project in Engineering with edit access. The source must belong to an enabled Data Source and be available to the server. Client Memory belongs to the browser and cannot be a Gateway source or destination. Built-in simulation may provide the source but cannot receive the destination. Choose different TAGs and make sure the destination TAG is writable.",
                "Abra el proyecto correcto en Engineering con acceso de edición. El origen debe pertenecer a una Data Source habilitada y estar disponible para el servidor. Client Memory pertenece al navegador y no puede ser origen ni destino del Gateway. La simulación integrada puede proporcionar el origen, pero no recibir el destino. Elija TAGs distintos y confirme que el TAG destino sea escribible."),
            Tx(
                "1. Abra Engineering > TAG Gateway. Na lista, pesquise e selecione uma rota existente para editá-la, ou escolha Nova rota. 2. Informe o nome da rota; ele também é sua chave estável, então mantenha-o único e significativo. Escolha se fica habilitada. 3. Selecione a TAG origem e a TAG destino; não use a mesma TAG nos dois campos. 4. Escolha OnChange para transferir quando o valor aceitável mudar, ou Periodic para transferir periodicamente o último valor Good. 5. Defina a inicialização: sincronizar o primeiro valor aceitável ou esperar a próxima atualização. Em OnChange, configure opcionalmente Deadband e intervalo mínimo em milissegundos; em Periodic, informe o período em milissegundos. 6. Mantenha Exact quando os tipos forem compatíveis sem conversão. Use CheckedNumeric somente entre tipos numéricos e informe Gain/Offset quando necessário. O Gateway aceita somente qualidade Good; essa regra não é editável. 7. Escolha Preview da rota e leia a validade, as contagens e os erros; corrija os erros antes de continuar. 8. Quando o candidato estiver válido, escolha Apply ao Workspace. Depois salve, publique e ative a revisão em etapas separadas.",
                "1. Open Engineering > TAG Gateway. Search and select an existing route to edit it, or choose New route. 2. Enter the route name; it is also the stable key, so keep it unique and meaningful. Choose whether it is enabled. 3. Select the source and destination TAGs; do not use the same TAG in both fields. 4. Choose OnChange to transfer when an acceptable value changes, or Periodic to transfer the latest Good value at intervals. 5. Set startup behavior: synchronize the first acceptable value or wait for the next update. In OnChange, optionally configure Deadband and minimum interval in milliseconds; in Periodic, enter the period in milliseconds. 6. Keep Exact when the data types are compatible without conversion. Use CheckedNumeric only between numeric types and enter Gain/Offset when needed. The Gateway accepts Good quality only; this rule cannot be edited. 7. Select Preview route and review validity, counts, and errors; fix errors before continuing. 8. When the candidate is valid, select Apply to Workspace. Then save, publish, and activate the revision as separate steps.",
                "1. Abra Engineering > TAG Gateway. Busque y seleccione una ruta existente para editarla o elija Nueva ruta. 2. Introduzca el nombre de la ruta; también es su clave estable, así que manténgala única y significativa. Elija si queda habilitada. 3. Seleccione los TAGs de origen y destino; no use el mismo TAG en ambos campos. 4. Elija OnChange para transferir cuando cambie un valor aceptable, o Periodic para transferir periódicamente el último valor Good. 5. Defina el inicio: sincronizar el primer valor aceptable o esperar la próxima actualización. En OnChange, configure opcionalmente Deadband e intervalo mínimo en milisegundos; en Periodic, indique el período en milisegundos. 6. Mantenga Exact cuando los tipos sean compatibles sin conversión. Use CheckedNumeric solo entre tipos numéricos e indique Gain/Offset cuando sea necesario. El Gateway solo acepta calidad Good; esta regla no se puede editar. 7. Seleccione Preview de ruta y revise validez, recuentos y errores; corríjalos antes de continuar. 8. Cuando el candidato sea válido, seleccione Aplicar al Workspace. Después guarde, publique y active la revisión en pasos separados."),
            Tx(
                "A rota validada fica no Engineering Workspace. Ela só participa do Runtime quando estiver habilitada e fizer parte da revisão publicada e ativada. O diagnóstico de Runtime mostra estado, origem/destino, transferências, valores ignorados/coalescidos e falhas de escrita.",
                "The validated route is stored in the Engineering Workspace. It participates in Runtime only when enabled and included in the published, activated revision. Runtime diagnostics show state, source/destination, transfers, skipped/coalesced values, and write failures.",
                "La ruta validada queda en el Engineering Workspace. Solo participa en Runtime cuando está habilitada y forma parte de la revisión publicada y activada. El diagnóstico de Runtime muestra estado, origen/destino, transferencias, valores omitidos/coalescidos y fallas de escritura."),
            Tx(
                "Lista de TAGs vazia; Data Source desabilitada; destino somente leitura; origem e destino iguais; nome de rota repetido; conversão Exact incompatível; CheckedNumeric usado com TAG não numérica; Deadband negativo ou aplicado a tipo não numérico; intervalo fora do limite; mais de uma rota ativa gravando no mesmo destino.",
                "Empty TAG list; disabled Data Source; read-only destination; identical source and destination; duplicate route name; incompatible Exact conversion; CheckedNumeric with a non-numeric TAG; negative Deadband or one used with a non-numeric type; interval out of range; more than one active route writing to the same destination.",
                "Lista de TAGs vacía; Data Source deshabilitada; destino de solo lectura; origen y destino iguales; nombre de ruta duplicado; conversión Exact incompatible; CheckedNumeric con un TAG no numérico; Deadband negativo o usado con un tipo no numérico; intervalo fuera del límite; más de una ruta activa escribiendo en el mismo destino."),
            Tx(
                "Na tela de Preview, leia o código e a mensagem de cada erro; confirme se a Data Source está habilitada e se a TAG destino aceita escrita. Se o Workspace mudou durante a validação, recarregue-o e faça Preview novamente. Após ativar a revisão, use Atualizar diagnóstico. Se não houver rotas ativas, confira se a rota está habilitada e se a revisão correta foi publicada/ativada. Compare estado, contagem de falhas consecutivas e Último erro. Diagnóstico de falha da rota Gateway é separado da conectividade da Data Source e da qualidade da TAG origem.",
                "In Preview, read each error code and message; confirm the Data Source is enabled and the destination TAG allows writes. If the Workspace changed during validation, reload it and run Preview again. After activating the revision, use Refresh diagnostics. If there are no active routes, check that the route is enabled and the correct revision was published/activated. Review state, consecutive failure count, and Last error. Gateway route failures are diagnosed separately from Data Source connectivity and source TAG quality.",
                "En Preview, lea el código y el mensaje de cada error; confirme que la Data Source esté habilitada y que el TAG destino permita escrituras. Si cambió el Workspace durante la validación, recárguelo y vuelva a ejecutar Preview. Después de activar la revisión, use Actualizar diagnóstico. Si no hay rutas activas, compruebe que la ruta esté habilitada y que se haya publicado/activado la revisión correcta. Revise estado, recuento de fallas consecutivas y Último error. Las fallas de rutas Gateway se diagnostican por separado de la conectividad de Data Source y la calidad del TAG origen."),
            "tags.overview", "tags.writeability", "sources.data-sources", "drivers.overview", "engineering.lifecycle", "diagnostics.overview"),

        TaskGuide(
            "sources.data-sources", "sources",
            Tx("Configurar uma Data Source", "Configure a Data Source", "Configurar una Data Source"),
            Tx(
                "Uma Data Source define como o projeto obtém dados; o tipo escolhido fornece os campos, formatos e ferramentas que o produto realmente suporta.",
                "A Data Source defines how the project obtains data; the selected type provides the fields, formats, and tools the product actually supports.",
                "Una Data Source define cómo obtiene datos el proyecto; el tipo elegido proporciona los campos, formatos y herramientas que el producto realmente admite."),
            Tx(
                "Use ao criar ou ajustar uma origem antes de associar TAGs a ela. Para parâmetros de protocolo específicos, consulte o tópico do Driver selecionado.",
                "Use when creating or adjusting a source before binding TAGs to it. For protocol-specific settings, consult the topic for the selected Driver.",
                "Úselo al crear o ajustar una fuente antes de asociarle TAGs. Para parámetros específicos del protocolo, consulte el tema del Driver seleccionado."),
            Tx(
                "Tenha uma sessão com acesso de Engineering, o projeto correto aberto e os dados de conexão fornecidos pelo responsável pelo equipamento/serviço. Se o schema mostrar uma referência de credencial protegida, configure essa referência no campo correspondente; não coloque segredos em campos de configuração comuns.",
                "Have an Engineering-enabled session, the correct project open, and connection details supplied by the equipment/service owner. If the schema shows a protected credential-reference field, configure that reference there; do not put secrets in ordinary configuration fields.",
                "Tenga una sesión con acceso de Engineering, el proyecto correcto abierto y los datos de conexión proporcionados por el responsable del equipo/servicio. Si el schema muestra un campo de referencia de credencial protegida, configure allí esa referencia; no escriba secretos en campos de configuración comunes."),
            Tx(
                "1. Abra Engineering > Fontes de dados e selecione uma origem existente ou Nova Fonte de dados. 2. Escolha um tipo disponível no catálogo. 3. Informe o nome e confira o estado Habilitado. 4. Preencha somente os campos apresentados pelo schema; formatos, exemplos, limites e valores permitidos aparecem junto aos campos. Abra configurações avançadas apenas quando necessário. 5. Use Connection Test, Discovery ou outra ferramenta somente quando o tipo e a tela oferecerem essa capability. O teste de conexão de um rascunho não aplica alterações. 6. Execute Preview, leia avisos/erros e corrija o rascunho; se o Workspace mudar, faça Preview novamente. 7. Use Apply somente com candidato válido. Para refletir no Runtime, salve/publique pelo fluxo de Engineering e ative a revisão conforme autorizado.",
                "1. Open Engineering > Data Sources and select an existing source or New Data Source. 2. Choose a type available in the catalog. 3. Enter the name and check the Enabled state. 4. Fill only fields shown by the schema; formats, examples, limits, and allowed values appear with the fields. Open advanced settings only when needed. 5. Use Connection Test, Discovery, or another tool only when the type and screen provide that capability. A connection test on a draft does not apply changes. 6. Run Preview, read warnings/errors, and correct the draft; if the Workspace changes, Preview again. 7. Use Apply only with a valid candidate. To affect Runtime, save/publish through the Engineering workflow and activate the revision when authorized.",
                "1. Abra Engineering > Fuentes de datos y seleccione una fuente existente o Nueva Fuente de datos. 2. Elija un tipo disponible en el catálogo. 3. Introduzca el nombre y compruebe el estado Habilitado. 4. Complete solamente los campos mostrados por el schema; los formatos, ejemplos, límites y valores permitidos aparecen junto a los campos. Abra las opciones avanzadas solo cuando sea necesario. 5. Use Connection Test, Discovery u otra herramienta solamente cuando el tipo y la pantalla ofrezcan esa capability. Una prueba de conexión de un borrador no aplica cambios. 6. Ejecute Preview, lea advertencias/errores y corrija el borrador; si cambia el Workspace, vuelva a ejecutar Preview. 7. Use Apply solo con un candidato válido. Para reflejarlo en Runtime, guarde/publique mediante el flujo de Engineering y active la revisión cuando esté autorizado."),
            Tx(
                "A configuração validada é aplicada ao Workspace. Aplicar não altera por si só a revisão Active do Runtime; salvar/publicar e ativar são etapas distintas.",
                "The validated configuration is applied to the Workspace. Apply does not by itself change Runtime's Active revision; save/publish and activate are separate steps.",
                "La configuración validada se aplica al Workspace. Apply no cambia por sí solo la revisión Active de Runtime; guardar/publicar y activar son pasos distintos."),
            Tx(
                "Catálogo indisponível; tipo antigo não suportado; campo obrigatório, formato ou endpoint incorreto; credencial/certificado rejeitado; configurações incompatíveis após trocar o tipo; teste de conexão falhou; Preview encontrou conflito ou o Workspace mudou.",
                "Catalog unavailable; unsupported legacy type; missing required field, wrong format or endpoint; rejected credential/certificate; incompatible settings after changing type; connection test failed; Preview found a conflict or the Workspace changed.",
                "Catálogo no disponible; tipo antiguo no soportado; campo obligatorio, formato o endpoint incorrecto; credencial/certificado rechazado; opciones incompatibles tras cambiar el tipo; falló la prueba de conexión; Preview encontró un conflicto o cambió el Workspace."),
            Tx(
                "Se o catálogo falhar, use recarregar e confirme a saúde do backend. Compare cada campo com o schema e o tópico do Driver; não copie sintaxe de outro protocolo. Em configurações incompatíveis, remova somente as opções identificadas pelo editor e preencha as do novo tipo. Corrija falhas de endpoint/rede/credencial antes de investigar TAGs. Após Apply, salve a aplicação pelo fluxo de Engineering; consulte Lifecycle para Publish/Activate.",
                "If the catalog fails, reload it and check backend health. Compare each field with the schema and Driver topic; do not copy syntax from another protocol. For incompatible settings, remove only the options identified by the editor and fill in those for the new type. Fix endpoint/network/credential failures before investigating TAGs. After Apply, save the application through Engineering; see Lifecycle for Publish/Activate.",
                "Si falla el catálogo, vuelva a cargarlo y compruebe la salud del backend. Compare cada campo con el schema y el tema del Driver; no copie sintaxis de otro protocolo. Ante opciones incompatibles, elimine solo las identificadas por el editor y complete las del nuevo tipo. Corrija los fallos de endpoint/red/credencial antes de investigar los TAGs. Después de Apply, guarde la aplicación mediante Engineering; consulte Lifecycle para Publish/Activate."),
            "drivers.overview", "tags.addressing", "engineering.lifecycle", "diagnostics.overview"),

        TaskGuide(
            "tags.overview", "tags",
            Tx("Criar e configurar um TAG", "Create and configure a TAG", "Crear y configurar un TAG"),
            Tx(
                "Um TAG representa uma variável do projeto: tem nome, tipo de dado e, quando aplicável, uma origem e um endereço. O Runtime apresenta seu valor com qualidade e horário.",
                "A TAG represents a project variable: it has a name and data type and, when applicable, a source and address. Runtime presents its value with quality and timestamp.",
                "Un TAG representa una variable del proyecto: tiene nombre y tipo de dato y, cuando corresponde, un origen y una dirección. Runtime presenta su valor con calidad y hora."),
            Tx(
                "Use ao integrar um ponto de equipamento, criar uma variável interna ou preparar um valor para telas, alarmes, scripts e histórico. Defina primeiro o tipo e a origem; os campos de endereço dependem da origem escolhida.",
                "Use it to connect an equipment point, create an internal variable, or prepare a value for screens, alarms, scripts, and history. Set the type and source first; address fields depend on the selected source.",
                "Úselo para integrar un punto de equipo, crear una variable interna o preparar un valor para pantallas, alarmas, scripts e histórico. Defina primero el tipo y el origen; los campos de dirección dependen del origen elegido."),
            Tx(
                "Tenha o projeto correto aberto e uma sessão com acesso de Engineering. Identifique o sinal, seu tipo de dado e unidade, a Data Source adequada e se o valor deve ser somente leitura ou precisa de histórico. Configure a Data Source antes do TAG quando ela ainda não existir.",
                "Have the correct project open and an Engineering-enabled session. Identify the signal, its data type and unit, the appropriate Data Source, and whether the value should be read-only or historized. Configure the Data Source first if it does not exist yet.",
                "Tenga abierto el proyecto correcto y una sesión con acceso de Engineering. Identifique la señal, su tipo de dato y unidad, la Data Source adecuada y si el valor debe ser solo lectura o necesita histórico. Configure primero la Data Source si aún no existe."),
            Tx(
                "1. Abra Engineering > TAGs e selecione um TAG existente ou Novo TAG. 2. Informe um nome claro. Ao criar, o caminho de referência é derivado do nome e espaços viram _; ao renomear um TAG existente, o caminho não muda. Confira se o caminho desejado não colide com outro TAG. 3. Escolha o tipo de dado compatível com o sinal e selecione a Data Source correta. Se trocar a origem de um TAG existente, endereço e binding anteriores são limpos; configure novamente os campos apresentados para a nova origem. 4. Preencha somente o endereço e os parâmetros oferecidos pela origem selecionada. Use o assistente ou as instruções daquele Driver; não copie sintaxe de outro protocolo. 5. Informe unidade e escala apenas quando necessárias. Scaling altera o valor de engenharia; formatação de casas decimais altera somente a apresentação. Marque Somente leitura quando o ponto não deve aceitar escrita; a possibilidade real de escrita também depende da origem e das permissões. 6. Abra Captura do Historian apenas se precisar registrar amostras históricas e configure os campos conforme a política do projeto. 7. Quando origem e binding estiverem configurados, use Testar leitura ou Monitor por alguns segundos se essas opções forem oferecidas. Esse teste é transitório: não altera Working, não ativa, não escreve no processo e não grava amostras no Historian. 8. Execute Preview, leia os diagnósticos e corrija o rascunho. Preview não altera o Working. Quando o candidato estiver válido, use Apply ao Workspace. Para disponibilizar a mudança no Runtime, salve, publique e ative a revisão apropriada em etapas separadas.",
                "1. Open Engineering > TAGs and select an existing TAG or New TAG. 2. Enter a clear name. When creating, the reference path is derived from the name and spaces become _; renaming an existing TAG does not rename its path. Check that the intended path does not conflict with another TAG. 3. Choose a data type that matches the signal and select the correct Data Source. If you change the source of an existing TAG, the previous address and binding are cleared; configure the fields offered by the new source again. 4. Fill only the address and settings offered by the selected source. Use its assistant or Driver guidance; do not copy syntax from another protocol. 5. Set units and scaling only when needed. Scaling changes the engineering value; decimal formatting changes presentation only. Mark the TAG read-only when it must not accept writes; actual write access also depends on the source and permissions. 6. Open Historian capture only when historical samples are required and set its fields according to project policy. 7. After the source and binding are configured, use Test read or Monitor for a few seconds if those options are offered. This test is transient: it does not change Working, activate, write the process, or store Historian samples. 8. Run Preview, read its diagnostics, and correct the draft. Preview does not mutate Working. When the candidate is valid, select Apply to Workspace. To make the change available in Runtime, save, publish, and activate the appropriate revision as separate steps.",
                "1. Abra Engineering > TAGs y seleccione un TAG existente o Nuevo TAG. 2. Introduzca un nombre claro. Al crear, la ruta de referencia se deriva del nombre y los espacios se convierten en _; renombrar un TAG existente no cambia su ruta. Compruebe que la ruta deseada no entre en conflicto con otro TAG. 3. Elija un tipo de dato compatible con la señal y seleccione la Data Source correcta. Si cambia el origen de un TAG existente, se borran la dirección y el binding anteriores; vuelva a configurar los campos ofrecidos por el nuevo origen. 4. Complete solamente la dirección y los parámetros que ofrece el origen seleccionado. Use el asistente o las instrucciones de ese Driver; no copie sintaxis de otro protocolo. 5. Configure unidades y scaling solo cuando sea necesario. Scaling cambia el valor de ingeniería; el formato decimal cambia solo la presentación. Marque el TAG como solo lectura cuando no deba aceptar escrituras; la posibilidad real de escritura también depende del origen y los permisos. 6. Abra la captura de Historian solo si necesita guardar muestras históricas y configure los campos según la política del proyecto. 7. Cuando el origen y el binding estén configurados, use Probar lectura o Monitor durante unos segundos si se ofrecen esas opciones. Esta prueba es transitoria: no cambia Working, no activa, no escribe en el proceso ni guarda muestras en Historian. 8. Ejecute Preview, lea los diagnósticos y corrija el borrador. Preview no modifica Working. Cuando el candidato sea válido, seleccione Aplicar al Workspace. Para que el cambio esté disponible en Runtime, guarde, publique y active la revisión apropiada en pasos separados."),
            Tx(
                "O TAG validado é aplicado ao Workspace com sua origem, tipo e binding configurados. A leitura de teste é temporária e não altera processo nem histórico. Runtime passa a usar a edição somente quando ela integra uma revisão ativada.",
                "The validated TAG is applied to the Workspace with its source, type, and binding configured. A test read is temporary and does not change the process or history. Runtime uses the edit only when it is part of an activated revision.",
                "El TAG validado se aplica al Workspace con su origen, tipo y binding configurados. La lectura de prueba es temporal y no cambia el proceso ni el histórico. Runtime usa la edición solo cuando forma parte de una revisión activada."),
            Tx(
                "Caminho duplicado; Data Source ausente ou não resolvida; endereço ou binding inválido para a origem; tipo de dado incompatível; unidade ou scaling incorretos; Testar leitura retorna BAD, NO_DATA ou leitura instável; Preview aponta erro ou o Workspace mudou.",
                "Duplicate path; missing or unresolved Data Source; address or binding invalid for the source; incompatible data type; incorrect unit or scaling; Test read returns BAD, NO_DATA, or an unstable read; Preview reports an error or the Workspace changed.",
                "Ruta duplicada; Data Source ausente o no resuelta; dirección o binding no válido para el origen; tipo de dato incompatible; unidad o scaling incorrectos; Probar lectura devuelve BAD, NO_DATA o una lectura inestable; Preview informa un error o cambió el Workspace."),
            Tx(
                "Confirme o TAG e a Data Source selecionados. Se a origem foi trocada, configure novamente endereço e binding usando os campos do tipo escolhido. Para Testar leitura, confira o estado, a qualidade, o endereço e os valores bruto/convertido; BAD, NO_DATA ou leitura instável indicam que a origem, conexão, endereço, tipo ou transformação precisam ser verificados. O teste não substitui a operação no Runtime. Leia os diagnósticos do Preview; se o Workspace mudou, recarregue e faça Preview novamente antes de aplicar. Se não houver valor no Runtime, confirme Save/Publish/Activate e consulte TAG quality, timestamps e Diagnostics. Se uma escrita for rejeitada, confira Read-only, capacidade da origem e autorização da sessão.",
                "Confirm the selected TAG and Data Source. If the source changed, configure the address and binding again using the fields for that source type. For Test read, check the state, quality, address, and raw/converted values; BAD, NO_DATA, or an unstable read means the source, connection, address, type, or transformation needs checking. The test does not replace Runtime operation. Read Preview diagnostics; if the Workspace changed, reload it and run Preview again before applying. If Runtime has no value, confirm Save/Publish/Activate and consult TAG quality, timestamps, and Diagnostics. If a write is rejected, check read-only state, source capability, and session authorization.",
                "Confirme el TAG y la Data Source seleccionados. Si cambió el origen, configure de nuevo la dirección y el binding con los campos de ese tipo. Para Probar lectura, revise el estado, la calidad, la dirección y los valores bruto/convertido; BAD, NO_DATA o una lectura inestable indican que hay que revisar el origen, la conexión, la dirección, el tipo o la transformación. La prueba no sustituye la operación en Runtime. Lea los diagnósticos de Preview; si cambió el Workspace, recárguelo y vuelva a ejecutar Preview antes de aplicar. Si Runtime no muestra valor, confirme Save/Publish/Activate y consulte calidad de TAG, timestamps y Diagnostics. Si se rechaza una escritura, revise solo lectura, capacidad del origen y autorización de la sesión."),
            "sources.data-sources", "tags.addressing", "tags.quality", "tags.timestamps", "tags.writeability", "tags.scaling-formatting", "historian.overview", "engineering.lifecycle", "diagnostics.overview", "tags.copy-duplicate-sequential"),

        TaskGuide(
            "engineering.lifecycle", "engineering",
            Tx("Editar, salvar, publicar e ativar o projeto", "Edit, save, publish and activate a project", "Editar, guardar, publicar y activar el proyecto"),
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
            Tx("Pacote portátil e autocontido de uma aplicação SCADIVO.", "Portable self-contained package for a SCADIVO application.", "Paquete portátil y autocontenido de una aplicación SCADIVO."),
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
            Tx("Dinâmicas visuais", "Visual dynamics", "Dinámicas visuales"),
            Tx("Comportamentos Boolean, mapeamento por faixa numérica, preenchimento analógico e expressões tipadas ligam fontes do projeto a propriedades visuais.", "Boolean conditions, numeric range mapping, analog fill, and typed expressions connect project sources to visual properties.", "Condiciones Boolean, mapeo por rangos numéricos, relleno analógico y expresiones tipadas conectan fuentes del proyecto con propiedades visuales."),
            Tx("Use condição Boolean para estado, intervalo numérico para limites, Property Map para estados tipados por faixa ou Analog Fill para nível/escala.", "Use a Boolean condition for state, numeric interval for limits, Property Map for typed range states, or Analog Fill for a level/scale.", "Use condición Boolean para estado, intervalo numérico para límites, Property Map para estados tipados por rango o Analog Fill para nivel/escala."),
            Tx("Selecione objeto e destino suportado; confirme fonte TAG, Client Memory ou expressão e seu tipo Boolean/Number.", "Select an object and supported destination; confirm the TAG, Client Memory, or expression source and its Boolean/Number type.", "Seleccione objeto y destino compatible; confirme la fuente TAG, Client Memory o expresión y su tipo Boolean/Number."),
            Tx("Escolha condição direta ou Numeric Interval; defina mínimo/máximo, inclusão de borda e Inside/Outside. Property Map usa regras em ordem, primeira correspondência vence. Analog Fill requer faixa de entrada, cor e direção; revise Clamp/InvertScale.", "Choose a direct condition or Numeric Interval; set minimum/maximum, boundary inclusion, and Inside/Outside. Property Map rules are ordered and the first match wins. Analog Fill requires input range, color, and direction; review Clamp/InvertScale.", "Elija condición directa o Numeric Interval; defina mínimo/máximo, inclusión de bordes e Inside/Outside. Property Map evalúa reglas en orden y gana la primera coincidencia. Analog Fill requiere rango, color y dirección; revise Clamp/InvertScale."),
            Tx("A propriedade muda conforme a regra no Runtime. A Dynamic altera apresentação e não grava o resultado de volta no TAG de origem.", "The property changes according to the rule in Runtime. A Dynamic changes presentation and does not write the result back to the source TAG.", "La propiedad cambia según la regla en Runtime. Una Dynamic cambia la presentación y no escribe el resultado en el TAG de origen."),
            Tx("Propriedade não suportada, incompatibilidade Boolean/Number, TAG indisponível ou com qualidade inadequada, limites invertidos, faixas sobrepostas ou fallback ausente.", "Unsupported property, Boolean/Number mismatch, unavailable TAG or unsuitable quality, reversed bounds, overlapping ranges, or missing fallback.", "Propiedad no compatible, incompatibilidad Boolean/Number, TAG no disponible o quality inadecuada, límites invertidos, rangos superpuestos o fallback ausente."),
            Tx("Confirme destino no inspetor, tipo da fonte e qualidade. Em Property Map, revise ordem/limites; em Analog Fill, máximo deve superar mínimo. Corrija o primeiro erro em Diagnostics.", "Confirm the destination in the inspector, source type, and quality. For Property Map, review order/bounds; for Analog Fill, maximum must exceed minimum. Fix the first Diagnostics error.", "Confirme destino en el inspector, tipo de fuente y quality. En Property Map, revise orden/límites; en Analog Fill, el máximo debe superar el mínimo. Corrija el primer error de Diagnostics."),
            "visual.properties", "engineering.object-browser", "tags.quality"),

        TaskGuide(
            "visual.events", "visual",
            Tx("Eventos visuais", "Visual events", "Eventos visuales"),
            Tx("Ações rápidas de clique e associação de Scripts a interações suportadas por objetos Screen e Popup.", "Quick click actions and Script associations with interactions supported by Screen and Popup objects.", "Acciones rápidas de clic y asociaciones de Scripts con interacciones compatibles de objetos Screen y Popup."),
            Tx("Use Click para definir valor de TAG, alternar Boolean, abrir Popup, navegar para Screen ou executar parâmetro Command. Use eventos de ponteiro para Scripts de interação.", "Use Click to set a TAG value, toggle Boolean, open a Popup, navigate to a Screen, or execute a Command parameter. Use pointer events for interaction Scripts.", "Use Click para definir valor TAG, alternar Boolean, abrir Popup, navegar a Screen o ejecutar parámetro Command. Use eventos de puntero para Scripts de interacción."),
            Tx("Selecione objeto com painel Events, TAG gravável e destino existente. O Runtime/servidor ainda verifica a autorização efetiva.", "Select an object with an Events panel, writable TAG, and existing destination. Runtime/server still checks effective authorization.", "Seleccione objeto con panel Events, TAG escribible y destino existente. Runtime/servidor sigue comprobando autorización efectiva."),
            Tx("Em Quick event, selecione ação e alvo; para TAG value informe valor compatível, para Toggle use Boolean. Para Script, escolha objectInteraction/Click ou Pointer Enter/Move/Leave e associe o handler declarado. Valide, salve e siga o lifecycle.", "In Quick event, select action and target; for TAG value enter a compatible value, for Toggle use Boolean. For a Script, choose objectInteraction/Click or Pointer Enter/Move/Leave and bind its declared handler. Validate, save, and follow the lifecycle.", "En Quick event, seleccione acción y destino; para TAG value introduzca valor compatible y para Toggle use Boolean. Para Script, elija objectInteraction/Click o Pointer Enter/Move/Leave y asocie su handler declarado. Valide, guarde y siga el ciclo de vida."),
            Tx("A interação executa somente a ação configurada e continua sujeita às permissões da sessão.", "The interaction runs only the configured action and remains subject to session permissions.", "La interacción ejecuta solo la acción configurada y sigue sujeta a los permisos de la sesión."),
            Tx("Alvo removido, tipo de TAG incompatível, handler não declarado ou permissão insuficiente para Command/escrita.", "Removed target, incompatible TAG type, undeclared handler, or insufficient Command/write permission.", "Destino eliminado, tipo TAG incompatible, handler no declarado o permiso insuficiente para Command/escritura."),
            Tx("Confirme Screen/Popup e revisão ativas; revalide alvo, tipo e handler. Leia feedback do Runtime, Diagnostics e Audit para rejeições; não repita comando ambíguo sem confirmar seu estado.", "Confirm the active Screen/Popup and revision; revalidate target, type, and handler. Read Runtime feedback, Diagnostics, and Audit for rejections; do not repeat an ambiguous command without checking state.", "Confirme Screen/Popup y revisión activas; revalide destino, tipo y handler. Lea feedback de Runtime, Diagnostics y Audit para rechazos; no repita un comando ambiguo sin confirmar su estado."),
            "bindings-commands.overview", "scripts.engineering", "audit.overview"),

        TaskGuide(
            "scripts.engineering", "scripts",
            Tx("Editor de Scripts", "Script Editor", "Editor de Scripts"),
            Tx("Área de Engineering para criar, revisar e configurar Scripts e seus eventos. Client Visual e Server têm execução e APIs diferentes.", "Engineering area for creating, reviewing, and configuring Scripts and their events. Client Visual and Server have different execution scopes and APIs.", "Área de Engineering para crear, revisar y configurar Scripts y sus eventos. Client Visual y Server tienen ejecución y APIs diferentes."),
            Tx("Use para criar automação do servidor ou lógica associada à Screen/Popup. Consulte o guia Client Visual antes de importar APIs ou exemplos para esse escopo.", "Use for server automation or logic associated with a Screen/Popup. Consult the Client Visual guide before using APIs or examples for that scope.", "Úselo para automatización del servidor o lógica asociada a Screen/Popup. Consulte la guía Client Visual antes de usar APIs o ejemplos de ese alcance."),
            Tx("Tenha permissão de Engineering, escolha o escopo correto, configure os entry points/eventos e declare as dependências exigidas.", "Have Engineering permission, choose the correct scope, configure entry points/events, and declare the required dependencies.", "Tenga permiso de Engineering, elija el alcance correcto, configure entry points/eventos y declare las dependencias requeridas."),
            Tx("Selecione o Script; configure escopo e evento; edite no Editor de código; use validação/diagnósticos; revise referências; salve e siga o lifecycle.", "Select the Script; configure scope and event; edit in the code editor; use validation/diagnostics; review references; save and follow lifecycle.", "Seleccione el Script; configure scope y evento; edite en el Editor de código; use validación/diagnósticos; revise referencias; guarde y siga el lifecycle."),
            Tx("O Script salvo permanece associado ao Working e só participa da operação quando o lifecycle correspondente permitir.", "The saved Script remains associated with Working and only participates in operation when the corresponding lifecycle allows it.", "El Script guardado permanece asociado a Working y solo participa en operación cuando el lifecycle correspondiente lo permite."),
            Tx("Erro de sintaxe, trigger incompleto, referência ausente, API não disponível ou execução bloqueada.", "Syntax error, incomplete trigger, missing reference, unavailable API, or blocked execution.", "Error de sintaxis, trigger incompleto, referencia ausente, API no disponible o ejecución bloqueada."),
            Tx("Use os diagnósticos do editor, confira trigger e dependências e abra a referência de API correspondente antes de alterar a lógica.", "Use editor diagnostics, check trigger and dependencies, and open the corresponding API reference before changing logic.", "Use los diagnósticos del editor, compruebe trigger y dependencias y abra la referencia de API correspondiente antes de cambiar la lógica."),
            "scripts.python-validation", "scripts.client-visual", "engineering.object-browser", "scripts.server"),

        Detailed(
            "scripts.client-visual", "scripts",
            Tx("Scripts Client Visual: APIs e exemplos", "Client Visual Scripts: APIs and examples", "Scripts Client Visual: APIs y ejemplos"),
            Tx("Guia de Python executado no Runtime do cliente para interação e apresentação visual.", "Guide to Python that runs in the client Runtime for interaction and visual presentation.", "Guía de Python ejecutado en el Runtime cliente para interacción y presentación visual."),
            S(Tx("Escolha Client Visual", "Choose Client Visual", "Elegir Client Visual"), Tx(
                "Client Visual executa na Screen/Popup aberta e atende interações de quem opera. Server executa no backend, associado à revisão Active. Use Server Scripts para funções, eventos, limites e APIs do backend. Client Visual não acessa diretamente Driver, banco, arquivos, sistema operacional, credenciais nem rede arbitrária.",
                "Client Visual runs on the open Screen/Popup for operator interactions. Server runs in the backend against the Active revision. Use Server Scripts for backend functions, events, limits, and APIs. Client Visual has no direct access to Drivers, database, files, operating system, credentials, or arbitrary network.",
                "Client Visual se ejecuta en la Screen/Popup abierta para interacciones del operador. Server se ejecuta en el backend sobre la revisión Active. Use Server Scripts para funciones, eventos, límites y APIs del backend. Client Visual no accede directamente a Drivers, base de datos, archivos, sistema operativo, credenciales ni red arbitraria.")),
            S(Tx("Criar e associar o handler", "Create and associate the handler", "Crear y asociar el handler"), Tx(
                "Crie um Script Python com escopo Client Visual. Declare o entry point `objectInteraction` com handler, por exemplo `on_click`. No painel Events do objeto, selecione Click, Script e handler; valide e salve. O nome do handler deve coincidir exatamente. Também são aceitos Pointer enters object, Pointer moves over object e Pointer leaves object.",
                "Create a Python Script with Client Visual scope. Declare an `objectInteraction` entry point with a handler such as `on_click`. In the object's Events panel, select Click, Script, and handler; validate and save. The handler name must match exactly. Pointer enters object, Pointer moves over object, and Pointer leaves object are also supported.",
                "Cree un Script Python con alcance Client Visual. Declare el entry point `objectInteraction` con un handler como `on_click`. En Events del objeto, seleccione Click, Script y handler; valide y guarde. El nombre debe coincidir exactamente. También se admiten Pointer enters object, Pointer moves over object y Pointer leaves object.")),
            S(Tx("Funções disponíveis", "Available functions", "Funciones disponibles"), Tx(
                "Importe do módulo `elite_scada`: `tag_read` e `tag_write`; `client_memory_read` e `client_memory_write`; `visual_property_read`, `visual_property_write` e `visual_property_clear`; `visual_tween_request`. Use `await` nas chamadas assíncronas. `tag_read` retorna dados da TAG incluindo `value`, `quality`, `timestamp` e `source`. Declare dependências TAG no Script. Escritas passam pelas permissões e validações normais do Runtime.",
                "Import from the `elite_scada` module: `tag_read` and `tag_write`; `client_memory_read` and `client_memory_write`; `visual_property_read`, `visual_property_write`, and `visual_property_clear`; `visual_tween_request`. Use `await` for asynchronous calls. `tag_read` returns TAG data including `value`, `quality`, `timestamp`, and `source`. Declare TAG dependencies on the Script. Writes use normal Runtime permissions and validation.",
                "Importe del módulo `elite_scada`: `tag_read` y `tag_write`; `client_memory_read` y `client_memory_write`; `visual_property_read`, `visual_property_write` y `visual_property_clear`; `visual_tween_request`. Use `await` en llamadas asíncronas. `tag_read` devuelve datos TAG como `value`, `quality`, `timestamp` y `source`. Declare dependencias TAG en el Script. Las escrituras usan permisos y validación normales de Runtime.")),
            S(Tx("Exemplo: ler e validar uma TAG", "Example: read and validate a TAG", "Ejemplo: leer y validar un TAG"), Tx(
                "Associe `on_click` ao evento Click e declare `Plant.Pump.DischargePressure` como dependência. Faça uma leitura e só use o valor quando quality for Good. Leitura ausente ou qualidade ruim não equivale a zero.",
                "Associate `on_click` with Click and declare `Plant.Pump.DischargePressure` as a dependency. Read the TAG and use its value only when quality is Good. A missing reading or bad quality is not zero.",
                "Asocie `on_click` a Click y declare `Plant.Pump.DischargePressure` como dependencia. Lea el TAG y use el valor solo cuando quality sea Good. Una lectura ausente o de mala calidad no equivale a cero."),
                HelpExample("client-read")),
            S(Tx("Exemplo: gravar Client Memory", "Example: write Client Memory", "Ejemplo: escribir Client Memory"), Tx(
                "Configure a referência Client Memory `UI.LastPressure`. Ela guarda estado local do cliente Runtime, não histórico de processo e não sincroniza automaticamente entre clientes.",
                "Configure the Client Memory reference `UI.LastPressure`. It stores local state for this Runtime client; it is not process history and is not automatically synchronized between clients.",
                "Configure la referencia Client Memory `UI.LastPressure`. Guarda estado local de este cliente Runtime; no es histórico de proceso ni se sincroniza automáticamente entre clientes."),
                HelpExample("client-store")),
            S(Tx("Exemplo: receber interação de ponteiro", "Example: receive a pointer interaction", "Ejemplo: recibir una interacción de puntero"), Tx(
                "Declare `on_pointer_enter` como handler objectInteraction e associe Pointer enters object. O payload do evento contém `eventKey` e `visualObjectId`. Use essa informação para reação visual rápida; processamento demorado em Pointer moves pode deixar a interface lenta.",
                "Declare `on_pointer_enter` as an objectInteraction handler and associate Pointer enters object. The event payload contains `eventKey` and `visualObjectId`. Use it for a quick visual response; lengthy work on Pointer moves can make the interface sluggish.",
                "Declare `on_pointer_enter` como handler objectInteraction y asocie Pointer enters object. El payload contiene `eventKey` y `visualObjectId`. Úselo para una reacción visual rápida; trabajo prolongado en Pointer moves puede ralentizar la interfaz."),
                HelpExample("client-pointer")),
            S(Tx("Erros comuns", "Common errors", "Errores comunes"), Tx(
                "ImportError: confira nome do módulo/função e se a API é do Client Visual. Erro de TAG não declarada: adicione a dependência estável ao Script e valide novamente. Falha ao escrever: confira se TAG aceita escrita e se a sessão possui capacidade. Handler ausente: confira o nome exato no entry point e no Events. Não use loops longos nem espera ativa; cada execução tem limite de tempo e falhas são isoladas do restante do Runtime.",
                "ImportError: check the module/function name and that the API belongs to Client Visual. Undeclared TAG error: add the stable dependency to the Script and validate again. Write failure: check TAG writability and session capability. Missing handler: verify the exact name in the entry point and Events. Avoid long loops and busy waits; each execution is time-bounded and failures are isolated from the rest of Runtime.",
                "ImportError: compruebe el módulo/función y que la API sea de Client Visual. Error de TAG no declarado: agregue la dependencia estable al Script y valide de nuevo. Fallo de escritura: compruebe escritura TAG y capacidad de sesión. Handler ausente: verifique el nombre exacto en entry point y Events. No use bucles largos ni espera activa; cada ejecución tiene límite de tiempo y los fallos se aíslan del resto de Runtime."))
            ),

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
            "security.policy-scopes", "security.users-roles-capabilities", "security.engineering-lock", "audit.query-diagnostics", "audit.overview"),

        TaskGuide(
            "security.users-roles-capabilities", "security",
            Tx("Administrar usuários e papéis de segurança", "Manage security users and roles", "Administrar usuarios y roles de seguridad"),
            Tx(
                "A Security Authority mantém usuários locais e uma política de roles. A role agrupa grants; cada grant combina uma capability (a ação permitida) e, quando necessário, um escopo. O backend decide e aplica a autorização; o nome da role, sozinho, não concede acesso.",
                "Security Authority manages local users and a role policy. A role groups grants; each grant combines a capability (the allowed action) and, when needed, a scope. The backend decides and enforces authorization; a role name alone grants no access.",
                "Security Authority administra usuarios locales y una política de roles. Una role agrupa grants; cada grant combina una capability (la acción permitida) y, cuando sea necesario, un scope. El backend decide y aplica la autorización; el nombre de la role por sí solo no concede acceso."),
            Tx(
                "Use esta tela para criar contas locais, atribuir roles e definir o que cada role permite. Use o guia Scopes e Authority para montar a hierarquia e analisar por que uma ação concreta foi permitida ou recusada. Licensing e a classe da sessão podem impor limites adicionais; grants não os removem.",
                "Use this page to create local accounts, assign roles, and define what each role permits. Use Scopes and Authority to build the hierarchy and investigate why a specific action was allowed or denied. Licensing and the session class may add restrictions; grants do not remove them.",
                "Use esta página para crear cuentas locales, asignar roles y definir lo que permite cada role. Use Scopes y Authority para crear la jerarquía e investigar por qué se permitió o rechazó una acción concreta. Licensing y la clase de sesión pueden añadir límites; los grants no los eliminan."),
            Tx(
                "Entre com uma conta autorizada a administrar usuários e roles. Decida o login, o nome visível e as roles iniciais de cada pessoa. Para grants por planta/área/equipamento/TAG/tela/command, confirme que os nós necessários já existem; esta tela os consulta, mas não cria a hierarquia.",
                "Sign in with an account authorized to manage users and roles. Decide each person's login, display name, and initial roles. For grants scoped to a plant/area/equipment/TAG/screen/command, make sure the needed nodes already exist; this page reads them but does not create the hierarchy.",
                "Inicie sesión con una cuenta autorizada para administrar usuarios y roles. Decida el login, nombre visible y roles iniciales de cada persona. Para grants por planta/área/equipo/TAG/pantalla/command, confirme que ya existan los nodos necesarios; esta página los consulta, pero no crea la jerarquía."),
            Tx(
                "1. Abra Engineering > Security. 2. Para criar uma conta, escolha Criar e informe login, nome para exibição, senha inicial, estado habilitado e roles; o login identifica a conta e o nome para exibição é apenas seu rótulo. 3. Para uma conta existente, pesquise ou filtre por estado e selecione-a. Altere nome, habilitação ou roles, escolha Revisar alterações e confira o impacto antes de confirmar. Desabilitar bloqueia novos logins locais; alterações de conta avançam sua versão de segurança e invalidam tokens locais anteriores. Se editar a própria conta, a sessão deste navegador pode ser invalidada imediatamente. 4. Para trocar a senha, use Redefinir senha na seção separada e confirme: as sessões locais anteriores dessa conta serão invalidadas. 5. Na seção de política, selecione uma role existente ou crie/duplique uma. Defina sua chave estável, nome visível e descrição. A chave pode ser ajustada até o primeiro Apply; depois, mantenha-a estável, pois atribuições persistidas dependem dela. Uma role atribuída a usuários não pode ser excluída. 6. Adicione grants escolhendo a capability e seu escopo. Sem escopo, aquela capability vale globalmente; escolha um nó para limitar o alcance e marque Incluir descendentes somente se subnós também devem ser cobertos. CommandExecute permite executar commands e é diferente de ProcessValueWrite, que permite escrita operacional de valores. 7. Confira Prévia configurada do usuário como resumo da união de grants de suas roles; ela não é a decisão de autorização para um recurso concreto. Capabilities efetivas da sessão atual mostra o resultado do backend para a conta que está conectada, não para o usuário selecionado. 8. Escolha Validar no backend e leia os erros. Se a política estiver válida, use Aplicar política; ela é persistida diretamente na Authority, fora do Save/Publish/Activate do Engineering Workspace.",
                "1. Open Engineering > Security. 2. To create an account, select Create and enter the login, display name, initial password, enabled state, and roles; the login identifies the account, while the display name is only its label. 3. For an existing account, search or filter by status and select it. Change its name, enabled state, or roles, select Review changes, and inspect the impact before confirming. Disabling blocks new local logins; account changes advance its security version and invalidate previously issued local tokens. If you edit your own account, this browser session may be invalidated immediately. 4. To change a password, use Reset password in its separate section and confirm: all previous local sessions for that account will be invalidated. 5. In the policy section, select an existing role or create/duplicate one. Set its stable key, display name, and description. The key can be adjusted until the first Apply; afterward, keep it stable because persisted assignments depend on it. A role assigned to users cannot be deleted. 6. Add grants by choosing a capability and its scope. With no scope, that capability applies globally; choose a node to limit its reach and check Include descendants only if child nodes should also be covered. CommandExecute allows commands to be executed and differs from ProcessValueWrite, which allows operational value writes. 7. Review Configured user preview as a summary of the union of grants from the user's roles; it is not the authorization decision for a concrete resource. Current session effective capabilities shows the backend result for the account that is signed in, not the selected user. 8. Select Validate on backend and read any errors. If the policy is valid, choose Apply policy; it is persisted directly in Authority, outside the Engineering Workspace Save/Publish/Activate lifecycle.",
                "1. Abra Engineering > Security. 2. Para crear una cuenta, elija Crear e indique login, nombre visible, contraseña inicial, estado habilitado y roles; el login identifica la cuenta y el nombre visible es solo su etiqueta. 3. Para una cuenta existente, búsquela o filtre por estado y selecciónela. Cambie el nombre, la habilitación o las roles, elija Revisar cambios y compruebe el impacto antes de confirmar. Deshabilitar bloquea nuevos inicios de sesión locales; los cambios de cuenta avanzan su versión de seguridad e invalidan tokens locales anteriores. Si modifica su propia cuenta, esta sesión del navegador puede invalidarse inmediatamente. 4. Para cambiar la contraseña, use Restablecer contraseña en su sección separada y confirme: se invalidarán las sesiones locales anteriores de esa cuenta. 5. En la sección de política, seleccione una role existente o cree/duplique una. Defina su clave estable, nombre visible y descripción. La clave se puede ajustar hasta el primer Apply; después, manténgala estable porque las asignaciones persistidas dependen de ella. No se puede eliminar una role asignada a usuarios. 6. Añada grants eligiendo capability y scope. Sin scope, esa capability se aplica globalmente; elija un nodo para limitar su alcance y marque Incluir descendientes solo si también deben cubrirse los nodos secundarios. CommandExecute permite ejecutar commands y es distinto de ProcessValueWrite, que permite escrituras operacionales de valores. 7. Revise Vista previa configurada del usuario como resumen de la unión de grants de sus roles; no es la decisión de autorización para un recurso concreto. Capabilities efectivas de la sesión actual muestra el resultado del backend para la cuenta conectada, no para el usuario seleccionado. 8. Elija Validar en backend y lea los errores. Si la política es válida, use Aplicar política; se guarda directamente en Authority, fuera del ciclo Save/Publish/Activate de Engineering Workspace."),
            Tx(
                "Usuários locais podem iniciar sessão conforme suas roles e a política vigente. A política aplicada fica persistida na Authority e é avaliada pelo backend; não é uma alteração do projeto Engineering nem aguarda Publish/Activate.",
                "Local users can sign in according to their roles and the current policy. The applied policy is persisted in Authority and evaluated by the backend; it is not an Engineering project change and does not wait for Publish/Activate.",
                "Los usuarios locales pueden iniciar sesión según sus roles y la política vigente. La política aplicada queda persistida en Authority y el backend la evalúa; no es un cambio del proyecto Engineering ni espera Publish/Activate."),
            Tx(
                "Conta não carrega; usuário sem role; role sem capability necessária; escopo não cobre o recurso; nó de escopo ausente; chave de role já aplicada tratada como editável; role atribuída que não pode ser excluída; erro de validação ou versão da Authority alterada por outra sessão.",
                "Account does not load; user has no role; role lacks the required capability; scope does not cover the resource; scope node is missing; an already-applied role key is treated as editable; an assigned role cannot be deleted; validation error or Authority version changed in another session.",
                "La cuenta no carga; el usuario no tiene role; la role carece de la capability necesaria; el scope no cubre el recurso; falta el nodo de scope; se intenta editar una clave de role ya aplicada; no se puede eliminar una role asignada; error de validación o la versión de Authority cambió en otra sesión."),
            Tx(
                "Confirme a identidade conectada e se ela tem autorização administrativa. Selecione a conta correta e confira estado e roles atribuídas. Compare a prévia configurada com o recurso e a ação exatos; use Scopes e Authority para verificar a hierarquia. Não confunda CommandExecute com ProcessValueWrite. Para conferir o usuário conectado, consulte Capabilities efetivas da sessão atual; ela não representa a pessoa selecionada. Leia o erro do Preview; se a versão de Authority mudou, recarregue a política e refaça a alteração antes de validar e aplicar. Se a própria sessão for encerrada após a confirmação, entre novamente com uma conta autorizada.",
                "Confirm the signed-in identity and its administrative access. Select the correct account and review its state and assigned roles. Compare the configured preview with the exact resource and action; use Scopes and Authority to inspect the hierarchy. Do not confuse CommandExecute with ProcessValueWrite. To check the signed-in user, consult Current session effective capabilities; it does not describe the selected person. Read the Preview error; if the Authority version changed, reload the policy and repeat the change before validating and applying. If your own session ends after confirmation, sign in again with an authorized account.",
                "Confirme la identidad conectada y su autorización administrativa. Seleccione la cuenta correcta y revise su estado y roles asignadas. Compare la vista configurada con el recurso y la acción exactos; use Scopes y Authority para inspeccionar la jerarquía. No confunda CommandExecute con ProcessValueWrite. Para comprobar el usuario conectado, consulte Capabilities efectivas de la sesión actual; no describe a la persona seleccionada. Lea el error de Preview; si cambió la versión de Authority, vuelva a cargar la política y repita el cambio antes de validar y aplicar. Si su sesión termina después de confirmar, inicie sesión de nuevo con una cuenta autorizada."),
            "security.local-accounts", "security.policy-scopes", "security.scopes-authority", "security.engineering-lock", "runtime.session-classes", "licensing.overview", "audit.query-diagnostics", "audit.overview"),

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
            "security.lock-workflow", "security.scopes-authority", "engineering.shell-navigation"),

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
            "packages.escadapkg", "recovery.backup-system-recovery", "engineering.lifecycle"),

        TaskGuide(
            "intelligence.process-stage-member-insights", "intelligence",
            Tx("Análises individuais dos membros da etapa", "Process-stage member insights", "Análisis individuales de miembros de etapa"),
            Tx("Acesso guiado a análises existentes para um equipamento associado à etapa do processo por vez.", "Guided access to existing analyses for one Equipment assignment in a process stage at a time.", "Acceso guiado a análisis existentes para una asignación de equipo de una etapa del proceso a la vez."),
            Tx("Use quando precisar examinar separadamente o desempenho ou o desvio contextual de um equipamento associado a uma etapa.", "Use when you need to examine one assigned Equipment's performance or contextual deviation separately.", "Úselo cuando necesite examinar por separado el rendimiento o la desviación contextual de un equipo asignado."),
            Tx("A etapa e os equipamentos associados precisam ter referências configuradas para as análises desejadas. Para criar a tela, a instalação precisa da licença de Inteligência e sua conta precisa de EngineeringModify e permissão explícita AssetProcessIntelligenceManage, global ou para o recurso. O papel developer inicial já inclui essa permissão global.", "The stage and its Equipment assignments need configured references for the analyses you want to use. Creating the screen requires the Intelligence license and EngineeringModify plus an explicit AssetProcessIntelligenceManage grant, global or scoped to the resource. The initial developer role already includes this global grant.", "La etapa y sus asignaciones de equipo necesitan referencias configuradas para los análisis deseados. Para crear la pantalla se requiere la licencia de Inteligencia y EngineeringModify más un permiso explícito AssetProcessIntelligenceManage, global o para el recurso. El rol developer inicial ya incluye este permiso global."),
            Tx("No Engineering, abra Inteligência de Ativos e Processos, selecione a etapa e use Criar análises dos membros da etapa. Revise o rascunho na tela editável e salve pelo fluxo normal de Preview/Apply; publique e ative a revisão conforme o processo do projeto. No Runtime, escolha um equipamento associado, abra Desempenho ou Desvio contextual, defina o período e solicite a análise.", "In Engineering, open Asset & Process Intelligence, select the stage, and choose Create stage member insights. Review the draft in the editable screen and save it through the normal Preview/Apply flow; publish and activate the revision according to the project lifecycle. In Runtime, select an assigned Equipment, open Performance or Contextual deviation, set the time window, and request the analysis.", "En Engineering, abra Inteligencia de Activos y Procesos, seleccione la etapa y elija Crear análisis de los miembros de etapa. Revise el borrador en la pantalla editable y guárdelo mediante el flujo normal de Preview/Apply; publique y active la revisión según el ciclo del proyecto. En Runtime, seleccione un equipo asignado, abra Rendimiento o Desviación contextual, defina el período y solicite el análisis."),
            Tx("As duas análises só aparecem após solicitação explícita; cada solicitação é independente, e cada resultado é temporário, somente leitura e ligado ao equipamento, período e revisão ativos. Trocar de equipamento apaga o resultado anterior. Não se calcula score do processo nem se juntam timestamps entre equipamentos.", "The two analyses appear only after an explicit request; each request is independent, and its result is temporary, read-only, and tied to the Equipment, time window, and active revision. Changing Equipment clears the previous result. No process-wide score or timestamp join across Equipment is calculated.", "Los dos análisis aparecen solo después de una solicitud explícita; cada solicitud es independiente y su resultado es temporal, de solo lectura y está vinculado al equipo, período y revisión activos. Al cambiar de equipo se borra el resultado anterior. No se calcula una puntuación global del proceso ni se combinan marcas de tiempo entre equipos."),
            Tx("Etapa sem membros configurados, referência incompleta, licença/permissão ausente, janela inválida, dados insuficientes, versão de regra indisponível ou resposta incompatível com a tela/revisão atual.", "A stage with no configured members, incomplete references, missing license/permission, an invalid window, insufficient data, an unavailable rule version, or a response that does not match the current screen/revision.", "Etapa sin miembros configurados, referencias incompletas, licencia/permiso ausente, período no válido, datos insuficientes, versión de regla no disponible o respuesta incompatible con la pantalla/revisión actual."),
            Tx("Confira equipamento, período, revisão e referências; corrija a janela e consulte estados de qualidade/disponibilidade. Não trate valor ausente ou indisponível como zero ou normal. Se a regra não estiver disponível ou a resposta estiver desatualizada, atualize a configuração e faça uma nova solicitação antes de interpretar. O resultado não é diagnóstico nem comprovação de eficiência.", "Check the Equipment, time window, revision, and references; correct the window and review quality/availability states. Do not treat missing or unavailable values as zero or normal. If the rule is unavailable or the response is stale, refresh the configuration and make a new request before interpreting it. The result is not a diagnosis or proof of efficiency.", "Compruebe el equipo, período, revisión y referencias; corrija el período y revise los estados de calidad/disponibilidad. No interprete valores ausentes o no disponibles como cero o normales. Si la regla no está disponible o la respuesta está desactualizada, actualice la configuración y vuelva a solicitar el análisis antes de interpretarlo. El resultado no es un diagnóstico ni una prueba de eficiencia."),
            "engineering.lifecycle", "historian.overview", "licensing.overview"),

        TaskGuide(
            "historian.overview", "historian",
            Tx("Configurar a captura no Historian", "Configure Historian capture", "Configurar la captura en Historian"),
            Tx(
                "Historian guarda amostras dos TAGs cuja captura está habilitada. A estratégia define quando registrar; deadband e períodos só se aplicam às estratégias que os exigem. A tela Historian resume os TAGs configurados, mas a captura é configurada no próprio TAG.",
                "Historian stores samples for TAGs whose capture is enabled. The strategy defines when to record; deadband and periods apply only to strategies that require them. The Historian page summarizes configured TAGs, but capture is configured on the TAG itself.",
                "Historian guarda muestras de los TAGs cuya captura está habilitada. La estrategia define cuándo registrar; deadband y períodos solo se aplican a las estrategias que los requieren. La página Historian resume los TAGs configurados, pero la captura se configura en el propio TAG."),
            Tx(
                "Use quando precisar comparar valores ao longo do tempo ou alimentar Trend e Report com dados históricos. Não habilite captura para todo TAG sem necessidade: estratégia e frequência afetam volume armazenado.",
                "Use it when you need to compare values over time or supply historical data to a Trend or Report. Do not enable capture for every TAG without a need: strategy and frequency affect stored volume.",
                "Úselo cuando necesite comparar valores a lo largo del tiempo o proporcionar datos históricos a Trend o Report. No habilite la captura en todos los TAGs sin necesidad: la estrategia y la frecuencia afectan el volumen almacenado."),
            Tx(
                "Tenha o TAG correto criado, com fonte, tipo, unidade e timestamps compreendidos. Decida com a equipe do processo quais amostras são necessárias e a frequência aceitável; deadband exige TAG numérico.",
                "Have the correct TAG created, with its source, type, unit, and timestamps understood. Decide with the process team which samples are needed and what frequency is acceptable; deadband requires a numeric TAG.",
                "Tenga creado el TAG correcto y comprenda su origen, tipo, unidad y timestamps. Decida con el equipo de proceso qué muestras se necesitan y qué frecuencia es aceptable; deadband requiere un TAG numérico."),
            Tx(
                "1. Em Engineering > TAGs, crie ou edite o TAG. 2. Abra Histórico e habilite a captura. 3. Em Estratégia, use um valor aceito pelo produto: periodic grava por intervalo e requer Period (ms); onChange grava em mudanças; onChangeDeadband requer Deadband numérico; onChangeDeadbandMaxInterval requer Deadband e período máximo. 4. Preencha somente os campos exigidos pela estratégia escolhida; não use deadband em TAG não numérico. 5. Execute Preview, leia as validações e escolha Apply ao Workspace. 6. Salve, publique e ative a revisão em etapas separadas. 7. Em Engineering > Historian, confirme se o TAG aparece com estratégia, deadband e período configurados.",
                "1. In Engineering > TAGs, create or edit the TAG. 2. Open Historian and enable capture. 3. In Strategy, use a value accepted by the product: periodic records by interval and requires Period (ms); onChange records changes; onChangeDeadband requires a numeric Deadband; onChangeDeadbandMaxInterval requires Deadband and a maximum period. 4. Fill only the fields required by the chosen strategy; do not use deadband on a non-numeric TAG. 5. Run Preview, read the validation results, and choose Apply to Workspace. 6. Save, publish, and activate the revision as separate steps. 7. In Engineering > Historian, confirm that the TAG appears with the configured strategy, deadband, and period.",
                "1. En Engineering > TAGs, cree o edite el TAG. 2. Abra Histórico y habilite la captura. 3. En Estrategia, use un valor aceptado por el producto: periodic registra por intervalo y requiere Period (ms); onChange registra cambios; onChangeDeadband requiere Deadband numérico; onChangeDeadbandMaxInterval requiere Deadband y período máximo. 4. Complete solo los campos requeridos por la estrategia elegida; no use deadband en TAG no numérico. 5. Ejecute Preview, lea las validaciones y elija Aplicar al Workspace. 6. Guarde, publique y active la revisión en pasos separados. 7. En Engineering > Historian, confirme que el TAG aparezca con la estrategia, deadband y período configurados."),
            Tx(
                "A revisão ativa passa a solicitar amostras conforme a estratégia do TAG. O Historian lista a configuração; ele não cria dados retroativos. Uma consulta histórica só retorna amostras que foram coletadas e estão disponíveis no período consultado.",
                "The Active revision begins requesting samples according to the TAG strategy. Historian lists the configuration; it does not create backfilled data. A historical query returns only samples that were collected and are available for the queried period.",
                "La revisión Active empieza a solicitar muestras según la estrategia del TAG. Historian muestra la configuración; no crea datos retroactivos. Una consulta histórica devuelve solo muestras que se recopilaron y están disponibles en el período consultado."),
            Tx(
                "TAG ausente da lista: confirme que Histórico está habilitado e a revisão foi aplicada. Preview acusa erro: confirme o valor de estratégia e seus campos obrigatórios. Sem amostras: verifique revisão Active, execução da fonte, quality, timestamps e se a captura foi habilitada antes do período esperado.",
                "TAG missing from the list: confirm Historian is enabled and the revision was applied. Preview reports an error: check the strategy value and its required fields. No samples: check the Active revision, source operation, quality, timestamps, and whether capture was enabled before the expected period.",
                "TAG ausente de la lista: confirme que Histórico esté habilitado y que la revisión se haya aplicado. Preview muestra un error: compruebe el valor de estrategia y sus campos obligatorios. Sin muestras: revise la revisión Active, el funcionamiento del origen, quality, timestamps y si la captura se habilitó antes del período esperado."),
            Tx(
                "Abra Diagnostics para conferir a saúde do armazenamento e o resultado do Runtime. Para um TAG sem amostras, confira primeiro se a revisão ativa inclui a captura e se o TAG recebe valores com timestamps válidos; não espere preenchimento retroativo.",
                "Open Diagnostics to check storage health and Runtime results. For a TAG with no samples, first confirm that the Active revision includes capture and that the TAG receives values with valid timestamps; do not expect backfill.",
                "Abra Diagnostics para comprobar la salud del almacenamiento y los resultados de Runtime. Si un TAG no tiene muestras, confirme primero que la revisión Active incluya la captura y que el TAG reciba valores con timestamps válidos; no espere datos retroactivos."),
            "tags.overview", "tags.quality", "tags.timestamps", "trends.overview", "diagnostics.overview", "engineering.lifecycle"),

        TaskGuide(
            "trends.overview", "trends",
            Tx("Configurar um Trend em uma tela", "Configure a Trend on a screen", "Configurar un Trend en una pantalla"),
            Tx(
                "Trend é um objeto visual que desenha uma ou mais séries TAG ao longo do tempo. O modo Live usa amostras atuais; History consulta amostras do Historian na janela configurada. O gráfico apresenta dados, mas não altera a captura nem o valor do TAG.",
                "A Trend is a visual object that plots one or more TAG series over time. Live mode uses current samples; History queries Historian samples for the configured window. The chart presents data but does not change capture settings or TAG values.",
                "Trend es un objeto visual que dibuja una o más series TAG a lo largo del tiempo. El modo Live usa muestras actuales; History consulta muestras de Historian en la ventana configurada. El gráfico presenta datos, pero no cambia la captura ni el valor del TAG."),
            Tx(
                "Use em uma Screen para acompanhar variação atual ou comparar TAGs numéricos com histórico disponível. Escolha Live para valores recentes; escolha History quando a captura dos TAGs já estiver habilitada.",
                "Use it on a Screen to follow current changes or compare numeric TAGs with available history. Choose Live for current values; choose History when TAG capture is already enabled.",
                "Úselo en una Screen para seguir cambios actuales o comparar TAGs numéricos con historial disponible. Elija Live para valores actuales; elija History cuando la captura de los TAGs ya esté habilitada."),
            Tx(
                "Tenha uma Screen editável e os TAGs que deseja comparar. Para History, configure antes a captura no Historian e tenha uma sessão autorizada a consultar tendências. Para comparar escalas, conheça as unidades e faixas esperadas.",
                "Have an editable Screen and the TAGs you want to compare. For History, configure Historian capture first and use a session authorized to query trends. To compare scales, know the expected units and ranges.",
                "Tenga una Screen editable y los TAGs que desea comparar. Para History, configure primero la captura en Historian y use una sesión autorizada para consultar tendencias. Para comparar escalas, conozca las unidades y rangos esperados."),
            Tx(
                "1. Abra a Screen no editor e insira o objeto Trend. 2. Selecione-o e, no Inspector, configure o modo Live ou History, a janela temporal e a atualização. Ajuste legenda, eixos, grade e indicação de quality conforme necessário. 3. Em Penas e escalas do Trend, escolha Adicionar pena; selecione um TAG e configure rótulo, unidade, cor, espessura, estilo, eixo esquerdo/direito e escala automática ou fixa. Cada eixo tem sua própria escala; até 16 penas podem ser adicionadas. 4. Em History, confirme que cada TAG tem captura habilitada e dados no período. 5. Execute Preview, revise a apresentação e escolha Apply ao Workspace. Salve, publique e ative a revisão para disponibilizar a alteração no Runtime.",
                "1. Open the Screen in the editor and insert a Trend object. 2. Select it and use the Inspector to choose Live or History mode, the time window, and refresh interval. Adjust the legend, axes, grid, and quality indicator as needed. 3. In Trend pens and scales, select Add pen; choose a TAG and set its label, unit, color, width, style, left/right axis, and automatic or fixed scale. Each axis has its own scale; up to 16 pens can be added. 4. In History mode, confirm that each TAG has capture enabled and data in the period. 5. Run Preview, review the presentation, and choose Apply to Workspace. Save, publish, and activate the revision to make the change available in Runtime.",
                "1. Abra la Screen en el editor e inserte un objeto Trend. 2. Selecciónelo y, en el Inspector, configure el modo Live o History, la ventana temporal y la actualización. Ajuste leyenda, ejes, cuadrícula e indicación de quality según sea necesario. 3. En Plumas y escalas del Trend, elija Agregar pluma; seleccione un TAG y configure etiqueta, unidad, color, espesor, estilo, eje izquierdo/derecho y escala automática o fija. Cada eje tiene su propia escala; se pueden agregar hasta 16 plumas. 4. En modo History, confirme que cada TAG tenga captura habilitada y datos en el período. 5. Ejecute Preview, revise la presentación y elija Aplicar al Workspace. Guarde, publique y active la revisión para que el cambio esté disponible en Runtime."),
            Tx(
                "A Screen ativa mostra uma série por pena visível. A legenda permite ocultar ou exibir penas temporariamente no Runtime; isso não modifica a definição salva. O modo History depende dos registros efetivamente coletados, não apenas da existência do TAG.",
                "The Active Screen shows one series for each visible pen. The legend can temporarily hide or show pens in Runtime; this does not modify the saved definition. History mode depends on samples actually collected, not merely on the TAG existing.",
                "La Screen Active muestra una serie por cada pluma visible. La leyenda permite ocultar o mostrar plumas temporalmente en Runtime; esto no modifica la definición guardada. El modo History depende de muestras realmente recopiladas, no solo de que exista el TAG."),
            Tx(
                "Trend vazio: confira se há penas e se os TAGs têm valor atual ou amostras históricas, conforme o modo. Erro de History: verifique captura, janela, TrendUse e acesso ao Historian. Escala ilegível: separe TAGs com unidades/faixas incompatíveis entre eixos ou defina limites fixos válidos. Edição de penas indisponível: salve o objeto Trend e confirme TAGs no projeto.",
                "Empty Trend: check that pens exist and that TAGs have current values or historical samples for the selected mode. History error: check capture, window, TrendUse, and Historian access. Unreadable scale: separate TAGs with incompatible units/ranges across axes or set valid fixed limits. Pen editing unavailable: save the Trend object and confirm that project TAGs exist.",
                "Trend vacío: compruebe que haya plumas y que los TAGs tengan valores actuales o muestras históricas según el modo. Error de History: revise captura, ventana, TrendUse y acceso a Historian. Escala ilegible: separe TAGs con unidades/rangos incompatibles entre ejes o defina límites fijos válidos. Edición de plumas no disponible: guarde el objeto Trend y confirme que existan TAGs en el proyecto."),
            Tx(
                "Confirme o modo selecionado e se a revisão ativa contém o objeto e as penas esperadas. Em History, valide captura, período, quality e timestamps; em Live, valide valor atual e conexão. Se a visualização usar eixos diferentes, compare as escalas antes de interpretar a relação entre séries.",
                "Confirm the selected mode and that the Active revision contains the expected object and pens. In History, check capture, period, quality, and timestamps; in Live, check the current value and connection. If the chart uses separate axes, compare their scales before interpreting relationships between series.",
                "Confirme el modo seleccionado y que la revisión Active contenga el objeto y las plumas esperadas. En History, compruebe captura, período, quality y timestamps; en Live, compruebe el valor actual y la conexión. Si el gráfico usa ejes separados, compare sus escalas antes de interpretar la relación entre series."),
            "screens.overview", "tags.overview", "historian.overview", "security.users-roles-capabilities", "visual.properties", "engineering.lifecycle"),

        TaskGuide(
            "reports.overview", "reports",
            Tx("Criar e executar um relatório", "Create and run a report", "Crear y ejecutar un informe"),
            Tx(
                "Um Report organiza uma consulta de dados e uma apresentação para que usuários autorizados possam gerar uma saída legível. O Designer configura definição e layout em Engineering; Runtime executa o relatório sem editar a definição.",
                "A Report combines a data query and a presentation so authorized users can generate a readable output. The Designer configures its definition and layout in Engineering; Runtime runs the report without editing its definition.",
                "Un Report combina una consulta de datos y una presentación para que usuarios autorizados generen una salida legible. El Designer configura su definición y diseño en Engineering; Runtime ejecuta el informe sin editar la definición."),
            Tx(
                "Use para resumir leituras de TAGs em um período, com variáveis, agrupamentos e formato de página. Para visualizar séries interativas dentro de uma Screen, use o objeto Trend; Report é uma saída gerada e exportável.",
                "Use it to summarize TAG readings over a period with variables, grouping, and page layout. For interactive series inside a Screen, use a Trend object; a Report is a generated, exportable output.",
                "Úselo para resumir lecturas de TAGs en un período, con variables, agrupaciones y formato de página. Para visualizar series interactivas dentro de una Screen, use el objeto Trend; Report es una salida generada y exportable."),
            Tx(
                "Tenha uma consulta de dados permitida ou saiba qual conjunto de dados usará; confirme os TAGs, campos e período necessários. Para incluir leituras históricas, habilite a captura no Historian e confirme a autorização do usuário que desenhará e executará o relatório.",
                "Have an allowed data query or know which dataset you will use; confirm the required TAGs, fields, and period. To include historical readings, enable Historian capture and confirm the permissions of the user who will design and run the report.",
                "Tenga una consulta de datos permitida o sepa qué conjunto de datos utilizará; confirme los TAGs, campos y período necesarios. Para incluir lecturas históricas, habilite la captura en Historian y confirme los permisos del usuario que diseñará y ejecutará el informe."),
            Tx(
                "1. Em Engineering > Reports, crie um relatório ou selecione um existente. Informe nome e identificador e escolha orientação da página. 2. Selecione uma Data Query salva ou configure a consulta incorporada, escolha o conjunto de dados e defina período padrão e resolução. Não é necessário escrever SQL: use apenas campos e variáveis oferecidos pelo produto. 3. Inclua variáveis a partir de TAGs do projeto; ajuste rótulo, unidade, casas decimais e formato. Configure agrupamentos/resumos se necessários. 4. Organize cabeçalho, detalhe e rodapé com os controles de texto e campo; confira dimensões em milímetros e a largura útil. 5. Use Preview para conferir dados e layout; use Validate para verificar a definição e corrigir problemas. Apply só incorpora a definição validada ao Workspace; depois salve, publique e ative a revisão. 6. No Runtime, abra Reports, selecione um relatório disponível, informe período relativo ou De/Até se permitido, preencha parâmetros e escolha Generate report. Revise a saída e exporte PDF, Excel ou CSV se necessário.",
                "1. In Engineering > Reports, create a report or select an existing one. Enter its name and identifier and choose page orientation. 2. Select a saved Data Query or configure the embedded query, choose the dataset, and set the default period and resolution. You do not need to write SQL: use only fields and variables offered by the product. 3. Add variables from project TAGs; adjust label, unit, decimal places, and format. Configure grouping and summaries if needed. 4. Arrange the header, detail, and footer with text and field controls; check dimensions in millimeters and usable width. 5. Use Preview to inspect data and layout; use Validate to check the definition and correct problems. Apply only adds the validated definition to the Workspace; then save, publish, and activate the revision. 6. In Runtime, open Reports, select an available report, set a relative or From/To period if allowed, enter parameters, and select Generate report. Review the output and export PDF, Excel, or CSV if needed.",
                "1. En Engineering > Reports, cree un informe o seleccione uno existente. Introduzca nombre e identificador y elija la orientación de página. 2. Seleccione una Data Query guardada o configure la consulta incorporada, elija el conjunto de datos y defina período predeterminado y resolución. No es necesario escribir SQL: use solo los campos y variables que ofrece el producto. 3. Añada variables de TAGs del proyecto; ajuste etiqueta, unidad, decimales y formato. Configure agrupaciones y resúmenes si es necesario. 4. Organice encabezado, detalle y pie con controles de texto y campo; revise dimensiones en milímetros y ancho útil. 5. Use Preview para revisar datos y diseño; use Validate para comprobar la definición y corregir problemas. Apply solo incorpora la definición validada al Workspace; después guarde, publique y active la revisión. 6. En Runtime, abra Reports, seleccione un informe disponible, indique un período relativo o Desde/Hasta si se permite, complete parámetros y elija Generate report. Revise la salida y exporte PDF, Excel o CSV si lo necesita."),
            Tx(
                "O relatório fica disponível para execução quando a definição validada foi incorporada, salva, publicada e ativada, e quando a sessão tem autorização. Cada geração mostra o intervalo e os parâmetros usados; exporta a saída sem alterar o projeto.",
                "The report becomes available to run after its validated definition is applied, saved, published, and activated, and the session is authorized. Each generation shows the period and parameters used; exporting the output does not change the project.",
                "El informe queda disponible para ejecutarse cuando su definición validada se incorpora, guarda, publica y activa, y la sesión está autorizada. Cada generación muestra el período y los parámetros utilizados; exportar la salida no cambia el proyecto."),
            Tx(
                "Relatório não aparece no Runtime: confirme ativação da revisão e autorização. Preview sem linhas: verifique TAGs, dataset, período e captura; ausência de dados não significa defeito na apresentação. Validação falha: confirme campos da consulta e seções/controles referenciam campos existentes. Período inválido: De deve ser anterior a Até; alguns relatórios podem desabilitar um dos modos de período.",
                "Report missing in Runtime: confirm the revision is active and the session is authorized. Preview has no rows: check TAGs, dataset, period, and capture; no data does not mean the presentation is broken. Validation fails: check query fields and whether sections/controls reference existing fields. Invalid period: From must be earlier than To; some reports may disable one period mode.",
                "Informe ausente en Runtime: confirme que la revisión esté activa y que la sesión esté autorizada. Preview sin filas: revise TAGs, conjunto de datos, período y captura; no tener datos no significa que la presentación esté rota. Validation falla: revise los campos de consulta y que secciones/controles hagan referencia a campos existentes. Período no válido: Desde debe ser anterior a Hasta; algunos informes pueden deshabilitar un modo de período."),
            Tx(
                "Use Validate no Designer para localizar referências inválidas e Preview para verificar a consulta. No Runtime, confirme que o usuário pode consultar o dataset e que o período e parâmetros atendem à definição. Consulte Historian e Diagnostics para separar falta de dados de erro de layout.",
                "Use Validate in the Designer to locate invalid references and Preview to check the query. In Runtime, confirm that the user may query the dataset and that the period and parameters satisfy the definition. Check Historian and Diagnostics to distinguish missing data from a layout error.",
                "Use Validate en el Designer para localizar referencias no válidas y Preview para comprobar la consulta. En Runtime, confirme que el usuario pueda consultar el conjunto de datos y que el período y los parámetros cumplan la definición. Consulte Historian y Diagnostics para distinguir falta de datos de un error de diseño."),
            "historian.overview", "tags.overview", "engineering.lifecycle", "security.users-roles-capabilities", "diagnostics.overview"),

        TaskGuide(
            "licensing.overview", "licensing",
            Tx("Consultar e ativar o licenciamento", "Review and activate licensing", "Consultar y activar el licenciamiento"),
            Tx(
                "A tela Licenciamento mostra a licença instalada e o entitlement efetivo do Runtime. São leituras relacionadas, mas diferentes: confira ambos, incluindo faixa, limite de TAGs, clientes e recursos licenciados. Licença limita recursos comerciais; não concede roles ou capabilities de segurança.",
                "The Licensing page shows the installed license and the Runtime's effective entitlement. They are related but distinct views: check both, including tier, TAG limit, clients, and licensed features. A license limits commercial features; it does not grant security roles or capabilities.",
                "La página Licenciamiento muestra la licencia instalada y el entitlement efectivo de Runtime. Son vistas relacionadas, pero distintas: revise ambas, incluidos nivel, límite de TAGs, clientes y funciones licenciadas. La licencia limita funciones comerciales; no concede roles ni capabilities de seguridad."),
            Tx(
                "Use para conferir validade e limites, preparar a ativação desta máquina ou entender por que um recurso licenciado está indisponível. A tela não emite licenças nem substitui a política da Security Authority.",
                "Use it to check validity and limits, prepare activation for this machine, or understand why a licensed feature is unavailable. This page does not issue licenses or replace Security Authority policy.",
                "Úsela para comprobar validez y límites, preparar la activación de esta máquina o entender por qué no está disponible una función licenciada. Esta página no emite licencias ni sustituye la política de Security Authority."),
            Tx(
                "Tenha acesso à tela Licenciamento. Para uma ativação, solicite a licença assinada usando o código de solicitação exibido e a autoridade de licenciamento indicada; não é necessário nem exibido um identificador bruto de hardware.",
                "Have access to the Licensing page. To activate, request a signed license using the displayed machine request code and the designated licensing authority; no raw hardware identifier is required or displayed.",
                "Tenga acceso a la página Licenciamiento. Para activar, solicite una licencia firmada usando el código de solicitud mostrado y la autoridad de licenciamiento indicada; no se requiere ni se muestra un identificador bruto de hardware."),
            Tx(
                "1. Abra Licenciamento e escolha Atualizar para carregar o estado atual. Compare os cartões Licença e Runtime: leia Estado, Faixa, Limite de TAGs, validade, clientes e recursos, além do diagnóstico. 2. Para ativar esta máquina, copie Solicitação da máquina e envie-a somente à autoridade de licenciamento designada. 3. Cole o código de licença assinado recebido em Instalar licença e escolha Validar e instalar. O produto valida o código; não edite o conteúdo. 4. Atualize a tela e confirme o novo estado e entitlement do Runtime. 5. Use Remover licença somente quando desejar remover deliberadamente a licença desta máquina; a próxima ativação usará Demo.",
                "1. Open Licensing and select Refresh to load the current state. Compare the License and Runtime cards: review State, Tier, TAG limit, expiry, clients, and features, as well as diagnostics. 2. To activate this machine, copy Machine request and send it only to the designated licensing authority. 3. Paste the signed license code you receive into Install license and select Validate and install. The product validates the code; do not edit its contents. 4. Refresh the page and confirm the new Runtime state and entitlement. 5. Use Remove license only when you deliberately want to remove the license from this machine; the next activation will use Demo.",
                "1. Abra Licenciamiento y elija Actualizar para cargar el estado actual. Compare las tarjetas Licencia y Runtime: revise Estado, Nivel, límite de TAGs, validez, clientes y funciones, además del diagnóstico. 2. Para activar esta máquina, copie Solicitud de la máquina y envíela solo a la autoridad de licenciamiento designada. 3. Pegue el código de licencia firmado que reciba en Instalar licencia y elija Validar e instalar. El producto valida el código; no edite su contenido. 4. Actualice la página y confirme el nuevo estado y entitlement de Runtime. 5. Use Eliminar licencia solo cuando quiera quitarla deliberadamente de esta máquina; la próxima activación utilizará Demo."),
            Tx(
                "O painel apresenta o estado e os limites que o Runtime está usando; diagnóstico e validade ajudam a explicar restrições. A resposta não muda roles, scopes nem autorização por ação.",
                "The page shows the state and limits Runtime is using; diagnostics and expiry help explain restrictions. The result does not change roles, scopes, or action authorization.",
                "La página muestra el estado y los límites que usa Runtime; el diagnóstico y la validez ayudan a explicar restricciones. El resultado no cambia roles, scopes ni autorización de acciones."),
            Tx(
                "Estado não carregado: use Atualizar e leia a mensagem. Licença válida, mas recurso bloqueado: compare o entitlement do Runtime, a validade, limites, classe de sessão e a capability concedida pela Authority. Código recusado: confira se recebeu o código assinado para esta solicitação/máquina e encaminhe o diagnóstico à autoridade designada; não publique códigos de licença em chamados públicos. Se a licença foi removida por engano, solicite nova ativação em vez de tentar alterar arquivos locais.",
                "State not loaded: select Refresh and read the message. License is valid but a feature is blocked: compare Runtime entitlement, expiry, limits, session class, and the capability granted by Authority. Code rejected: confirm that the signed code matches this machine request and send the diagnostic to the designated authority; do not post license codes in public tickets. If the license was removed by mistake, request activation again instead of editing local files.",
                "Estado no cargado: elija Actualizar y lea el mensaje. La licencia es válida, pero una función está bloqueada: compare entitlement de Runtime, validez, límites, clase de sesión y capability concedida por Authority. Código rechazado: confirme que el código firmado corresponda a esta solicitud/máquina y envíe el diagnóstico a la autoridad designada; no publique códigos de licencia en tickets públicos. Si eliminó la licencia por error, solicite una nueva activación en lugar de modificar archivos locales."),
            Tx(
                "Atualize o estado e compare as duas áreas: licença mostra a licença instalada; Runtime mostra o entitlement que está efetivamente usando. Se houver divergência ou erro de validação, registre o estado e o diagnóstico sem expor o código da licença e contate a autoridade designada.",
                "Refresh the status and compare both areas: License shows the installed license; Runtime shows the entitlement it is actually using. If they differ or validation fails, record the state and diagnostic without exposing the license code and contact the designated authority.",
                "Actualice el estado y compare ambas áreas: Licencia muestra la licencia instalada; Runtime muestra el entitlement que está usando efectivamente. Si hay diferencias o falla la validación, registre el estado y el diagnóstico sin exponer el código de licencia y contacte a la autoridad designada."),
            "security.users-roles-capabilities", "runtime.session-classes", "licensing.generator", "trends.overview", "intelligence.process-stage-member-insights")
    };

    private static IReadOnlyCollection<TopicDefinition> BuildValidatedSurfaceTaskTopics() => new[]
    {
        TaskGuide(
            "runtime.history", "runtime",
            Tx("Consultar Histórico e Playback no Runtime", "Query Runtime history and Playback", "Consultar Histórico y Playback en Runtime"),
            Tx("Consulte valores de TAG, alarmes e eventos registrados em um período. O Histórico é somente leitura; Playback é uma revisão histórica separada do Runtime ao vivo.", "Query recorded TAG values, alarms, and events over a time range. History is read-only; Playback is a separate historical view from live Runtime.", "Consulte valores de TAG, alarmas y eventos registrados en un período. El Histórico es de solo lectura; Playback es una vista histórica separada del Runtime en vivo."),
            Tx("Use Histórico para localizar registros de valores, alarmes ou eventos. Use Playback quando precisar rever a apresentação do Runtime em um instante histórico, sem confundir esse contexto com a operação ao vivo.", "Use History to find recorded values, alarms, or events. Use Playback when you need to review the Runtime presentation at a historical instant, without confusing that context with live operation.", "Use Histórico para localizar valores, alarmas o eventos registrados. Use Playback cuando necesite revisar la presentación del Runtime en un instante histórico, sin confundir ese contexto con la operación en vivo."),
            Tx("A conta precisa estar autorizada para consultar o conjunto histórico escolhido. Para ver valores, a captura Historian deve ter sido habilitada antes do período; alarmes e eventos também precisam ter sido registrados.", "The account must be authorized to query the selected historical dataset. TAG capture must have been enabled before the period to see values; alarms and events must also have been recorded.", "La cuenta debe estar autorizada para consultar el conjunto histórico seleccionado. La captura Historian debe haberse habilitado antes del período para ver valores; las alarmas y eventos también deben haberse registrado."),
            Tx("1. No Runtime, abra Histórico. 2. Escolha Valores de TAGs, Alarmes ou Eventos. 3. Selecione Período relativo ou absoluto; use um atalho ou informe a quantidade/unidade. Para o período absoluto, preencha De e Até no fuso mostrado e confirme que De seja anterior a Até. A janela não pode exceder 31 dias; em uma mudança de horário, escolha um horário local não ambíguo. 4. Escolha Consultar. Depois da primeira consulta, use somente os campos de busca, ordenação e filtro que o conjunto disponibilizar. 5. Para Playback, quando disponível na tela operacional, escolha a janela e o instante e selecione Entrar no Playback. O Runtime permanece ao vivo até essa confirmação. Use Voltar ao Live para retornar à operação atual.", "1. In Runtime, open History. 2. Choose TAG values, Alarms, or Events. 3. Select Relative or Absolute period; use a quick range or enter the amount/unit. For an absolute period, enter From and To in the displayed time zone and make sure From is earlier. The range cannot exceed 31 days; during a daylight-saving transition, choose an unambiguous local time. 4. Select Query. After the first query, use only the search, sort, and filter fields exposed by that dataset. 5. For Playback, when available from the operational screen, choose the time range and instant, then select Enter Playback. Runtime remains live until you confirm. Select Return to Live to resume the current view.", "1. En Runtime, abra Histórico. 2. Elija Valores de TAG, Alarmas o Eventos. 3. Seleccione período Relativo o Absoluto; use un acceso rápido o indique cantidad/unidad. Para un período absoluto, introduzca Desde y Hasta en la zona horaria mostrada y confirme que Desde sea anterior. El intervalo no puede superar 31 días; durante un cambio de horario estacional, elija una hora local no ambigua. 4. Elija Consultar. Después de la primera consulta, use solo los campos de búsqueda, orden y filtro que ofrezca ese conjunto. 5. Para Playback, cuando esté disponible desde la pantalla operacional, elija el intervalo y el instante y seleccione Entrar en Playback. Runtime sigue en vivo hasta que lo confirme. Elija Volver a Live para regresar a la vista actual."),
            Tx("A consulta apresenta somente registros disponíveis no conjunto e período selecionados; não cria amostras retroativas. Playback identifica explicitamente a revisão histórica e pode ser encerrado para voltar ao Live. Nenhum dos dois é uma autorização para comandar o processo a partir de dados antigos.", "The query shows only records available for the selected dataset and period; it does not create backfilled samples. Playback explicitly identifies the historical view and can be exited to return to Live. Neither mode authorizes process commands from old data.", "La consulta muestra solo los registros disponibles para el conjunto y período seleccionados; no crea muestras retroactivas. Playback identifica explícitamente la vista histórica y puede cerrarse para volver a Live. Ninguno de los dos modos autoriza comandos de proceso basados en datos antiguos."),
            Tx("Nenhum resultado: confirme conjunto, período, autorização e se havia captura/registro no intervalo. Período rejeitado: confirme ordem De/Até, duração máxima e horários ambíguos por mudança de fuso. Filtros indisponíveis: consulte primeiro; alguns conjuntos não oferecem busca, ordenação ou todos os filtros. Sem autorização: peça o acesso apropriado; a tela histórica não amplia permissões.", "No results: check the dataset, period, authorization, and whether capture/recording existed in that interval. Rejected period: check From/To order, maximum duration, and times made ambiguous by a time-zone change. Unavailable filters: run a query first; some datasets do not expose search, sorting, or every filter. Unauthorized: request the appropriate access; the history screen does not expand permissions.", "Sin resultados: compruebe el conjunto, período, autorización y si había captura/registro en ese intervalo. Período rechazado: compruebe el orden Desde/Hasta, la duración máxima y las horas ambiguas por cambio de zona. Filtros no disponibles: consulte primero; algunos conjuntos no ofrecen búsqueda, orden ni todos los filtros. Sin autorización: solicite el acceso adecuado; la pantalla histórica no amplía permisos."),
            "historian.overview", "trends.overview", "operational-events.overview", "runtime.overview"),

        TaskGuide(
            "ha.overview", "high-availability",
            Tx("Configurar e acompanhar Alta disponibilidade", "Configure and monitor High Availability", "Configurar y supervisar Alta disponibilidad"),
            Tx("A área Alta disponibilidade apresenta Standalone ou HA, prontidão, configuração salva e em execução, estado dos nós e operações controladas do cluster.", "The High Availability area presents Standalone or HA mode, readiness, saved and running configuration, node state, and controlled cluster operations.", "El área Alta disponibilidad presenta el modo Standalone o HA, la preparación, la configuración guardada y en ejecución, el estado de los nodos y las operaciones controladas del clúster."),
            Tx("Use Standalone para um único servidor. Prepare HA quando a instalação tiver dois servidores e a licença, conectividade, autenticação e proteção exigidas pela implantação. A tela ajuda a configurar e acompanhar; não substitui um plano de manutenção nem ensaio controlado de transferência.", "Use Standalone for one server. Prepare HA when the installation has two servers and the deployment's license, connectivity, authentication, and protection requirements. The page helps configure and monitor it; it does not replace a maintenance plan or a controlled transfer exercise.", "Use Standalone para un solo servidor. Prepare HA cuando la instalación tenga dos servidores y cumpla los requisitos de licencia, conectividad, autenticación y protección de la implementación. La página ayuda a configurarlo y supervisarlo; no sustituye un plan de mantenimiento ni una prueba controlada de transferencia."),
            Tx("Tenha uma licença com HA Runtime, os endereços corretos dos dois servidores, transporte autenticado entre eles e a política de fencing/referência definida para a topologia. Use uma conta com HighAvailabilityObserve para acompanhar e HighAvailabilityTransfer para salvar ou operar. Não exponha o segredo compartilhado.", "Have a license with HA Runtime, correct addresses for both servers, authenticated transport between them, and a fencing/reference policy defined for the topology. Use an account with HighAvailabilityObserve to monitor and HighAvailabilityTransfer to save or operate. Do not expose the shared secret.", "Disponga de una licencia con HA Runtime, las direcciones correctas de ambos servidores, transporte autenticado entre ellos y una política de fencing/referencia definida para la topología. Use una cuenta con HighAvailabilityObserve para supervisar y HighAvailabilityTransfer para guardar u operar. No exponga el secreto compartido."),
            Tx("1. Abra Engineering > Alta disponibilidade e confira o Modo em execução. 2. Para preparar uma nova topologia, escolha HA; Standalone continua sendo a configuração padrão. 3. Informe os endereços dos servidores e mantenha o endpoint automático, salvo se sua rede exigir um endereço externo diferente. Configure a autenticação do peer. 4. Revise Preferência do servidor e, quando a topologia exigir, habilite Proteção HA e informe a referência compartilhada de fencing. Failover automático só pode ser habilitado com a proteção correspondente. 5. Confira todos os itens de Prontidão para HA e corrija cada requisito pendente. 6. Selecione Salvar configuração desejada. Se a tela indicar restart necessário, o valor salvo ainda não está em execução; programe a aplicação conforme o procedimento operacional da instalação. 7. Depois, atualize a tela e confira nós, peer, Active efetivo, saúde, freshness e referência. Quando uma transição planejada for necessária, use Switchover somente no Active efetivo; use Failback ou Recovery apenas para a condição indicada na tela, leia a confirmação e confira o resultado/histórico da operação.", "1. Open Engineering > High Availability and check Running mode. 2. To prepare a new topology, choose HA; Standalone remains the default. 3. Enter both server addresses and keep the automatic endpoint unless your network requires a different external address. Configure peer authentication. 4. Review Preferred server and, when the topology requires it, enable HA protection and enter the shared fencing reference. Automatic failover can be enabled only with the corresponding protection. 5. Check every item under HA readiness and resolve each outstanding requirement. 6. Select Save desired configuration. If the page indicates that a restart is required, the saved value is not running yet; schedule its application according to the installation's operating procedure. 7. Then refresh and check nodes, peer, effective Active, health, freshness, and reference. For a planned transition, use Switchover only on the effective Active node; use Failback or Recovery only for the condition indicated on the page, read the confirmation, and check the operation result/history.", "1. Abra Engineering > Alta disponibilidad y compruebe el modo En ejecución. 2. Para preparar una topología nueva, elija HA; Standalone sigue siendo el valor predeterminado. 3. Introduzca las direcciones de ambos servidores y mantenga el endpoint automático, salvo que la red exija otra dirección externa. Configure la autenticación del peer. 4. Revise Servidor preferido y, cuando la topología lo requiera, habilite Protección HA e indique la referencia compartida de fencing. El failover automático solo puede habilitarse con la protección correspondiente. 5. Compruebe todos los elementos de Preparación para HA y resuelva cada requisito pendiente. 6. Elija Guardar configuración deseada. Si la página indica que se necesita reiniciar, el valor guardado todavía no está en ejecución; programe su aplicación según el procedimiento operativo de la instalación. 7. Después, actualice y compruebe nodos, peer, Active efectivo, salud, freshness y referencia. Para una transición planificada, use Switchover solo en el nodo Active efectivo; use Failback o Recovery únicamente para la condición indicada en la página, lea la confirmación y compruebe el resultado/historial de la operación."),
            Tx("A tela distingue configuração desejada da configuração em execução e mostra o estado observado do cluster. Operações são enviadas ao backend e podem ser aceitas ou rejeitadas conforme elegibilidade e proteção; a exibição de um botão não garante que a transição ocorrerá.", "The page distinguishes desired from running configuration and shows observed cluster state. Operations are sent to the backend and may be accepted or rejected according to eligibility and protection; a visible button does not guarantee that a transition will occur.", "La página distingue la configuración deseada de la que está en ejecución y muestra el estado observado del clúster. Las operaciones se envían al backend y pueden aceptarse o rechazarse según la elegibilidad y la protección; que aparezca un botón no garantiza que la transición ocurra."),
            Tx("HA não editável: confira licença e modo atual. Prontidão incompleta: leia o requisito pendente, confira os dois endpoints e autenticação e valide a referência quando a proteção estiver habilitada. Estado stale, degradado ou ambíguo: não force uma promoção; verifique conectividade, freshness e fencing e siga o procedimento de recuperação. 403: solicite a capability HighAvailabilityObserve ou HighAvailabilityTransfer adequada. Restart necessário: configuração salva ainda difere da execução.", "HA is not editable: check the license and current mode. Readiness is incomplete: read the pending requirement, check both endpoints and authentication, and validate the reference when protection is enabled. Stale, degraded, or ambiguous state: do not force promotion; check connectivity, freshness, and fencing and follow the recovery procedure. 403: request the appropriate HighAvailabilityObserve or HighAvailabilityTransfer capability. Restart required: saved configuration still differs from what is running.", "HA no es editable: compruebe la licencia y el modo actual. Preparación incompleta: lea el requisito pendiente, compruebe ambos endpoints y la autenticación, y valide la referencia cuando la protección esté habilitada. Estado stale, degradado o ambiguo: no fuerce una promoción; compruebe conectividad, freshness y fencing y siga el procedimiento de recuperación. 403: solicite la capability HighAvailabilityObserve o HighAvailabilityTransfer adecuada. Reinicio requerido: la configuración guardada aún difiere de la que está en ejecución."),
            "licensing.overview", "security.users-roles-capabilities", "diagnostics.overview", "troubleshooting.overview"),

        TaskGuide(
            "installation.switching", "installation",
            Tx("Trocar a aplicação desta instalação", "Switch the Application on this installation", "Cambiar la aplicación de esta instalación"),
            Tx("O fluxo seguro desvincula a Application e a Authority atuais e devolve a instalação a um estado neutral para um novo bootstrap. Não é excluir projeto, desinstalar o produto nem remover automaticamente a licença.", "The secure workflow detaches the current Application and Authority and returns the installation to a neutral state for a new bootstrap. It is not project deletion, product uninstall, or automatic license removal.", "El flujo seguro desvincula la Application y Authority actuales y devuelve la instalación a un estado neutral para un nuevo bootstrap. No es eliminar el proyecto, desinstalar el producto ni quitar automáticamente la licencia."),
            Tx("Use ao preparar esta instalação para outra aplicação ou para recomeçar o vínculo de forma deliberada. Faça backups antes de desvincular; a operação invalida sessões antigas e a decisão de licença deve ser explícita.", "Use it when preparing this installation for another Application or deliberately restarting its binding. Back up before detaching; the operation invalidates old sessions and the license choice must be explicit.", "Úselo al preparar esta instalación para otra Application o reiniciar deliberadamente su vinculación. Haga copias antes de desvincular; la operación invalida sesiones antiguas y la decisión sobre la licencia debe ser explícita."),
            Tx("Entre com permissão para administrar a instalação. Confirme a Application/Authority atuais, o estado Working e o que precisa preservar. Se também for necessário restaurar o Historian, trate o banco/armazenamento pelo procedimento próprio; o detach preserva esses dados, mas não substitui backup de banco.", "Sign in with permission to administer the installation. Confirm the current Application/Authority, Working state, and what must be preserved. If Historian also needs recovery, handle its database/storage using its own procedure; detach preserves this data but does not replace a database backup.", "Inicie sesión con permiso para administrar la instalación. Confirme la Application/Authority actuales, el estado Working y lo que debe conservarse. Si también necesita restaurar Historian, gestione la base/almacenamiento mediante su propio procedimiento; detach conserva esos datos, pero no sustituye una copia de la base."),
            Tx("1. Abra Engineering > Instalação e revise o resumo do estado atual, inclusive Working não publicado. 2. Exporte a Application como .escadapkg e a Authority separadamente se precisar preservar ambas; o pacote da Application não contém credenciais. A exportação de Authority exige a senha solicitada pela tela. 3. Abra Desvincular aplicação desta instalação e leia as consequências. 4. Confirme os acknowledgements pedidos sobre Application, Authority, sessões, Historian e alterações Working. 5. Escolha manter, remover ou substituir a licença. Uma licença substituta é validada antes da desvinculação. 6. Confira os bloqueios apresentados e selecione Desvincular e voltar ao neutro. Não feche a página durante o processamento. 7. Quando o bootstrap neutral abrir, associe a próxima Application e configure sua Authority pelo fluxo normal.", "1. Open Engineering > Installation and review the current-state summary, including unpublished Working changes. 2. Export the Application as .escadapkg and export Authority separately if both must be preserved; the Application package does not contain credentials. Authority export requires the password requested by the page. 3. Open Detach application from this installation and read the consequences. 4. Confirm the requested acknowledgements about Application, Authority, sessions, Historian, and Working changes. 5. Choose to keep, remove, or replace the license. A replacement license is validated before detachment. 6. Review any blockers and select Detach and return to neutral. Do not close the page while it is processing. 7. When neutral bootstrap opens, bind the next Application and configure its Authority through the normal workflow.", "1. Abra Engineering > Instalación y revise el resumen del estado actual, incluidos los cambios Working no publicados. 2. Exporte la Application como .escadapkg y exporte Authority por separado si necesita conservar ambas; el paquete de Application no incluye credenciales. La exportación de Authority requiere la contraseña que pide la página. 3. Abra Desvincular aplicación de esta instalación y lea las consecuencias. 4. Confirme los acknowledgements solicitados sobre Application, Authority, sesiones, Historian y cambios Working. 5. Elija mantener, quitar o reemplazar la licencia. La licencia sustituta se valida antes de desvincular. 6. Revise los bloqueos indicados y seleccione Desvincular y volver a neutral. No cierre la página durante el procesamiento. 7. Cuando se abra el bootstrap neutral, vincule la siguiente Application y configure su Authority mediante el flujo normal."),
            Tx("A instalação volta ao estado neutral; a Application e a Authority anteriores deixam de estar vinculadas e sessões anteriores deixam de autorizar ações. Historian é preservado. A licença permanece conforme à escolha confirmada. O novo vínculo ainda precisa ser concluído pelo bootstrap.", "The installation returns to neutral; the previous Application and Authority are no longer bound, and previous sessions no longer authorize actions. Historian is preserved. The license follows the confirmed choice. The new binding still needs to be completed through bootstrap.", "La instalación vuelve al estado neutral; la Application y Authority anteriores dejan de estar vinculadas y las sesiones anteriores dejan de autorizar acciones. Historian se conserva. La licencia sigue la opción confirmada. La nueva vinculación aún debe completarse mediante el bootstrap."),
            Tx("A ação está bloqueada: leia os itens e corrija o estado antes de repetir. Working não publicado: revise ou reconheça explicitamente sua perda. Backup de Authority não exporta: confira a senha e guarde o arquivo em local seguro. Licença substituta rejeitada: corrija/obtenha um código válido antes do detach. Estado após erro incerto: atualize e confira a situação real antes de tentar novamente.", "The action is blocked: read the listed items and correct the state before retrying. Unpublished Working: review it or explicitly acknowledge its loss. Authority backup does not export: check the password and store the file safely. Replacement license rejected: correct or obtain a valid code before detaching. State after an error is uncertain: refresh and inspect the actual state before trying again.", "La acción está bloqueada: lea los elementos indicados y corrija el estado antes de repetir. Working no publicado: revíselo o reconozca explícitamente su pérdida. No se exporta Authority: compruebe la contraseña y guarde el archivo en un lugar seguro. Licencia sustituta rechazada: corrija u obtenga un código válido antes del detach. Estado incierto tras un error: actualice y compruebe la situación real antes de volver a intentarlo."),
            "application.export-import", "recovery.backup-system-recovery", "getting-started.neutral-bootstrap", "licensing.overview"),

        TaskGuide(
            "intelligence.overview", "intelligence",
            Tx("Começar com Inteligência de Ativos e Processos", "Get started with Asset and Process Intelligence", "Empezar con Inteligencia de Activos y Procesos"),
            Tx("Configure perfis de medição por equipamento e associe-os a etapas do processo para consultar análises históricas somente leitura e criar rascunhos de telas editáveis.", "Configure measurement profiles per Equipment and associate them with process stages to request read-only historical analyses and create editable Screen drafts.", "Configure perfiles de medición por equipo y asócielos a etapas del proceso para solicitar análisis históricos de solo lectura y crear borradores de Pantallas editables."),
            Tx("Use Inteligência somente nas áreas/equipamentos que deseja acompanhar. A licença habilita os recursos, mas análises são solicitadas em escopos configurados; a tela não monitora nem altera automaticamente todo o processo.", "Use Intelligence only for the areas/Equipment you want to examine. The license enables the features, but analyses are requested for configured scopes; the page does not automatically monitor or change the whole process.", "Use Inteligencia solo en las áreas/equipos que desea examinar. La licencia habilita las funciones, pero los análisis se solicitan en scopes configurados; la página no supervisa ni cambia automáticamente todo el proceso."),
            Tx("Tenha licença de Inteligência, permissão EngineeringModify e AssetProcessIntelligenceManage no recurso a configurar, TAGs existentes com unidade/quality/timestamp compreendidos e Historian capturando dados no período que será analisado. Para contexto operacional, identifique os TAGs que definem cada condição.", "Have an Intelligence license, EngineeringModify and AssetProcessIntelligenceManage permission for the resource, existing TAGs with understood units/quality/timestamps, and Historian capture for the analysis period. For operating context, identify the TAGs that define each condition.", "Disponga de licencia de Inteligencia, permisos EngineeringModify y AssetProcessIntelligenceManage sobre el recurso, TAGs existentes con unidad/quality/timestamp comprendidos y captura Historian durante el período de análisis. Para el contexto operacional, identifique los TAGs que definen cada condición."),
            Tx("1. Em Engineering > Inteligência de ativos, crie um Perfil de medição e adicione os TAGs relevantes ao equipamento, marcando sinais obrigatórios ou opcionais e completando os qualificadores semânticos exigidos. 2. Associe o perfil à identidade de Equipamento existente; um equipamento pode ter vários perfis. 3. Salve a configuração no Working. Selecione um equipamento associado para consultar cobertura, desempenho ou desvio contextual quando a análise estiver disponível. Defina o período e solicite explicitamente a consulta; contexto operacional precisa corresponder às condições e evidências temporais requeridas. 4. Para analisar etapas do processo, crie etapas em ordem e adicione as associações de equipamentos/perfis. Configure regras de desempenho e intervalos de contexto quando a análise guiada exigir. 5. Para criar uma tela, escolha um perfil ou uma etapa e gere o rascunho; ele abre no editor normal. Revise-o, execute Preview e use Apply ao Workspace. Salvar, publicar e ativar continuam sendo etapas explícitas do lifecycle.", "1. In Engineering > Asset Intelligence, create a Measurement profile and add the TAGs relevant to the Equipment, marking signals as required or optional and completing any required semantic qualifiers. 2. Assign the profile to an existing Equipment identity; one Equipment can have multiple profiles. 3. Save the configuration to Working. Select an assigned Equipment to request coverage, performance, or contextual-deviation analysis when available. Set the period and explicitly request the query; operating context must match the required conditions and temporal evidence. 4. To analyze process stages, create ordered stages and add Equipment/profile assignments. Configure performance rules and context ranges when the guided analysis requires them. 5. To create a Screen, choose a profile or stage and generate a draft; it opens in the normal editor. Review it, run Preview, and use Apply to Workspace. Save, publish, and activate remain explicit lifecycle steps.", "1. En Engineering > Inteligencia de activos, cree un Perfil de medición y añada los TAGs relevantes para el equipo, marcando señales obligatorias u opcionales y completando los calificadores semánticos requeridos. 2. Asigne el perfil a una identidad de Equipo existente; un equipo puede tener varios perfiles. 3. Guarde la configuración en Working. Seleccione un equipo asignado para solicitar análisis de cobertura, rendimiento o desviación contextual cuando estén disponibles. Defina el período y solicite explícitamente la consulta; el contexto operacional debe coincidir con las condiciones y evidencias temporales requeridas. 4. Para analizar etapas de proceso, cree etapas ordenadas y añada las asignaciones de equipos/perfiles. Configure reglas de rendimiento e intervalos de contexto cuando el análisis guiado lo requiera. 5. Para crear una Pantalla, elija un perfil o etapa y genere un borrador; se abre en el editor normal. Revíselo, ejecute Preview y use Aplicar al Workspace. Guardar, publicar y activar siguen siendo pasos explícitos del lifecycle."),
            Tx("As configurações ficam associadas aos equipamentos e etapas escolhidos. Uma análise explicitamente solicitada retorna evidências históricas somente leitura para o escopo e período disponíveis. Um rascunho gerado é uma Tela comum, editável, e não é salvo/ativado automaticamente. A geração usa metadados e não inventa valores ou KPIs.", "Configurations stay associated with the selected Equipment and stages. An explicitly requested analysis returns read-only historical evidence for the available scope and period. A generated draft is a normal editable Screen and is not saved or activated automatically. Generation uses metadata and does not invent values or KPIs.", "Las configuraciones quedan asociadas a los equipos y etapas elegidos. Un análisis solicitado explícitamente devuelve evidencia histórica de solo lectura para el scope y período disponibles. El borrador generado es una Pantalla editable normal y no se guarda ni activa automáticamente. La generación usa metadatos y no inventa valores ni KPIs."),
            Tx("Recurso bloqueado: confira licença, EngineeringModify, AssetProcessIntelligenceManage e escopo. Perfil sem TAG ou com referência duplicada: revise sinais e identidades. Sem dados ou cobertura: confira captura Historian, período, quality, timestamp e disponibilidade; ausência não significa zero nem normalidade. Contexto sem correspondência: revise os predicados e a mesma evidência temporal exigida. Rascunho de tela incompleto: volte ao perfil/etapa e complete as seleções obrigatórias. O resultado não é diagnóstico nem comprovação de eficiência.", "Resource blocked: check the license, EngineeringModify, AssetProcessIntelligenceManage, and scope. Profile without TAGs or with duplicate references: review signals and identities. No data or coverage: check Historian capture, period, quality, timestamp, and availability; missing data does not mean zero or normal. Context mismatch: review predicates and the required shared temporal evidence. Incomplete Screen draft: return to the profile/stage and complete required selections. Results are not a diagnosis or proof of efficiency.", "Recurso bloqueado: compruebe la licencia, EngineeringModify, AssetProcessIntelligenceManage y el scope. Perfil sin TAGs o con referencias duplicadas: revise señales e identidades. Sin datos o cobertura: compruebe captura Historian, período, quality, timestamp y disponibilidad; la ausencia no significa cero ni normalidad. Contexto sin coincidencia: revise los predicados y la evidencia temporal compartida requerida. Borrador de Pantalla incompleto: vuelva al perfil/etapa y complete las selecciones obligatorias. El resultado no es diagnóstico ni prueba de eficiencia."),
            "intelligence.process-stage-member-insights", "historian.overview", "tags.overview", "screens.overview", "licensing.overview", "engineering.lifecycle")
    };

    private static IReadOnlyCollection<TopicDefinition> BuildFoundationTagTaskTopics() => new[]
    {
        TaskGuide(
            "getting-started.startup-authentication", "getting-started",
            Tx("Primeiro acesso: iniciar e entrar com segurança", "First use: start and sign in safely", "Primer acceso: iniciar y entrar de forma segura"),
            Tx("Prepare o acesso ao EliteSCADA e use a identidade correta sem presumir credenciais iniciais.", "Prepare access to EliteSCADA and use the correct identity without assuming initial credentials.", "Prepare el acceso a EliteSCADA y use la identidad correcta sin dar por sentidas credenciales iniciales."),
            Tx("Use estas etapas ao abrir uma instalação já preparada ou ao concluir seu primeiro acesso. Em uma instalação neutral, siga o bootstrap; em uma instalação existente, entre com uma conta local administrada pela Authority.", "Use these steps when opening a prepared installation or completing first use. On a neutral installation, follow bootstrap; on an existing installation, sign in with a local account managed by Authority.", "Use estos pasos al abrir una instalación preparada o completar el primer acceso. En una instalación neutral, siga el bootstrap; en una instalación existente, entre con una cuenta local administrada por Authority."),
            Tx("Tenha a URL/ambiente fornecido pelo administrador e confirme se a instalação está em bootstrap neutral ou já possui Authority e aplicação. Não use senhas padrão nem compartilhe credenciais.", "Have the URL/environment provided by the administrator and confirm whether the installation is in neutral bootstrap or already has an Authority and Application. Do not use default passwords or share credentials.", "Tenga la URL/entorno proporcionado por el administrador y confirme si la instalación está en bootstrap neutral o ya tiene Authority y Application. No use contraseñas predeterminadas ni comparta credenciales."),
            Tx("1. Abra o endereço da instalação correta e aguarde a tela de entrada carregar. 2. Se a instalação solicitar bootstrap, siga o assistente para criar/configurar a Authority e a primeira identidade administrativa; se houver backup para recuperar, use o fluxo Restore-first em vez de criar dados temporários. 3. Em uma instalação já configurada, informe login e senha da conta local autorizada. 4. Depois de entrar, confira o nome/estado da sessão e abra somente as áreas autorizadas. 5. Se a sessão expirar ou uma mudança de segurança invalidar o token, entre novamente pela tela oficial.", "1. Open the correct installation address and wait for the entry page. 2. If the installation requests bootstrap, follow the wizard to create/configure Authority and the first administrative identity; if a backup must be restored, use Restore-first instead of creating temporary data. 3. On an existing installation, enter the login and password of an authorized local account. 4. After sign-in, check the session identity/state and open only authorized areas. 5. If the session expires or a security change invalidates its token, sign in again through the official page.", "1. Abra la dirección de la instalación correcta y espere la página de entrada. 2. Si la instalación solicita bootstrap, siga el asistente para crear/configurar Authority y la primera identidad administrativa; si debe recuperar un backup, use Restore-first en lugar de crear datos temporales. 3. En una instalación existente, introduzca login y contraseña de una cuenta local autorizada. 4. Después de entrar, compruebe la identidad/estado de la sesión y abra solo las áreas autorizadas. 5. Si la sesión vence o un cambio de seguridad invalida el token, vuelva a entrar desde la página oficial."),
            Tx("A tela inicial corresponde ao estado real da instalação e a sessão autenticada recebe somente as permissões concedidas. O nome de uma role ou um botão visível não é prova de autorização para uma ação concreta.", "The entry page reflects the installation's actual state and an authenticated session receives only its granted permissions. A role name or visible button is not proof of authorization for a specific action.", "La página de entrada refleja el estado real de la instalación y la sesión autenticada recibe solo los permisos concedidos. El nombre de una role o un botón visible no demuestra autorización para una acción concreta."),
            Tx("Página não abre: confirme endereço, serviço e rede com o administrador. Bootstrap inesperado: pare e confira se abriu a instalação correta antes de criar a Authority. Login recusado: confirme o login e peça ao administrador para verificar se a conta está habilitada; não tente senhas repetidamente. Acesso negado: solicite a capability e o escopo necessários pela Security Authority.", "Page does not open: check the address, service, and network with the administrator. Unexpected bootstrap: stop and verify that this is the correct installation before creating Authority. Sign-in rejected: check the login and ask the administrator to verify that the account is enabled; do not repeatedly guess passwords. Access denied: request the required capability and scope through Security Authority.", "La página no abre: compruebe dirección, servicio y red con el administrador. Bootstrap inesperado: deténgase y verifique que sea la instalación correcta antes de crear Authority. Entrada rechazada: compruebe el login y pida al administrador verificar que la cuenta esté habilitada; no pruebe contraseñas repetidamente. Acceso denegado: solicite la capability y el scope necesarios mediante Security Authority."),
            "getting-started.neutral-bootstrap", "recovery.backup-system-recovery", "security.users-roles-capabilities", "licensing.overview"),

        TaskGuide(
            "runtime.overview", "runtime",
            Tx("Usar o Runtime como operador", "Use Runtime as an operator", "Usar Runtime como operador"),
            Tx("Runtime apresenta a revisão Active e os valores atuais sob as permissões da sessão conectada.", "Runtime presents the Active revision and current values under the connected session's permissions.", "Runtime presenta la revisión Active y los valores actuales según los permisos de la sesión conectada."),
            Tx("Use o Runtime para navegar pelas telas operacionais, acompanhar TAGs e alarmes e executar apenas interações previstas pela aplicação e permitidas à sua sessão.", "Use Runtime to navigate operational screens, monitor TAGs and alarms, and perform only interactions defined by the application and allowed to your session.", "Use Runtime para navegar por las pantallas operacionales, supervisar TAGs y alarmas y ejecutar solo las interacciones previstas por la aplicación y permitidas a su sesión."),
            Tx("A revisão desejada precisa estar Active. Entre com uma identidade autorizada e confirme se a sessão é Interactive ou View Only. Para dados atuais, a fonte precisa estar conectada e o TAG precisa ter qualidade/disponibilidade apropriadas.", "The desired revision must be Active. Sign in with an authorized identity and check whether the session is Interactive or View Only. For current data, the source must be connected and the TAG must have suitable quality/availability.", "La revisión deseada debe estar Active. Entre con una identidad autorizada y compruebe si la sesión es Interactive o View Only. Para datos actuales, la fuente debe estar conectada y el TAG debe tener quality/disponibilidad adecuadas."),
            Tx("1. Abra Runtime e confirme o projeto/tela inicial apresentados. 2. Navegue pelas telas e popups pelos controles da aplicação. 3. Observe valor, unidade, quality e horário do TAG; trate Stale, Uncertain, Bad, Disabled ou Unavailable como informação importante, não como zero. 4. Para um comando, leia a confirmação/feedback do Runtime e não repita automaticamente um resultado ambíguo. Escrita de valor e execução de comando são ações diferentes. 5. Use Histórico em seu contexto somente leitura; para voltar à operação atual, retorne ao Live. 6. Ao terminar, saia pelo controle de sessão quando disponível.", "1. Open Runtime and check the project/initial Screen shown. 2. Navigate through Screens and Popups using the application's controls. 3. Check each TAG's value, unit, quality, and time; treat Stale, Uncertain, Bad, Disabled, or Unavailable as meaningful information, not zero. 4. For a command, read Runtime's confirmation/feedback and do not automatically repeat an ambiguous result. A value write and command execution are different actions. 5. Use History in its read-only context; return to Live to resume current operation. 6. When finished, sign out through the session control when available.", "1. Abra Runtime y compruebe el proyecto/Pantalla inicial mostrados. 2. Navegue por Pantallas y Popups mediante los controles de la aplicación. 3. Revise valor, unidad, quality y hora del TAG; trate Stale, Uncertain, Bad, Disabled o Unavailable como información importante, no como cero. 4. Para un comando, lea la confirmación/feedback de Runtime y no repita automáticamente un resultado ambiguo. Escribir un valor y ejecutar un comando son acciones diferentes. 5. Use Histórico en su contexto de solo lectura; vuelva a Live para reanudar la operación actual. 6. Al terminar, cierre la sesión mediante el control disponible."),
            Tx("A tela mostra a revisão Active; o backend continua validando acesso a cada ação. View Only permite visualização sem receber autoridade de Interactive. Nenhum controle de tela concede permissão por si só.", "The Screen shows the Active revision; the backend continues to validate access for each action. View Only provides viewing without Interactive authority. No screen control grants permission by itself.", "La Pantalla muestra la revisión Active; el backend sigue validando el acceso para cada acción. View Only permite visualizar sin autoridad Interactive. Ningún control de pantalla concede permisos por sí solo."),
            Tx("Tela ausente ou antiga: confirme projeto, navegação e revisão Active. Valor sem qualidade Good ou sem atualização: abra Diagnostics e siga TAG > fonte > conexão > binding > qualidade/timestamp. Ação desabilitada/recusada: confira classe da sessão, licença e capability/escopo; não procure contornar a decisão do servidor. Playback ativo: volte ao Live antes de operar.", "Missing or old Screen: check the project, navigation, and Active revision. Value without Good quality or without updates: open Diagnostics and check TAG > source > connection > binding > quality/timestamp. Disabled/rejected action: check session class, license, and capability/scope; do not bypass the server decision. Playback active: return to Live before operating.", "Pantalla ausente o antigua: compruebe proyecto, navegación y revisión Active. Valor sin quality Good o sin actualización: abra Diagnostics y revise TAG > fuente > conexión > binding > quality/timestamp. Acción deshabilitada/rechazada: compruebe clase de sesión, licencia y capability/scope; no eluda la decisión del servidor. Playback activo: vuelva a Live antes de operar."),
            "runtime.session-classes", "runtime.history", "tags.quality", "tags.timestamps", "alarms.overview", "diagnostics.overview"),

        TaskGuide(
            "tags.quality", "tags",
            Tx("Entender a quality de um TAG", "Understand a TAG's quality", "Entender la quality de un TAG"),
            Tx("Quality acompanha o valor para indicar a confiança/condição da amostra; não é decoração nem substituto do valor.", "Quality accompanies a value to indicate the sample's trust/condition; it is not decoration or a substitute for the value.", "Quality acompaña al valor para indicar la confianza/condición de la muestra; no es decoración ni sustituye al valor."),
            Tx("Use quality ao diagnosticar leitura, comparar histórico ou decidir se um valor pode ser apresentado/consumido. Preserve o estado vindo da fonte e do Runtime.", "Use quality when diagnosing a read, reviewing history, or deciding whether a value can be displayed/consumed. Preserve the state supplied by the source and Runtime.", "Use quality al diagnosticar una lectura, revisar histórico o decidir si un valor puede presentarse/consumirse. Conserve el estado proporcionado por la fuente y Runtime."),
            Tx("Identifique o TAG, sua Data Source, o binding/endereço e a última amostra. Saiba qual condição de processo ela representa antes de interpretá-la.", "Identify the TAG, its Data Source, binding/address, and latest sample. Know which process condition it represents before interpreting it.", "Identifique el TAG, su Data Source, binding/dirección y última muestra. Sepa qué condición del proceso representa antes de interpretarla."),
            Tx("1. Abra o Monitor/diagnóstico do TAG ou uma apresentação que mostre quality. 2. Leia o estado junto com o valor e o timestamp. Good indica amostra marcada válida; Uncertain indica confiança limitada; Bad e seus detalhes (BadCommunication, BadConfiguration, BadDevice), Stale, Disabled e Unavailable indicam condições que não devem ser tratadas como valor atual válido. 3. Se houver problema, confira fonte, conexão, configuração/binding, dispositivo e atualização; corrija a camada de origem. 4. Depois de corrigir, confirme que uma amostra nova chegou com estado adequado.", "1. Open the TAG monitor/diagnostics or a view that shows quality. 2. Read the state together with the value and timestamp. Good means the sample is marked valid; Uncertain means limited trust; Bad and its details (BadCommunication, BadConfiguration, BadDevice), Stale, Disabled, and Unavailable indicate conditions that must not be treated as a valid current value. 3. If there is a problem, check the source, connection, configuration/binding, device, and updates; correct the source layer. 4. After fixing it, confirm a new sample arrives with a suitable state.", "1. Abra el monitor/diagnóstico del TAG o una vista que muestre quality. 2. Lea el estado junto con el valor y el timestamp. Good indica que la muestra está marcada como válida; Uncertain indica confianza limitada; Bad y sus detalles (BadCommunication, BadConfiguration, BadDevice), Stale, Disabled y Unavailable indican condiciones que no deben tratarse como valor actual válido. 3. Si hay un problema, compruebe fuente, conexión, configuración/binding, dispositivo y actualización; corrija la capa de origen. 4. Después de corregirlo, confirme que llegue una muestra nueva con un estado adecuado."),
            Tx("O operador vê estado de qualidade e horário junto ao valor. Alarmes, tendências, histórico e análises devem conservar essa evidência; uma amostra ruim não se torna boa por formatação ou por manter o último número visível.", "The operator sees quality and time alongside the value. Alarms, trends, history, and analytics should preserve this evidence; a poor sample does not become good through formatting or by keeping the last number visible.", "El operador ve el estado de quality y la hora junto al valor. Alarmas, tendencias, histórico y análisis deben conservar esta evidencia; una muestra deficiente no se vuelve buena por formato ni por mantener visible el último número."),
            Tx("Quality ruim ou stale: investigue primeiro origem, conexão, endereço/binding e dispositivo. Valor ausente: não converta em zero. Uncertain: confirme a condição antes de usar em decisão. Depois da correção, aguarde uma leitura nova e confira timestamp/quality, não apenas o número exibido.", "Poor or stale quality: first investigate source, connection, address/binding, and device. Missing value: do not convert it to zero. Uncertain: confirm the condition before using it in a decision. After fixing the issue, wait for a new read and check timestamp/quality, not just the displayed number.", "Quality deficiente o stale: investigue primero origen, conexión, dirección/binding y dispositivo. Valor ausente: no lo convierta en cero. Uncertain: confirme la condición antes de usarlo en una decisión. Después de corregir, espere una lectura nueva y compruebe timestamp/quality, no solo el número mostrado."),
            "tags.timestamps", "tags.addressing", "sources.data-sources", "diagnostics.overview", "historian.overview"),

        TaskGuide(
            "tags.timestamps", "tags",
            Tx("Interpretar o horário de uma amostra", "Interpret a sample timestamp", "Interpretar la hora de una muestra"),
            Tx("O TAG separa o horário de observação no EliteSCADA do horário de origem quando o protocolo fornece essa evidência.", "A TAG separates the EliteSCADA observation time from source time when the protocol supplies that evidence.", "El TAG separa la hora de observación en EliteSCADA de la hora de origen cuando el protocolo proporciona esa evidencia."),
            Tx("Use timestamps para ordenar amostras, avaliar frescor e comparar sinais no histórico. Não troque o horário da amostra pelo relógio do navegador.", "Use timestamps to order samples, assess freshness, and compare historical signals. Do not replace a sample's time with the browser clock.", "Use timestamps para ordenar muestras, evaluar frescura y comparar señales históricas. No sustituya la hora de la muestra por el reloj del navegador."),
            Tx("Conheça a origem do TAG e se o protocolo fornece horário do dispositivo ou servidor. Confirme fuso e sincronização dos equipamentos antes de comparar relógios distintos.", "Know the TAG's source and whether the protocol supplies a device or server timestamp. Confirm time zone and clock synchronization before comparing different clocks.", "Conozca el origen del TAG y si el protocolo proporciona una marca de tiempo del dispositivo o servidor. Confirme zona horaria y sincronización antes de comparar relojes distintos."),
            Tx("1. Leia Observado como o horário em que EliteSCADA observou/publicou o valor localmente. 2. Quando disponível, leia Source timestamp como horário informado pela origem; a presença desse horário, sozinha, não prova sincronização. 3. Server timestamp é um horário intermediário exposto por alguns protocolos e não deve ser confundido com a hora do dispositivo. 4. Preserve a evidência associada: NotProvided, PresentUnverified, Unsynchronized ou Synchronized quando disponível. 5. Ao analisar múltiplos sinais, use apenas a evidência temporal que a análise exige; igualdade do horário armazenado não prova aquisição simultânea na fonte.", "1. Read Observed as the time EliteSCADA locally observed/published the value. 2. When available, read Source timestamp as time supplied by the source; its presence alone does not prove synchronization. 3. Server timestamp is an intermediate time exposed by some protocols and must not be confused with device time. 4. Preserve associated evidence: NotProvided, PresentUnverified, Unsynchronized, or Synchronized when available. 5. When analyzing multiple signals, use only the temporal evidence required by the analysis; equal stored timestamps do not prove simultaneous source acquisition.", "1. Lea Observado como la hora en que EliteSCADA observó/publicó localmente el valor. 2. Cuando esté disponible, lea Source timestamp como la hora indicada por la fuente; su presencia por sí sola no demuestra sincronización. 3. Server timestamp es una hora intermedia expuesta por algunos protocolos y no debe confundirse con la hora del dispositivo. 4. Conserve la evidencia asociada: NotProvided, PresentUnverified, Unsynchronized o Synchronized cuando esté disponible. 5. Al analizar varias señales, use solo la evidencia temporal requerida; la igualdad de timestamps almacenados no demuestra adquisición simultánea en la fuente."),
            Tx("A tela pode apresentar horário observado e, quando disponível, horário de origem separadamente. Cada um mantém seu significado; histórico usa os horários persistidos conforme o contrato da fonte.", "The page may show observation time and, when available, source time separately. Each keeps its meaning; history uses the persisted timestamps according to the source contract.", "La página puede mostrar por separado la hora observada y, cuando esté disponible, la hora de origen. Cada una conserva su significado; el histórico usa los timestamps persistidos según el contrato de la fuente."),
            Tx("Horários parecem diferentes: confira fuso e diferença entre origem/observação/servidor. Timestamp de origem ausente: o protocolo pode não fornecê-lo. Timestamp presente sem sincronização: não o trate como relógio sincronizado. Para correlação, use o método de alinhamento explícito da análise, não arredondamento manual de horários.", "Times appear different: check time zone and the distinction between source/observation/server time. Missing source timestamp: the protocol may not provide one. Present timestamp without synchronization: do not treat it as a synchronized clock. For correlation, use the analysis's explicit alignment method, not manual timestamp rounding.", "Las horas parecen diferentes: compruebe zona horaria y la diferencia entre origen/observación/servidor. Timestamp de origen ausente: el protocolo puede no proporcionarlo. Timestamp presente sin sincronización: no lo trate como reloj sincronizado. Para correlación, use el método explícito de alineación del análisis, no redondeo manual de horas."),
            "tags.quality", "historian.overview", "runtime.history", "intelligence.overview"),

        TaskGuide(
            "tags.writeability", "tags",
            Tx("Entender quando um TAG pode ser escrito", "Understand when a TAG can be written", "Entender cuándo se puede escribir un TAG"),
            Tx("Escrita de valor depende do TAG, da fonte, da sessão e da autorização efetiva; um campo/botão visível não concede acesso.", "A value write depends on the TAG, source, session, and effective authorization; a visible field/button does not grant access.", "La escritura de valor depende del TAG, la fuente, la sesión y la autorización efectiva; un campo/botón visible no concede acceso."),
            Tx("Use quando estiver projetando uma interação que grava um valor de processo. Para executar uma ação modelada como Command, consulte o fluxo de Commands: CommandExecute é diferente de ProcessValueWrite.", "Use when designing an interaction that writes a process value. For an action modeled as a Command, use the Commands workflow: CommandExecute differs from ProcessValueWrite.", "Úselo al diseñar una interacción que escribe un valor de proceso. Para ejecutar una acción modelada como Command, consulte el flujo de Commands: CommandExecute es distinto de ProcessValueWrite."),
            Tx("Confirme que o TAG e o Driver suportam escrita, que o valor e destino estão corretos, que a revisão Active inclui a configuração e que a sessão Interactive/licença e Authority concedem capability e escopo. Em teste, use ambiente/sinal simulado ou controlado, nunca um processo vivo sem procedimento de segurança.", "Confirm the TAG and Driver support writes, the value and destination are correct, the Active revision includes the configuration, and the Interactive session/license and Authority grant the capability and scope. For testing, use a simulated or controlled environment, never a live process without an approved safety procedure.", "Confirme que el TAG y el Driver admiten escrituras, que el valor y destino son correctos, que la revisión Active incluye la configuración y que la sesión Interactive/licencia y Authority conceden capability y scope. Para probar, use un entorno/señal simulado o controlado, nunca un proceso real sin procedimiento de seguridad aprobado."),
            Tx("1. Confira a propriedade de leitura/escrita do TAG e o suporte declarado pela fonte. 2. Em Security, confira `ProcessValueWrite` no escopo do TAG; se houver restrição de roles do TAG, a role também precisa satisfazê-la. 3. Diferencie isso de CommandExecute: comando segue o contrato do Command e sua própria autorização. 4. Use sessão Interactive; View Only não recebe autoridade de escrita/comando, e licença pode impor limites adicionais. 5. Configure a interação pela superfície suportada, salve e ative a revisão após validação. 6. Verifique o feedback de Runtime; não repita automaticamente resultado Accepted/Unknown como se fosse confirmação física.", "1. Check the TAG's read/write property and the source's declared support. 2. In Security, check `ProcessValueWrite` for the TAG scope; if the TAG has a role restriction, the role must also satisfy it. 3. Distinguish this from CommandExecute: a command follows the Command contract and its own authorization. 4. Use an Interactive session; View Only receives no write/command authority, and the license may add limits. 5. Configure the interaction through a supported surface, then validate, save, and activate the revision. 6. Check Runtime feedback; do not automatically repeat an Accepted/Unknown result as if it confirmed physical completion.", "1. Compruebe la propiedad de lectura/escritura del TAG y el soporte declarado por la fuente. 2. En Security, compruebe `ProcessValueWrite` para el scope del TAG; si el TAG tiene restricción de roles, la role también debe cumplirla. 3. Distinga esto de CommandExecute: un comando sigue el contrato del Command y su propia autorización. 4. Use una sesión Interactive; View Only no recibe autoridad de escritura/comando y la licencia puede imponer límites adicionales. 5. Configure la interacción mediante una superficie admitida; valide, guarde y active la revisión. 6. Compruebe el feedback de Runtime; no repita automáticamente un resultado Accepted/Unknown como si confirmara la finalización física."),
            Tx("A solicitação só é aceita quando todos os contratos e permissões permitem. O servidor revalida a autorização; um controle de interface não contorna a decisão. Command e escrita de processo continuam sendo operações distintas.", "A request is accepted only when all contracts and permissions allow it. The server revalidates authorization; a UI control cannot bypass the decision. A Command and a process-value write remain distinct operations.", "La solicitud solo se acepta cuando todos los contratos y permisos lo permiten. El servidor vuelve a validar la autorización; un control de interfaz no puede eludir la decisión. Command y escritura de proceso siguen siendo operaciones distintas."),
            Tx("Escrita recusada: confira suporte da fonte, revisão Active, classe de sessão, capability, escopo e restrição de role do TAG. Controle escondido/visível: confira a decisão efetiva no servidor. Resposta ambígua: verifique estado/diagnóstico antes de qualquer nova solicitação; não teste escrevendo no processo real sem autorização operacional.", "Write rejected: check source support, Active revision, session class, capability, scope, and TAG role restriction. Hidden/visible control: check the server's effective decision. Ambiguous response: inspect state/diagnostics before another request; do not test by writing to a live process without operational authorization.", "Escritura rechazada: compruebe soporte de la fuente, revisión Active, clase de sesión, capability, scope y restricción de role del TAG. Control oculto/visible: compruebe la decisión efectiva del servidor. Respuesta ambigua: revise estado/diagnóstico antes de otra solicitud; no pruebe escribiendo en un proceso real sin autorización operacional."),
            "security.users-roles-capabilities", "runtime.session-classes", "bindings-commands.overview", "tags.quality"),

        TaskGuide(
            "tags.addressing", "tags",
            Tx("Configurar o endereço e o binding de um TAG", "Configure a TAG address and binding", "Configurar la dirección y el binding de un TAG"),
            Tx("O endereço e os parâmetros de binding dependem da Data Source e do schema do Driver selecionado.", "The address and binding settings depend on the selected Data Source and Driver schema.", "La dirección y los parámetros de binding dependen de la Data Source y el schema del Driver seleccionado."),
            Tx("Use ao criar ou alterar o ponto que liga um TAG do projeto a um sinal externo. Configure somente os campos que a origem selecionada realmente oferece.", "Use when creating or changing the point that connects a project TAG to an external signal. Configure only fields actually offered by the selected source.", "Úselo al crear o cambiar el punto que conecta un TAG del proyecto con una señal externa. Configure solo los campos que realmente ofrece la fuente seleccionada."),
            Tx("Tenha escolhido a Data Source correta, conhecido o sinal no dispositivo/servidor externo e conferido o tipo de valor do TAG. Consulte a documentação/cadastro do Driver específico para endereço e opções.", "Select the correct Data Source, identify the signal on the external device/server, and check the TAG value type. Consult the specific Driver's registered help for address syntax and options.", "Seleccione la Data Source correcta, identifique la señal en el dispositivo/servidor externo y compruebe el tipo de valor del TAG. Consulte la ayuda registrada del Driver específico para sintaxis y opciones."),
            Tx("1. Em Engineering > TAGs, selecione o TAG e a Data Source. 2. Preencha o endereço portátil conforme a origem e o formato indicado pela ajuda do Driver. 3. Se aparecer o assistente de binding, complete os campos gerados pelo schema exibido; ele não inventa campos de protocolo. 4. Para um driver sem assistente especializado, use o Generic Binding Assistant e selecione somente opções documentadas no formulário. Simulation usa seu editor próprio e pode não mostrar endereço manual. 5. Revise tipo e binding, execute Preview e corrija validações. Quando disponível, Test read/Monitor é leitura transitória: não grava processo, não aplica nem armazena Historian. 6. Apply ao Workspace e siga Save/Publish/Activate para levar a revisão ao Runtime.", "1. In Engineering > TAGs, select the TAG and Data Source. 2. Enter the portable address according to the source and syntax in the Driver help. 3. If a binding assistant appears, fill the fields generated by its displayed schema; it does not invent protocol fields. 4. For a driver without a specialized assistant, use Generic Binding Assistant and select only options documented in the form. Simulation uses its own editor and may not show a manual address. 5. Review the type and binding, run Preview, and fix validation issues. When available, Test read/Monitor is transient and read-only: it does not write to the process, apply changes, or store Historian samples. 6. Apply to Workspace, then follow Save/Publish/Activate to make the revision available to Runtime.", "1. En Engineering > TAGs, seleccione el TAG y la Data Source. 2. Introduzca la dirección portable según la fuente y el formato indicado por la ayuda del Driver. 3. Si aparece un asistente de binding, complete los campos generados por el schema mostrado; no inventa campos de protocolo. 4. Para un driver sin asistente especializado, use Generic Binding Assistant y elija solo opciones documentadas en el formulario. Simulation usa su propio editor y puede no mostrar dirección manual. 5. Revise tipo y binding, ejecute Preview y corrija las validaciones. Cuando esté disponible, Test read/Monitor es lectura transitoria: no escribe en el proceso, no aplica cambios ni almacena muestras Historian. 6. Aplique al Workspace y siga Save/Publish/Activate para llevar la revisión a Runtime."),
            Tx("O TAG fica ligado à origem pelo binding válido para esse schema. Preview comprova validações de configuração, não a disponibilidade do dispositivo em produção.", "The TAG is connected to the source through a binding valid for that schema. Preview checks configuration validation, not production device availability.", "El TAG queda conectado a la fuente mediante un binding válido para ese schema. Preview comprueba validaciones de configuración, no la disponibilidad del dispositivo en producción."),
            Tx("Schema indisponível: confirme o tipo de Data Source e a versão compatível do Driver. Endereço rejeitado: consulte a ajuda específica e confira unidade/indexação/formato com a origem, sem copiar sintaxe de outro protocolo. Sem leitura: siga Diagnostics da Data Source até o dispositivo. Não use escrita em processo vivo como teste de endereçamento.", "Schema unavailable: check the Data Source type and compatible Driver version. Address rejected: consult the specific help and check units/indexing/format with the source; do not copy syntax from another protocol. No read: use Diagnostics from the Data Source down to the device. Do not write to a live process to test addressing.", "Schema no disponible: compruebe el tipo de Data Source y la versión compatible del Driver. Dirección rechazada: consulte la ayuda específica y compruebe unidad/indexación/formato con la fuente; no copie sintaxis de otro protocolo. Sin lectura: use Diagnostics desde la Data Source hasta el dispositivo. No escriba en un proceso real para probar el direccionamiento."),
            "sources.data-sources", "drivers.overview", "tags.quality", "tags.timestamps", "diagnostics.overview", "engineering.lifecycle"),

        TaskGuide(
            "tags.scaling-formatting", "tags",
            Tx("Configurar scaling e formatação", "Configure scaling and formatting", "Configurar scaling y formato"),
            Tx("Scaling define o valor de engenharia; a formatação controla somente como esse valor aparece.", "Scaling defines the engineering value; formatting controls only how that value is displayed.", "Scaling define el valor de ingeniería; el formato controla solo cómo se presenta ese valor."),
            Tx("Use scaling quando o sinal bruto precisa ser convertido para a unidade/faixa de Engineering. Use formato para legibilidade, como casas decimais; não use formato para corrigir um valor operacional errado.", "Use scaling when a raw signal must be converted to the Engineering unit/range. Use formatting for readability, such as decimal places; do not use formatting to fix a wrong operational value.", "Use scaling cuando una señal raw debe convertirse a la unidad/rango de Engineering. Use formato para legibilidad, como decimales; no use formato para corregir un valor operacional incorrecto."),
            Tx("Confirme unidade, tipo, transformação prevista e valores de referência conhecidos com quem entende o instrumento/processo. Faça o teste em ambiente controlado e sem escrita em processo vivo.", "Confirm the unit, type, intended transform, and known reference values with the person responsible for the instrument/process. Test in a controlled environment without writing to a live process.", "Confirme unidad, tipo, transformación prevista y valores de referencia conocidos con quien conoce el instrumento/proceso. Pruebe en un entorno controlado y sin escribir en un proceso real."),
            Tx("1. Abra o TAG em Engineering e confira tipo, unidade e origem. 2. Configure os parâmetros de scaling oferecidos por esse tipo de TAG/origem para converter a leitura bruta em valor de engenharia; não assuma uma fórmula genérica se a superfície oferecer outro contrato. 3. Configure separadamente a precisão/formatação de apresentação. 4. Compare Raw evidence, valor raw decodificado, Engineering value e transformação efetiva quando Test read/Monitor estiver disponível. Essa leitura é transitória e não aplica, ativa, grava no processo nem armazena Historian. 5. Teste pontos de referência conhecidos e limites; execute Preview, Apply e lifecycle normal após validar.", "1. Open the TAG in Engineering and check its type, unit, and source. 2. Configure the scaling parameters offered by that TAG/source type to convert the raw reading into an engineering value; do not assume a generic formula if the surface provides another contract. 3. Separately configure display precision/formatting. 4. Compare Raw evidence, decoded raw value, Engineering value, and effective transform when Test read/Monitor is available. This read is transient and does not apply, activate, write to the process, or store Historian samples. 5. Test known reference points and limits; run Preview, Apply, and the normal lifecycle after validation.", "1. Abra el TAG en Engineering y compruebe tipo, unidad y origen. 2. Configure los parámetros de scaling ofrecidos por ese tipo de TAG/fuente para convertir la lectura raw en valor de ingeniería; no suponga una fórmula genérica si la superficie define otro contrato. 3. Configure por separado la precisión/formato de presentación. 4. Compare Raw evidence, valor raw decodificado, Engineering value y transformación efectiva cuando Test read/Monitor esté disponible. Esta lectura es transitoria y no aplica, activa, escribe en el proceso ni almacena muestras Historian. 5. Pruebe puntos de referencia y límites conocidos; ejecute Preview, Apply y el lifecycle normal tras validar."),
            Tx("O valor operacional segue a transformação configurada; a formatação não muda o dado. Scaling incorreto pode afetar limites, alarmes, histórico, cálculos e telas.", "The operational value follows the configured transform; formatting does not change the data. Incorrect scaling can affect limits, alarms, history, calculations, and screens.", "El valor operacional sigue la transformación configurada; el formato no cambia el dato. Un scaling incorrecto puede afectar límites, alarmas, histórico, cálculos y pantallas."),
            Tx("Valor raw correto, Engineering incorreto: confira scaling, tipo e unidade. Número exibido arredondado: ajuste formato, não scaling. Test read difere do Runtime: confira fonte/binding e revisão Active; a leitura transitória não prova que a revisão foi aplicada. Não ajuste conversão só para fazer um valor de processo parecer plausível.", "Raw value correct, Engineering value wrong: check scaling, type, and unit. Displayed number rounded: adjust formatting, not scaling. Test read differs from Runtime: check the source/binding and Active revision; a transient read does not prove the revision was applied. Do not adjust a transform merely to make a process value look plausible.", "Valor raw correcto, valor de Engineering incorrecto: compruebe scaling, tipo y unidad. Número presentado redondeado: ajuste el formato, no scaling. Test read difiere de Runtime: compruebe fuente/binding y revisión Active; una lectura transitoria no demuestra que la revisión se aplicó. No ajuste la conversión solo para que el valor del proceso parezca plausible."),
            "tags.addressing", "tags.quality", "tags.timestamps", "historian.overview", "alarms.overview", "tags.overview")
    };

    private static TopicDefinition BuildServerScriptsTopic() => Detailed(
        "scripts.server", "scripts",
        Tx("Server Scripts", "Server Scripts", "Server Scripts"),
        Tx("Automação Python executada no servidor, vinculada à revisão ativa e limitada a uma sintaxe e a APIs próprias do EliteSCADA. Não é um interpretador Python geral.", "Server-side Python automation bound to the Active revision and limited to EliteSCADA-specific syntax and APIs. It is not a general Python interpreter.", "Automatización Python ejecutada en el servidor, vinculada a la revisión Active y limitada a una sintaxis y APIs propias de EliteSCADA. No es un intérprete general de Python."),
        S(Tx("API suportada", "Supported API", "API soportada"), Tx(
            "As seis funções da referência abaixo formam a API de TAG, Server Memory e Operational Event. Declare cada TAG como dependência do Script antes de ler ou escrever. Você pode usar o caminho legível quando a dependência tem esse binding configurado; o produto continua verificando a identidade estável. Funções de memória também exigem dependência ServerMemoryTag.",
            "The six functions in the reference below are the TAG, Server Memory and Operational Event API. Declare each TAG as a Script dependency before reading or writing it. You may use a readable path when the dependency has that binding configured; the product still verifies its stable identity. Memory functions also require a ServerMemoryTag dependency.",
            "Las seis funciones de la referencia siguiente forman la API de TAG, Server Memory y Operational Event. Declare cada TAG como dependencia del Script antes de leerlo o escribirlo. Puede usar la ruta legible cuando la dependencia tiene configurado ese binding; el producto sigue verificando la identidad estable. Las funciones de memoria también requieren una dependencia ServerMemoryTag."),
            """
            def update_visual_state():
                left = read_tag("Plant.Pump.LeftPressure")
                right = read_tag("Plant.Pump.RightPressure")
                if left is not None:
                    if right is not None:
                        write_tag("Plant.Pump.PressureBalanced", left == right)

            def record_transition():
                emit_operational_event("<stable-event-definition-id>", "Pressure state evaluated", {"source": "script"})
            """),
        S(Tx("Client Visual e Server: escolha o escopo", "Client Visual and Server: choose a scope", "Client Visual y Server: elija el ámbito"), Tx(
            "Client Visual Script roda no Runtime do cliente e trabalha com interação/propriedades visuais e Client Memory conforme a superfície visual. Server Script roda no backend na revisão Active e não acessa a tela nem Client Memory. Escolha Server quando a regra precisa operar sem uma tela aberta; escolha Client Visual quando a ação pertence à interação ou apresentação do usuário. Nenhum escopo dá acesso direto a Drivers, banco de dados, rede arbitrária ou segredos.",
            "A Client Visual Script runs in the client Runtime and works with visual interactions/properties and Client Memory according to the visual surface. A Server Script runs in the backend on the Active revision and cannot access screens or Client Memory. Choose Server when a rule must run without an open screen; choose Client Visual when the action belongs to user interaction or presentation. Neither scope grants direct access to Drivers, databases, arbitrary network or secrets.",
            "Un Client Visual Script se ejecuta en el Runtime del cliente y trabaja con interacciones/propiedades visuales y Client Memory según la superficie visual. Un Server Script se ejecuta en el backend, en la revisión Active, y no accede a pantallas ni Client Memory. Elija Server cuando la regla deba operar sin una pantalla abierta; elija Client Visual cuando la acción pertenezca a la interacción o presentación del usuario. Ningún ámbito concede acceso directo a Drivers, bases de datos, red arbitraria ni secretos.")),
        S(Tx("Como organizar o código", "How to organize code", "Cómo organizar el código"), Tx(
            "Cada ponto de entrada configurado em Engineering aponta para uma função pelo nome. No nível principal do arquivo, declare funções; não execute leituras, escritas ou cálculos fora delas. Um handler pode receber zero argumentos. Para ServerRuntimeEvent, declare um argumento para receber o dicionário do evento. O nome da função deve coincidir exatamente com o configurado no ponto de entrada.",
            "Each Engineering entry point refers to a function by name. At the top level of the file, declare functions; do not run reads, writes or calculations outside them. A handler may receive zero arguments. For ServerRuntimeEvent, declare one argument to receive the event dictionary. The function name must exactly match the entry point configuration.",
            "Cada punto de entrada de Engineering apunta a una función por su nombre. En el nivel principal del archivo, declare funciones; no ejecute lecturas, escrituras ni cálculos fuera de ellas. Un handler puede recibir cero argumentos. Para ServerRuntimeEvent, declare un argumento para recibir el diccionario del evento. El nombre de la función debe coincidir exactamente con el configurado en el punto de entrada."),
            """
            def on_start():
                pressure = read_tag("Plant.Pump.DischargePressure")

            def on_device_event(event):
                action = event["canonicalEvent"]["payload"]["action"]
            """),
        S(Tx("Sintaxe aceita e limites", "Accepted syntax and limits", "Sintaxis aceptada y límites"), Tx(
            "O runtime aceita funções def sem decorators, parâmetros padrão ou keyword-only; atribuição local e +=, -=, *=; if/else; return; pass; listas, tuplas, dicionários e acesso por índice; operações +, -, *, /, //, %, **; comparações ==, !=, <, <=, >, >=, is, is not; not, and e or; chamadas posicionais a funções do Script e a min, max, abs, round, int, float, bool, str e len. Não aceita import, execução/variáveis no nível principal, for/while, comprehensions, class, try/except, lambda, f-string, acesso a atributo nem argumentos nomeados. and/or avaliam todos os operandos e não têm o curto-circuito do Python padrão. Para operações que podem falhar, use ifs aninhados. Mesmo a sintaxe aceita pode falhar por tipo incompatível ou divisão por zero.",
            "The runtime accepts def functions without decorators, default or keyword-only parameters; local assignment and +=, -=, *=; if/else; return; pass; lists, tuples, dictionaries and indexing; +, -, *, /, //, %, ** operations; ==, !=, <, <=, >, >=, is, is not comparisons; not, and and or; positional calls to Script functions and to min, max, abs, round, int, float, bool, str and len. It does not accept import, top-level execution/variables, for/while, comprehensions, class, try/except, lambda, f-strings, attribute access or keyword arguments. and/or evaluate every operand and do not short-circuit like standard Python. For operations that can fail, use nested if statements. Accepted syntax can still fail on incompatible types or division by zero.",
            "El runtime acepta funciones def sin decorators, parámetros predeterminados ni keyword-only; asignación local y +=, -=, *=; if/else; return; pass; listas, tuplas, diccionarios y acceso por índice; operaciones +, -, *, /, //, %, **; comparaciones ==, !=, <, <=, >, >=, is, is not; not, and y or; llamadas posicionales a funciones del Script y a min, max, abs, round, int, float, bool, str y len. No acepta import, ejecución/variables en el nivel principal, for/while, comprehensions, class, try/except, lambda, f-string, acceso a atributos ni argumentos con nombre. and/or evalúan todos los operandos y no tienen el cortocircuito del Python estándar. Para operaciones que puedan fallar, use if anidados. La sintaxis aceptada también puede fallar por tipos incompatibles o división por cero."),
            """
            def calculate_deviation():
                value = read_tag("Plant.Pump.Current")
                reference = read_tag("Plant.Pump.ReferenceCurrent")
                if value is not None:
                    if reference is not None:
                        return round(value - reference, 2)
                return None
            """),
        S(Tx("Exemplo: limite com histerese e evento", "Example: threshold with hysteresis and event", "Ejemplo: umbral con histéresis y evento"), Tx(
            "Associe o ponto de entrada TagChanged ao TAG de pressão. Declare os TAGs de limite e o TAG Server Memory de estado como dependências; o de estado precisa ser ServerMemoryTag. O limite de retorno deve ser menor que o limite de disparo. Assim o mesmo Operational Event não é repetido a cada atualização enquanto a pressão permanece alta. Substitua caminhos e ID de evento pelos objetos reais do projeto.",
            "Bind the TagChanged entry point to the pressure TAG. Declare the limit TAGs and the state Server Memory TAG as dependencies; the state TAG must be a ServerMemoryTag. The reset threshold should be lower than the trigger threshold. This prevents the same Operational Event from repeating on every update while pressure remains high. Replace paths and event ID with the project's actual objects.",
            "Asocie el punto de entrada TagChanged al TAG de presión. Declare los TAGs de límite y el TAG Server Memory de estado como dependencias; el TAG de estado debe ser ServerMemoryTag. El umbral de retorno debe ser menor que el de activación. Así el mismo Operational Event no se repite en cada actualización mientras la presión siga alta. Sustituya las rutas y el ID del evento por los objetos reales del proyecto."),
            """
            def on_pressure_change():
                pressure = read_tag("Plant.Pump.DischargePressure")
                high_limit = read_tag("Limits.Pump.HighPressure")
                reset_limit = read_tag("Limits.Pump.ResetPressure")
                alarm_latched = read_server_memory("State.Pump.HighPressure")
                if pressure is not None:
                    if high_limit is not None:
                        if reset_limit is not None:
                            if alarm_latched is not True:
                                if pressure >= high_limit:
                                    write_server_memory("State.Pump.HighPressure", True)
                                    emit_operational_event("<stable-event-definition-id>", "Pump discharge pressure crossed the high limit", {"pressure": str(pressure)})
                            else:
                                if pressure <= reset_limit:
                                    write_server_memory("State.Pump.HighPressure", False)
            """),
        S(Tx("Exemplo: Timer sem laço", "Example: Timer without a loop", "Ejemplo: Timer sin bucle"), Tx(
            "Configure um ponto de entrada Timer com intervalo de pelo menos 50 ms (ou o mínimo informado pela instalação). O host chama o handler a cada disparo; não crie while/for nem espere dentro do Script. Timer não garante sincronismo com o ciclo de aquisição do Driver: leia o valor corrente disponível e trate ausência explicitamente.",
            "Configure a Timer entry point with an interval of at least 50 ms (or the minimum reported by the installation). The host calls the handler on each tick; do not create while/for loops or wait inside the Script. A Timer does not guarantee synchronization with Driver acquisition; read the current available value and handle absence explicitly.",
            "Configure un punto de entrada Timer con un intervalo de al menos 50 ms (o el mínimo indicado por la instalación). El host llama al handler en cada tick; no cree bucles while/for ni espere dentro del Script. Timer no garantiza sincronización con la adquisición del Driver: lea el valor disponible y trate explícitamente su ausencia."),
            """
            def on_timer():
                current = read_tag("Plant.Pump.Current")
                if current is not None:
                    write_server_memory("State.Pump.LastCurrent", current)
            """),
        S(Tx("Exemplo: Rich Command com resultado tratado", "Example: Rich Command with handled result", "Ejemplo: Rich Command con resultado tratado"), Tx(
            "invoke_rich_command é uma extensão do host para Rich Commands declarados, não uma conexão direta ao Driver. Declare o comando como dependência RichCommand, configure o binding, associe ServerRuntimeEvent a um Transient Event real e conceda CommandExecute no scope exato do comando. Parâmetros escalares devem seguir o schema. A resposta distingue Rejected, Accepted, Completed, Failed, TimedOut e Unknown. Accepted não prova conclusão física; Unknown é ambíguo e não deve ser repetido automaticamente. O exemplo demonstra um pulso de 250 ms após a ação single.",
            "invoke_rich_command is a host extension for declared Rich Commands, not a direct Driver connection. Declare the command as a RichCommand dependency, configure its binding, associate ServerRuntimeEvent with a real Transient Event, and grant CommandExecute for the exact command scope. Scalar parameters must follow the schema. The response distinguishes Rejected, Accepted, Completed, Failed, TimedOut and Unknown. Accepted does not prove physical completion; Unknown is ambiguous and must not be retried automatically. The example demonstrates a 250 ms pulse after the single action.",
            "invoke_rich_command es una extensión del host para Rich Commands declarados, no una conexión directa al Driver. Declare el comando como dependencia RichCommand, configure su binding, asocie ServerRuntimeEvent a un Transient Event real y conceda CommandExecute en el scope exacto del comando. Los parámetros escalares deben seguir el schema. La respuesta distingue Rejected, Accepted, Completed, Failed, TimedOut y Unknown. Accepted no prueba la finalización física; Unknown es ambiguo y no debe repetirse automáticamente. El ejemplo demuestra un pulso de 250 ms tras la acción single."),
            """
            def on_device_event(event):
                action = event["canonicalEvent"]["payload"]["action"]
                if action == "single":
                    result = invoke_rich_command("<stable-rich-command-guid>", {"onTime": "00:00:00.2500000"})
                    if result["outcome"] == "Completed":
                        write_server_memory("State.Command.LastOutcome", 1)
                    else:
                        write_server_memory("State.Command.LastOutcome", -1)
            """),
        S(Tx("Quality: não invente Good", "Quality: do not invent Good", "Quality: no invente Good"), Tx(
            "read_tag retorna o valor, não expõe ao Script a quality nem o timestamp de origem. publish_server_memory_sample exige que o autor informe uma TagQuality. Use Good somente se a aplicação tiver uma regra validada para qualidade e atualidade dos dados — testar apenas se o valor não é None não basta, pois não revela quality nem timestamp de origem. Se a confiabilidade não puder ser estabelecida, use uma qualidade conservadora apropriada, como Uncertain, ou não publique. Não transforme valor ausente em zero. O destino deve ser um ServerMemoryTag declarado.",
            "read_tag returns the value; it does not expose the source quality or timestamp to the Script. publish_server_memory_sample requires the author to provide a TagQuality. Use Good only if the application has a validated rule for data quality and freshness—checking only whether the value is None is not enough because source quality and timestamp are not exposed. If reliability cannot be established, use an appropriate conservative quality such as Uncertain, or do not publish. Do not turn a missing value into zero. The target must be a declared ServerMemoryTag.",
            "read_tag devuelve el valor; no expone al Script la quality ni la marca de tiempo de origen. publish_server_memory_sample requiere que el autor indique una TagQuality. Use Good solo si la aplicación tiene una regla validada para calidad y vigencia de los datos: comprobar únicamente que el valor no sea None no basta porque no se exponen la quality ni la marca de tiempo de origen. Si no puede establecerse la confiabilidad, use una calidad conservadora adecuada, como Uncertain, o no publique. No convierta un valor ausente en cero. El destino debe ser un ServerMemoryTag declarado."),
            """
            def publish_calculated_sample():
                value = read_tag("Plant.Pump.ValidatedDerivedValue")
                if value is not None:
                    publish_server_memory_sample("State.Pump.DerivedValue", value, "Uncertain")
            """),
        S(Tx("Receita: estado visual permitido", "Recipe: allowed visual state", "Receta: estado visual permitido"), Tx(
            "Leia TAGs declarados, avalie a condição e escreva somente em um TAG gravável e autorizado que já esteja vinculado à propriedade visual. Server Script não altera objetos de tela diretamente. Se o valor do processo estiver ausente, não substitua por zero nem escreva um estado normal por suposição.",
            "Read declared TAGs, evaluate the condition and write only to a writable, authorized TAG already bound to the visual property. A Server Script does not mutate screen objects directly. If the process value is missing, do not replace it with zero or write a normal state by assumption.",
            "Lea TAGs declarados, evalúe la condición y escriba solo en un TAG grabable y autorizado ya vinculado a la propiedad visual. Server Script no modifica directamente objetos de pantalla. Si falta el valor del proceso, no lo sustituya por cero ni escriba un estado normal por suposición."),
            """
            def update_pressure_indicator():
                pressure = read_tag("Plant.Pump.DischargePressure")
                high_limit = read_tag("Limits.Pump.HighPressure")
                if pressure is not None:
                    if high_limit is not None:
                        write_tag("Plant.Screen.PumpPressureHigh", pressure >= high_limit)
            """),
        S(Tx("Lifecycle e triggers", "Lifecycle and triggers", "Lifecycle y triggers"), Tx(
            "Somente scripts habilitados de scope Server são hospedados na revisão Active. O host atual despacha Initialize, Dispose, TagChanged, Timer e ServerRuntimeEvent ligados a ocorrências canônicas de Transient Event referenciadas no grafo Active; targets genéricos não acionam scripts. Ao trocar Active, a geração anterior é cancelada; acesso de TAG e emissão de Operational Event usam revision gate para impedir execução obsoleta sobre uma revisão nova.",
            "Only enabled Server-scope scripts are hosted on the Active revision. The current host dispatches Initialize, Dispose, TagChanged, Timer and ServerRuntimeEvent triggers bound to canonical Transient Event occurrences referenced by the Active graph; generic targets do not trigger scripts. When Active changes, the previous generation is cancelled; TAG access and Operational Event emission use a revision gate to prevent obsolete execution against a new revision.",
            "Solo scripts habilitados de scope Server se hospedan en la revisión Active. El host actual despacha Initialize, Dispose, TagChanged, Timer y ServerRuntimeEvent vinculados a ocurrencias canónicas de Transient Event referenciadas por el grafo Active; los targets genéricos no activan scripts. Al cambiar Active, la generación anterior se cancela; acceso TAG y emisión de Operational Event usan revision gate para impedir ejecución obsoleta sobre una revisión nueva.")),
        S(Tx("Execução e falhas", "Execution and failures", "Ejecución y fallas"), Tx(
            "A política padrão usa timeout de handler de 250 ms, fila limitada a 128 eventos, Timer mínimo de 50 ms e entra em cooldown após 5 falhas consecutivas. Ao fim do cooldown configurável, somente um evento é aceito como probe de recuperação; sucesso restaura o processamento e nova falha reinicia o cooldown. Fila, timeout, cancelamento, fault isolation e diagnósticos pertencem à instância do script e não concedem fallback de Authority.",
            "The default policy uses a 250 ms handler timeout, a queue bounded to 128 events, a 50 ms minimum Timer and enters cooldown after 5 consecutive failures. After the configurable cooldown, only one event is accepted as a recovery probe; success restores processing and another failure starts the cooldown again. Queue, timeout, cancellation, fault isolation and diagnostics belong to the script instance and grant no Authority fallback.",
            "La política por defecto usa timeout de handler de 250 ms, cola limitada a 128 eventos, Timer mínimo de 50 ms y entra en cooldown después de 5 fallas consecutivas. Al terminar el cooldown configurable, solo un evento se acepta como prueba de recuperación; el éxito restaura el procesamiento y una nueva falla reinicia el cooldown. Cola, timeout, cancelación, fault isolation y diagnósticos pertenecen a la instancia y no conceden fallback de Authority.")),
        S(Tx("Sandbox e segurança", "Sandbox and security", "Sandbox y seguridad"), Tx(
            "A superfície de Server Script permite leitura de TAGs compartilhados, leitura/escrita de Server Memory e escrita de TAGs conforme o contrato. O sandbox nega filesystem, sistema operacional, shell/process execution, rede arbitrária, database, acesso direto a industrial drivers, secrets, browser DOM e browser storage. O preflight rejeita imports/calls obviamente proibidos, mas é feedback de editor; enforcement de sandbox não deve depender de scan de texto.",
            "The Server Script surface allows shared TAG reads, Server Memory reads/writes and TAG writes according to contract. The sandbox denies filesystem, operating system, shell/process execution, arbitrary network, database, direct industrial-driver access, secrets, browser DOM and browser storage. Preflight rejects obviously prohibited imports/calls but is editor feedback; sandbox enforcement must not depend on text scanning.",
            "La superficie Server Script permite lectura de TAGs compartidos, lectura/escritura de Server Memory y escritura de TAGs según contrato. El sandbox niega filesystem, sistema operativo, shell/process execution, red arbitraria, database, acceso directo a industrial drivers, secrets, browser DOM y browser storage. Preflight rechaza imports/calls claramente prohibidos, pero es feedback del editor; enforcement del sandbox no debe depender de escaneo de texto.")),
        S(Tx("Exemplo Server Memory", "Server Memory example", "Ejemplo Server Memory"), Tx(
            "No editor de dependências, inclua o TAG pelo caminho legível e confirme que é ServerMemoryTag. O caminho fica associado ao ID estável; se mudar ou apontar para outra identidade, a execução falha fechada. Este exemplo lê um estado retentivo e atualiza outro TAG de memória; adapte ambos os TAGs ao tipo de dado real.",
            "In the dependency editor, add the TAG by readable path and confirm that it is a ServerMemoryTag. The path is bound to the stable ID; if it changes or points to another identity, execution fails closed. This example reads a retained state and updates another memory TAG; adapt both TAGs to the real data type.",
            "En el editor de dependencias, agregue el TAG por ruta legible y confirme que es ServerMemoryTag. La ruta queda vinculada al ID estable; si cambia o apunta a otra identidad, la ejecución falla de forma cerrada. Este ejemplo lee un estado retentivo y actualiza otro TAG de memoria; adapte ambos TAGs al tipo de dato real."),
            """
            def update_runtime_memory():
                running = read_server_memory("Plant.Pump.Running")
                if running is not None:
                    write_server_memory("Plant.Pump.LastKnownRunning", bool(running))
            """));

    private static IReadOnlyCollection<ContextualHelpScriptApi> BuildServerScriptApi(string locale) => new[]
    {
        ScriptApi("read_tag", "read_tag(tag_reference)", Tx("tag_reference: caminho com binding declarado ou ID estável de TAG declarado.", "tag_reference: path with a declared binding or stable ID of a declared TAG.", "tag_reference: ruta con binding declarado o ID estable de un TAG declarado."), Tx("Retorna o valor corrente do TAG; não retorna quality nem timestamp de origem.", "Returns the current TAG value; it does not return source quality or timestamp.", "Devuelve el valor actual del TAG; no devuelve quality ni marca de tiempo de origen."), Tx("Somente leitura. A dependência precisa estar declarada. Um caminho legível deve corresponder exatamente ao binding da dependência.", "Read only. The dependency must be declared. A readable path must exactly match the dependency binding.", "Solo lectura. La dependencia debe estar declarada. Una ruta legible debe coincidir exactamente con el binding de la dependencia."), "def example_read_tag():\n    value = read_tag(\"Plant.Pump.Running\")", locale),
        ScriptApi("read_server_memory", "read_server_memory(tag_reference)", Tx("tag_reference: caminho com binding declarado ou ID estável de ServerMemoryTag declarado.", "tag_reference: path with a declared binding or stable ID of a declared ServerMemoryTag.", "tag_reference: ruta con binding declarado o ID estable de un ServerMemoryTag declarado."), Tx("Retorna o valor corrente da Server Memory.", "Returns the current Server Memory value.", "Devuelve el valor actual de Server Memory."), Tx("Somente ServerMemoryTag declarado é aceito. Use o caminho apenas quando ele corresponder ao binding e à identidade estável.", "Only a declared ServerMemoryTag is accepted. Use a path only when it matches the binding and stable identity.", "Solo se acepta un ServerMemoryTag declarado. Use la ruta solo cuando coincida con el binding y la identidad estable."), "def example_read_server_memory():\n    value = read_server_memory(\"State.Pump.Running\")", locale),
        ScriptApi("write_tag", "write_tag(tag_reference, value)", Tx("tag_reference: caminho com binding declarado ou ID estável de TAG declarado; value: valor compatível com o tipo do TAG.", "tag_reference: path with a declared binding or stable ID of a declared TAG; value: value compatible with the TAG type.", "tag_reference: ruta con binding declarado o ID estable de un TAG declarado; value: valor compatible con el tipo del TAG."), Tx("Escreve o valor no TAG e retorna None.", "Writes the value to the TAG and returns None.", "Escribe el valor en el TAG y devuelve None."), Tx("Exige TAG declarado, gravável e autorizado no escopo correspondente. Não altera diretamente um objeto de tela.", "Requires a declared, writable TAG authorized in the corresponding scope. It does not directly change a screen object.", "Requiere un TAG declarado y grabable, autorizado en el ámbito correspondiente. No cambia directamente un objeto de pantalla."), "def example_write_tag():\n    write_tag(\"Plant.Screen.PumpPressureHigh\", True)", locale),
        ScriptApi("write_server_memory", "write_server_memory(tag_reference, value)", Tx("tag_reference: caminho com binding declarado ou ID estável de ServerMemoryTag declarado; value: valor serializável.", "tag_reference: path with a declared binding or stable ID of a declared ServerMemoryTag; value: serializable value.", "tag_reference: ruta con binding declarado o ID estable de un ServerMemoryTag declarado; value: valor serializable."), Tx("Grava o valor em Server Memory e retorna None.", "Writes the value to Server Memory and returns None.", "Escribe el valor en Server Memory y devuelve None."), Tx("Exige dependência ServerMemoryTag declarada. O caminho deve corresponder ao binding e à identidade estável.", "Requires a declared ServerMemoryTag dependency. The path must match the binding and stable identity.", "Requiere una dependencia ServerMemoryTag declarada. La ruta debe coincidir con el binding y la identidad estable."), "def example_write_server_memory():\n    write_server_memory(\"State.Pump.LastCurrent\", 12.5)", locale),
        ScriptApi("publish_server_memory_sample", "publish_server_memory_sample(tag_reference, value, quality)", Tx("tag_reference: caminho com binding declarado ou ID estável de ServerMemoryTag declarado; value: valor serializável; quality: nome canônico de TagQuality.", "tag_reference: path with a declared binding or stable ID of a declared ServerMemoryTag; value: serializable value; quality: canonical TagQuality name.", "tag_reference: ruta con binding declarado o ID estable de un ServerMemoryTag declarado; value: valor serializable; quality: nombre canónico de TagQuality."), Tx("Publica uma amostra qualificada em Server Memory e retorna None.", "Publishes a qualified Server Memory sample and returns None.", "Publica una muestra cualificada en Server Memory y devuelve None."), Tx("Exige ServerMemoryTag declarado e quality exatamente igual a um nome TagQuality aceito. A função não deriva quality nem timestamp do TAG de origem.", "Requires a declared ServerMemoryTag and a quality exactly matching an accepted TagQuality name. This function does not derive quality or timestamp from the source TAG.", "Requiere un ServerMemoryTag declarado y una quality que coincida exactamente con un nombre TagQuality aceptado. Esta función no deriva la quality ni la marca de tiempo del TAG de origen."), "def example_publish_sample():\n    publish_server_memory_sample(\"State.Pump.LastCurrent\", 12.5, \"Uncertain\")", locale),
        ScriptApi("emit_operational_event", "emit_operational_event(definition_id, message=None, context=None)", Tx("definition_id: ID estável da definição do Operational Event; message: texto opcional; context: dicionário opcional cujos valores são textos.", "definition_id: stable Operational Event definition ID; message: optional text; context: optional dictionary whose values are strings.", "definition_id: ID estable de la definición del Operational Event; message: texto opcional; context: diccionario opcional cuyos valores son textos."), Tx("Solicita a emissão do Operational Event e retorna None.", "Requests emission of the Operational Event and returns None.", "Solicita la emisión del Operational Event y devuelve None."), Tx("A definição precisa existir e as entradas têm limites de tamanho/quantidade. A chamada não concede fallback de Authority.", "The definition must exist and inputs have size/count limits. This call grants no Authority fallback.", "La definición debe existir y las entradas tienen límites de tamaño/cantidad. La llamada no concede fallback de Authority."), "def example_emit_event():\n    emit_operational_event(\"<stable-event-definition-id>\", \"threshold crossed\", {\"source\": \"script\"})", locale)
    };

    private static ContextualHelpScriptApi ScriptApi(
        string name,
        string signature,
        LocalizedText parameters,
        LocalizedText result,
        LocalizedText safety,
        string example,
        string locale) => new(
        name,
        signature,
        parameters.For(locale),
        result.For(locale),
        safety.For(locale),
        example);

    private static TopicDefinition BuildReusableLibrariesTopic() => Detailed(
        "libraries.reusable-resources", "libraries",
        Tx("Bibliotecas de recursos reutilizáveis", "Reusable resource libraries", "Bibliotecas de recursos reutilizables"),
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
        params string[] relatedTopicIds) =>
        TaskGuide(
            id,
            category,
            title,
            summary,
            whenToUse,
            prerequisites,
            steps,
            expectedResult,
            commonProblems,
            DefaultTaskGuideDiagnostics(category),
            relatedTopicIds);

    private static LocalizedText DefaultTaskGuideDiagnostics(string category) => category switch
    {
        "high-availability" => Tx(
            "Se a prontidão não avançar ou uma operação for recusada, compare a configuração salva com a em execução e confira nós, peer, freshness, referência de fencing e histórico da operação. Corrija o requisito indicado; não force promoção para remover um aviso.",
            "If readiness does not advance or an operation is rejected, compare saved and running configuration and check nodes, peer, freshness, fencing reference, and operation history. Resolve the reported prerequisite; do not force promotion to clear a warning.",
            "Si la preparación no avanza o se rechaza una operación, compare la configuración guardada con la activa y compruebe nodos, peer, freshness, referencia de fencing e historial de operación. Corrija el requisito indicado; no fuerce una promoción para quitar un aviso."),
        "installation" => Tx(
            "Se a troca ou desassociação não prosseguir, confira a instalação e o destino selecionados, o estado atual de Application e Authority e as confirmações exibidas. Verifique separadamente os exports e dados que precisam ser preservados; não repita a operação até reconciliar o estado atual.",
            "If switching or detachment does not proceed, verify the selected installation and target, current Application and Authority state, and displayed confirmations. Separately verify exports and data that must be preserved; do not repeat the operation until the current state is reconciled.",
            "Si el cambio o la desasociación no continúa, compruebe la instalación y el destino seleccionados, el estado actual de Application y Authority y las confirmaciones mostradas. Verifique por separado los exports y datos que deben preservarse; no repita la operación hasta reconciliar el estado actual."),
        "intelligence" => Tx(
            "Sem resultado ou com cobertura baixa, confira licença e escopo habilitado, perfis e TAGs associados, qualidade e período dos dados, contexto operacional e etapas selecionadas. Resultado insuficiente não prova falha do equipamento nem eficiência; consulte os dados de cobertura antes de interpretar o indicador.",
            "If there is no result or coverage is low, check the enabled license and scope, assigned profiles and TAGs, data quality and time range, operating context, and selected stages. Insufficient evidence does not prove equipment failure or efficiency; review coverage data before interpreting an indicator.",
            "Si no hay resultado o la cobertura es baja, compruebe la licencia y el alcance habilitados, los perfiles y TAGs asignados, la calidad y el período de datos, el contexto operativo y las etapas seleccionadas. La evidencia insuficiente no demuestra una falla del equipo ni eficiencia; revise la cobertura antes de interpretar un indicador."),
        "tags" => Tx(
            "Para um valor inesperado, confira identidade e caminho do TAG, Data Source, binding/endereço, quality e timestamp. Depois valide tipo/unidade e scaling/formatação; use Diagnostics para localizar a primeira etapa divergente, sem transformar qualidade ausente em zero.",
            "For an unexpected value, check the TAG identity and path, Data Source, binding/address, quality, and timestamp. Then verify type/unit and scaling/formatting; use Diagnostics to find the first stage that differs, without turning missing quality into zero.",
            "Ante un valor inesperado, compruebe la identidad y ruta del TAG, Data Source, binding/dirección, quality y timestamp. Después valide tipo/unidad y scaling/formato; use Diagnostics para localizar la primera etapa distinta, sin convertir una quality ausente en cero."),
        "runtime" => Tx(
            "Se a tela ou dado não corresponder ao esperado, confirme revisão Active, classe/estado da sessão e qualidade/timestamp do TAG; verifique se está em Live ou Playback. Para acesso negado ou comando recusado, consulte capability e scope e não repita automaticamente um resultado ambíguo.",
            "If the Screen or data does not match expectations, check the Active revision, session class/state, and TAG quality/timestamp; verify whether the view is Live or Playback. For denied access or a rejected command, check capability and scope, and do not automatically repeat an ambiguous result.",
            "Si la Pantalla o el dato no coincide con lo esperado, compruebe la revisión Active, la clase/estado de sesión y quality/timestamp del TAG; verifique si está en Live o Playback. Ante acceso denegado o comando rechazado, consulte capability y scope y no repita automáticamente un resultado ambiguo."),
        "getting-started" => Tx(
            "Se a tela inicial ou o modo de entrada surpreender, confirme o endereço da instalação e se ela está em bootstrap neutral ou já tem Authority. Em erro de login, confira a conta com o administrador e não faça tentativas repetidas; em acesso negado, peça o capability/scope apropriado.",
            "If the entry page or sign-in mode is unexpected, verify the installation address and whether it is in neutral bootstrap or already has Authority. For sign-in errors, check the account with the administrator and avoid repeated attempts; for denied access, request the appropriate capability/scope.",
            "Si la página inicial o el modo de entrada sorprende, confirme la dirección de instalación y si está en bootstrap neutral o ya tiene Authority. Ante un error de login, compruebe la cuenta con el administrador y no repita intentos; ante acceso denegado, solicite el capability/scope adecuado."),
        _ => Tx(
            "Compare a mensagem e o estado exibidos com os pré-requisitos e a configuração atual. Consulte Diagnostics e o tópico relacionado antes de repetir uma operação, especialmente se o resultado for ambíguo.",
            "Compare the displayed message and state with prerequisites and current configuration. Check Diagnostics and the related topic before repeating an operation, especially when its result is ambiguous.",
            "Compare el mensaje y el estado mostrados con los requisitos previos y la configuración actual. Consulte Diagnostics y el tema relacionado antes de repetir una operación, especialmente si el resultado es ambiguo.")
    };

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
