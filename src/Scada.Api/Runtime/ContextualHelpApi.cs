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

    public static ContextualHelpCatalogView Build(string? requestedLocale)
    {
        var locale = NormalizeLocale(requestedLocale);
        var text = Text.For(locale);
        var topics = new List<ContextualHelpTopic>
        {
            Topic("runtime.overview", "runtime", text.RuntimeTitle, text.RuntimeSummary,
                Section(text.AuthorityHeading, text.RuntimeAuthority),
                Section(text.NavigationHeading, text.RuntimeNavigation)),
            Topic("runtime.history", "runtime", text.HistoryTitle, text.HistorySummary,
                Section(text.AuthorityHeading, text.HistoryAuthority)),
            Topic("engineering.overview", "engineering", text.EngineeringTitle, text.EngineeringSummary,
                Section(text.AuthorityHeading, text.EngineeringAuthority)),
            Topic("audit.overview", "audit", text.AuditTitle, text.AuditSummary,
                Section(text.AuthorityHeading, text.AuditAuthority)),
            Topic("licensing.overview", "licensing", text.LicensingTitle, text.LicensingSummary,
                Section(text.AuthorityHeading, text.LicensingAuthority)),
            Topic("gateway.overview", "gateway", text.GatewayTitle, text.GatewaySummary,
                Section(text.AuthorityHeading, text.GatewayAuthority)),
            Topic("scripts.server", "scripts", text.ScriptTitle, text.ScriptSummary,
                Section(text.ScriptApiHeading, text.ScriptApiBody,
                    "value = read_tag(\"<stable-tag-id>\")\nwrite_tag(\"<stable-tag-id>\", value)\nemit_operational_event(\"<definition-id>\", \"message\", {\"source\": \"script\"})"),
                Section(text.ScriptMemoryHeading, text.ScriptMemoryBody,
                    "value = read_server_memory(\"<stable-tag-id>\")\nwrite_server_memory(\"<stable-tag-id>\", value)\npublish_server_memory_sample(\"<stable-tag-id>\", value, \"Good\")"))
        };

        var dataSourceTypes = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes;

        foreach (var source in dataSourceTypes
            .Where(item => string.Equals(item.Kind, "sourceProvider", StringComparison.Ordinal))
            .OrderBy(item => item.TypeKey, StringComparer.Ordinal))
        {
            topics.Add(Topic(
                $"source.{source.TypeKey}",
                "sources",
                source.DisplayName,
                text.SourceSummary,
                Section(text.AuthorityHeading, text.SourceAuthority)));
        }

        foreach (var driver in dataSourceTypes
            .Where(item => string.Equals(item.Kind, "communicationDriver", StringComparison.Ordinal))
            .OrderBy(item => item.TypeKey, StringComparer.Ordinal))
        {
            var schema = driver.ConfigurationSchema;
            var sourceFields = schema?.DataSourceFields
                .Select(field => $"{field.Key} [{field.ValueKind}]{(field.Required ? " *" : string.Empty)}")
                .ToArray() ?? Array.Empty<string>();
            var bindingFields = schema?.TagBindingFields
                .Select(field => $"{field.Key} [{field.ValueKind}]{(field.Required ? " *" : string.Empty)}")
                .ToArray() ?? Array.Empty<string>();
            var capabilities = new[]
            {
                driver.Capabilities.SupportsConnectionTest ? text.CapabilityConnectionTest : null,
                driver.Capabilities.SupportsDiscovery ? text.CapabilityDiscovery : null,
                driver.Capabilities.SupportsBrowse ? text.CapabilityBrowse : null,
                driver.Capabilities.SupportsFileImport ? text.CapabilityFileImport : null,
                driver.Capabilities.SupportsReconcile ? text.CapabilityReconcile : null,
                driver.Capabilities.SupportsSharedTransportInfrastructure ? text.CapabilitySharedTransport : null
            }.Where(value => value is not null).Cast<string>().ToArray();

            topics.Add(Topic(
                $"driver.{driver.TypeKey}",
                "drivers",
                driver.DisplayName,
                text.DriverSummary,
                Section(text.DriverIdentityHeading, $"{text.DriverTypeKey}: {driver.TypeKey}"),
                Section(text.DriverCapabilitiesHeading,
                    capabilities.Length == 0 ? text.DriverNoCapabilities : string.Join("\n", capabilities.Select(item => $"• {item}"))),
                Section(text.DriverSourceFieldsHeading,
                    sourceFields.Length == 0 ? text.DriverNoFields : string.Join("\n", sourceFields)),
                Section(text.DriverBindingFieldsHeading,
                    bindingFields.Length == 0 ? text.DriverNoFields : string.Join("\n", bindingFields))));
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

    private static ContextualHelpTopic Topic(
        string id,
        string category,
        string title,
        string summary,
        params ContextualHelpSection[] sections) =>
        new(id, category, title, summary, sections);

    private static ContextualHelpSection Section(string heading, string body, string? code = null) =>
        new(heading, body, code);

    private sealed record Text(
        string AuthorityHeading,
        string NavigationHeading,
        string RuntimeTitle,
        string RuntimeSummary,
        string RuntimeAuthority,
        string RuntimeNavigation,
        string HistoryTitle,
        string HistorySummary,
        string HistoryAuthority,
        string EngineeringTitle,
        string EngineeringSummary,
        string EngineeringAuthority,
        string AuditTitle,
        string AuditSummary,
        string AuditAuthority,
        string LicensingTitle,
        string LicensingSummary,
        string LicensingAuthority,
        string GatewayTitle,
        string GatewaySummary,
        string GatewayAuthority,
        string SourceSummary,
        string SourceAuthority,
        string ScriptTitle,
        string ScriptSummary,
        string ScriptApiHeading,
        string ScriptApiBody,
        string ScriptMemoryHeading,
        string ScriptMemoryBody,
        string DriverAvailabilityHeading,
        string DriverSummary,
        string DriverIdentityHeading,
        string DriverTypeKey,
        string DriverCapabilitiesHeading,
        string DriverSourceFieldsHeading,
        string DriverBindingFieldsHeading,
        string DriverNoCapabilities,
        string DriverNoFields,
        string CapabilityConnectionTest,
        string CapabilityDiscovery,
        string CapabilityBrowse,
        string CapabilityFileImport,
        string CapabilityReconcile,
        string CapabilitySharedTransport)
    {
        public static Text For(string locale) => locale switch
        {
            "en" => English,
            "es" => Spanish,
            _ => Portuguese
        };

        private static readonly Text Portuguese = new(
            "Autoridade", "Navegação", "Runtime", "Operação da aplicação ativa.",
            "O backend e a revisão Active são a autoridade do Runtime. A interface não cria um segundo motor de processo.",
            "Use a navegação do Runtime para visão operacional e histórico conforme as capacidades efetivas da sessão.",
            "Histórico", "Consulta local de dados históricos autorizados.",
            "A consulta respeita a autoridade do Runtime e a visibilidade dos TAGs da sessão.",
            "Engineering", "Configuração e ciclo de vida do projeto.",
            "Working, Revision, Published e Active permanecem estados distintos. Salvar não ativa automaticamente uma aplicação.",
            "Auditoria", "Rastreabilidade de ações de segurança e engenharia.",
            "Auditoria é distinta de Alarmes e de Eventos Operacionais e não deve ser usada como substituto desses domínios.",
            "Licenciamento", "Estado e recursos da licença do produto.",
            "A licença limita recursos comerciais sem substituir autenticação, autorização ou autoridade do Runtime.",
            "Gateway", "Coordenação de fontes e fluxo de dados do Runtime.",
            "Gateway participa da composição do Runtime; consumidores usam TAG Engine/cache/eventos, sem acesso direto privado aos drivers.",
            "Fonte de memória disponível nesta compilação.",
            "A fonte é declarada pelo catálogo canônico do produto e participa do modelo de Data Sources sem se apresentar como driver de comunicação.",
            "Server Scripts", "Subset determinístico de Python executado pelo Runtime.",
            "API suportada", "Somente as funções listadas abaixo pertencem à superfície de API específica do Server Script. TAGs devem ser dependências declaradas.",
            "Server Memory", "As funções específicas de Server Memory exigem uma dependência explícita ServerMemoryTag.",
            "Driver disponível", "Driver de comunicação registrado nesta compilação do EliteSCADA.",
            "Identidade do driver", "Type key", "Capacidades de Engineering", "Campos do Data Source", "Campos de binding do TAG",
            "Nenhuma capacidade opcional de Engineering declarada.", "Nenhum campo declarado neste schema.",
            "Teste de conexão", "Descoberta", "Browse", "Importação de arquivo", "Reconciliação", "Infraestrutura de transporte compartilhada");

        private static readonly Text English = new(
            "Authority", "Navigation", "Runtime", "Operation of the active application.",
            "The backend and the Active revision are the Runtime authority. The UI does not create a second process engine.",
            "Use Runtime navigation for operational overview and history according to the session's effective capabilities.",
            "History", "Local query of authorized historical data.",
            "Queries respect Runtime authority and the session's TAG visibility.",
            "Engineering", "Project configuration and lifecycle.",
            "Working, Revision, Published and Active remain distinct states. Saving does not automatically activate an application.",
            "Audit", "Traceability for security and engineering actions.",
            "Audit is distinct from Alarms and Operational Events and must not replace either domain.",
            "Licensing", "Product license state and features.",
            "Licensing limits commercial features without replacing authentication, authorization or Runtime authority.",
            "Gateway", "Runtime source and data-flow coordination.",
            "Gateway participates in Runtime composition; consumers use the TAG Engine/cache/events without private direct driver access.",
            "Memory source available in this build.",
            "The source is declared by the canonical product catalog and participates in the Data Source model without pretending to be a communication driver.",
            "Server Scripts", "Deterministic Python subset executed by the Runtime.",
            "Supported API", "Only the functions listed below belong to the Server Script-specific API surface. TAGs must be declared dependencies.",
            "Server Memory", "Server Memory-specific functions require an explicit ServerMemoryTag dependency.",
            "Available driver", "Communication driver registered in this EliteSCADA build.",
            "Driver identity", "Type key", "Engineering capabilities", "Data Source fields", "TAG binding fields",
            "No optional Engineering capabilities declared.", "No fields declared in this schema.",
            "Connection test", "Discovery", "Browse", "File import", "Reconcile", "Shared transport infrastructure");

        private static readonly Text Spanish = new(
            "Autoridad", "Navegación", "Runtime", "Operación de la aplicación activa.",
            "El backend y la revisión Active son la autoridad del Runtime. La interfaz no crea un segundo motor de proceso.",
            "Use la navegación del Runtime para la vista operacional y el histórico según las capacidades efectivas de la sesión.",
            "Histórico", "Consulta local de datos históricos autorizados.",
            "La consulta respeta la autoridad del Runtime y la visibilidad de TAGs de la sesión.",
            "Engineering", "Configuración y ciclo de vida del proyecto.",
            "Working, Revision, Published y Active siguen siendo estados distintos. Guardar no activa automáticamente una aplicación.",
            "Auditoría", "Trazabilidad de acciones de seguridad e ingeniería.",
            "Auditoría es distinta de Alarmas y Eventos Operacionales y no debe sustituir ninguno de esos dominios.",
            "Licenciamiento", "Estado y funciones de la licencia del producto.",
            "La licencia limita funciones comerciales sin sustituir autenticación, autorización ni autoridad del Runtime.",
            "Gateway", "Coordinación de fuentes y flujo de datos del Runtime.",
            "Gateway participa de la composición del Runtime; los consumidores usan TAG Engine/cache/eventos sin acceso directo privado a los drivers.",
            "Fuente de memoria disponible en esta compilación.",
            "La fuente es declarada por el catálogo canónico del producto y participa del modelo de Data Sources sin presentarse como driver de comunicación.",
            "Server Scripts", "Subconjunto determinístico de Python ejecutado por el Runtime.",
            "API soportada", "Solo las funciones listadas abajo pertenecen a la superficie específica de Server Script. Los TAGs deben ser dependencias declaradas.",
            "Server Memory", "Las funciones específicas de Server Memory requieren una dependencia explícita ServerMemoryTag.",
            "Driver disponible", "Driver de comunicación registrado en esta compilación de EliteSCADA.",
            "Identidad del driver", "Type key", "Capacidades de Engineering", "Campos del Data Source", "Campos de binding del TAG",
            "No hay capacidades opcionales de Engineering declaradas.", "No hay campos declarados en este schema.",
            "Prueba de conexión", "Descubrimiento", "Browse", "Importación de archivo", "Reconciliación", "Infraestructura de transporte compartida");
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
