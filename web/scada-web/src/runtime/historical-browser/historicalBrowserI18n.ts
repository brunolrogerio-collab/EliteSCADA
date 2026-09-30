export type HistoricalBrowserLocale = 'pt-BR' | 'en' | 'es';

export type HistoricalBrowserCopy = Readonly<{
  title: string; description: string; refresh: string; dataset: string; period: string;
  live: string; relative: string; absolute: string; quickRange: string; customAmount: string; unit: string;
  seconds: string; minutes: string; hours: string; days: string; relativePeriod: string; start: string; end: string; timezone: string; query: string;
  historicalRecord: string; readonlyNote: string; loading: string; unauthorized: string; queryFailed: string; empty: string; idle: string;
  search: string; searchPlaceholder: string; searchDiscovery: string; sortField: string; serverDefault: string; direction: string;
  descending: string; ascending: string; applyQuery: string; previousPage: string; nextPage: string; page: string;
  filters: string; filterField: string; discoverFilterFields: string; operator: string; valueType: string; value: string; values: string;
  select: string; commaSeparated: string; addFilter: string; clearFilters: string; remove: string; removeFilter: (index: number) => string; invalidFilter: string;
  datasetHistorian: string; datasetAlarms: string; datasetOperationalEvents: string; last: string; absoluteNotSelected: string; unknownDataset: string;
  relativePositive: string; rangeTooLarge: string; absoluteRequired: string; absoluteOrder: string; absoluteAmbiguous: string;
  unavailable: string; trueLabel: string; falseLabel: string;
}>;

const COPY: Readonly<Record<HistoricalBrowserLocale, HistoricalBrowserCopy>> = Object.freeze({
  'pt-BR': Object.freeze({
    title: 'Browser de dados históricos',
    description: 'Exploração somente leitura de amostras do historian, eventos de alarme e eventos operacionais persistidos.',
    refresh: 'Atualizar', dataset: 'Conjunto de dados', period: 'Período', live: 'Ao vivo', relative: 'Período relativo', absolute: 'Período absoluto',
    quickRange: 'Atalho', customAmount: 'Quantidade', unit: 'Unidade', seconds: 'segundos', minutes: 'minutos', hours: 'horas', days: 'dias',
    relativePeriod: 'Período relativo', start: 'De', end: 'Até', timezone: 'Fuso horário', query: 'Consultar',
    historicalRecord: 'Registro histórico', readonlyNote: 'Contexto somente leitura. Comandos operacionais de alarme não estão disponíveis aqui.', loading: 'Carregando dados históricos…', unauthorized: 'Sem autorização para consultar este conjunto de dados históricos.', queryFailed: 'Falha na consulta histórica.', empty: 'Nenhum registro histórico corresponde à visualização atual.', idle: 'Escolha um conjunto de dados e um período e execute a consulta.',
    search: 'Buscar', searchPlaceholder: 'Buscar nos campos de texto históricos permitidos', searchDiscovery: 'Execute uma consulta para descobrir campos pesquisáveis', sortField: 'Campo de ordenação', serverDefault: 'Padrão do servidor', direction: 'Direção', descending: 'Decrescente', ascending: 'Crescente', applyQuery: 'Aplicar consulta', previousPage: 'Página anterior', nextPage: 'Próxima página', page: 'Página',
    filters: 'Filtros históricos', filterField: 'Campo do filtro', discoverFilterFields: 'Execute uma consulta para descobrir campos filtráveis', operator: 'Operador', valueType: 'Tipo do valor', value: 'Valor', values: 'Valores', select: 'Selecione', commaSeparated: 'Valores separados por vírgula', addFilter: 'Adicionar filtro', clearFilters: 'Limpar filtros', remove: 'Remover', removeFilter: (index: number) => 'Remover filtro histórico ' + index, invalidFilter: 'O filtro histórico é inválido.',
    datasetHistorian: 'Amostras do historian', datasetAlarms: 'Eventos de alarme', datasetOperationalEvents: 'Eventos operacionais', last: 'Últimos', absoluteNotSelected: 'Período absoluto não selecionado', unknownDataset: 'Conjunto de dados histórico desconhecido.',
    relativePositive: 'A quantidade do período deve ser um número inteiro positivo.', rangeTooLarge: 'O período não pode exceder 31 dias.', absoluteRequired: 'O período absoluto exige De e Até válidos.', absoluteOrder: 'De deve ser anterior a Até.', absoluteAmbiguous: 'Este horário local é ambíguo por mudança de fuso/DST. Escolha um horário não ambíguo.', unavailable: 'Indisponível', trueLabel: 'Verdadeiro', falseLabel: 'Falso'
  }),
  en: Object.freeze({
    title: 'Historical Data Browser',
    description: 'Read-only exploration of persisted historian samples, alarm events, and operational events.',
    refresh: 'Refresh', dataset: 'Dataset', period: 'Period', live: 'Live', relative: 'Relative period', absolute: 'Absolute period',
    quickRange: 'Quick range', customAmount: 'Amount', unit: 'Unit', seconds: 'seconds', minutes: 'minutes', hours: 'hours', days: 'days',
    relativePeriod: 'Relative period', start: 'From', end: 'To', timezone: 'Time zone', query: 'Query',
    historicalRecord: 'Historical record', readonlyNote: 'Read-only context. Operational alarm commands are not available here.', loading: 'Loading historical data…', unauthorized: 'Not authorized to query this historical dataset.', queryFailed: 'Historical query failed.', empty: 'No historical records matched the current view.', idle: 'Choose a dataset and period, then run a query.',
    search: 'Search', searchPlaceholder: 'Search allowlisted historical text fields', searchDiscovery: 'Run a query to discover searchable fields', sortField: 'Sort field', serverDefault: 'Server default', direction: 'Direction', descending: 'Descending', ascending: 'Ascending', applyQuery: 'Apply query', previousPage: 'Previous page', nextPage: 'Next page', page: 'Page',
    filters: 'Historical filters', filterField: 'Filter field', discoverFilterFields: 'Run a query to discover filterable fields', operator: 'Operator', valueType: 'Value type', value: 'Value', values: 'Values', select: 'Select', commaSeparated: 'Comma-separated values', addFilter: 'Add filter', clearFilters: 'Clear filters', remove: 'Remove', removeFilter: (index: number) => 'Remove historical filter ' + index, invalidFilter: 'Historical filter is invalid.',
    datasetHistorian: 'Historian samples', datasetAlarms: 'Alarm events', datasetOperationalEvents: 'Operational events', last: 'Last', absoluteNotSelected: 'Absolute period not selected', unknownDataset: 'Unknown historical dataset.',
    relativePositive: 'Period amount must be a positive whole number.', rangeTooLarge: 'The period cannot exceed 31 days.', absoluteRequired: 'Absolute period requires valid From and To date/time values.', absoluteOrder: 'From must be before To.', absoluteAmbiguous: 'This local time is ambiguous because of a time-zone/DST transition. Choose an unambiguous time.', unavailable: 'Unavailable', trueLabel: 'True', falseLabel: 'False'
  }),
  es: Object.freeze({
    title: 'Browser de datos históricos',
    description: 'Exploración de solo lectura de muestras del historian, eventos de alarma y eventos operacionales persistidos.',
    refresh: 'Actualizar', dataset: 'Conjunto de datos', period: 'Período', live: 'En vivo', relative: 'Período relativo', absolute: 'Período absoluto',
    quickRange: 'Acceso rápido', customAmount: 'Cantidad', unit: 'Unidad', seconds: 'segundos', minutes: 'minutos', hours: 'horas', days: 'días',
    relativePeriod: 'Período relativo', start: 'Desde', end: 'Hasta', timezone: 'Zona horaria', query: 'Consultar',
    historicalRecord: 'Registro histórico', readonlyNote: 'Contexto de solo lectura. Los comandos operacionales de alarma no están disponibles aquí.', loading: 'Cargando datos históricos…', unauthorized: 'Sin autorización para consultar este conjunto de datos históricos.', queryFailed: 'Falló la consulta histórica.', empty: 'Ningún registro histórico coincide con la vista actual.', idle: 'Seleccione un conjunto de datos y un período y ejecute la consulta.',
    search: 'Buscar', searchPlaceholder: 'Buscar en los campos de texto históricos permitidos', searchDiscovery: 'Ejecute una consulta para descubrir campos buscables', sortField: 'Campo de ordenación', serverDefault: 'Predeterminado del servidor', direction: 'Dirección', descending: 'Descendente', ascending: 'Ascendente', applyQuery: 'Aplicar consulta', previousPage: 'Página anterior', nextPage: 'Página siguiente', page: 'Página',
    filters: 'Filtros históricos', filterField: 'Campo del filtro', discoverFilterFields: 'Ejecute una consulta para descubrir campos filtrables', operator: 'Operador', valueType: 'Tipo del valor', value: 'Valor', values: 'Valores', select: 'Seleccione', commaSeparated: 'Valores separados por comas', addFilter: 'Agregar filtro', clearFilters: 'Limpiar filtros', remove: 'Eliminar', removeFilter: (index: number) => 'Eliminar filtro histórico ' + index, invalidFilter: 'El filtro histórico no es válido.',
    datasetHistorian: 'Muestras del historian', datasetAlarms: 'Eventos de alarma', datasetOperationalEvents: 'Eventos operacionales', last: 'Últimos', absoluteNotSelected: 'Período absoluto no seleccionado', unknownDataset: 'Conjunto de datos histórico desconocido.',
    relativePositive: 'La cantidad del período debe ser un número entero positivo.', rangeTooLarge: 'El período no puede superar 31 días.', absoluteRequired: 'El período absoluto requiere Desde y Hasta válidos.', absoluteOrder: 'Desde debe ser anterior a Hasta.', absoluteAmbiguous: 'Esta hora local es ambigua por una transición de zona horaria/DST. Elija una hora no ambigua.', unavailable: 'No disponible', trueLabel: 'Verdadero', falseLabel: 'Falso'
  })
});

export function historicalBrowserCopy(locale: HistoricalBrowserLocale): HistoricalBrowserCopy {
  return COPY[locale];
}
