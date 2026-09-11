import { expect, test } from '@playwright/test';
import { createTrendPen, VISUAL_PROPERTY_KEYS } from '../src/visual-runtime';
import {
  rebindTrendPenToTag,
  trendInspectorControlCopy,
  trendPropertyLabel,
  trendPropertyOptionLabel
} from '../src/engineering/visual-editor/trendAuthoringModel';

const pressureTag = {
  id: '11111111-1111-1111-1111-111111111111',
  path: 'Area/Pump/Pressure',
  name: 'Pressure',
  engineeringUnit: 'bar'
} as const;
const frequencyTag = {
  id: '22222222-2222-2222-2222-222222222222',
  path: 'Area/Pump/Frequency',
  name: 'Frequency',
  engineeringUnit: 'Hz'
} as const;

test('C15 TAG rebind follows catalog defaults instead of carrying stale label and unit', () => {
  const automatic = createTrendPen({
    id: pressureTag.id,
    path: pressureTag.path,
    label: pressureTag.name,
    unit: pressureTag.engineeringUnit
  });

  const rebound = rebindTrendPenToTag(automatic, pressureTag, frequencyTag);
  expect(rebound.id).toBe(automatic.id);
  expect(rebound.tagId).toBe(frequencyTag.id);
  expect(rebound.tagPath).toBe(frequencyTag.path);
  expect(rebound.label).toBe('Frequency');
  expect(rebound.unit).toBe('Hz');
});

test('C15 TAG rebind preserves explicit Pen label and unit overrides', () => {
  const automatic = createTrendPen({
    id: pressureTag.id,
    path: pressureTag.path,
    label: pressureTag.name,
    unit: pressureTag.engineeringUnit
  });
  const customized = Object.freeze({ ...automatic, label: 'Discharge pressure', unit: 'psi' });

  const rebound = rebindTrendPenToTag(customized, pressureTag, frequencyTag);
  expect(rebound.tagId).toBe(frequencyTag.id);
  expect(rebound.tagPath).toBe(frequencyTag.path);
  expect(rebound.label).toBe('Discharge pressure');
  expect(rebound.unit).toBe('psi');
});

test('C15 scalar property chrome is localized without changing canonical enum values', () => {
  expect(trendPropertyLabel('pt-BR', VISUAL_PROPERTY_KEYS.trendMode)).toBe('Modo do Trend');
  expect(trendPropertyLabel('en', VISUAL_PROPERTY_KEYS.trendMode)).toBe('Trend mode');
  expect(trendPropertyLabel('es', VISUAL_PROPERTY_KEYS.trendMode)).toBe('Modo del Trend');

  expect(trendPropertyOptionLabel('pt-BR', VISUAL_PROPERTY_KEYS.trendMode, 'history')).toBe('Histórico');
  expect(trendPropertyOptionLabel('pt-BR', VISUAL_PROPERTY_KEYS.trendMode, 'live')).toBe('Tempo real');
  expect(trendPropertyOptionLabel('en', VISUAL_PROPERTY_KEYS.trendMode, 'history')).toBe('History');
  expect(trendPropertyOptionLabel('en', VISUAL_PROPERTY_KEYS.trendMode, 'live')).toBe('Live');
  expect(trendPropertyOptionLabel('es', VISUAL_PROPERTY_KEYS.trendMode, 'history')).toBe('Histórico');
  expect(trendPropertyOptionLabel('es', VISUAL_PROPERTY_KEYS.trendMode, 'live')).toBe('En vivo');

  expect(trendInspectorControlCopy('pt-BR')).toMatchObject({
    useDefault: 'Usar padrão', trueLabel: 'Verdadeiro', falseLabel: 'Falso', defaultState: 'Padrão'
  });
  expect(trendInspectorControlCopy('en')).toMatchObject({
    useDefault: 'Use default', trueLabel: 'True', falseLabel: 'False', defaultState: 'Default'
  });
  expect(trendInspectorControlCopy('es')).toMatchObject({
    useDefault: 'Usar predeterminado', trueLabel: 'Verdadero', falseLabel: 'Falso', defaultState: 'Predeterminado'
  });

  expect(trendPropertyLabel('pt-BR', VISUAL_PROPERTY_KEYS.width)).toBeNull();
  expect(trendPropertyOptionLabel('es', VISUAL_PROPERTY_KEYS.strokeStyle, 'dashed')).toBe('dashed');
});
