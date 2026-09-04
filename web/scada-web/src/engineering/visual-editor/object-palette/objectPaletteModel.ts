import {
  BUILTIN_VISUAL_OBJECT_TYPES,
  getBuiltinVisualObjectSchema,
  type BuiltinVisualObjectType
} from '../../../visual-runtime/builtinVisualObjectSchemas';
import { VISUAL_PROPERTY_KEYS } from '../../../visual-runtime/visualPropertyRegistry';
import type {
  VisualEditorMutationIntent,
  VisualEditorPoint
} from '../visualEditorContracts';

export type VisualObjectPaletteCategory = 'structure' | 'shape' | 'content' | 'control';

export type VisualObjectPaletteItem = Readonly<{
  objectType: BuiltinVisualObjectType;
  labelKey: string;
  category: VisualObjectPaletteCategory;
  propertyKeys: readonly string[];
  supportsAssetReference: boolean;
}>;

export type CreateObjectAddIntentOptions = Readonly<{
  parentObjectId?: string | null;
  at?: VisualEditorPoint | null;
}>;

const PALETTE_ORDER: readonly BuiltinVisualObjectType[] = Object.freeze([
  BUILTIN_VISUAL_OBJECT_TYPES.group,
  BUILTIN_VISUAL_OBJECT_TYPES.rectangle,
  BUILTIN_VISUAL_OBJECT_TYPES.ellipse,
  BUILTIN_VISUAL_OBJECT_TYPES.line,
  BUILTIN_VISUAL_OBJECT_TYPES.polygon,
  BUILTIN_VISUAL_OBJECT_TYPES.text,
  BUILTIN_VISUAL_OBJECT_TYPES.image,
  BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay,
  BUILTIN_VISUAL_OBJECT_TYPES.trend,
  BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser,
  BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser,
  BUILTIN_VISUAL_OBJECT_TYPES.button,
  BUILTIN_VISUAL_OBJECT_TYPES.slider
]);

const PALETTE_METADATA: Readonly<Record<BuiltinVisualObjectType, Readonly<{
  labelKey: string;
  category: VisualObjectPaletteCategory;
}>>> = Object.freeze({
  [BUILTIN_VISUAL_OBJECT_TYPES.group]: { labelKey: 'group', category: 'structure' },
  [BUILTIN_VISUAL_OBJECT_TYPES.rectangle]: { labelKey: 'rectangle', category: 'shape' },
  [BUILTIN_VISUAL_OBJECT_TYPES.ellipse]: { labelKey: 'ellipse', category: 'shape' },
  [BUILTIN_VISUAL_OBJECT_TYPES.line]: { labelKey: 'line', category: 'shape' },
  [BUILTIN_VISUAL_OBJECT_TYPES.polygon]: { labelKey: 'polygon', category: 'shape' },
  [BUILTIN_VISUAL_OBJECT_TYPES.text]: { labelKey: 'text', category: 'content' },
  [BUILTIN_VISUAL_OBJECT_TYPES.image]: { labelKey: 'image', category: 'content' },
  [BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay]: { labelKey: 'valueDisplay', category: 'content' },
  [BUILTIN_VISUAL_OBJECT_TYPES.trend]: { labelKey: 'trend', category: 'content' },
  [BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser]: { labelKey: 'alarmBrowser', category: 'content' },
  [BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser]: { labelKey: 'eventBrowser', category: 'content' },
  [BUILTIN_VISUAL_OBJECT_TYPES.button]: { labelKey: 'button', category: 'control' },
  [BUILTIN_VISUAL_OBJECT_TYPES.slider]: { labelKey: 'slider', category: 'control' }
});

export function listVisualObjectPaletteItems(): readonly VisualObjectPaletteItem[] {
  return Object.freeze(PALETTE_ORDER.map(objectType => {
    const schema = getBuiltinVisualObjectSchema(objectType);
    const metadata = PALETTE_METADATA[objectType];
    return Object.freeze({
      objectType,
      labelKey: metadata.labelKey,
      category: metadata.category,
      propertyKeys: Object.freeze([...schema.propertyKeys]),
      supportsAssetReference: schema.declares(VISUAL_PROPERTY_KEYS.assetRef)
    });
  }));
}

export function createObjectAddIntent(
  objectType: string,
  options: CreateObjectAddIntentOptions = {}
): Extract<VisualEditorMutationIntent, { kind: 'object.add' }> {
  getBuiltinVisualObjectSchema(objectType);

  const parentObjectId = normalizeOptionalIdentity(options.parentObjectId, 'parentObjectId');
  const at = normalizeOptionalPoint(options.at);

  return Object.freeze({
    kind: 'object.add',
    objectType,
    ...(parentObjectId !== undefined ? { parentObjectId } : {}),
    ...(at !== undefined ? { at } : {})
  });
}

function normalizeOptionalIdentity(
  value: string | null | undefined,
  label: string
): string | null | undefined {
  if (value === undefined) return undefined;
  if (value === null) return null;
  if (!value.trim() || value !== value.trim() || /[\u0000-\u001F\u007F]/.test(value)) {
    throw new Error(`${label} must be a stable non-empty identity.`);
  }
  return value;
}

function normalizeOptionalPoint(
  value: VisualEditorPoint | null | undefined
): VisualEditorPoint | null | undefined {
  if (value === undefined) return undefined;
  if (value === null) return null;
  if (!Number.isFinite(value.x) || !Number.isFinite(value.y)) {
    throw new Error('Object placement coordinates must be finite.');
  }
  return Object.freeze({ x: value.x, y: value.y });
}
