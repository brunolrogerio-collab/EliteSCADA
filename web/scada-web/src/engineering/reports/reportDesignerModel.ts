import type { EngineeringPackageView } from '../types';
import type {
  HistoricalQueryRow,
  HistoricalQueryValue,
  ReportControlEngineeringDto,
  ReportControlKind,
  ReportEngineeringDto,
  ReportExecutionResult,
  ReportQueryEngineeringDto,
  ReportSectionEngineeringDto
} from './reportContracts';

export const NEW_REPORT_IDENTITY = '__new-report__';
export const DEFAULT_REPORT_QUERY_KEY = 'main';
export const DEFAULT_REPORT_DURATION_SECONDS = 3600;

export type EngineeringPackageWithReports = EngineeringPackageView & {
  reports?: readonly ReportEngineeringDto[] | null;
};

export function reportCollection(engineeringPackage: EngineeringPackageView): readonly ReportEngineeringDto[] {
  const reports = (engineeringPackage as EngineeringPackageWithReports).reports;
  return Array.isArray(reports) ? reports : [];
}

export function reportIdentity(report: ReportEngineeringDto): string {
  return report.id ? `id:${report.id}` : `key:${report.key}`;
}

export function matchesReportIdentity(report: ReportEngineeringDto, identity: string): boolean {
  return reportIdentity(report) === identity;
}

export function cloneReport(report: ReportEngineeringDto): ReportEngineeringDto {
  return JSON.parse(JSON.stringify(report)) as ReportEngineeringDto;
}

export function createReportDraft(existingReports: readonly ReportEngineeringDto[]): ReportEngineeringDto {
  const nextNumber = nextAvailableNumber(existingReports.map(report => report.key), 'report-');
  const key = `report-${nextNumber}`;

  return {
    key,
    name: `Report ${nextNumber}`,
    description: 'Operational historian report',
    page: {
      paperSizeKey: 'A4',
      orientation: 'portrait',
      marginTopMillimeters: 10,
      marginRightMillimeters: 10,
      marginBottomMillimeters: 10,
      marginLeftMillimeters: 10,
      showPageNumbers: true
    },
    parameters: [
      {
        key: 'periodSeconds',
        name: 'Period (seconds)',
        type: 'durationSeconds',
        defaultValue: { type: 'durationSeconds', value: String(DEFAULT_REPORT_DURATION_SECONDS) }
      }
    ],
    queries: [createHistoricalQuery(DEFAULT_REPORT_QUERY_KEY, 'historian.samples')],
    sections: [
      {
        key: 'report-header',
        kind: 'reportHeader',
        heightMillimeters: 20,
        controls: [
          {
            key: 'title',
            kind: 'label',
            xMillimeters: 0,
            yMillimeters: 2,
            widthMillimeters: 170,
            heightMillimeters: 10,
            text: 'Operational Report',
            style: { fontSizePoints: 16, bold: true, textAlignment: 'left' }
          }
        ]
      },
      {
        key: 'detail',
        kind: 'detail',
        heightMillimeters: 9,
        queryKey: DEFAULT_REPORT_QUERY_KEY,
        controls: [
          {
            key: 'value',
            kind: 'dataField',
            xMillimeters: 0,
            yMillimeters: 1,
            widthMillimeters: 80,
            heightMillimeters: 6,
            queryKey: DEFAULT_REPORT_QUERY_KEY,
            field: 'value'
          },
          {
            key: 'timestamp',
            kind: 'dataField',
            xMillimeters: 90,
            yMillimeters: 1,
            widthMillimeters: 80,
            heightMillimeters: 6,
            queryKey: DEFAULT_REPORT_QUERY_KEY,
            field: 'timestamp'
          }
        ]
      },
      {
        key: 'report-footer',
        kind: 'reportFooter',
        heightMillimeters: 12,
        controls: [
          {
            key: 'footer-label',
            kind: 'label',
            xMillimeters: 0,
            yMillimeters: 2,
            widthMillimeters: 170,
            heightMillimeters: 6,
            text: 'Generated from canonical Report Engineering'
          }
        ]
      }
    ],
    groups: [],
    aggregates: []
  };
}

export function createHistoricalQuery(
  key: string,
  datasetKey: 'historian.samples' | 'alarm.events'
): ReportQueryEngineeringDto {
  return {
    key,
    query: {
      version: 1,
      datasetKey,
      timeRange: {
        kind: 'relative',
        durationSeconds: DEFAULT_REPORT_DURATION_SECONDS,
        anchor: 'now'
      },
      filters: [],
      orderBy: [{ field: 'timestamp', direction: 'descending' }],
      page: { limit: 50 }
    },
    parameterBindings: [
      {
        parameterKey: 'periodSeconds',
        target: 'relativeDurationSeconds'
      }
    ]
  };
}

export function replaceReportInPackage(
  engineeringPackage: EngineeringPackageView,
  selected: ReportEngineeringDto | null,
  draft: ReportEngineeringDto
): EngineeringPackageView {
  const reports = reportCollection(engineeringPackage);
  const selectedIdentity = selected ? reportIdentity(selected) : null;
  let replaced = false;
  const nextReports = reports.map(report => {
    if (selectedIdentity && reportIdentity(report) === selectedIdentity) {
      replaced = true;
      return cloneReport(draft);
    }
    return report;
  });
  if (!replaced) nextReports.push(cloneReport(draft));
  return { ...engineeringPackage, reports: nextReports };
}

export function updateReportSection(
  report: ReportEngineeringDto,
  sectionKey: string,
  update: (section: ReportSectionEngineeringDto) => ReportSectionEngineeringDto
): ReportEngineeringDto {
  return {
    ...report,
    sections: (report.sections ?? []).map(section => section.key === sectionKey ? update(section) : section)
  };
}

export function updateReportControl(
  report: ReportEngineeringDto,
  sectionKey: string,
  controlKey: string,
  update: (control: ReportControlEngineeringDto) => ReportControlEngineeringDto
): ReportEngineeringDto {
  return updateReportSection(report, sectionKey, section => ({
    ...section,
    controls: (section.controls ?? []).map(control => control.key === controlKey ? update(control) : control)
  }));
}

export function addReportControl(
  report: ReportEngineeringDto,
  sectionKey: string,
  kind: Extract<ReportControlKind, 'label' | 'dataField'>
): ReportEngineeringDto {
  const query = report.queries?.[0];
  const base = kind === 'label' ? 'label' : 'field';
  const existingKeys = (report.sections ?? []).flatMap(section => (section.controls ?? []).map(control => control.key));
  const key = `${base}-${nextAvailableNumber(existingKeys, `${base}-`)}`;
  const field = defaultFieldForDataset(query?.query.datasetKey);
  const control: ReportControlEngineeringDto = kind === 'label'
    ? {
        key,
        kind,
        xMillimeters: 5,
        yMillimeters: 2,
        widthMillimeters: 60,
        heightMillimeters: 7,
        text: 'Label'
      }
    : {
        key,
        kind,
        xMillimeters: 5,
        yMillimeters: 2,
        widthMillimeters: 60,
        heightMillimeters: 7,
        queryKey: query?.key ?? DEFAULT_REPORT_QUERY_KEY,
        field
      };

  return updateReportSection(report, sectionKey, section => ({
    ...section,
    controls: [...(section.controls ?? []), control]
  }));
}

export function removeReportControl(
  report: ReportEngineeringDto,
  sectionKey: string,
  controlKey: string
): ReportEngineeringDto {
  return updateReportSection(report, sectionKey, section => ({
    ...section,
    controls: (section.controls ?? []).filter(control => control.key !== controlKey)
  }));
}

export function updatePrimaryQueryDataset(
  report: ReportEngineeringDto,
  datasetKey: 'historian.samples' | 'alarm.events'
): ReportEngineeringDto {
  const queries = report.queries ?? [];
  if (queries.length === 0) return report;
  const primary = queries[0];
  const nextPrimary: ReportQueryEngineeringDto = {
    ...primary,
    query: {
      ...primary.query,
      datasetKey,
      filters: [],
      search: null,
      orderBy: [{ field: 'timestamp', direction: 'descending' }]
    }
  };
  const defaultField = defaultFieldForDataset(datasetKey);
  return {
    ...report,
    queries: [nextPrimary, ...queries.slice(1)],
    sections: (report.sections ?? []).map(section => ({
      ...section,
      controls: (section.controls ?? []).map(control =>
        control.kind === 'dataField' && control.queryKey === primary.key
          ? { ...control, field: control.field === 'timestamp' ? 'timestamp' : defaultField }
          : control)
    }))
  };
}

export function updateRelativeDuration(report: ReportEngineeringDto, seconds: number): ReportEngineeringDto {
  const normalized = Math.max(1, Math.trunc(seconds));
  return {
    ...report,
    parameters: (report.parameters ?? []).map(parameter =>
      parameter.key === 'periodSeconds'
        ? { ...parameter, defaultValue: { type: 'durationSeconds', value: String(normalized) } }
        : parameter),
    queries: (report.queries ?? []).map((query, index) => index === 0
      ? {
          ...query,
          query: {
            ...query.query,
            timeRange: { kind: 'relative', durationSeconds: normalized, anchor: 'now' }
          }
        }
      : query)
  };
}

export function queryResult(
  result: ReportExecutionResult | null,
  queryKey: string | null | undefined
) {
  if (!result || !queryKey) return null;
  return result.queries.find(query => query.queryKey === queryKey) ?? null;
}

export function rowForSection(
  result: ReportExecutionResult | null,
  section: ReportSectionEngineeringDto,
  rowIndex: number
): HistoricalQueryRow | null {
  const query = queryResult(result, section.queryKey);
  return query?.rows[rowIndex] ?? null;
}

export function formatHistoricalValue(value: HistoricalQueryValue | undefined): string {
  if (!value || value.kind === 'null' || value.value === null) return '—';
  return value.value;
}

export function defaultFieldForDataset(datasetKey?: string): string {
  return datasetKey === 'alarm.events' ? 'message' : 'value';
}

function nextAvailableNumber(values: readonly string[], prefix: string): number {
  const used = new Set(values);
  let candidate = 1;
  while (used.has(`${prefix}${candidate}`)) candidate++;
  return candidate;
}
