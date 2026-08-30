export type HistoricalValueKind =
  | 'guid'
  | 'string'
  | 'enum'
  | 'int16'
  | 'int32'
  | 'int64'
  | 'float'
  | 'double'
  | 'number'
  | 'boolean'
  | 'dateTime'
  | 'null';

export type HistoricalQueryValue = Readonly<{
  kind: HistoricalValueKind;
  value: string | null;
}>;

export type HistoricalTimeRange = Readonly<{
  kind: 'absolute' | 'relative';
  fromUtc?: string | null;
  toUtc?: string | null;
  durationSeconds?: number | null;
  anchor?: 'now' | null;
}>;

export type HistoricalFilter = Readonly<{
  field: string;
  operator: 'eq' | 'notEq' | 'in' | 'contains' | 'startsWith' | 'gt' | 'gte' | 'lt' | 'lte';
  values: readonly HistoricalQueryValue[];
}>;

export type HistoricalSort = Readonly<{
  field: string;
  direction: 'ascending' | 'descending';
}>;

export type HistoricalPageRequest = Readonly<{
  limit: number;
  cursor?: string | null;
}>;

export type HistoricalQueryRequest = Readonly<{
  version: 1;
  datasetKey: 'historian.samples' | 'alarm.events' | string;
  timeRange: HistoricalTimeRange;
  filters?: readonly HistoricalFilter[] | null;
  search?: string | null;
  orderBy?: readonly HistoricalSort[] | null;
  page?: HistoricalPageRequest | null;
}>;

export type HistoricalColumn = Readonly<{
  field: string;
  type: 'guid' | 'string' | 'enum' | 'number' | 'boolean' | 'dateTime' | 'int64' | 'scalar';
  filterable: boolean;
  sortable: boolean;
  searchable: boolean;
}>;

export type HistoricalQueryRow = Readonly<{
  cells: Readonly<Record<string, HistoricalQueryValue>>;
}>;

export type ReportPageOrientation = 'portrait' | 'landscape';
export type ReportSectionKind =
  | 'reportHeader'
  | 'reportFooter'
  | 'pageHeader'
  | 'pageFooter'
  | 'groupHeader'
  | 'detail'
  | 'groupFooter';
export type ReportControlKind =
  | 'label'
  | 'dataField'
  | 'booleanState'
  | 'image'
  | 'barcode'
  | 'chart'
  | 'line'
  | 'rectangle'
  | 'roundedRectangle'
  | 'ellipse'
  | 'pageBreak';
export type ReportTextAlignment = 'left' | 'center' | 'right';
export type ReportParameterType =
  | 'string'
  | 'boolean'
  | 'number'
  | 'int64'
  | 'dateTime'
  | 'durationSeconds'
  | 'guid'
  | 'enum';
export type ReportQueryParameterTarget =
  | 'absoluteFromUtc'
  | 'absoluteToUtc'
  | 'relativeDurationSeconds'
  | 'search'
  | 'filterValue';

export type ReportParameterValue = Readonly<{
  type: ReportParameterType;
  value: string;
}>;

export type ReportParameterEngineeringDto = Readonly<{
  key: string;
  name: string;
  type: ReportParameterType;
  defaultValue: ReportParameterValue;
  description?: string | null;
  allowedValues?: readonly ReportParameterValue[] | null;
}>;

export type ReportQueryParameterBindingEngineeringDto = Readonly<{
  parameterKey: string;
  target: ReportQueryParameterTarget;
  filterIndex?: number | null;
  valueIndex?: number | null;
}>;

export type ReportQueryEngineeringDto = Readonly<{
  key: string;
  query: HistoricalQueryRequest;
  parameterBindings?: readonly ReportQueryParameterBindingEngineeringDto[] | null;
}>;

export type ReportPageEngineeringDto = Readonly<{
  paperSizeKey?: string;
  orientation?: ReportPageOrientation;
  marginTopMillimeters?: number;
  marginRightMillimeters?: number;
  marginBottomMillimeters?: number;
  marginLeftMillimeters?: number;
  showPageNumbers?: boolean;
}>;

export type ReportControlStyleEngineeringDto = Readonly<{
  fontFamily?: string | null;
  fontSizePoints?: number | null;
  bold?: boolean;
  italic?: boolean;
  textAlignment?: ReportTextAlignment;
  foreground?: string | null;
  background?: string | null;
  borderWidth?: number | null;
}>;

export type ReportControlEngineeringDto = Readonly<{
  id?: string | null;
  key: string;
  kind: ReportControlKind;
  xMillimeters: number;
  yMillimeters: number;
  widthMillimeters: number;
  heightMillimeters: number;
  text?: string | null;
  queryKey?: string | null;
  field?: string | null;
  assetId?: string | null;
  style?: ReportControlStyleEngineeringDto | null;
  metadata?: Record<string, string> | null;
}>;

export type ReportSectionEngineeringDto = Readonly<{
  id?: string | null;
  key: string;
  kind: ReportSectionKind;
  heightMillimeters: number;
  queryKey?: string | null;
  groupKey?: string | null;
  repeatOnNewPage?: boolean;
  controls?: readonly ReportControlEngineeringDto[] | null;
}>;

export type ReportGroupEngineeringDto = Readonly<{
  key: string;
  queryKey: string;
  field: string;
  direction?: 'ascending' | 'descending';
}>;

export type ReportAggregateEngineeringDto = Readonly<{
  key: string;
  queryKey: string;
  function: 'count' | 'sum' | 'average' | 'minimum' | 'maximum' | 'first' | 'last';
  field?: string | null;
  groupKey?: string | null;
}>;

export type ReportEngineeringDto = Readonly<{
  id?: string | null;
  key: string;
  name: string;
  description?: string | null;
  category?: string | null;
  page?: ReportPageEngineeringDto | null;
  parameters?: readonly ReportParameterEngineeringDto[] | null;
  queries?: readonly ReportQueryEngineeringDto[] | null;
  sections?: readonly ReportSectionEngineeringDto[] | null;
  groups?: readonly ReportGroupEngineeringDto[] | null;
  aggregates?: readonly ReportAggregateEngineeringDto[] | null;
  metadata?: Record<string, string> | null;
}>;

export type ReportQueryExecutionResult = Readonly<{
  queryKey: string;
  dataset: string;
  columns: readonly HistoricalColumn[];
  rows: readonly HistoricalQueryRow[];
  fromUtc: string;
  toUtc: string;
}>;

export type ReportExecutionResult = Readonly<{
  reportId?: string | null;
  reportKey: string;
  parameters: Readonly<Record<string, ReportParameterValue>>;
  queries: readonly ReportQueryExecutionResult[];
}>;

export type ReportExecutionRequest = Readonly<{
  report: ReportEngineeringDto;
  parameters?: Readonly<Record<string, ReportParameterValue>> | null;
}>;
