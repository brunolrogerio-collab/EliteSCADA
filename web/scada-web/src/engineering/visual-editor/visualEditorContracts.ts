import type {
  BindingEngineering,
  ScreenEngineering,
  TagValueReferenceEngineering,
  VisualAnalogFillEngineering,
  VisualBooleanConditionEngineering,
  VisualElementEngineering,
  VisualEngineeringPropertyValue,
  VisualPropertyExpressionEngineering
} from '../types';

/**
 * Shared Wave 08 integration contract between coordinator composition and visual
 * editor components. UI-only state is deliberately separated from canonical
 * mutation intents so viewport/selection/adornment state cannot become project data.
 */
export type VisualEditorPoint = Readonly<{
  x: number;
  y: number;
}>;

export type VisualEditorBounds = Readonly<{
  x: number;
  y: number;
  width: number;
  height: number;
}>;

export type VisualEditorViewport = Readonly<{
  zoom: number;
  panX: number;
  panY: number;
}>;

export type VisualEditorSelectionMode = 'replace' | 'add' | 'toggle';

export type VisualEditorUiIntent =
  | Readonly<{
      kind: 'selection.change';
      objectIds: readonly string[];
      mode: VisualEditorSelectionMode;
    }>
  | Readonly<{
      kind: 'viewport.change';
      viewport: VisualEditorViewport;
    }>;

export type VisualEditorZOrderOperation =
  | 'bringForward'
  | 'sendBackward'
  | 'bringToFront'
  | 'sendToBack';

export type VisualEditorMutationIntent =
  | Readonly<{
      kind: 'object.add';
      objectType: string;
      parentObjectId?: string | null;
      at?: VisualEditorPoint | null;
      initialProperties?: Readonly<Record<string, VisualEngineeringPropertyValue>>;
    }>
  | Readonly<{
      kind: 'object.move';
      objectIds: readonly string[];
      delta: VisualEditorPoint;
    }>
  | Readonly<{
      kind: 'object.resize';
      objectId: string;
      bounds: VisualEditorBounds;
    }>
  | Readonly<{
      kind: 'object.rotate';
      objectIds: readonly string[];
      deltaDegrees: number;
    }>
  | Readonly<{
      kind: 'object.duplicate';
      objectIds: readonly string[];
    }>
  | Readonly<{
      kind: 'object.delete';
      objectIds: readonly string[];
    }>
  | Readonly<{
      kind: 'object.zOrder';
      objectIds: readonly string[];
      operation: VisualEditorZOrderOperation;
    }>
  | Readonly<{
      kind: 'polygon.create';
      points: readonly VisualEditorPoint[];
    }>
  | Readonly<{
      kind: 'polygon.points.set';
      objectId: string;
      points: readonly VisualEditorPoint[];
    }>
  | Readonly<{
      kind: 'property.set';
      objectIds: readonly string[];
      propertyKey: string;
      value: VisualEngineeringPropertyValue;
    }>
  | Readonly<{
      kind: 'property.remove';
      objectIds: readonly string[];
      propertyKey: string;
    }>
  | Readonly<{
      kind: 'binding.set';
      objectId: string;
      binding: BindingEngineering;
    }>
  | Readonly<{
      kind: 'binding.remove';
      objectId: string;
      propertyKey: string;
    }>
  | Readonly<{
      kind: 'propertyExpression.set';
      objectId: string;
      configuration: VisualPropertyExpressionEngineering;
    }>
  | Readonly<{
      kind: 'propertyExpression.remove';
      objectId: string;
      propertyKey: string;
    }>
  | Readonly<{
      kind: 'booleanCondition.set';
      objectId: string;
      configuration: VisualBooleanConditionEngineering;
    }>
  | Readonly<{
      kind: 'booleanCondition.remove';
      objectId: string;
      propertyKey: string;
    }>
  | Readonly<{
      kind: 'analogFill.set';
      objectId: string;
      configuration: VisualAnalogFillEngineering;
    }>
  | Readonly<{
      kind: 'analogFill.remove';
      objectId: string;
    }>;

export type VisualEditorBindingSelectorCapability = Readonly<{
  kind: 'bit';
  minIndex: number;
  maxIndex: number;
}>;

export type VisualEditorBindingSourceCatalogItem = Readonly<{
  kind: BindingEngineering['kind'];
  target: string;
  label: string;
  dataType?: string | null;
  engineeringUnit?: string | null;
  writable?: boolean;
  family?: 'tag' | 'serverMemory' | 'clientMemory' | 'system' | 'driverDiagnostic' | 'asset';
  bindable?: boolean;
  tagReference?: TagValueReferenceEngineering | null;
  selectorCapability?: VisualEditorBindingSelectorCapability | null;
}>;

export type VisualEditorCanvasContractProps = Readonly<{
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  viewport: VisualEditorViewport;
  onUiIntent: (intent: VisualEditorUiIntent) => void;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  polygonToolActive?: boolean;
  onPolygonToolCancel?: () => void;
}>;

export type VisualEditorPropertyInspectorContractProps = Readonly<{
  selectedElements: readonly VisualElementEngineering[];
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}>;

export type VisualEditorObjectPaletteContractProps = Readonly<{
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}>;

export type VisualEditorBindingEditorContractProps = Readonly<{
  element: VisualElementEngineering;
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}>;
