import { expect, test } from '@playwright/test';
import type { EngineeringPackageView, PopupEngineering } from '../src/engineering/types';
import {
  createPopupDraft,
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
    templateKey: 'template.confirmation',
    properties: { 'engineering.surface.backgroundColor': '#101820' },
    context: { area: 'A' },
    metadata: { owner: 'ops' },
    elements: [{ id: 'shape', key: 'shape', type: 'core.rectangle', properties: { x: 10, y: 20 } }]
  };
}

test('Popup visual adapter round-trips canonical composition without inventing a Screen route', () => {
  const source = popup();
  const screen = popupToVisualScreen(source);
  expect(screen.route).toBeNull();
  expect(screen.key).toBe(source.key);
  expect(screen.elements).toEqual(source.elements);

  const restored = visualScreenToPopup({ ...screen, name: 'Edited' }, popupFrame(source));
  expect(restored.name).toBe('Edited');
  expect(restored.templateKey).toBe('template.confirmation');
  expect(restored.properties).toEqual(source.properties);
  expect(restored.context).toEqual(source.context);
  expect(restored.metadata).toEqual(source.metadata);
});

test('Popup adapter does not create frontend-only dimension or version fields', () => {
  const restored = visualScreenToPopup(popupToVisualScreen(popup()), popupFrame(popup()));
  expect('width' in restored).toBe(false);
  expect('height' in restored).toBe(false);
  expect('version' in restored).toBe(false);
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

test('new Popup defaults are unique and stay inside the canonical Popup DTO', () => {
  const draft = createPopupDraft([{ ...popup(), key: 'popup-1' }], 'pt-BR');
  expect(draft.key).toBe('popup-2');
  expect(draft.templateKey).toBeNull();
  expect(draft.elements).toEqual([]);
  expect('width' in draft).toBe(false);
  expect('height' in draft).toBe(false);
});
