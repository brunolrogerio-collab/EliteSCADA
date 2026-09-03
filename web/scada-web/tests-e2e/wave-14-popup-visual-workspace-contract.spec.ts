import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const appSource = readFileSync(join(process.cwd(), 'src/engineering/EngineeringApp.tsx'), 'utf8');
const workspaceSource = readFileSync(
  join(process.cwd(), 'src/engineering/visual-editor/PopupVisualEditorWorkspace.tsx'),
  'utf8'
);

test('Engineering Popups section routes to graphical authoring instead of read-only EntitySection', () => {
  expect(appSource).toContain("import { PopupVisualEditorWorkspace } from './visual-editor/PopupVisualEditorWorkspace';");
  expect(appSource).toContain("case 'popups': return <PopupVisualEditorWorkspace snapshot={snapshot} locale={locale} onApplied={onReload}/>");
  expect(appSource).not.toContain("case 'popups': return <EntitySection");
});

test('Popup authoring reuses the canonical visual Canvas, inspectors, Dynamo library and Preview Apply lifecycle', () => {
  expect(workspaceSource).toContain('<VisualEditorCanvas');
  expect(workspaceSource).toContain('<PropertyInspector');
  expect(workspaceSource).toContain('<DynamicPropertyEditor');
  expect(workspaceSource).toContain('<BindingEditor');
  expect(workspaceSource).toContain('<DynamoLibraryPalette');
  expect(workspaceSource).toContain('previewEngineeringPackage(nextPackage)');
  expect(workspaceSource).toContain('applyEngineeringPackage(candidate.package, candidate.changeVersion)');
});

test('Popup visual lifecycle reconstructs PopupEngineering before package replacement', () => {
  expect(workspaceSource).toContain('const draftPopup = visualScreenToPopup(draftScreen, frame);');
  expect(workspaceSource).toContain('replacePopupInPackage(snapshot.package, selected, draftPopup)');
});
