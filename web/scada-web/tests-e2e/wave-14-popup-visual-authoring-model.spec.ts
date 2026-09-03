import { expect, test } from '@playwright/test';
import type { EngineeringPackageView, PopupEngineering } from '../src/engineering/types';
import {
  createPopupDraft,
  normalizePopupDimension,
  popupFrame,
  popupIdentity,
  popupToVisualScreen,
  replacePopupInPackage,
  visualScreenToPopup
} from '../src/engineering/visual-editor/popupVisualAuthoringModel';

function popup(): PopupEngineering {
  return {
    id: 'popup-id',
    key: 'popup.detail',
    name: 'Detail',
    width: 420,
    height: 260,
    version: 3,
    properties: { 'engineering.surface.backgroundColor': '#101820' },
    context: { area: 'A' },
    metadata: { owner: 'ops' },
    elements: [{ id: 'shape', key: 'shape', type: 'core.rectangle', properties: { x: 10, y: 20 } }]
  };
}

test('Popup visual adapter round-trips composition without inventing a Screen route', () => {
  const source = popup();
  const screen = popupToVisualScreen(source);
  expect(screen.route).toBeNull();
  expect(screen.key).toBe(source.key);
  expect(screen.elements).toEqual(source.elements);

  const restored = visualScreenToPopup({ ...screen, name: 'Edited' }, popupFrame(source));
  expect(restored.name).toBe('Edited');
  expect(restored.width).toBe(420);
  expect(restored.height).toBe(260);
  expect(restored.version).toBe(3);
  expect(restored.properties).toEqual(source.properties);
  expect(restored.context).toEqual(source.context);
  expect(restored.metadata).toEqual(source.metadata);
});

test('replacePopupInPackage changes only the selected canonical Popup identity', () => {
  const first = popup();
  const second: PopupEngineering = { ...popup(), id: 'other-id', key: 'popup.other', name: 'Other' };
  const model = { popups: [first, second] } as EngineeringPackageView;
  const edited = { ...first, name: 'Edited' };
  const candidate = replacePopupInPackage(model, first, edited);
  expect(candidate.popups?.map(item => item.name)).toEqual(['Edited', 'Other']);
  expect(popupIdentity(candidate.popups![1])).toBe('id:other-id');
});

test('new Popup defaults are unique and use a practical authoring frame', () => {
  const draft = createPopupDraft([{ ...popup(), key: 'popup-1' }], 'pt-BR');
  expect(draft.key).toBe('popup-2');
  expect(draft.width).toBe(480);
  expect(draft.height).toBe(320);
});

test('Popup dimensions reject non-positive and non-finite values', () => {
  expect(normalizePopupDimension(320.12345)).toBe(320.123);
  expect(() => normalizePopupDimension(0)).toThrow(/positive/);
  expect(() => normalizePopupDimension(Number.NaN)).toThrow(/positive/);
});
