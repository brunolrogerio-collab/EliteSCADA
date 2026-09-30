import { expect, test } from '@playwright/test';
import { appShellText } from '../src/appShellI18n';
import { editorTranslator } from '../src/engineering/editorI18n';
import { productTerm, translator, type EngineeringLocale, type ProductGlossaryTerm } from '../src/engineering/i18n';
import { pythonEditorCopy } from '../src/engineering/python-editor/pythonEditorCopy';
import { scriptWorkspaceCopy } from '../src/engineering/scripts/ScriptEngineeringWorkspace.copy';
import { scriptAssistantCopy } from '../src/engineering/scripts/scriptAssistantCopy';

const locales: EngineeringLocale[] = ['pt-BR', 'en', 'es'];

test('W15 multilingual glossary matches the frozen pt-BR/en/es product terminology', () => {
  const expected: Record<ProductGlossaryTerm, [string, string, string]> = {
    engineering: ['Engenharia', 'Engineering', 'Ingeniería'],
    runtime: ['Runtime', 'Runtime', 'Runtime'],
    tag: ['TAG', 'TAG', 'TAG'],
    driver: ['Driver', 'Driver', 'Driver'],
    dataSource: ['Fonte de dados', 'Data Source', 'Fuente de datos'],
    historian: ['Historiador', 'Historian', 'Historiador'],
    script: ['Script', 'Script', 'Script'],
    popup: ['Popup', 'Popup', 'Popup'],
    workspace: ['Área de trabalho', 'Workspace', 'Área de trabajo'],
    preview: ['Pré-visualização', 'Preview', 'Vista previa'],
    screenEditor: ['Editor de Tela', 'Screen Editor', 'Editor de Pantalla'],
    popupEditor: ['Editor de Popup', 'Popup Editor', 'Editor de Popup'],
    editingArea: ['Área de edição', 'Editing area', 'Área de edición'],
    codeEditor: ['Editor de código', 'Code editor', 'Editor de código'],
    structure: ['Estrutura', 'Structure', 'Estructura']
  };

  for (const [term, forms] of Object.entries(expected) as Array<[ProductGlossaryTerm, [string, string, string]]>) {
    expect(locales.map(locale => productTerm(locale, term))).toEqual(forms);
  }
});

test('stable shell and structured editor copy uses locale-native glossary terms', () => {
  expect(appShellText('pt-BR').engineering).toBe('Engenharia');
  expect(appShellText('es').engineering).toBe('Ingeniería');

  const pt = translator('pt-BR');
  const es = translator('es');
  expect(pt('nav.dataSources')).toBe('Fontes de dados');
  expect(pt('workspace.status')).toBe('Área de trabalho');
  expect(pt('overview.lifecycleHint')).toBe('Em edição → Revisão → Publicado → Ativo');
  expect(es('nav.dataSources')).toBe('Fuentes de datos');
  expect(es('workspace.status')).toBe('Área de trabajo');
  expect(es('overview.lifecycleHint')).toBe('En edición → Revisión → Publicado → Activo');

  const ptEditor = editorTranslator('pt-BR');
  const esEditor = editorTranslator('es');
  expect(ptEditor('editor.field.source')).toBe('Fonte de dados');
  expect(ptEditor('editor.preview')).toBe('Validar pré-visualização');
  expect(esEditor('editor.field.source')).toBe('Fuente de datos');
  expect(esEditor('editor.preview')).toBe('Validar vista previa');
});

test('stable Script copy does not expose implementation brands or coordination jargon', () => {
  const forbidden = /Monaco|Pyodide|Web Worker|Wave 0?6|can[oô]nic|canónic/i;

  for (const locale of locales) {
    const visibleCopy = [
      pythonEditorCopy(locale),
      scriptWorkspaceCopy(locale),
      scriptAssistantCopy(locale)
    ];
    expect(JSON.stringify(visibleCopy)).not.toMatch(forbidden);
  }
});

test('locale-only translation lookup does not mutate stable identifiers', () => {
  const stableIdentity = Object.freeze({
    tagId: 'tag-demo-frequency',
    tagPath: 'Demo.P01.Frequency',
    dataSourceId: 'source-sim-01'
  });

  for (const locale of locales) {
    translator(locale)('nav.tags');
    translator(locale)('nav.dataSources');
    productTerm(locale, 'preview');
  }

  expect(stableIdentity).toEqual({
    tagId: 'tag-demo-frequency',
    tagPath: 'Demo.P01.Frequency',
    dataSourceId: 'source-sim-01'
  });
});
