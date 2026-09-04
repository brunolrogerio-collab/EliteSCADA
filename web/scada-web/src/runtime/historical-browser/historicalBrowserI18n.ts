export type HistoricalBrowserLocale = 'pt-BR' | 'en' | 'es';

export type HistoricalBrowserCopy = Readonly<{
  title: string;
  description: string;
  refresh: string;
  dataset: string;
  period: string;
  relative: string;
  absolute: string;
  relativePeriod: string;
  start: string;
  end: string;
  query: string;
  historicalRecord: string;
  readonlyNote: string;
  loading: string;
  unauthorized: string;
  queryFailed: string;
  empty: string;
  idle: string;
  search: string;
  searchPlaceholder: string;
  searchDiscovery: string;
  sortField: string;
  serverDefault: string;
  direction: string;
  descending: string;
  ascending: string;
  applyQuery: string;
  previousPage: string;
  nextPage: string;
  page: string;
  filters: string;
  filterField: string;
  discoverFilterFields: string;
  operator: string;
  valueType: string;
  value: string;
  values: string;
  select: string;
  commaSeparated: string;
  addFilter: string;
  clearFilters: string;
  remove: string;
  removeFilter: (index: number) => string;
  invalidFilter: string;
  datasetHistorian: string;
  datasetAlarms: string;
  datasetOperationalEvents: string;
  last: string;
  absoluteNotSelected: string;
  unknownDataset: string;
  relativePositive: string;
  absoluteRequired: string;
  absoluteOrder: string;
  unavailable: string;
  trueLabel: string;
  falseLabel: string;
}>;

const COPY: Readonly<Record<HistoricalBrowserLocale, HistoricalBrowserCopy>> = Object.freeze({
  'pt-BR': Object.freeze({
    title: 'Browser de dados históricos',
    description: 'Exploração somente leitura de amostras do historian, eventos de alarme e eventos operacionais persistidos.',
    refresh: 'Atualizar', dataset: 'Conjunto de dados', period: 'Período', relative: 'Relativo', absolute: 'Absoluto', relativePeriod: 'Período relativo', start: 'Início', end: 'Fim', query: 'Consultar',
    historicalRecord: 'Registro histórico', readonlyNote: 'Contexto somente leitura. Comandos operacionais de alarme não estão disponíveis aqui.', loading: 'Carregando dados históricos…', unauthorized: 'Sem autorização para consultar este conjunto de dados históricos.', queryFailed: 'Falha na consulta histórica.', empty: 'Nenhum registro histórico corresponde à visualização atual.', idle: 'Escolha um conjunto de dados e um período e execute a consulta.',
    search: 'Buscar', searchPlaceholder: 'Buscar nos campos de texto históricos permitidos', searchDiscovery: 'Execute uma consulta para descobrir campos pesquisáveis', sortField: 'Campo de ordenação', serverDefault: 'Padrão do servidor', direction: 'Direção', descending: 'Decrescente', ascending: 'Crescente', applyQuery: 'Aplicar consulta', previousPage: 'Página anterior', nextPage: 'Próxima página', page: 'Página',
    filters: 'Filtros históricos', filterField: 'Campo do filtro', discoverFilterFields: 'Execute uma consulta para descobrir campos filtráveis', operator: 'Operador', valueType: 'Tipo do valor', value: 'Valor', values: 'Valores', select: 'Selecione', commaSeparated: 'Valores separados por vírgula', addFilter: 'Adicionar filtro', clearFilters: 'Limpar filtros', remove: 'Remover', removeFilter: index => `Remover filtro histórico ${index}`, invalidFilter: 'O filtro histórico é inválido.',
    datasetHistorian: 'Amostras do historian', datasetAlarms: 'Eventos de alarme', datasetOperationalEvents: 'Eventos operacionais', last: 'Últimos', absoluteNotSelected: 'Período absoluto não selecionado', unknownDataset: 'Conjunto de dados histórico desconhecido.', relativePositive: 'O período relativo deve ser um número inteiro positivo de segundos.', absoluteRequired: 'O período absoluto exige início e fim válidos.', absoluteOrder: 'O início do período absoluto deve ser anterior ao fim.', unavailable: 'Indisponível', trueLabel: 'Verdadeiro', falseLabel: 'Falso'
  }),
  en: Object.freeze({
    title: 'Historical Data Browser',
    description: 'Read-only exploration of persisted historian samples, alarm events, and operational events.',
    refresh: 'Refresh', dataset: 'Dataset', period: 'Period', relative: 'Relative', absolute: 'Absolute', relativePeriod: 'Relative period', start: 'Start', end: 'End', query: 'Query',
    historicalRecord: 'Historical record', readonlyNote: 'Read-only context. Operational alarm commands are not available here.', loading: 'Loading historical data…', unauthorized: 'Not authorized to query this historical dataset.', queryFailed: 'Historical query failed.', empty: 'No historical records matched the current view.', idle: 'Choose a dataset and period, then run a query.',
    search: 'Search', searchPlaceholder: 'Search allowlisted historical text fields', searchDiscovery: 'Run a query to discover searchable fields', sortField: 'Sort field', serverDefault: 'Server default', direction: 'Direction', descending: 'Descending', ascending: 'Ascending', applyQuery: 'Apply query', previousPage: 'Previous page', nextPage: 'Next page', page: 'Page',
    filters: 'Historical filters', filterField: 'Filter field', discoverFilterFields: 'Run a query to discover filterable fields', operator: 'Operator', valueType: 'Value type', value: 'Value', values: 'Values', select: 'Select', commaSeparated: 'Comma-separated values', addFilter: 'Add filter', clearFilters: 'Clear filters', remove: 'Remove', removeFilter: index => `Remove historical filter ${index}`, invalidFilter: 'Historical filter is invalid.',
    datasetHistorian: 'Historian samples', datasetAlarms: 'Alarm events', datasetOperationalEvents: 'Operational events', last: 'Last', absoluteNotSelected: 'Absolute period not selected', unknownDataset: 'Unknown historical dataset.', relativePositive: 'Relative period must be a positive whole number of seconds.', absoluteRequired: 'Absolute period requires valid start and end date/time values.', absoluteOrder: 'Absolute period start must be before end.', unavailable: 'Unavailable', trueLabel: 'True', falseLabel: 'False'
  }),
  es: Object.freeze({
    title: 'Browser de datos históricos',
    description: 'Exploración de solo lectura de muestras del historian, eventos de alarma y eventos operacionales persistidos.',
    refresh: 'Actualizar', dataset: 'Conjunto de datos', period: 'Período', relative: 'Relativo', absolute: 'Absoluto', relativePeriod: 'Período relativo', start: 'Inicio', end: 'Fin', query: 'Consultar',
    historicalRecord: 'Registro histórico', readonlyNote: 'Contexto de solo lectura. Los comandos operacionales de alarma no están disponibles aquí.', loading: 'Cargando datos históricos…', unauthorized: 'Sin autorización para consultar este conjunto de datos históricos.', queryFailed: 'Falló la consulta histórica.', empty: 'Ningún registro histórico coincide con la vista actual.', idle: 'Seleccione un conjunto de datos y un período y ejecute la consulta.',
    search: 'Buscar', searchPlaceholder: 'Buscar en los campos de texto históricos permitidos', searchDiscovery: 'Ejecute una consulta para descubrir campos buscables', sortField: 'Campo de ordenación', serverDefault: 'Predeterminado del servidor', direction: 'Dirección', descending: 'Descendente', ascending: 'Ascendente', applyQuery: 'Aplicar consulta', previousPage: 'Página anterior', nextPage: 'Página siguiente', page: 'Página',
    filters: 'Filtros históricos', filterField: 'Campo del filtro', discoverFilterFields: 'Ejecute una consulta para descubrir campos filtrables', operator: 'Operador', valueType: 'Tipo del valor', value: 'Valor', values: 'Valores', select: 'Seleccione', commaSeparated: 'Valores separados por comas', addFilter: 'Agregar filtro', clearFilters: 'Limpiar filtros', remove: 'Eliminar', removeFilter: index => `Eliminar filtro histórico ${index}`, invalidFilter: 'El filtro histórico no es válido.',
    datasetHistorian: 'Muestras del historian', datasetAlarms: 'Eventos de alarma', datasetOperationalEvents: 'Eventos operacionales', last: 'Últimos', absoluteNotSelected: 'Período absoluto no seleccionado', unknownDataset: 'Conjunto de datos histórico desconocido.', relativePositive: 'El período relativo debe ser un número entero positivo de segundos.', absoluteRequired: 'El período absoluto requiere inicio y fin válidos.', absoluteOrder: 'El inicio del período absoluto debe ser anterior al fin.', unavailable: 'No disponible', trueLabel: 'Verdadero', falseLabel: 'Falso'
  })
});

export function historicalBrowserCopy(locale: HistoricalBrowserLocale): HistoricalBrowserCopy {
  return COPY[locale];
}
