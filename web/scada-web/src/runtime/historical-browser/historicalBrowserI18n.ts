export type HistoricalBrowserLocale = 'pt-BR' | 'en' | 'es';

export type HistoricalBrowserCopy = Readonly<{
  title: string; description: string; refresh: string; dataset: string; period: string;
  live: string; relative: string; absolute: string; customPeriod: string; quickRange: string; customAmount: string; unit: string;
  seconds: string; minutes: string; hours: string; days: string; relativePeriod: string; start: string; end: string; timezone: string; query: string;
  closeHistory: string; advancedFilters: string; advancedDiscovery: string; searchUnavailable: string; sortUnavailable: string; filterUnavailable: string;
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
    title: 'Histórico',
    description: 'Consulta somente leitura de valores, alarmes e eventos registrados.',
    refresh: 'Atualizar', dataset: 'Conjunto', period: 'Período', live: 'Ao vivo', relative: 'Período relativo', absolute: 'Período absoluto', customPeriod: 'Personalizado',
    quickRange: 'Atalho', customAmount: 'Quantidade', unit: 'Unidade', seconds: 'segundos', minutes: 'minutos', hours: 'horas', days: 'dias',
    relativePeriod: 'Período relativo', start: 'De', end: 'Até', timezone: 'Fuso horário', query: 'Consultar',
    closeHistory: 'Fechar histórico', advancedFilters: 'Filtros avançados', advancedDiscovery: 'Consulte primeiro para disponibilizar os campos deste conjunto de dados.', searchUnavailable: 'Este conjunto não oferece campos de texto pesquisáveis.', sortUnavailable: 'Este conjunto não oferece campos ordenáveis.', filterUnavailable: 'Este conjunto não oferece campos filtráveis.',
    historicalRecord: 'Registro histórico', readonlyNote: 'Contexto somente leitura. Comandos operacionais de alarme não estão disponíveis aqui.', loading: 'Carregando histórico…', unauthorized: 'Sem autorização para consultar este histórico.', queryFailed: 'Não foi possível consultar o histórico.', empty: 'Nenhum resultado para o conjunto e período selecionados.', idle: 'Escolha o conjunto e o período e consulte.',
    search: 'Buscar', searchPlaceholder: 'Buscar nos campos de texto disponíveis', searchDiscovery: 'Consulte primeiro para descobrir campos pesquisáveis', sortField: 'Ordenar por', serverDefault: 'Padrão do servidor', direction: 'Direção', descending: 'Decrescente', ascending: 'Crescente', applyQuery: 'Aplicar consulta', previousPage: 'Página anterior', nextPage: 'Próxima página', page: 'Página',
    filters: 'Filtros', filterField: 'Campo', discoverFilterFields: 'Consulte primeiro para descobrir campos filtráveis', operator: 'Condição', valueType: 'Tipo do valor', value: 'Valor', values: 'Valores', select: 'Selecione', commaSeparated: 'Valores separados por vírgula', addFilter: 'Adicionar filtro', clearFilters: 'Limpar filtros', remove: 'Remover', removeFilter: (index: number) => 'Remover filtro ' + index, invalidFilter: 'O filtro informado é inválido.',
    datasetHistorian: 'Valores de TAGs', datasetAlarms: 'Alarmes', datasetOperationalEvents: 'Eventos', last: 'Últimos', absoluteNotSelected: 'Período personalizado não selecionado', unknownDataset: 'Conjunto histórico desconhecido.',
    relativePositive: 'A quantidade do período deve ser um número inteiro positivo.', rangeTooLarge: 'O período não pode exceder 31 dias.', absoluteRequired: 'O período personalizado exige De e Até válidos.', absoluteOrder: 'De deve ser anterior a Até.', absoluteAmbiguous: 'Este horário local é ambíguo por mudança de fuso/DST. Escolha um horário não ambíguo.', unavailable: 'Indisponível', trueLabel: 'Verdadeiro', falseLabel: 'Falso'
  }),
  en: Object.freeze({
    title: 'History',
    description: 'Read-only query of recorded values, alarms, and events.',
    refresh: 'Refresh', dataset: 'Dataset', period: 'Period', live: 'Live', relative: 'Relative period', absolute: 'Absolute period', customPeriod: 'Custom',
    quickRange: 'Quick range', customAmount: 'Amount', unit: 'Unit', seconds: 'seconds', minutes: 'minutes', hours: 'hours', days: 'days',
    relativePeriod: 'Relative period', start: 'From', end: 'To', timezone: 'Time zone', query: 'Query',
    closeHistory: 'Close history', advancedFilters: 'Advanced filters', advancedDiscovery: 'Run a query first to make this dataset fields available.', searchUnavailable: 'This dataset does not expose searchable text fields.', sortUnavailable: 'This dataset does not expose sortable fields.', filterUnavailable: 'This dataset does not expose filterable fields.',
    historicalRecord: 'Historical record', readonlyNote: 'Read-only context. Operational alarm commands are not available here.', loading: 'Loading history…', unauthorized: 'You are not authorized to query this history.', queryFailed: 'History query failed.', empty: 'No results for the selected dataset and period.', idle: 'Choose a dataset and period, then query.',
    search: 'Search', searchPlaceholder: 'Search available text fields', searchDiscovery: 'Run a query first to discover searchable fields', sortField: 'Sort by', serverDefault: 'Server default', direction: 'Direction', descending: 'Descending', ascending: 'Ascending', applyQuery: 'Apply query', previousPage: 'Previous page', nextPage: 'Next page', page: 'Page',
    filters: 'Filters', filterField: 'Field', discoverFilterFields: 'Run a query first to discover filterable fields', operator: 'Condition', valueType: 'Value type', value: 'Value', values: 'Values', select: 'Select', commaSeparated: 'Comma-separated values', addFilter: 'Add filter', clearFilters: 'Clear filters', remove: 'Remove', removeFilter: (index: number) => 'Remove filter ' + index, invalidFilter: 'The filter is invalid.',
    datasetHistorian: 'TAG values', datasetAlarms: 'Alarms', datasetOperationalEvents: 'Events', last: 'Last', absoluteNotSelected: 'Custom period not selected', unknownDataset: 'Unknown historical dataset.',
    relativePositive: 'Period amount must be a positive whole number.', rangeTooLarge: 'The period cannot exceed 31 days.', absoluteRequired: 'Custom period requires valid From and To date/time values.', absoluteOrder: 'From must be before To.', absoluteAmbiguous: 'This local time is ambiguous because of a time-zone/DST transition. Choose an unambiguous time.', unavailable: 'Unavailable', trueLabel: 'True', falseLabel: 'False'
  }),
  es: Object.freeze({
    title: 'Histórico',
    description: 'Consulta de solo lectura de valores, alarmas y eventos registrados.',
    refresh: 'Actualizar', dataset: 'Conjunto', period: 'Período', live: 'En vivo', relative: 'Período relativo', absolute: 'Período absoluto', customPeriod: 'Personalizado',
    quickRange: 'Acceso rápido', customAmount: 'Cantidad', unit: 'Unidad', seconds: 'segundos', minutes: 'minutos', hours: 'horas', days: 'días',
    relativePeriod: 'Período relativo', start: 'Desde', end: 'Hasta', timezone: 'Zona horaria', query: 'Consultar',
    closeHistory: 'Cerrar histórico', advancedFilters: 'Filtros avanzados', advancedDiscovery: 'Consulte primero para habilitar los campos de este conjunto de datos.', searchUnavailable: 'Este conjunto no ofrece campos de texto buscables.', sortUnavailable: 'Este conjunto no ofrece campos ordenables.', filterUnavailable: 'Este conjunto no ofrece campos filtrables.',
    historicalRecord: 'Registro histórico', readonlyNote: 'Contexto de solo lectura. Los comandos operacionales de alarma no están disponibles aquí.', loading: 'Cargando histórico…', unauthorized: 'Sin autorización para consultar este histórico.', queryFailed: 'No fue posible consultar el histórico.', empty: 'No hay resultados para el conjunto y período seleccionados.', idle: 'Seleccione el conjunto y el período y consulte.',
    search: 'Buscar', searchPlaceholder: 'Buscar en los campos de texto disponibles', searchDiscovery: 'Consulte primero para descubrir campos buscables', sortField: 'Ordenar por', serverDefault: 'Predeterminado del servidor', direction: 'Dirección', descending: 'Descendente', ascending: 'Ascendente', applyQuery: 'Aplicar consulta', previousPage: 'Página anterior', nextPage: 'Página siguiente', page: 'Página',
    filters: 'Filtros', filterField: 'Campo', discoverFilterFields: 'Consulte primero para descubrir campos filtrables', operator: 'Condición', valueType: 'Tipo del valor', value: 'Valor', values: 'Valores', select: 'Seleccione', commaSeparated: 'Valores separados por comas', addFilter: 'Agregar filtro', clearFilters: 'Limpiar filtros', remove: 'Eliminar', removeFilter: (index: number) => 'Eliminar filtro ' + index, invalidFilter: 'El filtro no es válido.',
    datasetHistorian: 'Valores de TAGs', datasetAlarms: 'Alarmas', datasetOperationalEvents: 'Eventos', last: 'Últimos', absoluteNotSelected: 'Período personalizado no seleccionado', unknownDataset: 'Conjunto histórico desconocido.',
    relativePositive: 'La cantidad del período debe ser un número entero positivo.', rangeTooLarge: 'El período no puede superar 31 días.', absoluteRequired: 'El período personalizado requiere Desde y Hasta válidos.', absoluteOrder: 'Desde debe ser anterior a Hasta.', absoluteAmbiguous: 'Esta hora local es ambigua por una transición de zona horaria/DST. Elija una hora no ambigua.', unavailable: 'No disponible', trueLabel: 'Verdadero', falseLabel: 'Falso'
  })
});

const FIELD_SEGMENTS: Readonly<Record<HistoricalBrowserLocale, Readonly<Record<string, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({
    tag: 'TAG', alarm: 'Alarme', event: 'Evento', id: 'ID', key: 'Chave', name: 'Nome',
    timestamp: 'Data/hora', value: 'Valor', quality: 'Qualidade', state: 'Estado', severity: 'Severidade',
    priority: 'Prioridade', message: 'Mensagem', source: 'Origem', area: 'Área', category: 'Categoria',
    actor: 'Responsável', equipment: 'Equipamento', subcondition: 'Subcondição', type: 'Tipo'
  }),
  en: Object.freeze({
    tag: 'TAG', alarm: 'Alarm', event: 'Event', id: 'ID', key: 'Key', name: 'Name',
    timestamp: 'Timestamp', value: 'Value', quality: 'Quality', state: 'State', severity: 'Severity',
    priority: 'Priority', message: 'Message', source: 'Source', area: 'Area', category: 'Category',
    actor: 'Actor', equipment: 'Equipment', subcondition: 'Subcondition', type: 'Type'
  }),
  es: Object.freeze({
    tag: 'TAG', alarm: 'Alarma', event: 'Evento', id: 'ID', key: 'Clave', name: 'Nombre',
    timestamp: 'Fecha/hora', value: 'Valor', quality: 'Calidad', state: 'Estado', severity: 'Severidad',
    priority: 'Prioridad', message: 'Mensaje', source: 'Origen', area: 'Área', category: 'Categoría',
    actor: 'Responsable', equipment: 'Equipo', subcondition: 'Subcondición', type: 'Tipo'
  })
});

const OPERATOR_LABELS: Readonly<Record<HistoricalBrowserLocale, Readonly<Record<string, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({ eq: 'é igual a', notEq: 'é diferente de', in: 'está em', contains: 'contém', startsWith: 'começa com', gt: 'maior que', gte: 'maior ou igual a', lt: 'menor que', lte: 'menor ou igual a' }),
  en: Object.freeze({ eq: 'equals', notEq: 'does not equal', in: 'is in', contains: 'contains', startsWith: 'starts with', gt: 'greater than', gte: 'greater than or equal to', lt: 'less than', lte: 'less than or equal to' }),
  es: Object.freeze({ eq: 'es igual a', notEq: 'es diferente de', in: 'está en', contains: 'contiene', startsWith: 'comienza con', gt: 'mayor que', gte: 'mayor o igual que', lt: 'menor que', lte: 'menor o igual que' })
});

const SCALAR_LABELS: Readonly<Record<HistoricalBrowserLocale, Readonly<Record<string, string>>>> = Object.freeze({
  'pt-BR': Object.freeze({ string: 'Texto', enum: 'Lista', int16: 'Inteiro 16 bits', int32: 'Inteiro 32 bits', int64: 'Inteiro 64 bits', float: 'Número decimal', double: 'Número decimal (dupla precisão)', number: 'Número', boolean: 'Verdadeiro/Falso', dateTime: 'Data/hora' }),
  en: Object.freeze({ string: 'Text', enum: 'List', int16: '16-bit integer', int32: '32-bit integer', int64: '64-bit integer', float: 'Decimal number', double: 'Decimal number (double precision)', number: 'Number', boolean: 'True/False', dateTime: 'Date/time' }),
  es: Object.freeze({ string: 'Texto', enum: 'Lista', int16: 'Entero de 16 bits', int32: 'Entero de 32 bits', int64: 'Entero de 64 bits', float: 'Número decimal', double: 'Número decimal (doble precisión)', number: 'Número', boolean: 'Verdadero/Falso', dateTime: 'Fecha/hora' })
});

export function historicalBrowserCopy(locale: HistoricalBrowserLocale): HistoricalBrowserCopy {
  return COPY[locale];
}

export function historicalFieldLabel(field: string, locale: HistoricalBrowserLocale = 'en'): string {
  return field
    .split('.')
    .map(part => FIELD_SEGMENTS[locale][part] ?? humanizeFieldPart(part))
    .join(' / ');
}

export function historicalOperatorLabel(operator: string, locale: HistoricalBrowserLocale = 'en'): string {
  return OPERATOR_LABELS[locale][operator] ?? humanizeFieldPart(operator);
}

export function historicalScalarKindLabel(kind: string, locale: HistoricalBrowserLocale = 'en'): string {
  return SCALAR_LABELS[locale][kind] ?? humanizeFieldPart(kind);
}

function humanizeFieldPart(value: string): string {
  const spaced = value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ').trim();
  if (!spaced) return value;
  return spaced.replace(/^./, first => first.toUpperCase());
}
