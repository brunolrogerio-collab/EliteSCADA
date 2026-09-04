import React, { useMemo, useState } from 'react';
import type { VisualEditorObjectPaletteContractProps } from '../visualEditorContracts';
import {
  createObjectAddIntent,
  listVisualObjectPaletteItems,
  type VisualObjectPaletteItem
} from './objectPaletteModel';

export type ObjectPaletteCopy = Readonly<{
  title: string;
  hint: string;
  addLabel: string;
  labels: Readonly<Record<string, string>>;
}>;

export type ObjectPaletteProps = VisualEditorObjectPaletteContractProps & Readonly<{
  copy?: Partial<ObjectPaletteCopy>;
  parentObjectId?: string | null;
}>;

const DEFAULT_LABELS: Readonly<Record<string, string>> = Object.freeze({
  group: 'Group',
  rectangle: 'Rectangle',
  ellipse: 'Ellipse',
  line: 'Line',
  polygon: 'Polygon',
  text: 'Text',
  image: 'Image',
  valueDisplay: 'Value display',
  trend: 'Trend',
  alarmBrowser: 'Alarm Browser',
  eventBrowser: 'Event Browser',
  button: 'Button',
  slider: 'Slider'
});

export function ObjectPalette({
  onMutationIntent,
  copy,
  parentObjectId
}: ObjectPaletteProps) {
  const items = useMemo(() => listVisualObjectPaletteItems(), []);
  const [error, setError] = useState<string | null>(null);
  const labels = { ...DEFAULT_LABELS, ...(copy?.labels ?? {}) };
  const title = copy?.title ?? 'Object palette';
  const hint = copy?.hint ?? 'Add a registered visual object to the current Screen.';
  const addLabel = copy?.addLabel ?? 'Add';

  function add(item: VisualObjectPaletteItem) {
    setError(null);
    try {
      onMutationIntent(createObjectAddIntent(item.objectType, { parentObjectId }));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    }
  }

  return (
    <section className="visual-object-palette" aria-label={title} data-testid="visual-object-palette">
      <header>
        <strong>{title}</strong>
        <p>{hint}</p>
      </header>

      <div className="visual-object-palette__items" role="list">
        {items.map(item => {
          const label = labels[item.labelKey] ?? item.objectType;
          return (
            <button
              key={item.objectType}
              type="button"
              role="listitem"
              data-object-type={item.objectType}
              data-supports-asset-ref={item.supportsAssetReference ? 'true' : 'false'}
              onClick={() => add(item)}
              title={`${addLabel}: ${label}`}
            >
              <span aria-hidden="true">{paletteGlyph(item)}</span>
              <span>{label}</span>
              <small>{item.objectType}</small>
            </button>
          );
        })}
      </div>

      {error && <div role="alert">{error}</div>}
    </section>
  );
}

function paletteGlyph(item: VisualObjectPaletteItem): string {
  switch (item.labelKey) {
    case 'group': return '▣';
    case 'rectangle': return '▭';
    case 'ellipse': return '◯';
    case 'line': return '╱';
    case 'polygon': return '⬠';
    case 'text': return 'T';
    case 'image': return '▧';
    case 'valueDisplay': return '#';
    case 'trend': return '⌁';
    case 'alarmBrowser': return '⚠';
    case 'eventBrowser': return '≣';
    case 'button': return '▰';
    case 'slider': return '↔';
    default: return '□';
  }
}
