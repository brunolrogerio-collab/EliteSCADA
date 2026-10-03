import React from 'react';
import type {
  EngineeringLocale,
} from '../i18n';
import type {
  VisualAssetEngineering,
  VisualElementEngineering
} from '../types';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import {
  SVG_PAINT_OVERRIDES_PROPERTY,
  normalizeSvgPaintOverrides,
  readSvgPaintMetadata,
  readSvgPaintOverrides,
  svgPaintOverridesAsEngineeringValue,
  type SvgPaintOverrides,
  type SvgPaintSlotOverride
} from './svgSymbolModel';

export function SvgPaintOverrideEditor({
  element,
  visualAssets,
  locale,
  onMutationIntent
}: {
  element: VisualElementEngineering;
  visualAssets: readonly VisualAssetEngineering[];
  locale: EngineeringLocale;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}) {
  if (!element.id) return null;
  const assetId = assetReferenceId(element.properties?.assetRef);
  const asset = assetId ? visualAssets.find(candidate => candidate.id === assetId) : undefined;
  const metadata = readSvgPaintMetadata(asset);
  const overrides = readSvgPaintOverrides(element);
  const text = copy(locale);

  const commit = (next: SvgPaintOverrides) => onMutationIntent({
    kind: 'property.set',
    objectIds: [element.id!],
    propertyKey: SVG_PAINT_OVERRIDES_PROPERTY,
    value: svgPaintOverridesAsEngineeringValue(normalizeSvgPaintOverrides(next))
  });

  if (!asset || asset.mediaType !== 'image/svg+xml') return null;
  if (metadata.palette.length === 0 && metadata.slots.length === 0) {
    return <section className="property-inspector__group property-inspector__svg-paint" data-testid="svg-paint-overrides">
      <header><strong>{text.title}</strong></header>
      <p>{text.noPaint}</p>
    </section>;
  }

  return <section className="property-inspector__group property-inspector__svg-paint" data-testid="svg-paint-overrides">
    <header>
      <strong>{text.title}</strong>
      <small>{text.hint}</small>
    </header>

    {metadata.palette.length > 0 ? <div className="property-inspector__group-fields">
      <strong>{text.palette}</strong>
      {metadata.palette.map(entry => {
        const target = overrides.palette?.[entry.color] ?? entry.color;
        return <label key={entry.color} className="property-inspector__field">
          <span>{entry.color} · {[entry.fill ? 'fill' : '', entry.stroke ? 'stroke' : ''].filter(Boolean).join(' / ')}</span>
          <input
            type="color"
            value={colorPickerValue(target)}
            aria-label={`${text.palette} ${entry.color}`}
            onChange={event => commit({
              ...overrides,
              version: 1,
              palette: { ...(overrides.palette ?? {}), [entry.color]: event.currentTarget.value.toUpperCase() }
            })}
          />
        </label>;
      })}
    </div> : null}

    {metadata.slots.length > 0 ? <div className="property-inspector__group-fields">
      <strong>{text.slots}</strong>
      {metadata.slots.map(slot => {
        const current = overrides.slots?.[slot.name] ?? {};
        const updateSlot = (patch: SvgPaintSlotOverride) => commit({
          ...overrides,
          version: 1,
          slots: {
            ...(overrides.slots ?? {}),
            [slot.name]: { ...current, ...patch }
          }
        });
        return <fieldset key={slot.name} className="property-inspector__svg-slot">
          <legend>{slot.name}</legend>
          {slot.fill ? <label>
            <span>{text.fill}</span>
            <input type="color" value={colorPickerValue(current.fill ?? '#000000')} onChange={event => updateSlot({ fill: event.currentTarget.value.toUpperCase() })} />
          </label> : null}
          {slot.stroke ? <label>
            <span>{text.stroke}</span>
            <input type="color" value={colorPickerValue(current.stroke ?? '#000000')} onChange={event => updateSlot({ stroke: event.currentTarget.value.toUpperCase() })} />
          </label> : null}
          {slot.strokeWidth ? <label>
            <span>{text.strokeWidth}</span>
            <input
              type="number"
              min={0}
              max={10000}
              step={0.1}
              value={current.strokeWidth ?? ''}
              placeholder={text.source}
              onChange={event => {
                const value = Number(event.currentTarget.value);
                if (Number.isFinite(value) && value >= 0) updateSlot({ strokeWidth: value });
              }}
            />
          </label> : null}
        </fieldset>;
      })}
    </div> : null}

    <button
      type="button"
      className="secondary"
      onClick={() => commit({ version: 1 })}
    >{text.reset}</button>
  </section>;
}

function assetReferenceId(value: unknown): string | null {
  if (typeof value === 'object' && value !== null && !Array.isArray(value) && 'assetId' in value) {
    const id = (value as { assetId?: unknown }).assetId;
    return typeof id === 'string' && id.trim() ? id.trim() : null;
  }
  if (typeof value === 'string') {
    const normalized = value.trim();
    return normalized.startsWith('asset:') ? normalized.slice(6) : normalized || null;
  }
  return null;
}

function colorPickerValue(value: string): string {
  const match = /^#([0-9A-F]{6})/i.exec(value);
  return match ? `#${match[1]}` : '#000000';
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'SVG paint overrides', hint: 'Per-instance; source asset stays unchanged.',
    palette: 'Detected palette', slots: 'Semantic slots', fill: 'Fill', stroke: 'Stroke',
    strokeWidth: 'Stroke width', source: 'Source', reset: 'Reset SVG paint overrides',
    noPaint: 'This SVG has no editable static paint metadata. Global Fill, Stroke and Stroke width remain available above.'
  };
  if (locale === 'es') return {
    title: 'Overrides de pintura SVG', hint: 'Por instancia; el asset fuente no cambia.',
    palette: 'Paleta detectada', slots: 'Slots semánticos', fill: 'Relleno', stroke: 'Trazo',
    strokeWidth: 'Espesor de línea', source: 'Fuente', reset: 'Restablecer pintura SVG',
    noPaint: 'Este SVG no tiene metadatos de pintura estática editables. Relleno, Trazo y Espesor global siguen disponibles arriba.'
  };
  return {
    title: 'Overrides de pintura SVG', hint: 'Por instância; o asset-fonte permanece intacto.',
    palette: 'Paleta detectada', slots: 'Slots semânticos', fill: 'Preenchimento', stroke: 'Linha',
    strokeWidth: 'Espessura da linha', source: 'Origem', reset: 'Resetar pintura SVG',
    noPaint: 'Este SVG não possui metadados de pintura estática editáveis. Preenchimento, Linha e Espessura globais continuam disponíveis acima.'
  };
}
