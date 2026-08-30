import { expect, test } from '@playwright/test';
import type { EngineeringPackageView } from '../src/engineering/types';
import {
  addReportControl,
  createReportDraft,
  formatHistoricalValue,
  replaceReportInPackage,
  reportCollection,
  updatePrimaryQueryDataset,
  updateRelativeDuration,
  updateReportControl
} from '../src/engineering/reports/reportDesignerModel';

test.describe('Report Designer canonical contract', () => {
  test('creates a valid first-mile report shape with millimeter geometry and Historical Query v1', () => {
    const report = createReportDraft([]);

    expect(report.key).toBe('report-1');
    expect(report.page?.paperSizeKey).toBe('A4');
    expect(report.sections?.map(section => section.kind)).toEqual([
      'reportHeader',
      'detail',
      'reportFooter'
    ]);
    expect(report.sections?.every(section => section.heightMillimeters > 0)).toBe(true);
    expect(report.sections?.flatMap(section => section.controls ?? []).every(control =>
      Number.isFinite(control.xMillimeters) &&
      Number.isFinite(control.yMillimeters) &&
      control.widthMillimeters > 0 &&
      control.heightMillimeters > 0)).toBe(true);

    const query = report.queries?.[0];
    expect(query?.key).toBe('main');
    expect(query?.query.version).toBe(1);
    expect(query?.query.datasetKey).toBe('historian.samples');
    expect(query?.query.timeRange).toEqual({ kind: 'relative', durationSeconds: 3600, anchor: 'now' });
    expect(query?.query.page).toEqual({ limit: 50 });
    expect(query?.query.page?.cursor).toBeUndefined();
    expect(query?.parameterBindings).toEqual([
      { parameterKey: 'periodSeconds', target: 'relativeDurationSeconds' }
    ]);
  });

  test('edits canonical controls without mutating the source report', () => {
    const source = createReportDraft([]);
    const withLabel = addReportControl(source, 'report-header', 'label');
    const label = withLabel.sections?.find(section => section.key === 'report-header')?.controls?.at(-1);
    expect(label?.kind).toBe('label');
    expect(source.sections?.find(section => section.key === 'report-header')?.controls).toHaveLength(1);

    const moved = updateReportControl(withLabel, 'report-header', label!.key, control => ({
      ...control,
      xMillimeters: 23.5,
      yMillimeters: 4,
      text: 'Updated label'
    }));

    const updated = moved.sections?.find(section => section.key === 'report-header')?.controls?.find(control => control.key === label!.key);
    expect(updated?.xMillimeters).toBe(23.5);
    expect(updated?.yMillimeters).toBe(4);
    expect(updated?.text).toBe('Updated label');
    expect(label?.xMillimeters).toBe(5);
  });

  test('keeps report query semantics in Historical Query v1 when dataset and runtime duration change', () => {
    const source = createReportDraft([]);
    const alarm = updatePrimaryQueryDataset(source, 'alarm.events');
    const duration = updateRelativeDuration(alarm, 7200);

    expect(duration.queries?.[0].query.datasetKey).toBe('alarm.events');
    expect(duration.queries?.[0].query.timeRange).toEqual({ kind: 'relative', durationSeconds: 7200, anchor: 'now' });
    expect(duration.parameters?.find(parameter => parameter.key === 'periodSeconds')?.defaultValue).toEqual({
      type: 'durationSeconds',
      value: '7200'
    });
    const detailFields = duration.sections?.find(section => section.kind === 'detail')?.controls
      ?.filter(control => control.kind === 'dataField')
      .map(control => control.field);
    expect(detailFields).toEqual(['message', 'timestamp']);
  });

  test('replaces only the selected canonical report in the Engineering package', () => {
    const first = createReportDraft([]);
    const second = { ...createReportDraft([first]), name: 'Second' };
    const engineeringPackage = packageWithReports([first, second]);
    const draft = { ...first, name: 'Updated' };

    const next = replaceReportInPackage(engineeringPackage, first, draft);
    const nextReports = reportCollection(next);

    expect(nextReports).toHaveLength(2);
    expect(nextReports[0].name).toBe('Updated');
    expect(nextReports[1].name).toBe('Second');
    expect(reportCollection(engineeringPackage)[0].name).toBe(first.name);
  });

  test('renders Int64 Historical Query values as exact base-10 text', () => {
    expect(formatHistoricalValue({ kind: 'int64', value: '9223372036854775807' })).toBe('9223372036854775807');
    expect(formatHistoricalValue({ kind: 'null', value: null })).toBe('—');
  });
});

function packageWithReports(reports: unknown[]): EngineeringPackageView {
  return {
    schema: 'scada.engineering',
    schemaVersion: 14,
    exportedAt: new Date(0).toISOString(),
    tags: [],
    alarms: [],
    reports
  };
}
