import { expect, test } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const workspaceSource = fs.readFileSync(path.join(root, 'src/engineering/reports/ReportDesignerWorkspace.tsx'), 'utf8');
const apiSource = fs.readFileSync(path.join(root, 'src/engineering/reports/reportApi.ts'), 'utf8');
const modelSource = fs.readFileSync(path.join(root, 'src/engineering/reports/reportDesignerModel.ts'), 'utf8');

test('Report Designer persists only through Engineering Preview/Apply authority', () => {
  expect(workspaceSource).toContain('previewEngineeringPackage(nextPackage)');
  expect(workspaceSource).toContain('applyEngineeringPackage(candidate.package, candidate.changeVersion)');
  expect(workspaceSource).toContain('loadEngineeringWorkspace()');
  expect(workspaceSource).not.toContain('localStorage.setItem');
  expect(modelSource).toContain('replaceReportInPackage');
});

test('Report Preview uses the canonical server execution seam and never a browser SQL path', () => {
  expect(apiSource).toContain('/api/reports/preview');
  expect(workspaceSource).toContain('previewReportExecution({ report: draft, parameters }');
  expect(workspaceSource).toContain("datasetKey: 'historian.samples'");
  expect(`${workspaceSource}\n${modelSource}\n${apiSource}`.toLowerCase()).not.toContain('select *');
  expect(`${workspaceSource}\n${modelSource}\n${apiSource}`.toLowerCase()).not.toContain('from elitescada');
});

test('Report layout keeps canonical millimeters while pixel scaling remains presentation-only', () => {
  expect(workspaceSource).toContain('data-unit="millimeter"');
  expect(workspaceSource).toContain('control.xMillimeters * MILLIMETER_SCALE');
  expect(workspaceSource).toContain('control.yMillimeters * MILLIMETER_SCALE');
  expect(modelSource).toContain('xMillimeters');
  expect(modelSource).toContain('heightMillimeters');
});

test('image presentation resolves only canonical Visual Asset IDs', () => {
  expect(workspaceSource).toContain('visualAssetContentUrl(control.assetId)');
  expect(workspaceSource).not.toContain('imageUrl');
  expect(workspaceSource).not.toContain('backgroundImageUrl');
});
