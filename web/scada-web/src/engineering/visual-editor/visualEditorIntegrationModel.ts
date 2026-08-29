import type { ScreenEngineering, TagEngineering, VisualElementEngineering } from '../types';
import type {
  VisualEditorBindingSourceCatalogItem,
  VisualEditorMutationIntent,
  VisualEditorUiIntent,
  VisualEditorViewport
} from './visualEditorContracts';

export function applyVisualEditorSelectionIntent(
  current: readonly string[],
  intent: Extract<VisualEditorUiIntent, { kind: 'selection.change' }>
): readonly string[] {
  const normalizedCurrent = uniqueStableIds(current);
  const requested = uniqueStableIds(intent.objectIds);

  if (intent.mode === 'replace') return requested;

  const next = [...normalizedCurrent];
  for (const objectId of requested) {
    const index = next.indexOf(objectId);
    if (intent.mode === 'add') {
      if (index < 0) next.push(objectId);
      continue;
    }
    if (index >= 0) next.splice(index, 1);
    else next.push(objectId);
  }
  return Object.freeze(next);
}

export function normalizeVisualEditorViewport(viewport: VisualEditorViewport): VisualEditorViewport {
  const zoom = Number.isFinite(viewport.zoom) ? Math.min(4, Math.max(0.1, viewport.zoom)) : 1;
  return Object.freeze({
    zoom,
    panX: Number.isFinite(viewport.panX) ? viewport.panX : 0,
    panY: Number.isFinite(viewport.panY) ? viewport.panY : 0
  });
}

export function selectedVisualElements(
  screen: ScreenEngineering,
  objectIds: readonly string[]
): readonly VisualElementEngineering[] {
  const requested = new Set(uniqueStableIds(objectIds));
  if (requested.size === 0) return Object.freeze([]);

  const selected: VisualElementEngineering[] = [];
  const visit = (element: VisualElementEngineering): void => {
    const objectId = element.id?.trim();
    if (objectId && requested.has(objectId)) selected.push(element);
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of screen.elements ?? []) visit(element);
  return Object.freeze(selected);
}

export function existingVisualObjectIds(screen: ScreenEngineering): ReadonlySet<string> {
  const ids = new Set<string>();
  const visit = (element: VisualElementEngineering): void => {
    const objectId = element.id?.trim();
    if (objectId) ids.add(objectId);
    for (const child of element.children ?? []) visit(child);
  };
  for (const element of screen.elements ?? []) visit(element);
  return ids;
}

/**
 * DEV 3 deliberately emits its worker-local authoring labels (Tag/Property/Expression).
 * Canonical Engineering and the existing Runtime adapter use lowercase kinds.
 * Normalize only at the coordinator authority boundary so persisted bindings and
 * Runtime projection cannot diverge by casing.
 */
export function normalizeVisualEditorMutationIntent(
  intent: VisualEditorMutationIntent
): VisualEditorMutationIntent {
  if (intent.kind !== 'binding.set') return intent;

  const normalizedKind = intent.binding.kind.trim().toLowerCase();
  if (!['tag', 'property', 'binding', 'expression'].includes(normalizedKind)) {
    throw new Error(`Visual binding kind '${intent.binding.kind}' is not supported by canonical Runtime projection.`);
  }

  return Object.freeze({
    ...intent,
    binding: Object.freeze({
      ...intent.binding,
      kind: normalizedKind
    })
  });
}

/**
 * Current Wave 08 exposes only canonical TAG sources. Property/expression source
 * authoring is intentionally held back until the later typed-expression contract
 * is implemented, even though DEV 3's generic editor can represent those kinds.
 */
export function buildVisualEditorTagSourceCatalog(
  tags: readonly TagEngineering[]
): readonly VisualEditorBindingSourceCatalogItem[] {
  const items = tags
    .filter(tag => Boolean(tag.path?.trim()))
    .map(tag => Object.freeze({
      kind: 'Tag',
      target: tag.path.trim(),
      label: tag.name?.trim() ? `${tag.name} · ${tag.path}` : tag.path,
      dataType: tag.dataType,
      engineeringUnit: tag.engineeringUnit ?? null,
      writable: !tag.readOnly
    } satisfies VisualEditorBindingSourceCatalogItem));

  return Object.freeze(items);
}

function uniqueStableIds(values: readonly string[]): readonly string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const value of values) {
    const normalized = value.trim();
    if (!normalized || seen.has(normalized)) continue;
    seen.add(normalized);
    result.push(normalized);
  }
  return Object.freeze(result);
}
