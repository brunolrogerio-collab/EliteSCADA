import React, { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { visualAssetContentUrl } from '../../api';
import type { ScreenEngineering } from '../../types';
import { useC07VisualEditorText } from '../c07VisualEditorI18n';
import { useDynamoAuthoringCatalog } from '../DynamoAuthoringCatalogContext';
import type { VisualEditorKeyboardCommand } from '../visualEditorKeyboardModel';
import {
  readVisualDefinitionSurfaceConfig,
  resolveVisualDefinitionSurfaceStyle,
  type VisualDefinitionBackgroundFit
} from '../visualDefinitionSurfaceModel';
import './VisualDefinitionSurfaceInspector.css';

const FIT_OPTIONS: readonly VisualDefinitionBackgroundFit[] = Object.freeze([
  'cover', 'contain', 'stretch', 'center', 'tile'
]);

export function VisualDefinitionSurfaceInspector({
  screen,
  onCommand
}: {
  screen: ScreenEngineering;
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
}) {
  const text = useC07VisualEditorText().surface;
  const hostRef = useRef<HTMLDetailsElement | null>(null);
  const [canvasSurface, setCanvasSurface] = useState<HTMLElement | null>(null);
  const catalog = useDynamoAuthoringCatalog();
  const config = useMemo(() => readVisualDefinitionSurfaceConfig(screen.properties), [screen.properties]);
  const [colorDraft, setColorDraft] = useState(config.backgroundColor ?? '');

  useEffect(() => setColorDraft(config.backgroundColor ?? ''), [config.backgroundColor]);
  useEffect(() => {
    const wrapper = hostRef.current?.closest('.visual-editor-canvas-enhanced');
    setCanvasSurface(wrapper?.querySelector<HTMLElement>('.visual-editor-canvas__surface') ?? null);
  }, []);

  const setSurface = (patch: Extract<VisualEditorKeyboardCommand, { kind: 'surface.set' }>['patch']) => {
    onCommand?.({ kind: 'surface.set', patch });
  };

  const commitColor = () => {
    const value = colorDraft.trim();
    setSurface({ backgroundColor: value || null });
  };

  const previewStyle = resolveVisualDefinitionSurfaceStyle(screen.properties, visualAssetContentUrl);
  const selectedAsset = catalog.visualAssets.find(asset => asset.id === config.backgroundImageAssetId) ?? null;

  return <>
    <details ref={hostRef} className="visual-editor-surface-inspector" data-testid="visual-definition-surface-inspector">
      <summary>
        <strong>{text.background}</strong>
        <small>{config.backgroundImageAssetId ? text.image : config.backgroundColor ? text.color : text.default}</small>
      </summary>
      <div className="visual-editor-surface-inspector__body">
        <div className="visual-editor-surface-inspector__preview" style={previewStyle} aria-hidden="true" />

        <label className="visual-editor-surface-inspector__field">
          <span>{text.colorLabel}</span>
          <div className="visual-editor-surface-inspector__color-row">
            <input
              type="color"
              value={colorPickerValue(config.backgroundColor)}
              disabled={!onCommand}
              onChange={event => {
                const value = event.currentTarget.value.toUpperCase();
                setColorDraft(value);
                setSurface({ backgroundColor: value });
              }}
            />
            <input
              type="text"
              value={colorDraft}
              placeholder="#FFFFFF / transparent"
              disabled={!onCommand}
              onChange={event => setColorDraft(event.currentTarget.value)}
              onBlur={commitColor}
              onKeyDown={event => {
                if (event.key === 'Enter') {
                  event.preventDefault();
                  commitColor();
                }
              }}
            />
            <button type="button" disabled={!onCommand || !config.backgroundColor} onClick={() => {
              setColorDraft('');
              setSurface({ backgroundColor: null });
            }}>{text.clear}</button>
          </div>
        </label>

        <label className="visual-editor-surface-inspector__field">
          <span>{text.imageAsset}</span>
          <select
            value={config.backgroundImageAssetId ?? ''}
            disabled={!onCommand}
            onChange={event => setSurface({ backgroundImageAssetId: event.currentTarget.value || null })}
          >
            <option value="">{text.noBackgroundImage}</option>
            {catalog.visualAssets
              .filter(asset => Boolean(asset.id?.trim()))
              .map(asset => <option key={asset.id!} value={asset.id!}>
                {asset.name} · {asset.pixelWidth ?? '?'}×{asset.pixelHeight ?? '?'}
              </option>)}
          </select>
        </label>

        <label className="visual-editor-surface-inspector__field">
          <span>{text.imageFit}</span>
          <select
            value={config.backgroundImageFit}
            disabled={!onCommand || !config.backgroundImageAssetId}
            onChange={event => setSurface({ backgroundImageFit: event.currentTarget.value as VisualDefinitionBackgroundFit })}
          >
            {FIT_OPTIONS.map(fit => <option key={fit} value={fit}>{text.fit[fit]}</option>)}
          </select>
        </label>

        <footer>
          <span>{selectedAsset ? `${selectedAsset.originalFileName} · ${selectedAsset.byteLength} bytes` : text.assetIdentityOnly}</span>
          <button
            type="button"
            disabled={!onCommand || (!config.backgroundColor && !config.backgroundImageAssetId)}
            onClick={() => {
              setColorDraft('');
              setSurface({ backgroundColor: null, backgroundImageAssetId: null, backgroundImageFit: null });
            }}
          >{text.resetBackground}</button>
        </footer>
      </div>
    </details>
    {canvasSurface ? createPortal(
      <div
        className="visual-editor-canvas__authored-background"
        data-testid="visual-editor-authored-background"
        style={previewStyle}
        aria-hidden="true"
      />,
      canvasSurface
    ) : null}
  </>;
}

function colorPickerValue(value: string | null): string {
  return value && /^#[0-9A-F]{6}$/i.test(value) ? value : '#FFFFFF';
}
